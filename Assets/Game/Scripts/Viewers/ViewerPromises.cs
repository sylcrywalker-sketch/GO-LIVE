using System;
using System.Collections.Generic;
using System.Linq;

namespace GoLive.Viewers
{
    public enum PromiseAction { Purchase, Install, StartBroadcast }
    public enum PromiseCondition { Occurrence, ValueBelow }
    public enum PromiseStatus { Open, Fulfilled, Broken, Expired }

    public sealed class PromiseProposal
    {
        public PromiseAction Action { get; }
        public string Subject { get; }
        public PromiseCondition Condition { get; }
        public double Created { get; }
        public double WindowStart { get; }
        public double Deadline { get; }
        public double Threshold { get; }
        public bool VerifiableDeadline { get; }
        public PromiseProposal(PromiseAction action, string subject, double created, double windowStart, double deadline,
            PromiseCondition condition = PromiseCondition.Occurrence, double threshold = 0, bool verifiableDeadline = true)
        {
            if (!ViewerPromiseLedger.Identifier(subject) || !ViewerPromiseLedger.Time(created) || !ViewerPromiseLedger.Time(windowStart) ||
                !ViewerPromiseLedger.Time(deadline) || windowStart < created || deadline <= windowStart || !ViewerPromiseLedger.Time(threshold) ||
                !Enum.IsDefined(typeof(PromiseAction), action) || !Enum.IsDefined(typeof(PromiseCondition), condition))
                throw new ArgumentException("Invalid promise condition.");
            Action = action; Subject = subject; Created = created; WindowStart = windowStart; Deadline = deadline;
            Condition = condition; Threshold = threshold; VerifiableDeadline = verifiableDeadline;
        }
    }

    // A typed, completed domain outcome. Callers are gameplay owners/adapters, never language generation.
    public sealed class PromiseGameplayFact
    {
        public PromiseAction Action { get; }
        public string Subject { get; }
        public double GameMinutes { get; }
        public string Identity { get; }
        public EventWitnesses Witnesses { get; }
        public double Value { get; }
        public PromiseGameplayFact(PromiseAction action, string subject, double gameMinutes, string identity, EventWitnesses witnesses, double value = 0)
        {
            if (!ViewerPromiseLedger.Identifier(subject) || !ViewerPromiseLedger.Time(gameMinutes) || !ViewerPromiseLedger.Time(value) ||
                string.IsNullOrEmpty(identity) || identity.Length > 200 || witnesses == null || !Enum.IsDefined(typeof(PromiseAction), action))
                throw new ArgumentException("Invalid gameplay fact.");
            Action = action; Subject = subject; GameMinutes = gameMinutes; Identity = identity; Witnesses = witnesses; Value = value;
        }
    }

    [Serializable] public sealed class PromiseKnowledgeSnapshot
    {
        public string ViewerId;
        public PromiseStatus Status;
        public double TerminalMinutes;
        public int References;
        public double LastReferenceMinutes = -1;
    }
    [Serializable] public sealed class ViewerPromiseSnapshot
    {
        public string Id;
        public PromiseAction Action;
        public string Subject;
        public PromiseCondition Condition;
        public double Created, WindowStart, Deadline, Threshold, TerminalMinutes;
        public bool VerifiableDeadline;
        public PromiseStatus Status;
        public List<string> Witnesses = new();
        public List<PromiseKnowledgeSnapshot> Knowledge = new();
    }
    [Serializable] public sealed class ViewerPromiseLedgerSnapshot
    {
        public List<ViewerPromiseSnapshot> Records = new();
        // Events before this absolute time can never be admitted again after retention eviction.
        public double AdmissionFloor;
    }
    public sealed class ViewerPromiseContext
    {
        public string Id { get; }
        public PromiseAction Action { get; }
        public string Subject { get; }
        public PromiseStatus Status { get; }
        public double CreatedGameMinutes { get; }
        public double DeadlineGameMinutes { get; }
        public double TerminalGameMinutes { get; }
        internal ViewerPromiseContext(ViewerPromiseSnapshot record, PromiseStatus status, double terminal)
        { Id = record.Id; Action = record.Action; Subject = record.Subject; Status = status;
            CreatedGameMinutes = record.Created; DeadlineGameMinutes = record.Deadline; TerminalGameMinutes = terminal; }
    }
    public sealed class ViewerPromiseRecord
    {
        public string Id { get; }
        public PromiseAction Action { get; }
        public string Subject { get; }
        public PromiseStatus Status { get; }
        public double CreatedGameMinutes { get; }
        public double DeadlineGameMinutes { get; }
        public double TerminalGameMinutes { get; }
        public IReadOnlyList<string> Witnesses { get; }
        internal ViewerPromiseRecord(ViewerPromiseSnapshot r)
        { Id = r.Id; Action = r.Action; Subject = r.Subject; Status = r.Status; CreatedGameMinutes = r.Created;
            DeadlineGameMinutes = r.Deadline; TerminalGameMinutes = r.TerminalMinutes; Witnesses = new List<string>(r.Witnesses).AsReadOnly(); }
    }

