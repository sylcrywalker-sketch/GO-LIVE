using System;
using System.Linq;
using GoLive.Desktop;
using NUnit.Framework;

namespace GoLive.Tests
{
    public sealed class DesktopAccountTests
    {
        [TestCase(" ab ")]
        [TestCase(".user")]
        [TestCase("user_")]
        [TestCase("имя")]
        [TestCase("<b>player</b>")]
        [TestCase("a b")]
        [TestCase("abcdefghijklmnopqrstuvwxyz")]
        public void InvalidOutlineNameDoesNotCreateAddress(string name)
        {
            var account = new OutlineAccount();
            Assert.That(account.CreateAddress(name), Is.EqualTo("desktop.outline.invalid_username"));
            Assert.That(account.IsCreated, Is.False);
        }

        [Test]
        public void AddressNormalizesAndCannotBeOverwritten()
        {
            var account = new OutlineAccount();
            int changes = 0;
            account.Changed += () => changes++;
            Assert.That(account.CreateAddress(" Player.Name "), Is.Null);
            Assert.That(account.Address, Is.EqualTo("player.name@outline.local"));
            Assert.That(account.CreateAddress("another"), Is.EqualTo("desktop.outline.already_created"));
            Assert.That(account.Address, Is.EqualTo("player.name@outline.local"));
            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void MailIsIdempotentOwnedAndIndividuallyReadable()
        {
            var account = CreatedOutline();
            string[] args = { "42" };
            Assert.That(account.Receive("first", "desktop.mail.summary.subject", "desktop.mail.summary.body", args), Is.Null);
            args[0] = "changed";
            Assert.That(account.Messages[0].BodyArguments[0], Is.EqualTo("42"));
            Assert.That(account.Receive("first", "desktop.mail.summary.subject", "desktop.mail.summary.body"), Is.Null);
            Assert.That(account.Receive("second", "desktop.mail.summary.subject", "desktop.mail.summary.body"), Is.Null);
            Assert.That(account.MarkRead("first"), Is.Null);
            Assert.That(account.Messages.Count, Is.EqualTo(2));
            Assert.That(account.Messages[0].IsRead, Is.True);
            Assert.That(account.Messages[1].IsRead, Is.False);
            Assert.That(account.MarkRead("absent"), Is.EqualTo("desktop.outline.message_missing"));
        }

        [Test]
        public void MailHistoryIsBoundedButEvictedIdsCannotReappearAfterRestore()
        {
            var account = CreatedOutline();
            for (int i = 0; i <= OutlineAccount.MaximumMessages; i++)
                Assert.That(account.Receive("mail-" + i, "desktop.mail.summary.subject", "desktop.mail.summary.body"), Is.Null);
            Assert.That(account.Messages.Count, Is.EqualTo(OutlineAccount.MaximumMessages));
            var restored = new OutlineAccount();
            restored.Restore(account.Capture());
            Assert.That(restored.Receive("mail-0", "desktop.mail.summary.subject", "desktop.mail.summary.body"), Is.Null);
            Assert.That(restored.Messages.Any(m => m.Id == "mail-0"), Is.False);
        }

        [Test]
        public void OutlineRestoreRejectsDuplicateIdsAndLeavesExistingStateUntouched()
        {
            var account = CreatedOutline();
            account.Receive("mail-1", "desktop.mail.summary.subject", "desktop.mail.summary.body");
            var snapshot = account.Capture();
            snapshot.Address = "other@outline.local";
            snapshot.Messages = new[] { snapshot.Messages[0], snapshot.Messages[0] };
            Assert.That(OutlineAccount.Validate(snapshot), Is.False);
            Assert.Throws<ArgumentException>(() => account.Restore(snapshot));
            Assert.That(account.Address, Is.EqualTo("player@outline.local"));
            Assert.That(account.Messages.Count, Is.EqualTo(1));
        }

        [Test]
        public void OutlineRequiresCanonicalAddressAndCompleteIdLedger()
        {
            var account = CreatedOutline();
            account.Receive("mail-1", "desktop.mail.summary.subject", "desktop.mail.summary.body");
            var snapshot = account.Capture();
            snapshot.Address = "PLAYER@outline.local";
            Assert.That(OutlineAccount.Validate(snapshot), Is.False);
            snapshot.Address = account.Address;
            snapshot.ReceivedIds = Array.Empty<string>();
            Assert.That(OutlineAccount.Validate(snapshot), Is.False);
        }

        [Test]
        public void TrichRequiresTheExactCreatedOutlineAddressAndKeepsItsCode()
        {
            var outline = new OutlineAccount();
            var channel = new TrichChannel();
            Assert.That(channel.Register(outline, "player@outline.local"), Is.EqualTo("desktop.trich.outline_required"));
            outline.CreateAddress("player");
            Assert.That(channel.Register(outline, "other@outline.local"), Is.EqualTo("desktop.trich.email_mismatch"));
            Assert.That(channel.Register(outline, " PLAYER@OUTLINE.LOCAL "), Is.Null);
            string code = channel.ChannelCode;
            Assert.That(code.Length, Is.EqualTo(32));
            Assert.That(channel.Register(outline, outline.Address), Is.EqualTo("desktop.trich.already_registered"));
            var restored = new TrichChannel();
            restored.Restore(channel.Capture());
            Assert.That(restored.ChannelCode, Is.EqualTo(code));
        }

        [TestCase("<b>name</b>", "text", 0)]
        [TestCase("name", "<size=9>text</size>", 0)]
        [TestCase("", "text", 0)]
        [TestCase("name", "text", 4)]
        public void InvalidProfileIsAtomic(string name, string description, int avatar)
        {
            var channel = RegisteredChannel();
            string originalName = channel.Name;
            Assert.That(channel.EditProfile(name, description, avatar), Is.EqualTo("desktop.trich.invalid_profile"));
            Assert.That(channel.Name, Is.EqualTo(originalName));
            Assert.That(channel.Description, Is.Empty);
            Assert.That(channel.AvatarId, Is.Zero);
        }

        [Test]
        public void ProfileAndTotalsRoundTripWithSummaryIdempotence()
        {
            var channel = RegisteredChannel();
            Assert.That(channel.EditProfile("Live Player", "Daily games", 3), Is.Null);
            var first = new StreamSummary("stream-1", 1, 35, 20, 7.5, 4, 2, 125, false);
            Assert.That(channel.CompleteStream(first), Is.Null);
            Assert.That(channel.CompleteStream(first), Is.Null);
            var second = new StreamSummary("stream-2", 2, 10, 5, 2.25, 1, 1, 50, true);
            Assert.That(channel.CompleteStream(second), Is.Null);
            Assert.That(channel.CompleteStream(first), Is.Null);
            var restored = new TrichChannel();
            restored.Restore(channel.Capture());
            Assert.That(restored.CompletedStreams, Is.EqualTo(2));
            Assert.That(restored.TotalDurationSeconds, Is.EqualTo(45));
            Assert.That(restored.TotalFollowers, Is.EqualTo(5));
            Assert.That(restored.TotalSubscriptions, Is.EqualTo(3), "paid subscriptions apply once per summary");
            Assert.That(restored.TotalDonationCents, Is.EqualTo(175));
            Assert.That(restored.PeakViewers, Is.EqualTo(20));
            Assert.That(restored.Name, Is.EqualTo("Live Player"));
            Assert.That(restored.AvatarId, Is.EqualTo(3));
        }

        [Test]
        public void ChannelRejectsNonfiniteCounterAndUnknownAvatarWithoutPartialRestore()
        {
            var channel = RegisteredChannel();
            string code = channel.ChannelCode;
            var snapshot = channel.Capture();
            snapshot.TotalDurationSeconds = double.NaN;
            Assert.That(TrichChannel.Validate(snapshot), Is.False);
            Assert.Throws<ArgumentException>(() => channel.Restore(snapshot));
            snapshot.TotalDurationSeconds = 0;
            snapshot.AvatarId = 4;
            Assert.That(TrichChannel.Validate(snapshot), Is.False);
            Assert.That(channel.ChannelCode, Is.EqualTo(code));
            Assert.That(channel.TotalDurationSeconds, Is.Zero);
        }

        [Test]
        public void UnregisteredChannelCannotHideProfileOrTotalsInSnapshot()
        {
            Assert.That(TrichChannel.Validate(new TrichSnapshot()), Is.True);
            Assert.That(TrichChannel.Validate(new TrichSnapshot { TotalFollowers = 1 }), Is.False);
            Assert.That(TrichChannel.Validate(new TrichSnapshot { TotalSubscriptions = 1 }), Is.False);
            Assert.That(TrichChannel.Validate(new TrichSnapshot { ChannelCode = new string('a', 32) }), Is.False);
        }

        [Test]
        public void DonationEventsAreIdempotentAcrossHistoryEvictionAndRoundTrip()
        {
            var account = new DonationAccount();
            Assert.That(account.Configure("Player", false), Is.Null);
            for (int i = 0; i <= DonationAccount.MaximumHistory; i++)
                Assert.That(account.Receive("receipt-" + i, "Viewer", 100), Is.Null);
            Assert.That(account.History.Count, Is.EqualTo(DonationAccount.MaximumHistory));
            var restored = new DonationAccount();
            restored.Restore(account.Capture());
            Assert.That(restored.Receive("receipt-0", "Viewer", 100), Is.Null);
            Assert.That(restored.TotalCents, Is.EqualTo(10100));
            Assert.That(restored.AlertsEnabled, Is.False);
            Assert.That(restored.Name, Is.EqualTo("Player"));
        }

        [Test]
        public void DonationRejectsNegativeOverflowAndCorruptSnapshotsAtomically()
        {
            var account = new DonationAccount();
            Assert.That(account.Receive("bad", "Viewer", -1), Is.EqualTo("desktop.donation.invalid_receipt"));
            Assert.That(account.Receive("first", "Viewer", 100), Is.Null);
            var snapshot = account.Capture();
            snapshot.TotalCents = 99;
            Assert.That(DonationAccount.Validate(snapshot), Is.False);
            Assert.Throws<ArgumentException>(() => account.Restore(snapshot));
            Assert.That(account.TotalCents, Is.EqualTo(100));
            Assert.That(account.Receive("overflow", "Viewer", long.MaxValue), Is.EqualTo("desktop.donation.total_limit"));
            Assert.That(account.TotalCents, Is.EqualTo(100));
        }

        internal static OutlineAccount CreatedOutline()
        {
            var outline = new OutlineAccount();
            Assert.That(outline.CreateAddress("player"), Is.Null);
            return outline;
        }

        internal static TrichChannel RegisteredChannel()
        {
            var outline = CreatedOutline();
            var channel = new TrichChannel();
            Assert.That(channel.Register(outline, outline.Address), Is.Null);
            return channel;
        }
    }
}
