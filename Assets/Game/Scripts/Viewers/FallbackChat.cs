using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GoLive.Desktop;
using GoLive.PcBuilding;

namespace GoLive.Viewers
{
    // Keeps the chat alive without a language model: a few short, ordinary lines per kind of moment. It preserves
    // gameplay feedback (someone answers when addressed, donations are noticed); it does not imitate the model.
    // A question C# has classified gets an answer built from the same facts the prompt supplied, or silence: a stock
    // line that ignores the question ("норм, а ты как?" to "почему без настроения?") is worse than no line.
    // Returns null when silence is the better fallback.
    public static class FallbackChat
    {
        private static readonly Dictionary<string, (string[] ru, string[] en)> Lines = new()
        {
            ["greeting"] = (new[] { "привет", "прив", "хай", "здарова", "о, стрим" }, new[] { "hi", "yo", "hey", "o/" }),
            ["direct"] = (new[] { "а?", "я тут", "тут я", "чего", "да-да, тут" }, new[] { "yeah?", "here", "what", "yo im here" }),
            ["question"] = (new[] { "хз", "не знаю даже", "сложно сказать", "без понятия", "а сам как думаешь" }, new[] { "idk", "hard to say", "no idea tbh", "hmm" }),
            ["personal"] = (new[] { "норм, а ты как?", "да потихоньку, отдыхаю", "нормально, день длинный был", "всё ок, вот стрим смотрю", "живой, а у тебя как?" },
                new[] { "good, you?", "pretty chill, just watching", "long day but ok", "doing alright, hbu" }),
            ["emotional"] = (new[] { "ахах", "бывает", "спокойно", "ну ты чего", "держись" }, new[] { "lol", "happens", "rip", "oof" }),
            ["speech"] = (new[] { "ахах", "ну да", "лол", "понял", "+" }, new[] { "lol", "true", "fair", "ok" }),
            ["thanks"] = (new[] { "да не за что", "пожалуйста)", "на здоровье" }, new[] { "np", "anytime", "youre welcome" }),
            ["farewell"] = (new[] { "пока", "до завтра", "споки" }, new[] { "bye", "gn", "cya" }),
            ["silence"] = (new[] { "алло", "ты тут?", "?", "уснул?" }, new[] { "hello?", "you there?", "?" }),
            ["away"] = (new[] { "афк?", "он ушёл", "ну и где он" }, new[] { "afk?", "where'd he go" }),
            ["donation.own"] = (new[] { "на чай", "держи", "это тебе", "на развитие" }, new[] { "for the stream", "have a coffee", "here you go" }),
            ["donation"] = (new[] { "о, донат", "щедро", "ого" }, new[] { "nice dono", "generous", "oh nice" }),
            ["follow.own"] = (new[] { "фолловнул", "зафолловил" }, new[] { "followed", "just followed" }),
            ["subscription.own"] = (new[] { "подписался)", "оформил подписку" }, new[] { "subbed", "just subbed" }),
            ["support"] = (new[] { "о, новенький", "+1", "растём" }, new[] { "new one", "+1", "growing" }),
            ["milestone"] = (new[] { "о, нас уже {0}", "растём", "{0} человек, ого" }, new[] { "{0} people lets go", "chat's growing" }),
            ["microphone.off"] = (new[] { "звук стал хуже", "что со звуком?", "микро отвалился?" }, new[] { "audio's rough now", "mic?" }),
            ["microphone.on"] = (new[] { "о, звук норм", "так лучше" }, new[] { "audio's better now", "much better" }),
            ["webcam.on"] = (new[] { "о, камера", "вебка!" }, new[] { "cam's on", "oh cam" }),
            ["webcam.off"] = (new[] { "камера пропала", "где камера" }, new[] { "cam's off?", "cam died" }),
            ["ambient"] = (new[] { "а что сегодня делаем", "норм", "как дела вообще" }, new[] { "what are we doing today", "chillin" })
        };

