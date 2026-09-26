using System;
using System.Collections.Generic;
using GoLive.Desktop;

namespace GoLive.Viewers
{
    [Serializable]
    public sealed class PermanentViewerSnapshot
    {
        public string ViewerId;
        public int Sentiment;
        public int VisitCount;
        public int Acknowledgements;
    }

    [Serializable]
    public sealed class ViewerCommunitySnapshot
    {
        public int Version = 1;
        public List<PermanentViewerSnapshot> Viewers = new();
    }

    // Only community commands can change the durable relationship. Profiles remain configuration.
    public sealed class PermanentViewerState
    {
        public string ViewerId { get; }
        public int Sentiment { get; internal set; }
        public int VisitCount { get; internal set; }
        public int Acknowledgements { get; internal set; }
        internal PermanentViewerState(string id, int sentiment) { ViewerId = id; Sentiment = sentiment; }
    }

    // Owns durable social state and a transient attendance plan. This owner separates authored identity from
    // saves, and presence decisions from text generation. The audience simulation still owns every seat.
    // API: begin/update context/tick/end, process observed facts, capture/validate/restore. Joined is emitted only
    // after a seat is acquired. Only relationship and visit counters save; plans and cooldowns never save.
    public sealed class ViewerCommunity
    {
        private sealed class Attendance
        {
            public ViewerProfile Profile;
            public double Roll, Arrival, Dwell, LeavesAt;
            public bool Visited;
        }

        private const double InteractionCooldown = 60;
        private readonly AudienceRoster _roster;
        private readonly List<ViewerProfile> _profiles;
        private readonly Dictionary<string, PermanentViewerState> _states = new(StringComparer.Ordinal);
        private readonly List<Attendance> _attendance = new();
        private readonly Dictionary<string, double> _lastInteraction = new(StringComparer.Ordinal);
        private string _broadcast = "";
        private double _gameMinutes;
        private StreamTopic _content;

        public event Action<ChatParticipant> Joined;
        public IReadOnlyList<ViewerProfile> Profiles { get; }
        // Known identity is independent of attendance. Cached forms also recognize an absent explicit target.
        public IReadOnlyList<ViewerNameForms> KnownNames { get; }

        public ViewerCommunity(IReadOnlyList<ViewerProfile> profiles, AudienceRoster roster)
        {
            _roster = roster ?? throw new ArgumentNullException(nameof(roster));
            _profiles = new List<ViewerProfile>(profiles ?? Array.Empty<ViewerProfile>());
            Profiles = _profiles.AsReadOnly();
            if (_profiles.Count > 0)
            {
                string error = ViewerCommunityCatalog.Validate(_profiles);
                if (error != null) throw new ArgumentException(error, nameof(profiles));
            }
            var names = new List<ViewerNameForms>(_profiles.Count);
            foreach (ViewerProfile profile in _profiles)
                names.Add(new ViewerNameForms(profile.Id, profile.Participant().NameForms));
            KnownNames = names.AsReadOnly();
            ResetStates();
        }

        public PermanentViewerState State(string viewerId) => viewerId != null && _states.TryGetValue(viewerId, out var state) ? state : null;

        public void BeginBroadcast(string broadcastId, ulong seed, double gameMinutes, StreamTopic content)
        {
            if (string.IsNullOrEmpty(broadcastId)) throw new ArgumentException("A broadcast needs an id.", nameof(broadcastId));
            if (_broadcast == broadcastId) return;
            int seats = _roster.AudienceSize;
            EndBroadcast();
            _roster.SetAudienceSize(seats);
            _broadcast = broadcastId;
            UpdateContext(gameMinutes, content);
            foreach (ViewerProfile profile in _profiles)
            {
                ulong hash = seed;
                foreach (char character in profile.Id) hash = AudienceRandom.Hash(hash, character);
                var random = new AudienceRandom(hash);
                _attendance.Add(new Attendance
                {
                    Profile = profile, Roll = random.NextDouble(), Arrival = 5 + random.NextDouble() * 85,
                    Dwell = 180 + random.NextDouble() * 720
                });
            }
            // Seeded arrival order, not catalog order, decides who gets scarce named seats.
            _attendance.Sort((a, b) => a.Arrival.CompareTo(b.Arrival));
        }

