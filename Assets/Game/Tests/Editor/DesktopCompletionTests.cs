using System;
using System.Collections.Generic;
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
            Start(state);
            state.Stream.Tick(60.75f);
            Assert.That(state.Stream.Stop(), Is.Null);
            state.Stream.Tick(0.25f);
            state.Stream.Abort();
            state.Stream.Tick(60);
            Assert.That(state.Trich.CompletedStreams, Is.EqualTo(1));
            Assert.That(state.Trich.TotalDurationSeconds, Is.EqualTo(60));
            Assert.That(state.Trich.TotalFollowers, Is.EqualTo(4));
            Assert.That(state.Trich.TotalDonationCents, Is.EqualTo(200));
            Assert.That(state.Donation.TotalCents, Is.EqualTo(200));
            Assert.That(state.Outline.Messages.Count, Is.EqualTo(1));
            Assert.That(state.Outline.Messages[0].IsRead, Is.False);
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
            state.Stream.Tick(60.75f);
            state.Windows.Open(DesktopAppId.Streamly);
            Assert.That(state.Donation.TotalCents, Is.GreaterThan(0));
            state.Restore(saved, Array.Empty<DesktopDrive>());
            state.Stream.Tick(60);
            Assert.That(completions, Is.Zero);
            Assert.That(state.Stream.State, Is.EqualTo(StreamState.Offline));
            Assert.That(state.Stream.IsConnected, Is.False);
            Assert.That(state.Stream.Quality, Is.EqualTo(StreamQuality.Low));
            Assert.That(state.Stream.Chat, Is.Empty);
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
            state.Stream.Tick(30.75f);
            state.Windows.Open(DesktopAppId.Streamly);
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
            Assert.That(state.Donation.TotalCents, Is.EqualTo(100));
            Assert.That(state.Windows.Windows.Count, Is.EqualTo(1));
            Assert.That(completions, Is.Zero);
        }

        [Test]
        public void FinishedStreamRoundTripAllowsNewUniqueCompletionMailAndDonations()
        {
            using var state = RegisteredDesktop();
            Start(state);
            state.Stream.Tick(30.75f);
            state.Stream.Abort();
            state.Restore(state.Capture(), Array.Empty<DesktopDrive>());
            Start(state);
            state.Stream.Tick(30.75f);
            state.Stream.Abort();
            Assert.That(state.Trich.CompletedStreams, Is.EqualTo(2));
            Assert.That(state.Trich.TotalDurationSeconds, Is.EqualTo(60));
            Assert.That(state.Outline.Messages.Count, Is.EqualTo(2));
            Assert.That(state.Outline.Messages.Select(m => m.Id).Distinct().Count(), Is.EqualTo(2));
            Assert.That(state.Donation.History.Count, Is.EqualTo(2));
            Assert.That(state.Donation.History.Select(d => d.Id).Distinct().Count(), Is.EqualTo(2));
            Assert.That(state.Donation.TotalCents, Is.EqualTo(200));
        }

        [Test]
        public void DisposeRemovesOnlyTheRootCompletionSubscription()
        {
            var state = RegisteredDesktop();
            Start(state);
            state.Dispose();
            state.Stream.Tick(30.75f);
            state.Stream.Abort();
            Assert.That(state.Trich.CompletedStreams, Is.Zero);
            Assert.That(state.Outline.Messages, Is.Empty);
            Assert.That(state.Donation.TotalCents, Is.EqualTo(100), "receipt account remains the owner of accepted activity");
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
            var state = new DesktopState(apps);
            Assert.That(state.Outline.CreateAddress("player"), Is.Null);
            Assert.That(state.Trich.Register(state.Outline, state.Outline.Address), Is.Null);
            return state;
        }
    }
}
