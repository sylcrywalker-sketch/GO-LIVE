using System;
using System.Collections.Generic;
using System.Linq;
using GoLive.Desktop;
using GoLive.Viewers;
using NUnit.Framework;

namespace GoLive.Tests
{
    public sealed class ViewerIntegrationGuardTests
    {
        [TestCase("Anonymous")]
        [TestCase("anonymous")]
        public void AnonymousReceiptCannotBelongToAPresentViewerWithTheSameDisplayName(string sender)
        {
            var profile = ViewerCommunityTests.Profile();
            profile.DisplayName = "Anonymous"; profile.DonationTendency = 0;
            using var live = new LiveDesktop(20260926, profiles: new[] { profile });
            for (int i = 0; i < 300 && live.State.Stream.Audience.CurrentViewers == 0; i++) live.Tick(1);
            var core = live.State.Viewers;
            core.Roster.SetAudienceSize(live.State.Stream.Audience.CurrentViewers);
            core.Roster.Join(profile.Participant());
            Assert.That(core.Roster.IsWatching(profile.Id), Is.True);
            string receiptId = live.State.Stream.BroadcastId + ".donation.anonymous";
            Assert.That(live.State.Donation.Receive(receiptId, sender, 200), Is.Null);
            var events = new List<StreamEvent>(); core.Events.Drain(events);
            var donation = events.Single(e => e.Kind == StreamEventKind.Donation && e.Key == receiptId);
            Assert.That(donation.SubjectName, Is.EqualTo(sender));
            Assert.That(donation.SubjectViewerId, Is.Null,
                "The fallback sender is an unowned outcome, not a present viewer's personal donation.");
            var intents = ReactionFoundationTests.Selector(core.Roster, 1).Select(donation, live.State.Stream.DurationSeconds, true, out _);
            Assert.That(intents.Any(i => i.Direct), Is.False);
        }

        [Test]
        public void InvalidSelectedDonorNameCannotDiscardTheAuthoritativeAmount()
        {
            var profiles = Enumerable.Range(0, 40).Select(i => ViewerCommunityTests.Profile(i)).ToArray();
            foreach (var profile in profiles)
            {
                profile.DisplayName = "<" + profile.DisplayName + ">";
                profile.DonationTendency = 1;
            }
            using var invalidNames = new LiveDesktop(20260926, profiles: profiles);
            using var baseline = new LiveDesktop(20260926);
            for (int i = 0; i < 3600 && baseline.State.Donation.History.Count < 3; i++)
            {
                var roster = invalidNames.State.Viewers.Roster;
                roster.SetAudienceSize(invalidNames.State.Stream.Audience.CurrentViewers);
                foreach (var profile in profiles) roster.Join(profile.Participant());
                invalidNames.Tick(1, listening: false); baseline.Tick(1, listening: false);
                Assert.That(invalidNames.State.Donation.TotalCents, Is.EqualTo(baseline.State.Donation.TotalCents),
                    "An invalid presentation name must not discard a simulation donation.");
                Assert.That(invalidNames.State.Stream.DonationCents, Is.EqualTo(baseline.State.Stream.DonationCents));
            }
            Assert.That(baseline.State.Donation.History, Is.Not.Empty);
            Assert.That(invalidNames.State.Donation.History.Any(r => r.SenderName == "Anonymous"), Is.True,
                "The test must exercise a selected name falling back to a valid account sender.");
        }

        [Test]
        public void SaveCannotClaimAStartEarlierThanMidnightWasFulfilled()
        {
            var ledger = new ViewerPromiseLedger();
            ledger.Add("midnight", ViewerPromiseVocabulary.Parse("я начну следующий стрим раньше", 100, 0), EventWitnesses.Empty());
            var snapshot = ledger.Capture(); var record = snapshot.Records.Single();
            Assert.That(record.Threshold, Is.Zero);
            record.Status = PromiseStatus.Fulfilled; record.TerminalMinutes = 200;
            Assert.That(ViewerPromiseLedger.Validate(snapshot, new HashSet<string>()), Is.Not.Null,
                "Nonnegative gameplay values cannot satisfy ValueBelow zero.");
        }

        [TestCase("я задонатил тебе")]
        [TestCase("я подписался на канал")]
        [TestCase("я сабнул наконец")]
        public void DiscussionOfSupportCannotInventAnOwnTransaction(string text)
        {
            var viewer = ChatDirectorTests.Viewer("viewer.a", "Alpha");
            var intent = ChatDirectorTests.Intent(viewer, "Alpha что думаешь о донатах?");
            Assert.That(ChatOutputValidator.Validate(text, intent, null).Accepted, Is.False);
        }

