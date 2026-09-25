using System;
using System.Collections.Generic;
using System.Globalization;
using GoLive.PcBuilding;

namespace GoLive.Desktop
{
    [Serializable]
    public sealed class DesktopSnapshot
    {
        public int Version = 1;
        public DesktopStorageSnapshot Storage = new();
        public OutlineSnapshot Outline = new();
        public TrichSnapshot Trich = new();
        public DonationSnapshot Donation = new();
    }

    // Composition boundary: coordinates completion and atomically validates a durable desktop graph.
    // Each contained domain remains the sole owner of its state; views never own a second copy.
    public sealed class DesktopState : IDisposable
    {
        public DesktopStorage Storage { get; }
        public DesktopWindows Windows { get; } = new();
        public OutlineAccount Outline { get; } = new();
        public TrichChannel Trich { get; } = new();
        public DonationAccount Donation { get; } = new();
        public StreamSession Stream { get; }
        // The player's recognized speech admitted into the live broadcast (transient, never saved).
        public StreamSpeechFeed SpeechFeed { get; }
        public PcPeripherals Peripherals { get; }
        public int RestoreGeneration { get; private set; }
        private readonly IReadOnlyList<DesktopAppDefinition> _apps;
        private bool _restoring;
        public DesktopState(IReadOnlyList<DesktopAppDefinition> apps, PcPeripherals peripherals = null,
            AudienceTuning audienceTuning = null, AudienceRandom audienceSeeds = null)
        {
            _apps = new List<DesktopAppDefinition>(apps).AsReadOnly();
            Storage = new DesktopStorage(apps);
            Peripherals = peripherals ?? new PcPeripherals();
            Stream = new StreamSession(Trich, Donation, Peripherals, audienceTuning, audienceSeeds);
            Stream.Completed += CompleteStream;
            SpeechFeed = new StreamSpeechFeed(Stream);
        }
        public string EnsureSystemApps()
        {
            var pending = new List<(DesktopAppId app, string drive)>();
            var free = new int[Storage.Drives.Count];
            for (int i = 0; i < free.Length; i++) free[i] = Storage.GetFreeMiB(Storage.Drives[i].DriveId);
            foreach (DesktopAppDefinition app in _apps)
            {
                if (!app.Preinstalled || Storage.IsInstalled(app.Id)) continue;
                int target = -1;
                for (int i = 0; i < free.Length; i++)
                    if (Storage.Drives[i].CapacityMiB > 0 && free[i] >= app.SizeMiB) { target = i; break; }
                if (target < 0) return free.Length == 0 ? "desktop.error.drive_unavailable" : "desktop.error.insufficient_storage";
                free[target] -= app.SizeMiB;
                pending.Add((app.Id, Storage.Drives[target].DriveId));
            }
            foreach (var install in pending) Storage.TryInstall(install.app, install.drive);
            return null;
        }
        public DesktopSnapshot Capture() => new()
        {
            Storage = Storage.CaptureSnapshot(), Outline = Outline.Capture(), Trich = Trich.Capture(), Donation = Donation.Capture()
        };
        public string Validate(DesktopSnapshot snapshot, IReadOnlyList<DesktopDrive> knownDrives)
        {
            if (snapshot == null || snapshot.Version != 1) return "Desktop snapshot is missing or unsupported.";
            string storageError = Storage.ValidateSnapshot(snapshot.Storage, knownDrives);
            if (storageError != null) return storageError;
            if (!OutlineAccount.Validate(snapshot.Outline)) return "Invalid Outline snapshot.";
            if (!TrichChannel.Validate(snapshot.Trich)) return "Invalid Trich snapshot.";
            if (!DonationAccount.Validate(snapshot.Donation)) return "Invalid Donation snapshot.";
            if (snapshot.Trich.Email.Length > 0 && snapshot.Trich.Email != snapshot.Outline.Address)
                return "Trich account does not belong to the saved Outline address.";
            return null;
        }
        public void BeginRestore()
        {
            _restoring = true;
            Stream.Reset();
            Windows.CloseAll();
            Storage.SetDrives(Array.Empty<DesktopDrive>());
        }
        public void EndRestore() => _restoring = false;
        public void Restore(DesktopSnapshot snapshot, IReadOnlyList<DesktopDrive> knownDrives)
        {
            string error = Validate(snapshot, knownDrives);
            if (error != null) throw new ArgumentException(error, nameof(snapshot));
            bool alreadyRestoring = _restoring;
            BeginRestore();
            try
            {
                Storage.Restore(snapshot.Storage, knownDrives);
                Outline.Restore(snapshot.Outline);
                Trich.Restore(snapshot.Trich);
                Donation.Restore(snapshot.Donation);
                RestoreGeneration++;
            }
            finally { _restoring = alreadyRestoring; }
        }
        private void CompleteStream(StreamSummary summary)
        {
            if (_restoring || summary.Sequence <= Trich.CompletedStreams) return;
            if (Trich.CompleteStream(summary) != null) return;
            Outline.Receive("stream." + summary.Id, "desktop.mail.stream.subject", "desktop.mail.stream.body_v2",
                Trich.Name, FormatDuration(summary.DurationSeconds),
                summary.AverageViewers.ToString("0.0", CultureInfo.InvariantCulture),
                summary.PeakViewers.ToString(CultureInfo.InvariantCulture), summary.Followers.ToString(CultureInfo.InvariantCulture),
                summary.Subscriptions.ToString(CultureInfo.InvariantCulture),
                (summary.DonationCents / 100d).ToString("0.00", CultureInfo.InvariantCulture));
        }
        internal static string FormatDuration(double seconds)
        {
            double whole = Math.Floor(Math.Min(seconds, 359999));
            int minutes = (int)(whole / 60);
            return minutes >= 60
                ? $"{minutes / 60}:{minutes % 60:00}:{(int)(whole % 60):00}"
                : $"{minutes}:{(int)(whole % 60):00}";
        }
        public void Dispose()
        {
            Stream.Completed -= CompleteStream;
            SpeechFeed.Dispose();
        }
    }
}
