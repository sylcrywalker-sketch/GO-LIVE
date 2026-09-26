using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GoLive.Desktop;
using GoLive.Viewers;
using NUnit.Framework;

namespace GoLive.Tests
{
    // What C# can guarantee about a direct answer: the prompt carries one purpose-specific task with the right facts,
    // the fallback answers that purpose from the same facts or stays silent, and hard game facts stay protected.
    // Nothing here asserts model wording.
    public sealed class ViewerConversationQualityTests
    {
        private const string Kritik = "viewer.kritik228";
        private const string PreviousMood = "без настроения но здесь сижу";
        private const string RankedReason = "весь день катал ранкед и сливал";
        private const string RankedNow = "по мелким стримам прыгаю";
        // Stock lines that fit no particular question: never a fallback for a question C# has classified.
        private static readonly string[] StockLines =
        {
            "норм, а ты как?", "да потихоньку, отдыхаю", "нормально, день длинный был", "всё ок, вот стрим смотрю", "живой, а у тебя как?",
            "а?", "чего", "хз", "не знаю даже", "без понятия", "а сам как думаешь"
        };

        private static ChatParticipant Participant(string id) => ViewerProfileTests.Profile(id).Participant();

        private static ViewerDailyState Day(ChatParticipant viewer, string id, RelationshipTier tier = RelationshipTier.Neutral)
        {
            DailyActivity activity = viewer.Persona.Profile.DailyLife.Single(a => a.Id == id);
            return (ViewerDailyState)Activator.CreateInstance(typeof(ViewerDailyState), BindingFlags.Instance | BindingFlags.NonPublic, null,
                new object[] { activity, activity.Mood, activity.Energy, ConversationOpenness.Normal }, null);
        }

        private static (ReactionIntent intent, ChatSituation situation) Ask(string id, string question, string day, string previous = null,
            RelationshipTier tier = RelationshipTier.Neutral, bool withDay = true, long intentId = 7)
        {
            ChatParticipant viewer = Participant(id);
            var roster = ReactionFoundationTests.Roster(3, viewer);
            var intent = ViewerUtterancePlanTests.Make(intentId, ViewerUtterancePlanTests.Said(question, viewer), viewer, true, roster.Epoch(viewer.ViewerId));
            var target = Activator.CreateInstance(typeof(ConversationTarget), BindingFlags.Instance | BindingFlags.NonPublic, null,
                new object[] { previous == null ? ConversationTargetKind.SpecificViewer : ConversationTargetKind.ActiveThreadViewer, viewer.ViewerId,
                    roster.Epoch(viewer.ViewerId) }, null);
            typeof(ReactionIntent).GetProperty("ConversationTarget").SetValue(intent, target);
            typeof(ReactionIntent).GetProperty("Conversational").SetValue(intent, true);
            var chat = new StreamChat();
            if (previous != null)
            {
                typeof(ReactionIntent).GetProperty("FollowUp").SetValue(intent, true);
                typeof(ReactionIntent).GetProperty("PreviousViewerLine").SetValue(intent, previous);
                chat.Add("quality", viewer.ViewerId, viewer.DisplayName, previous, 590, 1, ReactionSource.LanguageModel);
            }
            var situation = new ChatSituation("quality", 600, 3, ViewerLanguage.Russian, chat.Messages.ToArray(), question,
                ViewerUtterancePlanner.Describe(tier, 1, 0), tier: tier);
            if (withDay && day != null) typeof(ChatSituation).GetProperty("Day").SetValue(situation, Day(viewer, day, tier));
            return (intent, situation);
        }

        private static string Prompt(ReactionIntent intent, ChatSituation situation) => ChatContextBuilder.Build(intent, situation, 64).User;

        private static List<string> Fallbacks(ReactionIntent intent, ChatSituation situation, int seeds = 20) =>
            Enumerable.Range(1, seeds).Select(seed => FallbackChat.Pick(intent, new AudienceRandom((ulong)seed), situation.RecentChat, situation)).ToList();

