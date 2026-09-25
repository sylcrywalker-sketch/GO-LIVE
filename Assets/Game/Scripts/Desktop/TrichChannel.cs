using System;

namespace GoLive.Desktop
{
    public sealed class TrichChannel
    {
        public string Email { get; private set; } = "";
        public string Name { get; private set; } = "";
        public string Description { get; private set; } = "";
        public int AvatarId { get; private set; }
        public string ChannelCode { get; private set; } = "";
        public bool IsRegistered => Email.Length > 0;
        public long CompletedStreams { get; private set; }
        public double TotalDurationSeconds { get; private set; }
        public long TotalFollowers { get; private set; }
        public long TotalSubscriptions { get; private set; }
        public long TotalDonationCents { get; private set; }
        public int PeakViewers { get; private set; }
        public event Action Changed;
        public string Register(OutlineAccount outline, string email)
        {
            if (IsRegistered) return "desktop.trich.already_registered";
            if (outline == null || !outline.IsCreated) return "desktop.trich.outline_required";
            string normalized = email?.Trim().ToLowerInvariant();
            if (!string.Equals(outline.Address, normalized, StringComparison.Ordinal)) return "desktop.trich.email_mismatch";
            Email = normalized;
            Name = normalized.Substring(0, normalized.IndexOf('@'));
            ChannelCode = Guid.NewGuid().ToString("N");
            Changed?.Invoke();
            return null;
        }

        public string EditProfile(string name, string description, int avatarId)
        {
            if (!IsRegistered) return "desktop.trich.account_required";
            string normalizedName = name?.Trim();
            string normalizedDescription = description?.Trim();
            if (!ValidProfile(normalizedName, normalizedDescription, avatarId)) return "desktop.trich.invalid_profile";
            if (Name == normalizedName && Description == normalizedDescription && AvatarId == avatarId) return null;
            Name = normalizedName;
            Description = normalizedDescription;
            AvatarId = avatarId;
            Changed?.Invoke();
            return null;
        }

        // Completion ownership belongs to the composition root, never to StreamSession.
        // Sequence <= CompletedStreams is an already committed completion, including after save/load.
        public string CompleteStream(StreamSummary summary)
        {
            if (!IsRegistered) return "desktop.trich.account_required";
            if (summary == null || !DesktopAccountValidation.Id(summary.Id) || summary.Sequence <= 0 ||
                !DesktopAccountValidation.FiniteNonnegative(summary.DurationSeconds) || !DesktopAccountValidation.FiniteNonnegative(summary.AverageViewers) ||
                summary.PeakViewers < 0 || summary.Followers < 0 || summary.Subscriptions < 0 || summary.DonationCents < 0)
                return "desktop.trich.invalid_summary";
            if (summary.Sequence <= CompletedStreams) return null;
            if (CompletedStreams == long.MaxValue || summary.Sequence != CompletedStreams + 1) return "desktop.trich.invalid_summary";
            if (TotalFollowers > long.MaxValue - summary.Followers || TotalSubscriptions > long.MaxValue - summary.Subscriptions ||
                TotalDonationCents > long.MaxValue - summary.DonationCents ||
                TotalDurationSeconds > double.MaxValue - summary.DurationSeconds) return "desktop.trich.total_limit";
            CompletedStreams++;
            TotalDurationSeconds += summary.DurationSeconds;
            TotalFollowers += summary.Followers;
            TotalSubscriptions += summary.Subscriptions;
            TotalDonationCents += summary.DonationCents;
            PeakViewers = Math.Max(PeakViewers, summary.PeakViewers);
            Changed?.Invoke();
            return null;
        }

        public TrichSnapshot Capture() => new TrichSnapshot
        {
            Email = Email, Name = Name, Description = Description, AvatarId = AvatarId, ChannelCode = ChannelCode,
            CompletedStreams = CompletedStreams, TotalDurationSeconds = TotalDurationSeconds, TotalFollowers = TotalFollowers,
            TotalSubscriptions = TotalSubscriptions, TotalDonationCents = TotalDonationCents, PeakViewers = PeakViewers
        };

        public static bool Validate(TrichSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Version != 1 || snapshot.Email == null || snapshot.Name == null || snapshot.Description == null ||
                snapshot.ChannelCode == null || snapshot.CompletedStreams < 0 || snapshot.TotalFollowers < 0 || snapshot.TotalSubscriptions < 0 || snapshot.TotalDonationCents < 0 ||
                snapshot.PeakViewers < 0 || !DesktopAccountValidation.FiniteNonnegative(snapshot.TotalDurationSeconds)) return false;
            bool zeroTotals = snapshot.TotalFollowers == 0 && snapshot.TotalSubscriptions == 0 && snapshot.TotalDonationCents == 0 &&
                snapshot.PeakViewers == 0 && snapshot.TotalDurationSeconds == 0;
            if (snapshot.Email.Length == 0)
                return snapshot.Name.Length == 0 && snapshot.Description.Length == 0 && snapshot.AvatarId == 0 && snapshot.ChannelCode.Length == 0 && snapshot.CompletedStreams == 0 && zeroTotals;
            return DesktopAccountValidation.Address(snapshot.Email) && DesktopAccountValidation.Code(snapshot.ChannelCode) &&
                ValidProfile(snapshot.Name, snapshot.Description, snapshot.AvatarId) && (snapshot.CompletedStreams > 0 || zeroTotals);
        }

        public void Restore(TrichSnapshot snapshot)
        {
            if (!Validate(snapshot)) throw new ArgumentException("Invalid Trich snapshot.", nameof(snapshot));
            Email = snapshot.Email;
            Name = snapshot.Name;
            Description = snapshot.Description;
            AvatarId = snapshot.AvatarId;
            ChannelCode = snapshot.ChannelCode;
            CompletedStreams = snapshot.CompletedStreams;
            TotalDurationSeconds = snapshot.TotalDurationSeconds;
            TotalFollowers = snapshot.TotalFollowers;
            TotalSubscriptions = snapshot.TotalSubscriptions;
            TotalDonationCents = snapshot.TotalDonationCents;
            PeakViewers = snapshot.PeakViewers;
            Changed?.Invoke();
        }

        private static bool ValidProfile(string name, string description, int avatarId)
            => DesktopAccountValidation.Text(name, 1, 32) && DesktopAccountValidation.Text(description, 0, 240, true) && avatarId >= 0 && avatarId <= 3;
    }
}
