using System;
using System.Collections.Generic;

namespace GoLive.Desktop
{
    public sealed class DesktopDrive
    {
        public string DriveId { get; }
        public string SlotId { get; }
        public int CapacityMiB { get; }
        public string Letter { get; }

        // SlotId may be null in the complete saved-disk inventory, but is mandatory in SetDrives.
        public DesktopDrive(string driveId, string slotId, int capacityMiB)
            : this(driveId, slotId, capacityMiB, null) { }

        internal DesktopDrive(string driveId, string slotId, int capacityMiB, string letter)
        {
            DriveId = driveId;
            SlotId = slotId;
            CapacityMiB = capacityMiB;
            Letter = letter;
        }
    }

    public sealed class DesktopInstalledContent
    {
        public DesktopAppId AppId { get; }
        public string ContentId { get; }
        public string DriveId { get; }
        public int SizeMiB { get; }

        internal DesktopInstalledContent(DesktopAppDefinition definition, string driveId)
        {
            AppId = definition.Id;
            ContentId = definition.ContentId;
            DriveId = driveId;
            SizeMiB = definition.SizeMiB;
        }
    }

    /// <summary>
    /// Owns installed content keyed by permanent physical disk identity. The caller supplies connected
    /// hardware in assembly slot order. Commands are synchronous on the owning game thread; changes
    /// commit before notification. Read projections should be reacquired after Changed.
    /// </summary>
    public sealed class DesktopStorage
    {
        private readonly Dictionary<DesktopAppId, DesktopAppDefinition> _apps = new();
        private readonly Dictionary<string, DesktopAppDefinition> _contentDefinitions = new(StringComparer.Ordinal);
        private Dictionary<string, DesktopDrive> _connected = new(StringComparer.Ordinal);
        private List<DesktopInstalledContent> _installed = new();

        public IReadOnlyList<DesktopDrive> Drives { get; private set; } = Array.Empty<DesktopDrive>();
        public IReadOnlyList<DesktopInstalledContent> InstalledContent { get; private set; }
        public event Action Changed;

        public DesktopStorage(IReadOnlyList<DesktopAppDefinition> definitions)
        {
            string error = DesktopAppCatalog.Validate(definitions);
            if (error != null)
                throw new ArgumentException(error, nameof(definitions));
            foreach (var definition in definitions)
            {
                _apps.Add(definition.Id, definition);
                _contentDefinitions.Add(definition.ContentId, definition);
            }
            InstalledContent = _installed.AsReadOnly();
        }

        /// <summary>Changes connectivity only. Content on removed disks remains owned by those disks.</summary>
        public void SetDrives(IReadOnlyList<DesktopDrive> drives)
        {
            string error = ValidateDrives(drives, true);
            if (error != null)
                throw new ArgumentException(error, nameof(drives));

            bool changed = drives.Count != Drives.Count;
            for (int i = 0; i < drives.Count; i++)
            {
                var drive = drives[i];
                if (GetUsedMiB(drive.DriveId) > drive.CapacityMiB)
                    throw new ArgumentException("Installed content exceeds the connected disk capacity.", nameof(drives));
                if (!changed && (drive.DriveId != Drives[i].DriveId || drive.SlotId != Drives[i].SlotId || drive.CapacityMiB != Drives[i].CapacityMiB))
                    changed = true;
            }
            if (!changed)
                return;

            var connected = new Dictionary<string, DesktopDrive>(StringComparer.Ordinal);
            var projected = new List<DesktopDrive>(drives.Count);
            for (int i = 0; i < drives.Count; i++)
            {
                var drive = drives[i];
                var copy = new DesktopDrive(drive.DriveId, drive.SlotId, drive.CapacityMiB, DriveLetter(i));
                projected.Add(copy);
                connected.Add(copy.DriveId, copy);
            }
            _connected = connected;
            Drives = projected.AsReadOnly();
            Changed?.Invoke();
        }

        public bool IsInstalled(DesktopAppId appId)
        {
            foreach (var content in _installed)
                if (content.AppId == appId && _connected.ContainsKey(content.DriveId))
                    return true;
            return false;
        }

        // A disconnected copy does not block installing on a replacement disk.
        public string TryInstall(DesktopAppId appId, string driveId = null)
        {
            if (!_apps.TryGetValue(appId, out var definition))
                return "desktop.error.unknown_app";
            if (IsInstalled(appId))
                return "desktop.error.already_installed";

            DesktopDrive target = null;
            if (driveId != null)
            {
                if (!_connected.TryGetValue(driveId, out target) || target.CapacityMiB == 0)
                    return "desktop.error.drive_unavailable";
                if (GetFreeMiB(driveId) < definition.SizeMiB)
                    return "desktop.error.insufficient_storage";
            }
            else
            {
                bool hasInstallTarget = false;
                foreach (var drive in Drives)
                {
                    if (drive.CapacityMiB == 0)
                        continue;
                    hasInstallTarget = true;
                    if (GetFreeMiB(drive.DriveId) < definition.SizeMiB)
                        continue;
                    target = drive;
                    break;
                }
                if (target == null)
                    return hasInstallTarget ? "desktop.error.insufficient_storage" : "desktop.error.drive_unavailable";
            }

            _installed.Add(new DesktopInstalledContent(definition, target.DriveId));
            Changed?.Invoke();
            return null;
        }

