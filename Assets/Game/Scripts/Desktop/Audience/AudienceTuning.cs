using System;
using System.Linq;

namespace GoLive.Desktop
{
    // Configuration for AudienceSimulation, authored in the AudienceTuningConfig asset. Never runtime state.
    // Rates are per viewer per simulated second; multipliers are dimensionless.
    [Serializable]
    public sealed class AudienceTuning
    {
        // Strangers a brand-new channel attracts at the daily peak, before quality and retention.
        public float DiscoveryViewers = 3f;
        // Share of existing followers who watch a stream at the daily peak.
        public float FollowerTurnout = .05f;
        // Discovery builds up after going live (time constant, seconds).
        public float RampSeconds = 75f;
        // Average stay of a viewer on a perfectly stable stream; worse retention shortens it.
        public float MeanWatchSeconds = 150f;

        // Potential audience by game time at 00, 03, 06, 09, 12, 15, 18, 21 and 24 h, interpolated.
        // The daily peak lies inside 15:00-21:00; night is weaker but never zero.
        public float[] DailyCurve = { .45f, .25f, .3f, .5f, .65f, .95f, 1f, .9f, .45f };

        public float LowQualityAttraction = .85f;
        public float MediumQualityAttraction = 1f;
        public float HighQualityAttraction = 1.12f;

        // Upload headroom: the minimum for the selected quality gives this stability; twice the minimum is fully stable.
        public float StabilityAtMinimumUpload = .78f;
        public float FullStabilityUploadRatio = 2f;
        // Encoding Medium/High on the processor alone (no dedicated graphics card) drops frames.
        public float ProcessorEncodingStability = .9f;
        // Perceived audio without the in-game microphone.
        public float MissingMicrophoneRetention = .75f;
        public float WebcamEngagement = 1.08f;
        // Retention while the streamer is silent for long (voice listened to), very long, or away from the desk.
        public float QuietRetention = .97f;
        public float VeryQuietRetention = .92f;
        public float AwayRetention = .88f;

        // Onboarding discovery for a channel that has never completed a broadcast.
        public int FirstStreamInitialViewersMin = 1;
        public int FirstStreamInitialViewersMax = 3;
        public float FirstStreamBoostViewers = 2f;
        public float FirstStreamBoostSeconds = 240f;

        public float FollowRate = .0006f;
        public float SubscriptionRate = .00004f;
        public float DonationRate = .0002f;
        public float ChatRate = .025f;
        public int[] DonationCents = { 100, 200, 300, 500, 1000, 2000 };
        public float[] DonationWeights = { 30, 25, 15, 15, 10, 5 };

        public string Validate()
        {
            if (!Positive(DiscoveryViewers, true) || !Positive(FollowerTurnout, true) || !Positive(RampSeconds) || !Positive(MeanWatchSeconds))
                return "Audience potential values must be finite and non-negative; times must be positive.";
            if (DailyCurve == null || DailyCurve.Length != 9 || !DailyCurve.All(value => Positive(value, true)))
                return "Audience daily curve needs nine finite non-negative multipliers (every three hours, 00-24).";
            if (!Positive(LowQualityAttraction) || !Positive(MediumQualityAttraction) || !Positive(HighQualityAttraction))
                return "Quality attraction multipliers must be positive.";
            if (!Fraction(StabilityAtMinimumUpload) || !(FullStabilityUploadRatio > 1) || !Fraction(ProcessorEncodingStability) ||
                !Fraction(MissingMicrophoneRetention) || !Positive(WebcamEngagement) ||
                !Fraction(QuietRetention) || !Fraction(VeryQuietRetention) || !Fraction(AwayRetention))
                return "Stability and retention values must be in (0, 1]; full stability needs an upload ratio above 1.";
            if (FirstStreamInitialViewersMin < 0 || FirstStreamInitialViewersMax < FirstStreamInitialViewersMin ||
                !Positive(FirstStreamBoostViewers, true) || !Positive(FirstStreamBoostSeconds))
                return "First-stream boost values are invalid.";
            if (!Positive(FollowRate, true) || !Positive(SubscriptionRate, true) || !Positive(DonationRate, true) || !Positive(ChatRate, true))
                return "Audience outcome rates must be finite and non-negative.";
            if (DonationCents == null || DonationWeights == null || DonationCents.Length == 0 || DonationCents.Length != DonationWeights.Length ||
                !DonationCents.All(cents => cents > 0) || !DonationWeights.All(weight => Positive(weight, true)) || !DonationWeights.Any(weight => weight > 0))
                return "Donation amounts need positive cents and matching non-negative weights.";
            return null;
        }

        private static bool Positive(float value, bool allowZero = false) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && (allowZero ? value >= 0 : value > 0);

        private static bool Fraction(float value) => Positive(value) && value <= 1;
    }
}
