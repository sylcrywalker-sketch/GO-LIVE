using System.Collections.Generic;

namespace GoLive.Viewers
{
    public enum QuestionPurpose
    {
        None, Mood, ReasonForMood, TodayActivity, CurrentActivity, GamePreference,
        Opinion, Explanation, Confirmation, OriginLocation, GenericQuestion
    }

    // Pure, bounded interpretation of a question C# has already selected for this viewer. It owns no state,
    // subscriptions or persistence; it only chooses the relevant fields of an existing day for one answer.
    internal static class ViewerQuestionPurpose
    {
        internal static QuestionPurpose Resolve(ReactionIntent intent, string previous)
        {
            SpeechAnalysis speech = intent.Event.Kind == StreamEventKind.StreamerSpeech ? intent.Event.Speech : null;
            if (speech == null || speech.Is(SpeechAct.Filler)) return QuestionPurpose.None;
            if (!speech.AsksForAnswer && !speech.Has(SpeechCue.Question) &&
                !(intent.FollowUp && SpeechRelevance.ConversationAsksForAnswer(speech))) return QuestionPurpose.None;

            string phrase = WithoutViewerName(Words(speech.Text), intent.Viewer);
            bool aboutSomeoneElse = AboutSomeoneElse(phrase);
            if (Has(phrase, " почему ", " отчего ", " why ", " how come "))
                return !aboutSomeoneElse && (MoodWords(phrase) || intent.FollowUp && EllipticalReason(phrase) && MoodWords(Words(previous)))
                    ? QuestionPurpose.ReasonForMood : QuestionPurpose.Explanation;
            if (!aboutSomeoneElse)
            {
                if (Has(phrase, " откуда ", " где жив", " где ты ", " where are you from ", " where do you live "))
                    return QuestionPurpose.OriginLocation;
                if (AsksToday(phrase)) return QuestionPurpose.TodayActivity;
                if (Has(phrase, " сейчас дела", " что делаешь ", " что делаете ", " чем занят", " чем занимаешься ",
                    " чем занимаетесь ", " what are you doing ", " what are you up to ")) return QuestionPurpose.CurrentActivity;
                if (Has(phrase, " устал", " вымотал", " tired ", " правда ", " серьезно ", " really ", " are you sure ",
                    " согласен ", " согласны ")) return QuestionPurpose.Confirmation;
                if (AsksMood(phrase)) return QuestionPurpose.Mood;
            }
            if (Has(phrase, " о чем ", " что значит ", " what do you mean ", " explain ") ||
                intent.FollowUp && RefersToPrevious(phrase))
                return QuestionPurpose.Explanation;
            if ((speech.Is(SpeechAct.GameplayQuestion) || (speech.Topics & StreamTopic.Games) != 0) &&
                Has(phrase, " запустить ", " поигр", " сыгра", " предпочита", " любишь ", " любите ", " нравит", " нравят",
                    " посовет", " выбрать ", " play ", " prefer ", " favorite ", " favourite ", " recommend "))
                return QuestionPurpose.GamePreference;
            if (speech.Is(SpeechAct.OpinionRequest)) return QuestionPurpose.Opinion;
            // PersonalQuestion / GameplayQuestion are broad social cues, not evidence that the day contains
            // the answer (a meal, game price, etc.). Unknown subtypes retain a direct generic answer task.
            return QuestionPurpose.GenericQuestion;
        }

        internal static bool UsesDay(QuestionPurpose purpose, string question, string previous) => purpose switch
        {
            QuestionPurpose.Mood or QuestionPurpose.ReasonForMood or QuestionPurpose.TodayActivity or QuestionPurpose.CurrentActivity => true,
            QuestionPurpose.Confirmation => MoodWords(Words(question)) || MoodWords(Words(previous)),
            QuestionPurpose.Explanation => DailyLine(Words(previous)) && RefersToPrevious(Words(question)),
            _ => false
        };

        // These are separate soft facts, never authority for claims about the streamer or their equipment.
        internal static List<GroundedFact> Facts(QuestionPurpose purpose, string question, string previous, ViewerDailyState day)
        {
            var facts = new List<GroundedFact>(3);
            if (day == null || !UsesDay(purpose, question, previous)) return facts;
            string phrase = Words(question);
            if (purpose == QuestionPurpose.Mood || purpose == QuestionPurpose.ReasonForMood ||
                purpose == QuestionPurpose.Confirmation || purpose == QuestionPurpose.Explanation || AsksMood(phrase))
                Add(facts, "mood " + day.Mood.ToString().ToLowerInvariant() + ", energy " + day.Energy.ToString().ToLowerInvariant(), "Mood");
            if (purpose == QuestionPurpose.TodayActivity || purpose == QuestionPurpose.ReasonForMood ||
                purpose == QuestionPurpose.Explanation || AsksToday(phrase))
                Add(facts, "Today: " + day.Activity.Today, "Today");
            if (purpose == QuestionPurpose.CurrentActivity || purpose == QuestionPurpose.Mood || purpose == QuestionPurpose.ReasonForMood)
                Add(facts, "Now: " + day.Activity.Now, "Now");
            return facts;
        }