        // Answers by the viewer's register: casual chat, or plain full words for viewers who never use slang.
        private static readonly Dictionary<ViewerMood, (string[] casual, string[] plain, string[] en)> MoodLines = new()
        {
            [ViewerMood.Good] = (new[] { "норм, всё хорошо", "да хорошо всё" }, new[] { "Всё хорошо, спасибо" }, new[] { "good, pretty chill", "doing good" }),
            [ViewerMood.Tired] = (new[] { "сил нет чет", "устало как-то" }, new[] { "Устало немного, сил мало" }, new[] { "tired tbh", "kinda tired" }),
            [ViewerMood.Chill] = (new[] { "спокойно всё, отдыхаю", "норм, расслабляюсь" }, new[] { "Спокойно, отдыхаю" }, new[] { "chill, just relaxing" }),
            [ViewerMood.Bored] = (new[] { "скучновато", "скучно чет" }, new[] { "Скучновато сегодня" }, new[] { "kinda bored", "bored tbh" }),
            [ViewerMood.Upbeat] = (new[] { "отлично вообще", "бодро, всё супер" }, new[] { "Отлично, настроение прекрасное" }, new[] { "great actually", "pretty hyped" }),
            [ViewerMood.Stressed] = (new[] { "на нервах сегодня", "так себе, нервы" }, new[] { "Нервный день сегодня" }, new[] { "stressed tbh", "not great, stressed" })
        };
        private static readonly (string[] casual, string[] plain, string[] en) Yes = (new[] { "да, есть немного" }, new[] { "Да, немного есть" }, new[] { "yeah a bit" });
        private static readonly (string[] casual, string[] plain, string[] en) No = (new[] { "не, норм" }, new[] { "Нет, всё хорошо" }, new[] { "nah im good" });
        private static readonly (string[] casual, string[] plain, string[] en) Game =
            (new[] { "да что угодно, мне без разницы", "что-нибудь попроще" }, new[] { "Что-нибудь спокойное, мне всё равно" }, new[] { "idk anything chill" });
        private static readonly (string[] casual, string[] plain, string[] en) Opinion =
            (new[] { "норм вроде", "сложно сказать" }, new[] { "Сложно сказать" }, new[] { "hard to say", "its ok i guess" });
        private static readonly (string[] casual, string[] plain, string[] en) WaryOpinion = (new[] { "ну такое", "так себе" }, new[] { "Так себе" }, new[] { "kinda mid" });
        private static readonly (string[] casual, string[] plain, string[] en) Presence = (new[] { "я тут", "тут я", "да-да, тут" }, new[] { "Я здесь" }, new[] { "here", "yo im here" });
        // A reply in an ongoing exchange that asked nothing: a short sign of listening, never a new topic.
        private static readonly (string[] casual, string[] plain, string[] en) Listening = (new[] { "ну да", "ага", "угу" }, new[] { "Да, понимаю" }, new[] { "yeah", "true" });

        public static string Pick(ReactionIntent intent, AudienceRandom random, IReadOnlyList<StreamChatMessage> recentChat)
            => Pick(intent, random, recentChat, null);

        // With the generation situation, a classified question is answered from its plan or left silent.
        public static string Pick(ReactionIntent intent, AudienceRandom random, IReadOnlyList<StreamChatMessage> recentChat, ChatSituation situation)
        {
            if (intent == null) throw new ArgumentNullException(nameof(intent));
            if (situation != null && Answerable(intent))
            {
                if (!intent.Direct && intent.Order > 0) return null;
                ViewerUtterancePlan plan = ViewerUtterancePlanner.For(intent, situation);
                if (plan.QuestionPurpose != QuestionPurpose.None) return First(Answer(intent, plan, situation), random, recentChat);
                if (intent.FollowUp) return First(Register(intent, Listening), random, recentChat);
            }
            string key = Key(intent, random);
            if (key == null || !Lines.TryGetValue(key, out var lines)) return null;
            string[] options = intent.Viewer.Persona.Language == ViewerLanguage.English ? lines.en : lines.ru;
            int start = random.NextInt(options.Length);
            for (int i = 0; i < options.Length; i++)
            {
                string text = string.Format(CultureInfo.InvariantCulture, options[(start + i) % options.Length], intent.Event.AudienceSize);
                if (!Recently(text, recentChat)) return text;
            }
            return null;
        }

