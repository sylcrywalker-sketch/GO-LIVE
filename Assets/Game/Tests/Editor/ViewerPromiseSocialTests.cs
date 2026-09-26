using System;
using System.Linq;
using NUnit.Framework;
using GoLive.Viewers;
using GoLive.Desktop;
using System.Collections.Generic;
using System.Reflection;
using GoLive.PcBuilding;
using GoLive.Shop;
using GoLive.Items;
using GoLive.GameTime;
using GoLive.Economy;

namespace GoLive.Tests
{
    public sealed class ViewerPromiseSocialTests
    {
        private static (ViewerCommunity community, AudienceRoster roster, ViewerProfile a, ViewerProfile b) World(ulong seed = 1)
        {
            var a = ViewerCommunityTests.Profile(); var b = ViewerCommunityTests.Profile(1);
            var roster = new AudienceRoster(EphemeralViewers.Create); roster.SetAudienceSize(20);
            var community = new ViewerCommunity(new[] { a, b }, roster);
            community.BeginBroadcast("promise." + seed, seed, 100, StreamTopic.Hardware);
            roster.Join(a.Participant()); roster.Join(b.Participant());
            return (community, roster, a, b);
        }
        private static StreamEvent Speech(AudienceRoster roster, long sequence, string phrase, double minute = 100)
            => StreamEvent.StreamerSpeech(sequence, "p", sequence,
                SpeechRelevance.Analyze(ReactionFoundationTests.Recognized(phrase, sequence), roster.NamesForMentions())).WithWitnesses(roster, minute);

        [Test]
        public void SameFrameSpeechBeforeConfirmedPurchaseResolvesWithoutLosingEventOrder()
        {
            var profile = ViewerCommunityTests.Profile(); var peripherals = new PcPeripherals();
            var model = new ChatDirectorTests.FakeModel(_ => new LanguageModelResult(LanguageModelStatus.Ok, "ну да", .1));
            using var live = new LiveDesktop(1, peripherals: peripherals, profiles: new[] { profile }, languageModel: model);
            var core = live.State.Viewers;
            var clock = new GameClock(1, 1, 40);
            var orders = new ShopOrderBook(); var assembly = new PcAssembly(Array.Empty<PcSlotSpec>());
            var product = ShopTestData.CreateProduct("gpu", 100, ItemCategory.Electronics, ShopTestData.LoadItem(ShopTestData.BudgetGpuItem));
            using var adapter = new ViewerPromiseGameplayAdapter(core, orders, new[] { product }, assembly, peripherals,
                live.State.Stream, () => clock.Current.TotalSeconds / 60d);
            core.Tick(new StreamerContext(true, true, 100, StreamTopic.Hardware));
            core.Roster.SetAudienceSize(5); core.Roster.Join(profile.Participant());
            Assert.That(live.Say("I will buy a gpu by tomorrow"), Is.True);
            int requests = model.Requests.Count;
            clock.AdvanceSeconds(1);
            var checkout = new ShopCheckout(new Wallet(1000), orders, clock);
            Assert.That(checkout.TryCheckout(new[] { new ShopCheckoutLine(new ShopPurchaseOffer("gpu", 100, 0, 1, true), 1) }).Succeeded, Is.True);
            Assert.That(model.Requests.Count, Is.EqualTo(requests), "order publication cannot start synchronous inference");
            core.Tick(new StreamerContext(true, true, 100.02, StreamTopic.Hardware));
            Assert.That(core.Community.Promises.Summary.Single().Status, Is.EqualTo(PromiseStatus.Fulfilled),
                "Earlier normalized speech must be observed before the paid order from the same frame.");
            Assert.That(core.Community.Promises.Known(profile.Id).Single().Status, Is.EqualTo(PromiseStatus.Fulfilled));
            int submitted = core.Director.Stats.Submitted;
            core.Tick(new StreamerContext(true, true, 100.03, StreamTopic.Hardware));
            Assert.That(core.Community.Promises.Summary.Count, Is.EqualTo(1));
            Assert.That(core.Community.State(profile.Id).Sentiment, Is.EqualTo(2), "normal tick cannot apply the resolved outcome twice");
            Assert.That(core.Director.Stats.Submitted, Is.EqualTo(submitted), "drained speech is not selected a second time");
        }

