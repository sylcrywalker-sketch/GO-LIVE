using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using GoLive.Desktop;
using GoLive.Viewers;
using NUnit.Framework;

namespace GoLive.Tests
{
    public sealed class ViewerDailyConversationRegressionTests
    {
        private static void Flag(ReactionIntent intent, string property, bool value = true) =>
            typeof(ReactionIntent).GetProperty(property).SetValue(intent, value);

        private static StreamEvent Said(AudienceRoster roster, string text, long sequence = 1, double at = 10) =>
            StreamEvent.StreamerSpeech(sequence, "daily", at,
                SpeechRelevance.Analyze(ReactionFoundationTests.Recognized(text, sequence), roster.NamesForMentions()));

        private static void Publish(ReactionSelector selector, ChatParticipant viewer, AudienceRoster roster,
            double at, bool followUp = false, long id = 1)
        {
            var origin = ViewerUtterancePlanTests.Make(id, Said(roster, "как дела", id), viewer, epoch: roster.Epoch(viewer.ViewerId));
            Flag(origin, "Conversational"); Flag(origin, "FollowUp", followUp);
            var chat = new StreamChat();
            selector.ObservePublished(chat.Add("daily", viewer.ViewerId, viewer.DisplayName, "с работы, устал", at, id, ReactionSource.LanguageModel), origin);
        }

        [TestCase("Всем привет, как дела, парни?")]
        [TestCase("Ребят, что сегодня делали?")]
        [TestCase("Как вы, настроение норм?")]
        public void PersonalQuestionOutranksGreetingAndPluralAddressesGroup(string phrase)
        {
            var speech = SpeechRelevance.Analyze(ReactionFoundationTests.Recognized(phrase), Array.Empty<ViewerNameForms>());
            Assert.That(speech.PrimaryAct, Is.EqualTo(SpeechAct.PersonalQuestion));
            Assert.That(speech.PluralAddress, Is.True);
        }

        [Test]
        public void FillerRemainsSilentInsideActiveConversation()
        {
            var viewer = ChatDirectorTests.Viewer("viewer.a", "Alpha");
            for (ulong seed = 1; seed <= 20; seed++)
            {
                var roster = ReactionFoundationTests.Roster(1, viewer);
                var selector = new ReactionSelector(new ReactionTuning(), roster, new AudienceRandom(seed));
                Publish(selector, viewer, roster, 0);
                Assert.That(selector.Select(Said(roster, "Так, ну, секунду, ага"), 12, true, out _), Is.Empty);
            }
        }

        [Test]
        public void SmallAudiencePersonalQuestionCanAnswerWithEmptyBudget()
        {
            var viewer = ChatDirectorTests.Viewer("viewer.a", "Alpha"); int answers = 0;
            for (ulong seed = 1; seed <= 30; seed++)
            {
                var roster = ReactionFoundationTests.Roster(1, viewer);
                var selector = new ReactionSelector(new ReactionTuning(), roster, new AudienceRandom(seed));
                selector.Rhythm.Refill(0, 1);
                for (int i = 0; i < 5; i++) selector.Rhythm.Record("other", 0);
                answers += selector.Select(Said(roster, "Alpha, как дела?"), 10, true, out _).Count(i => i.Conversational);
            }
            Assert.That(answers, Is.GreaterThan(20));
        }

        [Test]
        public void EphemeralOneSeatLateStatementFollowUpKeepsConfiguredChanceWithEmptyBudget()
        {
            int replies = 0;
            for (ulong seed = 1; seed <= 100; seed++)
            {
                var random = new AudienceRandom(seed);
                var roster = new AudienceRoster(EphemeralViewers.Create);
                roster.SetAudienceSize(1);
                var viewer = roster.AnonymousChatter(random, _ => true, 0);
                Assert.That(viewer.IsPermanent, Is.False);
                var selector = new ReactionSelector(new ReactionTuning(), roster, random);
                Publish(selector, viewer, roster, 10);
                selector.Rhythm.Refill(10, 1);
                selector.Rhythm.RecordConversation(viewer.ViewerId, 10);
                for (int i = 0; i < 5; i++) selector.Rhythm.Record("other", 10);
                Assert.That(selector.Select(Said(roster, "Так, ну, секунду, ага", 2, 27), 27, true, out _), Is.Empty);
                Assert.That(selector.Thread.Turns, Is.EqualTo(1));
                Assert.That(roster.IsWatching(viewer.ViewerId), Is.True);
                Assert.That(roster.Epoch(viewer.ViewerId), Is.EqualTo(selector.Thread.Epoch));
                Assert.That(selector.Rhythm.CanSpend(false), Is.False);
                var reactions = selector.Select(Said(roster, "Жесть. Устал, наверное.", 3, 43), 43, true, out _);
                replies += reactions.Count(i => i.FollowUp && i.Viewer.ViewerId == viewer.ViewerId);
            }
            TestContext.WriteLine("Exact late statement follow-ups across 100 seeds: " + replies);
            Assert.That(replies, Is.GreaterThan(40), "The configured statement continuation chance is 0.6 inside the 40-second window.");
        }

