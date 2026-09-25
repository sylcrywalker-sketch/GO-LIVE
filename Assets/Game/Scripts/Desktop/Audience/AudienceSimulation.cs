using System;
using System.Collections.Generic;

namespace GoLive.Desktop
{
    public enum AudienceTrend { Steady, Growing, Declining }

    // Game state the audience reacts to, sampled by StreamSession every tick. Hardware shapes stability
    // and perceived quality; it never awards viewers directly.
    public readonly struct AudienceConditions
    {
        public int MinuteOfDay { get; }
        public StreamQuality Quality { get; }
        public float UploadMbps { get; }
        public bool DedicatedGraphics { get; }
        public bool HasMicrophone { get; }
        public bool HasWebcam { get; }

        public AudienceConditions(int minuteOfDay, StreamQuality quality, float uploadMbps, bool dedicatedGraphics,
            bool hasMicrophone, bool hasWebcam)
        {
            MinuteOfDay = minuteOfDay;
            Quality = quality;
            UploadMbps = uploadMbps;
            DedicatedGraphics = dedicatedGraphics;
            HasMicrophone = hasMicrophone;
            HasWebcam = hasWebcam;
        }
    }

    // Multipliers derived from conditions. Retention = stability x encoding x audio, in (0, 1].
    public readonly struct AudienceFactors
    {
        public double TimeOfDay { get; }
        public double QualityAttraction { get; }
        public double Stability { get; }
        public double Encoding { get; }
        public double Audio { get; }
        public double Engagement { get; }
        public double Retention => Stability * Encoding * Audio;

        private AudienceFactors(double timeOfDay, double quality, double stability, double encoding, double audio, double engagement)
        {
            TimeOfDay = timeOfDay;
            QualityAttraction = quality;
            Stability = stability;
            Encoding = encoding;
            Audio = audio;
            Engagement = engagement;
        }

        public static AudienceFactors Evaluate(in AudienceConditions conditions, AudienceTuning tuning)
        {
            double quality = conditions.Quality switch
            {
                StreamQuality.Low => tuning.LowQualityAttraction,
                StreamQuality.Medium => tuning.MediumQualityAttraction,
                _ => tuning.HighQualityAttraction
            };
            // Upload headroom over the selected quality's minimum: the game-owned internet limits stability.
            double headroom = conditions.UploadMbps / StreamSession.MinimumUploadMbps(conditions.Quality);
            double excess = double.IsNaN(headroom) ? 0 : Math.Clamp((headroom - 1) / (tuning.FullStabilityUploadRatio - 1), 0, 1);
            double stability = tuning.StabilityAtMinimumUpload + (1 - tuning.StabilityAtMinimumUpload) * excess;
            // A dedicated graphics card encodes; without one the processor carries Medium/High and drops frames.
            double encoding = conditions.DedicatedGraphics || conditions.Quality == StreamQuality.Low ? 1 : tuning.ProcessorEncodingStability;
            double audio = conditions.HasMicrophone ? 1 : tuning.MissingMicrophoneRetention;
            double engagement = conditions.HasWebcam ? tuning.WebcamEngagement : 1;
            return new AudienceFactors(DailyMultiplier(conditions.MinuteOfDay, tuning), quality, stability, encoding, audio, engagement);
        }

        public static double DailyMultiplier(int minuteOfDay, AudienceTuning tuning)
        {
            if (minuteOfDay < 0 || minuteOfDay >= 1440) throw new ArgumentOutOfRangeException(nameof(minuteOfDay));
            double position = minuteOfDay / 180.0;
            int index = (int)position;
            double fraction = position - index;
            return tuning.DailyCurve[index] + (tuning.DailyCurve[index + 1] - tuning.DailyCurve[index]) * fraction;
        }
    }

    public readonly struct AudienceAdvance
    {
        public int Steps { get; }
        public long ChatMessages { get; }
        public int NewFollows { get; }
        public int NewSubscriptions { get; }

        public AudienceAdvance(int steps, long chatMessages, int newFollows, int newSubscriptions)
        {
            Steps = steps;
            ChatMessages = chatMessages;
            NewFollows = newFollows;
            NewSubscriptions = newSubscriptions;
        }
    }

    // Sole owner of the current broadcast's anonymous audience. A birth-death process in whole simulated
    // seconds: viewers arrive (Poisson) toward a target set by channel size, time of day, quality and
    // retention, and each viewer leaves with a probability that grows as retention falls. Follows,
    // subscriptions, donations and chat are drawn per viewer-second. It is transient: StreamSession
    // creates one per broadcast, advances it only while Live and freezes it when the broadcast ends.
    public sealed class AudienceSimulation
    {
        // One hour of simulated seconds per call keeps a huge frame delta bounded; any longer gap is
        // integrated at the current audience without drawing new outcomes.
        public const int MaximumStepsPerAdvance = 3600;
        private readonly AudienceTuning _tuning;
        private readonly AudienceRandom _random;
        private readonly double _baseViewers;
        private readonly bool _firstStream;
        private double _pendingSeconds;
        private bool _live;