        /// <summary>Without a disk id, removes the first connected copy in assembly slot order.</summary>
        public string TryUninstall(DesktopAppId appId, string driveId = null)
        {
            if (!_apps.ContainsKey(appId))
                return "desktop.error.unknown_app";
            if (driveId != null && !_connected.ContainsKey(driveId))
                return "desktop.error.drive_unavailable";

            foreach (var drive in Drives)
            {
                if (driveId != null && drive.DriveId != driveId)
                    continue;
                for (int i = 0; i < _installed.Count; i++)
                {
                    if (_installed[i].AppId != appId || _installed[i].DriveId != drive.DriveId)
                        continue;
                    _installed.RemoveAt(i);
                    Changed?.Invoke();
                    return null;
                }
            }
            foreach (var content in _installed)
                if (content.AppId == appId && !_connected.ContainsKey(content.DriveId))
                    return "desktop.error.drive_unavailable";
            return "desktop.error.not_installed";
        }

        public int GetUsedMiB(string driveId)
        {
            // Install/restore/connect validation keeps each physical disk's total within an int capacity.
            int total = 0;
            foreach (var content in _installed)
                if (string.Equals(content.DriveId, driveId, StringComparison.Ordinal))
                    total += content.SizeMiB;
            return total;
        }

        public int GetFreeMiB(string driveId)
            => driveId != null && _connected.TryGetValue(driveId, out var drive) ? drive.CapacityMiB - GetUsedMiB(driveId) : 0;

        public DesktopStorageSnapshot CaptureSnapshot()
        {
            var snapshot = new DesktopStorageSnapshot { InstalledContent = new DesktopInstalledContentSnapshot[_installed.Count] };
            for (int i = 0; i < _installed.Count; i++)
            {
                var content = _installed[i];
                snapshot.InstalledContent[i] = new DesktopInstalledContentSnapshot
                {
                    ContentId = content.ContentId,
                    DriveId = content.DriveId,
                    SizeMiB = content.SizeMiB
                };
            }
            return snapshot;
        }

        /// <summary>
        /// knownDrives must come from the complete saved physical-item inventory, including disconnected
        /// disks. Snapshot sizes are verified against configuration and never define available capacity.
        /// </summary>
        public string ValidateSnapshot(DesktopStorageSnapshot snapshot, IReadOnlyList<DesktopDrive> knownDrives)
        {
            if (snapshot == null || snapshot.Version != DesktopStorageSnapshot.CurrentVersion || snapshot.InstalledContent == null)
                return "The desktop storage snapshot is missing or has an unsupported version.";
            string drivesError = ValidateDrives(knownDrives, false);
            if (drivesError != null)
                return drivesError;

            var capacities = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var drive in knownDrives)
                capacities.Add(drive.DriveId, drive.CapacityMiB);
            var used = new Dictionary<string, long>(StringComparer.Ordinal);
            var identities = new HashSet<(string ContentId, string DriveId)>();
            foreach (var saved in snapshot.InstalledContent)
            {
                if (saved == null || saved.ContentId == null || !_contentDefinitions.TryGetValue(saved.ContentId, out var definition))
                    return "The desktop storage snapshot contains unknown content.";
                if (saved.DriveId == null || !capacities.TryGetValue(saved.DriveId, out int capacity))
                    return "The desktop storage snapshot refers to an unknown physical disk.";
                if (saved.SizeMiB != definition.SizeMiB)
                    return "The desktop storage snapshot size differs from its catalog definition.";
                if (!identities.Add((saved.ContentId, saved.DriveId)))
                    return "The desktop storage snapshot repeats content on the same physical disk.";
                used.TryGetValue(saved.DriveId, out long total);
                total += saved.SizeMiB;
                if (total > capacity)
                    return "The desktop storage snapshot exceeds a physical disk's capacity.";
                used[saved.DriveId] = total;
            }
            return null;
        }

        public void Restore(DesktopStorageSnapshot snapshot, IReadOnlyList<DesktopDrive> knownDrives)
        {
            string error = ValidateSnapshot(snapshot, knownDrives);
            if (error != null)
                throw new ArgumentException(error, nameof(snapshot));
            var restored = new List<DesktopInstalledContent>(snapshot.InstalledContent.Length);
            foreach (var saved in snapshot.InstalledContent)
                restored.Add(new DesktopInstalledContent(_contentDefinitions[saved.ContentId], saved.DriveId));
            _installed = restored;
            InstalledContent = restored.AsReadOnly();
            Changed?.Invoke();
        }

        private static string ValidateDrives(IReadOnlyList<DesktopDrive> drives, bool requireSlots)
        {
            if (drives == null)
                return "Physical storage disks must be supplied.";
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var slots = new HashSet<string>(StringComparer.Ordinal);
            foreach (var drive in drives)
            {
                if (drive == null || string.IsNullOrWhiteSpace(drive.DriveId) || drive.CapacityMiB < 0)
                    return "A physical storage disk has an invalid identity or capacity.";
                if (!ids.Add(drive.DriveId))
                    return "Physical storage disk identities must be unique.";
                if (requireSlots && (string.IsNullOrWhiteSpace(drive.SlotId) || !slots.Add(drive.SlotId)))
                    return "Connected physical disks must occupy unique, named assembly slots.";
            }
            return null;
        }

        private static string DriveLetter(int index)
        {
            string letter = string.Empty;
            for (int value = index + 2; value >= 0; value = value / 26 - 1)
                letter = (char)('A' + value % 26) + letter;
            return letter;
        }
    }
}
