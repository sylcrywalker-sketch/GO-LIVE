using System;
using System.Linq;
using System.Reflection;
using GoLive.Desktop;
using GoLive.Viewers;
using NUnit.Framework;

namespace GoLive.Tests
{
    public sealed class ViewerLowAudienceContinuationTests
    {
        private static StreamEvent Said(AudienceRoster roster, string text, long sequence = 2) =>
            StreamEvent.StreamerSpeech(sequence, "continuation", 20,
                SpeechRelevance.Analyze(ReactionFoundationTests.Recognized(text, sequence), roster.NamesForMentions()));

        private static void Publish(ReactionSelector selector, AudienceRoster roster, ChatParticipant viewer,
            double at = 10, bool followUp = false, long id = 1)
        {
            var origin = ViewerUtterancePlanTests.Make(id, Said(roster, "как дела", id), viewer, epoch: roster.Epoch(viewer.ViewerId));
            typeof(ReactionIntent).GetProperty(nameof(ReactionIntent.Conversational)).SetValue(origin, true);
            typeof(ReactionIntent).GetProperty(nameof(ReactionIntent.FollowUp)).SetValue(origin, followUp);
            selector.ObservePublished(new StreamChat().Add("continuation", viewer.ViewerId, viewer.DisplayName,
                "устал после города бегать", at, id, ReactionSource.LanguageModel), origin);
        }

        [TestCase("устал наверное")]
        [TestCase("Жесть. Устал, наверное.")]
        [TestCase("Жесть. Устал, наверное?")]
        [TestCase("устал?")]
        [TestCase("тяжело было")]
        [TestCase("нормально там")]
        [TestCase("серьезно")]
        [TestCase("а ты")]
        [TestCase("и как")]
        [TestCase("что потом")]
        [TestCase("понравилось")]
        [TestCase("долго")]
        [TestCase("ну ты устала наверное")]
        public void ShortPersonalFollowUpGetsQuestionReliabilityWithOrWithoutPunctuation(string phrase)
        {
            for (int audience = 1; audience <= 3; audience++)
            {
                int replies = 0;
                for (ulong seed = 1; seed <= 200; seed++)
                {
                    var viewer = ChatDirectorTests.Viewer("viewer.a", "Alpha");
                    var roster = ReactionFoundationTests.Roster(audience, viewer);
                    var selector = new ReactionSelector(new ReactionTuning(), roster, new AudienceRandom(seed));
                    Publish(selector, roster, viewer);
                    selector.Rhythm.Refill(10, audience);
                    selector.Rhythm.RecordConversation(viewer.ViewerId, 10);
                    for (int i = 0; i < 10; i++) selector.Rhythm.Record("other", 10);
                    var result = selector.Select(Said(roster, phrase), 20, true, out _);
                    replies += result.Count(i => i.FollowUp && i.Viewer.ViewerId == viewer.ViewerId);
                }
                TestContext.WriteLine($"{phrase}; audience={audience}; same-viewer replies={replies}/200");
                Assert.That(replies, Is.InRange(180, 200), "Question-like continuations should normally get the existing .95 path.");
            }
        }

        [Test]
        public void AcknowledgementStaysAProbabilisticStatement()
        {
            int replies = 0;
            for (ulong seed = 1; seed <= 200; seed++)
            {
                var viewer = ChatDirectorTests.Viewer("viewer.a", "Alpha");
                var roster = ReactionFoundationTests.Roster(1, viewer);
                var selector = new ReactionSelector(new ReactionTuning(), roster, new AudienceRandom(seed));
                Publish(selector, roster, viewer);
                var e = Said(roster, "понятно");
                Assert.That(e.Speech.AsksForAnswer, Is.False);
                replies += selector.Select(e, 20, true, out _).Count(i => i.FollowUp);
            }
            Assert.That(replies, Is.InRange(80, 155), "Plain acknowledgements keep the statement chance, not the question boost.");
        }

        [TestCase("я нормально а ты как дела")]
        [TestCase("я отдохнул а ты как сам")]
        public void ExplicitPersonalQuestionStillContinuesAfterStreamerSharesOwnState(string phrase)
        {
            int replies = 0;
            for (ulong seed = 1; seed <= 30; seed++)
            {
                var viewer = ChatDirectorTests.Viewer("viewer.a", "Alpha");
                var roster = ReactionFoundationTests.Roster(1, viewer);
                var selector = new ReactionSelector(new ReactionTuning(), roster, new AudienceRandom(seed));
                Publish(selector, roster, viewer);
                var e = Said(roster, phrase);
                Assert.That(e.Speech.Is(SpeechAct.PersonalQuestion), Is.True);
                replies += selector.Select(e, 20, true, out _).Count(i => i.FollowUp && i.Viewer.ViewerId == viewer.ViewerId);
            }
            Assert.That(replies, Is.GreaterThanOrEqualTo(25), "An explicit question to the listener must retain the existing thread and its turn count.");
        }

        [TestCase("устал наверное")]
        [TestCase("тяжело было")]
        [TestCase("серьезно")]
        [TestCase("понравилось")]
        public void ContextualFormsDoNotBecomeGlobalQuestions(string phrase)
        {
            var roster = ReactionFoundationTests.Roster(1, ChatDirectorTests.Viewer("viewer.a", "Alpha"));
            var e = Said(roster, phrase);
            Assert.That(e.Speech.AsksForAnswer, Is.False);
            for (ulong seed = 1; seed <= 20; seed++)
            {
                var selector = new ReactionSelector(new ReactionTuning(), roster, new AudienceRandom(seed));
                Assert.That(selector.Select(e, 20, true, out _).Any(i => i.FollowUp || i.Conversational), Is.False);
            }
        }