    // Owns up to 16 objective promises and separate stable-id knowledge. Generic action/subject/window
    // comparisons solve the speech-vs-gameplay truth boundary; hardware vocabulary lives in the adapter.
    // Only transitions emit KnownOutcome; callback reservations are transient, acknowledgement counts save.
    public sealed class ViewerPromiseLedger
    {
        public const int Capacity = 16;
        private readonly List<ViewerPromiseSnapshot> _records = new();
        private readonly Dictionary<string, long> _reservations = new(StringComparer.Ordinal);
        private double _admissionFloor;
        public event Action<string, int> KnownOutcome;
        public bool NeedsAdvance(double now)
        {
            foreach (var r in _records)
                if (r.Status == PromiseStatus.Open ? now > r.Deadline : now - r.TerminalMinutes >= 7 * 1440) return true;
            return false;
        }
        public IReadOnlyList<ViewerPromiseRecord> Summary => _records.Select(r => new ViewerPromiseRecord(r)).ToList().AsReadOnly();
        public IReadOnlyList<ViewerPromiseContext> Known(string viewerId) => _records
            .Where(r => r.Knowledge.Any(k => k.ViewerId == viewerId)).Select(r => Context(r, r.Knowledge.First(k => k.ViewerId == viewerId))).ToList().AsReadOnly();

        public bool Add(string eventKey, PromiseProposal proposal, EventWitnesses witnesses)
        {
            if (proposal == null || witnesses == null || string.IsNullOrEmpty(eventKey) || eventKey.Length > 200 || proposal.Created < _admissionFloor) return false;
            string id = "promise." + ChatContextBuilder.StableHash(eventKey).ToString("x16");
            if (_records.Any(r => r.Id == id || r.Action == proposal.Action && r.Subject == proposal.Subject &&
                r.WindowStart == proposal.WindowStart && r.Deadline == proposal.Deadline)) return false;
            if (_records.Count >= Capacity) return false;
            var record = new ViewerPromiseSnapshot { Id = id, Action = proposal.Action, Subject = proposal.Subject,
                Created = proposal.Created, WindowStart = proposal.WindowStart, Deadline = proposal.Deadline,
                Condition = proposal.Condition, Threshold = proposal.Threshold, VerifiableDeadline = proposal.VerifiableDeadline };
            foreach (var witness in witnesses.Viewers)
            {
                if (!witness.ViewerId.StartsWith("viewer.", StringComparison.Ordinal) || record.Witnesses.Contains(witness.ViewerId)) continue;
                record.Witnesses.Add(witness.ViewerId);
                record.Knowledge.Add(new PromiseKnowledgeSnapshot { ViewerId = witness.ViewerId });
            }
            _records.Add(record); return true;
        }

        public void Observe(PromiseGameplayFact fact)
        {
            if (fact == null) throw new ArgumentNullException(nameof(fact));
            foreach (var record in _records)
            {
                if (record.Status != PromiseStatus.Open || record.Action != fact.Action || record.Subject != fact.Subject ||
                    fact.GameMinutes < record.WindowStart || fact.GameMinutes > record.Deadline) continue;
                bool fulfilled = record.Condition == PromiseCondition.Occurrence || fact.Value < record.Threshold;
                Transition(record, fulfilled ? PromiseStatus.Fulfilled : PromiseStatus.Broken, fact.GameMinutes, fact.Witnesses);
            }
        }

