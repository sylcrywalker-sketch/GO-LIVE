using System;
using System.Collections.Generic;
using GoLive.Desktop;
using GoLive.PcBuilding;

namespace GoLive.Viewers
{
    // The current broadcast's living chat in one explicit flow:
    //   stream facts -> StreamEventSource (normalized, keyed events) -> ReactionSelector (who reacts, when, or silence)
    //   -> ChatDirector (local model or fallback text, validated) -> StreamChat (what the audience sees).
    // Owns only transient per-broadcast state; gameplay truth (audience, money, follows) stays with its owners.
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

        public AudienceRoster Roster { get; }
        public StreamEventSource Events { get; }
        public StreamChat Chat { get; } = new();
        public ChatDirector Director { get; }
        public ReactionLog Log { get; } = new();
        public ReactionSelector Selector => _selector;
        // The language the streamer actually speaks (recognized speech), Russian until known.
        public ViewerLanguage ChannelLanguage { get; private set; } = ViewerLanguage.Russian;

        public ViewerCore(StreamSession stream, StreamSpeechFeed speech, DonationAccount donations, PcPeripherals peripherals,
            TrichChannel channel, ReactionTuning tuning, IViewerLanguageModel languageModel = null, ChatModelSettings modelSettings = null)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _speech = speech ?? throw new ArgumentNullException(nameof(speech));
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
            _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
            string error = tuning.Validate();
            if (error != null) throw new ArgumentException(error, nameof(tuning));
            Roster = new AudienceRoster((random, index) => EphemeralViewers.Create(random, index, ChannelLanguage));
            Events = new StreamEventSource(stream, speech, donations, peripherals, channel, Roster, tuning);
            Director = new ChatDirector(languageModel, modelSettings ?? new ChatModelSettings { Enabled = false }, Chat, Log);
            _speech.SpeechAdded += RememberSpeech;
        }

        public void Tick(StreamerContext context)
        {
            Events.Tick(context);
            _stream.SetStreamerActivity(Events.IsLive ? Events.Activity : StreamerActivity.Active);
            if (!Events.IsLive)
            {
                EndBroadcast();
                return;
            }
            if (_broadcast != Events.BroadcastId) BeginBroadcast();
            double now = Events.Now;

            Events.Drain(_events);
            foreach (StreamEvent streamEvent in _events)
            {
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
            Director.Update(now, Roster, Situation, _broadcast);
        }

        // What the chat can see right now: bounded, current-broadcast only.
        public ChatSituation Situation()
        {
            double now = Events.Now;
            string recent = _recentSpeech != null && now - _recentSpeechAt <= RecentSpeechSeconds ? _recentSpeech : null;
            return new ChatSituation(_channel.Name, now, Roster.AudienceSize, ChannelLanguage, Chat.Messages, recent);
        }

        // Load-time discard: nothing of the replaced world's broadcast (chat, pending reactions) survives.
        public void Discard()
        {
            EndBroadcast();
            Chat.Clear();
        }

        public void Dispose()
        {
            _speech.SpeechAdded -= RememberSpeech;
            Director.Dispose();
            Events.Dispose();
        }

        private void RememberSpeech(StreamSpeechEvent entry)
        {
            _recentSpeech = entry.Speech.Text;
            _recentSpeechAt = entry.StreamSeconds;
            if (entry.Speech.Language != "ru" && entry.Speech.Language != "en") return;
            if (_speechLanguages.Count == 5) _speechLanguages.RemoveAt(0);
            _speechLanguages.Add(entry.Speech.Language);
            int english = 0;
            foreach (string language in _speechLanguages)
                if (language == "en") english++;
            ChannelLanguage = english * 2 > _speechLanguages.Count ? ViewerLanguage.English : ViewerLanguage.Russian;
        }

        private void BeginBroadcast()
        {
            _broadcast = Events.BroadcastId;
            Roster.Clear();
            Roster.SetAudienceSize(_stream.Audience.CurrentViewers);
            Chat.Clear();
            _recentSpeech = null;
            _selector = new ReactionSelector(_tuning, Roster, new AudienceRandom(AudienceRandom.Hash(_stream.Audience.Seed, SelectionSalt)));
            Director.BeginBroadcast(new AudienceRandom(AudienceRandom.Hash(_stream.Audience.Seed, FallbackSalt)));
        }

        private void EndBroadcast()
        {
            if (_broadcast.Length == 0) return;
            Director.CancelAll("broadcast ended");
            _broadcast = "";
            Roster.Clear();
        }
    }
}
