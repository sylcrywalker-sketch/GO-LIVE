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
        // Narrow first-person past-tense claims. Merely discussing support remains valid topic content.
        private const string OwnRu = @"(?:\bя\s+(?:(?:уже|только что|тебе|вам|тоже|сейчас)\s+){0,3}|^)";
        private const string OwnEn = @"\bi(?:['’]ve| have)?\s+(?:(?:just|already|also)\s+){0,2}";
        private static readonly Regex OwnDonation = new(OwnRu + @"(?:задонатил[аи]?|донатил[аи]?|закинул[аи]?)\b|" + OwnEn + @"(?:donated|tipped|gifted)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex OwnFollow = new(OwnRu + @"(?:зафолловил[аи]?|фолловнул[аи]?)\b|" + OwnEn + @"followed\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex OwnSubscription = new(OwnRu + @"(?:сабнул[аи]?|оформил[аи]? подписку)\b|" + OwnEn + @"(?:subscribed|subbed)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex OwnRussianSubscription = new(OwnRu + @"подписал(?:ся|ась)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex Markup = new(@"(```|\*\*|__|^#+\s|^\s*[-*•]\s|<\/?data>|[{}\[\]])", RegexOptions.Multiline | RegexOptions.CultureInvariant);
        // Word-start matches only ("рубля" and "корабля" are not "бля").
        private static readonly Regex StrongProfanity = new(@"\b(бля|сук[аи]|хуй|хуе|хуё|хуя|пизд|ебат|ебан|ебал|ёб|еби|ебу|нахуй|похуй|fuck|shit|bitch|cunt|dick)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex MildProfanity = new(@"\b(блин\w*|капец|жесть|хрен\w*|фиг|фига|фигня|нафиг|черт|чёрт|черти|damn|hell|crap)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex RoleLabel = new(@"^\s*(viewer|chat|message|user|assistant|зритель|сообщение|ответ)\s*[:\-–]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        // Specific claims need support in the plan's facts: an exact quantity of time or performance, a hardware
        // brand/model, or a long number. Ordinary chat numbers ("1v5", "10/10", "o7") are not claims of this kind.
        private const string CountWord = @"\d+|пол|дв[аеу]х?|две|тр[иеё]х?|четыр\w*|пят\w*|шест\w*|сем\w*|восем\w*|восьм\w*|девят\w*|десят\w*|" +
            @"двадцат\w*|тридцат\w*|сорок\w*|сто|two|three|four|five|six|seven|eight|nine|ten|fifteen|twenty|thirty|forty|fifty|hundred";
        private static readonly Regex Quantity = new(@"(?<!\w)(?<n>" + CountWord + @")\s*-?\s*(?:минут\w*|мин|час\w*|дн[еяию]\w*|день|недел\w*|месяц\w*|" +
            @"год\w*|лет|minutes?|mins?|hours?|hrs?|days?|weeks?|months?|years?|fps|фпс|гб|gb|мб|mb|тб|tb|гц|hz|ghz|ггц|ватт\w*|watts?)(?!\w)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex HardwareEntity = new(@"(?<!\w)(rtx|gtx|rx|radeon|geforce|nvidia|нвиди\w*|amd|intel|интел\w*|ryzen|райзен\w*|i[3579]|\d{3,5})(?!\w)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex Digits = new(@"\d+", RegexOptions.CultureInvariant);
        // Relationship claims above what game state supports. Romance is never a state of this game.
        private static readonly Regex Romance = new(@"(влюбил\w*|влюблен\w*|влюблён\w*|люблю тебя|тебя люблю|целую|in love|love you|luv u|marry me|женись на|😍|🥰|😘|💋|💕|💞|💖)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex Devotion = new(@"(любим\w* стрим\w*|обожаю тебя|скучал\w*|соскучил\w*|favou?rite streamer|missed you|miss you)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex Hearts = new(@"(❤|♥|💗|🫶)", RegexOptions.CultureInvariant);
        // "Again" asserts an earlier occurrence: the streamer's words, visible chat or a planned callback must back it.
        private static readonly Regex Recurrence = new(@"(?<!\w)(опять|снова)(?!\w)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex TryAgain = new(@"(?<!\w)(попроб\w*\s+снова|снова\s+попроб\w*)(?!\w)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex RecurrenceFact = new(@"(?<!\w)(опять|снова|again|ещё раз|еще раз)(?!\w)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        // What a viewer may claim to have done today, by kind. Checked per clause, only where the viewer talks about
        // themselves (a clause with "ты"/"вы" is about someone else: "а ты во что играл?").
        private static readonly (DayActivityKind kind, Regex claim)[] DayClaims =
        {
            (DayActivityKind.Work, Claim(@"на работе|с работы|работал\w*|отработал\w*|на смене|смену|в офисе|worked|at work|off shift")),
            (DayActivityKind.Study, Claim(@"учил(?:ся|ась)|на парах|с пар|универ\w*|в школе|из школы|со школы|лекци\w*|экзамен\w*|зач[её]т\w*|домашк\w*")),
            (DayActivityKind.Drawing, Claim(@"рисовал\w*|рисую|нарисовал\w*|скетч\w*")),
            (DayActivityKind.Gaming, Claim(@"играл\w*|поиграл\w*|катал[аи]?|каточк\w*|рубил(?:ся|ась)|played games|playing games|gaming")),
            (DayActivityKind.Sleep, Claim(@"спал[аи]?|выспал\w*|проспал\w*|дрых\w*|slept|overslept|napped")),
            (DayActivityKind.Errands, Claim(@"по делам|в магазин\w*|на рын\w*|в поликлиник\w*|в банк\w*")),
            (DayActivityKind.Housework, Claim(@"убирал\w*|уборк\w*|стирал\w*|стирк\w*|готовил\w*")),
            (DayActivityKind.Walk, Claim(@"гулял\w*|погулял\w*|на прогулк\w*")),
            (DayActivityKind.Tech, Claim(@"чинил\w*|починил\w*|собирал\w*|настраивал\w*"))
        };
        private static readonly Regex OtherPerson = new(@"(?<!\w)(ты|тебя|тебе|тобой|вы|вас|вам|сам|you)(?!\w)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex NegatedActivity = new(@"(?:^| )(?:не|not|never|didnt|havent|hasnt|wasnt|werent|dont|doesnt|cant|couldnt)$", RegexOptions.CultureInvariant);

        private static Regex Claim(string alternatives) =>
            new(@"(?<!\w)(?:" + alternatives + @")(?!\w)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static ChatValidation Validate(string raw, ReactionIntent intent, IReadOnlyList<StreamChatMessage> recentChat)
            => Validate(raw, intent, recentChat, null);

        public static ChatValidation Validate(string raw, ReactionIntent intent, IReadOnlyList<StreamChatMessage> recentChat, ChatSituation situation)
        {
            if (intent == null) throw new ArgumentNullException(nameof(intent));
            if (raw == null) return ChatValidation.Reject("empty");
            string promiseSubject = ViewerPromiseVocabulary.Subject(intent.Event);
            if (ViewerPromiseVocabulary.MentionsPromise(raw) && !ViewerPromiseVocabulary.References(raw, situation?.Promise, promiseSubject))
                return ChatValidation.Reject("unsupported promise claim");
            // Conservative phrase guard, not a semantic truth guarantee. Model text never enters the bank.
            if (ViewerMemoryBank.Historical(raw))
            {
                bool supported = ViewerPromiseVocabulary.References(raw, situation?.Promise, promiseSubject);
                string memorySubject = ViewerMemoryBank.Subject(intent.Event);
                if (situation != null) foreach (var memory in situation.Memories)
                    if (ViewerMemoryBank.References(raw, memory, memorySubject)) supported = true;
                if (!supported) return ChatValidation.Reject("unsupported historical claim");
            }
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
            // Structured grounding: the plan's envelope, not a topic blacklist, decides which specifics exist.
            string grounded = situation != null ? ViewerUtterancePlanner.For(intent, situation).GroundedText : Grounded(intent, recentChat);
            string claim = UngroundedClaim(text, grounded, ((intent.Event.Speech?.Topics ?? StreamTopic.None) & StreamTopic.Hardware) != 0);
            if (claim != null) return ChatValidation.Reject("ungrounded specific claim: " + claim);
            if (situation != null && situation.Memories.Count == 0 && situation.Promise == null && Recurrence.IsMatch(text) &&
                !TryAgain.IsMatch(text) && !RecurrenceFact.IsMatch(grounded) && !OtherChatRecurrence(intent, situation.RecentChat))
                return ChatValidation.Reject("unsupported recurrence");
            RelationshipTier tier = situation?.Tier ?? RelationshipTier.Neutral;
            if (Romance.IsMatch(text) || Devotion.IsMatch(text) && tier != RelationshipTier.Loyal || Hearts.IsMatch(text) && tier < RelationshipTier.Friendly)
                return ChatValidation.Reject("relationship claim above game state");
            bool ownSupport = intent.Direct && intent.Event.SubjectViewerId == intent.Viewer.ViewerId &&
                (intent.Event.Kind == StreamEventKind.Donation || intent.Event.Kind == StreamEventKind.Follow || intent.Event.Kind == StreamEventKind.Subscription);
            if (OwnDonation.IsMatch(text) && !(ownSupport && intent.Event.Kind == StreamEventKind.Donation) ||
                OwnFollow.IsMatch(text) && !(ownSupport && intent.Event.Kind == StreamEventKind.Follow) ||
                OwnSubscription.IsMatch(text) && !(ownSupport && intent.Event.Kind == StreamEventKind.Subscription) ||
                OwnRussianSubscription.IsMatch(text) && !(ownSupport && (intent.Event.Kind == StreamEventKind.Follow || intent.Event.Kind == StreamEventKind.Subscription)))
                return ChatValidation.Reject("unsupported own transaction");
            StreamTopic topics = intent.Event.Speech?.Topics ?? StreamTopic.None;
            bool aboutSupport = intent.Event.Kind == StreamEventKind.Donation || intent.Event.Kind == StreamEventKind.Follow ||
                intent.Event.Kind == StreamEventKind.Subscription || (topics & (StreamTopic.Money | StreamTopic.Community)) != 0;
            if (!ownSupport && !aboutSupport && Support.IsMatch(text)) return ChatValidation.Reject("support claim");
            if (situation?.Day != null && ViewerUtterancePlanner.For(intent, situation).Personal && ContradictsDay(text, situation.Day))
                return ChatValidation.Reject("contradicts your day");
            string languageError = Language(text, intent.Viewer.Persona.Language);
            if (languageError != null) return ChatValidation.Reject(languageError);
            string echo = intent.Event.Speech?.Text;
            if (echo != null && Echoes(text, echo)) return ChatValidation.Reject("repeats the streamer");
            string duplicate = Duplicate(text, recentChat);
            if (duplicate != null) return ChatValidation.Reject(duplicate);
            return ChatValidation.Accept(text);
        }

        // A viewer's own story about today stays the one C# gave them ("рисовала весь день" never turns into a day at work).
        internal static bool ContradictsDay(string text, ViewerDailyState day)
        {
            foreach (string clause in text.Split(new[] { ',', '.', '!', '?', ';', '(', ')', '—' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (OtherPerson.IsMatch(clause)) continue;
                foreach (var (kind, claim) in DayClaims)
                    if (!day.Allows(kind))
                        foreach (Match match in claim.Matches(clause))
                        {
                            string before = SpeechRelevance.Normalize(clause.Substring(0, match.Index));
                            if (NegatedActivity.IsMatch(before)) continue;
                            return true;
                        }
            }
            return false;
        }

        // Preserve ordinary recurrence cues from another chat participant, without letting the selected viewer's
        // own personal answer become evidence of an earlier streamer event on the next turn.
        private static bool OtherChatRecurrence(ReactionIntent intent, IReadOnlyList<StreamChatMessage> chat)
        {
            foreach (StreamChatMessage line in chat)
                if (line.ViewerId != intent.Viewer.ViewerId && RecurrenceFact.IsMatch(line.Text)) return true;
            return false;
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

        // Without a situation (tests, legacy callers) only the moment itself, visible chat and names are grounded.
        private static string Grounded(ReactionIntent intent, IReadOnlyList<StreamChatMessage> recentChat)
        {
            var grounded = new StringBuilder(256);
            grounded.Append(intent.Viewer.DisplayName).Append(' ').Append(intent.Event.SubjectName).Append(' ')
                .Append(intent.Event.Speech?.Text).Append(' ').Append(intent.Event.TriggeringLine).Append('\n');
            if (recentChat != null)
                foreach (StreamChatMessage line in recentChat) grounded.Append(line.SenderName).Append(' ').Append(line.Text).Append('\n');
            return grounded.ToString().ToLowerInvariant();
        }

        // A brand opinion is ordinary talk while the streamer discusses hardware; a model number is a factual claim.
        private static string UngroundedClaim(string text, string grounded, bool hardwareMoment)
        {
            HashSet<string> numbers = null;
            foreach (Match match in Quantity.Matches(text))
            {
                string value = Number(match.Groups["n"].Value);
                if (value == null || !(numbers ??= Numbers(grounded)).Contains(value)) return match.Value;
            }
            foreach (Match match in HardwareEntity.Matches(text))
            {
                string token = match.Value.ToLowerInvariant();
                bool supported = char.IsDigit(token[0]) ? (numbers ??= Numbers(grounded)).Contains(Number(token))
                    : hardwareMoment && !Regex.IsMatch(token, @"^(rx|i[3579])$") ||
                      Regex.IsMatch(grounded, @"(?<!\w)" + Regex.Escape(token) + @"(?!\w)", RegexOptions.CultureInvariant);
                if (!supported) return match.Value;
            }
            return null;
        }

        private static HashSet<string> Numbers(string grounded)
        {
            var numbers = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match match in Digits.Matches(grounded)) numbers.Add(Number(match.Value));
            foreach (string token in SpeechRelevance.Tokens(grounded))
                if (token.Length > 0 && !char.IsDigit(token[0]) && CountToken.IsMatch(token) && Number(token) is string value) numbers.Add(value);
            return numbers;
        }

        private static readonly Regex CountToken = new("^(?:" + CountWord + ")$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // Canonical digits for a numeral or a count word; null for vague amounts ("пол") that no fact can back.
        private static string Number(string word)
        {
            string w = word.ToLowerInvariant();
            if (w.Length > 0 && char.IsDigit(w[0])) return w.TrimStart('0').Length == 0 ? "0" : w.TrimStart('0');
            int n = w.StartsWith("пятнадцат") ? 15 : w.StartsWith("пятьдесят") || w.StartsWith("пятидесят") ? 50 : w.StartsWith("двадцат") ? 20
                : w.StartsWith("тридцат") ? 30 : w.StartsWith("сорок") ? 40 : w.StartsWith("десят") ? 10 : w.StartsWith("девят") ? 9
                : w.StartsWith("восем") || w.StartsWith("восьм") ? 8 : w.StartsWith("сем") ? 7 : w.StartsWith("шест") ? 6 : w.StartsWith("пят") ? 5
                : w.StartsWith("четыр") ? 4 : w.StartsWith("тр") ? 3 : w.StartsWith("дв") ? 2 : w == "сто" ? 100 : w switch
                {
                    "two" => 2, "three" => 3, "four" => 4, "five" => 5, "six" => 6, "seven" => 7, "eight" => 8, "nine" => 9, "ten" => 10,
                    "fifteen" => 15, "twenty" => 20, "thirty" => 30, "forty" => 40, "fifty" => 50, "hundred" => 100, _ => -1
                };
            return n < 0 ? null : n.ToString(CultureInfo.InvariantCulture);
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
