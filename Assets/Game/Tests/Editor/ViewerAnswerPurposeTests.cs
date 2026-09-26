using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GoLive.Viewers;
using NUnit.Framework;

namespace GoLive.Tests
{
    public sealed class ViewerAnswerPurposeTests
    {
        private const string PreviousMood = "без настроения но здесь сижу";
        private const string Today = "had boring school lessons today";
        private const string Now = "lying on the bed watching streams";

        private static (ReactionIntent intent, ChatSituation situation) Prepare(string question,
            string previous = null, long id = 1, string today = Today, bool includeDay = true)
        {
            var profile = UnityEngine.JsonUtility.FromJson<ViewerProfile>(
                UnityEngine.JsonUtility.ToJson(ViewerProfileTests.Profile("viewer.kritik228")));
            profile.DailyLife = new[] { new DailyActivity { Id = "answer.study", Today = today, Now = Now,
                Kind = DayActivityKind.Study, Mood = ViewerMood.Bored, Energy = ViewerEnergy.Low } };
            var viewer = profile.Participant();
            var intent = ViewerUtterancePlanTests.Make(id, ViewerUtterancePlanTests.Said(question, viewer), viewer, true);
            var chat = new StreamChat();
            if (previous != null)
            {
                typeof(ReactionIntent).GetProperty("FollowUp").SetValue(intent, true);
                chat.Add("answers", viewer.ViewerId, viewer.DisplayName, previous, 9, 1, ReactionSource.LanguageModel);
            }
            var situation = new ChatSituation("test", 12, 3, ViewerLanguage.Russian, chat.Messages, null);
            if (includeDay) typeof(ChatSituation).GetProperty("Day").SetValue(situation,
                ViewerDailyLife.For(viewer, 0, "answers", RelationshipTier.Neutral));
            return (intent, situation);
        }

        // Reflection keeps the red regression compilable before the new public plan fields exist.
        private static object Field(ViewerUtterancePlan plan, string name)
        {
            var property = typeof(ViewerUtterancePlan).GetProperty(name);
            Assert.That(property, Is.Not.Null, "The answer plan must expose " + name);
            return property.GetValue(plan);
        }

        private static string Purpose(ViewerUtterancePlan plan) => Field(plan, "QuestionPurpose").ToString();
        private static IReadOnlyList<GroundedFact> AnswerFacts(ViewerUtterancePlan plan) =>
            (IReadOnlyList<GroundedFact>)Field(plan, "DirectAnswerFacts");

        [TestCase("как дела?", null, "Mood")]
        [TestCase("да как у вас у всех дела расскажите мне пожалуйста", null, "Mood")]
        [TestCase("что сегодня делал?", null, "TodayActivity")]
        [TestCase("Зина, а у тебя как день прошёл?", null, "TodayActivity")]
        [TestCase("что сейчас делаешь?", null, "CurrentActivity")]
        [TestCase("Почему без настроения?", PreviousMood, "ReasonForMood")]
        [TestCase("почему?", PreviousMood, "ReasonForMood")]
        [TestCase("критик, почему?", PreviousMood, "ReasonForMood")]
        [TestCase("why?", "feeling bored today", "ReasonForMood")]
        [TestCase("почему?", "я выбираю такие игры", "Explanation")]
        [TestCase("О чем ты, критик?", "я про стрим", "Explanation")]
        [TestCase("жесть, устал наверное?", "в школе весь день", "Confirmation")]
        [TestCase("и как?", "в школе весь день", "Explanation")]
        [TestCase("какую игру лучше запустить?", null, "GamePreference")]
        [TestCase("как тебе стрим?", null, "Opinion")]
        [TestCase("откуда ты?", null, "OriginLocation")]
        [TestCase("where are you from?", null, "OriginLocation")]
        [TestCase("сколько будет два плюс два?", null, "GenericQuestion")]
        [TestCase("так, секунду", null, "None")]
        public void QuestionPurposeFollowsTheActualQuestion(string question, string previous, string expected)
        {
            var (intent, situation) = Prepare(question, previous);
            Assert.That(Purpose(ViewerUtterancePlanner.For(intent, situation)), Is.EqualTo(expected));
        }

        [Test]
        public void RealMoodFollowUpGetsTheViewersStructuredReason()
        {
            var (intent, situation) = Prepare("Почему без настроения?", PreviousMood);
            var plan = ViewerUtterancePlanner.For(intent, situation);
            Assert.That(Purpose(plan), Is.EqualTo("ReasonForMood"));
            Assert.That(plan.Intent, Is.EqualTo(UtteranceIntent.Answer));
            Assert.That(plan.Target, Is.EqualTo(UtteranceTarget.Streamer));
            var facts = AnswerFacts(plan);
            Assert.That(facts.All(f => f.Source == FactSource.ViewerDay), Is.True);
            var text = string.Join("\n", facts.Select(f => f.Text));
            Assert.That(text, Does.Contain("bored").And.Contain(Today).And.Contain(Now));
            Assert.That(text, Does.Not.Contain("девочка").And.Not.Contain("Openness"));
        }

