using System;
using System.Linq;
using GoLive.Desktop;
using GoLive.PcBuilding;
using NUnit.Framework;
using UnityEditor;

namespace GoLive.Tests
{
    public sealed class DesktopStorageTests
    {
        [Test]
        public void CatalogRequiresAllSevenUniqueKnownAppsAndValidMetadata()
        {
            var definitions = Definitions();
            Assert.That(DesktopAppCatalog.Validate(definitions), Is.Null);
            Assert.That(DesktopAppCatalog.Validate(definitions.Take(6).ToArray()), Is.Not.Null);
            definitions[6] = definitions[0];
            Assert.That(DesktopAppCatalog.Validate(definitions), Is.Not.Null);
            definitions = Definitions();
            definitions[6] = new DesktopAppDefinition((DesktopAppId)99, "name", "description", null, 10, false);
            Assert.That(DesktopAppCatalog.Validate(definitions), Is.Not.Null);
            definitions[6] = new DesktopAppDefinition(DesktopAppId.Web, "", "description", null, 10, true);
            Assert.That(DesktopAppCatalog.Validate(definitions), Is.Not.Null);
            definitions[6] = new DesktopAppDefinition(DesktopAppId.Web, "name", "description", null, 0, true);
            Assert.That(DesktopAppCatalog.Validate(definitions), Is.Not.Null);
        }

        [Test]
        public void StorageRejectsInvalidCatalogBeforeAcceptingCommands()
        {
            Assert.Throws<ArgumentException>(() => new DesktopStorage(Definitions().Take(6).ToArray()));
        }

        [Test]
        public void DriveLettersFollowSuppliedAssemblySlotOrder()
        {
            var storage = Storage();
            storage.SetDrives(new[] { Drive("second", "storage-2", 200), Drive("first", "storage-0", 300) });
            Assert.That(storage.Drives.Select(d => d.DriveId), Is.EqualTo(new[] { "second", "first" }));
            Assert.That(storage.Drives.Select(d => d.Letter), Is.EqualTo(new[] { "C", "D" }));
            Assert.That(storage.Drives.Select(d => d.CapacityMiB), Is.EqualTo(new[] { 200, 300 }));
        }

        [Test]
        public void PreinstalledAppsUseTheSameRealCapacityAsDownloadedApps()
        {
            var storage = Storage();
            storage.SetDrives(new[] { Drive("hdd", "storage-0", 100) });
            foreach (var app in Definitions().Where(d => d.Preinstalled))
                Assert.That(storage.TryInstall(app.Id), Is.Null);
            Assert.That(storage.GetUsedMiB("hdd"), Is.EqualTo(30));
            Assert.That(storage.GetFreeMiB("hdd"), Is.EqualTo(70));
        }

        [Test]
        public void InstallUsesSelectedPhysicalDiskAndUninstallReleasesItsSpace()
        {
            var storage = Storage();
            storage.SetDrives(new[] { Drive("hdd", "storage-0", 100), Drive("ssd", "storage-1", 80) });
            int changes = 0;
            storage.Changed += () => changes++;
            Assert.That(storage.TryInstall(DesktopAppId.Streamly, "ssd"), Is.Null);
            Assert.That(storage.IsInstalled(DesktopAppId.Streamly), Is.True);
            Assert.That(storage.InstalledContent.Single().DriveId, Is.EqualTo("ssd"));
            Assert.That(storage.GetUsedMiB("hdd"), Is.Zero);
            Assert.That(storage.GetFreeMiB("ssd"), Is.EqualTo(40));
            Assert.That(storage.TryUninstall(DesktopAppId.Streamly), Is.Null);
            Assert.That(storage.IsInstalled(DesktopAppId.Streamly), Is.False);
            Assert.That(storage.GetFreeMiB("ssd"), Is.EqualTo(80));
            Assert.That(changes, Is.EqualTo(2));
        }

        [Test]
        public void InsufficientSpaceCannotSplitOneAppAcrossDisksOrMutateAnything()
        {
            var storage = Storage();
            storage.SetDrives(new[] { Drive("a", "storage-0", 30), Drive("b", "storage-1", 30) });
            int changes = 0;
            storage.Changed += () => changes++;
            Assert.That(storage.TryInstall(DesktopAppId.Streamly), Is.EqualTo("desktop.error.insufficient_storage"));
            Assert.That(storage.InstalledContent, Is.Empty);
            Assert.That(changes, Is.Zero);
        }

        [Test]
        public void DefaultInstallationChoosesFirstConnectedDriveWithEnoughSpace()
        {
            var storage = Storage();
            storage.SetDrives(new[] { Drive("legacy", "storage-0", 0), Drive("small", "storage-1", 30), Drive("fit", "storage-2", 40) });
            Assert.That(storage.TryInstall(DesktopAppId.Streamly), Is.Null);
            Assert.That(storage.InstalledContent.Single().DriveId, Is.EqualTo("fit"));
            Assert.That(storage.GetFreeMiB("fit"), Is.Zero);
        }