        [Test]
        public void SameFramePurchaseBeforeSpeechCannotFulfillLaterPromise()
        {
            var profile = ViewerCommunityTests.Profile(); var peripherals = new PcPeripherals();
            using var live = new LiveDesktop(1, peripherals: peripherals, profiles: new[] { profile });
            var core = live.State.Viewers; var clock = new GameClock(1, 1, 40);
            var orders = new ShopOrderBook(); var assembly = new PcAssembly(Array.Empty<PcSlotSpec>());
            var product = ShopTestData.CreateProduct("gpu", 100, ItemCategory.Electronics, ShopTestData.LoadItem(ShopTestData.BudgetGpuItem));
            using var adapter = new ViewerPromiseGameplayAdapter(core, orders, new[] { product }, assembly, peripherals,
                live.State.Stream, () => clock.Current.TotalSeconds / 60d);
            core.Tick(new StreamerContext(true, true, 100, StreamTopic.Hardware));
            core.Roster.SetAudienceSize(5); core.Roster.Join(profile.Participant());
            var checkout = new ShopCheckout(new Wallet(1000), orders, clock);
            Assert.That(checkout.TryCheckout(new[] { new ShopCheckoutLine(new ShopPurchaseOffer("gpu", 100, 0, 1, true), 1) }).Succeeded, Is.True);
            Assert.That(live.Say("I will buy a gpu by tomorrow"), Is.True);
            core.Tick(new StreamerContext(true, true, 100.02, StreamTopic.Hardware));
            Assert.That(core.Community.Promises.Summary.Single().Status, Is.EqualTo(PromiseStatus.Open));
            Assert.That(core.Community.State(profile.Id).Sentiment, Is.Zero);
        }

        [Test]
        public void RealOrderAndInstallationChangesResolveButRestoreAndExistingInventoryDoNot()
        {
            var profile = ViewerCommunityTests.Profile(); var peripherals = new PcPeripherals();
            using var live = new LiveDesktop(1, peripherals: peripherals, profiles: new[] { profile });
            var core = live.State.Viewers; core.Roster.SetAudienceSize(5); core.Roster.Join(profile.Participant());
            var clock = new GameClock(2, 1, 0); double now = clock.Current.TotalSeconds / 60d;
            var orders = new ShopOrderBook(); var assembly = new PcAssembly(Array.Empty<PcSlotSpec>());
            var product = ShopTestData.CreateProduct("gpu", 100, ItemCategory.Electronics, ShopTestData.LoadItem(ShopTestData.BudgetGpuItem));
            using var adapter = new ViewerPromiseGameplayAdapter(core, orders, new[] { product }, assembly, peripherals, live.State.Stream, () => now);
            core.Community.Process(Speech(core.Roster, 1, "я завтра куплю видеокарту"));
            var checkout = new ShopCheckout(new Wallet(1000), orders, clock);
            var offer = new ShopPurchaseOffer("gpu", 100, 0, 1, true);
            Assert.That(checkout.Evaluate(new[] { new ShopCheckoutLine(offer, 1) }), Is.EqualTo(ShopPurchaseResultCode.Success));
            Assert.That(core.Community.Promises.Summary.Single().Status, Is.EqualTo(PromiseStatus.Open));
            adapter.BeginRestore();
            Assert.That(checkout.TryCheckout(new[] { new ShopCheckoutLine(offer, 1) }).Succeeded, Is.True);
            adapter.EndRestore();
            Assert.That(core.Community.Promises.Summary.Single().Status, Is.EqualTo(PromiseStatus.Open));
            Assert.That(checkout.TryCheckout(new[] { new ShopCheckoutLine(offer, 1) }).Succeeded, Is.True);
            Assert.That(core.Community.Promises.Summary.Single().Status, Is.EqualTo(PromiseStatus.Fulfilled));

            // A supported peripheral installation is the actual connected instance, not a speech claim.
            core.Community.Promises.Add("install", ViewerPromiseVocabulary.Parse("я завтра установлю микрофон", 100, 0), EventWitnesses.Capture(core.Roster));
            adapter.BeginRestore(); peripherals.TryConnect(PcPeripheralKind.Microphone, "restored-mic"); adapter.EndRestore();
            Assert.That(core.Community.Promises.Summary.Last().Status, Is.EqualTo(PromiseStatus.Open));
            peripherals.TryDisconnect(PcPeripheralKind.Microphone); peripherals.TryConnect(PcPeripheralKind.Microphone, "new-mic");
            Assert.That(core.Community.Promises.Summary.Last().Status, Is.EqualTo(PromiseStatus.Fulfilled));
        }