        public ulong Seed { get; }
        public bool FirstStreamBoost => _firstStream;
        public int CurrentViewers { get; private set; }
        public int PeakViewers { get; private set; }
        public double ViewerSeconds { get; private set; }
        public double SimulatedSeconds { get; private set; }
        public double AverageViewers => SimulatedSeconds > 0 ? ViewerSeconds / SimulatedSeconds : 0;
        public double TargetViewers { get; private set; }
        public AudienceTrend Trend { get; private set; }
        public int Follows { get; private set; }
        public int Subscriptions { get; private set; }
        public long ChatMessages { get; private set; }
        public bool IsFinished { get; private set; }

        public AudienceSimulation(AudienceTuning tuning, ulong seed, long channelFollowers, bool firstStream)
        {
            _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
            if (channelFollowers < 0) throw new ArgumentOutOfRangeException(nameof(channelFollowers));
            Seed = seed;
            _random = new AudienceRandom(seed);
            _firstStream = firstStream;
            _baseViewers = tuning.DiscoveryViewers + tuning.FollowerTurnout * (double)channelFollowers;
        }

        // The broadcast went live. An eligible first stream is discovered at once by a few viewers.
        public void GoLive()
        {
            if (_live || IsFinished) throw new InvalidOperationException("The audience is already live or finished.");
            _live = true;
            if (!_firstStream) return;
            int range = _tuning.FirstStreamInitialViewersMax - _tuning.FirstStreamInitialViewersMin + 1;
            CurrentViewers = _tuning.FirstStreamInitialViewersMin + _random.NextInt(range);
            PeakViewers = CurrentViewers;
        }

        // Advances whole simulated seconds; a fractional remainder carries into the next call, so any frame
        // split of the same live time produces the same audience. New donations are appended in cents.
        public AudienceAdvance Advance(double seconds, in AudienceConditions conditions, List<long> donations)
        {
            if (!_live || IsFinished) throw new InvalidOperationException("Only a live audience can advance.");
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (donations == null) throw new ArgumentNullException(nameof(donations));
            AudienceFactors factors = AudienceFactors.Evaluate(conditions, _tuning);
            int follows = Follows, subscriptions = Subscriptions;
            long chat = ChatMessages;
            int steps = 0;
            _pendingSeconds += seconds;
            while (_pendingSeconds >= 1 && steps < MaximumStepsPerAdvance)
            {
                Step(factors, donations);
                _pendingSeconds -= 1;
                steps++;
            }
            if (_pendingSeconds >= 1)
            {
                double skipped = Math.Floor(_pendingSeconds);
                ViewerSeconds += CurrentViewers * skipped;
                SimulatedSeconds += skipped;
                _pendingSeconds -= skipped;
            }
            return new AudienceAdvance(steps, ChatMessages - chat, Follows - follows, Subscriptions - subscriptions);
        }

        // Freezes the result; the owner reads the final values into the stream summary.
        public void Finish() => IsFinished = true;

        private void Step(in AudienceFactors factors, List<long> donations)
        {
            double elapsed = SimulatedSeconds;
            double ramp = 1 - Math.Exp(-elapsed / _tuning.RampSeconds);
            double boost = _firstStream ? _tuning.FirstStreamBoostViewers * Math.Exp(-elapsed / _tuning.FirstStreamBoostSeconds) : 0;
            double potential = _baseViewers * factors.TimeOfDay * factors.QualityAttraction * ramp + boost;
            double retention = factors.Retention;
            TargetViewers = potential * retention * factors.Engagement;

            // Equilibrium of arrivals (rate target/stay) and departures (1/stay per viewer) is the target.
            double stay = Math.Max(1, _tuning.MeanWatchSeconds * retention);
            int arrivals = _random.Poisson(TargetViewers / stay);
            int departures = _random.Binomial(CurrentViewers, 1 - Math.Exp(-1 / stay));
            CurrentViewers = (int)Math.Min(int.MaxValue, (long)CurrentViewers + arrivals - departures);
            PeakViewers = Math.Max(PeakViewers, CurrentViewers);
            ViewerSeconds += CurrentViewers;
            SimulatedSeconds += 1;
            Trend = TargetViewers > CurrentViewers + .75 ? AudienceTrend.Growing
                : TargetViewers < CurrentViewers - .75 ? AudienceTrend.Declining : AudienceTrend.Steady;

            double engaged = CurrentViewers * factors.Engagement;
            Follows = (int)Math.Min(int.MaxValue, (long)Follows + _random.Poisson(engaged * _tuning.FollowRate));
            Subscriptions = (int)Math.Min(int.MaxValue, (long)Subscriptions + _random.Poisson(engaged * _tuning.SubscriptionRate));
            int newDonations = _random.Poisson(engaged * _tuning.DonationRate);
            for (int i = 0; i < newDonations; i++) donations.Add(DonationAmount());
            ChatMessages += _random.Poisson(engaged * _tuning.ChatRate);
        }

        private long DonationAmount()
        {
            double total = 0;
            foreach (float weight in _tuning.DonationWeights) total += weight;
            double pick = _random.NextDouble() * total;
            for (int i = 0; i < _tuning.DonationCents.Length; i++)
            {
                pick -= _tuning.DonationWeights[i];
                if (pick < 0) return _tuning.DonationCents[i];
            }
            return _tuning.DonationCents[_tuning.DonationCents.Length - 1];
        }
    }
}
