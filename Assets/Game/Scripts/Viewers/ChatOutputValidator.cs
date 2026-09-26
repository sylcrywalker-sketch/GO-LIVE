using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace GoLive.Viewers
{
    public readonly struct ChatValidation
    {
        public bool Accepted { get; }
        public string Text { get; }
        public string Reason { get; }

        private ChatValidation(bool accepted, string text, string reason)
        {
            Accepted = accepted;
            Text = text;
            Reason = reason;
        }

        public static ChatValidation Accept(string text) => new(true, text, null);
        public static ChatValidation Reject(string reason) => new(false, null, reason);
    }

    // Every model result is untrusted presentation text. Small repairs only (quotes, a name prefix, spaces);
    // anything that sounds like an assistant, leaks the prompt, claims gameplay, repeats the chat or breaks the
    // viewer's language is rejected. A rejected message is discarded or replaced by fallback, never re-generated.
    public static class ChatOutputValidator
    {
        public const int MaximumCharacters = 150;
        private const int MaximumSentences = 3;
        private const int MaximumEmoji = 2;

        private static readonly string[] AssistantPhrases =
        {
            "as an ai", "as a language model", "language model", "i'm an ai", "i am an ai", "as a viewer", "as the viewer",
            "system prompt", "instruction", "here's a ", "here is a ", "sure! here", "certainly!", "i can't help with", "i cannot help",
            "viewer response", "chat message:", "response:", "my message", "как ии", "как языковая", "языковая модель", "я ии",
            "я — ии", "инструкци", "промпт", "вот сообщение", "вот реакция", "вот мой", "ответ зрителя", "сообщение зрителя",
            "great job", "keep it up", "you've got this", "you got this", "what an amazing", "amazing moment", "that was incredible",
            "so proud of you", "keep up the great", "так держать", "отличная работа", "продолжай в том же духе", "ты справишься",
            "горжусь тобой", "молодец, продолжай"
        };
        private static readonly Regex Money = new(@"(\$\s?\d|\d\s?(\$|₽|руб|р\.|бакс|доллар|usd|rub|bucks|dollars))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex Support = new(@"(донат|задонат|закинул|подписал|подписк|сабнул|фоллов|фолловнул|donat|subscrib|\bsubbed\b|\bfollowed\b|gifted)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex Markup = new(@"(```|\*\*|__|^#+\s|^\s*[-*•]\s|<\/?data>|[{}\[\]])", RegexOptions.Multiline | RegexOptions.CultureInvariant);
        // Word-start matches only ("рубля" and "корабля" are not "бля").
        private static readonly Regex StrongProfanity = new(@"\b(бля|сук[аи]|хуй|хуе|хуё|хуя|пизд|ебат|ебан|ебал|ёб|еби|ебу|нахуй|похуй|fuck|shit|bitch|cunt|dick)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex MildProfanity = new(@"\b(блин\w*|капец|жесть|хрен\w*|фиг|фига|фигня|нафиг|черт|чёрт|черти|damn|hell|crap)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex RoleLabel = new(@"^\s*(viewer|chat|message|user|assistant|зритель|сообщение|ответ)\s*[:\-–]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static ChatValidation Validate(string raw, ReactionIntent intent, IReadOnlyList<StreamChatMessage> recentChat)
        {
            if (intent == null) throw new ArgumentNullException(nameof(intent));
            if (raw == null) return ChatValidation.Reject("empty");
            string text = Repair(raw, intent.Viewer.DisplayName);
            if (text.Length == 0) return ChatValidation.Reject("empty");
            if (text.Length > MaximumCharacters) return ChatValidation.Reject("too long");
            if (text.IndexOf('\n') >= 0 || text.IndexOf('\r') >= 0) return ChatValidation.Reject("multiple lines");
            if (Markup.IsMatch(text)) return ChatValidation.Reject("markup or prompt leakage");
            if (RoleLabel.IsMatch(text) || text.StartsWith("as " + intent.Viewer.DisplayName, StringComparison.OrdinalIgnoreCase))
                return ChatValidation.Reject("role label");
            string lower = text.ToLowerInvariant();
            foreach (string phrase in AssistantPhrases)
                if (lower.Contains(phrase)) return ChatValidation.Reject("assistant voice: " + phrase);
            if (Sentences(text) > MaximumSentences) return ChatValidation.Reject("too many sentences");
            // A viewer's messages stay near their own typical length (a talkative viewer may run a little over).
            ViewerPersona persona = intent.Viewer.Persona;
            if (SpeechRelevance.Tokens(text).Count > Math.Max(persona.MaximumWords + 4, (int)Math.Ceiling(persona.MaximumWords * 1.5)))
                return ChatValidation.Reject("too long for this viewer");
            if (Emoji(text) > MaximumEmoji) return ChatValidation.Reject("emoji spam");
            Profanity allowed = intent.Viewer.Persona.Profanity;
            if (allowed != Profanity.Strong && StrongProfanity.IsMatch(text)) return ChatValidation.Reject("profanity above this viewer");
            if (allowed == Profanity.None && MildProfanity.IsMatch(text)) return ChatValidation.Reject("profanity above this viewer");
            if (text[0] == '/' || text[0] == '!') return ChatValidation.Reject("command");
            if (Money.IsMatch(text)) return ChatValidation.Reject("money claim");
            bool ownSupport = intent.Direct && intent.Event.SubjectViewerId == intent.Viewer.ViewerId &&
                (intent.Event.Kind == StreamEventKind.Donation || intent.Event.Kind == StreamEventKind.Follow || intent.Event.Kind == StreamEventKind.Subscription);
            bool aboutSupport = intent.Event.Kind == StreamEventKind.Donation || intent.Event.Kind == StreamEventKind.Follow ||
                intent.Event.Kind == StreamEventKind.Subscription || (intent.Event.Speech?.Topics & StreamTopic.Money) != 0 ||
                (intent.Event.Speech?.Topics & StreamTopic.Community) != 0;
            if (!ownSupport && !aboutSupport && Support.IsMatch(text)) return ChatValidation.Reject("support claim");
            string languageError = Language(text, intent.Viewer.Persona.Language);
            if (languageError != null) return ChatValidation.Reject(languageError);
            string echo = intent.Event.Speech?.Text;
            if (echo != null && Echoes(text, echo)) return ChatValidation.Reject("repeats the streamer");
            string duplicate = Duplicate(text, recentChat);
            if (duplicate != null) return ChatValidation.Reject(duplicate);
            return ChatValidation.Accept(text);
        }

        // Only cosmetic repairs: surrounding quotes, the viewer's own name as a prefix, whitespace.
        internal static string Repair(string raw, string displayName)
        {
            string text = Regex.Replace(raw.Trim(), @"[ \t]{2,}", " ");
            for (int i = 0; i < 2 && text.Length >= 2; i++)
            {
                char first = text[0], last = text[text.Length - 1];
                if ((first == '"' && last == '"') || (first == '«' && last == '»') || (first == '\'' && last == '\'') || (first == '“' && last == '”'))
                    text = text.Substring(1, text.Length - 2).Trim();
            }
            if (!string.IsNullOrEmpty(displayName) && text.StartsWith(displayName, StringComparison.OrdinalIgnoreCase))
            {
                string rest = text.Substring(displayName.Length).TrimStart();
                if (rest.Length > 0 && (rest[0] == ':' || rest[0] == '-' || rest[0] == '–')) text = rest.Substring(1).Trim();
            }
            return text;
        }

        private static string Language(string text, ViewerLanguage language)
        {
            int cyrillic = 0, latin = 0;
            foreach (char c in text)
            {
                if (c >= '\u0400' && c <= '\u04FF') cyrillic++;
                else if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')) latin++;
            }
            int letters = cyrillic + latin;
            // Short universal tokens ("?", "gg", "lol", "o7") fit any chat.
            if (letters <= 3) return null;
            if (language == ViewerLanguage.Russian && cyrillic < latin) return "not Russian";
            if (language == ViewerLanguage.English && cyrillic > 0) return "not English";
            return null;
        }

        private static bool Echoes(string text, string speech)
        {
            List<string> words = SpeechRelevance.Tokens(text);
            if (words.Count < 3) return false;
            var spoken = new HashSet<string>(SpeechRelevance.Tokens(speech));
            int shared = 0;
            foreach (string word in words)
                if (spoken.Contains(word)) shared++;
            return shared >= words.Count * .8;
        }

        private static string Duplicate(string text, IReadOnlyList<StreamChatMessage> recentChat)
        {
            if (recentChat == null || recentChat.Count == 0) return null;
            string normalized = SpeechRelevance.Normalize(text);
            List<string> words = SpeechRelevance.Tokens(text);
            for (int i = recentChat.Count - 1, seen = 0; i >= 0 && seen < 12; i--, seen++)
            {
                string other = SpeechRelevance.Normalize(recentChat[i].Text);
                if (seen < 8 && normalized.Length > 0 && other == normalized) return "duplicate of recent message";
                if (words.Count >= 3 && Similarity(words, SpeechRelevance.Tokens(recentChat[i].Text)) >= .75) return "near-duplicate of recent message";
            }
            return null;
        }

        private static double Similarity(List<string> a, List<string> b)
        {
            var left = new HashSet<string>(a);
            var right = new HashSet<string>(b);
            if (left.Count == 0 || right.Count == 0) return 0;
            int shared = 0;
            foreach (string word in left)
                if (right.Contains(word)) shared++;
            return shared / (double)(left.Count + right.Count - shared);
        }

        private static int Sentences(string text)
        {
            int count = 0;
            bool inSentence = false;
            foreach (char c in text)
            {
                if (c == '.' || c == '!' || c == '?')
                {
                    if (inSentence) count++;
                    inSentence = false;
                }
                else if (char.IsLetterOrDigit(c)) inSentence = true;
            }
            return count + (inSentence ? 1 : 0);
        }

        private static int Emoji(string text)
        {
            int count = 0;
            for (int i = 0; i < text.Length; i++)
            {
                int code;
                if (char.IsSurrogatePair(text, i)) code = char.ConvertToUtf32(text[i], text[++i]);
                else if (char.IsSurrogate(text[i])) continue;
                else code = text[i];
                if ((code >= 0x1F300 && code <= 0x1FAFF) || (code >= 0x2600 && code <= 0x27BF)) count++;
            }
            return count;
        }
    }
}