        [Test]
        public void ReasonForMoodPromptAsksWhyWithTheSuppliedReasonAndNothingThatCompetesWithIt()
        {
            var (intent, situation) = Ask(Kritik, "Почему без настроения?", "krit.ranked", PreviousMood, RelationshipTier.Wary);
            ViewerUtterancePlan plan = ViewerUtterancePlanner.For(intent, situation);
            Assert.That(plan.QuestionPurpose, Is.EqualTo(QuestionPurpose.ReasonForMood));
            string prompt = Prompt(intent, situation);

            Assert.That(prompt, Does.Contain("YOUR TASK: Answer why you feel this way"));
            Assert.That(prompt, Does.Contain("the reason, what happened today: " + RankedReason), "the authored Russian reason, not an English fact to calque");
            Assert.That(prompt, Does.Contain("how you feel: на нервах"));
            Assert.That(prompt, Does.Not.Contain(RankedNow).And.Not.Contain("hopping between"), "the current activity is not the reason");
            Assert.That(prompt.TrimEnd().Split('\n').Last(), Does.Contain("why you feel this way"), "the task is the last thing the model reads");
            foreach (string metadata in new[] { "Stream: live for", "QUESTION PURPOSE", "SOCIAL ACTION", "TOPIC:", "NOT KNOWN", "the hardware is unknown" })
                Assert.That(prompt, Does.Not.Contain(metadata), metadata);
            Assert.That(prompt, Does.Contain("dry and brief, but you still really answer"), "relationship shapes tone, never whether it answers");
        }

        [Test]
        public void PreviousLineIsPairedWithTheQuestionAndMarkedAsTheViewersOwn()
        {
            var (intent, situation) = Ask(Kritik, "Почему без настроения?", "krit.ranked", PreviousMood, RelationshipTier.Wary);
            string prompt = Prompt(intent, situation);
            int previous = prompt.IndexOf("YOUR PREVIOUS MESSAGE: «" + PreviousMood + "»", StringComparison.Ordinal);
            int question = prompt.IndexOf("STREAMER REPLIED TO YOU: «Почему без настроения?»", StringComparison.Ordinal);
            Assert.That(previous, Is.GreaterThanOrEqualTo(0));
            Assert.That(question, Is.GreaterThan(previous));
            Assert.That(prompt, Does.Contain("kritik228 (you): «" + PreviousMood + "»"));
            Assert.That(prompt, Does.Not.Contain("other people's messages"), "a viewer's own line is never presented as someone else's");
        }

        [TestCase("критик, а ты что сегодня делал?", "krit.school", "весь день на скучных уроках сидел", "валяюсь на кровати")]
        [TestCase("критик, что делаешь?", "krit.school", "валяюсь на кровати, стримы листаю", "на скучных уроках")]
        [TestCase("критик, как настроение?", "krit.nothing", "how you feel: скучно, сил мало", "весь день ничего не делал")]
        public void EachPurposeSuppliesOnlyItsOwnFactsInTheViewersLanguage(string question, string day, string included, string excluded)
        {
            var (intent, situation) = Ask(Kritik, question, day, tier: RelationshipTier.Wary);
            string prompt = Prompt(intent, situation);
            Assert.That(prompt, Does.Contain(included));
            Assert.That(prompt, Does.Not.Contain(excluded));
        }

        [Test]
        public void ReasonFallbackUsesTheSuppliedReasonAndNeverAStockLine()
        {
            var (intent, situation) = Ask(Kritik, "Почему без настроения?", "krit.ranked", PreviousMood, RelationshipTier.Wary);
            List<string> lines = Fallbacks(intent, situation);
            Assert.That(lines, Is.All.EqualTo(RankedReason));
        }

        [TestCase(Kritik, "критик, как настроение?", "krit.ranked")]
        [TestCase(Kritik, "критик, что сегодня делал?", "krit.school")]
        [TestCase(Kritik, "критик, чем занят?", "krit.nothing")]
        [TestCase("viewer.zinaivanovna", "Зина Ивановна, как поживаете?", "zina.clinic")]
        [TestCase("viewer.pixelfox", "Пиксель, во что поиграть?", "fox.photos")]
        [TestCase("viewer.pixelfox", "Пиксель, как тебе стрим?", "fox.photos")]
        public void DirectQuestionFallbackNeverUsesAnUnrelatedStockLine(string id, string question, string day)
        {
            var (intent, situation) = Ask(id, question, day);
            List<string> lines = Fallbacks(intent, situation);
            Assert.That(lines, Has.None.Null, "a truthful purpose answer exists for this question");
            Assert.That(lines.Intersect(StockLines), Is.Empty, string.Join(" | ", lines.Distinct()));
        }

        [Test]
        public void MoodFallbackFollowsTheSuppliedMood()
        {
            var (stressed, stressedSituation) = Ask(Kritik, "критик, как настроение?", "krit.ranked");
            Assert.That(Fallbacks(stressed, stressedSituation), Is.All.Contain("нерв"));
            var (bored, boredSituation) = Ask(Kritik, "критик, как настроение?", "krit.nothing");
            Assert.That(Fallbacks(bored, boredSituation), Is.All.Contain("скучн"));
        }

