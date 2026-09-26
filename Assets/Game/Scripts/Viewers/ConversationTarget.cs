namespace GoLive.Viewers
{
    public enum ConversationTargetKind { None, SpecificViewer, ActiveThreadViewer, Group }

    // The recipient of the streamer's speech, not the person who happens to win a reply roll.
    // Transient, immutable and resolved once before selection; it owns no runtime state.
    public readonly struct ConversationTarget
    {
        public ConversationTargetKind Kind { get; }
        public string ViewerId { get; }
        public long PresenceEpoch { get; }
        public bool IsSpecific => Kind == ConversationTargetKind.SpecificViewer || Kind == ConversationTargetKind.ActiveThreadViewer;

        internal ConversationTarget(ConversationTargetKind kind, string viewerId = null, long presenceEpoch = 0)
        { Kind = kind; ViewerId = viewerId; PresenceEpoch = presenceEpoch; }

        public override string ToString() => Kind switch
        {
            ConversationTargetKind.SpecificViewer => ViewerId + " [explicit]",
            ConversationTargetKind.ActiveThreadViewer => ViewerId + " [active-thread]",
            ConversationTargetKind.Group => "group",
            _ => "none"
        };
    }
}