        [Test]
        public void GlAuthoredConfigurationRetainsConversationWindowTurnsAndGap()
        {
            var config = UnityEditor.AssetDatabase.LoadAssetAtPath<ViewerCoreConfig>("Assets/Game/Config/Viewers/ViewerCore.asset");
            Assert.That(config, Is.Not.Null);
            TestContext.WriteLine("GL conversation values: window=" + config.Reactions.ConversationWindowSeconds +
                ", maximumTurns=" + config.Reactions.ConversationMaximumTurns + ", gap=" + config.Reactions.ConversationGapSeconds);
            Assert.That(config.Reactions.ConversationWindowSeconds, Is.EqualTo(40));
            Assert.That(config.Reactions.ConversationMaximumTurns, Is.EqualTo(5));
            Assert.That(config.Reactions.ConversationGapSeconds, Is.EqualTo(6));
        }
        [TestCase("Жесть. Устал, наверное.", true)]
        [TestCase("А ты во что играл?", true)]
        [TestCase("Я опять умер в игре!", false)]
        [TestCase("Какую игру запустить?", false)]
        [TestCase("Всем привет, как дела, парни?", false)]
        public void FollowUpStaysWithViewerOnlyForRelatedSpeech(string phrase, bool expected)
        {
            var viewer = ChatDirectorTests.Viewer("viewer.a", "Alpha"); int replies = 0;
            for (ulong seed = 1; seed <= 30; seed++)
            {
                var roster = ReactionFoundationTests.Roster(1, viewer);
                var selector = new ReactionSelector(new ReactionTuning(), roster, new AudienceRandom(seed));
                Publish(selector, viewer, roster, 0);
                var reactions = selector.Select(Said(roster, phrase), 12, true, out _);
                if (expected) replies += reactions.Count(i => i.FollowUp && i.Viewer.ViewerId == viewer.ViewerId);
                else Assert.That(reactions.Any(i => i.FollowUp), Is.False, phrase);
            }
            if (expected) Assert.That(replies, Is.GreaterThan(10));
        }

        [Test]
        public void ExpiredOrChangedVisitCannotContinueConversation()
        {
            var viewer = ChatDirectorTests.Viewer("viewer.a", "Alpha");
            var roster = ReactionFoundationTests.Roster(1, viewer);
            var selector = new ReactionSelector(new ReactionTuning(), roster, new AudienceRandom(7));
            Publish(selector, viewer, roster, 0);
            Assert.That(selector.Select(Said(roster, "Устал?"), 41, true, out _).Any(i => i.FollowUp), Is.False);
            Publish(selector, viewer, roster, 42, id: 2);
            roster.Leave(viewer.ViewerId); roster.Join(viewer);
            Assert.That(selector.Select(Said(roster, "Устал?", 3), 55, true, out _).Any(i => i.FollowUp), Is.False);
        }

        [TestCase("Устал?")]
        [TestCase("Alpha, как дела?")]
        public void MaximumIncludesInitialAnswerAndCannotImmediatelyReopenSameViewer(string phrase)
        {
            var viewer = ChatDirectorTests.Viewer("viewer.a", "Alpha");
            for (ulong seed = 1; seed <= 30; seed++)
            {
                var roster = ReactionFoundationTests.Roster(1, viewer);
                var selector = new ReactionSelector(new ReactionTuning { ConversationMaximumTurns = 3 }, roster, new AudienceRandom(seed));
                Publish(selector, viewer, roster, 0);
                Publish(selector, viewer, roster, 10, true, 2);
                Publish(selector, viewer, roster, 20, true, 3);
                Assert.That(selector.Thread.Turns, Is.EqualTo(3));
                Assert.That(selector.Select(Said(roster, phrase, 4), 30, true, out _).Any(i => i.Conversational || i.FollowUp), Is.False);
            }
        }