        // Speech that is not the streamer thanking this viewer or saying goodbye (those keep their own lines).
        private static bool Answerable(ReactionIntent intent)
        {
            StreamEvent e = intent.Event;
            if (e.Kind != StreamEventKind.StreamerSpeech || e.Speech == null) return false;
            bool own = intent.Direct && e.SubjectViewerId == intent.Viewer.ViewerId;
            bool named = e.Speech.MentionedViewerIds.Contains(intent.Viewer.ViewerId);
            return !own && !e.Speech.Has(SpeechCue.Farewell) && !(named && e.Speech.Has(SpeechCue.Thanks));
        }

        // The truthful answer to this purpose from supplied facts, or null (silence) when none can be built.
        internal static string[] Answer(ReactionIntent intent, ViewerUtterancePlan plan, ChatSituation situation)
        {
            ViewerDailyState day = plan.DirectAnswerFacts.Count > 0 ? situation.Day : null;
            bool english = intent.Viewer.Persona.Language == ViewerLanguage.English;
            string today = day == null ? null : Said(english ? day.Activity.Today : day.Activity.SayToday, intent);
            string now = day == null ? null : Said(english ? day.Activity.Now : day.Activity.SayNow, intent);
            bool Has(string label) { foreach (GroundedFact fact in plan.DirectAnswerFacts) if (fact.Label == label) return true; return false; }
            switch (plan.QuestionPurpose)
            {
                case QuestionPurpose.ReasonForMood:
                case QuestionPurpose.TodayActivity:
                case QuestionPurpose.Explanation:
                    return today != null && Has("Today") ? new[] { today } : null;
                case QuestionPurpose.CurrentActivity:
                    return now != null && Has("Now") ? new[] { now } : null;
                case QuestionPurpose.Mood:
                    if (day == null) return null;
                    string[] mood = Register(intent, MoodLines[day.Mood]);
                    return now != null && Has("Now") ? new[] { mood[0] + ", " + Lower(now, intent), mood[mood.Length - 1] } : mood;
                case QuestionPurpose.Confirmation:
                    if (day == null) return ViewerQuestionPurpose.AsksPresence(intent.Event.Speech.Text) ? Register(intent, Presence) : null;
                    return Register(intent, day.Mood == ViewerMood.Tired || day.Mood == ViewerMood.Stressed || day.Energy == ViewerEnergy.Low ? Yes : No);
                case QuestionPurpose.GenericQuestion:
                    // A doing question inside a personal exchange can be answered by the day; any other direct question
                    // cannot be answered truthfully without the model.
                    if (today != null && Has("Today")) return new[] { today };
                    if (plan.AnswerFirst) return null;
                    var question = Lines["question"];
                    return english ? question.en : question.ru;
                case QuestionPurpose.GamePreference: return Register(intent, Game);
                case QuestionPurpose.Opinion: return Register(intent, plan.RelationshipTone == RelationshipTier.Wary ? WaryOpinion : Opinion);
                default: return null; // origin/location and anything unclassified: nothing true to say without the model
            }
        }

        private static string[] Register(ReactionIntent intent, (string[] casual, string[] plain, string[] en) lines)
        {
            if (intent.Viewer.Persona.Language == ViewerLanguage.English) return lines.en;
            ChatStyle style = intent.Viewer.Persona.Profile?.Style;
            if (style != null && style.Slang == 0) return lines.plain;
            if (style == null || style.Case != LetterCase.Normal) return lines.casual;
            var capitalized = new string[lines.casual.Length];
            for (int i = 0; i < capitalized.Length; i++) capitalized[i] = char.ToUpperInvariant(lines.casual[i][0]) + lines.casual[i].Substring(1);
            return capitalized;
        }

