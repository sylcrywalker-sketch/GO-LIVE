using System;
using System.Collections.Generic;
using GoLive.Desktop;
using GoLive.Economy;
using GoLive.PcBuilding;
using NUnit.Framework;
using Object = UnityEngine.Object;

namespace GoLive.Tests
{
    // StreamSession stays the lifecycle authority; its AudienceSimulation is the only audience state.
    public sealed class StreamAudienceIntegrationTests
    {
        private readonly List<Object> _created = new();
        private PcCapabilities _desktop;
        private PcCapabilities _gaming;

        [SetUp]
        public void SetUp()
        {
            _desktop = StreamSessionTests.CreateCapabilities(_created, false);
            _gaming = StreamSessionTests.CreateCapabilities(_created, true);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object item in _created) Object.DestroyImmediate(item);
            _created.Clear();
        }

        [Test]
        public void AudienceRunsOnlyWhileLiveAndFreezesAtCompletion()
        {
            using DesktopState state = Registered(StreamSessionTests.BusyAudience(), 11);
            Assert.That(state.Stream.Audience.IsFinished, Is.True, "no broadcast: an idle, frozen audience");
            Assert.That(state.Stream.Audience.CurrentViewers, Is.Zero);
            Assert.That(state.Stream.Connect(state.Trich.ChannelCode), Is.Null);
            Assert.That(state.Stream.Start(_desktop, true, 5), Is.Null);
            AudienceSimulation audience = state.Stream.Audience;
            state.Stream.Tick(.5f, StreamSessionTests.PrimeTime);
            Assert.That(audience.SimulatedSeconds, Is.Zero, "Starting does not simulate viewers");
            Assert.That(audience.CurrentViewers, Is.Zero);
            state.Stream.Tick(.25f, StreamSessionTests.PrimeTime);
            Assert.That(state.Stream.State, Is.EqualTo(StreamState.Live));
            state.Stream.Tick(120, StreamSessionTests.PrimeTime);
            Assert.That(audience.SimulatedSeconds, Is.EqualTo(120));
            Assert.That(audience.PeakViewers, Is.GreaterThan(0));
            Assert.That(state.Stream.Stop(), Is.Null);
            double frozenSeconds = audience.SimulatedSeconds;
            int frozenViewers = audience.CurrentViewers;
            state.Stream.Tick(.1f, StreamSessionTests.PrimeTime);
            Assert.That(audience.SimulatedSeconds, Is.EqualTo(frozenSeconds), "Stopping does not simulate viewers");
            state.Stream.Tick(1, StreamSessionTests.PrimeTime);
            Assert.That(state.Stream.State, Is.EqualTo(StreamState.Offline));
            Assert.That(audience.IsFinished, Is.True);
            state.Stream.Tick(60, StreamSessionTests.PrimeTime);
            Assert.That(audience.CurrentViewers, Is.EqualTo(frozenViewers));
            Assert.That(state.Stream.Audience, Is.SameAs(audience), "the last result stays readable until the next start");
        }

        [Test]
        public void FirstStreamBoostAppliesOnlyBeforeTheFirstCompletedStream()
        {
            using DesktopState state = Registered(new AudienceTuning(), 3);
            Assert.That(state.Stream.Connect(state.Trich.ChannelCode), Is.Null);
            Assert.That(state.Stream.Start(_desktop, true, 5), Is.Null);
            state.Stream.Tick(.75f, StreamSessionTests.PrimeTime);
            Assert.That(state.Stream.Audience.FirstStreamBoost, Is.True);
            Assert.That(state.Stream.Audience.CurrentViewers, Is.InRange(1, 3));
            state.Stream.Tick(30, StreamSessionTests.PrimeTime);
            state.Stream.Abort();
            Assert.That(state.Trich.CompletedStreams, Is.EqualTo(1));

            Assert.That(state.Stream.Start(_desktop, true, 5), Is.Null);
            state.Stream.Tick(.75f, StreamSessionTests.PrimeTime);
            Assert.That(state.Stream.Audience.FirstStreamBoost, Is.False, "the onboarding boost is not a permanent bonus");
            Assert.That(state.Stream.Audience.CurrentViewers, Is.Zero);
        }

