using System;
using System.Collections.Generic;
using System.Linq;
using GoLive.PcBuilding;

namespace GoLive.Viewers
{
    public enum ViewerMemoryKind { ReportedFailure, ReportedAchievement, PersonalAcknowledgement, TechnicalIncident }
    public enum MemoryKnowledgeSource { Witnessed, HeardStreamer }

    [Serializable]
    public sealed class ViewerMemorySnapshot
    {
        public string MemoryId, EventKey, CanonicalSubject;
        public ViewerMemoryKind Kind;
        public StreamTopic Topics;
        public int Importance, Emotion, ReferenceCount;
        public double CreatedGameMinutes, LastReferencedGameMinutes;
        public MemoryKnowledgeSource KnowledgeSource;
        internal ViewerMemorySnapshot Copy() => (ViewerMemorySnapshot)MemberwiseClone();
    }

    // Immutable prompt/monitor projection. No transcript, model text or arbitrary diary is stored.
    public sealed class ViewerMemory
    {
        private readonly ViewerMemorySnapshot _fact;
        internal ViewerMemory(ViewerMemorySnapshot fact) { _fact = fact.Copy(); }
        public string MemoryId => _fact.MemoryId;
        public string EventKey => _fact.EventKey;
        public string CanonicalSubject => _fact.CanonicalSubject;
        public ViewerMemoryKind Kind => _fact.Kind;
        public StreamTopic Topics => _fact.Topics;
        public int Importance => _fact.Importance;
        public int Emotion => _fact.Emotion;
        public int ReferenceCount => _fact.ReferenceCount;
        public double CreatedGameMinutes => _fact.CreatedGameMinutes;
        public double LastReferencedGameMinutes => _fact.LastReferencedGameMinutes;
        public MemoryKnowledgeSource KnowledgeSource => _fact.KnowledgeSource;
    }

    // One permanent viewer owns one bank. Facts enter only through observed normalized events; the model has
    // no write API. Durable facts save, while in-flight callback reservations are bounded and transient.
    public sealed class ViewerMemoryBank
    {
        public const int Capacity = 16;
        public const double ReferenceCooldownMinutes = 120;
        private readonly List<ViewerMemorySnapshot> _facts = new();
        private readonly string _viewerId;
        private readonly Dictionary<string, long> _reserved = new(StringComparer.Ordinal);
        internal ViewerMemoryBank(string viewerId) { _viewerId = viewerId; }
        public IReadOnlyList<ViewerMemory> Summary => _facts.Select(f => new ViewerMemory(f)).ToList().AsReadOnly();

        internal void Observe(StreamEvent e, ViewerProfile profile, IReadOnlyList<string> nameForms)
        {
            if (!e.HasWitnesses || !e.WitnessedBy(profile.Id)) return;
            Decay(e.GameMinutes);
            if (_facts.Any(f => f.EventKey == e.Key)) return;
            ViewerMemorySnapshot fact = Extract(e, profile, nameForms);
            if (fact == null) return;
            if (fact.Kind == ViewerMemoryKind.TechnicalIncident && _facts.Any(f => f.Kind == fact.Kind && f.CanonicalSubject == fact.CanonicalSubject)) return;
            _facts.Add(fact);
            if (_facts.Count <= Capacity) return;
            var victim = _facts.OrderBy(f => f.Importance).ThenBy(f => f.ReferenceCount)
                .ThenBy(f => f.CreatedGameMinutes).ThenBy(f => f.MemoryId, StringComparer.Ordinal).First();
            _facts.Remove(victim);
            _reserved.Remove(victim.MemoryId);
        }