        [Test]
        public void NextBroadcastUsesActualStartTimeAndUnverifiableHorizonExpires()
        {
            var (community, roster, a, _) = World();
            community.Process(Speech(roster, 1, "я начну следующий стрим раньше"));
            community.ObserveGameplay(new PromiseGameplayFact(PromiseAction.StartBroadcast, "stream-start", 1480, "next", EventWitnesses.Capture(roster), 40));
            Assert.That(community.Promises.Summary.Single().Status, Is.EqualTo(PromiseStatus.Fulfilled));
            var ledger = new ViewerPromiseLedger();
            ledger.Add("later", ViewerPromiseVocabulary.Parse("я начну следующий стрим раньше", 100, 100), EventWitnesses.Capture(roster));
            ledger.Advance(11000, EventWitnesses.Capture(roster));
            Assert.That(ledger.Summary.Single().Status, Is.EqualTo(PromiseStatus.Expired));
        }

        [Test]
        public void SocialReplyRequiresPublishedOriginHasOneDepthCooldownAndCancelsWhenOriginatorLeaves()
        {
            var (_, roster, a, b) = World(); a.SocialTendency = b.SocialTendency = 1;
            // Existing participants refer to these profiles; no generated message can create a reply chain.
            var origin = ChatDirectorTests.Intent(a.Participant(), "Tester0 привет");
            var chat = new StreamChat();
            var message = chat.Add("b", a.Id, a.DisplayName, "ну ты опять", 20, origin.Id, ReactionSource.LanguageModel);
            ReactionIntent reply = null; ReactionSelector selected = null; int successes = 0;
            for (ulong seed = 1; seed <= 1000; seed++)
            {
                var selector = ReactionFoundationTests.Selector(roster, seed);
                var candidate = selector.SelectPublished(message, origin, 20, 100);
                if (candidate != null) { successes++; reply = candidate; selected = selector; }
                Assert.That(selector.SelectPublished(message, origin, 500, 100), Is.Null, "same real line gets one roll only");
            }
            Assert.That(successes, Is.InRange(8, 55)); Assert.That(reply, Is.Not.Null);
            Assert.That(reply.Event.ReplyDepth, Is.EqualTo(1));
            Assert.That(reply.Event.TriggeringLine, Is.EqualTo(message.Text));
            var second = chat.Add("b", a.Id, a.DisplayName, "снова", 21, origin.Id, ReactionSource.LanguageModel);
            Assert.That(selected.SelectPublished(second, origin, 21, 100), Is.Null, "global 180 second cooldown");
            var replyLine = chat.Add("b", b.Id, b.DisplayName, "ну да", 22, reply.Id, ReactionSource.LanguageModel);
            Assert.That(selected.SelectPublished(replyLine, reply, 500, 100), Is.Null, "depth one cannot chain");
            var model = new ChatDirectorTests.FakeModel(_ => new LanguageModelResult(LanguageModelStatus.Ok, "ну да опять", .1)) { Manual = true };
            var output = new StreamChat();
            using var director = new ChatDirector(model, new ChatModelSettings(), output, new ReactionLog());
            director.Submit(reply); director.Update(20, roster, () => new ChatSituation("c", 20, 20, ViewerLanguage.Russian, null, null), "b");
            Assert.That(model.Requests.Count, Is.EqualTo(1));
            roster.Leave(a.Id); roster.Join(a.Participant(), 21);
            director.Update(21, roster, () => new ChatSituation("c", 21, 20, ViewerLanguage.Russian, null, null), "b");
            Assert.That(model.Cancelled(0), Is.True); Assert.That(output.Messages, Is.Empty);
        }

