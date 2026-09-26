using System;
using System.Collections.Generic;
using GoLive.Desktop;
using GoLive.PcBuilding;

namespace GoLive.Viewers
{
    // The current broadcast's living chat in one explicit flow:
    //   stream facts -> StreamEventSource (normalized, keyed events) -> ReactionSelector (who reacts, when, or silence)
    //   -> ChatDirector (local model or fallback text, validated) -> StreamChat (what the audience sees).
    // Coordinates transient broadcast state and the durable community; audience, money and follows stay with their owners.
    // Owns the language model it is given and disposes it.
    public sealed class ViewerCore : IDisposable
    {
        private const ulong SelectionSalt = 0x5649455745525321UL;
        private const ulong FallbackSalt = 0x46414C4C4241434BUL;
        private const double RecentSpeechSeconds = 90;
        private readonly StreamSession _stream;
        private readonly StreamSpeechFeed _speech;
        private readonly TrichChannel _channel;
        private readonly ReactionTuning _tuning;
        private readonly List<StreamEvent> _events = new();
        private readonly List<string> _speechLanguages = new();
        private ReactionSelector _selector;
        private string _broadcast = "";
        private string _recentSpeech;
        private double _recentSpeechAt;
        private StreamTopic _recentTopics;
        private double _gameMinutes;
        private readonly Dictionary<string, EventWitnesses> _chatWitnesses = new(StringComparer.Ordinal);
        private readonly Queue<string> _chatWitnessOrder = new();

        public AudienceRoster Roster { get; }
        public ViewerCommunity Community { get; }
        public StreamEventSource Events { get; }
        public StreamChat Chat { get; } = new();
        public ChatDirector Director { get; }
        public ReactionLog Log { get; } = new();
        public ReactionSelector Selector => _selector;
        // The language the streamer actually speaks (recognized speech), Russian until known.
        public ViewerLanguage ChannelLanguage { get; private set; } = ViewerLanguage.Russian;

        public ViewerCore(StreamSession stream, StreamSpeechFeed speech, DonationAccount donations, PcPeripherals peripherals,
            TrichChannel channel, ReactionTuning tuning, IViewerLanguageModel languageModel = null, ChatModelSettings modelSettings = null,
            IReadOnlyList<ViewerProfile> profiles = null)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _speech = speech ?? throw new ArgumentNullException(nameof(speech));
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
            _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
            string error = tuning.Validate();
            if (error != null) throw new ArgumentException(error, nameof(tuning));
            Roster = new AudienceRoster((random, index) => EphemeralViewers.Create(random, index, ChannelLanguage));
            Community = new ViewerCommunity(profiles, Roster);
            Events = new StreamEventSource(stream, speech, donations, peripherals, channel, Roster, tuning, () => Community.KnownNames);
            Community.Joined += Events.NotifyJoined;
            Director = new ChatDirector(languageModel, modelSettings ?? new ChatModelSettings { Enabled = false }, Chat, Log);
            Director.Finished += FinishMemoryReference;
            Director.Shown += ObservePublished;
            Chat.Added += ObserveChat;
            Chat.Cleared += ClearChatWitnesses;
            _speech.SpeechAdded += RememberSpeech;
            _stream.Changed += ObserveLifecycle;
        }

        public void Tick(StreamerContext context)
        {
            _gameMinutes = context.GameMinutes;
            Events.UpdateClock(context.GameMinutes);
            Community.UpdateContext(context.GameMinutes, context.Content);
            if (_stream.State != StreamState.Live)
            {
                Events.Tick(context);
                _stream.SetStreamerActivity(StreamerActivity.Active);
                EndBroadcast();
                return;
            }
            if (_broadcast != _stream.BroadcastId) BeginBroadcast(context);
            double now = Events.Now;
            Roster.SetAudienceSize(_stream.Audience.CurrentViewers);
            StreamTopic topics = context.Content;
            if (_recentSpeech != null && now - _recentSpeechAt <= RecentSpeechSeconds) topics |= _recentTopics;
            Community.UpdateContext(context.GameMinutes, topics);
            Community.Tick(now);
            Events.Tick(context);
            _stream.SetStreamerActivity(Events.Activity);

            ProcessPendingEvents(now);
            Director.Update(now, Roster, GenerationSituationFor, _broadcast);
        }