        public void Advance(double now, EventWitnesses witnesses)
        {
            if (!Time(now) || witnesses == null) throw new ArgumentException("Invalid promise clock.");
            foreach (var record in _records)
                if (record.Status == PromiseStatus.Open && now > record.Deadline)
                    Transition(record, record.VerifiableDeadline ? PromiseStatus.Broken : PromiseStatus.Expired, now, witnesses);
            for (int i = _records.Count - 1; i >= 0; i--)
            {
                var r = _records[i];
                if (r.Status == PromiseStatus.Open || now - r.TerminalMinutes < 7 * 1440) continue;
                _admissionFloor = Math.Max(_admissionFloor, r.Created + .000001);
                _records.RemoveAt(i);
            }
        }

        private void Transition(ViewerPromiseSnapshot record, PromiseStatus status, double now, EventWitnesses witnesses)
        {
            record.Status = status; record.TerminalMinutes = now;
            foreach (var knowledge in record.Knowledge)
            {
                if (!witnesses.Contains(knowledge.ViewerId)) continue;
                knowledge.Status = status; knowledge.TerminalMinutes = now;
                if (status != PromiseStatus.Expired) KnownOutcome?.Invoke(knowledge.ViewerId, status == PromiseStatus.Fulfilled ? 2 : -2);
            }
        }

        public ViewerPromiseContext Retrieve(string viewerId, string subject, double now)
        {
            for (int i = _records.Count - 1; i >= 0; i--)
            {
                var r = _records[i];
                if (subject != r.Subject) continue;
                var k = r.Knowledge.FirstOrDefault(v => v.ViewerId == viewerId);
                if (k == null || k.References >= 3 || k.LastReferenceMinutes >= 0 && now - k.LastReferenceMinutes < 120 ||
                    _reservations.ContainsKey(viewerId + "/" + r.Id)) continue;
                return Context(r, k);
            }
            return null;
        }
        internal ViewerPromiseContext Reserve(string viewerId, string subject, double now, long intent)
        {
            var context = Retrieve(viewerId, subject, now);
            if (context != null) _reservations[viewerId + "/" + context.Id] = intent;
            return context;
        }
        internal void Finish(string viewerId, long intent, ViewerPromiseContext context, string published, double now) =>
            FinishPlanned(viewerId, intent, context, published, now, null);