        [Test]
        public void UnplugMakesAppsUnavailableAndOnlySamePhysicalDiskRestoresThem()
        {
            var storage = Storage();
            storage.SetDrives(new[] { Drive("original", "storage-0", 100) });
            storage.TryInstall(DesktopAppId.Streamly);
            storage.SetDrives(new[] { Drive("replacement", "storage-0", 100) });
            Assert.That(storage.IsInstalled(DesktopAppId.Streamly), Is.False);
            Assert.That(storage.InstalledContent.Single().DriveId, Is.EqualTo("original"));
            Assert.That(storage.GetUsedMiB("replacement"), Is.Zero);
            Assert.That(storage.TryUninstall(DesktopAppId.Streamly), Is.EqualTo("desktop.error.drive_unavailable"));
            storage.SetDrives(new[] { Drive("replacement", "storage-0", 100), Drive("original", "storage-1", 100) });
            Assert.That(storage.IsInstalled(DesktopAppId.Streamly), Is.True);
            Assert.That(storage.Drives[1].Letter, Is.EqualTo("D"));
            Assert.That(storage.GetUsedMiB("original"), Is.EqualTo(40));
        }

        [Test]
        public void ReplacementDiskCanInstallAppsWhileOldDiskRetainsItsOwnCopy()
        {
            var storage = Storage();
            storage.SetDrives(new[] { Drive("original", "storage-0", 100) });
            storage.TryInstall(DesktopAppId.Hub);
            storage.SetDrives(new[] { Drive("replacement", "storage-0", 100) });
            Assert.That(storage.TryInstall(DesktopAppId.Hub), Is.Null);
            Assert.That(storage.InstalledContent.Count, Is.EqualTo(2));
            Assert.That(storage.ValidateSnapshot(storage.CaptureSnapshot(), new[] { Drive("original", null, 100), Drive("replacement", null, 100) }), Is.Null);
            storage.SetDrives(new[] { Drive("original", "storage-0", 100), Drive("replacement", "storage-1", 100) });
            Assert.That(storage.IsInstalled(DesktopAppId.Hub), Is.True);
            Assert.That(storage.TryInstall(DesktopAppId.Hub), Is.EqualTo("desktop.error.already_installed"));
        }

        [Test]
        public void UnknownAppsMissingDrivesAndRepeatedInstallDoNotMutate()
        {
            var storage = Storage();
            Assert.That(storage.TryInstall(DesktopAppId.Streamly), Is.EqualTo("desktop.error.drive_unavailable"));
            storage.SetDrives(new[] { Drive("hdd", "storage-0", 100) });
            Assert.That(storage.TryInstall((DesktopAppId)99), Is.EqualTo("desktop.error.unknown_app"));
            Assert.That(storage.TryInstall(DesktopAppId.Streamly, "unknown"), Is.EqualTo("desktop.error.drive_unavailable"));
            Assert.That(storage.TryInstall(DesktopAppId.Streamly), Is.Null);
            Assert.That(storage.TryInstall(DesktopAppId.Streamly), Is.EqualTo("desktop.error.already_installed"));
            Assert.That(storage.InstalledContent.Count, Is.EqualTo(1));
        }

        [Test]
        public void DuplicateDrivesSlotsAndCapacityShrinkAreRejectedAtomically()
        {
            var storage = Storage();
            storage.SetDrives(new[] { Drive("hdd", "storage-0", 100) });
            storage.TryInstall(DesktopAppId.Streamly);
            Assert.Throws<ArgumentException>(() => storage.SetDrives(new[] { Drive("hdd", "storage-0", 100), Drive("hdd", "storage-1", 100) }));
            Assert.Throws<ArgumentException>(() => storage.SetDrives(new[] { Drive("a", "storage-0", 100), Drive("b", "storage-0", 100) }));
            Assert.Throws<ArgumentException>(() => storage.SetDrives(new[] { Drive("hdd", "storage-0", 39) }));
            Assert.That(storage.Drives.Single().CapacityMiB, Is.EqualTo(100));
            Assert.That(storage.IsInstalled(DesktopAppId.Streamly), Is.True);
        }