        // Paid orders/installs can arrive between frames after speech was normalized. Drain those earlier
        // events first so a later completed gameplay fact sees its promise. Generation still starts on Tick.
        public void ObserveGameplay(PromiseGameplayFact fact)
        {
            if (fact == null) throw new ArgumentNullException(nameof(fact));
            if (_stream.State == StreamState.Live)
            {
                if (_broadcast != _stream.BroadcastId) BeginBroadcast(new StreamerContext(false, false, fact.GameMinutes));
                ProcessPendingEvents(Events.Now);
            }
            Community.ObserveGameplay(fact);
        }

        private void ProcessPendingEvents(double now)
        {
            Events.Drain(_events);
            foreach (StreamEvent streamEvent in _events)
            {
                Community.Process(streamEvent);
                List<ReactionIntent> intents = _selector.Select(streamEvent, now, true, out string reason);
                if (intents.Count == 0)
                {
                    // Ambient impulses that fall to the budget are too frequent to trace one by one.
                    if (streamEvent.Kind != StreamEventKind.AudienceChatter) Log.Add(ReactionLog.ForEvent(streamEvent, ReactionOutcome.Rejected, reason));
                    continue;
                }
                foreach (ReactionIntent intent in intents)
                {
                    Log.Add(ReactionLog.ForIntent(intent, ReactionOutcome.Scheduled, intent.Direct ? "direct" : "chat"));
                    Director.Submit(intent);
                }
            }
            _events.Clear();
        }

        // What the chat can see right now: bounded, current-broadcast only.
        public ChatSituation Situation()
        {
            double now = Events.Now;
            string recent = _recentSpeech != null && now - _recentSpeechAt <= RecentSpeechSeconds ? _recentSpeech : null;
            return new ChatSituation(_channel.Name, now, Roster.AudienceSize, ChannelLanguage, Chat.Messages, recent);
        }

        public ChatSituation SituationFor(ReactionIntent intent)
            => SituationFor(intent, false);

        private ChatSituation GenerationSituationFor(ReactionIntent intent) => SituationFor(intent, true);

        private ChatSituation SituationFor(ReactionIntent intent, bool reserve)
        {
            ChatSituation current = Situation();
            string id = intent.Viewer.ViewerId;
            long epoch = Roster.Epoch(id);
            bool currentVisit = Roster.IsWatching(id) && epoch == intent.PresenceEpoch;
            // New anonymous identities only represent a seat witnessing their selected fact. They receive
            // no earlier speech/chat. Every established identity needs an explicit matching witness epoch.
            bool eventOnly = intent.Event.HasWitnesses && !intent.Event.WitnessedBy(id, epoch);
            var chat = new List<StreamChatMessage>();
            if (currentVisit && !eventOnly)
                foreach (var line in current.RecentChat)
                    if (_chatWitnesses.TryGetValue(line.Id, out var witnesses) && witnesses.Contains(id, epoch)) chat.Add(line);
            string speech = currentVisit && !eventOnly && Events.LatestSpeech?.WitnessedBy(id, epoch) == true ? current.RecentSpeech : null;
            ViewerMemoryBank bank = currentVisit && !eventOnly ? Community.State(id)?.Memories : null;
            IReadOnlyList<ViewerMemory> memories = bank == null ? Array.Empty<ViewerMemory>() : reserve
                ? bank.Reserve(intent.Event, _gameMinutes, intent.Id) : bank.Retrieve(intent.Event, _gameMinutes);
            string subject = ViewerPromiseVocabulary.Subject(intent.Event) ?? ViewerPromiseVocabulary.Subject(speech);
            ViewerPromiseContext promise = bank == null || subject == null ? null : reserve
                ? Community.Promises.Reserve(id, subject, _gameMinutes, intent.Id) : Community.Promises.Retrieve(id, subject, _gameMinutes);
            return new ChatSituation(current.ChannelName, current.StreamSeconds, current.Viewers, current.ChannelLanguage,
                chat.AsReadOnly(), speech, Community.RelationshipContext(id), memories, promise);
        }

