using System;

namespace GoLive.Phone
{
    [Serializable]
    public sealed class PhoneMessagesSnapshot
    {
        public const int CurrentVersion = 1;
        public int Version = CurrentVersion;
        public PhoneConversationSnapshot[] Conversations = Array.Empty<PhoneConversationSnapshot>();
    }

    [Serializable]
    public sealed class PhoneConversationSnapshot
    {
        public string ContactId;
        public PhoneMessageSnapshot[] Messages = Array.Empty<PhoneMessageSnapshot>();
    }

    [Serializable]
    public sealed class PhoneMessageSnapshot
    {
        public string MessageId;
        public MessageDirection Direction;
        public MessageContentKind ContentKind;
        public string Content;
        public long GameTimeSeconds;
        public bool IsRead;
    }
}
