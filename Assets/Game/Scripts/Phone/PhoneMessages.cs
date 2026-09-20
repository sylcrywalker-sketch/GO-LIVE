using System;
using System.Collections.Generic;
using GoLive.GameTime;

namespace GoLive.Phone
{
    public enum MessageDirection { Incoming = 0, Outgoing = 1 }
    public enum MessageContentKind { Text = 0, LocalizationKey = 1 }

    public readonly struct MessageContent
    {
        public MessageContentKind Kind { get; }
        public string Value { get; }

        private MessageContent(MessageContentKind kind, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Message content must not be blank.", nameof(value));

            Kind = kind;
            Value = value;
        }

        public static MessageContent FromText(string text) => new(MessageContentKind.Text, text);
        public static MessageContent FromLocalizationKey(string key) => new(MessageContentKind.LocalizationKey, key);

        internal void Validate()
        {
            if (string.IsNullOrWhiteSpace(Value) || (Kind != MessageContentKind.Text && Kind != MessageContentKind.LocalizationKey))
                throw new ArgumentException("A valid message body or localization key is required.", nameof(MessageContent));
        }
    }

    public sealed class PhoneMessage
    {
        public string MessageId { get; }
        public MessageDirection Direction { get; }
        public MessageContent Content { get; }
        public GameTimeSnapshot Timestamp { get; }
        // Read by the player; this is not a delivery/read receipt from the contact.
        public bool IsRead { get; private set; }

        internal PhoneMessage(string messageId, MessageDirection direction, MessageContent content, GameTimeSnapshot timestamp, bool isRead)
        {
            MessageId = messageId;
            Direction = direction;
            Content = content;
            Timestamp = timestamp;
            IsRead = isRead;
        }

        internal void MarkRead() => IsRead = true;
    }

    public sealed class PhoneConversation
    {
        public string ContactId { get; }
        public IReadOnlyList<PhoneMessage> Messages { get; }
        public int UnreadCount
        {
            get
            {
                int count = 0;
                foreach (var message in _messages)
                    if (message.Direction == MessageDirection.Incoming && !message.IsRead)
                        count++;
                return count;
            }
        }

        private readonly List<PhoneMessage> _messages = new();

        internal PhoneConversation(string contactId)
        {
            ContactId = contactId;
            Messages = _messages.AsReadOnly();
        }

        internal void Append(PhoneMessage message) => _messages.Add(message);

        internal bool MarkRead()
        {
            bool changed = false;
            foreach (var message in _messages)
            {
                if (message.IsRead)
                    continue;
                message.MarkRead();
                changed = true;
            }
            return changed;
        }
    }

    /// <summary>
    /// Owns all message state. Commands run on the owning game thread. No initial history is injected.
    /// Read projections are live until Restore replaces them; observers should reacquire them on Changed.
    /// </summary>
    public sealed class PhoneMessages
    {
        public IReadOnlyList<PhoneConversation> Conversations { get; private set; }
        public int TotalUnreadCount
        {
            get
            {
                int count = 0;
                foreach (var conversation in _conversations)
                    count += conversation.UnreadCount;
                return count;
            }
        }

        // Same synchronous event contract as PhoneSession: state is committed before notification.
        // Subscriber exceptions propagate without rolling back state; subscribers must handle their own errors.
        public event Action Changed;

        private List<PhoneConversation> _conversations = new();
        private Dictionary<string, PhoneConversation> _contacts = new(StringComparer.Ordinal);
        private HashSet<string> _messageIds = new(StringComparer.Ordinal);

        public PhoneMessages() => Conversations = _conversations.AsReadOnly();

        public PhoneConversation GetOrCreateConversation(string contactId)
        {
            ValidateId(contactId, nameof(contactId));
            if (_contacts.TryGetValue(contactId, out var conversation))
                return conversation;

            conversation = AddConversation(contactId);
            Changed?.Invoke();
            return conversation;
        }

        public bool TryGetConversation(string contactId, out PhoneConversation conversation)
        {
            ValidateId(contactId, nameof(contactId));
            return _contacts.TryGetValue(contactId, out conversation);
        }

        /// <summary>Returns false only for an already accepted MessageId; invalid arguments throw.</summary>
        public bool TryAddIncoming(string messageId, string contactId, MessageContent content, GameTimeSnapshot timestamp)
            => TryAdd(messageId, contactId, content, timestamp, MessageDirection.Incoming);

        /// <summary>Adds an authored or future player reply. Does not mark incoming history as read.</summary>
        public bool TryAddOutgoing(string messageId, string contactId, MessageContent content, GameTimeSnapshot timestamp)
            => TryAdd(messageId, contactId, content, timestamp, MessageDirection.Outgoing);

        public bool MarkConversationRead(string contactId)
        {
            ValidateId(contactId, nameof(contactId));
            if (!_contacts.TryGetValue(contactId, out var conversation) || !conversation.MarkRead())
                return false;

            Changed?.Invoke();
            return true;
        }