        [Test]
        public void CaptureIsDetachedAndRestoreRetainsDisconnectedDiskContent()
        {
            var storage = Storage();
            storage.SetDrives(new[] { Drive("hdd", "storage-0", 100) });
            storage.TryInstall(DesktopAppId.Streamly);
            var snapshot = storage.CaptureSnapshot();
            snapshot.InstalledContent[0].SizeMiB = 1;
            Assert.That(storage.GetUsedMiB("hdd"), Is.EqualTo(40));
            var restored = Storage();
            restored.Restore(storage.CaptureSnapshot(), new[] { Drive("hdd", null, 100) });
            Assert.That(restored.IsInstalled(DesktopAppId.Streamly), Is.False);
            restored.SetDrives(new[] { Drive("hdd", "storage-1", 100) });
            Assert.That(restored.IsInstalled(DesktopAppId.Streamly), Is.True);
            Assert.That(restored.GetUsedMiB("hdd"), Is.EqualTo(40));
        }

        [TestCase("unknown-content")]
        [TestCase("unknown-disk")]
        [TestCase("wrong-size")]
        [TestCase("duplicate")]
        [TestCase("over-capacity")]
        [TestCase("null-record")]
        [TestCase("null-list")]
        [TestCase("version")]
        public void CorruptSnapshotIsRejectedWithoutReplacingLiveState(string corruption)
        {
            var storage = Storage();
            storage.SetDrives(new[] { Drive("hdd", "storage-0", 100) });
            storage.TryInstall(DesktopAppId.Streamly);
            var snapshot = storage.CaptureSnapshot();
            var known = new[] { Drive("hdd", null, 100) };
            switch (corruption)
            {
                case "unknown-content": snapshot.InstalledContent[0].ContentId = "future.unknown"; break;
                case "unknown-disk": snapshot.InstalledContent[0].DriveId = "forged"; break;
                case "wrong-size": snapshot.InstalledContent[0].SizeMiB = 1; break;
                case "duplicate": snapshot.InstalledContent = new[] { snapshot.InstalledContent[0], snapshot.InstalledContent[0] }; break;
                case "over-capacity": known[0] = Drive("hdd", null, 39); break;
                case "null-record": snapshot.InstalledContent[0] = null; break;
                case "null-list": snapshot.InstalledContent = null; break;
                case "version": snapshot.Version = 99; break;
            }
            int changes = 0;
            storage.Changed += () => changes++;
            Assert.That(storage.ValidateSnapshot(snapshot, known), Is.Not.Null);
            Assert.Throws<ArgumentException>(() => storage.Restore(snapshot, known));
            Assert.That(storage.IsInstalled(DesktopAppId.Streamly), Is.True);
            Assert.That(storage.GetUsedMiB("hdd"), Is.EqualTo(40));
            Assert.That(changes, Is.Zero);
        }

        [Test]
        public void KnownSavedDisksMustHaveUniqueIdentityAndValidCapacity()
        {
            var storage = Storage();
            var empty = new DesktopStorageSnapshot();
            Assert.That(storage.ValidateSnapshot(empty, new[] { Drive("a", null, 100), Drive("a", null, 100) }), Is.Not.Null);
            Assert.That(storage.ValidateSnapshot(empty, new[] { Drive("a", null, -1) }), Is.Not.Null);
            Assert.That(storage.ValidateSnapshot(empty, new[] { Drive("", null, 100) }), Is.Not.Null);
        }

        [Test]
        public void StorageCapacityIsStorageOnlyWhileLegacyZeroAndFourArgumentApiRemainValid()
        {
            Assert.That(PcComponentSpec.Validate(PcComponentType.Storage, PcConnector.SataStorage, 6, 0), Is.Null);
            Assert.That(PcComponentSpec.Validate(PcComponentType.Storage, PcConnector.SataStorage, 6, 0, 0), Is.Null);
            Assert.That(PcComponentSpec.Validate(PcComponentType.Storage, PcConnector.SataStorage, 6, 0, 327680), Is.Null);
            Assert.That(PcComponentSpec.Validate(PcComponentType.Storage, PcConnector.SataStorage, 6, 0, -1), Is.Not.Null);
            Assert.That(PcComponentSpec.Validate(PcComponentType.Ram, PcConnector.MemorySlot, 6, 0, 1), Is.Not.Null);
            var starter = AssetDatabase.LoadAssetAtPath<PcComponentSpec>("Assets/Game/Scripts/PcBuilding/Config/PcComponent_StarterHdd.asset");
            Assert.That(starter.StorageCapacityMiB, Is.EqualTo(327680));
        }

        internal static DesktopAppDefinition[] Definitions() => Enum.GetValues(typeof(DesktopAppId)).Cast<DesktopAppId>()
            .Select(id => new DesktopAppDefinition(id, "desktop.app." + id + ".name", "desktop.app." + id + ".description", null,
                id == DesktopAppId.Streamly ? 40 : 10, id == DesktopAppId.MyComputer || id == DesktopAppId.Hub || id == DesktopAppId.Web)).ToArray();

        private static DesktopStorage Storage() => new(Definitions());
        private static DesktopDrive Drive(string id, string slot, int capacity) => new(id, slot, capacity);
    }
}
