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

        public static string Pick(ReactionIntent intent, AudienceRandom random, IReadOnlyList<StreamChatMessage> recentChat)
        {
            if (intent == null) throw new ArgumentNullException(nameof(intent));
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
                    if (own) return "thanks";
                    if (speech.Has(SpeechCue.Farewell)) return "farewell";
                    // Asked about themselves, a person answers something, never "hi" or "хз".
                    if (speech.Is(SpeechAct.PersonalQuestion) || intent.FollowUp && SpeechRelevance.ContinuesConversation(speech)) return "personal";
                    if (speech.MentionedViewerIds.Contains(intent.Viewer.ViewerId)) return "direct";
                    if (speech.Has(SpeechCue.Greeting)) return "greeting";
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
