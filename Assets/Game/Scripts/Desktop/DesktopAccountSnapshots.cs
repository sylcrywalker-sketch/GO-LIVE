using System;

namespace GoLive.Desktop
{
    [Serializable]
    public sealed class OutlineSnapshot
    {
        public int Version = 1;
        public string Address = "";
        public OutlineMessageSnapshot[] Messages = Array.Empty<OutlineMessageSnapshot>();
        public string[] ReceivedIds = Array.Empty<string>();
    }

    [Serializable]
    public sealed class OutlineMessageSnapshot
    {
        public string Id;
        public string SubjectKey;
        public string BodyKey;
        public string[] BodyArguments = Array.Empty<string>();
        public bool IsRead;
    }

    [Serializable]
    public sealed class TrichSnapshot
    {
        public int Version = 1;
        public string Email = "";
        public string Name = "";
        public string Description = "";
        public int AvatarId;
        public string ChannelCode = "";
        public long CompletedStreams;
        public double TotalDurationSeconds;
        public long TotalFollowers;
        // Added after save version 7 shipped; saves without it load zero paid subscriptions.
        public long TotalSubscriptions;
        public long TotalDonationCents;
        public int PeakViewers;
    }

    [Serializable]
    public sealed class DonationSnapshot
    {
        public int Version = 1;
        public string Name = "";
        public bool AlertsEnabled = true;
        public long TotalCents;
        public DonationReceiptSnapshot[] History = Array.Empty<DonationReceiptSnapshot>();
        public string[] ReceivedIds = Array.Empty<string>();
    }

    [Serializable]
    public sealed class DonationReceiptSnapshot
    {
        public string Id;
        public string SenderName;
        public long AmountCents;
    }
}
