using System;
using System.Collections.Generic;
using GoLive.Desktop;

namespace GoLive.Viewers
{
    // How a chat participant tends to react. Built from an authored profile, or generic for anonymous viewers.
    public sealed class ReactionTraits
    {
        private static readonly int KindCount = Enum.GetValues(typeof(StreamEventKind)).Length;
        private readonly float[] _affinity;

        // Base willingness to write at all, 0..1.
        public float Talkativeness { get; }
        // Multiplies the minimum gap between two of this viewer's messages (2 = writes half as often).
        public float Pace { get; }
        // Multiplies the reading/typing delay (0.6 = quick typer).
        public float Speed { get; }
        public StreamTopic Interests { get; }

        public ReactionTraits(float talkativeness, float pace, float speed, StreamTopic interests, IReadOnlyDictionary<StreamEventKind, float> affinity = null)
        {
            if (!(talkativeness > 0 && talkativeness <= 1)) throw new ArgumentOutOfRangeException(nameof(talkativeness));
            if (!(pace > 0) || !(speed > 0)) throw new ArgumentOutOfRangeException(nameof(pace));
            Talkativeness = talkativeness;
            Pace = pace;
            Speed = speed;
            Interests = interests;
            _affinity = new float[KindCount];
            for (int i = 0; i < KindCount; i++) _affinity[i] = 1f;
            if (affinity != null)
                foreach (var pair in affinity)
                    _affinity[(int)pair.Key] = Math.Max(0f, pair.Value);
        }

        // How strongly this viewer cares about a kind of moment (0 = never reacts to it).
        public float Affinity(StreamEventKind kind) => _affinity[(int)kind];
    }

    public sealed class ChatParticipant
    {
        public string ViewerId { get; }
        public string DisplayName { get; }
        // Permanent viewers are authored (or promoted) community members; others exist for one broadcast.
        public bool IsPermanent { get; }
        public ReactionTraits Traits { get; }
        // Spoken/transcribed forms of the name, normalized.
        public IReadOnlyList<string> NameForms { get; }
        // How they write (prompt data only).
        public ViewerPersona Persona { get; }

        public ChatParticipant(string viewerId, string displayName, bool isPermanent, ReactionTraits traits, IReadOnlyList<string> nameForms = null,
            ViewerPersona persona = null)
        {
            if (string.IsNullOrWhiteSpace(viewerId)) throw new ArgumentException("A participant needs an id.", nameof(viewerId));
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("A participant needs a name.", nameof(displayName));
            ViewerId = viewerId;
            DisplayName = displayName;
            IsPermanent = isPermanent;
            Traits = traits ?? throw new ArgumentNullException(nameof(traits));
            Persona = persona ?? ViewerPersona.AnonymousPersona(0, ViewerLanguage.Russian);
            var forms = new List<string> { SpeechRelevance.Normalize(displayName) };
            if (nameForms != null)
                foreach (string form in nameForms)
                {
                    string normalized = SpeechRelevance.Normalize(form);
                    if (normalized.Length > 0 && !forms.Contains(normalized)) forms.Add(normalized);
                }
            NameForms = forms.AsReadOnly();
        }
    }

    // Who is watching the current broadcast. The audience simulation owns the count; this roster only names a
    // small subset of it (permanent viewers present) plus the anonymous chatters who actually wrote. The rest of
    // the audience stays a number. Transient: one per broadcast, never saved.
    public sealed class AudienceRoster
    {
        private readonly List<ChatParticipant> _named = new();
        private readonly List<ChatParticipant> _ephemeral = new();
        private readonly HashSet<string> _ephemeralNames = new(StringComparer.OrdinalIgnoreCase);
        private readonly Func<AudienceRandom, int, ChatParticipant> _createEphemeral;
        private readonly Dictionary<string, long> _visits = new(StringComparer.Ordinal);
        private readonly Dictionary<string, double> _joinedAt = new(StringComparer.Ordinal);
        private int _chatterSerial;
        private readonly HashSet<string> _reservedNames = new(StringComparer.OrdinalIgnoreCase);

        public void ReserveNames(IEnumerable<string> names)
        { _reservedNames.Clear(); foreach (string name in names) _reservedNames.Add(name); }

        public IReadOnlyList<ChatParticipant> Named { get; }
        // Anonymous viewers who have written in this broadcast (created on demand, never persisted).
        public IReadOnlyList<ChatParticipant> Ephemeral { get; }
        public int AudienceSize { get; private set; }
        public int AnonymousCount => Math.Max(0, AudienceSize - _named.Count);

        public AudienceRoster(Func<AudienceRandom, int, ChatParticipant> createEphemeral)
        {
            _createEphemeral = createEphemeral ?? throw new ArgumentNullException(nameof(createEphemeral));
            Named = _named.AsReadOnly();
            Ephemeral = _ephemeral.AsReadOnly();
        }

        // The simulation's current viewer count; named viewers can never outnumber it.
        public void SetAudienceSize(int viewers)
        {
            AudienceSize = Math.Max(0, viewers);
            while (_named.Count > AudienceSize) Leave(_named[_named.Count - 1].ViewerId);
            TrimEphemeral();
        }

        // Named visit generations survive Clear; a delayed line from a previous visit can never revive.
        public long Epoch(string viewerId) => IsWatching(viewerId) && _visits.TryGetValue(viewerId, out long epoch) ? epoch : 0;
        public double JoinedAt(string viewerId) => viewerId != null && _joinedAt.TryGetValue(viewerId, out double at) ? at : double.NaN;

