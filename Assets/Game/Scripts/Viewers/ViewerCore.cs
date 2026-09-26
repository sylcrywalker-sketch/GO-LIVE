using System;
using System.Collections.Generic;
using GoLive.Desktop;
using GoLive.PcBuilding;

namespace GoLive.Viewers
{
    // The current broadcast's living chat in one explicit flow:
    //   stream facts -> StreamEventSource (normalized, keyed events) -> ReactionSelector (who reacts, when, or silence)
    //   -> pending reaction intents -> due intents handed to the presenter.
    // Owns only transient per-broadcast state; gameplay truth (audience, money, follows) stays with its owners.
    public sealed class ViewerCore : IDisposable
    {
        private const ulong SelectionSalt = 0x5649455745525321UL;
        private readonly StreamSession _stream;
        private readonly ReactionTuning _tuning;
        private readonly List<StreamEvent> _events = new();
        private readonly List<ReactionIntent> _pending = new();
        private readonly List<ReactionIntent> _due = new();
        private ReactionSelector _selector;
        private string _broadcast = "";

        public AudienceRoster Roster { get; }
        public StreamEventSource Events { get; }
        public ReactionLog Log { get; } = new();
        public IReadOnlyList<ReactionIntent> Pending { get; }
        public ReactionSelector Selector => _selector;
        // Receives intents whose time has come; returns false when nothing could be shown for it.
        public Func<ReactionIntent, bool> Present { get; set; }

        public ViewerCore(StreamSession stream, StreamSpeechFeed speech, DonationAccount donations, PcPeripherals peripherals,
            TrichChannel channel, ReactionTuning tuning)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
            string error = tuning.Validate();
            if (error != null) throw new ArgumentException(error, nameof(tuning));
            Roster = new AudienceRoster(EphemeralViewers.Create);
            Events = new StreamEventSource(stream, speech, donations, peripherals, channel, Roster, tuning);
            Pending = _pending.AsReadOnly();
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
                    _pending.Add(intent);
                    Log.Add(ReactionLog.ForIntent(intent, ReactionOutcome.Scheduled, intent.Direct ? "direct" : "chat"));
                }
            }
            _events.Clear();

            _due.Clear();
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                ReactionIntent intent = _pending[i];
                if (intent.DueSeconds > now) continue;
                _pending.RemoveAt(i);
                if (!Roster.IsWatching(intent.Viewer.ViewerId))
                    Log.Add(ReactionLog.ForIntent(intent, ReactionOutcome.Dropped, "viewer left"));
                else if (now > intent.ExpiresSeconds)
                    Log.Add(ReactionLog.ForIntent(intent, ReactionOutcome.Dropped, "stale"));
                else _due.Add(intent);
            }
            _due.Sort((a, b) => a.DueSeconds.CompareTo(b.DueSeconds));
            foreach (ReactionIntent intent in _due)
                if (Present == null || !Present(intent))
                    Log.Add(ReactionLog.ForIntent(intent, ReactionOutcome.Discarded, Present == null ? "no presenter" : "not shown"));
        }

        public void Dispose() => Events.Dispose();

        private void BeginBroadcast()
        {
            _broadcast = Events.BroadcastId;
            _pending.Clear();
            Roster.Clear();
            Roster.SetAudienceSize(_stream.Audience.CurrentViewers);
            var random = new AudienceRandom(AudienceRandom.Hash(_stream.Audience.Seed, SelectionSalt));
            _selector = new ReactionSelector(_tuning, Roster, random);
        }

        private void EndBroadcast()
        {
            if (_broadcast.Length == 0) return;
            foreach (ReactionIntent intent in _pending) Log.Add(ReactionLog.ForIntent(intent, ReactionOutcome.Dropped, "broadcast ended"));
            _pending.Clear();
            _broadcast = "";
            Roster.Clear();
        }
    }
}
