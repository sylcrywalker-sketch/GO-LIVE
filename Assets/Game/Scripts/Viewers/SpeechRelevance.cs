using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GoLive.Voice;

namespace GoLive.Viewers
{
    // Cheap, deterministic features of one recognized phrase. No language model is asked whether speech is
    // interesting: these cues decide relevance, and the phrase itself stays untrusted quoted data downstream.
    [Flags]
    public enum SpeechCue
    {
        None = 0,
        Filler = 1 << 0,            // "так", "секунду", "ok", "hold on": nothing for chat
        SelfTalk = 1 << 1,          // minor personal action: "воды попью", "brb"
        Question = 1 << 2,
        AddressesChat = 1 << 3,     // "чат", "ребят", "chat", "guys"
        MentionsViewer = 1 << 4,    // a known viewer's name
        Greeting = 1 << 5,
        Farewell = 1 << 6,
        Thanks = 1 << 7,
        Emotional = 1 << 8,         // intensity, frustration, "опять", "!"
        Conditional = 1 << 9,       // "если ...", "if ..."
        FutureCommitment = 1 << 10  // "завтра куплю", "I'll ... tomorrow": a possible promise
    }

    [Flags]
    public enum StreamTopic
    {
        None = 0,
        Hardware = 1 << 0,
        StreamSetup = 1 << 1,   // microphone, camera, sound, quality, internet
        Games = 1 << 2,
        Money = 1 << 3,
        Life = 1 << 4,          // food, sleep, apartment, work
        Community = 1 << 5      // chat, viewers, follows
    }

    // A viewer name as it may be spoken (and transcribed): normalized forms, e.g. "nightowl", "найт оул".
    public readonly struct ViewerNameForms
    {
        public string ViewerId { get; }
        public IReadOnlyList<string> Forms { get; }

        public ViewerNameForms(string viewerId, IReadOnlyList<string> forms)
        {
            ViewerId = viewerId ?? throw new ArgumentNullException(nameof(viewerId));
            Forms = forms ?? Array.Empty<string>();
        }
    }

    public sealed class SpeechAnalysis
    {
        public long Sequence { get; }
        public string Text { get; }
        public string Language { get; }
        public float Relevance { get; }
        public SpeechCue Cues { get; }
        public StreamTopic Topics { get; }
        public IReadOnlyList<string> MentionedViewerIds { get; }
        // Normalized content words, for matching the phrase against memories and recent chat.
        public IReadOnlyList<string> Words { get; }

        internal SpeechAnalysis(long sequence, string text, string language, float relevance, SpeechCue cues, StreamTopic topics,
            IReadOnlyList<string> mentioned, IReadOnlyList<string> words)
        {
            Sequence = sequence;
            Text = text;
            Language = language;
            Relevance = relevance;
            Cues = cues;
            Topics = topics;
            MentionedViewerIds = mentioned;
            Words = words;
        }

        public bool Has(SpeechCue cue) => (Cues & cue) != 0;
    }