        [Test]
        public void OrdinarySpeechStillUsesBudgetAndOrdinaryViewerGap()
        {
            var viewer = ChatDirectorTests.Viewer("viewer.a", "Alpha");
            for (ulong seed = 1; seed <= 30; seed++)
            {
                var roster = ReactionFoundationTests.Roster(1, viewer);
                var selector = new ReactionSelector(new ReactionTuning(), roster, new AudienceRandom(seed));
                selector.Rhythm.Refill(0, 1); selector.Rhythm.Record(viewer.ViewerId, 0);
                Assert.That(selector.Select(Said(roster, "Я опять умер в игре!"), 12, true, out _), Is.Empty);
            }
        }

        [Test]
        public void PendingFinalAnswerReservesLastConversationTurn()
        {
            var viewer = ChatDirectorTests.Viewer("viewer.a", "Alpha"); int pending = 0;
            for (ulong seed = 1; seed <= 30; seed++)
            {
                var roster = ReactionFoundationTests.Roster(1, viewer);
                var tuning = new ReactionTuning { ConversationMaximumTurns = 3 };
                var selector = new ReactionSelector(tuning, roster, new AudienceRandom(seed));
                Publish(selector, viewer, roster, 0);
                Publish(selector, viewer, roster, 10, true, 2);
                var first = selector.Select(Said(roster, "Устал?", 3), 20, true, out _).FirstOrDefault(i => i.FollowUp);
                if (first == null) continue;
                pending++;
                double next = first.DueSeconds + tuning.ConversationGapSeconds;
                Assert.That(next, Is.LessThan(first.ExpiresSeconds));
                Assert.That(selector.Select(Said(roster, "Alpha, как дела?", 4), next, true, out _)
                    .Any(i => i.Conversational || i.FollowUp), Is.False, "A pending final answer already owns the last turn.");
            }
            Assert.That(pending, Is.GreaterThan(20));
        }

        [Test]
        public void FallbackSelectedContinuationPublishesFinalTurnWithoutResettingThread()
        {
            var viewer = ChatDirectorTests.Viewer("viewer.a", "Alpha");
            var roster = ReactionFoundationTests.Roster(1, viewer);
            var selector = new ReactionSelector(new ReactionTuning { ConversationMaximumTurns = 3 }, roster, new AudienceRandom(44));
            Publish(selector, viewer, roster, 0);
            Publish(selector, viewer, roster, 10, true, 2);
            var answer = selector.Select(Said(roster, "Устал?", 3), 20, true, out _).Single(i => i.Viewer.ViewerId == viewer.ViewerId);
            Assert.That(answer.Direct, Is.False, "Seed 44 misses the direct continuation roll and takes the conversational fallback.");
            var chat = new StreamChat();
            selector.ObservePublished(chat.Add("daily", viewer.ViewerId, viewer.DisplayName, "да, отдыхаю", answer.DueSeconds, answer.Id, ReactionSource.Fallback), answer);
            Assert.That(selector.Thread.Turns, Is.EqualTo(3), "Publishing the actual fallback origin must increment, not reset, the thread.");
            Assert.That(answer.FollowUp, Is.True);
            Assert.That(selector.Select(Said(roster, "Устал?", 4), answer.DueSeconds + 6, true, out _)
                .Any(i => i.Conversational || i.FollowUp), Is.False);
        }

