using System;
using System.Collections.Generic;

namespace GoLive.Viewers
{
    public sealed class StreamChatMessage
    {
        public string Id { get; }
        public string ViewerId { get; }
        public string SenderName { get; }
        public string Text { get; }
        public double StreamSeconds { get; }
        // The reaction it answers (for tracing and viewer-to-viewer replies); 0 for none.
        public long IntentId { get; }
        public ReactionSource Source { get; }
        // Set when the message accompanies an accepted donation (the amount is C# truth, never model text).
        public long DonationCents { get; }

        public StreamChatMessage(string id, string viewerId, string senderName, string text, double streamSeconds, long intentId,
            ReactionSource source, long donationCents = 0)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("A chat message needs an id.", nameof(id));
            if (string.IsNullOrWhiteSpace(senderName)) throw new ArgumentException("A chat message needs a sender.", nameof(senderName));
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("A chat message needs text.", nameof(text));
            Id = id;
            ViewerId = viewerId;
            SenderName = senderName;
            Text = text;
            StreamSeconds = streamSeconds;
            IntentId = intentId;
            Source = source;
            DonationCents = donationCents;
        }
    }

    // The visible chat of the current broadcast. Bounded, transient (cleared when a broadcast starts and on load).
    public sealed class StreamChat
    {
        public const int Capacity = 60;
        private readonly List<StreamChatMessage> _messages = new();
        private long _serial;

        public IReadOnlyList<StreamChatMessage> Messages { get; }
        public event Action<StreamChatMessage> Added;
        public event Action Cleared;

        public StreamChat() => Messages = _messages.AsReadOnly();

        public StreamChatMessage Add(string broadcastId, string viewerId, string senderName, string text, double streamSeconds, long intentId,
            ReactionSource source, long donationCents = 0)
        {
            var message = new StreamChatMessage(broadcastId + ".chat." + ++_serial, viewerId, senderName, text, streamSeconds, intentId, source, donationCents);
            if (_messages.Count == Capacity) _messages.RemoveAt(0);
            _messages.Add(message);
            Added?.Invoke(message);
            return message;
        }

        public void Clear()
        {
            if (_messages.Count == 0) return;
            _messages.Clear();
            Cleared?.Invoke();
        }
    }
}
