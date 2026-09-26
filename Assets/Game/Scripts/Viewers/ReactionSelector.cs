using System;
using System.Collections.Generic;
using System.Linq;
using GoLive.Desktop;

namespace GoLive.Viewers
{
    // A C#-approved decision that one watching viewer will write about one event, at a human delay. The language
    // model only phrases it later; if the moment passes (ExpiresSeconds) the reaction is dropped, never shown late.
    public sealed class ReactionIntent
    {
        public long Id { get; }
        public StreamEvent Event { get; }
        public ChatParticipant Viewer { get; }
        // The streamer addressed this viewer (said their name, thanked their donation) or the fact is about them.
        public bool Direct { get; }
        // 0 for the first viewer reacting to the event, 1 for the second, ...
        public int Order { get; }
        public double DueSeconds { get; }
        public double ExpiresSeconds { get; }
        public long PresenceEpoch { get; }
        public string CandidateIds { get; internal set; }
        // The relationship tier that shaped this selection (eligibility, delay); planning re-reads current state.
        public RelationshipTier Tier { get; internal set; }
        // An answer to the streamer talking TO the chat (a question, request or greeting): it opens or continues a thread.
        public bool Conversational { get; internal set; }
        // The streamer's phrase continues this viewer's exchange with them (the viewer's last line is what they answer).
        public bool FollowUp { get; internal set; }

        internal ReactionIntent(long id, StreamEvent streamEvent, ChatParticipant viewer, bool direct, int order, double due, double expires)
            : this(id, streamEvent, viewer, direct, order, due, expires, 0) { }

        internal ReactionIntent(long id, StreamEvent streamEvent, ChatParticipant viewer, bool direct, int order, double due, double expires, long presenceEpoch)
        {
            Id = id;
            Event = streamEvent;
            Viewer = viewer;
            Direct = direct;
            Order = order;
            DueSeconds = due;
            ExpiresSeconds = expires;
            PresenceEpoch = presenceEpoch;
        }
    }

    // The chat's breathing room for the current broadcast: a message budget that refills with the audience size,
    // per-viewer gaps and the recent density. Messages are counted when a reaction is scheduled.
    public sealed class ChatRhythm
    {
        private readonly ReactionTuning _tuning;
        private readonly Dictionary<string, double> _lastMessage = new();
        private readonly List<(string viewer, double at)> _recent = new();
        private double _refilledAt;
        private bool _started;

        public double Tokens { get; private set; }