        public void UpdateContext(double gameMinutes, StreamTopic content)
        {
            if (double.IsNaN(gameMinutes) || double.IsInfinity(gameMinutes) || gameMinutes < 0)
                throw new ArgumentOutOfRangeException(nameof(gameMinutes));
            _gameMinutes = gameMinutes;
            _content = content;
        }

        public void Tick(double streamSeconds)
        {
            if (_broadcast.Length == 0) return;
            foreach (Attendance plan in _attendance)
            {
                string id = plan.Profile.Id;
                if (plan.Visited)
                {
                    if (streamSeconds >= plan.LeavesAt) _roster.Leave(id);
                    continue;
                }
                if (streamSeconds < plan.Arrival || plan.Roll >= AttendanceChance(plan.Profile) || _roster.Named.Count >= _roster.AudienceSize) continue;
                ChatParticipant participant = plan.Profile.Participant();
                if (!_roster.Join(participant, streamSeconds)) continue;
                plan.Visited = true;
                plan.LeavesAt = streamSeconds + plan.Dwell;
                PermanentViewerState state = _states[id];
                state.VisitCount = Increment(state.VisitCount);
                Joined?.Invoke(participant);
            }
        }

        private double AttendanceChance(ViewerProfile profile)
        {
            int hour = (int)(_gameMinutes % 1440 / 60);
            double schedule = profile.Schedule.Contains(hour) ? 1 : .18;
            double interest = (_content & profile.Interests) != 0 ? 1.15 : .65;
            double relationship = 1 + _states[profile.Id].Sentiment / 250d;
            return Math.Clamp(profile.Schedule.Regularity * schedule * interest * relationship, 0, .95);
        }

        public void EndBroadcast()
        {
            _broadcast = "";
            _attendance.Clear();
            _lastInteraction.Clear();
            _roster.Clear();
        }

        // Social mutations consume domain facts, never language-model output. Explicit names take precedence
        // over inferred donation subjects, and only current named viewers can receive an acknowledgement.
        public void Process(StreamEvent streamEvent)
        {
            if (_broadcast.Length == 0 || streamEvent?.Kind != StreamEventKind.StreamerSpeech || streamEvent.Speech == null) return;
            SpeechAnalysis speech = streamEvent.Speech;
            bool knownTarget = false;
            string phrase = " " + SpeechRelevance.Normalize(speech.Text) + " ";
            foreach (ViewerNameForms name in KnownNames)
            {
                if (!ExactlyNames(phrase, name.Forms)) continue;
                knownTarget = true;
                Acknowledge(name.ViewerId, streamEvent, true);
            }
            // This also protects callers that analyzed only the current roster: an absent named person is
            // still the explicit target, so a recent donor must not receive their thanks.
            if (knownTarget) return;
            if (speech.MentionedViewerIds.Count > 0)
            {
                foreach (string id in speech.MentionedViewerIds) Acknowledge(id, streamEvent, true);
            }
            else if (speech.Has(SpeechCue.Thanks) && streamEvent.SubjectViewerId != null)
                Acknowledge(streamEvent.SubjectViewerId, streamEvent, false);
        }

        private void Acknowledge(string id, StreamEvent streamEvent, bool explicitName)
        {
            if (!_states.TryGetValue(id, out var state) || !_roster.IsWatching(id)) return;
            // The chat relevance detector tolerates transcription typos. Durable social changes are more
            // conservative: a near-match to another viewer's name must not change their relationship.
            if (explicitName && !ExactlyNames(" " + SpeechRelevance.Normalize(streamEvent.Speech.Text) + " ", _roster.Find(id).NameForms)) return;
            if (_lastInteraction.TryGetValue(id, out double previous) && streamEvent.StreamSeconds - previous < InteractionCooldown) return;
            int delta = explicitName && IsDirectHostility(streamEvent.Speech.Text, _roster.Find(id)) ? -4
                : streamEvent.Speech.Has(SpeechCue.Thanks) ? 3 : 1;
            state.Sentiment = Math.Clamp(state.Sentiment + delta, -100, 100);
            state.Acknowledgements = Increment(state.Acknowledgements);
            _lastInteraction[id] = streamEvent.StreamSeconds;
        }

