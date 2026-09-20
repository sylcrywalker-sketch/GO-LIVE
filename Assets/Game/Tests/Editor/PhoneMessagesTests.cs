using System;
using System.Collections.Generic;
using System.Linq;
using GoLive.GameTime;
using GoLive.Phone;
using NUnit.Framework;
using UnityEngine;

namespace GoLive.Tests
{
    public sealed class PhoneMessagesTests
    {
        private static MessageContent Text(string value = "Мне нужны деньги...") => MessageContent.FromText(value);
        private static GameTimeSnapshot Time(long seconds = 25920) => new(seconds);

        [Test]
        public void IncomingIncreasesConversationAndTotalUnread()
        {
            var messages = new PhoneMessages();
            Assert.That(messages.TryAddIncoming("m1", "landlord", Text(), Time()), Is.True);
            Assert.That(messages.TryGetConversation("landlord", out var conversation), Is.True);
            Assert.That(conversation.ContactId, Is.EqualTo("landlord"));
            Assert.That(conversation.UnreadCount, Is.EqualTo(1));
            Assert.That(messages.TotalUnreadCount, Is.EqualTo(1));
            Assert.That(conversation.Messages[0].Direction, Is.EqualTo(MessageDirection.Incoming));
            Assert.That(conversation.Messages[0].IsRead, Is.False);
        }

        [Test]
        public void MarkingConversationReadClearsOnlyThatConversation()
        {
            var messages = new PhoneMessages();
            messages.TryAddIncoming("m1", "landlord", Text(), Time());
            messages.TryAddIncoming("m2", "landlord", Text(), Time());
            messages.TryAddIncoming("m3", "friend", Text(), Time());
            Assert.That(messages.MarkConversationRead("landlord"), Is.True);
            messages.TryGetConversation("landlord", out var conversation);
            Assert.That(conversation.UnreadCount, Is.Zero);
            Assert.That(conversation.Messages.All(message => message.IsRead), Is.True);
            Assert.That(messages.TotalUnreadCount, Is.EqualTo(1));
            Assert.That(messages.MarkConversationRead("landlord"), Is.False);
            messages.TryAddIncoming("m4", "landlord", Text(), Time());
            Assert.That(conversation.UnreadCount, Is.EqualTo(1));
        }

        [Test]
        public void OutgoingCanBeAddedAtAnyTimeAndNeverCreatesPlayerUnread()
        {
            var messages = new PhoneMessages();
            messages.TryAddIncoming("m1", "landlord", Text(), Time());
            messages.MarkConversationRead("landlord");
            Assert.That(messages.TryAddOutgoing("reply1", "landlord", Text("Заплачу завтра."), Time(27000)), Is.True);
            messages.TryGetConversation("landlord", out var conversation);
            Assert.That(conversation.Messages[1].Direction, Is.EqualTo(MessageDirection.Outgoing));
            Assert.That(conversation.Messages[1].IsRead, Is.True);
            Assert.That(conversation.Messages[1].Content.Value, Is.EqualTo("Заплачу завтра."));
            Assert.That(messages.TotalUnreadCount, Is.Zero);
            Assert.That(messages.TryAddOutgoing("reply2", "new-contact", Text("Привет!"), Time()), Is.True);
            Assert.That(messages.Conversations.Count, Is.EqualTo(2));
            Assert.That(messages.TotalUnreadCount, Is.Zero);
        }

        [Test]
        public void UnreadTotalsSumMultipleContacts()
        {
            var messages = new PhoneMessages();
            messages.TryAddIncoming("a", "landlord", Text(), Time());
            messages.TryAddIncoming("b", "landlord", Text(), Time());
            messages.TryAddIncoming("c", "friend", Text(), Time());
            messages.TryAddOutgoing("d", "friend", Text(), Time());
            Assert.That(messages.TotalUnreadCount, Is.EqualTo(3));
            Assert.That(messages.Conversations.Select(conversation => conversation.UnreadCount), Is.EqualTo(new[] { 2, 1 }));
        }

        [Test]
        public void MessagesKeepAppendOrderIncludingEqualAndEarlierTimestamps()
        {
            var messages = new PhoneMessages();
            messages.TryAddIncoming("z", "landlord", Text(), Time(50));
            messages.TryAddOutgoing("a", "landlord", Text(), Time(50));
            messages.TryAddIncoming("b", "landlord", Text(), Time(20));
            Assert.That(messages.Conversations[0].Messages.Select(message => message.MessageId), Is.EqualTo(new[] { "z", "a", "b" }));
        }

