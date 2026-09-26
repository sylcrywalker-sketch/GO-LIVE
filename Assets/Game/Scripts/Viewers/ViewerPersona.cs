using System;

namespace GoLive.Viewers
{
    public enum ViewerLanguage { Russian, English, Mixed }

    // How a viewer writes, as bounded prompt data. Built from structured authored data for permanent viewers;
    // one of a few generic personas for anonymous chatters. Never the viewer's state or history.
    public sealed class ViewerPersona
    {
        public ViewerLanguage Language { get; }
        // Who they are, in a sentence or two.
        public string Personality { get; }
        // How they type: length, case, punctuation, laughter, slang, emoji.
        public string Style { get; }
        // Typical message length in words, for the prompt and for validating output.
        public int MinimumWords { get; }
        public int MaximumWords { get; }

        public ViewerPersona(ViewerLanguage language, string personality, string style, int minimumWords, int maximumWords)
        {
            if (string.IsNullOrWhiteSpace(personality)) throw new ArgumentException("A persona needs a personality.", nameof(personality));
            if (string.IsNullOrWhiteSpace(style)) throw new ArgumentException("A persona needs a style.", nameof(style));
            if (minimumWords < 1 || maximumWords < minimumWords || maximumWords > 30) throw new ArgumentOutOfRangeException(nameof(maximumWords));
            Language = language;
            Personality = personality.Trim();
            Style = style.Trim();
            MinimumWords = minimumWords;
            MaximumWords = maximumWords;
        }

        // Anonymous chatters: ordinary people from the aggregated audience, speaking the channel's language.
        internal static readonly (string personality, string style, int min, int max)[] Anonymous =
        {
            ("A quiet viewer who mostly lurks and writes only when something catches their eye.",
                "Very short: 1-4 words. All lowercase. No final punctuation. No emoji.", 1, 4),
            ("A random viewer with a dry sense of humor.",
                "Short: 2-7 words. Lowercase. Laughs rarely. No emoji.", 2, 7),
            ("A friendly, curious viewer who is new to this channel.",
                "Short: 3-9 words. Mostly lowercase. Sometimes asks a question. At most one emoji, usually none.", 3, 9),
            ("A viewer who jokes around and teases a little, never mean.",
                "Short: 2-8 words. Lowercase. Laughs like real chat (ахах / лол / lmao). No emoji.", 2, 8),
            ("A slightly skeptical viewer who is not impressed easily.",
                "Short: 2-8 words. Lowercase. Dry, a bit critical, never polite filler.", 2, 8)
        };

        public static ViewerPersona AnonymousPersona(int index, ViewerLanguage language)
        {
            var (personality, style, min, max) = Anonymous[Math.Abs(index) % Anonymous.Length];
            return new ViewerPersona(language, personality, style, min, max);
        }
    }
}
