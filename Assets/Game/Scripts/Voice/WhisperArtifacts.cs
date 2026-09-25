using System;
using System.Text;

namespace GoLive.Voice
{
    // Whisper was trained on subtitles: on noise, music or tones it can emit sound tags ("[Bell]", "(музыка)")
    // or subtitle credits ("Редактор субтитров ...", "Subtitles by the Amara.org community"). Those are never
    // the player's speech. Ordinary phrases pass through unchanged apart from whitespace.
    public static class WhisperArtifacts
    {
        private static readonly string[] CreditMarkers =
        {
            "субтитр", "корректор а.", "dimatorzok", "amara.org", "subtitles by", "captioned by", "transcribed by"
        };

        public static string Clean(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            var kept = new StringBuilder(text.Length);
            char closing = '\0';
            foreach (char character in text)
            {
                if (closing != '\0')
                {
                    if (character == closing) closing = '\0';
                    continue;
                }
                closing = character switch { '[' => ']', '(' => ')', '{' => '}', '*' => '*', _ => '\0' };
                if (closing != '\0' || character == '♪' || character == '♫') continue;
                kept.Append(char.IsWhiteSpace(character) ? ' ' : character);
            }
            string cleaned = Collapse(kept.ToString());
            if (!HasLetterOrDigit(cleaned)) return "";
            string lower = cleaned.ToLowerInvariant();
            foreach (string marker in CreditMarkers)
                if (lower.Contains(marker, StringComparison.Ordinal)) return "";
            return cleaned;
        }

        private static string Collapse(string text)
        {
            var builder = new StringBuilder(text.Length);
            bool space = false;
            foreach (char character in text.Trim())
            {
                if (character == ' ')
                {
                    if (!space) builder.Append(' ');
                    space = true;
                    continue;
                }
                space = false;
                builder.Append(character);
            }
            return builder.ToString();
        }

        private static bool HasLetterOrDigit(string text)
        {
            foreach (char character in text)
                if (char.IsLetterOrDigit(character)) return true;
            return false;
        }
    }
}