        [Test]
        public void AddressingEveryNamedViewerStillUsesTheTotalChatBudgetAndEventCap()
        {
            var roster = new AudienceRoster(EphemeralViewers.Create); roster.SetAudienceSize(40);
            for (int i = 0; i < 40; i++) roster.Join(ChatDirectorTests.Viewer("viewer." + i, "Tester" + i));
            var selector = new ReactionSelector(new ReactionTuning(), roster, new AudienceRandom(12));
            string speech = string.Join(" ", roster.Named.Select(v => v.DisplayName)) + " привет";
            var e = StreamEvent.StreamerSpeech(1, "budget", 1, SpeechRelevance.Analyze(
                ReactionFoundationTests.Recognized(speech, 1), roster.NamesForMentions()));
            var intents = selector.Select(e, 1, true, out _);
            Assert.That(intents.Count, Is.LessThanOrEqualTo(ReactionSelector.MaximumReactions(e, 40)));
            Assert.That(selector.Rhythm.Tokens, Is.GreaterThanOrEqualTo(-1));
        }

        [Test]
        public void OrdinaryDonationReceiptsCannotInventAbsentAuthoredNames()
        {
            using var live = new LiveDesktop(20260926);
            var receipts = new List<DonationReceipt>();
            live.State.Donation.Received += receipt =>
            {
                receipts.Add(receipt);
                Assert.That(live.State.Viewers.Roster.FindByName(receipt.SenderName) != null || receipt.SenderName == "Anonymous", Is.True,
                    "Attribution must exist before the account publishes the accepted receipt: " + receipt.SenderName);
            };
            for (int i = 0; i < 7200 && receipts.Count < 3; i++) live.Tick(1);
            Assert.That(receipts, Is.Not.Empty);
        }

        [Test]
        public void SaveCannotAssertFulfillmentBeforeItsPromisedWindow()
        {
            var profile = ViewerCommunityTests.Profile();
            var roster = new AudienceRoster(EphemeralViewers.Create); roster.SetAudienceSize(1); roster.Join(profile.Participant());
            var ledger = new ViewerPromiseLedger();
            ledger.Add("tomorrow", ViewerPromiseVocabulary.Parse("я завтра куплю видеокарту", 100, 0), EventWitnesses.Capture(roster));
            var snapshot = ledger.Capture();
            // The serialized schema is intentionally inspected through its public capture contract.
            var record = snapshot.Records.Single();
            record.Status = PromiseStatus.Fulfilled; record.TerminalMinutes = 101;
            Assert.That(ViewerPromiseLedger.Validate(snapshot, new HashSet<string> { profile.Id }), Is.Not.Null);
        }

        [Test]
        public void OnlyPresentWillingSupportersCanOwnAnOutcomeAndFirstFollowsSurviveRepeatedLoad()
        {
            var present = ViewerCommunityTests.Profile(); present.DonationTendency = 1; present.FollowTendency = 1;
            var absent = ViewerCommunityTests.Profile(1); absent.DonationTendency = 1; absent.FollowTendency = 1;
            var roster = new AudienceRoster(EphemeralViewers.Create); roster.SetAudienceSize(1);
            var community = new ViewerCommunity(new[] { present, absent }, roster);
            roster.Join(present.Participant());
            var support = new ViewerSupportAttribution(roster, community);
            Assert.That(support.Choose(StreamEventKind.Donation, 4, 1, 10, 1)?.ViewerId, Is.EqualTo(present.Id));
            Assert.That(support.Choose(StreamEventKind.Follow, 4, 1, 10, 1)?.ViewerId, Is.EqualTo(present.Id));
            Assert.That(support.Choose(StreamEventKind.Subscription, 4, 1, 10, 1)?.ViewerId, Is.EqualTo(present.Id));
            Assert.That(community.State(absent.Id).HasFollowed, Is.False);
            var saved = community.Capture();
            community.Restore(saved); community.Restore(saved);
            roster.SetAudienceSize(1); roster.Join(present.Participant());
            Assert.That(support.Choose(StreamEventKind.Follow, 4, 2, 20, 1), Is.Null);
            Assert.That(support.Choose(StreamEventKind.Subscription, 4, 2, 20, 1), Is.Null);
            Assert.That(support.Choose(StreamEventKind.Donation, 4, 2, 20, 1)?.ViewerId, Is.EqualTo(present.Id), "Donations can recur.");
            Assert.That(support.Choose(StreamEventKind.Donation, 4, 3, 20, 0), Is.Null, "Reconcile loss of the last seat before naming a donor.");
            Assert.That(roster.Named, Is.Empty);
        }

        [Test]
        public void ZeroTendencyCannotBorrowAnAbsentProfileAndAnonymousSupportStaysTransient()
        {
            var profile = ViewerCommunityTests.Profile(); profile.DonationTendency = 0; profile.FollowTendency = 0;
            var roster = new AudienceRoster(EphemeralViewers.Create); roster.SetAudienceSize(1);
            var community = new ViewerCommunity(new[] { profile }, roster); roster.Join(profile.Participant());
            var support = new ViewerSupportAttribution(roster, community);
            Assert.That(support.Choose(StreamEventKind.Donation, 4, 1, 10, 1), Is.Null);
            var donor = support.Choose(StreamEventKind.Donation, 4, 2, 10, 2);
            Assert.That(donor, Is.Not.Null); Assert.That(donor.IsPermanent, Is.False);
            Assert.That(roster.IsWatching(donor.ViewerId), Is.True);
            Assert.That(community.Capture().Viewers.Count, Is.EqualTo(1));
            Assert.That(community.State(profile.Id).HasFollowed, Is.False);
        }