        private void ObserveChat(StreamChatMessage message)
        {
            Roster.SetAudienceSize(_stream.Audience.CurrentViewers);
            _chatWitnesses[message.Id] = EventWitnesses.Capture(Roster);
            _chatWitnessOrder.Enqueue(message.Id);
            while (_chatWitnessOrder.Count > StreamChat.Capacity) _chatWitnesses.Remove(_chatWitnessOrder.Dequeue());
        }

        private void ClearChatWitnesses() { _chatWitnesses.Clear(); _chatWitnessOrder.Clear(); }

        private void FinishMemoryReference(ReactionIntent intent, ChatSituation situation, string publishedText)
        {
            Community.State(intent.Viewer.ViewerId)?.Memories.Finish(intent.Id, situation?.Memories, publishedText, _gameMinutes);
            Community.Promises.Finish(intent.Viewer.ViewerId, intent.Id, situation?.Promise, publishedText, _gameMinutes);
        }

        private void ObservePublished(StreamChatMessage message, ReactionIntent origin)
        {
            Community.ObservePublished(message);
            ReactionIntent reply = _selector?.SelectPublished(message, origin, Events.Now, _gameMinutes);
            if (reply == null) return;
            Log.Add(ReactionLog.ForIntent(reply, ReactionOutcome.Scheduled, "social reply"));
            Director.Submit(reply);
        }

        // Load-time discard: nothing of the replaced world's broadcast (chat, pending reactions) survives.
        public void Discard()
        {
            EndBroadcast();
            Chat.Clear();
        }

        public void Dispose()
        {
            _stream.Changed -= ObserveLifecycle;
            _speech.SpeechAdded -= RememberSpeech;
            Community.Joined -= Events.NotifyJoined;
            EndBroadcast();
            Director.Dispose();
            Director.Finished -= FinishMemoryReference;
            Director.Shown -= ObservePublished;
            Chat.Added -= ObserveChat;
            Chat.Cleared -= ClearChatWitnesses;
            Events.Dispose();
        }

        private void RememberSpeech(StreamSpeechEvent entry)
        {
            _recentSpeech = entry.Speech.Text;
            _recentSpeechAt = entry.StreamSeconds;
            _recentTopics = SpeechRelevance.Analyze(entry.Speech, Array.Empty<ViewerNameForms>()).Topics;
            if (entry.Speech.Language != "ru" && entry.Speech.Language != "en") return;
            if (_speechLanguages.Count == 5) _speechLanguages.RemoveAt(0);
            _speechLanguages.Add(entry.Speech.Language);
            int english = 0;
            foreach (string language in _speechLanguages)
                if (language == "en") english++;
            ChannelLanguage = english * 2 > _speechLanguages.Count ? ViewerLanguage.English : ViewerLanguage.Russian;
        }

        private void BeginBroadcast(StreamerContext context)
        {
            _broadcast = _stream.BroadcastId;
            Roster.Clear();
            Roster.SetAudienceSize(_stream.Audience.CurrentViewers);
            Chat.Clear();
            _recentSpeech = null;
            _recentTopics = StreamTopic.None;
            _selector = new ReactionSelector(_tuning, Roster, new AudienceRandom(AudienceRandom.Hash(_stream.Audience.Seed, SelectionSalt)));
            Director.BeginBroadcast(new AudienceRandom(AudienceRandom.Hash(_stream.Audience.Seed, FallbackSalt)));
            Community.BeginBroadcast(_broadcast, _stream.Audience.Seed, context.GameMinutes, context.Content);
        }

        private void ObserveLifecycle()
        {
            if (_stream.State != StreamState.Live) EndBroadcast();
            else Roster.SetAudienceSize(_stream.Audience.CurrentViewers);
        }

        private void EndBroadcast()
        {
            if (_broadcast.Length == 0) return;
            Director.CancelAll("broadcast ended");
            _broadcast = "";
            Community.EndBroadcast();
        }
    }
}