        public bool IsWatching(string viewerId)
        {
            if (viewerId == null) return false;
            foreach (ChatParticipant participant in _named)
                if (participant.ViewerId == viewerId) return true;
            foreach (ChatParticipant participant in _ephemeral)
                if (participant.ViewerId == viewerId) return AnonymousCount > 0;
            return false;
        }

        public ChatParticipant Find(string viewerId)
        {
            foreach (ChatParticipant participant in _named)
                if (participant.ViewerId == viewerId) return participant;
            foreach (ChatParticipant participant in _ephemeral)
                if (participant.ViewerId == viewerId) return participant;
            return null;
        }

        public ChatParticipant FindByName(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return null;
            foreach (ChatParticipant participant in _named)
                if (string.Equals(participant.DisplayName, displayName, StringComparison.OrdinalIgnoreCase)) return participant;
            foreach (ChatParticipant participant in _ephemeral)
                if (string.Equals(participant.DisplayName, displayName, StringComparison.OrdinalIgnoreCase)) return participant;
            return null;
        }

        // Returns false when the viewer already watches or the audience has no room for another named viewer.
        public bool Join(ChatParticipant participant, double streamSeconds = 0)
        {
            if (participant == null || !participant.IsPermanent) throw new ArgumentException("Only permanent viewers join by name.", nameof(participant));
            if (Find(participant.ViewerId) != null || _named.Count >= AudienceSize) return false;
            _named.Add(participant);
            _visits.TryGetValue(participant.ViewerId, out long previous);
            _visits[participant.ViewerId] = checked(previous + 1);
            _joinedAt[participant.ViewerId] = streamSeconds;
            TrimEphemeral();
            return true;
        }

        public bool Leave(string viewerId)
        {
            for (int i = 0; i < _named.Count; i++)
                if (_named[i].ViewerId == viewerId)
                {
                    _named.RemoveAt(i);
                    _joinedAt.Remove(viewerId);
                    return true;
                }
            return false;
        }

        // Identity replacement consumes the chatter's existing anonymous seat and invalidates its queued jobs.
        public bool Promote(string ephemeralId, ChatParticipant permanent)
        {
            if (permanent == null || !permanent.IsPermanent || Find(permanent.ViewerId) != null || AnonymousCount == 0) return false;
            int index = _ephemeral.FindIndex(p => p.ViewerId == ephemeralId);
            if (index < 0) return false;
            double at = JoinedAt(ephemeralId);
            var old = _ephemeral[index]; _ephemeral.RemoveAt(index);
            _ephemeralNames.Remove(old.DisplayName); _visits.Remove(ephemeralId); _joinedAt.Remove(ephemeralId);
            return Join(permanent, at);
        }

        // An anonymous chatter: an existing one (they keep writing under the same name) or a new one.
        public ChatParticipant AnonymousChatter(AudienceRandom random, Func<ChatParticipant, bool> available, double streamSeconds = 0)
        {
            if (AnonymousCount == 0) return null;
            int existing = Math.Min(_ephemeral.Count, AnonymousCount);
            // Someone who already wrote is proportionally likely to be the one writing again.
            if (existing > 0 && random.NextDouble() < existing / (double)AnonymousCount)
            {
                int start = random.NextInt(existing);
                for (int i = 0; i < existing; i++)
                {
                    ChatParticipant candidate = _ephemeral[(start + i) % existing];
                    if (available(candidate)) return candidate;
                }
            }
            if (_ephemeral.Count >= Math.Min(AnonymousCount, 128)) return null;
            for (int attempt = 0; attempt < 4; attempt++)
            {
                ChatParticipant created = _createEphemeral(random, _chatterSerial++);
                if (created == null || created.IsPermanent || Find(created.ViewerId) != null || _reservedNames.Contains(created.DisplayName) ||
                    _named.Exists(p => string.Equals(p.DisplayName, created.DisplayName, StringComparison.OrdinalIgnoreCase)) || !_ephemeralNames.Add(created.DisplayName)) continue;
                _ephemeral.Add(created);
                _visits[created.ViewerId] = 1;
                _joinedAt[created.ViewerId] = streamSeconds;
                return created;
            }
            return null;
        }

        public IReadOnlyList<ViewerNameForms> NamesForMentions()
        {
            var names = new List<ViewerNameForms>(_named.Count + _ephemeral.Count);
            foreach (ChatParticipant participant in _named) names.Add(new ViewerNameForms(participant.ViewerId, participant.NameForms));
            foreach (ChatParticipant participant in _ephemeral) names.Add(new ViewerNameForms(participant.ViewerId, participant.NameForms));
            return names;
        }

        public void Clear()
        {
            _named.Clear();
            foreach (ChatParticipant participant in _ephemeral) _visits.Remove(participant.ViewerId);
            _ephemeral.Clear();
            _ephemeralNames.Clear();
            _joinedAt.Clear();
            AudienceSize = 0;
        }

        private void TrimEphemeral()
        {
            while (_ephemeral.Count > Math.Min(AnonymousCount, 128))
            {
                ChatParticipant removed = _ephemeral[_ephemeral.Count - 1];
                _ephemeral.RemoveAt(_ephemeral.Count - 1);
                _ephemeralNames.Remove(removed.DisplayName);
                _visits.Remove(removed.ViewerId);
                _joinedAt.Remove(removed.ViewerId);
            }
        }
    }
}
