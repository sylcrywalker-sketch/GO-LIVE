using System;
using GoLive.Desktop;

namespace GoLive.Viewers
{
    // Names an already-authoritative outcome, never rolls an amount or creates a reward. The roster owns
    // seats; community owns the two durable once-only flags. No individual state for anonymous seats.
    public sealed class ViewerSupportAttribution
    {
        private readonly AudienceRoster _roster;
        private readonly ViewerCommunity _community;
        public ViewerSupportAttribution(AudienceRoster roster, ViewerCommunity community)
        { _roster = roster ?? throw new ArgumentNullException(nameof(roster)); _community = community ?? throw new ArgumentNullException(nameof(community)); }

        public ChatParticipant Choose(StreamEventKind kind, ulong seed, int serial, double now, int audienceSize)
        {
            if (kind != StreamEventKind.Donation && kind != StreamEventKind.Follow && kind != StreamEventKind.Subscription)
                throw new ArgumentOutOfRangeException(nameof(kind));
            // Audience may have shrunk since the last viewer tick. Reconcile before naming anyone.
            _roster.SetAudienceSize(audienceSize);
            var random = new AudienceRandom(AudienceRandom.Hash(seed, ((ulong)(uint)serial << 8) | (uint)kind));
            double total = _roster.AnonymousCount * .2;
            foreach (var viewer in _roster.Named) total += Weight(viewer, kind);
            if (total <= 0) return null;
            double roll = random.NextDouble() * total;
            foreach (var viewer in _roster.Named)
            {
                double weight = Weight(viewer, kind);
                if (weight <= 0) continue;
                roll -= weight;
                if (roll >= 0) continue;
                _community.RecordSupport(viewer.ViewerId, kind);
                return viewer;
            }
            // Donations can be accompanied by an ephemeral chatter. Follows/subscriptions stay anonymous:
            // retaining individual purchase/follow state for the aggregate would create another population.
            return kind == StreamEventKind.Donation ? _roster.AnonymousChatter(random, _ => true, now) : null;
        }

        public string DonationSender(ulong seed, int serial, double now, int audienceSize)
            => Choose(StreamEventKind.Donation, seed, serial, now, audienceSize)?.DisplayName ?? "Anonymous";

        private double Weight(ChatParticipant viewer, StreamEventKind kind)
        {
            var state = _community.State(viewer.ViewerId);
            var profile = viewer.Persona.Profile;
            if (state == null || profile == null || kind == StreamEventKind.Follow && state.HasFollowed ||
                kind == StreamEventKind.Subscription && state.HasSubscribed) return 0;
            return kind == StreamEventKind.Donation ? profile.DonationTendency : profile.FollowTendency;
        }
    }
}