        internal static string Task(ViewerUtterancePlan plan)
        {
            string action = plan.QuestionPurpose switch
            {
                QuestionPurpose.Mood => "Answer how you are, using the supplied mood and current state",
                QuestionPurpose.ReasonForMood => "Answer why you feel that way, using the supplied day as the reason",
                QuestionPurpose.TodayActivity => "Answer what you did today or how your day went",
                QuestionPurpose.CurrentActivity => "Answer what you are doing right now",
                QuestionPurpose.GamePreference => "Answer with a kind of game you prefer; do not invent a specific title",
                QuestionPurpose.Opinion => "Answer with your opinion about the actual question",
                QuestionPurpose.Explanation => "Explain the point the streamer asked about; use YOUR PREVIOUS MESSAGE only when relevant",
                QuestionPurpose.Confirmation => "Confirm or correct what the streamer asked, using supplied facts",
                QuestionPurpose.OriginLocation => "Answer about your origin or location only if supplied in WHO; otherwise say you have not said",
                _ => "Answer the actual question directly using supplied facts; if unknown, say so briefly"
            };
            if (plan.QuestionPurpose == QuestionPurpose.TodayActivity &&
                HasFact(plan.DirectAnswerFacts, "Mood")) action += ", including how you feel";
            if (plan.Personal && plan.DirectAnswerFacts.Count == 0)
                action += "; the relevant day facts are not supplied, so do not invent an activity or cause";
            return action + ". Do not change topic or add new causes, people or events.";
        }

        private static bool HasFact(IReadOnlyList<GroundedFact> facts, string label)
        {
            foreach (GroundedFact fact in facts) if (fact.Label == label) return true;
            return false;
        }

        private static void Add(List<GroundedFact> facts, string text, string label) =>
            facts.Add(new GroundedFact(FactSource.ViewerDay, ChatContextBuilder.Clean(text, ChatContextBuilder.QuoteLimit), label));

        private static bool AsksToday(string phrase) =>
            Has(phrase, " сегодня дел", " сегодня занима", " сегодня игра", " делал сегодня ", " делали сегодня ",
                " делала сегодня ", " день прош", " прошел день ", " прошел твой день ", " прошел ваш день ",
                " как день ", " как твой день ", " как ваш день ", " what did you do ", " how was your day ");

        private static bool AsksMood(string phrase) =>
            phrase.Contains(" как ") && Has(phrase, " дела ", " делишки ", " настроен", " самочувств", " пожива", " сам ") ||
            Has(phrase, " how are you ", " how r u ", " how you doing ", " how is it going ", " hows it going ", " how is your mood ");

        private static bool MoodWords(string phrase) =>
            Has(phrase, " настроен", " скуч", " устал", " вымотал", " груст", " расстро", " bored ", " tired ", " mood ", " stressed ", " sad ");

        // The target viewer can be asked about someone else. A request recipient ("расскажите мне") is not
        // the subject of the answer; dative pronouns only count in explicit personal question forms here.
        private static bool AboutSomeoneElse(string phrase) =>
            Has(phrase, " у меня ", " у него ", " у нее ", " у них ", " я ", " он ", " она ", " они ",
                " почему мне ", " отчего мне ", " как мне ", " почему ему ", " почему ей ", " почему им ",
                " i ", " my ", " he ", " she ", " they ", " his ", " her ", " their ") &&
            !Has(phrase, " а ты ", " а вы ", " а у тебя ", " а у вас ", " and you ");

        private static bool DailyLine(string phrase) => MoodWords(phrase) ||
            Has(phrase, " в школе ", " урок", " учеб", " работ", " смен", " рисовал", " рисую ", " отдых", " спал", " гулял",
                " играл", " проиграл", " катал", " school ", " class", " work", " shift ", " draw", " sketch", " rest", " slept ",
                " walk", " played ", " gaming ");

        // The prior mood may fill in an omitted subject ("why?"); it must not replace an explicit new subject
        // ("why is the sky blue?"). This is a small set of conversational short forms, not general NLP.
        private static bool EllipticalReason(string phrase) => phrase.Trim() switch
        {
            "почему" or "а почему" or "и почему" or "ну почему" or "почему так" or "а почему так" or
            "почему же" or "почему это" or "отчего" or "why" or "why is that" or "how come" => true,
            _ => false
        };

        private static bool RefersToPrevious(string phrase) => EllipticalReason(phrase) ||
            Has(phrase, " о чем ты ", " о чем речь ", " что ты имеешь в виду ", " what do you mean ") || (phrase.Trim() switch
            {
                "как" or "и как" or "а как" or "как прошло" or "как получилось" or "долго" or "how did it go" => true,
                _ => false
            });

        private static string Words(string text) => " " + SpeechRelevance.Normalize(text ?? "") + " ";

        private static string WithoutViewerName(string phrase, ChatParticipant viewer)
        {
            // A known addressee is not a new topic in "kritik, why?". Prefer the full name over its short form.
            string longest = null;
            foreach (string form in viewer.NameForms)
            {
                string name = Words(form);
                if (name.Length > 2 && phrase.Contains(name) && (longest == null || name.Length > longest.Length)) longest = name;
            }
            return longest == null ? phrase : phrase.Replace(longest, " ");
        }

        private static bool Has(string phrase, params string[] fragments)
        {
            foreach (string fragment in fragments) if (phrase.Contains(fragment)) return true;
            return false;
        }
    }
}