        [TestCase("что сегодня делал?", Today, Now)]
        [TestCase("что сейчас делаешь?", Now, Today)]
        public void PersonalAnswerAuthorizesOnlyRelevantDayFields(string question, string included, string excluded)
        {
            var (intent, situation) = Prepare(question);
            var plan = ViewerUtterancePlanner.For(intent, situation);
            Assert.That(string.Join("\n", AnswerFacts(plan).Select(f => f.Text)), Does.Contain(included).And.Not.Contain(excluded));
            Assert.That(string.Join("\n", plan.AllowedFacts.Where(f => f.Source == FactSource.ViewerDay).Select(f => f.Text)),
                Does.Contain(included).And.Not.Contain(excluded));
        }

        [TestCase("сколько будет два плюс два?")]
        [TestCase("where are you from?")]
        [TestCase("почему небо синее?")]
        public void UnrelatedQuestionDoesNotAuthorizeDailyFactsEvenInsideAConversation(string question)
        {
            var (intent, situation) = Prepare(question, PreviousMood);
            var plan = ViewerUtterancePlanner.For(intent, situation);
            Assert.That(AnswerFacts(plan), Is.Empty);
            Assert.That(plan.AllowedFacts.Any(f => f.Source == FactSource.ViewerDay), Is.False);
            Assert.That(ChatContextBuilder.Build(intent, situation, 64).User, Does.Not.Contain(Today).And.Not.Contain(Now));
        }

        [Test]
        public void CombinedGroupQuestionIncludesBothStateAndTodaysActivity()
        {
            var (intent, situation) = Prepare("как у вас дела, что сегодня делали?");
            var plan = ViewerUtterancePlanner.For(intent, situation);
            var facts = string.Join("\n", AnswerFacts(plan).Select(f => f.Text));
            Assert.That(facts, Does.Contain("bored").And.Contain(Today));
            Assert.That(plan.Intent, Is.EqualTo(UtteranceIntent.Answer));
        }

        [Test]
        public void FollowUpPromptPairsThePriorMessageWithTheStreamersQuestionAndAnswerTask()
        {
            var (intent, situation) = Prepare("Почему без настроения?", PreviousMood);
            var request = ChatContextBuilder.Build(intent, situation, 64);
            Assert.That(request.User, Does.Contain("YOUR PREVIOUS MESSAGE: «" + PreviousMood + "»"));
            Assert.That(request.User, Does.Contain("STREAMER REPLIED TO YOU: «Почему без настроения?»"));
            Assert.That(request.User, Does.Contain("QUESTION PURPOSE: ReasonForMood").And.Contain("YOUR TASK:"));
            Assert.That(request.User, Does.Contain("Answer why").And.Contain("Do not change topic"));
            Assert.That(request.System, Does.Contain("Personality changes wording, never replaces the answer"));
            Assert.That(request.System, Does.Not.Contain("with small harmless everyday details"));
        }

        [Test]
        public void ThreadSnapshotWinsOverMoreRecentOwnChatWhenGenerationStarts()
        {
            var (intent, situation) = Prepare("Почему без настроения?", "это другая более поздняя реплика");
            var previous = typeof(ReactionIntent).GetProperty("PreviousViewerLine");
            Assert.That(previous, Is.Not.Null, "The selected conversation must snapshot its own LastLine.");
            previous.SetValue(intent, PreviousMood);
            var plan = ViewerUtterancePlanner.For(intent, situation);
            Assert.That(Field(plan, "PreviousViewerLine"), Is.EqualTo(PreviousMood));
            Assert.That(plan.AllowedFacts.Single(f => f.Source == FactSource.OwnLine).Text, Is.EqualTo(PreviousMood));
        }

        [Test]
        public void PersonalQuestionAlwaysAnswersBeforeTheViewersPersonality()
        {
            for (long id = 1; id <= 80; id++)
            {
                var (intent, situation) = Prepare("критик, почему без настроения?", PreviousMood, id);
                Assert.That(ViewerUtterancePlanner.For(intent, situation).Intent, Is.EqualTo(UtteranceIntent.Answer), "reaction " + id);
            }
        }

        [Test]
        public void MissingDayDoesNotCreateAFictionalMoodCause()
        {
            var (intent, situation) = Prepare("Почему без настроения?", PreviousMood, includeDay: false);
            var plan = ViewerUtterancePlanner.For(intent, situation);
            Assert.That(Purpose(plan), Is.EqualTo("ReasonForMood"));
            Assert.That(AnswerFacts(plan), Is.Empty);
            Assert.That(ChatContextBuilder.Build(intent, situation, 64).User, Does.Contain("not supplied").And.Contain("do not invent"));
        }

        [Test]
        public void DirectAnswerDayAndPriorMessageCannotAuthorizeStreamerHardware()
        {
            var (intent, situation) = Prepare("почему?", PreviousMood + ", работал с rtx 4090",
                today: "had boring school lessons with rtx 4090 for 12 hours");
            var plan = ViewerUtterancePlanner.For(intent, situation);
            Assert.That(AnswerFacts(plan).Any(f => f.Text.Contains("rtx 4090")), Is.True);
            Assert.That(ChatOutputValidator.Validate("у тебя rtx 4090", intent, null, situation).Accepted, Is.False);
            Assert.That(ChatOutputValidator.Validate("стрим уже 12 часов", intent, null, situation).Accepted, Is.False);
        }

