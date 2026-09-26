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

    // What the streamer is doing socially with one recognized phrase. Several acts can apply ("всем привет, как дела?"
    // is a Greeting and a PersonalQuestion); SpeechAnalysis.PrimaryAct orders them so the question is answered first.
    [Flags]
    public enum SpeechAct
    {
        None = 0,
        Greeting = 1 << 0,
        QuestionToChat = 1 << 1,    // a question or request to the whole chat ("чат, во что поиграем?", "расскажите")
        QuestionToViewer = 1 << 2,  // a question to one person: named, or asked with "ты"
        OpinionRequest = 1 << 3,    // "что думаете", "как вам", "согласны?"
        PersonalQuestion = 1 << 4,  // about the viewers themselves: "как дела", "как настроение", "что сегодня делали"
        GameplayQuestion = 1 << 5,  // about games / what to play
        Statement = 1 << 6,
        PromiseCandidate = 1 << 7,
        Filler = 1 << 8
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
        public SpeechAct Acts { get; }
        // Asked with "ты"/"тебя"/"-ешь": one listener, not the whole chat.
        public bool SingularAddress { get; }
        // Addressed to several people: "чат", "ребят", "вы", "-ете/-ите", "что делали".
        public bool PluralAddress { get; }

        internal SpeechAnalysis(long sequence, string text, string language, float relevance, SpeechCue cues, StreamTopic topics,
            IReadOnlyList<string> mentioned, IReadOnlyList<string> words, SpeechAct acts = SpeechAct.None, bool singular = false, bool plural = false)
        {
            Sequence = sequence;
            Text = text;
            Language = language;
            Relevance = relevance;
            Cues = cues;
            Topics = topics;
            MentionedViewerIds = mentioned;
            Words = words;
            Acts = acts;
            SingularAddress = singular;
            PluralAddress = plural;
        }

        public bool Has(SpeechCue cue) => (Cues & cue) != 0;
        public bool Is(SpeechAct act) => (Acts & act) != 0;

        // The streamer asked the audience (or one viewer) something that expects an answer.
        public bool AsksForAnswer => (Acts & (SpeechAct.QuestionToChat | SpeechAct.QuestionToViewer | SpeechAct.OpinionRequest |
                                              SpeechAct.PersonalQuestion | SpeechAct.GameplayQuestion)) != 0;

        // The streamer is actively talking TO the chat: a question/request, a greeting, or naming someone.
        public bool Addresses => AsksForAnswer || Is(SpeechAct.Greeting) || MentionedViewerIds.Count > 0;

        // The act the reaction answers first: a question outranks the greeting it came with.
        public SpeechAct PrimaryAct =>
            Is(SpeechAct.PersonalQuestion) ? SpeechAct.PersonalQuestion
            : Is(SpeechAct.OpinionRequest) ? SpeechAct.OpinionRequest
            : Is(SpeechAct.GameplayQuestion) ? SpeechAct.GameplayQuestion
            : Is(SpeechAct.QuestionToViewer) ? SpeechAct.QuestionToViewer
            : Is(SpeechAct.QuestionToChat) ? SpeechAct.QuestionToChat
            : Is(SpeechAct.PromiseCandidate) ? SpeechAct.PromiseCandidate
            : Is(SpeechAct.Greeting) ? SpeechAct.Greeting
            : Is(SpeechAct.Filler) ? SpeechAct.Filler
            : SpeechAct.Statement;
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
            "чат", "чатик", "чатику", "чате", "ребят", "ребята", "ребятки", "ребзя", "народ", "пацаны", "пацанчики", "парни", "мужики",
            "девчонки", "девочки", "братва", "друзья", "зрители", "всем", "chat", "guys", "everyone", "yall", "folks", "people"
        };
        // Talking to one listener ("ты", "тебе", "как сам") or to several ("вы", "вас").
        private static readonly string[] SingularWords = { "ты", "тебя", "тебе", "тобой", "твой", "твоя", "твое", "твои", "твоих", "сам" };
        private static readonly string[] PluralWords = { "вы", "вас", "вам", "вами", "ваш", "ваша", "ваше", "ваши", "сами" };
        // Asking the chat to answer, tell or advise: a request is a question even without "?".
        private static readonly string[] RequestWords =
        {
            "расскажите", "рассказать", "напишите", "написать", "скажите", "ответьте", "ответить", "поделитесь", "подскажите",
            "посоветуйте", "пишите", "отпишитесь", "tell", "answer", "share", "recommend"
        };
        // After "как": the listener's own state ("как дела", "как у вас настроение", "как сам").
        private static readonly string[] StateWords =
        {
            "дела", "делишки", "настроение", "настроения", "настрой", "жизнь", "самочувствие", "день", "денек", "выходные",
            "учеба", "работа", "поживаете", "поживаешь", "сам", "сами", "ты", "вы", "спалось", "спал", "спали"
        };
        // The listener's own recent doings, asked in the 2nd person or plural past ("что делали", "во что играл").
        private static readonly string[] ActivityWords =
        {
            "делали", "делал", "делала", "делаете", "делаешь", "занимались", "занимался", "занималась", "занимаетесь", "занимаешься",
            "играли", "играл", "играла", "играете", "играешь", "смотрели", "смотрел", "смотрела", "ели", "ел", "ела", "отдыхали",
            "гуляли", "работал", "работала", "работали", "учился", "училась", "успели", "успел", "успела", "устал", "устала", "устали"
        };
        private static readonly string[] ConversationAcknowledgements =
            { "понятно", "ясно", "жесть", "сочувствую", "отдыхай", "держись", "устаешь", "tired", "rough", "rest" };
        private static readonly string[] OtherConversationSubjects =
            { "я", "мы", "он", "она", "они", "i", "we", "he", "she", "they" };

        // Shared by selection and planning: a short reply about the listener or their day can continue an
        // exchange even when recognition omits '?'. A new gameplay/setup topic or streamer self-talk cannot.
        internal static bool ContinuesConversation(SpeechAnalysis speech)
        {
            if (speech == null || speech.Is(SpeechAct.Filler) || speech.PluralAddress) return false;
            // Preserve already explicit personal questions, including "я нормально, а ты как дела".
            if (speech.Is(SpeechAct.PersonalQuestion)) return true;
            List<string> tokens = Tokens(speech.Text);
            if (Any(tokens, OtherConversationSubjects)) return false;
            // Check the complete short form before broad topic stems: "долго" alone is a duration question,
            // although the global money lexicon also matches its "долг" prefix. "долго копить деньги" is not.
            if (ShortPersonalQuestion(tokens)) return true;
            if (speech.Is(SpeechAct.GameplayQuestion) || speech.Is(SpeechAct.OpinionRequest) ||
                (speech.Topics & (StreamTopic.Games | StreamTopic.Hardware | StreamTopic.StreamSetup | StreamTopic.Money)) != 0) return false;
            return Any(tokens, ActivityWords) || Any(tokens, ConversationAcknowledgements);
        }

        // Only selection of an active, present conversation partner (or planning an already selected FollowUp)
        // may use this predicate. It never changes global acts or the recognized text. Whisper punctuation is optional.
        internal static bool ConversationAsksForAnswer(SpeechAnalysis speech) =>
            ContinuesConversation(speech) && (speech.AsksForAnswer || ShortPersonalQuestion(Tokens(speech.Text)));

        private static bool ShortPersonalQuestion(List<string> tokens)
        {
            if (tokens.Count == 0 || tokens.Count > 8) return false;
            int start = 0, end = tokens.Count;
            // Discourse particles and an addressed "ты" may surround an elliptical personal question.
            while (start < end && (tokens[start] == "а" || tokens[start] == "и" || tokens[start] == "ну" ||
                tokens[start] == "жесть" || tokens[start] == "ого")) start++;
            if (start + 1 < end && tokens[start] == "ты") start++;
            if (end > start && (tokens[end - 1] == "наверное" || tokens[end - 1] == "видимо")) end--;
            string form = string.Join(" ", tokens.GetRange(start, end - start));
            return form switch
            {
                "устал" or "устала" or "устаешь" or "вымотался" or "вымоталась" => true,
                "тяжело было" or "было тяжело" or "нормально там" or "там нормально" => true,
                "серьезно" or "правда" or "ты" or "как" or "что потом" or "что дальше" => true,
                "понравилось" or "долго" or "как прошло" or "как получилось" => true,
                _ => false
            };
        }
        private static readonly string[] OpinionPhrases =
        {
            " что думаете", " как думаете", " что думаешь", " как думаешь", " как вам ", " как тебе ", " как считаете", " что скажете",
            " что скажешь", " согласны", " согласен", " нравится", " стоит ли", " what do you think", " thoughts", " do you like", " should i"
        };
        private static readonly string[] EnglishPersonal =
        {
            " how are you", " how r u", " how you doing", " how is it going", " hows it going", " how was your day", " what did you do",
            " what are you up to", " what have you been", " where are you from", " how is your day"
        };
        private static readonly string[] GameAskWords = { "поиграем", "поиграть", "играть", "игру", "игры", "игра", "сыграть", "сыграем", "play", "game" };
        private static readonly string[] InnerQuestionWords =
        {
            "что", "чем", "чё", "че", "как", "где", "куда", "откуда", "когда", "почему", "зачем", "сколько", "какой", "какая", "какие", "какое",
            "what", "how", "where", "when", "why"
        };
        // Plural past forms asked of the chat ("что делали", "во что играли") also address several people.
        private static readonly string[] PluralVerbs = { "делали", "занимались", "играли", "смотрели", "отдыхали", "гуляли", "успели", "устали" };
        // Asking for opinions is a question wherever it appears, even when recognition drops the "?".
        private static readonly string[] OpinionWords = { "думаете", "согласны", "считаете", "скажете", "thoughts", "think" };
        // Whisper often hears a leading "чат," as "чет,"; only as the first word does it address the chat.
        private static readonly string[] LeadingChatMishearings = { "чет", "чят", "чад" };
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
            "завтра", "послезавтра", "потом", "скоро", "следующ*", "вечером", "сегодня", "обещаю", "обещаю*",
            "tomorrow", "tonight", "next", "later", "promise", "soon"
        };
        private static readonly string[] CommitmentVerbs =
        {
            "куплю", "сделаю", "поставлю", "покажу", "сыграю", "буду", "будем", "проведу", "запущу", "начну", "подключу", "накоплю",
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
            if (Any(tokens, ChatWords) || (tokens.Count > 1 && Matches(tokens[0], LeadingChatMishearings))) cues |= SpeechCue.AddressesChat;
            // A question: "?", a question word first (or right after addressing the chat), or asking for opinions.
            int first = (cues & SpeechCue.AddressesChat) != 0 && tokens.Count > 1 && (Matches(tokens[0], ChatWords) || Matches(tokens[0], LeadingChatMishearings)) ? 1 : 0;
            if (text.IndexOf('?') >= 0 || (tokens.Count > first && Matches(tokens[first], QuestionWords)) || Any(tokens, OpinionWords)) cues |= SpeechCue.Question;
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
            SpeechAct acts = ClassifyActs(tokens, ref cues, topics, mentioned.Count > 0, out bool singular, out bool plural);
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
            // A question put to the audience is an invitation, whatever else the phrase carries.
            const SpeechAct Invitation = SpeechAct.QuestionToChat | SpeechAct.QuestionToViewer | SpeechAct.OpinionRequest |
                                         SpeechAct.PersonalQuestion | SpeechAct.GameplayQuestion;
            if ((acts & Invitation) != 0) relevance = Math.Max(relevance, .8f);
            if (onlyFiller) relevance = Math.Min(relevance, .1f);
            if (speech.Confidence is float confidence && confidence < .35f) relevance *= .7f;

            var words = new List<string>();
            foreach (string token in tokens)
                if (token.Length >= 3 && !Matches(token, FillerWords)) words.Add(token);
            return new SpeechAnalysis(speech.Sequence, text, speech.Language, Math.Clamp(relevance, 0f, 1f), cues, topics,
                mentioned.AsReadOnly(), words.AsReadOnly(), acts, singular, plural);
        }

        // Small, deterministic social classification of the recognized phrase. Bounded lexicons, never a model.
        private static SpeechAct ClassifyActs(List<string> tokens, ref SpeechCue cues, StreamTopic topics, bool mentions, out bool singular, out bool plural)
        {
            var acts = SpeechAct.None;
            singular = plural = false;
            if ((cues & SpeechCue.Filler) != 0) return SpeechAct.Filler;
            string phrase = " " + string.Join(" ", tokens) + " ";
            singular = Any(tokens, SingularWords) || EndsAny(tokens, "ешь", "ишь");
            plural = Any(tokens, PluralWords) || Any(tokens, PluralVerbs) || EndsAny(tokens, "ете", "ите") || (cues & SpeechCue.AddressesChat) != 0;
            bool asked = (cues & SpeechCue.Question) != 0;
            bool request = plural && Any(tokens, RequestWords);
            bool opinion = ContainsAny(phrase, OpinionPhrases);
            // "как" with the listener's state within three words: "как дела", "как у вас настроение", "как сам".
            bool state = Near(tokens, "как", StateWords, 3) && !opinion;
            // The listener's own doings: "что сегодня делали", "а ты во что играл", "устал?" (never "я устал").
            bool inner = Any(tokens, InnerQuestionWords) && (singular || plural || Contains(tokens, "сегодня"));
            bool activity = Any(tokens, ActivityWords) && !Contains(tokens, "я") && (asked || request || inner);
            bool personal = state || activity || ContainsAny(phrase, EnglishPersonal);
            bool games = (topics & StreamTopic.Games) != 0 || Any(tokens, GameAskWords);
            if (personal || opinion || request) cues |= SpeechCue.Question;
            asked = (cues & SpeechCue.Question) != 0;

            if ((cues & SpeechCue.Greeting) != 0) acts |= SpeechAct.Greeting;
            if (personal) acts |= SpeechAct.PersonalQuestion;
            if (opinion) acts |= SpeechAct.OpinionRequest;
            if (asked && games && !personal) acts |= SpeechAct.GameplayQuestion;
            if (asked && (mentions || singular && !plural)) acts |= SpeechAct.QuestionToViewer;
            if (asked && (plural || !singular && !mentions)) acts |= SpeechAct.QuestionToChat;
            if ((cues & SpeechCue.FutureCommitment) != 0) acts |= SpeechAct.PromiseCandidate;
            // A bare greeting ("всем привет") stays a greeting; anything else said besides it is a statement.
            if ((acts & ~SpeechAct.Greeting) == SpeechAct.None && tokens.Count > 0 && !(acts == SpeechAct.Greeting && tokens.Count <= 3))
                acts |= SpeechAct.Statement;
            return acts;
        }

        private static bool Near(List<string> tokens, string first, string[] lexicon, int window)
        {
            for (int i = 0; i < tokens.Count; i++)
            {
                if (tokens[i] != first) continue;
                for (int j = i + 1; j < tokens.Count && j <= i + window; j++)
                    if (Matches(tokens[j], lexicon)) return true;
            }
            return false;
        }

        private static bool EndsAny(List<string> tokens, params string[] endings)
        {
            foreach (string token in tokens)
            {
                if (token.Length < 5) continue;
                foreach (string ending in endings)
                    if (token.EndsWith(ending, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private static bool Contains(List<string> tokens, string word)
        {
            foreach (string token in tokens)
                if (token == word) return true;
            return false;
        }

        private static bool ContainsAny(string phrase, string[] fragments)
        {
            foreach (string fragment in fragments)
                if (phrase.Contains(fragment)) return true;
            return false;
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