    public static class SpeechRelevance
    {
        // Lexicons: an entry ending in '*' matches as a word prefix (Russian morphology), otherwise the whole word.
        private static readonly string[] FillerWords =
        {
            "так", "ну", "эм", "ээ", "эээ", "мм", "короче", "секунду", "секунда", "секундочку", "сек", "щас", "сейчас", "ладно",
            "окей", "ок", "ага", "угу", "да", "нет", "вот", "типа", "блин", "итак", "ща", "погоди", "подожди",
            "hmm", "uh", "um", "ok", "okay", "wait", "alright", "so", "yeah", "yes", "no", "well", "sec", "hold", "on", "one"
        };
        private static readonly string[] SelfTalkWords =
        {
            "воды", "водички", "попью", "попить", "чай", "чаю", "налью", "отойду", "вернусь", "кофе", "пописать",
            "brb", "water", "drink", "coffee", "bathroom"
        };
        private static readonly string[] ChatWords =
        {
            "чат", "чатик", "чатику", "чате", "ребят", "ребята", "ребятки", "народ", "пацаны", "девчонки", "друзья", "зрители",
            "chat", "guys", "everyone", "yall", "folks", "people"
        };
        private static readonly string[] QuestionWords =
        {
            "что", "чё", "че", "как", "почему", "зачем", "кто", "где", "когда", "какой", "какая", "какое", "какие", "сколько",
            "думаете", "согласны", "знаете", "помните",
            "what", "why", "how", "who", "where", "when", "which", "should", "anyone", "thoughts"
        };
        private static readonly string[] GreetingWords =
        {
            "привет", "приветик", "приветствую", "здарова", "здорово", "здравствуйте", "хай", "салют", "йоу",
            "hello", "hi", "hey", "sup", "welcome", "yo"
        };
        private static readonly string[] FarewellWords =
        {
            "пока", "покеда", "заканчиваем", "заканчиваю", "закругляемся", "спокойной", "bye", "goodbye", "goodnight"
        };
        private static readonly string[] ThanksWords = { "спасибо", "спс", "благодарю", "thanks", "thank", "thx", "ty" };
        private static readonly string[] EmotionalWords =
        {
            "опять", "снова", "капец", "пипец", "черт", "чёрт", "бесит", "бесят", "ненавиж*", "обожа*", "удаля*", "удалю", "ужас*",
            "кошмар*", "офиге*", "жесть", "жесть*", "нифига", "вау", "обалде*", "бля*", "пизд*", "хуй*", "хуе*", "ебат*",
            "again", "damn", "wtf", "hate", "love", "insane", "crazy", "omg", "wow", "fuck*", "shit", "unbelievable"
        };
        private static readonly string[] ConditionalWords = { "если", "if" };
        private static readonly string[] FutureMarkers =
        {
            "завтра", "послезавтра", "потом", "скоро", "следующ*", "вечером", "обещаю", "обещаю*",
            "tomorrow", "tonight", "next", "later", "promise", "soon"
        };
        private static readonly string[] CommitmentVerbs =
        {
            "куплю", "сделаю", "поставлю", "покажу", "сыграю", "буду", "проведу", "запущу", "начну", "подключу", "накоплю",
            "обещаю", "закажу", "стримлю", "постримлю",
            "ill", "will", "gonna", "promise", "buy", "get"
        };
        private static readonly string[] HardwareWords =
        {
            "видеокарт*", "видюх*", "видях*", "гпу", "gpu", "проц", "процессор*", "cpu", "памят*", "оперативк*", "ram", "ssd",
            "диск*", "комп", "компа", "компу", "компьютер*", "пк", "pc", "фпс", "fps", "лаг*", "тормоз*", "материнк*", "кулер*",
            "железо", "железк*", "hardware", "graphics", "rtx", "gtx"
        };
        private static readonly string[] StreamSetupWords =
        {
            "микрофон*", "микро", "мик", "камер*", "вебк*", "звук*", "качеств*", "битрейт*", "интернет*", "пинг*", "стрим*",
            "mic", "microphone", "camera", "webcam", "cam", "sound", "audio", "quality", "stream", "internet", "bitrate"
        };
        private static readonly string[] GameWords =
        {
            "игр*", "катк*", "раунд*", "умру", "умер", "умерли", "умираю", "убил*", "убьют", "проигра*", "проиграю", "выигра*",
            "победи*", "слил*", "game", "games", "round", "die", "died", "dead", "win", "won", "lose", "lost", "kill*", "match"
        };
        private static readonly string[] MoneyWords =
        {
            "деньг*", "денег", "аренд*", "зарплат*", "донат*", "рубл*", "бакс*", "доллар*", "бабк*", "нищ*", "кредит*", "долг*",
            "money", "rent", "cash", "broke", "donat*", "dollars", "bucks", "pay"
        };
        private static readonly string[] LifeWords =
        {
            "есть", "поесть", "жрать", "голоден", "голодный", "еда", "еду", "спать", "сплю", "устал*", "квартир*", "сосед*",
            "работ*", "учеб*", "пар", "сон", "food", "hungry", "eat", "sleep", "tired", "apartment", "neighbor*", "work", "job"
        };
        private static readonly string[] CommunityWords =
        {
            "подписч*", "зрител*", "фолло*", "подпис*", "follow*", "sub", "subs", "viewers", "followers"
        };