        [Test]
        public void TimestampUsesExistingGameClockAndDoesNotFollowLaterClockChanges()
        {
            var clock = new GameClock(3, 7, 12);
            var messages = new PhoneMessages();
            messages.TryAddIncoming("a", "landlord", Text(), clock.Current);
            clock.AdvanceMinutes(2);
            var timestamp = messages.Conversations[0].Messages[0].Timestamp;
            Assert.That(timestamp.Day, Is.EqualTo(3));
            Assert.That(timestamp.Hour, Is.EqualTo(7));
            Assert.That(timestamp.Minute, Is.EqualTo(12));
            Assert.That(clock.Current.Minute, Is.EqualTo(14));
        }

        [Test]
        public void DuplicateMessageIdIsRejectedAcrossContactsAndDirectionsWithoutNotification()
        {
            var messages = new PhoneMessages();
            messages.TryAddIncoming("id", "landlord", Text(), Time());
            int changes = 0;
            messages.Changed += () => changes++;
            Assert.That(messages.TryAddIncoming("id", "landlord", Text("overwrite"), Time()), Is.False);
            Assert.That(messages.TryAddOutgoing("id", "other", Text(), Time()), Is.False);
            Assert.That(messages.Conversations.Count, Is.EqualTo(1));
            Assert.That(messages.Conversations[0].Messages.Count, Is.EqualTo(1));
            Assert.That(messages.Conversations[0].Messages[0].Content.Value, Is.EqualTo(Text().Value));
            Assert.That(messages.TotalUnreadCount, Is.EqualTo(1));
            Assert.That(changes, Is.Zero);
        }

        [Test]
        public void ContactLookupAndEmptyConversationCreationAreExplicitAndIdempotent()
        {
            var messages = new PhoneMessages();
            Assert.That(messages.TryGetConversation("missing", out _), Is.False);
            Assert.That(messages.MarkConversationRead("missing"), Is.False);
            Assert.That(messages.Conversations, Is.Empty);
            var first = messages.GetOrCreateConversation("landlord");
            Assert.That(messages.GetOrCreateConversation("landlord"), Is.SameAs(first));
            Assert.That(first.Messages, Is.Empty);
            Assert.That(messages.Conversations.Count, Is.EqualTo(1));
        }