        [TestCase("expired")]
        [TestCase("absent")]
        [TestCase("new visit")]
        [TestCase("limit")]
        [TestCase("cooldown")]
        public void ContextualQuestionCannotBypassThreadGuards(string guard)
        {
            for (ulong seed = 1; seed <= 30; seed++)
            {
                var viewer = ChatDirectorTests.Viewer("viewer.a", "Alpha");
                var roster = ReactionFoundationTests.Roster(1, viewer);
                var tuning = new ReactionTuning { ConversationMaximumTurns = 2 };
                var selector = new ReactionSelector(tuning, roster, new AudienceRandom(seed));
                Publish(selector, roster, viewer);
                double now = guard == "expired" ? 51 : 20;
                if (guard == "absent" || guard == "new visit") roster.Leave(viewer.ViewerId);
                if (guard == "new visit") roster.Join(viewer);
                if (guard == "limit") Publish(selector, roster, viewer, 15, true, 3);
                if (guard == "cooldown") selector.Rhythm.RecordConversation(viewer.ViewerId, 19);
                var result = selector.Select(Said(roster, "устал наверное"), now, true, out _);
                Assert.That(result.Any(i => i.FollowUp || i.Conversational), Is.False, guard);
            }
        }

        [Test]
        public void PendingAnswerCannotBeDuplicatedBySemanticQuestion()
        {
            int selected = 0;
            for (ulong seed = 1; seed <= 30; seed++)
            {
                var viewer = ChatDirectorTests.Viewer("viewer.a", "Alpha");
                var roster = ReactionFoundationTests.Roster(1, viewer);
                var selector = new ReactionSelector(new ReactionTuning(), roster, new AudienceRandom(seed));
                Publish(selector, roster, viewer);
                var first = selector.Select(Said(roster, "устал наверное"), 20, true, out _).FirstOrDefault(i => i.FollowUp);
                if (first == null) continue;
                selected++;
                Assert.That(selector.Select(Said(roster, "и как", 3), first.DueSeconds + 7, true, out _).Any(i => i.FollowUp), Is.False);
            }
            Assert.That(selected, Is.GreaterThan(20));
        }

        [TestCase("Так, секунду.")]
        [TestCase("я устал наверное")]
        [TestCase("он устал наверное")]
        [TestCase("она устала наверное")]
        [TestCase("тяжело было играть")]
        [TestCase("серьезно микрофон сломался")]
        [TestCase("долго копить деньги")]
        [TestCase("я думаю ты устал наверное но сейчас запущу игру")]
        public void FillerAndUnrelatedOrThirdPersonSpeechDoNotContinue(string phrase)
        {
            for (ulong seed = 1; seed <= 30; seed++)
            {
                var viewer = ChatDirectorTests.Viewer("viewer.a", "Alpha");
                var roster = ReactionFoundationTests.Roster(1, viewer);
                var selector = new ReactionSelector(new ReactionTuning(), roster, new AudienceRandom(seed));
                Publish(selector, roster, viewer);
                var result = selector.Select(Said(roster, phrase), 20, true, out _);
                Assert.That(result.Any(i => i.FollowUp), Is.False, phrase);
                if (phrase == "Так, секунду.") Assert.That(result, Is.Empty);
            }
        }

        [TestCase("Парни, какую игру запустить?")]
        [TestCase("Beta, как дела?")]
        public void GroupOrDifferentViewerAddressLeavesCurrentExchange(string phrase)
        {
            for (ulong seed = 1; seed <= 30; seed++)
            {
                var viewer = ChatDirectorTests.Viewer("viewer.a", "Alpha");
                var roster = ReactionFoundationTests.Roster(2, viewer, ChatDirectorTests.Viewer("viewer.b", "Beta"));
                var selector = new ReactionSelector(new ReactionTuning(), roster, new AudienceRandom(seed));
                Publish(selector, roster, viewer);
                Assert.That(selector.Select(Said(roster, phrase), 20, true, out _).Any(i => i.FollowUp), Is.False);
                Assert.That(selector.Thread.ViewerId, Is.Null);
            }
        }

        [TestCase("устал наверное")]
        [TestCase("и как")]
        [TestCase("долго")]
        public void FollowUpPlanningUsesTheSameQuestionSemantics(string phrase)
        {
            int inspected = 0;
            for (ulong seed = 1; seed <= 30; seed++)
            {
                var viewer = ChatDirectorTests.Viewer("viewer.a", "Alpha");
                var roster = ReactionFoundationTests.Roster(1, viewer);
                var selector = new ReactionSelector(new ReactionTuning(), roster, new AudienceRandom(seed));
                Publish(selector, roster, viewer);
                var followup = selector.Select(Said(roster, phrase), 20, true, out _).FirstOrDefault(i => i.FollowUp);
                if (followup == null) continue;
                var plan = ViewerUtterancePlanner.For(followup,
                    new ChatSituation("test", 20, 1, ViewerLanguage.Russian, Array.Empty<StreamChatMessage>(), null));
                Assert.That(plan.FollowUp && plan.Personal, Is.True);
                Assert.That(plan.Topic, Does.Contain("question"));
                Assert.That(plan.Intent, Is.EqualTo(UtteranceIntent.Answer).Or.EqualTo(UtteranceIntent.Tease));
                inspected++;
            }
            Assert.That(inspected, Is.GreaterThan(20));
        }
    }
}
