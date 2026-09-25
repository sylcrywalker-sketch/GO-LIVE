using System;
using System.Collections.Generic;

namespace GoLive.Desktop
{
    public sealed class OutlineMessage
    {
        public string Id { get; }
        public string SubjectKey { get; }
        public string BodyKey { get; }
        public IReadOnlyList<string> BodyArguments { get; }
        public bool IsRead { get; internal set; }

        internal OutlineMessage(string id, string subjectKey, string bodyKey, string[] arguments, bool isRead)
        {
            Id = id;
            SubjectKey = subjectKey;
            BodyKey = bodyKey;
            BodyArguments = Array.AsReadOnly((string[])arguments.Clone());
            IsRead = isRead;
        }
    }

    public sealed class OutlineAccount
    {
        public const int MaximumMessages = 100;
        public const int MaximumReceivedIds = 4096;
        public string Address { get; private set; } = "";
        public bool IsCreated => Address.Length > 0;
        public IReadOnlyList<OutlineMessage> Messages { get; private set; }
        public event Action Changed;
        private List<OutlineMessage> _messages = new();
        private List<string> _receivedIds = new();
        private HashSet<string> _received = new(StringComparer.Ordinal);

        public OutlineAccount() => Messages = _messages.AsReadOnly();

        public string CreateAddress(string username)
        {
            if (IsCreated) return "desktop.outline.already_created";
            string normalized = username?.Trim().ToLowerInvariant();
            if (!DesktopAccountValidation.Username(normalized)) return "desktop.outline.invalid_username";
            Address = normalized + "@outline.local";
            Changed?.Invoke();
            return null;
        }

        public string Receive(string id, string subjectKey, string bodyKey, params string[] bodyArguments)
        {
            if (!IsCreated) return "desktop.outline.account_required";
            if (!DesktopAccountValidation.Id(id)) return "desktop.outline.invalid_message";
            if (_received.Contains(id)) return null;
            if (!ValidContent(subjectKey, bodyKey, bodyArguments)) return "desktop.outline.invalid_message";
            if (_receivedIds.Count == MaximumReceivedIds) return "desktop.outline.history_full";
            var message = new OutlineMessage(id, subjectKey, bodyKey, bodyArguments, false);
            _received.Add(id);
            _receivedIds.Add(id);
            if (_messages.Count == MaximumMessages) _messages.RemoveAt(0);
            _messages.Add(message);
            Changed?.Invoke();
            return null;
        }

        public string MarkRead(string id)
        {
            foreach (OutlineMessage message in _messages)
            {
                if (!string.Equals(message.Id, id, StringComparison.Ordinal)) continue;
                if (!message.IsRead)
                {
                    message.IsRead = true;
                    Changed?.Invoke();
                }
                return null;
            }
            return "desktop.outline.message_missing";
        }

        public OutlineSnapshot Capture()
        {
            var snapshot = new OutlineSnapshot { Address = Address, ReceivedIds = _receivedIds.ToArray(), Messages = new OutlineMessageSnapshot[_messages.Count] };
            for (int i = 0; i < _messages.Count; i++)
            {
                OutlineMessage message = _messages[i];
                var arguments = new string[message.BodyArguments.Count];
                for (int j = 0; j < arguments.Length; j++) arguments[j] = message.BodyArguments[j];
                snapshot.Messages[i] = new OutlineMessageSnapshot
                {
                    Id = message.Id, SubjectKey = message.SubjectKey, BodyKey = message.BodyKey,
                    BodyArguments = arguments, IsRead = message.IsRead
                };
            }
            return snapshot;
        }

        public static bool Validate(OutlineSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Version != 1 || snapshot.Address == null || snapshot.Messages == null ||
                !DesktopAccountValidation.Ledger(snapshot.ReceivedIds, MaximumReceivedIds)) return false;
            if (snapshot.Address.Length == 0) return snapshot.Messages.Length == 0 && snapshot.ReceivedIds.Length == 0;
            if (!DesktopAccountValidation.Address(snapshot.Address) || snapshot.Messages.Length != Math.Min(snapshot.ReceivedIds.Length, MaximumMessages)) return false;
            int offset = snapshot.ReceivedIds.Length - snapshot.Messages.Length;
            for (int i = 0; i < snapshot.Messages.Length; i++)
            {
                OutlineMessageSnapshot message = snapshot.Messages[i];
                if (message == null || message.Id != snapshot.ReceivedIds[offset + i] || !ValidContent(message.SubjectKey, message.BodyKey, message.BodyArguments)) return false;
            }
            return true;
        }

        public void Restore(OutlineSnapshot snapshot)
        {
            if (!Validate(snapshot)) throw new ArgumentException("Invalid Outline snapshot.", nameof(snapshot));
            var messages = new List<OutlineMessage>(snapshot.Messages.Length);
            foreach (OutlineMessageSnapshot message in snapshot.Messages)
                messages.Add(new OutlineMessage(message.Id, message.SubjectKey, message.BodyKey, message.BodyArguments, message.IsRead));
            var ids = new List<string>(snapshot.ReceivedIds);
            var received = new HashSet<string>(ids, StringComparer.Ordinal);
            Address = snapshot.Address;
            _messages = messages;
            Messages = _messages.AsReadOnly();
            _receivedIds = ids;
            _received = received;
            Changed?.Invoke();
        }

        private static bool ValidContent(string subjectKey, string bodyKey, string[] arguments)
        {
            if (!DesktopAccountValidation.Id(subjectKey) || !DesktopAccountValidation.Id(bodyKey) || arguments == null || arguments.Length > 8) return false;
            foreach (string argument in arguments)
                if (!DesktopAccountValidation.Text(argument, 0, 240, true)) return false;
            return true;
        }
    }
}