        [Test]
        public void PromotionIsRareSingleRollStableAndReplacesOnlyExistingChatterSeat()
        {
            int successes = 0;
            for (ulong seed = 1; seed <= 1000; seed++)
            {
                var (community, roster, _, _) = World(seed);
                var chatter = roster.AnonymousChatter(new AudienceRandom(123), _ => true, 0);
                int seats = roster.AudienceSize;
                StreamChatMessage Line(int id, double at) => new("p." + id, chatter.ViewerId, chatter.DisplayName, "ну да", at, id, ReactionSource.Fallback);
                Assert.That(community.ObservePublished(Line(1, 0)), Is.Null);
                Assert.That(community.ObservePublished(Line(2, 200)), Is.Null);
                Assert.That(community.ObservePublished(Line(3, 479)), Is.Null);
                var promoted = community.ObservePublished(Line(4, 480));
                if (promoted == null)
                {
                    for (int n = 5; n <= 20; n++) Assert.That(community.ObservePublished(Line(n, n * 200)), Is.Null, "failed identity never rerolls");
                    continue;
                }
                successes++;
                Assert.That(roster.IsWatching(chatter.ViewerId), Is.False);
                Assert.That(roster.IsWatching(promoted.ViewerId), Is.True);
                Assert.That(roster.AudienceSize, Is.EqualTo(seats));
                Assert.That(community.State(promoted.ViewerId), Is.Not.Null);
                var saved = community.Capture(); community.Restore(saved); community.Restore(saved);
                Assert.That(community.Profiles.Count(p => p.Id == promoted.ViewerId), Is.EqualTo(1));
                Assert.That(community.Capture().Promoted.Single().DisplayName, Is.EqualTo(chatter.DisplayName));
                var duplicateRoster = new AudienceRoster((_, _) => new ChatParticipant("anon.duplicate", chatter.DisplayName, false, chatter.Traits));
                var restored = new ViewerCommunity(new[] { ViewerCommunityTests.Profile(), ViewerCommunityTests.Profile(1) }, duplicateRoster);
                restored.Restore(community.Capture()); duplicateRoster.SetAudienceSize(20);
                Assert.That(duplicateRoster.AnonymousChatter(new AudienceRandom(1), _ => true), Is.Null, "absent durable name is reserved");
                var oversized = community.Capture();
                while (oversized.Promoted.Count < 9) oversized.Promoted.Add(oversized.Promoted[0]);
                Assert.That(community.Validate(oversized), Is.Not.Null, "generated population remains bounded at eight");
                saved.Promoted[0].DisplayName = "tampered";
                Assert.That(community.Profiles.Single(p => p.Id == promoted.ViewerId).DisplayName, Is.EqualTo(chatter.DisplayName));
            }
            Assert.That(successes, Is.InRange(7, 40));
        }

        [Test]
        public void CreationWitnessSurvivesDepartureButAbsentResolutionNeverBecomesFirsthandKnowledge()
        {
            var (community, roster, a, b) = World();
            var speech = Speech(roster, 1, "я завтра куплю видеокарту");
            roster.Leave(a.Id); community.Process(speech);
            Assert.That(community.Promises.Known(a.Id).Count, Is.EqualTo(1));
            community.ObserveGameplay(new PromiseGameplayFact(PromiseAction.Purchase, "gpu", 1500, "order.1", EventWitnesses.Capture(roster)));
            Assert.That(community.Promises.Summary.Single().Status, Is.EqualTo(PromiseStatus.Fulfilled));
            Assert.That(community.Promises.Known(a.Id).Single().Status, Is.EqualTo(PromiseStatus.Open));
            Assert.That(community.Promises.Known(b.Id).Single().Status, Is.EqualTo(PromiseStatus.Fulfilled));
            Assert.That(community.State(a.Id).Sentiment, Is.Zero);
            Assert.That(community.State(b.Id).Sentiment, Is.EqualTo(2));
            community.ObserveGameplay(new PromiseGameplayFact(PromiseAction.Purchase, "gpu", 1500, "order.1", EventWitnesses.Capture(roster)));
            Assert.That(community.State(b.Id).Sentiment, Is.EqualTo(2));
            var save = community.Capture(); community.Restore(save); community.Restore(save);
            Assert.That(community.Promises.Known(a.Id).Single().Status, Is.EqualTo(PromiseStatus.Open));
        }