        public static SpeechAnalysis Analyze(RecognizedSpeech speech, IReadOnlyList<ViewerNameForms> names)
        {
            if (speech == null) throw new ArgumentNullException(nameof(speech));
            string text = speech.Text;
            List<string> tokens = Tokens(text);
            var cues = SpeechCue.None;
            StreamTopic topics = StreamTopic.None;
            if (text.IndexOf('?') >= 0 || (tokens.Count > 0 && Matches(tokens[0], QuestionWords))) cues |= SpeechCue.Question;
            if (Any(tokens, ChatWords)) cues |= SpeechCue.AddressesChat;
            if (Any(tokens, GreetingWords)) cues |= SpeechCue.Greeting;
            if (Any(tokens, FarewellWords) || Contains(tokens, "до", "завтра") || Contains(tokens, "see", "you")) cues |= SpeechCue.Farewell;
            if (Any(tokens, ThanksWords)) cues |= SpeechCue.Thanks;
            if (Any(tokens, EmotionalWords) || text.IndexOf('!') >= 0) cues |= SpeechCue.Emotional;
            if (tokens.Count >= 3 && Any(tokens, ConditionalWords)) cues |= SpeechCue.Conditional;
            if (Any(tokens, FutureMarkers) && Any(tokens, CommitmentVerbs)) cues |= SpeechCue.FutureCommitment;
            if (Any(tokens, HardwareWords)) topics |= StreamTopic.Hardware;
            if (Any(tokens, StreamSetupWords)) topics |= StreamTopic.StreamSetup;
            if (Any(tokens, GameWords)) topics |= StreamTopic.Games;
            if (Any(tokens, MoneyWords)) topics |= StreamTopic.Money;
            if (Any(tokens, LifeWords)) topics |= StreamTopic.Life;
            if (Any(tokens, CommunityWords) || (cues & SpeechCue.AddressesChat) != 0) topics |= StreamTopic.Community;

            var mentioned = new List<string>();
            if (names != null)
                foreach (ViewerNameForms name in names)
                    if (MentionsAny(tokens, name.Forms)) mentioned.Add(name.ViewerId);
            if (mentioned.Count > 0) cues |= SpeechCue.MentionsViewer;

            bool onlyFiller = tokens.Count > 0 && AllMatch(tokens, FillerWords);
            if (onlyFiller) cues |= SpeechCue.Filler;
            const SpeechCue Social = SpeechCue.AddressesChat | SpeechCue.Question | SpeechCue.MentionsViewer | SpeechCue.Greeting | SpeechCue.Thanks;
            if (Any(tokens, SelfTalkWords) && (cues & Social) == 0) cues |= SpeechCue.SelfTalk;

            float relevance = .12f;
            if ((cues & SpeechCue.AddressesChat) != 0) relevance += .4f;
            if ((cues & SpeechCue.Question) != 0) relevance += .3f;
            if ((cues & SpeechCue.MentionsViewer) != 0) relevance += .55f;
            if ((cues & SpeechCue.Emotional) != 0) relevance += .25f;
            if ((cues & SpeechCue.Conditional) != 0) relevance += .2f;
            if ((cues & SpeechCue.FutureCommitment) != 0) relevance += .25f;
            if ((cues & SpeechCue.Greeting) != 0) relevance += .25f;
            if ((cues & SpeechCue.Farewell) != 0) relevance += .25f;
            if ((cues & SpeechCue.Thanks) != 0) relevance += .25f;
            if (topics != StreamTopic.None) relevance += .1f;
            if (tokens.Count >= 6) relevance += .08f;
            if ((cues & SpeechCue.SelfTalk) != 0) relevance -= .3f;
            if (onlyFiller) relevance = Math.Min(relevance, .1f);
            if (speech.Confidence is float confidence && confidence < .35f) relevance *= .7f;

            var words = new List<string>();
            foreach (string token in tokens)
                if (token.Length >= 3 && !Matches(token, FillerWords)) words.Add(token);
            return new SpeechAnalysis(speech.Sequence, text, speech.Language, Math.Clamp(relevance, 0f, 1f), cues, topics,
                mentioned.AsReadOnly(), words.AsReadOnly());
        }