        [TestCase("Alpha, как дела?")]
        [TestCase("Как дела?")]
        public void PendingInitialAnswerReservesOneAnswerConversation(string phrase)
        {
            var viewer = ChatDirectorTests.Viewer("viewer.a", "Alpha"); int openings = 0;
            for (ulong seed = 1; seed <= 30; seed++)
            {
                var roster = ReactionFoundationTests.Roster(1, viewer);
                var tuning = new ReactionTuning { ConversationMaximumTurns = 1 };
                var selector = new ReactionSelector(tuning, roster, new AudienceRandom(seed));
                var first = selector.Select(Said(roster, phrase), 20, true, out _).FirstOrDefault(i => i.Conversational);
                if (first == null) continue;
                openings++;
                double next = first.DueSeconds + tuning.ConversationGapSeconds;
                Assert.That(next, Is.LessThan(first.ExpiresSeconds));
                Assert.That(selector.Select(Said(roster, phrase, 2), next, true, out _).Any(i => i.Conversational), Is.False,
                    "A delayed initial answer already owns the only allowed answer.");
                var chat = new StreamChat();
                selector.ObservePublished(chat.Add("daily", viewer.ViewerId, viewer.DisplayName, "норм", next, first.Id, ReactionSource.LanguageModel), first);
                Assert.That(selector.Thread.Turns, Is.EqualTo(1));
                Assert.That(selector.Select(Said(roster, "Устал?", 3), next + 6, true, out _).Any(i => i.Conversational), Is.False);
            }
            Assert.That(openings, Is.GreaterThan(20));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CancelledOrExpiredFinalAnswerReleasesConversationTurn(bool expire)
        {
            var viewer = ChatDirectorTests.Viewer("viewer.a", "Alpha"); int resumed = 0;
            for (ulong seed = 1; seed <= 30; seed++)
            {
                var tuning = new ReactionTuning { ConversationMaximumTurns = 3 };
                using var live = new LiveDesktop(seed, reactions: tuning);
                var core = live.State.Viewers;
                core.Roster.SetAudienceSize(1); core.Roster.Join(viewer);
                Publish(core.Selector, viewer, core.Roster, 0);
                Publish(core.Selector, viewer, core.Roster, 10, true, 2);
                var first = core.Selector.Select(Said(core.Roster, "Устал?", 3), 20, true, out _).FirstOrDefault(i => i.FollowUp);
                if (first == null) continue;
                core.Director.Submit(first);
                double next = first.DueSeconds + tuning.ConversationGapSeconds;
                if (expire)
                {
                    next = first.ExpiresSeconds + 1;
                    core.Director.Update(next, core.Roster, core.SituationFor, live.State.Stream.BroadcastId);
                }
                else core.Director.CancelAll("cancelled before publication");
                resumed += core.Selector.Select(Said(core.Roster, "Устал?", 4), next, true, out _).Count(i => i.FollowUp);
            }
            Assert.That(resumed, Is.GreaterThan(15), "An unpublished final turn must not stay reserved.");
        }

        [Test]
        public void QueueRejectedFinalAnswerReleasesConversationTurn()
        {
            var viewer = ChatDirectorTests.Viewer("viewer.a", "Alpha"); int resumed = 0;
            for (ulong seed = 1; seed <= 30; seed++)
            {
                using var live = new LiveDesktop(seed, reactions: new ReactionTuning { ConversationMaximumTurns = 3 });
                var core = live.State.Viewers;
                core.Roster.SetAudienceSize(1); core.Roster.Join(viewer);
                core.Director.CancelAll("prepare empty queue before reserving a turn");
                int droppedBefore = core.Director.Stats.DroppedQueueFull;
                Assert.That(core.Director.QueueDepth, Is.Zero);
                Publish(core.Selector, viewer, core.Roster, 0);
                Publish(core.Selector, viewer, core.Roster, 10, true, 2);
                var first = core.Selector.Select(Said(core.Roster, "Устал?", 3), 20, true, out _).FirstOrDefault(i => i.FollowUp);
                if (first == null) continue;
                for (int i = 0; i < 6; i++) core.Director.Submit(ViewerUtterancePlanTests.Make(100 + i, first.Event, viewer, true, first.PresenceEpoch));
                Assert.That(core.Director.QueueDepth, Is.EqualTo(6));
                Assert.That(core.Director.Stats.DroppedQueueFull, Is.EqualTo(droppedBefore), "The fixture fills the queue without rejecting a blocker.");
                core.Director.Submit(first);
                Assert.That(core.Director.QueueDepth, Is.EqualTo(6));
                Assert.That(core.Director.Stats.DroppedQueueFull, Is.EqualTo(droppedBefore + 1));
                resumed += core.Selector.Select(Said(core.Roster, "Устал?", 4), first.DueSeconds + 6, true, out _).Count(i => i.FollowUp);
            }
            Assert.That(resumed, Is.GreaterThan(15), "Queue rejection is terminal and must release the unsubmitted final turn.");
        }

        private static ViewerProfile DailyProfile(string story = "worked a long day")
        {
            var profile = ViewerCommunityTests.Profile();
            profile.DailyLife = new[] { new DailyActivity { Id = "test.work", Today = story, Now = "resting", Kind = DayActivityKind.Work, Mood = ViewerMood.Tired, Energy = ViewerEnergy.Low } };
            return profile;
        }

        [Test]
        public void PermanentDayIsStableAcrossBroadcastsAndAnonymousDayWithinBroadcast()
        {
            var profile = ViewerProfileTests.Profile("viewer.nightowl"); var viewer = profile.Participant();
            var first = ViewerDailyLife.For(viewer, 1500, "a", RelationshipTier.Neutral);
            var same = ViewerDailyLife.For(viewer, 2800, "b", RelationshipTier.Neutral);
            Assert.That(same.Describe(), Is.EqualTo(first.Describe()));
            Assert.That(Enumerable.Range(2, 20).Select(d => ViewerDailyLife.For(viewer, d * 1440, "a", RelationshipTier.Neutral).Activity.Id).Distinct().Count(), Is.GreaterThan(1));
            Assert.That(profile.DailyLife.Any(a => ReferenceEquals(a, first.Activity)), Is.True);
            var anonymous = EphemeralViewers.Create(new AudienceRandom(2), 1);
            Assert.That(ViewerDailyLife.For(anonymous, 100, "x", RelationshipTier.Neutral).Describe(),
                Is.EqualTo(ViewerDailyLife.For(anonymous, 9999, "x", RelationshipTier.Neutral).Describe()));
            Assert.That(ViewerDailyLife.For(viewer, 1500, "a", RelationshipTier.Neutral).Activity.Id, Is.EqualTo(first.Activity.Id));
        }

        [TestCase("Tester0, как дела?", true)]
        [TestCase("Я опять умер в игре!", false)]
        public void ActualCorePlannerIncludesSelectedViewerDayOnlyForPersonalSpeech(string phrase, bool personal)
        {
            var profile = DailyProfile(); using var live = new LiveDesktop(123, profiles: new[] { profile });
            var core = live.State.Viewers; var viewer = profile.Participant(); core.Roster.SetAudienceSize(1); core.Roster.Join(viewer);
            var intent = ViewerUtterancePlanTests.Make(10, Said(core.Roster, phrase), viewer, epoch: core.Roster.Epoch(viewer.ViewerId));
            var context = core.SituationFor(intent); var plan = ViewerUtterancePlanner.For(intent, context);
            Assert.That(context.Day != null, Is.EqualTo(personal), "Core supplies daily context only when socially relevant.");
            Assert.That(plan.Personal, Is.EqualTo(personal));
            Assert.That(plan.AllowedFacts.Any(f => f.Source == FactSource.ViewerDay), Is.EqualTo(personal));
            Assert.That(ChatContextBuilder.Build(intent, context, 64).User.Contains("YOUR DAY"), Is.EqualTo(personal));
            if (!personal) Assert.That(ChatOutputValidator.Validate("играл сегодня", intent, null, context).Accepted, Is.True, "Unrelated response must not be constrained by omitted day context.");
        }

        [TestCase("у тебя rtx 4090")]
        [TestCase("стрим уже 12 часов")]
        [TestCase("ты опять слил")]
        [TestCase("задонатил тебе")]
        [TestCase("ты купил rtx 4090")]
        [TestCase("ты обещал rtx 4090")]
        [TestCase("видел вчера как ты выиграл")]
        public void DailyStoryDoesNotAuthorizeHardStreamerClaims(string output)
        {
            var profile = DailyProfile("worked 12 hours with rtx 4090, again");
            using var live = new LiveDesktop(123, profiles: new[] { profile });
            var core = live.State.Viewers; var viewer = profile.Participant(); core.Roster.SetAudienceSize(1); core.Roster.Join(viewer);
            var intent = ViewerUtterancePlanTests.Make(10, Said(core.Roster, "как дела?"), viewer, epoch: core.Roster.Epoch(viewer.ViewerId));
            var context = core.SituationFor(intent);
            Assert.That(ViewerUtterancePlanner.For(intent, context).AllowedFacts.Any(f => f.Source == FactSource.ViewerDay), Is.True);
            Assert.That(ChatOutputValidator.Validate(output, intent, null, context).Accepted, Is.False, output);
        }

        [Test]
        public void OwnDailyAnswerCannotLaunderHardwareIntoFollowUpGrounding()
        {
            var profile = DailyProfile(); using var live = new LiveDesktop(123, profiles: new[] { profile });
            var core = live.State.Viewers; var viewer = profile.Participant(); core.Roster.SetAudienceSize(1); core.Roster.Join(viewer);
            core.Chat.Add(live.State.Stream.BroadcastId, viewer.ViewerId, viewer.DisplayName, "работал с rtx 4090", core.Events.Now, 1, ReactionSource.LanguageModel);
            var intent = ViewerUtterancePlanTests.Make(2, Said(core.Roster, "Устал?", 2), viewer, epoch: core.Roster.Epoch(viewer.ViewerId));
            Flag(intent, "FollowUp");
            var context = core.SituationFor(intent);
            Assert.That(ViewerUtterancePlanner.For(intent, context).AllowedFacts.Any(f => f.Source == FactSource.OwnLine), Is.True);
            Assert.That(ChatOutputValidator.Validate("у тебя rtx 4090", intent, null, context).Accepted, Is.False);
        }

        [Test]
        public void NegatedActivityDoesNotContradictWorkDay()
        {
            var profile = DailyProfile(); using var live = new LiveDesktop(123, profiles: new[] { profile });
            var core = live.State.Viewers; var viewer = profile.Participant(); core.Roster.SetAudienceSize(1); core.Roster.Join(viewer);
            var intent = ViewerUtterancePlanTests.Make(10, Said(core.Roster, "как дела?"), viewer, epoch: core.Roster.Epoch(viewer.ViewerId));
            var context = core.SituationFor(intent);
            Assert.That(ChatOutputValidator.Validate("не играл, работал", intent, null, context).Accepted, Is.True);
            Assert.That(ChatOutputValidator.Validate("играл весь день", intent, null, context).Accepted, Is.False);
        }

        [TestCase(ViewerLanguage.English, "slept too much", false)]
        [TestCase(ViewerLanguage.Russian, "проспал весь день", false)]
        [TestCase(ViewerLanguage.English, "played games all day", false)]
        [TestCase(ViewerLanguage.Russian, "играл весь день", false)]
        [TestCase(ViewerLanguage.English, "worked all day", true)]
        [TestCase(ViewerLanguage.Russian, "работал весь день", true)]
        [TestCase(ViewerLanguage.English, "didn't sleep, worked", true)]
        [TestCase(ViewerLanguage.English, "did not sleep, worked", true)]
        [TestCase(ViewerLanguage.Russian, "не спал, работал", true)]
        [TestCase(ViewerLanguage.English, "you slept too much", true)]
        [TestCase(ViewerLanguage.Russian, "ты проспал весь день", true)]
        public void BilingualWorkDayRejectsContradictionsAndKeepsControls(ViewerLanguage language, string text, bool accepted)
        {
            var profile = DailyProfile("had a stressful day at work"); profile.Language = language;
            using var live = new LiveDesktop(123, profiles: new[] { profile });
            var core = live.State.Viewers; var viewer = profile.Participant(); core.Roster.SetAudienceSize(1); core.Roster.Join(viewer);
            var intent = ViewerUtterancePlanTests.Make(10, Said(core.Roster, "как дела?"), viewer, epoch: core.Roster.Epoch(viewer.ViewerId));
            var result = ChatOutputValidator.Validate(text, intent, null, core.SituationFor(intent));
            Assert.That(result.Accepted, Is.EqualTo(accepted), text + " => " + result.Reason);
            if (!accepted) Assert.That(result.Reason, Is.EqualTo("contradicts your day"));
        }

        [TestCase("haven't slept, worked", true)]
        [TestCase("haven’t slept, worked", true)]
        [TestCase("wasn't gaming, worked", true)]
        [TestCase("wasn’t gaming, worked", true)]
        [TestCase("have not slept, worked", true)]
        [TestCase("was not gaming, worked", true)]
        [TestCase("have slept, worked", false)]
        [TestCase("was gaming, worked", false)]
        public void ContractedEnglishNegationOfMatchedDayClaimKeepsControls(string text, bool accepted)
        {
            BilingualWorkDayRejectsContradictionsAndKeepsControls(ViewerLanguage.English, text, accepted);
        }

        [TestCase(ViewerLanguage.English, "slept too much")]
        [TestCase(ViewerLanguage.Russian, "проспал весь день")]
        public void BilingualSleepStoryAllowsOwnSleepClaim(ViewerLanguage language, string text)
        {
            var profile = DailyProfile("slept after a night shift"); profile.Language = language;
            profile.DailyLife[0].Kind = DayActivityKind.Sleep;
            using var live = new LiveDesktop(123, profiles: new[] { profile });
            var core = live.State.Viewers; var viewer = profile.Participant(); core.Roster.SetAudienceSize(1); core.Roster.Join(viewer);
            var intent = ViewerUtterancePlanTests.Make(10, Said(core.Roster, "как дела?"), viewer, epoch: core.Roster.Epoch(viewer.ViewerId));
            Assert.That(ChatOutputValidator.Validate(text, intent, null, core.SituationFor(intent)).Accepted, Is.True);
        }

        [TestCase("Tester0, привет, как дела?")]
        [TestCase("Привет, парни, как дела?")]
        public void PersonalFallbackAnswersStateBeforeGreetingOrPresence(string phrase)
        {
            var viewer = DailyProfile().Participant(); var roster = ReactionFoundationTests.Roster(1, viewer);
            var intent = ViewerUtterancePlanTests.Make(10, Said(roster, phrase), viewer);
            for (ulong seed = 1; seed <= 10; seed++)
            {
                string text = FallbackChat.Pick(intent, new AudienceRandom(seed), null);
                Assert.That(text, Is.Not.Null.And.Not.EqualTo("я тут").And.Not.EqualTo("а?").And.Not.EqualTo("тут я").And.Not.EqualTo("чего").And.Not.EqualTo("да-да, тут"));
                Assert.That(text, Does.Contain("норм").Or.Contain("ок").Or.Contain("живой").Or.Contain("отдыхаю"));
            }
        }

        [Test]
        public void WarmupUsesWorkerAndRunsOncePerBroadcast()
        {
            var model = new ChatDirectorTests.FakeModel(_ => new LanguageModelResult(LanguageModelStatus.Ok, "норм", .1)) { Manual = true };
            var chat = new StreamChat(); using var director = new ChatDirector(model, new ChatModelSettings(), chat, new ReactionLog());
            director.BeginBroadcast(new AudienceRandom(1)); director.Warmup(); director.Warmup();
            var viewer = ChatDirectorTests.Viewer("viewer.a", "Alpha"); var roster = ReactionFoundationTests.Roster(1, viewer);
            var intent = ViewerUtterancePlanTests.Make(1, Said(roster, "Alpha, как дела?"), viewer, epoch: roster.Epoch(viewer.ViewerId));
            director.Submit(intent); director.Update(0, roster, () => new ChatSituation("x", 0, 1, ViewerLanguage.Russian, chat.Messages, null), "a");
            Assert.That(model.Requests, Has.Count.EqualTo(1), "Pending warmup owns the only worker.");
            model.Complete(0, new LanguageModelResult(LanguageModelStatus.Ok, "ok", .2));
            director.Update(1, roster, () => new ChatSituation("x", 1, 1, ViewerLanguage.Russian, chat.Messages, null), "a");
            director.Warmup(); Assert.That(model.Requests, Has.Count.EqualTo(2), "Completed warmup is not repeated within a broadcast.");
            Assert.That(chat.Messages, Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void BroadcastEndOrLoadCancelsWarmup(bool load)
        {
            var model = new ChatDirectorTests.FakeModel(_ => new LanguageModelResult(LanguageModelStatus.Ok, "ok", .1)) { Manual = true };
            using var live = new LiveDesktop(123, languageModel: model);
            Assert.That(model.Requests, Has.Count.EqualTo(1));
            if (load) live.State.Restore(live.State.Capture(), Array.Empty<DesktopDrive>());
            else live.State.Viewers.Director.CancelAll("broadcast ended");
            Assert.That(model.Cancelled(0), Is.True);
            Assert.That(live.State.Viewers.Chat.Messages, Is.Empty);
        }

        [Test]
        public void CancellationResistantWarmupRetainsWorkerAcrossBroadcastRestart()
        {
            var model = new DelayedCancellationModel(); var chat = new StreamChat();
            using var director = new ChatDirector(model, new ChatModelSettings(), chat, new ReactionLog());
            director.BeginBroadcast(new AudienceRandom(1)); director.Warmup();
            director.BeginBroadcast(new AudienceRandom(2)); director.Warmup();
            var viewer = ChatDirectorTests.Viewer("viewer.a", "Alpha"); var roster = ReactionFoundationTests.Roster(1, viewer);
            director.Submit(ViewerUtterancePlanTests.Make(1, Said(roster, "Alpha, как дела?"), viewer, epoch: roster.Epoch(viewer.ViewerId)));
            ChatSituation Context() => new("x", 0, 1, ViewerLanguage.Russian, chat.Messages, null);
            director.Update(0, roster, Context, "b");
            Assert.That(model.Tokens[0].IsCancellationRequested, Is.True);
            Assert.That(model.Pending, Has.Count.EqualTo(1), "Cancellation does not itself free a busy adapter.");
            model.Pending[0].SetResult(new LanguageModelResult(LanguageModelStatus.Ok, "old warmup", 99));
            director.Update(1, roster, Context, "b");
            Assert.That(model.Pending, Has.Count.EqualTo(2));
            Assert.That(double.IsNaN(director.Stats.WarmupSeconds), Is.True, "An old broadcast cannot overwrite new warmup measurements.");
            model.Pending[1].SetResult(new LanguageModelResult(LanguageModelStatus.Ok, "ok", .2));
            director.Update(2, roster, Context, "b");
            Assert.That(model.Pending, Has.Count.EqualTo(3), "Real generation starts after this broadcast's warmup.");
            Assert.That(director.Stats.WarmupSeconds, Is.EqualTo(.2));
            Assert.That(chat.Messages, Is.Empty, "Neither warmup can publish a chat message.");
        }

        [TestCase("restart")]
        [TestCase("stale")]
        [TestCase("departure")]
        public void CancellationResistantOrdinaryRequestRetainsWorkerUntilCompletion(string terminal)
        {
            var model = new DelayedCancellationModel(); var chat = new StreamChat();
            using var director = new ChatDirector(model, new ChatModelSettings(), chat, new ReactionLog());
            director.BeginBroadcast(new AudienceRandom(1)); director.Warmup();
            model.Pending[0].SetResult(new LanguageModelResult(LanguageModelStatus.Ok, "ok", .2));
            var viewer = ChatDirectorTests.Viewer("viewer.a", "Alpha"); var roster = ReactionFoundationTests.Roster(1, viewer);
            ChatSituation Context() => new("x", 0, 1, ViewerLanguage.Russian, chat.Messages, null);
            director.Update(0, roster, Context, "a");
            var old = ViewerUtterancePlanTests.Make(1, Said(roster, "Alpha, как дела?"), viewer, epoch: roster.Epoch(viewer.ViewerId));
            director.Submit(old); director.Update(1, roster, Context, "a");
            Assert.That(model.Pending, Has.Count.EqualTo(2));
            double now = 2;
            if (terminal == "restart") { director.BeginBroadcast(new AudienceRandom(2)); director.Warmup(); }
            else if (terminal == "stale") { now = old.ExpiresSeconds + 1; director.Update(now, roster, Context, "a"); }
            else { roster.Leave(viewer.ViewerId); director.Update(now, roster, Context, "a"); roster.Join(viewer); }
            Assert.That(model.Tokens[1].IsCancellationRequested, Is.True);
            var next = (ReactionIntent)typeof(ReactionIntent).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single(c => c.GetParameters().Length == 8).Invoke(new object[] { 2L, Said(roster, "Alpha, как дела?", 2), viewer, true, 0, 40d, 60d, roster.Epoch(viewer.ViewerId) });
            director.Submit(next); director.Update(now, roster, Context, "b");
            Assert.That(model.Pending, Has.Count.EqualTo(2), "Cancelled ordinary work still occupies the adapter, including after " + terminal);
            model.Pending[1].SetResult(new LanguageModelResult(LanguageModelStatus.Ok, "old result", 99));
            director.Update(now + 1, roster, Context, "b");
            Assert.That(model.Pending, Has.Count.EqualTo(3));
            Assert.That(double.IsNaN(director.Stats.FirstSeconds), Is.True, "A retired result cannot become this broadcast's first answer.");
            Assert.That(chat.Messages, Is.Empty);
        }

        private sealed class DelayedCancellationModel : IViewerLanguageModel
        {
            public List<TaskCompletionSource<LanguageModelResult>> Pending { get; } = new();
            public List<CancellationToken> Tokens { get; } = new();
            public Task<LanguageModelResult> GenerateAsync(ViewerChatRequest request, CancellationToken cancellation)
            {
                var task = new TaskCompletionSource<LanguageModelResult>();
                Pending.Add(task); Tokens.Add(cancellation);
                return task.Task;
            }
            public void Dispose() { }
        }
    }
}