        [Test]
        public void NoTruthfulFallbackMeansSilence()
        {
            // The mood question has no day to answer from.
            var (noDay, noDaySituation) = Ask(Kritik, "Почему без настроения?", "krit.ranked", PreviousMood, withDay: false);
            Assert.That(Fallbacks(noDay, noDaySituation), Is.All.Null);
            // Nothing C# knows explains a nonsense line.
            var (nonsense, nonsenseSituation) = Ask(Kritik, "О чем ты, критик?", "krit.ranked", "девочка с девятого этажа");
            Assert.That(ViewerUtterancePlanner.For(nonsense, nonsenseSituation).QuestionPurpose, Is.EqualTo(QuestionPurpose.Explanation));
            Assert.That(Fallbacks(nonsense, nonsenseSituation), Is.All.Null);
            // A direct question outside the viewer's known state.
            var (generic, genericSituation) = Ask(Kritik, "критик, сколько будет два плюс два?", "krit.ranked");
            Assert.That(Fallbacks(generic, genericSituation), Is.All.Null);
            var (origin, originSituation) = Ask(Kritik, "критик, откуда ты?", "krit.ranked");
            Assert.That(Fallbacks(origin, originSituation), Is.All.Null);
        }

        [Test]
        public void RussianViewerWithoutRussianPhrasingIsSilentRatherThanSpeakingEnglish()
        {
            ViewerProfile profile = UnityEngine.JsonUtility.FromJson<ViewerProfile>(UnityEngine.JsonUtility.ToJson(ViewerProfileTests.Profile(Kritik)));
            profile.DailyLife = new[] { new DailyActivity { Id = "plain", Today = "played ranked games", Now = "watching", Kind = DayActivityKind.Gaming,
                Mood = ViewerMood.Stressed } };
            ChatParticipant viewer = profile.Participant();
            var intent = ViewerUtterancePlanTests.Make(3, ViewerUtterancePlanTests.Said("критик, что сегодня делал?", viewer), viewer, true);
            var situation = new ChatSituation("quality", 600, 3, ViewerLanguage.Russian, null, null);
            typeof(ChatSituation).GetProperty("Day").SetValue(situation, ViewerDailyLife.For(viewer, 0, "quality", RelationshipTier.Neutral));
            Assert.That(FallbackChat.Pick(intent, new AudienceRandom(1), null, situation), Is.Null);
            Assert.That(Prompt(intent, situation), Does.Contain("today: played ranked games"), "the prompt keeps the canonical fact");
        }

        [Test]
        public void PresenceQuestionIsAnsweredTruthfully()
        {
            var (intent, situation) = Ask(Kritik, "критик, ты тут?", "krit.ranked");
            Assert.That(ViewerUtterancePlanner.For(intent, situation).QuestionPurpose, Is.EqualTo(QuestionPurpose.Confirmation));
            Assert.That(Fallbacks(intent, situation).All(line => new[] { "я тут", "тут я", "да-да, тут" }.Contains(line)), Is.True);
        }

        [Test]
        public void NamedThanksFallsBackToAThanksReplyNotAPresenceNoise()
        {
            var (intent, situation) = Ask("viewer.mika", "мика, спасибо что заходишь", "mika.studies", tier: RelationshipTier.Loyal);
            List<string> lines = Fallbacks(intent, situation);
            Assert.That(lines.All(line => new[] { "да не за что", "пожалуйста)", "на здоровье" }.Contains(line)), Is.True, string.Join(" | ", lines.Distinct()));
        }