        public void Decay(double gameMinutes)
        {
            if (!Finite(gameMinutes) || gameMinutes < 0) throw new ArgumentOutOfRangeException(nameof(gameMinutes));
            for (int i = _facts.Count - 1; i >= 0; i--)
            {
                var fact = _facts[i];
                double days = fact.Importance == 1 ? 7 : fact.Importance == 2 ? 30 : 90;
                double horizon = days * 1440;
                // References refresh the base horizon, but never beyond twice the original lifetime.
                double expiry = Math.Min(fact.CreatedGameMinutes + 2 * horizon,
                    Math.Max(fact.CreatedGameMinutes, fact.LastReferencedGameMinutes) + horizon);
                if (gameMinutes < expiry) continue;
                _reserved.Remove(fact.MemoryId);
                _facts.RemoveAt(i);
            }
        }

        public IReadOnlyList<ViewerMemory> Retrieve(StreamEvent current, double gameMinutes) => Choose(current, gameMinutes, 0);
        internal IReadOnlyList<ViewerMemory> Reserve(StreamEvent current, double gameMinutes, long requestId) => Choose(current, gameMinutes, requestId);

        private IReadOnlyList<ViewerMemory> Choose(StreamEvent current, double gameMinutes, long requestId)
        {
            Decay(gameMinutes);
            if (current == null) return Array.Empty<ViewerMemory>();
            string subject = Subject(current);
            if (subject == null) return Array.Empty<ViewerMemory>();
            if (subject == "personal-acknowledgement" && current.SubjectViewerId != _viewerId &&
                current.Speech?.MentionedViewerIds.Contains(_viewerId) != true) return Array.Empty<ViewerMemory>();
            var chosen = _facts.Where(f => f.EventKey != current.Key && f.CanonicalSubject == subject &&
                f.CreatedGameMinutes <= gameMinutes && f.ReferenceCount < 3 && !_reserved.ContainsKey(f.MemoryId) &&
                (f.ReferenceCount == 0 || gameMinutes - f.LastReferencedGameMinutes >= ReferenceCooldownMinutes))
                .OrderByDescending(f => Relevance(current, f)).ThenByDescending(f => f.CreatedGameMinutes)
                .ThenBy(f => f.MemoryId, StringComparer.Ordinal).Take(2).Select(f => new ViewerMemory(f)).ToList();
            if (requestId != 0) foreach (var fact in chosen) _reserved[fact.MemoryId] = requestId;
            return chosen.AsReadOnly();
        }

        private static int Relevance(StreamEvent current, ViewerMemorySnapshot fact)
        {
            StreamTopic topics = current.Kind == StreamEventKind.PeripheralChanged ? StreamTopic.StreamSetup : current.Speech?.Topics ?? StreamTopic.None;
            bool sameType = current.Kind == StreamEventKind.PeripheralChanged && fact.Kind == ViewerMemoryKind.TechnicalIncident ||
                current.Speech?.Has(SpeechCue.Thanks) == true && fact.Kind == ViewerMemoryKind.PersonalAcknowledgement;
            return fact.Importance * 10 + ((topics & fact.Topics) != 0 ? 2 : 0) + (sameType ? 1 : 0);
        }

        internal void Finish(long requestId, IReadOnlyList<ViewerMemory> memories, string publishedText, double now)
        {
            if (memories == null) return;
            bool consumed = false;
            foreach (var memory in memories)
            {
                if (!_reserved.TryGetValue(memory.MemoryId, out long owner) || owner != requestId) continue;
                _reserved.Remove(memory.MemoryId);
                var fact = _facts.Find(f => f.MemoryId == memory.MemoryId);
                if (fact == null || consumed || !References(publishedText, memory)) continue;
                fact.ReferenceCount = Math.Min(3, fact.ReferenceCount + 1);
                fact.LastReferencedGameMinutes = Math.Max(fact.CreatedGameMinutes, now);
                consumed = true;
            }
        }

        internal void ClearReservations() => _reserved.Clear();
        internal List<ViewerMemorySnapshot> Capture() => _facts.Select(f => f.Copy()).ToList();
        internal void Restore(List<ViewerMemorySnapshot> facts)
        {
            _facts.Clear(); _reserved.Clear();
            if (facts != null) foreach (var fact in facts) _facts.Add(fact.Copy());
        }