        private static bool ExactlyNames(string phrase, IReadOnlyList<string> forms)
        {
            foreach (string name in forms)
                if (phrase.Contains(" " + name + " ")) return true;
            return false;
        }

        private static bool IsDirectHostility(string text, ChatParticipant viewer)
        {
            string phrase = " " + SpeechRelevance.Normalize(text) + " ";
            foreach (string name in viewer.NameForms)
            {
                string addressed = " " + name + " ";
                if (phrase.Contains(addressed + "ты идиот ") || phrase.Contains(addressed + "заткнись ") ||
                    phrase.Contains(addressed + "ненавижу тебя ") || phrase.Contains(addressed + "you idiot ") ||
                    phrase.Contains(addressed + "shut up ") || phrase.Contains(addressed + "i hate you ")) return true;
            }
            return false;
        }

        // A derived social description changes warmth/familiarity without replacing the authored personality.
        public string RelationshipContext(string viewerId)
        {
            PermanentViewerState state = State(viewerId);
            if (state == null) return null;
            string familiarity = state.VisitCount >= 5 || state.Acknowledgements >= 3 ? "You recognize this streamer from previous interactions. "
                : state.VisitCount > 1 ? "You have visited this stream before. " : "You are still getting to know this streamer. ";
            string warmth = state.Sentiment >= 25 ? "You feel warmly toward the streamer. "
                : state.Sentiment <= -25 ? "You feel wary of the streamer. " : "Your attitude toward the streamer is neutral. ";
            return familiarity + warmth + "Keep your authored personality; familiarity never makes a gentle person aggressive.";
        }

        public ViewerCommunitySnapshot Capture()
        {
            var snapshot = new ViewerCommunitySnapshot();
            foreach (ViewerProfile profile in _profiles)
            {
                PermanentViewerState state = _states[profile.Id];
                snapshot.Viewers.Add(new PermanentViewerSnapshot
                {
                    ViewerId = state.ViewerId, Sentiment = state.Sentiment,
                    VisitCount = state.VisitCount, Acknowledgements = state.Acknowledgements
                });
            }
            return snapshot;
        }

        public string Validate(ViewerCommunitySnapshot snapshot)
        {
            if (snapshot == null) return null; // Pre-community saves seed authored defaults.
            if (snapshot.Version != 1 || snapshot.Viewers == null) return "Invalid viewer community snapshot.";
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (PermanentViewerSnapshot saved in snapshot.Viewers)
            {
                if (saved == null || saved.ViewerId == null || !_states.ContainsKey(saved.ViewerId) || !ids.Add(saved.ViewerId))
                    return "Unknown or duplicate saved viewer.";
                if (saved.Sentiment < -100 || saved.Sentiment > 100 || saved.VisitCount < 0 || saved.Acknowledgements < 0)
                    return "Invalid saved viewer relationship.";
            }
            return null;
        }

        public void Restore(ViewerCommunitySnapshot snapshot)
        {
            string error = Validate(snapshot);
            if (error != null) throw new ArgumentException(error, nameof(snapshot));
            EndBroadcast();
            ResetStates();
            if (snapshot == null) return;
            foreach (PermanentViewerSnapshot saved in snapshot.Viewers)
            {
                PermanentViewerState state = _states[saved.ViewerId];
                state.Sentiment = saved.Sentiment;
                state.VisitCount = saved.VisitCount;
                state.Acknowledgements = saved.Acknowledgements;
            }
        }

        private void ResetStates()
        {
            _states.Clear();
            foreach (ViewerProfile profile in _profiles) _states.Add(profile.Id, new PermanentViewerState(profile.Id, profile.InitialSentiment));
        }

        private static int Increment(int value) => value == int.MaxValue ? value : value + 1;
    }
}