        public ChatRhythm(ReactionTuning tuning) => _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));

        public double RatePerMinute(int viewers) =>
            viewers <= 0 ? 0 : Math.Min(_tuning.MaximumPerMinute, _tuning.RateScale * Math.Pow(viewers, _tuning.RateExponent));

        public double Burst(int viewers) => viewers <= 0 ? 0 : _tuning.BurstBase + _tuning.BurstScale * Math.Sqrt(viewers);

        public void Refill(double now, int viewers)
        {
            if (!_started)
            {
                _started = true;
                _refilledAt = now;
                Tokens = Burst(viewers);
                return;
            }
            double elapsed = Math.Max(0, now - _refilledAt);
            _refilledAt = now;
            Tokens = Math.Min(Burst(viewers), Tokens + RatePerMinute(viewers) / 60.0 * elapsed);
        }

        // A very significant moment may overdraw the budget by one message, nothing else can.
        public bool CanSpend(bool overdraw) => Tokens >= 1 || (overdraw && Tokens >= 0);

        // An answer in a conversation still uses the chat's energy, but never pushes the budget far below zero: talking
        // with the streamer must not silence the chat for minutes afterwards.
        public void RecordConversation(string viewerId, double at)
        {
            double tokens = Tokens;
            Record(viewerId, at);
            Tokens = Math.Max(Tokens, Math.Min(tokens, -1));
        }

        public void Record(string viewerId, double at)
        {
            Tokens -= 1;
            _lastMessage[viewerId] = at;
            _recent.Add((viewerId, at));
            if (_recent.Count > 400) _recent.RemoveRange(0, 100);
        }

        public double SinceLast(string viewerId, double now) =>
            _lastMessage.TryGetValue(viewerId, out double at) ? now - at : double.PositiveInfinity;

        public int Recent(double now, double window, string viewerId = null)
        {
            // Scheduled times are not strictly ordered (reply delays differ), so the bounded list is scanned whole.
            int count = 0;
            foreach (var (viewer, at) in _recent)
                if (now - at <= window && (viewerId == null || viewer == viewerId)) count++;
            return count;
        }
    }

    // Who the streamer is talking with right now: the viewer whose answer to them was published last, for a few turns
    // inside a short window. Transient, one per broadcast, never saved.
    public sealed class ConversationThread
    {
        private readonly Dictionary<string, (long intentId, long epoch, double until)> _pending = new(StringComparer.Ordinal);
        public string ViewerId { get; private set; }
        public long Epoch { get; private set; }
        public double LastAt { get; private set; } = double.NegativeInfinity;
        public int Turns { get; private set; }
        public string LastLine { get; private set; }

        internal void Observe(string viewerId, long epoch, double at, string line, bool followUp)
        {
            Turns = followUp && viewerId == ViewerId ? Turns + 1 : 1;
            ViewerId = viewerId;
            Epoch = epoch;
            LastAt = at;
            LastLine = line;
        }

        internal void End()
        {
            ViewerId = null;
            LastLine = null;
            Turns = 0;
            LastAt = double.NegativeInfinity;
        }

        internal bool Exhausted(string viewerId, long epoch, double now, ReactionTuning tuning) =>
            Pending(viewerId, epoch, now) || viewerId == ViewerId && epoch == Epoch &&
                Turns >= tuning.ConversationMaximumTurns && now - LastAt <= tuning.ConversationWindowSeconds;

        // Wait for the selected viewer's opening or continuation before scheduling another answer. A cancelled/dropped
        // intent releases this reservation; expiry also stops it blocking if publication never happens.
        internal void Reserve(ReactionIntent intent) =>
            _pending[intent.Viewer.ViewerId] = (intent.Id, intent.PresenceEpoch, intent.ExpiresSeconds);

        internal void Release(long intentId)
        {
            string owner = null;
            foreach (var pair in _pending) if (pair.Value.intentId == intentId) { owner = pair.Key; break; }
            if (owner != null) _pending.Remove(owner);
        }

        internal void Expire(double now)
        {
            // Normally Finished releases every entry. Prune expired openings as well for selector-only callers.
            while (true)
            {
                string owner = null;
                foreach (var pair in _pending) if (now > pair.Value.until) { owner = pair.Key; break; }
                if (owner == null) return;
                _pending.Remove(owner);
            }
        }

        private bool Pending(string viewerId, long epoch, double now) => viewerId != null &&
            _pending.TryGetValue(viewerId, out var pending) && pending.epoch == epoch && now <= pending.until;

        internal bool Active(AudienceRoster roster, double now, ReactionTuning tuning) =>
            ViewerId != null && roster.IsWatching(ViewerId) && roster.Epoch(ViewerId) == Epoch && now - LastAt <= tuning.ConversationWindowSeconds &&
            Turns < tuning.ConversationMaximumTurns && !Pending(ViewerId, Epoch, now);
    }

    // Decides zero, one or occasionally a few reactions to a normalized event. Silence is a normal result. All
    // chance comes from one seeded random stream per broadcast, so a test seed reproduces every decision.
    public sealed class ReactionSelector
    {
        private const float DirectReplyChance = .92f;
        private readonly ReactionTuning _tuning;
        private readonly AudienceRoster _roster;
        private readonly AudienceRandom _random;
        private readonly Func<string, RelationshipTier> _relationship;
        private readonly HashSet<string> _seenKeys = new(StringComparer.Ordinal);
        private readonly List<ChatParticipant> _chosen = new();
        private readonly List<(ChatParticipant viewer, double weight)> _weights = new();
        private long _intentSerial;
        private double _lastSocial = double.NegativeInfinity;

        public ChatRhythm Rhythm { get; }
        public ConversationThread Thread { get; } = new();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private readonly List<string> _candidateIds = new();
#endif
        public string LastCandidateIds { get; private set; }

        public ReactionSelector(ReactionTuning tuning, AudienceRoster roster, AudienceRandom random, Func<string, RelationshipTier> relationship = null)
        {
            _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
            string error = tuning.Validate();
            if (error != null) throw new ArgumentException(error, nameof(tuning));
            _roster = roster ?? throw new ArgumentNullException(nameof(roster));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _relationship = relationship;
            Rhythm = new ChatRhythm(tuning);
        }

        // Returns the reactions to schedule (possibly none). A repeated event key never reacts twice.
        public List<ReactionIntent> Select(StreamEvent streamEvent, double now, bool live, out string reason)
        {
            var intents = new List<ReactionIntent>();
            LastCandidateIds = null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _candidateIds.Clear();
#endif
            if (streamEvent == null) throw new ArgumentNullException(nameof(streamEvent));
            if (!live) { reason = "offline"; return intents; }
            if (streamEvent.Kind == StreamEventKind.ViewerReply) { reason = "social events require publication"; return intents; }
            if (!_seenKeys.Add(streamEvent.Key)) { reason = "duplicate"; return intents; }
            int viewers = _roster.AudienceSize;
            Rhythm.Refill(now, viewers);
            Thread.Expire(now);
            if (viewers == 0) { reason = "no audience"; return intents; }
            SpeechAnalysis speech = streamEvent.Kind == StreamEventKind.StreamerSpeech ? streamEvent.Speech : null;
            bool small = viewers <= _tuning.ConversationAudience;
            // The streamer is talking TO the chat: asking, requesting, greeting or naming someone.
            bool addressed = speech != null && speech.Addresses;
            // A phrase soon after a viewer's answer continues with that viewer unless the streamer turns to the group
            // or names somebody else. The exchange is bounded by the window and the turn count.
            bool followUp = SpeechRelevance.ContinuesConversation(speech) && Thread.Active(_roster, now, _tuning) &&
                            (speech.MentionedViewerIds.Count == 0 || speech.MentionedViewerIds.Contains(Thread.ViewerId));
            if (speech != null && !followUp && (speech.PluralAddress ||
                speech.MentionedViewerIds.Count > 0 && !speech.MentionedViewerIds.Contains(Thread.ViewerId))) Thread.End();
            if (streamEvent.Kind == StreamEventKind.StreamerSpeech && streamEvent.Significance < _tuning.SpeechThreshold && !followUp)
            {
                reason = "speech below threshold";
                return intents;
            }
            _chosen.Clear();

            // 1. Viewers the moment is about: named by the streamer, thanked, the donor, the one who joined, the one the
            //    streamer is talking with.
            foreach (string viewerId in DirectViewers(streamEvent, followUp ? Thread.ViewerId : null))
            {
                bool continuing = followUp && viewerId == Thread.ViewerId;
                bool conversation = continuing || addressed;
                if (intents.Count >= Math.Max(1, MaximumReactions(streamEvent, viewers)) ||
                    !(conversation && small) && !Rhythm.CanSpend((streamEvent.Significance >= .8f || conversation) && intents.Count == 0)) break;
                ChatParticipant viewer = _roster.Find(viewerId);
                if (viewer == null || !_roster.IsWatching(viewerId) || _chosen.Contains(viewer) || !Witnessed(streamEvent, viewer)) continue;
                if (conversation && Thread.Exhausted(viewerId, _roster.Epoch(viewerId), now, _tuning)) continue;
                if (Rhythm.SinceLast(viewerId, now) < (conversation ? _tuning.ConversationGapSeconds : _tuning.DirectGapSeconds)) continue;
                TraceCandidate(viewerId);
                double chance = continuing ? (SpeechRelevance.ConversationAsksForAnswer(speech) ? .95 : .6) : DirectChance(streamEvent, viewer);
                if (_random.NextDouble() >= chance) continue;
                ReactionIntent intent = Schedule(streamEvent, viewer, true, intents.Count, now, conversation);
                intents.Add(intent);
            }

            // 2. Talked to, somebody normally answers: in a small audience regardless of the chat budget, in a bigger
            //    one the first answer may overdraw it. Not every time, and a bare greeting less often than a question.
            if (intents.Count == 0 && addressed && (small || speech.AsksForAnswer) &&
                _random.NextDouble() < (speech.AsksForAnswer ? _tuning.ConversationAnswerChance : _tuning.GreetingAnswerChance) &&
                (small || Rhythm.CanSpend(true)))
            {
                ChatParticipant viewer = PickConversational(streamEvent, now);
                if (viewer != null)
                {
                    ReactionIntent intent = Schedule(streamEvent, viewer, false, 0, now, true);
                    intents.Add(intent);
                }
            }

            // 3. The rest of the watching chat, at a chance and count scaled by significance and audience size.
            float significance = streamEvent.Significance;
            double more = Math.Pow(significance, .9) * Engagement(viewers) * KindFactor(streamEvent.Kind);
            // Being asked something (or the chat being addressed) is an invitation even a tiny audience usually takes.
            if (speech != null && (speech.Has(SpeechCue.AddressesChat) || speech.Has(SpeechCue.Question)))
                more = Math.Min(.95, more * 1.5);
            if (intents.Count > 0) more *= .45;
            int maximum = MaximumReactions(streamEvent, viewers) - intents.Count;
            // A small group asked something together ("как дела, парни?") may get a second voice now and then.
            bool groupQuestion = small && viewers >= 2 && addressed && speech.AsksForAnswer && speech.PluralAddress;
            if (groupQuestion) maximum = Math.Max(maximum, 2 - intents.Count);
            // A chat that is already busy for its size lets ordinary moments pass.
            if (Rhythm.Recent(now, 60) >= Rhythm.Burst(viewers) + Rhythm.RatePerMinute(viewers) && significance < .75f) more *= .25;
            for (int reaction = 0; reaction < maximum; reaction++)
            {
                if (_random.NextDouble() >= more) break;
                bool overdraw = significance >= .8f && reaction == 0 && streamEvent.Kind != StreamEventKind.AudienceChatter;
                if (!(groupQuestion && intents.Count < 2) && !Rhythm.CanSpend(overdraw)) break;
                ChatParticipant viewer = groupQuestion ? PickConversational(streamEvent, now) : PickViewer(streamEvent, now);
                if (viewer == null) break;
                ReactionIntent intent = Schedule(streamEvent, viewer, false, intents.Count, now, groupQuestion);
                intents.Add(intent);
                more *= .42;
            }
            reason = intents.Count == 0 ? "silence" : null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LastCandidateIds = string.Join(", ", _candidateIds);
            foreach (var intent in intents) intent.CandidateIds = LastCandidateIds;
#endif
            return intents;
        }

        // Called only by the director's Shown event. One fixed roll per real line, then a weighted permanent
        // recipient; budget, gaps, expiry and the director's queue apply exactly as for ordinary reactions.
        public ReactionIntent SelectPublished(StreamChatMessage message, ReactionIntent origin, double now, double gameMinutes)
        {
            if (message == null || origin == null || origin.Event.ReplyDepth != 0 || !origin.Viewer.IsPermanent ||
                message.IntentId != origin.Id || message.ViewerId != origin.Viewer.ViewerId || !_roster.IsWatching(message.ViewerId) ||
                _roster.Epoch(message.ViewerId) != origin.PresenceEpoch || !_seenKeys.Add("reply." + message.Id)) return null;
            if (now - _lastSocial < 180 || _random.NextDouble() >= .03) return null;
            Rhythm.Refill(now, _roster.AudienceSize);
            if (!Rhythm.CanSpend(false)) return null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _candidateIds.Clear();
#endif
            _weights.Clear(); double total = 0;
            RelationshipTier originTier = Tier(message.ViewerId);
            foreach (var viewer in _roster.Named)
            {
                if (viewer.ViewerId == message.ViewerId || viewer.Persona.Profile == null ||
                    Rhythm.SinceLast(viewer.ViewerId, now) < _tuning.ViewerGapSeconds * viewer.Traits.Pace) continue;
                RelationshipTier tier = Tier(viewer.ViewerId);
                // A skeptic's dig draws out the people who like the streamer; a wary viewer rarely bothers.
                double weight = viewer.Persona.Profile.SocialTendency * (tier == RelationshipTier.Wary ? .6
                    : originTier == RelationshipTier.Wary && tier >= RelationshipTier.Friendly ? 2 : 1);
                if (!(weight > 0)) continue;
                TraceCandidate(viewer.ViewerId);
                _weights.Add((viewer, weight)); total += weight;
            }
            if (total <= 0) return null;
            double roll = _random.NextDouble() * total;
            foreach (var (viewer, weight) in _weights)
            {
                roll -= weight; if (roll > 0) continue;
                _chosen.Clear(); _lastSocial = now;
                var intent = Schedule(StreamEvent.Reply(message, now, _roster, gameMinutes), viewer, false, 0, now);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                intent.CandidateIds = string.Join(", ", _candidateIds);
#endif
                return intent;
            }
            return null;
        }

        // The audience's appetite to respond at all: tiny audiences mostly watch.
        public static double Engagement(int viewers) => .45 + .55 * (1 - Math.Exp(-viewers / 6.0));

        public static int MaximumReactions(StreamEvent streamEvent, int viewers)
        {
            if (streamEvent.Kind == StreamEventKind.AudienceChatter) return 1;
            return Math.Clamp(1 + (int)Math.Floor(streamEvent.Significance * Math.Log10(1 + viewers) * 1.6), 1, 5);
        }

        private static float KindFactor(StreamEventKind kind) => kind switch
        {
            // The simulation already decided somebody feels like talking; the budget decides whether they do.
            StreamEventKind.AudienceChatter => 5f,
            StreamEventKind.ViewerJoined => .35f,
            StreamEventKind.Follow => .6f,
            _ => 1f
        };

        private IEnumerable<string> DirectViewers(StreamEvent streamEvent, string conversationPartner)
        {
            if (conversationPartner != null) yield return conversationPartner;
            if (streamEvent.SubjectViewerId != null) yield return streamEvent.SubjectViewerId;
            if (streamEvent.Speech != null)
                foreach (string viewerId in streamEvent.Speech.MentionedViewerIds)
                    yield return viewerId;
        }

        // Called for every published line: a viewer's answer to the streamer talking to them opens (or continues) the
        // bounded conversation thread; anything else leaves it alone.
        public void ObservePublished(StreamChatMessage message, ReactionIntent origin)
        {
            if (message == null || origin == null || origin.Event.Kind != StreamEventKind.StreamerSpeech) return;
            if (!(origin.Conversational || origin.FollowUp || origin.Direct)) return;
            Thread.Release(origin.Id);
            Thread.Observe(message.ViewerId, origin.PresenceEpoch, message.StreamSeconds, message.Text, origin.FollowUp);
        }

        internal void FinishConversation(ReactionIntent intent) => Thread.Release(intent.Id);

        // Who answers when the streamer talks to the chat: anyone present who can write now. People with an authored
        // life, those the streamer was just talking with and talkative viewers are likelier; a viewer bored by the topic
        // still answers a direct question, only less often.
        private ChatParticipant PickConversational(StreamEvent streamEvent, double now)
        {
            _weights.Clear();
            double total = 0;
            foreach (ChatParticipant viewer in _roster.Named)
            {
                if (!ConversationAvailable(viewer, streamEvent, now) || !Witnessed(streamEvent, viewer)) continue;
                double weight = .5 + viewer.Traits.Talkativeness;
                if (Ignores(viewer, streamEvent)) weight *= .4;
                if (viewer.ViewerId == Thread.ViewerId) weight *= 1.5;
                weight *= Tier(viewer.ViewerId) switch
                {
                    RelationshipTier.Wary => .7, RelationshipTier.Friendly => 1.2, RelationshipTier.Loyal => 1.4, _ => 1
                };
                TraceCandidate(viewer.ViewerId);
                _weights.Add((viewer, weight));
                total += weight;
            }
            int anonymousSeats = streamEvent.HasWitnesses ? Math.Min(_roster.AnonymousCount, streamEvent.Witnesses.AnonymousSeats) : _roster.AnonymousCount;
            // An unnamed seat answers a direct question about as readily as a quiet regular.
            double anonymous = Math.Min(anonymousSeats, 3) * .45 + Math.Max(0, anonymousSeats - 3) * _tuning.AnonymousTalkativeness;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (anonymousSeats > 0) TraceCandidate("anonymous seats: " + anonymousSeats);
#endif
            double roll = _random.NextDouble() * (total + anonymous);
            if (roll >= total && anonymousSeats > 0)
            {
                ChatParticipant chatter = _roster.AnonymousChatter(_random, viewer => ConversationAvailable(viewer, streamEvent, now) && Witnessed(streamEvent, viewer), now);
                if (chatter != null) return chatter;
                roll = _random.NextDouble() * total;
            }
            foreach (var (viewer, weight) in _weights)
            {
                if (roll < weight) return viewer;
                roll -= weight;
            }
            return _weights.Count > 0 ? _weights[_weights.Count - 1].viewer : null;
        }

        private bool ConversationAvailable(ChatParticipant viewer, StreamEvent streamEvent, double now)
        {
            SpeechAnalysis speech = streamEvent.Speech;
            // Repeated general questions to the whole chat still use its ordinary per-viewer pace. A personal
            // question or direct address gets the short conversational gap, without dropping cooldown entirely.
            bool personal = speech != null && (speech.Is(SpeechAct.PersonalQuestion) || speech.SingularAddress ||
                speech.MentionedViewerIds.Contains(viewer.ViewerId));
            double gap = personal ? _tuning.ConversationGapSeconds : _tuning.ViewerGapSeconds * viewer.Traits.Pace;
            return !_chosen.Contains(viewer) && !Thread.Exhausted(viewer.ViewerId, _roster.Epoch(viewer.ViewerId), now, _tuning) &&
                Rhythm.SinceLast(viewer.ViewerId, now) >= gap;
        }

        private double DirectChance(StreamEvent streamEvent, ChatParticipant viewer)
        {
            float affinity = viewer.Traits.Affinity(streamEvent.Kind);
            return streamEvent.Kind switch
            {
                // Friends answer when addressed; a wary viewer may let pleasantries pass but still takes a question.
                StreamEventKind.StreamerSpeech => Tier(viewer.ViewerId) switch
                {
                    RelationshipTier.Wary => streamEvent.Speech?.Has(SpeechCue.Question) == true ? .8 : .5,
                    RelationshipTier.Friendly => .95,
                    RelationshipTier.Loyal => .97,
                    _ => DirectReplyChance
                },
                StreamEventKind.Donation => .85 * Math.Min(1f, affinity),
                StreamEventKind.Subscription => .6 * Math.Min(1f, affinity),
                StreamEventKind.Follow => .3 * Math.Min(1f, affinity),
                StreamEventKind.ViewerJoined => .5 * viewer.Traits.Talkativeness * Math.Min(1.5f, affinity),
                _ => .5 * viewer.Traits.Talkativeness
            };
        }

        private ChatParticipant PickViewer(StreamEvent streamEvent, double now)
        {
            _weights.Clear();
            double total = 0;
            foreach (ChatParticipant viewer in _roster.Named)
            {
                if (!Available(viewer, streamEvent, now) || !Witnessed(streamEvent, viewer) || Ignores(viewer, streamEvent)) continue;
                ReactionTraits traits = viewer.Traits;
                double weight = traits.Talkativeness * traits.Affinity(streamEvent.Kind);
                StreamTopic topics = streamEvent.Speech?.Topics ?? StreamTopic.None;
                if ((topics & traits.Interests) != 0) weight *= 1.9;
                weight *= Tier(viewer.ViewerId) switch
                {
                    RelationshipTier.Wary => .75, RelationshipTier.Friendly => 1.1, RelationshipTier.Loyal => 1.25, _ => 1
                };
                weight /= 1 + Rhythm.Recent(now, 120, viewer.ViewerId);
                if (weight <= 0) continue;
                TraceCandidate(viewer.ViewerId);
                _weights.Add((viewer, weight));
                total += weight;
            }
            int anonymousSeats = streamEvent.HasWitnesses ? Math.Min(_roster.AnonymousCount, streamEvent.Witnesses.AnonymousSeats) : _roster.AnonymousCount;
            double anonymous = anonymousSeats * _tuning.AnonymousTalkativeness;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (anonymousSeats > 0) TraceCandidate("anonymous seats: " + anonymousSeats);
#endif
            double roll = _random.NextDouble() * (total + anonymous);
            if (roll >= total && anonymousSeats > 0)
            {
                // A new chatter represents an unnamed seat present at this fact and gets event-only context.
                ChatParticipant chatter = _roster.AnonymousChatter(_random, viewer => Available(viewer, streamEvent, now) && Witnessed(streamEvent, viewer), now);
                if (chatter != null) return chatter;
                roll = _random.NextDouble() * total;
            }
            foreach (var (viewer, weight) in _weights)
            {
                if (roll < weight) return viewer;
                roll -= weight;
            }
            return _weights.Count > 0 ? _weights[_weights.Count - 1].viewer : null;
        }

        private RelationshipTier Tier(string viewerId) => _relationship?.Invoke(viewerId) ?? RelationshipTier.Neutral;

        // Authored silence: an unprompted phrase only about topics this viewer ignores draws nothing from them.
        private static bool Ignores(ChatParticipant viewer, StreamEvent e)
        {
            StreamTopic ignored = viewer.Persona.Profile?.Habits?.Ignores ?? StreamTopic.None;
            StreamTopic topics = (e.Speech?.Topics ?? StreamTopic.None) & ~StreamTopic.Community;
            return ignored != StreamTopic.None && topics != StreamTopic.None && (topics & ~ignored) == 0;
        }

        private bool Witnessed(StreamEvent e, ChatParticipant viewer) => !e.HasWitnesses || e.WitnessedBy(viewer.ViewerId, _roster.Epoch(viewer.ViewerId));

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void TraceCandidate(string id)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_candidateIds.Count < 64 && !_candidateIds.Contains(id)) _candidateIds.Add(id);
#endif
        }

        private bool Available(ChatParticipant viewer, StreamEvent streamEvent, double now) =>
            !_chosen.Contains(viewer) && Rhythm.SinceLast(viewer.ViewerId, now) >= _tuning.ViewerGapSeconds * viewer.Traits.Pace &&
            (!(streamEvent.Speech?.Addresses == true || SpeechRelevance.ContinuesConversation(streamEvent.Speech)) ||
                !Thread.Exhausted(viewer.ViewerId, _roster.Epoch(viewer.ViewerId), now, _tuning));

        private ReactionIntent Schedule(StreamEvent streamEvent, ChatParticipant viewer, bool direct, int order, double now, bool conversation = false)
        {
            bool continuing = SpeechRelevance.ContinuesConversation(streamEvent.Speech) &&
                Thread.Active(_roster, now, _tuning) && viewer.ViewerId == Thread.ViewerId;
            conversation |= continuing || streamEvent.Speech?.Addresses == true;
            // Reading and typing take time; later reactors to the same moment come later still. Someone who is being
            // talked to is already looking at the chat.
            double minimum = conversation ? _tuning.ConversationMinimumDelaySeconds : _tuning.MinimumDelaySeconds;
            double maximum = conversation ? _tuning.ConversationMaximumDelaySeconds : _tuning.MaximumDelaySeconds;
            double delay = minimum + (maximum - minimum) * Math.Pow(_random.NextDouble(), 1.4);
            RelationshipTier tier = Tier(viewer.ViewerId);
            // Closer viewers answer sooner; a wary one takes their time.
            delay *= viewer.Traits.Speed * (direct ? .8 : 1) * tier switch
            {
                RelationshipTier.Wary => 1.15, RelationshipTier.Friendly => direct ? .9 : .95, RelationshipTier.Loyal => direct ? .8 : .9, _ => 1
            };
            delay += order * (1.4 + 2.4 * _random.NextDouble());
            _chosen.Add(viewer);
            if (conversation) Rhythm.RecordConversation(viewer.ViewerId, now + delay);
            else Rhythm.Record(viewer.ViewerId, now + delay);
            var intent = new ReactionIntent(++_intentSerial, streamEvent, viewer, direct, order, now + delay, now + delay + _tuning.StaleSeconds,
                _roster.Epoch(viewer.ViewerId)) { Tier = tier, Conversational = conversation, FollowUp = continuing };
            if (conversation) Thread.Reserve(intent);
            return intent;
        }
    }
}