        [Test]
        public void SpokenSuccessCannotResolveAndOfflineDeadlineDoesNotInformAbsentWitnesses()
        {
            var (community, roster, a, _) = World();
            community.Process(Speech(roster, 1, "я завтра куплю видеокарту"));
            community.Process(Speech(roster, 2, "я купил видеокарту", 1500));
            Assert.That(community.Promises.Summary.Single().Status, Is.EqualTo(PromiseStatus.Open));
            community.EndBroadcast(); community.UpdateContext(2881, StreamTopic.None);
            Assert.That(community.Promises.Summary.Single().Status, Is.EqualTo(PromiseStatus.Broken));
            Assert.That(community.Promises.Known(a.Id).Single().Status, Is.EqualTo(PromiseStatus.Open));
        }

        [Test]
        public void PromiseKnowledgeCannotAuthorizeUnseenOutcomeOrDifferentActionInModelText()
        {
            var (community, roster, a, _) = World();
            community.Process(Speech(roster, 1, "я завтра куплю видеокарту"));
            var context = community.Promises.Retrieve(a.Id, "gpu", 100);
            Assert.That(ViewerPromiseVocabulary.References("помню ты обещал видеокарту", context), Is.True);
            Assert.That(ViewerPromiseVocabulary.References("помню ты обещал и купил видеокарту", context), Is.False);
            Assert.That(ViewerPromiseVocabulary.References("помню ты обещал установить видеокарту", context), Is.False);
            community.ObserveGameplay(new PromiseGameplayFact(PromiseAction.Purchase, "gpu", 200, "too-early", EventWitnesses.Capture(roster)));
            Assert.That(community.Promises.Summary.Single().Status, Is.EqualTo(PromiseStatus.Open));
            community.ObserveGameplay(new PromiseGameplayFact(PromiseAction.Purchase, "microphone", 1500, "wrong-subject", EventWitnesses.Capture(roster)));
            Assert.That(community.Promises.Summary.Single().Status, Is.EqualTo(PromiseStatus.Open));
        }

        [Test]
        public void MalformedPromiseSaveIsRejectedBeforeAnyRelationshipMutation()
        {
            var (community, roster, a, _) = World();
            community.Process(Speech(roster, 1, "я завтра куплю видеокарту"));
            var save = community.Capture(); save.Viewers[0].Sentiment = 80;
            save.Promises.Records[0].Knowledge[0].ViewerId = "viewer.unknown";
            Assert.That(community.Validate(save), Is.Not.Null);
            Assert.Throws<ArgumentException>(() => community.Restore(save));
            Assert.That(community.State(a.Id).Sentiment, Is.Zero);
        }

        [Test]
        public void PromiseCallbacksReleaseOnDropHaveCooldownAndThreeReferenceLimit()
        {
            var (community, roster, a, _) = World();
            community.Process(Speech(roster, 1, "я завтра куплю видеокарту"));
            var ledger = community.Promises;
            var reserve = typeof(ViewerPromiseLedger).GetMethod("Reserve", BindingFlags.Instance | BindingFlags.NonPublic);
            var finish = typeof(ViewerPromiseLedger).GetMethod("Finish", BindingFlags.Instance | BindingFlags.NonPublic);
            ViewerPromiseContext Reserve(long id, double at) => (ViewerPromiseContext)reserve.Invoke(ledger, new object[] { a.Id, "gpu", at, id });
            void Finish(long id, ViewerPromiseContext context, string text, double at) => finish.Invoke(ledger, new object[] { a.Id, id, context, text, at });
            var first = Reserve(1, 100); Assert.That(first, Is.Not.Null);
            Assert.That(Reserve(2, 100), Is.Null); Finish(1, first, null, 100);
            first = Reserve(3, 100); Assert.That(first, Is.Not.Null);
            Finish(3, first, "помню ты обещал видеокарту", 100);
            Assert.That(Reserve(4, 219), Is.Null);
            var second = Reserve(5, 220); Assert.That(second, Is.Not.Null); Finish(5, second, "помню ты обещал видеокарту", 220);
            var third = Reserve(6, 340); Assert.That(third, Is.Not.Null); Finish(6, third, "помню ты обещал видеокарту", 340);
            Assert.That(Reserve(7, 900), Is.Null);
        }