        // Lowercase words; 'ё' folds to 'е'; everything but letters and digits separates words.
        public static List<string> Tokens(string text)
        {
            var tokens = new List<string>();
            if (string.IsNullOrEmpty(text)) return tokens;
            var word = new StringBuilder();
            foreach (char raw in text.ToLower(CultureInfo.InvariantCulture))
            {
                char c = raw == 'ё' ? 'е' : raw;
                if (char.IsLetterOrDigit(c)) word.Append(c);
                else if (c == '\'' || c == '’') continue;
                else Flush();
            }
            Flush();
            return tokens;

            void Flush()
            {
                if (word.Length == 0) return;
                tokens.Add(word.ToString());
                word.Clear();
            }
        }

        // The normalized, space-joined form used for names and aliases ("Night Owl" -> "night owl").
        public static string Normalize(string text) => string.Join(" ", Tokens(text));

        private static bool MentionsAny(List<string> tokens, IReadOnlyList<string> forms)
        {
            foreach (string form in forms)
            {
                List<string> parts = Tokens(form);
                if (parts.Count == 0) continue;
                string joined = string.Concat(parts);
                // Joined consecutive tokens, so "найт оул" matches "найтоул" and the reverse.
                for (int start = 0; start < tokens.Count; start++)
                {
                    string candidate = "";
                    for (int end = start; end < tokens.Count && end < start + 3; end++)
                    {
                        candidate += tokens[end];
                        if (candidate.Length > joined.Length + 1) break;
                        if (candidate == joined || (joined.Length >= 6 && EditDistanceAtMostOne(candidate, joined))) return true;
                    }
                }
            }
            return false;
        }

        private static bool EditDistanceAtMostOne(string a, string b)
        {
            if (Math.Abs(a.Length - b.Length) > 1) return false;
            int i = 0, j = 0, edits = 0;
            while (i < a.Length && j < b.Length)
            {
                if (a[i] == b[j]) { i++; j++; continue; }
                if (++edits > 1) return false;
                if (a.Length > b.Length) i++;
                else if (b.Length > a.Length) j++;
                else { i++; j++; }
            }
            return edits + (a.Length - i) + (b.Length - j) <= 1;
        }

        private static bool Contains(List<string> tokens, string first, string second)
        {
            for (int i = 0; i + 1 < tokens.Count; i++)
                if (tokens[i] == first && tokens[i + 1] == second) return true;
            return false;
        }

        private static bool Any(List<string> tokens, string[] lexicon)
        {
            foreach (string token in tokens)
                if (Matches(token, lexicon)) return true;
            return false;
        }

        private static bool AllMatch(List<string> tokens, string[] lexicon)
        {
            foreach (string token in tokens)
                if (!Matches(token, lexicon)) return false;
            return true;
        }

        private static bool Matches(string token, string[] lexicon)
        {
            foreach (string entry in lexicon)
            {
                if (entry[entry.Length - 1] == '*')
                {
                    if (token.Length >= entry.Length - 1 && string.CompareOrdinal(token, 0, entry, 0, entry.Length - 1) == 0) return true;
                }
                else if (token == entry) return true;
            }
            return false;
        }
    }
}
