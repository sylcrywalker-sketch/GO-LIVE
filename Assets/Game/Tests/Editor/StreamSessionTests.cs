using System;
using System.Collections.Generic;
using System.Linq;
using GoLive.Desktop;
using GoLive.PcBuilding;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GoLive.Tests
{
    public sealed class StreamSessionTests
    {
        // Game-clock minute used where the time of day is not under test (18:00, inside prime time).
        internal const int PrimeTime = 18 * 60;
        private readonly List<Object> _created = new();
        private PcCapabilities _desktop;
        private PcCapabilities _gaming;

        [SetUp]
        public void SetUp()
        {
            _desktop = Capabilities(false);
            _gaming = Capabilities(true);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object item in _created) Object.DestroyImmediate(item);
            _created.Clear();
        }

        [Test]
        public void AChannelAndWorkingPcWithoutConnectedMicrophoneCanStart()
        {
            var channel = DesktopAccountTests.RegisteredChannel();
            var session = new StreamSession(channel, new DonationAccount());
            Assert.That(session.Connect(channel.ChannelCode), Is.Null);
            Assert.That(session.Start(_desktop, true, 5), Is.Null);
            session.Tick(.75f, PrimeTime);
            Assert.That(session.State, Is.EqualTo(StreamState.Live));
        }

        [Test]
        public void StreamRequiresRegisteredChannelAndMatchingCode()
        {
            var channel = new TrichChannel();
            var session = new StreamSession(channel, new DonationAccount());
            Assert.That(session.Connect("anything"), Is.EqualTo("desktop.stream.channel_required"));
            var outline = DesktopAccountTests.CreatedOutline();
            channel.Register(outline, outline.Address);
            Assert.That(session.Connect("wrong"), Is.EqualTo("desktop.stream.invalid_code"));
            Assert.That(session.Start(_desktop, true, 5), Is.EqualTo("desktop.stream.not_connected"));
            Assert.That(session.Connect(channel.ChannelCode), Is.Null);
            Assert.That(session.IsConnected, Is.True);
        }

        [Test]
        public void DisconnectClearsChannelLinkWithoutChangingQualityAndCanReconnect()
        {
            var session = Connected(out var channel, out _);
            session.SetQuality(StreamQuality.Medium);
            int changes = 0;
            session.Changed += () => changes++;
            Assert.That(session.Disconnect(), Is.Null);
            Assert.That(session.IsConnected, Is.False);
            Assert.That(session.Quality, Is.EqualTo(StreamQuality.Medium));
            Assert.That(session.CheckStart(_desktop, true, 5), Is.EqualTo("desktop.stream.not_connected"));
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(session.Connect(channel.ChannelCode), Is.Null);
            Assert.That(session.IsConnected, Is.True);
        }

        [Test]
        public void DisconnectAlreadyUnlinkedChannelIsIdempotent()
        {
            var session = new StreamSession(new TrichChannel(), new DonationAccount());
            int changes = 0;
            session.Changed += () => changes++;
            Assert.That(session.Disconnect(), Is.Null);
            Assert.That(session.Disconnect(), Is.Null);
            Assert.That(changes, Is.Zero);
        }

        [TestCase(StreamState.Starting)]
        [TestCase(StreamState.Live)]
        [TestCase(StreamState.Stopping)]
        public void DisconnectCannotInterruptAnActiveBroadcast(StreamState state)
        {
            var session = Connected(out _, out _);
            session.Start(_desktop, true, 5);
            if (state != StreamState.Starting) session.Tick(10.75f, PrimeTime);
            if (state == StreamState.Stopping) session.Stop();
            double elapsed = session.DurationSeconds;
            int changes = 0;
            int completions = 0;
            session.Changed += () => changes++;
            session.Completed += _ => completions++;
            Assert.That(session.Disconnect(), Is.EqualTo("desktop.stream.busy"));
            Assert.That(session.IsConnected, Is.True);
            Assert.That(session.State, Is.EqualTo(state));
            Assert.That(session.DurationSeconds, Is.EqualTo(elapsed));
            Assert.That(changes, Is.Zero);
            Assert.That(completions, Is.Zero);
        }

        [Test]
        public void DisconnectPreservesFinishedBroadcastAndSavedSupport()
        {
            var session = Connected(out _, out var donations, BusyAudience());
            int completions = 0;
            StreamSummary summary = null;
            session.Completed += value => { completions++; summary = value; };
            session.Start(_desktop, true, 5);
            session.Tick(60.75f, PrimeTime);
            session.Stop();
            session.Tick(0.25f, PrimeTime);
            AudienceSimulation audience = session.Audience;
            long chatImpulses = audience.ChatMessages;
            Assert.That(session.Disconnect(), Is.Null);
            Assert.That(session.DurationSeconds, Is.EqualTo(60));
            Assert.That(audience.ChatMessages, Is.EqualTo(chatImpulses));
            Assert.That(chatImpulses, Is.GreaterThan(0));
            Assert.That(session.Audience, Is.SameAs(audience));
            Assert.That(audience.IsFinished, Is.True);
            Assert.That(audience.Follows, Is.GreaterThan(0));
            Assert.That(summary.Followers, Is.EqualTo(audience.Follows));
            Assert.That(session.DonationCents, Is.GreaterThan(0));
            Assert.That(summary.DonationCents, Is.EqualTo(session.DonationCents));
            Assert.That(donations.TotalCents, Is.EqualTo(session.DonationCents));
            Assert.That(completions, Is.EqualTo(1));
        }

        [TestCase(StreamQuality.Low, 0.99f, false, "desktop.stream.upload_low")]
        [TestCase(StreamQuality.Low, 1f, false, null)]
        [TestCase(StreamQuality.Medium, 2.99f, false, "desktop.stream.upload_low")]
        [TestCase(StreamQuality.Medium, 3f, false, null)]
        [TestCase(StreamQuality.High, 6f, false, "desktop.stream.gpu_required")]
        [TestCase(StreamQuality.High, 5.99f, true, "desktop.stream.upload_low")]
        [TestCase(StreamQuality.High, 6f, true, null)]
        public void QualityUsesOneEligibilityRule(StreamQuality quality, float upload, bool gaming, string expected)
        {
            var session = Connected(out _, out _);
            Assert.That(session.SetQuality(quality), Is.Null);
            Assert.That(session.CheckStart(gaming ? _gaming : _desktop, true, upload), Is.EqualTo(expected));
            Assert.That(session.Start(gaming ? _gaming : _desktop, true, upload), Is.EqualTo(expected));
        }

        [Test]
        public void PowerAndDesktopAndFiniteUploadAreRequired()
        {
            var session = Connected(out _, out _);
            Assert.That(session.CheckStart(_desktop, false, 5), Is.EqualTo("desktop.stream.pc_off"));
            Assert.That(session.CheckStart(default, true, 5), Is.EqualTo("desktop.stream.desktop_required"));
            Assert.That(session.CheckStart(_desktop, true, float.NaN), Is.EqualTo("desktop.stream.internet_missing"));
            Assert.That(session.CheckStart(_desktop, true, float.PositiveInfinity), Is.EqualTo("desktop.stream.internet_missing"));
        }

        [Test]
        public void StartStopTransitionsRejectDuplicatesAndCompleteExactlyOnce()
        {
            var session = Connected(out var channel, out _);
            var summaries = new List<StreamSummary>();
            session.Completed += summaries.Add;
            Assert.That(session.Start(_desktop, true, 5), Is.Null);
            Assert.That(session.State, Is.EqualTo(StreamState.Starting));
            Assert.That(session.Start(_desktop, true, 5), Is.EqualTo("desktop.stream.busy"));
            Assert.That(session.SetQuality(StreamQuality.High), Is.EqualTo("desktop.stream.busy"));
            session.Tick(0.5f, PrimeTime);
            Assert.That(session.State, Is.EqualTo(StreamState.Starting));
            session.Tick(0.25f, PrimeTime);
            Assert.That(session.State, Is.EqualTo(StreamState.Live));
            session.Tick(20, PrimeTime);
            Assert.That(session.Stop(), Is.Null);
            Assert.That(session.State, Is.EqualTo(StreamState.Stopping));
            Assert.That(session.Stop(), Is.EqualTo("desktop.stream.not_live"));
            session.Tick(0.25f, PrimeTime);
            session.Abort();
            session.Tick(1, PrimeTime);
            Assert.That(session.State, Is.EqualTo(StreamState.Offline));
            Assert.That(summaries.Count, Is.EqualTo(1));
            Assert.That(summaries[0].DurationSeconds, Is.EqualTo(20));
            Assert.That(channel.CompletedStreams, Is.Zero, "root coordinator owns channel totals");
            Assert.That(channel.CompleteStream(summaries[0]), Is.Null);
            Assert.That(channel.CompletedStreams, Is.EqualTo(1));
        }

        [Test]
        public void LiveActivityIsDeterministicAcrossFrameSizesAndBounded()
        {
            var one = Connected(out _, out var oneDonations, BusyAudience());
            var many = Connected(out _, out var manyDonations, BusyAudience());
            one.Start(_desktop, true, 5);
            many.Start(_desktop, true, 5);
            one.Tick(3600.75f, PrimeTime);
            for (int i = 0; i < 14403; i++) many.Tick(0.25f, PrimeTime);
            Assert.That(one.DurationSeconds, Is.EqualTo(many.DurationSeconds));
            Assert.That(one.Audience.CurrentViewers, Is.GreaterThan(0));
            Assert.That(one.Audience.CurrentViewers, Is.EqualTo(many.Audience.CurrentViewers));
            Assert.That(one.Audience.PeakViewers, Is.EqualTo(many.Audience.PeakViewers));
            Assert.That(one.Audience.AverageViewers, Is.EqualTo(many.Audience.AverageViewers));
            Assert.That(one.Audience.Follows, Is.EqualTo(many.Audience.Follows));
            Assert.That(one.Audience.Subscriptions, Is.EqualTo(many.Audience.Subscriptions));
            Assert.That(one.DonationCents, Is.GreaterThan(0));
            Assert.That(one.DonationCents, Is.EqualTo(many.DonationCents));
            Assert.That(oneDonations.TotalCents, Is.EqualTo(manyDonations.TotalCents));
            Assert.That(one.Audience.ChatMessages, Is.EqualTo(many.Audience.ChatMessages));
            Assert.That(oneDonations.History.Count, Is.LessThanOrEqualTo(DonationAccount.MaximumHistory));
        }

        [Test]
        public void PowerLossAbortsImmediatelyAndPostsOneSummaryDuringLiveOrStopping()
        {
            var session = Connected(out _, out _);
            int completions = 0;
            StreamSummary summary = null;
            session.Completed += value => { completions++; summary = value; };
            session.Start(_desktop, true, 5);
            session.Tick(10.75f, PrimeTime);
            session.Stop();
            session.RefreshEnvironment(_desktop, false, 5);
            session.RefreshEnvironment(_desktop, false, 5);
            Assert.That(session.State, Is.EqualTo(StreamState.Offline));
            Assert.That(completions, Is.EqualTo(1));
            Assert.That(summary.Aborted, Is.True);
            Assert.That(summary.DurationSeconds, Is.EqualTo(10));
        }

        [Test]
        public void StartupCancellationDoesNotInventAFinishedLiveStream()
        {
            var session = Connected(out _, out var donations);
            int completions = 0;
            session.Completed += _ => completions++;
            session.Start(_desktop, true, 5);
            session.Tick(0.5f, PrimeTime);
            session.Abort();
            Assert.That(session.State, Is.EqualTo(StreamState.Offline));
            Assert.That(completions, Is.Zero);
            Assert.That(donations.TotalCents, Is.Zero);
        }

        [Test]
        public void LosingGpuAtHighQualityAbortsAndNeverEmitsFurtherDonations()
        {
            var session = Connected(out _, out var donations, BusyAudience());
            session.SetQuality(StreamQuality.High);
            session.Start(_gaming, true, 6);
            session.Tick(60.75f, PrimeTime);
            Assert.That(donations.TotalCents, Is.GreaterThan(0));
            session.RefreshEnvironment(_desktop, true, 6);
            long before = donations.TotalCents;
            session.Tick(1000, PrimeTime);
            Assert.That(session.State, Is.EqualTo(StreamState.Offline));
            Assert.That(donations.TotalCents, Is.EqualTo(before));
        }

        [Test]
        public void InvalidDeltasNeverPoisonTimersOrEmitEvents()
        {
            var session = Connected(out _, out _);
            session.Start(_desktop, true, 5);
            foreach (float delta in new[] { -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                Assert.Throws<ArgumentOutOfRangeException>(() => session.Tick(delta, PrimeTime));
            Assert.That(session.State, Is.EqualTo(StreamState.Starting));
            session.Tick(0.75f, PrimeTime);
            Assert.That(session.DurationSeconds, Is.Zero);
        }

        [Test]
        public void ExtremeFiniteDeltaKeepsFiniteDurationAndBoundedActivity()
        {
            var session = Connected(out _, out var donations, BusyAudience());
            int receiptEvents = 0;
            donations.Changed += () => receiptEvents++;
            session.Start(_desktop, true, 5);
            session.Tick(float.MaxValue, PrimeTime);
            Assert.That(session.State, Is.EqualTo(StreamState.Live), "long elapsed time must not impose a stream cutoff");
            Assert.That(double.IsNaN(session.DurationSeconds) || double.IsInfinity(session.DurationSeconds), Is.False);
            Assert.That(session.DurationSeconds, Is.GreaterThan(1e30));
            Assert.That(receiptEvents, Is.LessThanOrEqualTo(DonationAccount.MaximumReceivedIds));
            Assert.That(session.Audience.ChatMessages, Is.GreaterThan(0));
            Assert.That(session.Audience.Follows, Is.GreaterThanOrEqualTo(0));
            Assert.That(session.Audience.CurrentViewers, Is.GreaterThanOrEqualTo(0));
            Assert.That(double.IsNaN(session.Audience.AverageViewers) || double.IsInfinity(session.Audience.AverageViewers), Is.False);
            Assert.That(session.DonationCents, Is.GreaterThanOrEqualTo(0));
            Assert.That(session.DonationCents, Is.EqualTo(donations.TotalCents));
        }

        [Test]
        public void CompletedCoordinatorCanStartAnotherSessionWithoutDuplicateSummaries()
        {
            var session = Connected(out var channel, out _);
            var outline = DesktopAccountTests.CreatedOutline();
            session.Completed += summary =>
            {
                Assert.That(channel.CompleteStream(summary), Is.Null);
                Assert.That(outline.Receive(summary.Id, "desktop.mail.summary.subject", "desktop.mail.summary.body", summary.Sequence.ToString()), Is.Null);
            };
            for (int i = 0; i < 2; i++)
            {
                Assert.That(session.Start(_desktop, true, 5), Is.Null);
                session.Tick(30.75f, PrimeTime);
                session.Abort();
                session.Abort();
            }
            Assert.That(channel.CompletedStreams, Is.EqualTo(2));
            Assert.That(outline.Messages.Count, Is.EqualTo(2));
            Assert.That(outline.Messages[0].Id, Is.Not.EqualTo(outline.Messages[1].Id));
            Assert.That(channel.TotalDurationSeconds, Is.EqualTo(60));
        }

        [Test]
        public void ResetDiscardsLiveSessionAndAllTransientPresentationWithoutCompleting()
        {
            var session = Connected(out var channel, out var donations, BusyAudience());
            int completions = 0;
            session.Completed += _ => completions++;
            session.SetQuality(StreamQuality.High);
            session.Start(_gaming, true, 6);
            session.Tick(60.75f, PrimeTime);
            long committedReceipts = donations.TotalCents;
            session.Reset();
            Assert.That(session.State, Is.EqualTo(StreamState.Offline));
            Assert.That(session.IsConnected, Is.False);
            Assert.That(session.Quality, Is.EqualTo(StreamQuality.Low));
            Assert.That(session.DurationSeconds, Is.Zero);
            Assert.That(session.Audience.CurrentViewers, Is.Zero);
            Assert.That(session.Audience.PeakViewers, Is.Zero);
            Assert.That(session.Audience.Follows, Is.Zero);
            Assert.That(session.DonationCents, Is.Zero);
            Assert.That(completions, Is.Zero);
            Assert.That(channel.CompletedStreams, Is.Zero);
            Assert.That(donations.TotalCents, Is.EqualTo(committedReceipts), "donation account owns saved receipts");
        }

        // A lively audience so outcome assertions are certain for the fixed seed.
        internal static AudienceTuning BusyAudience() => new()
        {
            DiscoveryViewers = 30, RampSeconds = 5, MeanWatchSeconds = 20,
            FollowRate = .02f, SubscriptionRate = .01f, DonationRate = .02f, ChatRate = .2f
        };

        private static StreamSession Connected(out TrichChannel channel, out DonationAccount donations, AudienceTuning tuning = null)
        {
            channel = DesktopAccountTests.RegisteredChannel();
            donations = new DonationAccount();
            var peripherals = new PcPeripherals();
            Assert.That(peripherals.TryConnect(PcPeripheralKind.Microphone, "connected-test-mic"), Is.True);
            var session = new StreamSession(channel, donations, peripherals, tuning, new AudienceRandom(20260925));
            Assert.That(session.Connect(channel.ChannelCode), Is.Null);
            return session;
        }

        private PcCapabilities Capabilities(bool gpu) => CreateCapabilities(_created, gpu);

        internal static PcCapabilities CreateCapabilities(List<Object> created, bool gpu)
        {
            var types = new[] { PcComponentType.Motherboard, PcComponentType.Cpu, PcComponentType.Ram, PcComponentType.Psu, PcComponentType.Storage, PcComponentType.Gpu };
            var connectors = new[] { PcConnector.MotherboardTray, PcConnector.CpuSocket, PcConnector.MemorySlot, PcConnector.PowerSupplyBay, PcConnector.SataStorage, PcConnector.PcieX16 };
            var slots = new List<PcSlotSpec>();
            for (int i = 0; i < types.Length; i++) slots.Add(new PcSlotSpec("slot-" + i, types[i], connectors[i]));
            var assembly = new PcAssembly(slots);
            for (int i = 0; i < (gpu ? 6 : 5); i++)
            {
                var spec = ScriptableObject.CreateInstance<PcComponentSpec>();
                ShopTestData.Set(spec, "componentType", types[i]);
                ShopTestData.Set(spec, "connector", connectors[i]);
                ShopTestData.Set(spec, "powerCapacityWatts", types[i] == PcComponentType.Psu ? 300 : 0);
                created.Add(spec);
                Assert.That(assembly.TryRecordInstall("slot-" + i, "part-" + i, spec), Is.True);
            }
            return PcCapabilities.Evaluate(assembly);
        }
    }
}