        internal static string Validate(List<ViewerMemorySnapshot> facts)
        {
            if (facts == null) return null; // Optional field in pre-memory saves.
            if (facts.Count > Capacity) return "Too many viewer memories.";
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var events = new HashSet<string>(StringComparer.Ordinal);
            foreach (var f in facts)
            {
                if (f == null || string.IsNullOrWhiteSpace(f.MemoryId) || f.MemoryId.Length > 192 ||
                    string.IsNullOrWhiteSpace(f.EventKey) || f.EventKey.Length > 160 || !ids.Add(f.MemoryId) || !events.Add(f.EventKey) ||
                    !Enum.IsDefined(typeof(ViewerMemoryKind), f.Kind) || !Enum.IsDefined(typeof(MemoryKnowledgeSource), f.KnowledgeSource) ||
                    !Canonical(f.CanonicalSubject) || ((int)f.Topics & ~63) != 0 || f.Topics == StreamTopic.None ||
                    f.Importance < 1 || f.Importance > 3 || f.Emotion < -2 || f.Emotion > 2 || f.ReferenceCount < 0 || f.ReferenceCount > 3 ||
                    !Finite(f.CreatedGameMinutes) || f.CreatedGameMinutes < 0 || !Finite(f.LastReferencedGameMinutes) ||
                    f.LastReferencedGameMinutes < 0 || (f.ReferenceCount > 0 && f.LastReferencedGameMinutes < f.CreatedGameMinutes) ||
                    (f.ReferenceCount == 0 && f.LastReferencedGameMinutes != 0) ||
                    f.MemoryId != f.EventKey + "." + (int)f.Kind ||
                    ((f.Kind == ViewerMemoryKind.ReportedFailure || f.Kind == ViewerMemoryKind.ReportedAchievement ||
                        f.Kind == ViewerMemoryKind.PersonalAcknowledgement) && f.KnowledgeSource != MemoryKnowledgeSource.HeardStreamer))
                    return "Invalid viewer memory.";
            }
            return null;
        }

        private static bool Finite(double n) => !double.IsNaN(n) && !double.IsInfinity(n);
        private static bool Canonical(string s) => s == "gpu" || s == "microphone" || s == "webcam" || s == "final" ||
            s == "boss" || s == "game-session" || s == "personal-acknowledgement";

        private static ViewerMemorySnapshot Extract(StreamEvent e, ViewerProfile profile, IReadOnlyList<string> nameForms)
        {
            ViewerMemoryKind kind;
            MemoryKnowledgeSource source = MemoryKnowledgeSource.HeardStreamer;
            int importance = 2, emotion = -1;
            string subject = Subject(e);
            StreamTopic topics;
            if (e.Kind == StreamEventKind.PeripheralChanged && !e.Connected &&
                (profile.Interests & (StreamTopic.Hardware | StreamTopic.StreamSetup)) != 0)
            {
                kind = ViewerMemoryKind.TechnicalIncident; source = MemoryKnowledgeSource.Witnessed;
                topics = StreamTopic.StreamSetup; importance = 1;
            }
            else if (e.Kind == StreamEventKind.StreamerSpeech && e.Speech != null)
            {
                string text = " " + SpeechRelevance.Normalize(e.Speech.Text) + " ";
                bool named = nameForms.Any(n => text.Contains(" " + n + " "));
                if (e.Speech.Has(SpeechCue.Thanks) && (named || (e.Speech.MentionedViewerIds.Count == 0 && e.SubjectViewerId == profile.Id)))
                {
                    kind = ViewerMemoryKind.PersonalAcknowledgement; subject = "personal-acknowledgement";
                    topics = StreamTopic.Community; importance = 2; emotion = 1;
                }
                else
                {
                    // Self-reports are hearsay, never verified gameplay. Hypotheticals, promises and ordinary kills are ignored.
                    if (e.Speech.Has(SpeechCue.Conditional | SpeechCue.FutureCommitment) ||
                        Contains(text, " не ", " not ", " never ", " didnt ", " didn t ", "он сказал", "она сказала", "они сказали", "мне сказали", "he said", "she said", "they said", "цитир", "quoted") ||
                        !(text.Contains(" я ") || text.Contains(" i ") || text.Contains(" у меня ")) ||
                        !(Contains(text, "впервые", "наконец", "финал", "турнир", "first time", "finally", "final", "tournament"))) return null;
                    if (ReportsFailure(text)) kind = ViewerMemoryKind.ReportedFailure;
                    else if (ReportsAchievement(text))
                    { kind = ViewerMemoryKind.ReportedAchievement; emotion = 2; }
                    else return null;
                    topics = StreamTopic.Games; importance = 3;
                    subject ??= "game-session";
                }
            }
            else return null;
            return new ViewerMemorySnapshot
            {
                MemoryId = e.Key + "." + (int)kind, EventKey = e.Key, Kind = kind, CanonicalSubject = subject,
                Topics = topics, Importance = importance, Emotion = emotion, CreatedGameMinutes = e.GameMinutes, KnowledgeSource = source
            };
        }