        internal void FinishPlanned(string viewerId, long intent, ViewerPromiseContext context, string published, double now, string currentSubject)
        {
            if (context == null) return;
            string key = viewerId + "/" + context.Id;
            if (!_reservations.TryGetValue(key, out long owner) || owner != intent) return;
            _reservations.Remove(key);
            if (published == null || !ViewerPromiseVocabulary.References(published, context, currentSubject)) return;
            var k = _records.FirstOrDefault(r => r.Id == context.Id)?.Knowledge.FirstOrDefault(v => v.ViewerId == viewerId);
            if (k != null) { k.References = Math.Min(3, k.References + 1); k.LastReferenceMinutes = now; }
        }
        internal void ClearReservations() => _reservations.Clear();
        private static ViewerPromiseContext Context(ViewerPromiseSnapshot r, PromiseKnowledgeSnapshot k) => new(r, k.Status, k.TerminalMinutes);
        public ViewerPromiseLedgerSnapshot Capture() => new() { AdmissionFloor = _admissionFloor, Records = _records.Select(Clone).ToList() };
        public static string Validate(ViewerPromiseLedgerSnapshot snapshot, ISet<string> knownIds)
        {
            if (snapshot == null) return null;
            if (!Time(snapshot.AdmissionFloor) || snapshot.Records == null || snapshot.Records.Count > Capacity) return "Invalid promise ledger.";
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var r in snapshot.Records)
            {
                if (r == null || r.Id == null || r.Id.Length != 24 || !r.Id.StartsWith("promise.") ||
                    !r.Id.Substring(8).All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f') || !ids.Add(r.Id) ||
                    !Identifier(r.Subject) || !Enum.IsDefined(typeof(PromiseAction), r.Action) || !Enum.IsDefined(typeof(PromiseCondition), r.Condition) ||
                    !Enum.IsDefined(typeof(PromiseStatus), r.Status) || !Time(r.Created) || !Time(r.WindowStart) || !Time(r.Deadline) ||
                    r.WindowStart < r.Created || r.Deadline <= r.WindowStart || !Time(r.Threshold) || !Time(r.TerminalMinutes) ||
                    (r.Status == PromiseStatus.Open ? r.TerminalMinutes != 0 : r.TerminalMinutes < r.Created) ||
                    !ReachableTerminal(r) ||
                    r.Witnesses == null || r.Knowledge == null || r.Witnesses.Count > knownIds.Count || r.Knowledge.Count != r.Witnesses.Count)
                    return "Invalid saved promise.";
                var witnesses = new HashSet<string>(StringComparer.Ordinal);
                foreach (string id in r.Witnesses) if (id == null || !knownIds.Contains(id) || !witnesses.Add(id)) return "Unknown promise witness.";
                foreach (var k in r.Knowledge)
                    if (k == null || !witnesses.Remove(k.ViewerId ?? "") || !Enum.IsDefined(typeof(PromiseStatus), k.Status) ||
                        k.Status != PromiseStatus.Open && k.Status != r.Status || k.References < 0 || k.References > 3 ||
                        !Time(k.TerminalMinutes) || (k.Status == PromiseStatus.Open ? k.TerminalMinutes != 0 : k.TerminalMinutes != r.TerminalMinutes) ||
                        double.IsNaN(k.LastReferenceMinutes) || double.IsInfinity(k.LastReferenceMinutes) ||
                        (k.References == 0 ? k.LastReferenceMinutes != -1 : k.LastReferenceMinutes < r.Created)) return "Invalid promise knowledge.";
            }
            return null;
        }
        private static bool ReachableTerminal(ViewerPromiseSnapshot record)
        {
            bool inWindow = record.TerminalMinutes >= record.WindowStart && record.TerminalMinutes <= record.Deadline;
            return record.Status switch
            {
                PromiseStatus.Open => true,
                PromiseStatus.Fulfilled => inWindow && (record.Condition == PromiseCondition.Occurrence || record.Threshold > 0),
                PromiseStatus.Broken => record.Condition == PromiseCondition.ValueBelow && inWindow ||
                    record.VerifiableDeadline && record.TerminalMinutes > record.Deadline,
                PromiseStatus.Expired => !record.VerifiableDeadline && record.TerminalMinutes > record.Deadline,
                _ => false
            };
        }
        internal void Restore(ViewerPromiseLedgerSnapshot snapshot)
        { _records.Clear(); _reservations.Clear(); _admissionFloor = snapshot?.AdmissionFloor ?? 0;
            if (snapshot != null) _records.AddRange(snapshot.Records.Select(Clone)); }
        private static ViewerPromiseSnapshot Clone(ViewerPromiseSnapshot r) => new()
        {
            Id = r.Id, Action = r.Action, Subject = r.Subject, Condition = r.Condition, Created = r.Created, WindowStart = r.WindowStart,
            Deadline = r.Deadline, Threshold = r.Threshold, TerminalMinutes = r.TerminalMinutes, VerifiableDeadline = r.VerifiableDeadline, Status = r.Status,
            Witnesses = new List<string>(r.Witnesses), Knowledge = r.Knowledge.Select(k => new PromiseKnowledgeSnapshot {
                ViewerId = k.ViewerId, Status = k.Status, TerminalMinutes = k.TerminalMinutes, References = k.References, LastReferenceMinutes = k.LastReferenceMinutes }).ToList()
        };
        internal static bool Time(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0;
        internal static bool Identifier(string value) => !string.IsNullOrEmpty(value) && value.Length <= 64 && value.All(c => c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '.' || c == '-');
    }
}