        [Test]
        public void StartupCancelledBeforeLiveKeepsTheFirstStreamEligible()
        {
            using DesktopState state = Registered(new AudienceTuning(), 5);
            Assert.That(state.Stream.Connect(state.Trich.ChannelCode), Is.Null);
            Assert.That(state.Stream.Start(_desktop, true, 5), Is.Null);
            state.Stream.Abort();
            Assert.That(state.Trich.CompletedStreams, Is.Zero);
            Assert.That(state.Stream.Start(_desktop, true, 5), Is.Null);
            Assert.That(state.Stream.Audience.FirstStreamBoost, Is.True);
        }

        [Test]
        public void FollowsAndSubscriptionsApplyOnceThroughTheCompletionPipeline()
        {
            using DesktopState state = Registered(StreamSessionTests.BusyAudience(), 21);
            var summaries = new List<StreamSummary>();
            state.Stream.Completed += summaries.Add;
            Assert.That(state.Stream.Connect(state.Trich.ChannelCode), Is.Null);
            Assert.That(state.Stream.Start(_desktop, true, 5), Is.Null);
            state.Stream.Tick(600.75f, StreamSessionTests.PrimeTime);
            AudienceSimulation audience = state.Stream.Audience;
            Assert.That(state.Trich.TotalFollowers, Is.Zero, "follows commit at completion, not per tick");
            state.Stream.Abort();
            state.Stream.Abort();
            state.Stream.Tick(10, StreamSessionTests.PrimeTime);
            Assert.That(summaries.Count, Is.EqualTo(1), "abort finalizes exactly once");
            StreamSummary summary = summaries[0];
            Assert.That(summary.Aborted, Is.True);
            Assert.That(summary.Followers, Is.EqualTo(audience.Follows).And.GreaterThan(0));
            Assert.That(summary.Subscriptions, Is.EqualTo(audience.Subscriptions).And.GreaterThan(0));
            Assert.That(state.Trich.TotalFollowers, Is.EqualTo(summary.Followers));
            Assert.That(state.Trich.TotalSubscriptions, Is.EqualTo(summary.Subscriptions));
            Assert.That(state.Trich.CompleteStream(summary), Is.Null, "a repeated completion is recognized");
            Assert.That(state.Trich.TotalFollowers, Is.EqualTo(summary.Followers));
            Assert.That(state.Trich.TotalSubscriptions, Is.EqualTo(summary.Subscriptions));
            Assert.That(state.Outline.Messages.Count, Is.EqualTo(1));
        }

        [Test]
        public void StreamDonationsReachTheWalletExactlyOnce()
        {
            using DesktopState state = Registered(StreamSessionTests.BusyAudience(), 31);
            var wallet = new Wallet(0);
            using var payout = new DonationPayout(state.Donation, wallet);
            Assert.That(state.Stream.Connect(state.Trich.ChannelCode), Is.Null);
            Assert.That(state.Stream.Start(_desktop, true, 5), Is.Null);
            state.Stream.Tick(300.75f, StreamSessionTests.PrimeTime);
            Assert.That(state.Stream.DonationCents, Is.GreaterThan(0));
            Assert.That(wallet.BalanceCents, Is.EqualTo(state.Stream.DonationCents));
            Assert.That(state.Donation.TotalCents, Is.EqualTo(state.Stream.DonationCents));
            state.Stream.Abort();
            long paid = wallet.BalanceCents;
            state.Restore(state.Capture(), Array.Empty<DesktopDrive>());
            state.Stream.Tick(60, StreamSessionTests.PrimeTime);
            Assert.That(wallet.BalanceCents, Is.EqualTo(paid), "completion and restore never pay again");
            Assert.That(state.Trich.TotalDonationCents, Is.EqualTo(paid));
        }

        [Test]
        public void ReceiptObserverThatEndsTheStreamStillCountsThatDonationOnce()
        {
            using DesktopState state = Registered(StreamSessionTests.BusyAudience(), 41);
            var wallet = new Wallet(0);
            using var payout = new DonationPayout(state.Donation, wallet);
            var summaries = new List<StreamSummary>();
            state.Stream.Completed += summaries.Add;
            state.Donation.Received += _ => state.Stream.Abort();
            Assert.That(state.Stream.Connect(state.Trich.ChannelCode), Is.Null);
            Assert.That(state.Stream.Start(_desktop, true, 5), Is.Null);
            state.Stream.Tick(300.75f, StreamSessionTests.PrimeTime);
            state.Stream.Tick(300, StreamSessionTests.PrimeTime);
            Assert.That(summaries.Count, Is.EqualTo(1));
            Assert.That(state.Donation.History.Count, Is.EqualTo(1), "no receipt after the stream ended");
            long amount = state.Donation.History[0].AmountCents;
            Assert.That(summaries[0].DonationCents, Is.EqualTo(amount));
            Assert.That(wallet.BalanceCents, Is.EqualTo(amount));
            Assert.That(state.Trich.TotalDonationCents, Is.EqualTo(amount));
        }