        [Test]
        public void PublicCollectionsCannotMutateDomainState()
        {
            var messages = new PhoneMessages();
            messages.TryAddIncoming("a", "landlord", Text(), Time());
            Assert.Throws<NotSupportedException>(() => ((IList<PhoneConversation>)messages.Conversations).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<PhoneMessage>)messages.Conversations[0].Messages).Clear());
        }

        [Test]
        public void LocalizationKeysAndLiteralTextRemainDistinct()
        {
            var messages = new PhoneMessages();
            messages.TryAddIncoming("a", "landlord", MessageContent.FromLocalizationKey("phone.landlord"), Time());
            messages.TryAddOutgoing("b", "landlord", Text("phone.landlord"), Time());
            Assert.That(messages.Conversations[0].Messages[0].Content.Kind, Is.EqualTo(MessageContentKind.LocalizationKey));
            Assert.That(messages.Conversations[0].Messages[1].Content.Kind, Is.EqualTo(MessageContentKind.Text));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void BlankIdentifiersAreRejectedWithoutCreatingState(string id)
        {
            var messages = new PhoneMessages();
            Assert.Throws<ArgumentException>(() => messages.TryAddIncoming(id, "landlord", Text(), Time()));
            Assert.Throws<ArgumentException>(() => messages.TryAddOutgoing("valid", id, Text(), Time()));
            Assert.Throws<ArgumentException>(() => messages.GetOrCreateConversation(id));
            Assert.That(messages.Conversations, Is.Empty);
        }

        [Test]
        public void InvalidContentCannotCreateAConversation()
        {
            var messages = new PhoneMessages();
            Assert.Throws<ArgumentException>(() => MessageContent.FromText(" "));
            Assert.Throws<ArgumentException>(() => MessageContent.FromLocalizationKey(null));
            Assert.Throws<ArgumentException>(() => messages.TryAddIncoming("a", "landlord", default, Time()));
            Assert.That(messages.Conversations, Is.Empty);
        }

        [Test]
        public void ChangesArePublishedAfterCommitAndReadNoOpIsSilent()
        {
            var messages = new PhoneMessages();
            int changes = 0;
            messages.Changed += () => { changes++; Assert.That(messages.Conversations.Count, Is.EqualTo(1)); };
            messages.TryAddIncoming("a", "landlord", Text(), Time());
            messages.MarkConversationRead("landlord");
            messages.MarkConversationRead("landlord");
            messages.GetOrCreateConversation("landlord");
            Assert.That(changes, Is.EqualTo(2));
        }

        [Test]
        public void ReentrantDuplicateFromObserverSeesCommittedIdentity()
        {
            var messages = new PhoneMessages();
            messages.Changed += () => Assert.That(messages.TryAddIncoming("a", "landlord", Text(), Time()), Is.False);
            Assert.That(messages.TryAddIncoming("a", "landlord", Text(), Time()), Is.True);
            Assert.That(messages.Conversations[0].Messages.Count, Is.EqualTo(1));
        }

        [Test]
        public void ObserverExceptionDoesNotUndoCommittedMessage()
        {
            var messages = new PhoneMessages();
            messages.Changed += () => throw new InvalidOperationException("presentation failed");
            Assert.Throws<InvalidOperationException>(() => messages.TryAddIncoming("a", "landlord", Text(), Time()));
            Assert.That(messages.TotalUnreadCount, Is.EqualTo(1));
            Assert.That(messages.TryAddIncoming("a", "landlord", Text(), Time()), Is.False);
        }

        [Test]
        public void JsonRoundTripRestoresHistoryReadStateContentAndEmptyContacts()
        {
            var original = new PhoneMessages();
            original.TryAddIncoming("a", "landlord", MessageContent.FromLocalizationKey("phone.landlord"), Time(30));
            original.MarkConversationRead("landlord");
            original.TryAddOutgoing("b", "landlord", Text("Уже отправлено"), Time(40));
            original.TryAddIncoming("c", "landlord", Text(), Time(40));
            original.TryAddIncoming("d", "friend", Text(), Time(20));
            original.GetOrCreateConversation("empty");
            var snapshot = original.CaptureSnapshot();
            string json = JsonUtility.ToJson(snapshot);
            var restored = new PhoneMessages();
            restored.Restore(JsonUtility.FromJson<PhoneMessagesSnapshot>(json));
            Assert.That(JsonUtility.ToJson(restored.CaptureSnapshot()), Is.EqualTo(json));
            Assert.That(restored.TotalUnreadCount, Is.EqualTo(2));
            Assert.That(restored.Conversations[0].Messages.Select(message => message.IsRead), Is.EqualTo(new[] { true, true, false }));
            Assert.That(restored.TryAddIncoming("a", "friend", Text(), Time()), Is.False);
            Assert.That(restored.TryAddOutgoing("new-reply", "landlord", Text("Новый ответ"), Time()), Is.True);
            Assert.That(restored.TotalUnreadCount, Is.EqualTo(2));
        }

        [Test]
        public void SnapshotsAreDetachedInBothDirections()
        {
            var messages = new PhoneMessages();
            messages.TryAddIncoming("a", "landlord", Text(), Time());
            var snapshot = messages.CaptureSnapshot();
            messages.MarkConversationRead("landlord");
            Assert.That(snapshot.Conversations[0].Messages[0].IsRead, Is.False);
            var restored = new PhoneMessages();
            restored.Restore(snapshot);
            snapshot.Conversations[0].Messages[0].Content = "mutated";
            snapshot.Conversations[0].Messages[0].IsRead = true;
            snapshot.Conversations[0].ContactId = "changed";
            Assert.That(restored.Conversations[0].ContactId, Is.EqualTo("landlord"));
            Assert.That(restored.Conversations[0].Messages[0].Content.Value, Is.EqualTo(Text().Value));
            Assert.That(restored.TotalUnreadCount, Is.EqualTo(1));
        }

        [TestCase("null snapshot")]
        [TestCase("version")]
        [TestCase("null conversations")]
        [TestCase("null conversation")]
        [TestCase("blank contact")]
        [TestCase("duplicate contact")]
        [TestCase("null messages")]
        [TestCase("null message")]
        [TestCase("blank message id")]
        [TestCase("duplicate message id")]
        [TestCase("cross contact duplicate")]
        [TestCase("direction")]
        [TestCase("content kind")]
        [TestCase("blank content")]
        [TestCase("negative time")]
        [TestCase("outgoing unread")]
        public void InvalidRestoreIsAtomicAndDoesNotNotify(string corruption)
        {
            var messages = new PhoneMessages();
            messages.TryAddIncoming("original", "landlord", Text(), Time());
            string before = JsonUtility.ToJson(messages.CaptureSnapshot());
            var source = new PhoneMessages();
            source.TryAddIncoming("new1", "contact1", Text(), Time());
            source.TryAddIncoming("new2", "contact2", Text(), Time());
            var snapshot = source.CaptureSnapshot();
            var conversation = snapshot.Conversations[1];
            var message = conversation.Messages[0];
            switch (corruption)
            {
                case "null snapshot": snapshot = null; break;
                case "version": snapshot.Version = 999; break;
                case "null conversations": snapshot.Conversations = null; break;
                case "null conversation": snapshot.Conversations[1] = null; break;
                case "blank contact": conversation.ContactId = " "; break;
                case "duplicate contact": conversation.ContactId = "contact1"; break;
                case "null messages": conversation.Messages = null; break;
                case "null message": conversation.Messages[0] = null; break;
                case "blank message id": message.MessageId = ""; break;
                case "duplicate message id": conversation.Messages = new[] { message, message }; break;
                case "cross contact duplicate": message.MessageId = "new1"; break;
                case "direction": message.Direction = (MessageDirection)999; break;
                case "content kind": message.ContentKind = (MessageContentKind)999; break;
                case "blank content": message.Content = " "; break;
                case "negative time": message.GameTimeSeconds = -1; break;
                case "outgoing unread": message.Direction = MessageDirection.Outgoing; message.IsRead = false; break;
            }
            int changes = 0;
            messages.Changed += () => changes++;
            Assert.That(() => messages.Restore(snapshot), Throws.InstanceOf<ArgumentException>());
            Assert.That(JsonUtility.ToJson(messages.CaptureSnapshot()), Is.EqualTo(before));
            Assert.That(changes, Is.Zero);
            Assert.That(messages.TryAddIncoming("new1", "landlord", Text(), Time()), Is.True);
        }

        [Test]
        public void SuccessfulRestoreReplacesRatherThanAppendsAndPublishesOnce()
        {
            var messages = new PhoneMessages();
            messages.TryAddIncoming("old", "old-contact", Text(), Time());
            var source = new PhoneMessages();
            source.TryAddIncoming("new", "landlord", Text(), Time());
            int changes = 0;
            messages.Changed += () => changes++;
            messages.Restore(source.CaptureSnapshot());
            messages.Restore(source.CaptureSnapshot());
            Assert.That(messages.Conversations.Count, Is.EqualTo(1));
            Assert.That(messages.Conversations[0].Messages.Count, Is.EqualTo(1));
            Assert.That(messages.TryGetConversation("old-contact", out _), Is.False);
            Assert.That(messages.TotalUnreadCount, Is.EqualTo(1));
            Assert.That(changes, Is.EqualTo(2));
            Assert.That(messages.TryAddOutgoing("old", "landlord", Text(), Time()), Is.True);
        }

        [Test]
        public void EmptySnapshotRestoresAnEmptyInbox()
        {
            var messages = new PhoneMessages();
            messages.TryAddIncoming("a", "landlord", Text(), Time());
            messages.Restore(new PhoneMessagesSnapshot());
            Assert.That(messages.Conversations, Is.Empty);
            Assert.That(messages.TotalUnreadCount, Is.Zero);
        }
    }

    public sealed class PhoneSessionBaselineTests
    {
        [Test]
        public void ClosedPhoneCannotNavigate()
        {
            var session = new PhoneSession();
            Assert.That(session.TryNavigate(PhoneScreenId.Messages), Is.False);
            Assert.That(session.IsOpen, Is.False);
        }

        [Test]
        public void OpeningStartsAtHomeAndIsIdempotent()
        {
            var session = new PhoneSession();
            Assert.That(session.Open(), Is.True);
            Assert.That(session.CurrentScreen, Is.EqualTo(PhoneScreenId.Home));
            Assert.That(session.Open(), Is.False);
        }

        [Test]
        public void MessagesBackReturnsHomeWithoutClosing()
        {
            var session = new PhoneSession();
            session.Open();
            Assert.That(session.TryNavigate(PhoneScreenId.Messages), Is.True);
            Assert.That(session.TryBack(), Is.True);
            Assert.That(session.CurrentScreen, Is.EqualTo(PhoneScreenId.Home));
            Assert.That(session.IsOpen, Is.True);
            Assert.That(session.TryBack(), Is.False);
        }

        [Test]
        public void ClosingFromMessagesClearsNavigationHistory()
        {
            var session = new PhoneSession();
            session.Open();
            session.TryNavigate(PhoneScreenId.Messages);
            Assert.That(session.Close(), Is.True);
            Assert.That(session.Close(), Is.False);
            session.Open();
            Assert.That(session.CurrentScreen, Is.EqualTo(PhoneScreenId.Home));
            Assert.That(session.TryBack(), Is.False);
        }

        [Test]
        public void InvalidScreenDoesNotChangeState()
        {
            var session = new PhoneSession();
            session.Open();
            Assert.That(session.TryNavigate((PhoneScreenId)999), Is.False);
            Assert.That(session.CurrentScreen, Is.EqualTo(PhoneScreenId.Home));
        }

        [Test]
        public void NoOpNavigationDoesNotPublishChanges()
        {
            var session = new PhoneSession();
            int changes = 0;
            session.Changed += () => changes++;
            session.Open();
            session.TryNavigate(PhoneScreenId.Home);
            session.TryBack();
            Assert.That(changes, Is.EqualTo(1));
        }
    }
}
