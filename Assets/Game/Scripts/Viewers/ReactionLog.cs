using System;
using System.Collections.Generic;

namespace GoLive.Viewers
{
    public enum ReactionOutcome
    {
        Rejected,    // the event deserved no chat (low-value speech, busy chat, chance)
        Scheduled,   // a viewer was chosen; text follows at the due time
        Shown,       // a validated message reached the chat
        Discarded,   // generation failed validation or went stale; nothing was shown
        Dropped      // the broadcast ended or the viewer left before the message was due
    }

    public enum ReactionSource { None, LanguageModel, Fallback }

    // One line of the development trace: what happened, what C# decided, and what (if anything) was shown.
    public sealed class ReactionLogEntry
    {
        public double StreamSeconds { get; internal set; }
        public string EventKey { get; internal set; }
        public StreamEventKind EventKind { get; internal set; }
        public string Speech { get; internal set; }
        public float Relevance { get; internal set; }
        public ReactionOutcome Outcome { get; internal set; }
        public string Reason { get; internal set; }
        public string ViewerId { get; internal set; }
        public string ViewerName { get; internal set; }
        public long IntentId { get; internal set; }
        public ReactionSource Source { get; internal set; }
        public double LatencySeconds { get; internal set; }
        public int PromptCharacters { get; internal set; }
        public string Text { get; internal set; }
        public string CandidateIds { get; internal set; }
        public string SelectionReason { get; internal set; }
        public string Relationship { get; internal set; }
        public string MemoryIds { get; internal set; }
        public string PromiseId { get; internal set; }
    }

    // Bounded development trace of the most recent reaction decisions (never saved, never shown in Streamly).
    public sealed class ReactionLog
    {
        public const int Capacity = 80;
        private readonly List<ReactionLogEntry> _entries = new();

        public IReadOnlyList<ReactionLogEntry> Entries { get; }
        public event Action<ReactionLogEntry> Added;

        public ReactionLog() => Entries = _entries.AsReadOnly();

        public ReactionLogEntry Add(ReactionLogEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            if (_entries.Count == Capacity) _entries.RemoveAt(0);
            _entries.Add(entry);
            Added?.Invoke(entry);
            return entry;
        }

        public void Clear() => _entries.Clear();

        internal static ReactionLogEntry ForEvent(StreamEvent streamEvent, ReactionOutcome outcome, string reason) => new()
        {
            StreamSeconds = streamEvent.StreamSeconds,
            EventKey = streamEvent.Key,
            EventKind = streamEvent.Kind,
            Speech = streamEvent.Speech?.Text,
            Relevance = streamEvent.Significance,
            Outcome = outcome,
            Reason = reason
        };

        internal static ReactionLogEntry ForIntent(ReactionIntent intent, ReactionOutcome outcome, string reason)
        {
            ReactionLogEntry entry = ForEvent(intent.Event, outcome, reason);
            entry.IntentId = intent.Id;
            entry.ViewerId = intent.Viewer.ViewerId;
            entry.ViewerName = intent.Viewer.DisplayName;
            entry.CandidateIds = intent.CandidateIds;
            entry.SelectionReason = intent.Direct ? "direct target" : intent.Event.Kind == StreamEventKind.ViewerReply ? "published reply" : "weighted audience";
            return entry;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        internal static void Context(ReactionLogEntry entry, ChatSituation situation)
        {
            if (situation == null) return;
            entry.Relationship = situation.Relationship;
            var ids = new List<string>(situation.Memories.Count);
            foreach (var memory in situation.Memories) ids.Add(memory.MemoryId);
            entry.MemoryIds = string.Join(", ", ids);
            entry.PromiseId = situation.Promise?.Id;
        }
    }
}
