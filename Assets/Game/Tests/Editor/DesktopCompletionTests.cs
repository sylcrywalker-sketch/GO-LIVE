using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GoLive.Desktop;
using GoLive.PcBuilding;
using NUnit.Framework;
using Object = UnityEngine.Object;

namespace GoLive.Tests.Desktop
{
    public sealed class DesktopCompletionTests
    {
        private readonly List<Object> _created = new();
        private PcCapabilities _pc;

        [SetUp]
        public void SetUp() => _pc = StreamSessionTests.CreateCapabilities(_created, true);

        [TearDown]
        public void TearDown()
        {
            foreach (Object item in _created) Object.DestroyImmediate(item);
            _created.Clear();
        }

        [Test]
        public void CompletedStreamCommitsTotalsAndSummaryMailExactlyOnce()
        {
            using var state = RegisteredDesktop();
            var summaries = new List<StreamSummary>();
            state.Stream.Completed += summaries.Add;
            Start(state);
            state.Stream.Tick(60.75f, StreamSessionTests.PrimeTime);
            Assert.That(state.Stream.Stop(), Is.Null);
            state.Stream.Tick(0.25f, StreamSessionTests.PrimeTime);
            state.Stream.Abort();
            state.Stream.Tick(60, StreamSessionTests.PrimeTime);
            Assert.That(summaries.Count, Is.EqualTo(1));
            StreamSummary summary = summaries[0];
            AudienceSimulation audience = state.Stream.Audience;
            // The existing completion pipeline receives the real simulation result.
            Assert.That(summary.PeakViewers, Is.EqualTo(audience.PeakViewers).And.GreaterThan(0));
            Assert.That(summary.AverageViewers, Is.EqualTo(audience.AverageViewers).And.GreaterThan(0));
            Assert.That(summary.Followers, Is.EqualTo(audience.Follows).And.GreaterThan(0));
            Assert.That(summary.Subscriptions, Is.EqualTo(audience.Subscriptions));
            Assert.That(state.Trich.CompletedStreams, Is.EqualTo(1));
            Assert.That(state.Trich.TotalDurationSeconds, Is.EqualTo(60));
            Assert.That(state.Trich.TotalFollowers, Is.EqualTo(summary.Followers));
            Assert.That(state.Trich.TotalSubscriptions, Is.EqualTo(summary.Subscriptions));
            Assert.That(state.Trich.PeakViewers, Is.EqualTo(summary.PeakViewers));
            Assert.That(state.Trich.TotalDonationCents, Is.EqualTo(summary.DonationCents).And.GreaterThan(0));
            Assert.That(state.Donation.TotalCents, Is.EqualTo(summary.DonationCents));
            Assert.That(state.Outline.Messages.Count, Is.EqualTo(1));
            Assert.That(state.Outline.Messages[0].IsRead, Is.False);
            Assert.That(state.Outline.Messages[0].BodyKey, Is.EqualTo("desktop.mail.stream.body_v2"));
            Assert.That(state.Outline.Messages[0].BodyArguments, Is.EqualTo(new[]
            {
                state.Trich.Name, "1:00", summary.AverageViewers.ToString("0.0", CultureInfo.InvariantCulture),
                summary.PeakViewers.ToString(CultureInfo.InvariantCulture), summary.Followers.ToString(CultureInfo.InvariantCulture),
                summary.Subscriptions.ToString(CultureInfo.InvariantCulture), (summary.DonationCents / 100d).ToString("0.00", CultureInfo.InvariantCulture)
            }));
            Assert.That(state.Validate(state.Capture(), Array.Empty<DesktopDrive>()), Is.Null);
        }

        [Test]
        public void LoadingDuringLiveSilentlyDiscardsTransientStateAndKeepsSavedAccounts()
        {
            using var state = RegisteredDesktop();
            var saved = state.Capture();
            string code = state.Trich.ChannelCode;
            int completions = 0;
            state.Stream.Completed += _ => completions++;
            state.Stream.SetQuality(StreamQuality.High);
            Start(state);
            state.Stream.Tick(60.75f, StreamSessionTests.PrimeTime);
            state.Windows.Open(DesktopAppId.Streamly);
            Assert.That(state.Donation.TotalCents, Is.GreaterThan(0));
            state.Restore(saved, Array.Empty<DesktopDrive>());
            state.Stream.Tick(60, StreamSessionTests.PrimeTime);
            Assert.That(completions, Is.Zero);
            Assert.That(state.Stream.State, Is.EqualTo(StreamState.Offline));
            Assert.That(state.Stream.IsConnected, Is.False);
            Assert.That(state.Stream.Quality, Is.EqualTo(StreamQuality.Low));
            Assert.That(state.Viewers.Chat.Messages, Is.Empty, "the live chat is transient and discarded by a load");
            Assert.That(state.Stream.DurationSeconds, Is.Zero);
            Assert.That(state.Windows.Windows, Is.Empty);
            Assert.That(state.Trich.ChannelCode, Is.EqualTo(code));
            Assert.That(state.Trich.CompletedStreams, Is.Zero);
            Assert.That(state.Outline.Messages, Is.Empty);
            Assert.That(state.Donation.TotalCents, Is.Zero);
        }

