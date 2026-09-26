using System;
using System.Collections.Generic;
using System.Linq;
using GoLive.Desktop;

namespace GoLive.Viewers
{
    [Serializable] public sealed class PromotedViewerSnapshot
    {
        public string Id, SourceIdentity, DisplayName, Personality;
        public ViewerLanguage Language;
        public StreamTopic Interests;
        public float Talkativeness, Pace, Speed;
    }

    // Transient eligibility only for the roster's existing <=128 ephemeral identities. One seeded roll
    // per qualifying chatter, <=1 success per broadcast, <=8 durable descriptors; no model-authored identity.
    internal sealed class ViewerPromotion
    {
        private sealed class Observation { public readonly HashSet<string> Lines = new(); public bool Rolled; }
        private readonly AudienceRoster _roster;
        private readonly Dictionary<string, Observation> _observations = new(StringComparer.Ordinal);
        private readonly List<PromotedViewerSnapshot> _promoted = new();
        private readonly List<string> _departed = new();
        private string _broadcast = "";
        private ulong _seed;
        private bool _accepted;
        public ViewerPromotion(AudienceRoster roster) => _roster = roster;
        public void Begin(string broadcast, ulong seed) { End(); _broadcast = broadcast; _seed = seed; }
        public void End() { _observations.Clear(); _broadcast = ""; _accepted = false; }
        public void Prune()
        {
            _departed.Clear();
            foreach (string id in _observations.Keys) if (!_roster.IsWatching(id)) _departed.Add(id);
            foreach (string id in _departed) _observations.Remove(id);
        }
        public PromotedViewerSnapshot Observe(StreamChatMessage message, IReadOnlyList<ViewerProfile> existing)
        {
            Prune();
            if (message == null || _accepted || _promoted.Count >= 8 || _broadcast.Length == 0) return null;
            ChatParticipant chatter = _roster.Find(message.ViewerId);
            if (chatter == null || chatter.IsPermanent || !_roster.IsWatching(chatter.ViewerId)) return null;
            if (!_observations.TryGetValue(chatter.ViewerId, out var observation))
                _observations.Add(chatter.ViewerId, observation = new Observation());
            if (observation.Lines.Count < 3) observation.Lines.Add(message.Id);
            if (observation.Rolled || observation.Lines.Count < 3 || message.StreamSeconds - _roster.JoinedAt(chatter.ViewerId) < 480) return null;
            observation.Rolled = true;
            string source = ChatContextBuilder.StableHash(_broadcast + "/" + chatter.ViewerId).ToString("x16");
            double roll = (AudienceRandom.Hash(_seed, ChatContextBuilder.StableHash(source)) >> 11) / 9007199254740992d;
            if (roll >= .02 || existing.Any(p => string.Equals(p.DisplayName, chatter.DisplayName, StringComparison.OrdinalIgnoreCase))) return null;
            return new PromotedViewerSnapshot { Id = "viewer.promoted." + source, SourceIdentity = source,
                DisplayName = chatter.DisplayName, Personality = chatter.Persona.Personality, Language = chatter.Persona.Language,
                Interests = chatter.Traits.Interests, Talkativeness = chatter.Traits.Talkativeness, Pace = chatter.Traits.Pace, Speed = chatter.Traits.Speed };
        }
        public void Accept(PromotedViewerSnapshot descriptor) { _accepted = true; _promoted.Add(Clone(descriptor)); }
        public List<PromotedViewerSnapshot> Capture() => _promoted.Select(Clone).ToList();
        public void Restore(List<PromotedViewerSnapshot> descriptors)
        { End(); _promoted.Clear(); if (descriptors != null) _promoted.AddRange(descriptors.Select(Clone)); }
        public static ViewerProfile Profile(PromotedViewerSnapshot d) => new()
        {
            Id = d.Id, DisplayName = d.DisplayName, Personality = d.Personality, Language = d.Language, Interests = d.Interests,
            Talkativeness = d.Talkativeness, Pace = d.Pace, ResponseSpeed = d.Speed,
            EventAffinity = Enumerable.Repeat(1f, ViewerProfile.AffinityKinds.Count).ToArray(),
            Schedule = new ScheduleTendency { StartHour = 0, EndHour = 23, Regularity = .5f },
            Style = new ChatStyle { MinimumWords = 2, MaximumWords = 8 }, SocialTendency = .25f
        };
        public static string Validate(List<PromotedViewerSnapshot> descriptors, IReadOnlyList<ViewerProfile> authored)
        {
            if (descriptors == null) return null;
            if (descriptors.Count > 8) return "Too many promoted viewers.";
            var ids = new HashSet<string>(authored.Select(p => p.Id), StringComparer.Ordinal);
            var names = new HashSet<string>(authored.Select(p => p.DisplayName), StringComparer.OrdinalIgnoreCase);
            foreach (var d in descriptors)
            {
                if (d == null || d.SourceIdentity == null || d.SourceIdentity.Length != 16 || !d.SourceIdentity.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f') ||
                    d.Id != "viewer.promoted." + d.SourceIdentity || !ids.Add(d.Id) || string.IsNullOrWhiteSpace(d.DisplayName) || d.DisplayName.Length > 32 ||
                    d.DisplayName.Any(char.IsControl) || !names.Add(d.DisplayName) || string.IsNullOrWhiteSpace(d.Personality) || d.Personality.Length > 600 ||
                    !Enum.IsDefined(typeof(ViewerLanguage), d.Language) || (d.Interests & ~(StreamTopic)63) != 0 ||
                    !(d.Talkativeness > 0 && d.Talkativeness <= 1) || !(d.Pace >= .5f && d.Pace <= 4) || !(d.Speed >= .4f && d.Speed <= 2.5f))
                    return "Invalid promoted identity.";
            }
            return null;
        }
        private static PromotedViewerSnapshot Clone(PromotedViewerSnapshot d) => new() { Id = d.Id, SourceIdentity = d.SourceIdentity,
            DisplayName = d.DisplayName, Personality = d.Personality, Language = d.Language, Interests = d.Interests,
            Talkativeness = d.Talkativeness, Pace = d.Pace, Speed = d.Speed };
    }
}