        public PhoneMessagesSnapshot CaptureSnapshot()
        {
            var snapshot = new PhoneMessagesSnapshot { Conversations = new PhoneConversationSnapshot[_conversations.Count] };
            for (int i = 0; i < _conversations.Count; i++)
            {
                var conversation = _conversations[i];
                var saved = new PhoneConversationSnapshot
                {
                    ContactId = conversation.ContactId,
                    Messages = new PhoneMessageSnapshot[conversation.Messages.Count]
                };
                for (int j = 0; j < conversation.Messages.Count; j++)
                {
                    var message = conversation.Messages[j];
                    saved.Messages[j] = new PhoneMessageSnapshot
                    {
                        MessageId = message.MessageId,
                        Direction = message.Direction,
                        ContentKind = message.Content.Kind,
                        Content = message.Content.Value,
                        GameTimeSeconds = message.Timestamp.TotalSeconds,
                        IsRead = message.IsRead
                    };
                }
                snapshot.Conversations[i] = saved;
            }
            return snapshot;
        }

        /// <summary>
        /// Validates and copies the complete snapshot before replacing state. Invalid input leaves state unchanged.
        /// Publishes one Changed event after commit; does not replay individual incoming/outgoing commands.
        /// </summary>
        public void Restore(PhoneMessagesSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.Version != PhoneMessagesSnapshot.CurrentVersion || snapshot.Conversations == null)
                throw new ArgumentException("Unsupported or incomplete messages snapshot.", nameof(snapshot));

            var conversations = new List<PhoneConversation>(snapshot.Conversations.Length);
            var contacts = new Dictionary<string, PhoneConversation>(StringComparer.Ordinal);
            var messageIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var saved in snapshot.Conversations)
            {
                if (saved == null || saved.Messages == null)
                    throw new ArgumentException("A conversation or its history is missing.", nameof(snapshot));
                ValidateId(saved.ContactId, nameof(saved.ContactId));
                if (contacts.ContainsKey(saved.ContactId))
                    throw new ArgumentException($"Duplicate contact ID: {saved.ContactId}.", nameof(snapshot));

                var conversation = new PhoneConversation(saved.ContactId);
                foreach (var savedMessage in saved.Messages)
                {
                    var message = RestoreMessage(savedMessage);
                    if (!messageIds.Add(message.MessageId))
                        throw new ArgumentException($"Duplicate message ID: {message.MessageId}.", nameof(snapshot));
                    conversation.Append(message);
                }
                contacts.Add(conversation.ContactId, conversation);
                conversations.Add(conversation);
            }

            var readOnlyConversations = conversations.AsReadOnly();
            _conversations = conversations;
            _contacts = contacts;
            _messageIds = messageIds;
            Conversations = readOnlyConversations;
            Changed?.Invoke();
        }

        private bool TryAdd(string messageId, string contactId, MessageContent content, GameTimeSnapshot timestamp, MessageDirection direction)
        {
            ValidateId(messageId, nameof(messageId));
            ValidateId(contactId, nameof(contactId));
            content.Validate();
            if (_messageIds.Contains(messageId))
                return false;

            var message = new PhoneMessage(messageId, direction, content, timestamp, direction == MessageDirection.Outgoing);
            if (!_contacts.TryGetValue(contactId, out var conversation))
                conversation = AddConversation(contactId);
            conversation.Append(message);
            _messageIds.Add(messageId);
            Changed?.Invoke();
            return true;
        }

        private PhoneConversation AddConversation(string contactId)
        {
            var conversation = new PhoneConversation(contactId);
            _contacts.Add(contactId, conversation);
            _conversations.Add(conversation);
            return conversation;
        }

        private static PhoneMessage RestoreMessage(PhoneMessageSnapshot saved)
        {
            if (saved == null)
                throw new ArgumentException("A saved message is missing.", nameof(saved));
            ValidateId(saved.MessageId, nameof(saved.MessageId));
            if (saved.Direction != MessageDirection.Incoming && saved.Direction != MessageDirection.Outgoing)
                throw new ArgumentException("Unknown message direction.", nameof(saved));
            if (saved.Direction == MessageDirection.Outgoing && !saved.IsRead)
                throw new ArgumentException("Outgoing messages must be read by the player.", nameof(saved));

            MessageContent content = saved.ContentKind switch
            {
                MessageContentKind.Text => MessageContent.FromText(saved.Content),
                MessageContentKind.LocalizationKey => MessageContent.FromLocalizationKey(saved.Content),
                _ => throw new ArgumentException("Unknown message content kind.", nameof(saved))
            };
            return new PhoneMessage(saved.MessageId, saved.Direction, content, new GameTimeSnapshot(saved.GameTimeSeconds), saved.IsRead);
        }

        private static void ValidateId(string id, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("A stable non-blank ID is required.", parameterName);
        }
    }
}