        [Test]
        public void EveryAuthoredRussianPhrasePublishesAsAnAnswerForItsOwnDay()
        {
            var failures = new List<string>();
            foreach (ViewerProfile profile in ViewerProfileTests.Catalog().Profiles)
            {
                if (profile.Language == ViewerLanguage.English) continue;
                foreach (DailyActivity activity in profile.DailyLife)
                {
                    Assert.That(activity.SayToday, Is.Not.Empty, activity.Id);
                    Assert.That(activity.SayNow, Is.Not.Empty, activity.Id);
                    string name = profile.SpokenNames[0];
                    foreach (string question in new[] { name + ", что сегодня делал?", name + ", чем занят?", name + ", как настроение?" })
                    {
                        var (intent, situation) = Ask(profile.Id, question, activity.Id);
                        foreach (string line in Fallbacks(intent, situation, 6).Distinct())
                        {
                            ChatValidation validation = ChatOutputValidator.Validate(line, intent, situation.RecentChat, situation);
                            if (line == null || !validation.Accepted) failures.Add($"{activity.Id} «{question}» -> «{line}» {validation.Reason}");
                        }
                    }
                }
            }
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        [TestCase("весь день катал ранкед на rtx 4090")]
        [TestCase("слил 500 рублей в ранкеде")]
        [TestCase("весь день катал, стрим уже 12 часов")]
        [TestCase("задонатил тебе и пошёл катать")]
        [TestCase("опять ты слил")]
        public void HardGameFactsStayProtectedInsideAnAnsweredReason(string text)
        {
            var (intent, situation) = Ask(Kritik, "Почему без настроения?", "krit.ranked", PreviousMood, RelationshipTier.Wary);
            Assert.That(ChatOutputValidator.Validate(text, intent, situation.RecentChat, situation).Accepted, Is.False, text);
            Assert.That(ChatOutputValidator.Validate(RankedReason, intent, situation.RecentChat, situation).Accepted, Is.True);
        }

        [Test]
        public void DirectorPublishesThePurposeAnswerWhenTheModelOutputIsRejected()
        {
            var (intent, situation) = Ask(Kritik, "Почему без настроения?", "krit.ranked", PreviousMood, RelationshipTier.Wary);
            var model = new ChatDirectorTests.FakeModel(_ => new LanguageModelResult(LanguageModelStatus.Ok, "As an AI language model I cannot", .1));
            var chat = new StreamChat();
            using var director = new ChatDirector(model, new ChatModelSettings(), chat, new ReactionLog(), () => 0);
            director.BeginBroadcast(new AudienceRandom(5));
            var roster = ReactionFoundationTests.Roster(3, intent.Viewer);
            director.Submit(intent);
            for (int i = 0; i < 4 && chat.Messages.Count == 0; i++) director.Update(intent.DueSeconds + i, roster, _ => situation, "quality");
            Assert.That(chat.Messages.Single().Text, Is.EqualTo(RankedReason));
            Assert.That(chat.Messages.Single().Source, Is.EqualTo(ReactionSource.Fallback));
        }

        [TestCase("мика, чем сейчас занята?", "viewer.mika", "mika.studies", QuestionPurpose.CurrentActivity)]
        [TestCase("мика, а чем занималась сегодня?", "viewer.mika", "mika.studies", QuestionPurpose.TodayActivity)]
        public void FreeWordOrderStillFindsTheDayQuestion(string question, string id, string day, QuestionPurpose expected)
        {
            var (intent, situation) = Ask(id, question, day);
            Assert.That(ViewerUtterancePlanner.For(intent, situation).QuestionPurpose, Is.EqualTo(expected));
        }

        [Test]
        public void QuestionAboutTheViewersOwnDoingGetsThatDayAsContext()
        {
            var (ranked, rankedSituation) = Ask(Kritik, "понятно. часто играешь в ранкед?", "krit.ranked", RankedReason, RelationshipTier.Wary);
            Assert.That(Prompt(ranked, rankedSituation), Does.Contain(RankedReason));
            var (shift, shiftSituation) = Ask("viewer.nightowl", "сова, а ты сегодня на смене?", "owl.day-off");
            Assert.That(Prompt(shift, shiftSituation), Does.Contain("дома сегодня, не на смене"));
            // A doing the day does not cover authorizes nothing.
            var (drawing, drawingSituation) = Ask(Kritik, "а ты рисуешь?", "krit.ranked", RankedReason);
            Assert.That(ViewerUtterancePlanner.For(drawing, drawingSituation).DirectAnswerFacts, Is.Empty);
        }

        [TestCase("viewer.kritik228", "You are male")]
        [TestCase("viewer.zinaivanovna", "You are female")]
        [TestCase("viewer.bytecat", null)]
        [TestCase("viewer.arcadekid", null)]
        public void GrammaticalGenderReachesRussianSpeakersOnly(string id, string expected)
        {
            var (intent, situation) = Ask(id, "как дела?", null, withDay: false);
            string prompt = Prompt(intent, situation);
            if (expected == null) Assert.That(prompt, Does.Not.Contain("You are male").And.Not.Contain("You are female"));
            else Assert.That(prompt, Does.Contain(expected));
        }

        [Test]
        public void BoredIsNotMissingSomeoneAndEmphasisIsMarkup()
        {
            var (intent, situation) = Ask(Kritik, "критик, а ты что сегодня делал?", "krit.school", tier: RelationshipTier.Wary);
            Assert.That(ChatOutputValidator.Validate("весь день на уроках скучал", intent, null, situation).Accepted, Is.True);
            Assert.That(ChatOutputValidator.Validate("скучал по тебе", intent, null, situation).Reason, Is.EqualTo("relationship claim above game state"));
            Assert.That(ChatOutputValidator.Validate("люблю игры типа *Зелёная Миля*", intent, null, situation).Reason, Is.EqualTo("markup or prompt leakage"));
        }
    }
}