        [Test]
        public void CapacityEvictsOnlyOldTerminalRecordsAndRejectsReplayedOldSpeech()
        {
            var ledger = new ViewerPromiseLedger(); var roster = new AudienceRoster(EphemeralViewers.Create);
            for (int i = 0; i < 16; i++)
                ledger.Add("event." + i, ViewerPromiseVocabulary.Parse("я завтра куплю видеокарту", i * 3000, 0), EventWitnesses.Capture(roster));
            Assert.That(ledger.Summary.Count, Is.EqualTo(16));
            ledger.Advance(100000, EventWitnesses.Capture(roster));
            ledger.Advance(111000, EventWitnesses.Capture(roster));
            Assert.That(ledger.Add("new", ViewerPromiseVocabulary.Parse("я завтра куплю видеокарту", 111000, 0), EventWitnesses.Capture(roster)), Is.True);
            Assert.That(ledger.Add("event.0", ViewerPromiseVocabulary.Parse("я завтра куплю видеокарту", 0, 0), EventWitnesses.Capture(roster)), Is.False);
            Assert.That(ledger.Summary.Count, Is.LessThanOrEqualTo(16));
        }
        [Test]
        public void CommunityOwnsObjectivePromiseLedgerSeparateFromViewerKnowledge()
        {
            Assert.That(typeof(ViewerCommunity).GetProperty("Promises"), Is.Not.Null,
                "Stage F needs an owned bounded ledger with per-viewer knowledge, not prose-derived truth.");
        }

        [Test]
        public void PromptHasAtMostOneStructuredKnownPromise()
        {
            Assert.That(typeof(ChatSituation).GetProperty("Promise"), Is.Not.Null);
        }

        [Test]
        public void ActualTypedPeripheralAndStreamStartFactsSelectRelevantPromiseSubjects()
        {
            Assert.That(ViewerPromiseVocabulary.Subject(StreamEvent.Started(1, "next", 0)), Is.EqualTo("stream-start"));
            Assert.That(ViewerPromiseVocabulary.Subject(StreamEvent.PeripheralChanged(2, "mic", 1, PcPeripheralKind.Microphone, true)), Is.EqualTo("microphone"));
            Assert.That(ViewerPromiseVocabulary.Subject(StreamEvent.PeripheralChanged(3, "cam", 1, PcPeripheralKind.Webcam, true)), Is.EqualTo("webcam"));
        }

        [Test]
        public void SocialEventIsAppendedWithoutChangingExistingAffinityOrdinals()
        {
            Assert.That((int)StreamEventKind.AudienceChatter, Is.EqualTo(10));
            Assert.That(Enum.GetNames(typeof(StreamEventKind)), Does.Contain("ViewerReply"));
        }

        [Test]
        public void PromotionHasSeparatePersistentDescriptors()
        {
            Assert.That(typeof(ViewerCommunitySnapshot).GetField("Promoted"), Is.Not.Null);
        }

        [TestCase("я завтра куплю видеокарту", true)]
        [TestCase("I will buy a microphone tomorrow", true)]
        [TestCase("я завтра не куплю видеокарту", false)]
        [TestCase("если получится я завтра куплю видеокарту", false)]
        [TestCase("может быть завтра куплю видеокарту", false)]
        [TestCase("он сказал я завтра куплю видеокарту", false)]
        [TestCase("\"я завтра куплю видеокарту\"", false)]
        [TestCase("I hope I will buy a microphone tomorrow", false)]
        [TestCase("I bought a microphone", false)]
        [TestCase("я завтра куплю видеокарту?", false)]
        [TestCase("'я завтра куплю видеокарту'", false)]
        [TestCase("‘я завтра куплю видеокарту’", false)]
        [TestCase("я завтра куплю видеокарту, если получится", false)]
        [TestCase("I will start the next stream earlier", true)]
        public void ParsingIsExplicitAndConservative(string speech, bool accepted)
        {
            var parser = typeof(ViewerCommunity).Assembly.GetType("GoLive.Viewers.ViewerPromiseVocabulary");
            Assert.That(parser, Is.Not.Null, "A conservative vocabulary adapter must precede the generic state machine.");
            var parse = parser.GetMethod("Parse");
            Assert.That(parse, Is.Not.Null);
            Assert.That(parse.Invoke(null, new object[] { speech, 100d, 60d }) != null, Is.EqualTo(accepted));
        }
    }
}