        // The authored phrase in the viewer's letter case; null when this viewer has none in their language.
        private static string Said(string phrase, ReactionIntent intent)
        {
            if (string.IsNullOrWhiteSpace(phrase)) return null;
            phrase = phrase.Trim();
            LetterCase letters = intent.Viewer.Persona.Profile?.Style.Case ?? LetterCase.Lowercase;
            return letters == LetterCase.Normal ? char.ToUpperInvariant(phrase[0]) + phrase.Substring(1) : Lower(phrase, intent);
        }

        private static string Lower(string phrase, ReactionIntent intent) =>
            (intent.Viewer.Persona.Profile?.Style.Case ?? LetterCase.Lowercase) == LetterCase.Normal
                ? char.ToLowerInvariant(phrase[0]) + phrase.Substring(1) : phrase.ToLowerInvariant();

        private static string First(string[] options, AudienceRandom random, IReadOnlyList<StreamChatMessage> recentChat)
        {
            if (options == null || options.Length == 0) return null;
            int start = random.NextInt(options.Length);
            for (int i = 0; i < options.Length; i++)
            {
                string text = options[(start + i) % options.Length];
                if (!Recently(text, recentChat)) return text;
            }
            return null;
        }

        private static string Key(ReactionIntent intent, AudienceRandom random)
        {
            StreamEvent e = intent.Event;
            bool own = intent.Direct && e.SubjectViewerId == intent.Viewer.ViewerId;
            // Only the first reaction to a moment gets a fallback line; a model outage makes the chat quieter.
            if (!intent.Direct && intent.Order > 0) return null;
            switch (e.Kind)
            {
                case StreamEventKind.StreamStarted: return "greeting";
                case StreamEventKind.ViewerJoined: return own ? "greeting" : null;
                case StreamEventKind.StreamerSpeech:
                    SpeechAnalysis speech = e.Speech;
                    // Thanked by name (or for a donation): "да не за что", never "а?".
                    if (own || speech.Has(SpeechCue.Thanks) && speech.MentionedViewerIds.Contains(intent.Viewer.ViewerId)) return "thanks";
                    if (speech.Has(SpeechCue.Farewell)) return "farewell";
                    // Asked about themselves, a person answers something, never "hi" or "хз".
                    if (speech.Is(SpeechAct.PersonalQuestion) || intent.FollowUp && SpeechRelevance.ContinuesConversation(speech)) return "personal";
                    if (speech.Has(SpeechCue.Greeting)) return "greeting";
                    if (speech.MentionedViewerIds.Contains(intent.Viewer.ViewerId)) return "direct";
                    if (speech.Has(SpeechCue.Question)) return "question";
                    if (speech.Has(SpeechCue.Emotional)) return "emotional";
                    return "speech";
                case StreamEventKind.StreamerSilence: return "silence";
                case StreamEventKind.StreamerAway: return "away";
                case StreamEventKind.Donation: return own ? "donation.own" : "donation";
                case StreamEventKind.Follow: return own ? "follow.own" : "support";
                case StreamEventKind.Subscription: return own ? "subscription.own" : "support";
                case StreamEventKind.AudienceMilestone: return "milestone";
                case StreamEventKind.PeripheralChanged:
                    return (e.Peripheral == PcPeripheralKind.Microphone ? "microphone." : "webcam.") + (e.Connected ? "on" : "off");
                case StreamEventKind.AudienceChatter: return random.NextDouble() < .25 ? "ambient" : null;
                default: return null;
            }
        }

        private static bool Recently(string text, IReadOnlyList<StreamChatMessage> recentChat)
        {
            if (recentChat == null) return false;
            for (int i = recentChat.Count - 1, seen = 0; i >= 0 && seen < 12; i--, seen++)
                if (string.Equals(recentChat[i].Text, text, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
