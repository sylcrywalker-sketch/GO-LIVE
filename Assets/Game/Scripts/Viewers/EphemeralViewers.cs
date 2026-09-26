using System;
using GoLive.Desktop;

namespace GoLive.Viewers
{
    // Lightweight anonymous chatters drawn from the aggregated audience for one broadcast: a plausible nickname
    // and generic reaction traits. No relationships, memory or save record unless one is promoted.
    public static class EphemeralViewers
    {
        private static readonly string[] Stems =
        {
            "kotik", "dimon", "sanya", "lexa", "vanek", "zhenya", "tapok", "ded", "ghost", "shadow", "pelmen", "borsch", "semechka",
            "mops", "kefir", "sonya", "noob", "tank", "lucky", "grisha", "vovan", "masha", "pavlik", "frosty", "pixel", "bublik",
            "rogue", "koshka", "arbuz", "morty", "sleepy", "mrak", "zayka", "kolyan", "stepa", "lisa", "vortex", "keks", "tolik"
        };
        private static readonly string[] Suffixes = { "", "_", "", "x", "_tv", "_play", "", "228", "_ru", "_", "" };

        public static ChatParticipant Create(AudienceRandom random, int index)
        {
            string stem = Stems[random.NextInt(Stems.Length)];
            string suffix = Suffixes[random.NextInt(Suffixes.Length)];
            string digits = random.NextDouble() < .55 ? random.NextInt(2) == 0 ? (random.NextInt(90) + 10).ToString() : (random.NextInt(9000) + 1000).ToString() : "";
            string name = stem + suffix + digits;
            if (random.NextDouble() < .3) name = char.ToUpperInvariant(name[0]) + name.Substring(1);
            StreamTopic interests = (StreamTopic)(1 << random.NextInt(6));
            var traits = new ReactionTraits(.25f + .6f * (float)random.NextDouble(), 1f + (float)random.NextDouble(),
                .8f + .6f * (float)random.NextDouble(), interests);
            return new ChatParticipant("anon." + index + "." + name, name, false, traits);
        }
    }
}
