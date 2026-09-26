using System;
using System.Linq;
using GoLive.Desktop;
using GoLive.Viewers;
using NUnit.Framework;

namespace GoLive.Tests
{
    public sealed class ViewerConversationTargetTests
    {
        private static ChatParticipant Kritik() => new("viewer.kritik228", "kritik228", true,
            new ReactionTraits(.6f, 1, 1, StreamTopic.Life), new[] { "критик", "kritik" });
        private static ChatParticipant Zina() => new("viewer.zinaivanovna", "ZinaIvanovna", true,
            new ReactionTraits(.8f, 1, 1, StreamTopic.Life), new[] { "зина", "зина ивановна" });
        private static ChatParticipant Other() => ChatDirectorTests.Viewer("viewer.other", "Other");
        private static StreamEvent Said(AudienceRoster roster, string text, double at = 20, long sequence = 2) =>
            StreamEvent.StreamerSpeech(sequence, "routing", at,
                SpeechRelevance.Analyze(ReactionFoundationTests.Recognized(text, sequence), roster.NamesForMentions()));
        private static void Publish(ReactionSelector selector, AudienceRoster roster, ChatParticipant viewer,
            string line = "без настроения но здесь сижу", double at = 10, bool followUp = false, long id = 1)
        {
            var origin = ViewerUtterancePlanTests.Make(id, Said(roster, "как дела", at, id), viewer, epoch: roster.Epoch(viewer.ViewerId));
            typeof(ReactionIntent).GetProperty("Conversational").SetValue(origin, true);
            typeof(ReactionIntent).GetProperty("FollowUp").SetValue(origin, followUp);
            selector.ObservePublished(new StreamChat().Add("routing", viewer.ViewerId, viewer.DisplayName,
                line, at, id, ReactionSource.LanguageModel), origin);
        }

        [TestCase("критик, почему без настроения?")]
        [TestCase("О чем ты, критик?")]
        [TestCase("Что? Девочка с девятого этажа? О чем ты критика?")]
        public void ExplicitTargetCannotBeStolenEvenIfItsRollFails(string phrase)
        {
            int silent = 0, answered = 0;
            for (ulong seed = 1; seed <= 200; seed++)
            {
                var kritik = Kritik(); var roster = ReactionFoundationTests.Roster(3, kritik, Zina(), Other());
                var selector = new ReactionSelector(new ReactionTuning(), roster, new AudienceRandom(seed));
                var speech = Said(roster, phrase);
                Assert.That(speech.Speech.MentionedViewerIds, Is.EquivalentTo(new[] { kritik.ViewerId }), "Retain existing fuzzy name recognition.");
                var intents = selector.Select(speech, 20, true, out _);
                Assert.That(intents.All(i => i.Viewer.ViewerId == kritik.ViewerId), Is.True, "No primary audience fallback after a direct roll.");
                Assert.That(intents.Count, Is.LessThanOrEqualTo(1));
                if (intents.Count == 0) silent++; else answered++;
            }
            Assert.That(silent, Is.GreaterThan(0)); Assert.That(answered, Is.GreaterThan(150));
        }

        [TestCase("Почему без настроения?")]
        [TestCase("критик, почему?")]
        [TestCase("почему?")]
        [TestCase("О чём речь? Какое плохое?")]
        [TestCase("что значит ну да ладно")]
        [TestCase("Жесть. Устал, наверное.")]
        public void ActiveExchangeOwnsTheOnlyAnswerEvenAfterFailedRoll(string phrase)
        {
            int responses = 0;
            for (ulong seed = 1; seed <= 150; seed++)
            {
                var kritik = Kritik(); var roster = ReactionFoundationTests.Roster(3, kritik, Zina(), Other());
                var selector = new ReactionSelector(new ReactionTuning(), roster, new AudienceRandom(seed));
                Publish(selector, roster, kritik);
                var intents = selector.Select(Said(roster, phrase), 20, true, out _);
                Assert.That(intents.All(i => i.Viewer.ViewerId == kritik.ViewerId && i.FollowUp), Is.True, phrase);
                Assert.That(intents.Count, Is.LessThanOrEqualTo(1)); responses += intents.Count;
            }
            Assert.That(responses, Is.GreaterThan(125));
        }

        [TestCase("cooldown")]
        [TestCase("absent")]
        [TestCase("changed epoch")]
        [TestCase("maximum")]
        public void IneligibleThreadOwnerIsNotReplacedByAnAudienceLottery(string guard)
        {
            for (ulong seed = 1; seed <= 60; seed++)
            {
                var kritik = Kritik(); var roster = ReactionFoundationTests.Roster(3, kritik, Zina(), Other());
                var selector = new ReactionSelector(new ReactionTuning { ConversationMaximumTurns = 1 + (guard == "maximum" ? 0 : 4) }, roster, new AudienceRandom(seed));
                Publish(selector, roster, kritik);
                if (guard == "cooldown") selector.Rhythm.RecordConversation(kritik.ViewerId, 19);
                if (guard == "absent" || guard == "changed epoch") roster.Leave(kritik.ViewerId);
                if (guard == "changed epoch") roster.Join(kritik);
                Assert.That(selector.Select(Said(roster, "Почему без настроения?"), 20, true, out _), Is.Empty, guard);
            }
        }