        [Test]
        public void InvalidRestoreLeavesRunningStreamAndAccountGraphUntouched()
        {
            using var state = RegisteredDesktop();
            Start(state);
            state.Stream.Tick(30.75f, StreamSessionTests.PrimeTime);
            state.Windows.Open(DesktopAppId.Streamly);
            long accepted = state.Stream.DonationCents;
            Assert.That(accepted, Is.GreaterThan(0));
            var corrupt = state.Capture();
            corrupt.Trich.AvatarId = 4;
            int completions = 0;
            state.Stream.Completed += _ => completions++;
            Assert.Throws<ArgumentException>(() => state.Restore(corrupt, Array.Empty<DesktopDrive>()));
            Assert.That(state.Stream.State, Is.EqualTo(StreamState.Live));
            Assert.That(state.Stream.IsConnected, Is.True);
            Assert.That(state.Stream.DurationSeconds, Is.EqualTo(30));
            Assert.That(state.Trich.AvatarId, Is.Zero);
            Assert.That(state.Trich.CompletedStreams, Is.Zero);
            Assert.That(state.Donation.TotalCents, Is.EqualTo(accepted));
            Assert.That(state.Windows.Windows.Count, Is.EqualTo(1));
            Assert.That(completions, Is.Zero);
        }

        [Test]
        public void FinishedStreamRoundTripAllowsNewUniqueCompletionMailAndDonations()
        {
            using var state = RegisteredDesktop();
            Start(state);
            state.Stream.Tick(30.75f, StreamSessionTests.PrimeTime);
            long first = state.Stream.DonationCents;
            state.Stream.Abort();
            int firstReceipts = state.Donation.History.Count;
            state.Restore(state.Capture(), Array.Empty<DesktopDrive>());
            Start(state);
            state.Stream.Tick(30.75f, StreamSessionTests.PrimeTime);
            long second = state.Stream.DonationCents;
            state.Stream.Abort();
            Assert.That(state.Trich.CompletedStreams, Is.EqualTo(2));
            Assert.That(state.Trich.TotalDurationSeconds, Is.EqualTo(60));
            Assert.That(state.Outline.Messages.Count, Is.EqualTo(2));
            Assert.That(state.Outline.Messages.Select(m => m.Id).Distinct().Count(), Is.EqualTo(2));
            Assert.That(first, Is.GreaterThan(0));
            Assert.That(second, Is.GreaterThan(0));
            Assert.That(state.Donation.History.Count, Is.GreaterThan(firstReceipts), "the second broadcast adds new receipts");
            Assert.That(state.Donation.History.Select(d => d.Id).Distinct().Count(), Is.EqualTo(state.Donation.History.Count));
            Assert.That(state.Donation.TotalCents, Is.EqualTo(first + second));
        }

        [Test]
        public void DisposeRemovesOnlyTheRootCompletionSubscription()
        {
            var state = RegisteredDesktop();
            Start(state);
            state.Dispose();
            state.Stream.Tick(30.75f, StreamSessionTests.PrimeTime);
            state.Stream.Abort();
            Assert.That(state.Trich.CompletedStreams, Is.Zero);
            Assert.That(state.Outline.Messages, Is.Empty);
            Assert.That(state.Stream.DonationCents, Is.GreaterThan(0));
            Assert.That(state.Donation.TotalCents, Is.EqualTo(state.Stream.DonationCents), "receipt account remains the owner of accepted activity");
        }

        private void Start(DesktopState state)
        {
            Assert.That(state.Stream.Connect(state.Trich.ChannelCode), Is.Null);
            Assert.That(state.Stream.Start(_pc, true, 6), Is.Null);
        }

        private static DesktopState RegisteredDesktop()
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
            var state = new DesktopState(apps, null, StreamSessionTests.BusyAudience(), new AudienceRandom(4242));
            Assert.That(state.Peripherals.TryConnect(PcPeripheralKind.Microphone, "connected-test-mic"), Is.True);
            Assert.That(state.Outline.CreateAddress("player"), Is.Null);
            Assert.That(state.Trich.Register(state.Outline, state.Outline.Address), Is.Null);
            return state;
        }
    }
}
