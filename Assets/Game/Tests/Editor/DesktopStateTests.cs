using System;
using GoLive.Desktop;
using NUnit.Framework;

namespace GoLive.Tests.Desktop
{
    public sealed class DesktopStateTests
    {
        private static DesktopAppDefinition[] Catalog() => new[]
        {
            new DesktopAppDefinition(DesktopAppId.MyComputer, "computer", "computer.desc", null, 20, true),
            new DesktopAppDefinition(DesktopAppId.Hub, "hub", "hub.desc", null, 40, true),
            new DesktopAppDefinition(DesktopAppId.Web, "web", "web.desc", null, 40, true),
            new DesktopAppDefinition(DesktopAppId.Outline, "outline", "outline.desc", null, 50, false),
            new DesktopAppDefinition(DesktopAppId.Trich, "trich", "trich.desc", null, 50, false),
            new DesktopAppDefinition(DesktopAppId.Streamly, "streamly", "streamly.desc", null, 50, false),
            new DesktopAppDefinition(DesktopAppId.Donation, "donation", "donation.desc", null, 50, false)
        };

        [Test]
        public void BootstrapInsufficientSpaceDoesNotPartiallyInstall()
        {
            using var state = new DesktopState(Catalog());
            state.Storage.SetDrives(new[] { new DesktopDrive("tiny", "storage-0", 60) });
            Assert.That(state.EnsureSystemApps(), Is.Not.Null);
            Assert.That(state.Storage.InstalledContent, Is.Empty);
        }

        [Test]
        public void ReplacementDriveGetsSystemAppsAndRetainsDisconnectedContent()
        {
            using var state = new DesktopState(Catalog());
            state.Storage.SetDrives(new[] { new DesktopDrive("first", "storage-0", 500) });
            Assert.That(state.EnsureSystemApps(), Is.Null);
            Assert.That(state.Storage.TryInstall(DesktopAppId.Outline), Is.Null);
            state.Storage.SetDrives(new[] { new DesktopDrive("second", "storage-0", 500) });
            Assert.That(state.EnsureSystemApps(), Is.Null);
            Assert.That(state.Storage.IsInstalled(DesktopAppId.Hub), Is.True);
            Assert.That(state.Storage.IsInstalled(DesktopAppId.Outline), Is.False);
            Assert.That(state.Storage.InstalledContent.Count, Is.EqualTo(7));
        }

        [Test]
        public void MismatchedEmailRejectsWholeGraphBeforeMutation()
        {
            using var state = new DesktopState(Catalog());
            state.Outline.CreateAddress("original");
            var before = state.Outline.Address;
            using var other = new DesktopState(Catalog());
            other.Outline.CreateAddress("other");
            other.Trich.Register(other.Outline, other.Outline.Address);
            var snapshot = other.Capture();
            snapshot.Trich.Email = "foreign@outline.mail";
            Assert.That(state.Validate(snapshot, Array.Empty<DesktopDrive>()), Is.Not.Null);
            Assert.Throws<ArgumentException>(() => state.Restore(snapshot, Array.Empty<DesktopDrive>()));
            Assert.That(state.Outline.Address, Is.EqualTo(before));
        }

        [Test]
        public void RestoreClosesTransientWindowsAndPreservesAccounts()
        {
            using var state = new DesktopState(Catalog());
            state.Outline.CreateAddress("player");
            state.Trich.Register(state.Outline, state.Outline.Address);
            state.Windows.Open(DesktopAppId.Hub);
            var snapshot = state.Capture();
            var code = state.Trich.ChannelCode;
            state.Restore(snapshot, Array.Empty<DesktopDrive>());
            Assert.That(state.Windows.Windows, Is.Empty);
            Assert.That(state.Outline.Address, Is.Not.Empty);
            Assert.That(state.Trich.ChannelCode, Is.EqualTo(code));
        }
    }
}