        [TestCase("донаты тут вообще работают", ViewerLanguage.Russian)]
        [TestCase("do subscriptions work here", ViewerLanguage.English)]
        public void DiscussingSupportRemainsAllowed(string text, ViewerLanguage language)
        {
            var viewer = ChatDirectorTests.Viewer("viewer.a", "Alpha", language);
            var intent = ChatDirectorTests.Intent(viewer, "Alpha что думаешь о донатах?");
            Assert.That(ChatOutputValidator.Validate(text, intent, null).Accepted, Is.True);
        }

        [TestCase("i just donated", ViewerLanguage.English)]
        [TestCase("i have subscribed", ViewerLanguage.English)]
        [TestCase("я зафолловил", ViewerLanguage.Russian)]
        public void OtherFirstPersonClaimsAlsoNeedTheirMatchingOutcome(string text, ViewerLanguage language)
        {
            var viewer = ChatDirectorTests.Viewer("viewer.a", "Alpha", language);
            var intent = ChatDirectorTests.Intent(viewer, "Alpha что думаешь о донатах?");
            Assert.That(ChatOutputValidator.Validate(text, intent, null).Accepted, Is.False);
        }

        [Test]
        public void OwnDonationMayBeAcknowledgedButCannotBecomeOwnSubscription()
        {
            var viewer = ChatDirectorTests.Viewer("viewer.a", "Alpha");
            ReactionIntent intent = null;
            for (ulong seed = 1; seed < 100 && intent == null; seed++)
                intent = ReactionFoundationTests.Selector(ReactionFoundationTests.Roster(1, viewer), seed)
                    .Select(StreamEvent.Donation(1, "own", 10, viewer.ViewerId, viewer.DisplayName, 100), 10, true, out _)
                    .FirstOrDefault(i => i.Direct);
            Assert.That(intent, Is.Not.Null);
            Assert.That(ChatOutputValidator.Validate("я задонатил тебе", intent, null).Accepted, Is.True);
            Assert.That(ChatOutputValidator.Validate("я сабнул наконец", intent, null).Accepted, Is.False);
        }

        [TestCase(PromiseStatus.Fulfilled, 3000)]
        [TestCase(PromiseStatus.Broken, 1441)]
        [TestCase(PromiseStatus.Expired, 3000)]
        public void SaveRejectsOtherUnreachableTerminalOutcomes(PromiseStatus status, double at)
        {
            var roster = new AudienceRoster(EphemeralViewers.Create);
            var ledger = new ViewerPromiseLedger();
            ledger.Add("tomorrow", ViewerPromiseVocabulary.Parse("я завтра куплю видеокарту", 100, 0), EventWitnesses.Capture(roster));
            var snapshot = ledger.Capture(); var record = snapshot.Records.Single();
            record.Status = status; record.TerminalMinutes = at;
            Assert.That(ViewerPromiseLedger.Validate(snapshot, new HashSet<string>()), Is.Not.Null);
        }

        [Test]
        public void RepeatedModelOutagesCannotChangeOrReplayAuthoritativeSupport()
        {
            var model = new ChatDirectorTests.FakeModel(_ => new LanguageModelResult(LanguageModelStatus.Ok, "ну ладно", .01));
            using var outage = new LiveDesktop(20260926, languageModel: model);
            using var baseline = new LiveDesktop(20260926);
            var receipts = new HashSet<string>();
            outage.State.Donation.Received += receipt => Assert.That(receipts.Add(receipt.Id), Is.True);
            for (int i = 0; i < 3600; i++)
            {
                outage.State.Viewers.Director.ModelEnabled = i % 120 < 60;
                outage.Tick(1, listening: false); baseline.Tick(1, listening: false);
                Assert.That(outage.State.Stream.Audience.CurrentViewers, Is.EqualTo(baseline.State.Stream.Audience.CurrentViewers));
                Assert.That(outage.State.Stream.Audience.Follows, Is.EqualTo(baseline.State.Stream.Audience.Follows));
                Assert.That(outage.State.Stream.Audience.Subscriptions, Is.EqualTo(baseline.State.Stream.Audience.Subscriptions));
                Assert.That(outage.State.Donation.TotalCents, Is.EqualTo(baseline.State.Donation.TotalCents));
                Assert.That(outage.State.Stream.State, Is.EqualTo(StreamState.Live));
            }
            Assert.That(receipts, Is.Not.Empty);
            Assert.That(model.Requests, Is.Not.Empty);
            Assert.That(outage.State.Viewers.Director.Stats.ShownFromFallback, Is.GreaterThan(0));
        }
    }
}
