using System;

namespace GoLive.Viewers
{
    // Chat rhythm and reaction rules, authored in the ViewerCoreConfig asset. Times are broadcast seconds.
    [Serializable]
    public sealed class ReactionTuning
    {
        // Recognized speech below this relevance never reaches the chat.
        public float SpeechThreshold = .35f;

        // Natural pauses are normal: only a long silence (and more so a very long one) is a chat moment.
        public float LongSilenceSeconds = 75f;
        public float VeryLongSilenceSeconds = 180f;
        // After a very long silence the chat may check in again only this much later.
        public float SilenceRepeatSeconds = 240f;
        // Leaving the desk on air becomes noticeable after this long.
        public float AwaySeconds = 25f;

        // Sustainable chat rate in messages per minute: RateScale * viewers^RateExponent, at most MaximumPerMinute.
        public float RateScale = .5f;
        public float RateExponent = .8f;
        public float MaximumPerMinute = 36f;
        // Short bursts the chat can afford: BurstBase + BurstScale * sqrt(viewers) messages.
        public float BurstBase = 1f;
        public float BurstScale = .6f;

        // A viewer who just wrote waits at least this long (scaled by their own pace) before writing again.
        public float ViewerGapSeconds = 25f;
        // A viewer the streamer addresses directly may answer after this minimal gap.
        public float DirectGapSeconds = 4f;
        // Weight of one anonymous viewer relative to a talkative regular.
        public float AnonymousTalkativeness = .22f;

        // Reaction timing: reading/typing delay before the message appears, and how long it stays relevant.
        public float MinimumDelaySeconds = 1.6f;
        public float MaximumDelaySeconds = 9f;
        public float StaleSeconds = 14f;

        // Conversation: when the streamer talks TO the chat (asks, requests, greets), somebody normally answers. Up to this
        // audience size the answer does not depend on the chat budget; bigger chats still let the first answer overdraw it.
        public int ConversationAudience = 3;
        public float ConversationAnswerChance = .92f;
        public float GreetingAnswerChance = .75f;
        // A viewer in a conversation may write again after this gap (instead of ViewerGapSeconds).
        public float ConversationGapSeconds = 6f;
        // The streamer's next phrase within this window after a viewer's answer continues with that viewer, for at most
        // this many published viewer answers, including the initial answer (never an endless 1:1 thread).
        public float ConversationWindowSeconds = 40f;
        public int ConversationMaximumTurns = 5;
        // Reading/typing delay for conversational answers (the model's own latency comes on top when it is slower).
        public float ConversationMinimumDelaySeconds = 1.2f;
        public float ConversationMaximumDelaySeconds = 4.5f;

        public string Validate()
        {
            if (!Fraction(SpeechThreshold)) return "Speech threshold must be in (0, 1].";
            if (!(LongSilenceSeconds > 0) || !(VeryLongSilenceSeconds > LongSilenceSeconds) || !(SilenceRepeatSeconds > 0) || !(AwaySeconds > 0))
                return "Silence thresholds must be positive and very long above long.";
            if (!(RateScale > 0) || !(RateExponent > 0 && RateExponent <= 1.5f) || !(MaximumPerMinute > 0))
                return "Chat rate must be positive.";
            if (!(BurstBase >= 1) || !(BurstScale >= 0)) return "Chat burst must allow at least one message.";
            if (!(ViewerGapSeconds > 0) || !(DirectGapSeconds > 0) || !(DirectGapSeconds <= ViewerGapSeconds)) return "Viewer gaps are inconsistent.";
            if (!Fraction(AnonymousTalkativeness)) return "Anonymous talkativeness must be in (0, 1].";
            if (!(MinimumDelaySeconds > 0) || !(MaximumDelaySeconds > MinimumDelaySeconds) || !(StaleSeconds > 0))
                return "Reaction delays are inconsistent.";
            if (ConversationAudience < 1 || !Fraction(ConversationAnswerChance) || !Fraction(GreetingAnswerChance))
                return "Conversation audience and answer chances are invalid.";
            if (!(ConversationGapSeconds >= DirectGapSeconds) || !(ConversationWindowSeconds > 0) || ConversationMaximumTurns < 1)
                return "Conversation gap, window or turns are invalid.";
            if (!(ConversationMinimumDelaySeconds > 0) || !(ConversationMaximumDelaySeconds > ConversationMinimumDelaySeconds) ||
                !(ConversationMaximumDelaySeconds < StaleSeconds)) return "Conversation delays are inconsistent.";
            return null;
        }

        private static bool Fraction(float value) => value > 0 && value <= 1;
    }
}