        internal static string Subject(StreamEvent e)
        {
            if (e.Kind == StreamEventKind.PeripheralChanged) return e.Peripheral == PcPeripheralKind.Microphone ? "microphone" : "webcam";
            string text = SpeechRelevance.Normalize(e.Speech?.Text ?? "");
            if (Contains(text, "видеокарт", "видюх", "gpu")) return "gpu";
            if (Contains(text, "микрофон", "microphone")) return "microphone";
            if (Contains(text, "вебк", "камер", "webcam")) return "webcam";
            if (Contains(text, "финал", "турнир", "tournament") || (" " + text + " ").Contains(" final ")) return "final";
            if (Contains(text, "босс", "boss")) return "boss";
            if (e.Speech?.Has(SpeechCue.Thanks) == true) return "personal-acknowledgement";
            return (e.Speech?.Topics & StreamTopic.Games) != 0 && e.Speech != null ? "game-session" : null;
        }

        internal static bool Historical(string text) => Contains((text ?? "").ToLowerInvariant(), "помню", "вчера", "прошл", "раньше", "remember", "last stream", "yesterday", "previous stream");
        internal static bool References(string text, ViewerMemory memory)
        {
            if (!Historical(text)) return false;
            string normalized = SpeechRelevance.Normalize(text);
            // Same subject is not the same outcome: recalling a loss cannot consume or justify a win.
            // Generic subject-only callbacks are allowed, but Finish charges at most one chosen record.
            bool failure = ReportsFailure(normalized);
            bool achievement = ReportsAchievement(normalized);
            if (memory.Kind == ViewerMemoryKind.ReportedFailure && achievement && !failure ||
                memory.Kind == ViewerMemoryKind.ReportedAchievement && failure && !achievement) return false;
            bool subject = memory.CanonicalSubject switch
            {
                "gpu" => Contains(normalized, "видеокарт", "видюх", "gpu"),
                "microphone" => Contains(normalized, "микрофон", "microphone"),
                "webcam" => Contains(normalized, "камер", "вебк", "webcam"),
                "final" => Contains(normalized, "финал", "турнир", "final", "tournament"),
                "boss" => Contains(normalized, "босс", "boss"),
                "personal-acknowledgement" => Contains(normalized, "спасибо", "thanks", "thank"),
                _ => Contains(normalized, "игр", "game")
            };
            return subject && (memory.KnowledgeSource == MemoryKnowledgeSource.Witnessed ||
                Contains(normalized, "говорил", "рассказывал", "сказал", "said", "told", "heard"));
        }
        private static bool ReportsFailure(string text) => Contains(text, "проиграл", "провалил", "потерял") ||
            Contains(" " + text + " ", " lost ", " failed ");
        private static bool ReportsAchievement(string text) => Contains(text, "выиграл", "победил", "прошел", "прошёл") ||
            Contains(" " + text + " ", " won ", " beat ", " completed ");
        private static bool Contains(string text, params string[] parts) => parts.Any(text.Contains);
    }
}
