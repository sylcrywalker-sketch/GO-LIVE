using System;
using System.Collections.Generic;
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

        internal ReactionIntent(long id, StreamEvent streamEvent, ChatParticipant viewer, bool direct, int order, double due, double expires)
        {
            Id = id;
            Event = streamEvent;
            Viewer = viewer;
            Direct = direct;
            Order = order;
            DueSeconds = due;
            ExpiresSeconds = expires;
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

    // Decides zero, one or occasionally a few reactions to a normalized event. Silence is a normal result. All
    // chance comes from one seeded random stream per broadcast, so a test seed reproduces every decision.
    public sealed class ReactionSelector
    {
        private const float DirectReplyChance = .92f;
        private readonly ReactionTuning _tuning;
        private readonly AudienceRoster _roster;
        private readonly AudienceRandom _random;
        private readonly HashSet<string> _seenKeys = new(StringComparer.Ordinal);
        private readonly List<ChatParticipant> _chosen = new();
        private readonly List<(ChatParticipant viewer, double weight)> _weights = new();
        private long _intentSerial;

        public ChatRhythm Rhythm { get; }

        public ReactionSelector(ReactionTuning tuning, AudienceRoster roster, AudienceRandom random)
        {
            _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
            string error = tuning.Validate();
            if (error != null) throw new ArgumentException(error, nameof(tuning));
            _roster = roster ?? throw new ArgumentNullException(nameof(roster));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            Rhythm = new ChatRhythm(tuning);
        }

        // Returns the reactions to schedule (possibly none). A repeated event key never reacts twice.
        public List<ReactionIntent> Select(StreamEvent streamEvent, double now, bool live, out string reason)
        {
            var intents = new List<ReactionIntent>();
            if (streamEvent == null) throw new ArgumentNullException(nameof(streamEvent));
            if (!live) { reason = "offline"; return intents; }
            if (!_seenKeys.Add(streamEvent.Key)) { reason = "duplicate"; return intents; }
            int viewers = _roster.AudienceSize;
            Rhythm.Refill(now, viewers);
            if (viewers == 0) { reason = "no audience"; return intents; }
            if (streamEvent.Kind == StreamEventKind.StreamerSpeech && streamEvent.Significance < _tuning.SpeechThreshold)
            {
                reason = "speech below threshold";
                return intents;
            }
            _chosen.Clear();

            // 1. Viewers the moment is about: named by the streamer, thanked, the donor, the one who joined.
            foreach (string viewerId in DirectViewers(streamEvent))
            {
                ChatParticipant viewer = _roster.Find(viewerId);
                if (viewer == null || !_roster.IsWatching(viewerId) || _chosen.Contains(viewer)) continue;
                if (Rhythm.SinceLast(viewerId, now) < _tuning.DirectGapSeconds) continue;
                if (_random.NextDouble() >= DirectChance(streamEvent, viewer)) continue;
                intents.Add(Schedule(streamEvent, viewer, true, intents.Count, now));
            }

            // 2. The rest of the watching chat, at a chance and count scaled by significance and audience size.
            float significance = streamEvent.Significance;
            double chance = Math.Pow(significance, .9) * Engagement(viewers) * KindFactor(streamEvent.Kind);
            if (intents.Count > 0) chance *= .45;
            int maximum = MaximumReactions(streamEvent, viewers) - intents.Count;
            // A chat that is already busy for its size lets ordinary moments pass.
            if (Rhythm.Recent(now, 60) >= Rhythm.Burst(viewers) + Rhythm.RatePerMinute(viewers) && significance < .75f) chance *= .25;
            for (int reaction = 0; reaction < maximum; reaction++)
            {
                if (_random.NextDouble() >= chance) break;
                bool overdraw = significance >= .8f && reaction == 0 && streamEvent.Kind != StreamEventKind.AudienceChatter;
                if (!Rhythm.CanSpend(overdraw)) break;
                ChatParticipant viewer = PickViewer(streamEvent, now);
                if (viewer == null) break;
                intents.Add(Schedule(streamEvent, viewer, false, intents.Count, now));
                chance *= .42;
            }
            reason = intents.Count == 0 ? "silence" : null;
            return intents;
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

        private IEnumerable<string> DirectViewers(StreamEvent streamEvent)
        {
            if (streamEvent.SubjectViewerId != null) yield return streamEvent.SubjectViewerId;
            if (streamEvent.Speech != null)
                foreach (string viewerId in streamEvent.Speech.MentionedViewerIds)
                    yield return viewerId;
        }

        private double DirectChance(StreamEvent streamEvent, ChatParticipant viewer)
        {
            float affinity = viewer.Traits.Affinity(streamEvent.Kind);
            return streamEvent.Kind switch
            {
                StreamEventKind.StreamerSpeech => DirectReplyChance,
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
                if (!Available(viewer, now)) continue;
                ReactionTraits traits = viewer.Traits;
                double weight = traits.Talkativeness * traits.Affinity(streamEvent.Kind);
                StreamTopic topics = streamEvent.Speech?.Topics ?? StreamTopic.None;
                if ((topics & traits.Interests) != 0) weight *= 1.9;
                weight /= 1 + Rhythm.Recent(now, 120, viewer.ViewerId);
                if (weight <= 0) continue;
                _weights.Add((viewer, weight));
                total += weight;
            }
            double anonymous = _roster.AnonymousCount * _tuning.AnonymousTalkativeness;
            double roll = _random.NextDouble() * (total + anonymous);
            if (roll >= total)
            {
                ChatParticipant chatter = _roster.AnonymousChatter(_random, viewer => Available(viewer, now));
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

        private bool Available(ChatParticipant viewer, double now) =>
            !_chosen.Contains(viewer) && Rhythm.SinceLast(viewer.ViewerId, now) >= _tuning.ViewerGapSeconds * viewer.Traits.Pace;

        private ReactionIntent Schedule(StreamEvent streamEvent, ChatParticipant viewer, bool direct, int order, double now)
        {
            // Reading and typing take time; later reactors to the same moment come later still.
            double delay = _tuning.MinimumDelaySeconds + (_tuning.MaximumDelaySeconds - _tuning.MinimumDelaySeconds) * Math.Pow(_random.NextDouble(), 1.4);
            delay *= viewer.Traits.Speed * (direct ? .8 : 1);
            delay += order * (1.4 + 2.4 * _random.NextDouble());
            _chosen.Add(viewer);
            Rhythm.Record(viewer.ViewerId, now + delay);
            return new ReactionIntent(++_intentSerial, streamEvent, viewer, direct, order, now + delay, now + delay + _tuning.StaleSeconds);
        }
    }
}