        [Test]
        public void ExplicitOtherViewerWinsOverCurrentThread()
        {
            for (ulong seed = 1; seed <= 60; seed++)
            {
                var kritik = Kritik(); var zina = Zina(); var roster = ReactionFoundationTests.Roster(3, kritik, zina, Other());
                var selector = new ReactionSelector(new ReactionTuning(), roster, new AudienceRandom(seed));
                Publish(selector, roster, kritik);
                Assert.That(selector.Select(Said(roster, "Зина, а у тебя как день прошёл?"), 20, true, out _)
                    .All(i => i.Viewer.ViewerId == zina.ViewerId && !i.FollowUp), Is.True);
            }
        }

        [Test]
        public void ExpiredThreadAllowsOrdinarySelectionAgain()
        {
            int otherAnswers = 0;
            for (ulong seed = 1; seed <= 100; seed++)
            {
                var kritik = Kritik(); var roster = ReactionFoundationTests.Roster(3, kritik, Zina(), Other());
                var selector = new ReactionSelector(new ReactionTuning(), roster, new AudienceRandom(seed));
                Publish(selector, roster, kritik);
                var intents = selector.Select(Said(roster, "Почему без настроения?", 60), 60, true, out _);
                Assert.That(intents.Any(i => i.FollowUp), Is.False);
                otherAnswers += intents.Count(i => i.Viewer.ViewerId != kritik.ViewerId);
            }
            Assert.That(otherAnswers, Is.GreaterThan(20));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void ExplicitGroupQuestionCreatesOneBoundedStaggeredWave(int audience)
        {
            int[] histogram = new int[4];
            for (ulong seed = 1; seed <= 300; seed++)
            {
                var viewers = new[] { Kritik(), Zina(), Other() };
                var roster = ReactionFoundationTests.Roster(audience, viewers.Take(audience).ToArray());
                var selector = new ReactionSelector(new ReactionTuning(), roster, new AudienceRandom(seed));
                Publish(selector, roster, viewers[0], at: 0);
                var e = Said(roster, "да как у вас у всех дела расскажите мне пожалуйста", 20);
                var intents = selector.Select(e, 20, true, out _);
                Assert.That(intents.Count, Is.LessThanOrEqualTo(audience));
                Assert.That(intents.All(i => ReferenceEquals(i.Event, e) && i.Conversational && !i.FollowUp), Is.True);
                Assert.That(intents.Select(i => i.Viewer.ViewerId).Distinct().Count(), Is.EqualTo(intents.Count));
                for (int i = 1; i < intents.Count; i++)
                    Assert.That(intents[i].DueSeconds - intents[i - 1].DueSeconds, Is.GreaterThanOrEqualTo(1.2), "Different people should not publish simultaneously.");
                Assert.That(selector.Rhythm.Tokens, Is.GreaterThanOrEqualTo(-1));
                selector.Rhythm.Refill(180, audience);
                Assert.That(selector.Rhythm.CanSpend(false), Is.True, "One group turn cannot destroy the ordinary chat budget.");
                histogram[intents.Count]++;
            }
            TestContext.WriteLine("audience=" + audience + "; counts 0/1/2/3=" + string.Join("/", histogram));
            if (audience == 1) Assert.That(histogram[1], Is.GreaterThan(240));
            if (audience == 2) { Assert.That(histogram[1], Is.GreaterThan(20)); Assert.That(histogram[2], Is.GreaterThan(70)); }
            if (audience == 3)
            {
                Assert.That(histogram[2], Is.GreaterThan(150));
                Assert.That(histogram[1], Is.GreaterThan(5));
                Assert.That(histogram[3], Is.InRange(10, 90));
            }
        }

        [TestCase("ребят я сегодня играл")]
        [TestCase("Так, секунду")]
        public void NonQuestionDoesNotStartAGroupResponseWave(string phrase)
        {
            for (ulong seed = 1; seed <= 40; seed++)
            {
                var roster = ReactionFoundationTests.Roster(3, Kritik(), Zina(), Other());
                var selector = new ReactionSelector(new ReactionTuning(), roster, new AudienceRandom(seed));
                Assert.That(selector.Select(Said(roster, phrase), 20, true, out _).Count, Is.LessThanOrEqualTo(1));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LateGroupGenerationStillPublishesInOrderWithAPause(bool secondFinishesFirst)
        {
            var a = Kritik(); var b = Zina(); var roster = ReactionFoundationTests.Roster(2, a, b);
            var e = Said(roster, "ребят как дела?", 0);
            var target = Activator.CreateInstance(typeof(ConversationTarget),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null,
                new object[] { ConversationTargetKind.Group, null, 0L }, null);
            ReactionIntent Make(long id, ChatParticipant viewer, int order, double due) =>
                (ReactionIntent)Activator.CreateInstance(typeof(ReactionIntent),
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null,
                    new object[] { id, e, viewer, false, order, due, 20d, roster.Epoch(viewer.ViewerId) }, null);
            var first = Make(1, a, 0, 2);
            var second = Make(2, b, 1, 4);
            foreach (var intent in new[] { first, second })
                typeof(ReactionIntent).GetProperty("ConversationTarget").SetValue(intent, target);
            var model = new ChatDirectorTests.FakeModel(_ => null) { Manual = true };
            var chat = new StreamChat(); var log = new ReactionLog();
            using var director = new ChatDirector(model, new ChatModelSettings { MaximumConcurrent = 2 }, chat, log, () => 0);
            director.BeginBroadcast(new AudienceRandom(1));
            director.Submit(first); director.Submit(second);
            ChatSituation Context() => new("test", 0, 2, ViewerLanguage.Russian, chat.Messages, null);
            director.Update(0, roster, Context, "test");
            model.Complete(1, new LanguageModelResult(LanguageModelStatus.Ok, "сегодня спокойно, чай пью", .2));
            if (secondFinishesFirst)
            {
                director.Update(7, roster, Context, "test");
                Assert.That(chat.Messages, Is.Empty, "The later answer waits for its earlier sibling or that sibling's cancellation.");
            }
            model.Complete(0, new LanguageModelResult(LanguageModelStatus.Ok, "сегодня устал, отдыхаю", .3));
            string textAtNotification = null;
            log.Added += entry => { if (entry.Outcome == ReactionOutcome.Shown) textAtNotification = entry.Text; };
            director.Update(8, roster, Context, "test");
            Assert.That(chat.Messages.Count, Is.EqualTo(1), "Delayed results cannot arrive in a simultaneous burst.");
            Assert.That(chat.Messages[0].ViewerId, Is.EqualTo(a.ViewerId));
            Assert.That(textAtNotification, Is.EqualTo(chat.Messages[0].Text), "Session trace receives the final published text.");
            director.Update(9, roster, Context, "test");
            Assert.That(chat.Messages.Count, Is.EqualTo(1));
            director.Update(9.5, roster, Context, "test");
            Assert.That(chat.Messages.Count, Is.EqualTo(2));
        }

        [Test]
        public void OlderGroupSiblingCannotOverwriteANewerNamedExchange()
        {
            int checkedExchanges = 0;
            for (ulong seed = 1; seed <= 50; seed++)
            {
                var roster = ReactionFoundationTests.Roster(3, Kritik(), Zina(), Other());
                var selector = new ReactionSelector(new ReactionTuning { StaleSeconds = 60 }, roster, new AudienceRandom(seed));
                var wave = selector.Select(Said(roster, "ребят как дела?", 0, 1), 0, true, out _);
                if (wave.Count < 2) continue;
                var first = wave[0]; var late = wave[1];
                var chat = new StreamChat();
                selector.ObservePublished(chat.Add("routing", first.Viewer.ViewerId, first.Viewer.DisplayName,
                    "без настроения", 3, first.Id, ReactionSource.LanguageModel), first);
                var named = selector.Select(Said(roster, first.Viewer.DisplayName + ", почему без настроения?", 20, 2), 20, true, out _);
                if (named.Count == 0) continue;
                selector.ObservePublished(chat.Add("routing", first.Viewer.ViewerId, first.Viewer.DisplayName,
                    "день тяжёлый был", 22, named[0].Id, ReactionSource.LanguageModel), named[0]);
                Assert.That(late.ExpiresSeconds, Is.GreaterThan(23));
                selector.ObservePublished(chat.Add("routing", late.Viewer.ViewerId, late.Viewer.DisplayName,
                    "а у меня спокойно", 23, late.Id, ReactionSource.LanguageModel), late);
                Assert.That(selector.Thread.ViewerId, Is.EqualTo(first.Viewer.ViewerId));
                Assert.That(selector.Thread.LastLine, Is.EqualTo("день тяжёлый был"));
                Assert.That(selector.Thread.Turns, Is.EqualTo(2));
                var next = selector.Select(Said(roster, "почему?", 32, 3), 32, true, out _);
                Assert.That(next.All(i => i.Viewer.ViewerId == first.Viewer.ViewerId), Is.True);
                checkedExchanges++;
            }
            Assert.That(checkedExchanges, Is.GreaterThan(20));
        }
    }
}