        [Test]
        public void PriorMessageAndDayFactsStayBoundedInTheAnswerEnvelope()
        {
            var (intent, situation) = Prepare("Почему без настроения?", PreviousMood + new string('я', 2000), today: Today + new string('x', 2000));
            var plan = ViewerUtterancePlanner.For(intent, situation);
            Assert.That(((string)Field(plan, "PreviousViewerLine")).Length, Is.LessThanOrEqualTo(ChatContextBuilder.QuoteLimit));
            Assert.That(AnswerFacts(plan).All(f => f.Text.Length <= ChatContextBuilder.QuoteLimit), Is.True);
        }

        [TestCase("ребят, как вам стрим?", "Opinion")]
        [TestCase("ребят, какую игру лучше запустить?", "GamePreference")]
        [TestCase("ребят, сколько будет два плюс два?", "GenericQuestion")]
        public void GroupSiblingAnswersTheStreamerInsteadOfBeingInvitedToTalkToOtherViewers(string question, string expected)
        {
            var viewer = ViewerProfileTests.Profile("viewer.nightowl").Participant();
            var intent = (ReactionIntent)typeof(ReactionIntent).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single(c => c.GetParameters().Length == 8).Invoke(new object[]
                { 2L, ViewerUtterancePlanTests.Said(question), viewer, false, 1, 10d, 30d, 1L });
            var group = typeof(ConversationTarget).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single().Invoke(new object[] { ConversationTargetKind.Group, null, 0L });
            typeof(ReactionIntent).GetProperty("ConversationTarget").SetValue(intent, group);
            var situation = new ChatSituation("test", 12, 3, ViewerLanguage.Russian, null, null);
            var plan = ViewerUtterancePlanner.For(intent, situation);
            Assert.That(Purpose(plan), Is.EqualTo(expected));
            Assert.That(plan.Target, Is.EqualTo(UtteranceTarget.Streamer));
            var prompt = ChatContextBuilder.Build(intent, situation, 64).User;
            Assert.That(prompt, Does.Contain("YOUR TASK:").And.Contain("your own distinct answer to the streamer"));
            Assert.That(prompt, Does.Not.Contain("or react to them"));
            Assert.That(AnswerFacts(plan), Is.Empty, "Nonpersonal group questions do not authorize daily life.");
        }

        [TestCase("Критик, что ты ел сегодня?", "GenericQuestion")]
        [TestCase("сколько стоит эта игра?", "GenericQuestion")]
        [TestCase("Критик, почему у меня плохое настроение?", "Explanation")]
        [TestCase("Критик, почему у него плохое настроение?", "Explanation")]
        public void UnanswerableOrStreamerQuestionsCannotBorrowUnrelatedViewerState(string question, string expected)
        {
            var (intent, situation) = Prepare(question, PreviousMood);
            var plan = ViewerUtterancePlanner.For(intent, situation);
            Assert.That(Purpose(plan), Is.EqualTo(expected));
            Assert.That(AnswerFacts(plan), Is.Empty);
            Assert.That(plan.AllowedFacts.Any(f => f.Source == FactSource.ViewerDay), Is.False);
            var prompt = ChatContextBuilder.Build(intent, situation, 64).User;
            Assert.That(prompt, Does.Not.Contain(Today).And.Not.Contain(Now));
            Assert.That(prompt, Does.Not.Contain("a kind of game").And.Not.Contain("asking what to play"));
        }

        [Test]
        public void HowDidItGoAfterRankedGamesGetsTheActualRankedDayFacts()
        {
            var profile = UnityEngine.JsonUtility.FromJson<ViewerProfile>(
                UnityEngine.JsonUtility.ToJson(ViewerProfileTests.Profile("viewer.kritik228")));
            var ranked = profile.DailyLife.Single(a => a.Id == "krit.ranked");
            profile.DailyLife = new[] { ranked };
            var viewer = profile.Participant();
            var intent = ViewerUtterancePlanTests.Make(1, ViewerUtterancePlanTests.Said("и как?", viewer), viewer, true);
            typeof(ReactionIntent).GetProperty("FollowUp").SetValue(intent, true);
            typeof(ReactionIntent).GetProperty("PreviousViewerLine").SetValue(intent, "сегодня играл в рейтинге, проигрывал");
            var situation = new ChatSituation("test", 12, 3, ViewerLanguage.Russian, null, null);
            typeof(ChatSituation).GetProperty("Day").SetValue(situation,
                ViewerDailyLife.For(viewer, 0, "answers", RelationshipTier.Neutral));
            var plan = ViewerUtterancePlanner.For(intent, situation);
            Assert.That(Purpose(plan), Is.EqualTo("Explanation"));
            Assert.That(string.Join("\n", AnswerFacts(plan).Select(f => f.Text)),
                Does.Contain("stressed").And.Contain(ranked.Today));
        }
    }
}
