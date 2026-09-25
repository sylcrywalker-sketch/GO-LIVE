using System.Linq;
using GoLive.Desktop;
using NUnit.Framework;

namespace GoLive.Tests
{
    public sealed class DesktopWindowsTests
    {
        [Test]
        public void OneWindowPerAppAndTaskbarKeepsOpenOrderWhenFocusChanges()
        {
            var windows = new DesktopWindows();
            windows.Open(DesktopAppId.Hub);
            windows.Open(DesktopAppId.Outline);
            windows.Open(DesktopAppId.Hub);
            Assert.That(windows.Windows.Select(w => w.AppId), Is.EqualTo(new[] { DesktopAppId.Hub, DesktopAppId.Outline }));
            Assert.That(windows.ActivationOrder, Is.EqualTo(new[] { DesktopAppId.Outline, DesktopAppId.Hub }));
            Assert.That(windows.ActiveApp, Is.EqualTo(DesktopAppId.Hub));
        }

        [Test]
        public void MinimizeKeepsTaskbarEntryAndRestoresPreviouslyVisibleFocus()
        {
            var windows = new DesktopWindows();
            windows.Open(DesktopAppId.Hub);
            windows.Open(DesktopAppId.Outline);
            Assert.That(windows.Minimize(DesktopAppId.Outline), Is.True);
            Assert.That(windows.IsOpen(DesktopAppId.Outline), Is.True);
            Assert.That(windows.IsVisible(DesktopAppId.Outline), Is.False);
            Assert.That(windows.Windows[1].IsMinimized, Is.True);
            Assert.That(windows.ActiveApp, Is.EqualTo(DesktopAppId.Hub));
            windows.Minimize(DesktopAppId.Hub);
            Assert.That(windows.ActiveApp, Is.Null);
            Assert.That(windows.ActivationOrder, Is.Empty);
        }

        [Test]
        public void OpenOrActivateRestoresMinimizedWindowWithoutDuplicatingIt()
        {
            var windows = new DesktopWindows();
            windows.Open(DesktopAppId.Hub);
            windows.Minimize(DesktopAppId.Hub);
            Assert.That(windows.Activate(DesktopAppId.Hub), Is.True);
            Assert.That(windows.IsVisible(DesktopAppId.Hub), Is.True);
            windows.Minimize(DesktopAppId.Hub);
            Assert.That(windows.Open(DesktopAppId.Hub), Is.True);
            Assert.That(windows.ActiveApp, Is.EqualTo(DesktopAppId.Hub));
            Assert.That(windows.Windows.Count, Is.EqualTo(1));
        }

        [Test]
        public void ClosingActiveWindowFocusesMostRecentlyActiveVisibleWindow()
        {
            var windows = new DesktopWindows();
            windows.Open(DesktopAppId.Hub);
            windows.Open(DesktopAppId.Outline);
            windows.Open(DesktopAppId.Streamly);
            windows.Activate(DesktopAppId.Hub);
            windows.Minimize(DesktopAppId.Outline);
            Assert.That(windows.Close(DesktopAppId.Hub), Is.True);
            Assert.That(windows.ActiveApp, Is.EqualTo(DesktopAppId.Streamly));
            windows.Open(DesktopAppId.Hub);
            Assert.That(windows.Windows.Select(w => w.AppId), Is.EqualTo(new[] { DesktopAppId.Outline, DesktopAppId.Streamly, DesktopAppId.Hub }));
        }

        [Test]
        public void NoOpCommandsAndUnknownAppsEmitNoChanges()
        {
            var windows = new DesktopWindows();
            int changes = 0;
            windows.Changed += () => changes++;
            Assert.That(windows.Close(DesktopAppId.Hub), Is.False);
            Assert.That(windows.Minimize(DesktopAppId.Hub), Is.False);
            Assert.That(windows.Activate(DesktopAppId.Hub), Is.False);
            Assert.That(windows.Open((DesktopAppId)99), Is.False);
            windows.Open(DesktopAppId.Hub);
            Assert.That(windows.Open(DesktopAppId.Hub), Is.False);
            Assert.That(windows.Activate(DesktopAppId.Hub), Is.False);
            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void CloseAllClearsTransientStateAndNotifiesOnlyOnActualChange()
        {
            var windows = new DesktopWindows();
            windows.Open(DesktopAppId.Hub);
            windows.Open(DesktopAppId.Outline);
            windows.Minimize(DesktopAppId.Hub);
            int changes = 0;
            windows.Changed += () => changes++;
            windows.CloseAll();
            windows.CloseAll();
            Assert.That(windows.Windows, Is.Empty);
            Assert.That(windows.ActivationOrder, Is.Empty);
            Assert.That(windows.ActiveApp, Is.Null);
            Assert.That(changes, Is.EqualTo(1));
        }
    }
}