        [Test]
        public void MissingInGameMicrophoneAndWebcamNeverStopTheAudience()
        {
            var channel = DesktopAccountTests.RegisteredChannel();
            var stream = new StreamSession(channel, new DonationAccount(), new PcPeripherals(), new AudienceTuning(), new AudienceRandom(8));
            Assert.That(stream.Connect(channel.ChannelCode), Is.Null);
            Assert.That(stream.Start(_desktop, true, 5), Is.Null);
            stream.Tick(900.75f, StreamSessionTests.PrimeTime);
            Assert.That(stream.State, Is.EqualTo(StreamState.Live));
            Assert.That(stream.Audience.SimulatedSeconds, Is.EqualTo(900));
            Assert.That(stream.Audience.PeakViewers, Is.GreaterThan(0));
        }

        [Test]
        public void HighQualityStillFollowsTheExistingCapabilityRule()
        {
            var channel = DesktopAccountTests.RegisteredChannel();
            var stream = new StreamSession(channel, new DonationAccount(), new PcPeripherals(), new AudienceTuning(), new AudienceRandom(9));
            Assert.That(stream.Connect(channel.ChannelCode), Is.Null);
            Assert.That(stream.SetQuality(StreamQuality.High), Is.Null);
            Assert.That(stream.Start(_desktop, true, 12), Is.EqualTo("desktop.stream.gpu_required"));
            Assert.That(stream.Start(_gaming, true, 12), Is.Null);
            stream.Tick(60.75f, StreamSessionTests.PrimeTime);
            Assert.That(stream.Audience.SimulatedSeconds, Is.EqualTo(60));
        }

        [Test]
        public void TickRequiresAValidGameClockMinute()
        {
            var channel = DesktopAccountTests.RegisteredChannel();
            var stream = new StreamSession(channel, new DonationAccount());
            Assert.Throws<ArgumentOutOfRangeException>(() => stream.Tick(1, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => stream.Tick(1, 1440));
        }

        [Test]
        public void SameSeedReproducesTheWholeBroadcast()
        {
            StreamSummary Run()
            {
                using DesktopState state = Registered(StreamSessionTests.BusyAudience(), 77);
                StreamSummary result = null;
                state.Stream.Completed += value => result = value;
                state.Stream.Connect(state.Trich.ChannelCode);
                state.Stream.Start(_desktop, true, 5);
                state.Stream.Tick(420.75f, StreamSessionTests.PrimeTime);
                state.Stream.Abort();
                return result;
            }
            StreamSummary a = Run(), b = Run();
            Assert.That(b.PeakViewers, Is.EqualTo(a.PeakViewers));
            Assert.That(b.AverageViewers, Is.EqualTo(a.AverageViewers));
            Assert.That(b.Followers, Is.EqualTo(a.Followers));
            Assert.That(b.Subscriptions, Is.EqualTo(a.Subscriptions));
            Assert.That(b.DonationCents, Is.EqualTo(a.DonationCents));
        }

        private static DesktopState Registered(AudienceTuning tuning, ulong seed)
        {
            var apps = new[]
            {
                new DesktopAppDefinition(DesktopAppId.MyComputer, "computer", "computer.desc", null, 20, true),
                new DesktopAppDefinition(DesktopAppId.Hub, "hub", "hub.desc", null, 40, true),
                new DesktopAppDefinition(DesktopAppId.Web, "web", "web.desc", null, 40, true),
                new DesktopAppDefinition(DesktopAppId.Outline, "outline", "outline.desc", null, 50, false),
                new DesktopAppDefinition(DesktopAppId.Trich, "trich", "trich.desc", null, 50, false),
                new DesktopAppDefinition(DesktopAppId.Streamly, "streamly", "streamly.desc", null, 50, false),
                new DesktopAppDefinition(DesktopAppId.Donation, "donation", "donation.desc", null, 50, false)
            };
            var state = new DesktopState(apps, null, tuning, new AudienceRandom(seed));
            Assert.That(state.Outline.CreateAddress("audience"), Is.Null);
            Assert.That(state.Trich.Register(state.Outline, state.Outline.Address), Is.Null);
            return state;
        }
    }
}
