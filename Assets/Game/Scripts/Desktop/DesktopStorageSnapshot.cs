using System;

namespace GoLive.Desktop
{
    [Serializable]
    public sealed class DesktopStorageSnapshot
    {
        public const int CurrentVersion = 1;
        public int Version = CurrentVersion;
        public DesktopInstalledContentSnapshot[] InstalledContent = Array.Empty<DesktopInstalledContentSnapshot>();
    }

    // Content identity is independent of drive letters and can accommodate future catalogued content kinds.
    [Serializable]
    public sealed class DesktopInstalledContentSnapshot
    {
        public string ContentId;
        public string DriveId;
        public int SizeMiB;
    }
}
