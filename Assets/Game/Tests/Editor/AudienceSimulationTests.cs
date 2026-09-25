using System;
using System.Collections.Generic;
using System.Linq;
using GoLive.Desktop;
using GoLive.Economy;
using NUnit.Framework;

namespace GoLive.Tests
{
    // Plain C# model tests: no Unity objects, deterministic seeds.
    public sealed class AudienceSimulationTests
    {
        private const int Prime = 18 * 60;
        private const int Noon = 12 * 60;
        private const int Night = 3 * 60;

        [TestCase(true)]
        [TestCase(false)]
        public void InitialViewerCountIsValid(bool firstStream)
        {
            for (ulong seed = 1; seed <= 50; seed++)
            {
                var audience = new AudienceSimulation(new AudienceTuning(), seed, 0, firstStream);
                Assert.That(audience.CurrentViewers, Is.Zero, "nobody watches before the broadcast is live");
                audience.GoLive();
                if (firstStream) Assert.That(audience.CurrentViewers, Is.InRange(1, 3));
                else Assert.That(audience.CurrentViewers, Is.Zero);
                Assert.That(audience.PeakViewers, Is.EqualTo(audience.CurrentViewers));
            }
        }

        [Test]
        public void ViewerCountMovesOrganicallyUpAndDownAndNeverGoesNegative()
        {
            var audience = Live(7, 40, false);
            var series = Series(audience, 1200, Conditions(Prime));
            Assert.That(series.Min(), Is.GreaterThanOrEqualTo(0));
            int rises = 0, falls = 0;
            for (int i = 1; i < series.Count; i++)
            {
                if (series[i] > series[i - 1]) rises++;
                if (series[i] < series[i - 1]) falls++;
            }
            Assert.That(rises, Is.GreaterThan(5), "viewers arrive during the stream");
            Assert.That(falls, Is.GreaterThan(5), "viewers also leave; the count is not a scripted ramp");
            Assert.That(series.Distinct().Count(), Is.GreaterThan(3));
            TestContext.WriteLine("first minutes: " + string.Join(" ", series.Take(240).Where((_, i) => i % 20 == 0)));
        }

        [Test]
        public void AverageAndPeakAreComputedFromTheSimulatedSeconds()
        {
            var audience = Live(11, 20, true);
            int opening = audience.CurrentViewers;
            var series = Series(audience, 600, Conditions(Prime));
            Assert.That(audience.SimulatedSeconds, Is.EqualTo(600));
            Assert.That(audience.ViewerSeconds, Is.EqualTo(series.Sum()));
            Assert.That(audience.AverageViewers, Is.EqualTo(series.Sum() / 600.0).Within(1e-9));
            Assert.That(audience.PeakViewers, Is.EqualTo(Math.Max(opening, series.Max())));
            Assert.That(opening, Is.GreaterThanOrEqualTo(1), "the first stream opens with discovered viewers");
        }

        [Test]
        public void SameSeedReproducesTheAudienceAndOtherSeedsDiffer()
        {
            var a = Series(Live(99, 30, false), 900, Conditions(Prime));
            var b = Series(Live(99, 30, false), 900, Conditions(Prime));
            var c = Series(Live(100, 30, false), 900, Conditions(Prime));
            Assert.That(a, Is.EqualTo(b));
            Assert.That(a, Is.Not.EqualTo(c));
        }

        [Test]
        public void FrameSplitDoesNotChangeTheAudience()
        {
            var whole = Live(5, 25, true);
            var split = Live(5, 25, true);
            var wholeDonations = new List<long>();
            var splitDonations = new List<long>();
            AudienceConditions conditions = Conditions(Prime);
            whole.Advance(600, conditions, wholeDonations);
            for (int i = 0; i < 2400; i++) split.Advance(.25, conditions, splitDonations);
            Assert.That(split.CurrentViewers, Is.EqualTo(whole.CurrentViewers));
            Assert.That(split.PeakViewers, Is.EqualTo(whole.PeakViewers));
            Assert.That(split.ViewerSeconds, Is.EqualTo(whole.ViewerSeconds));
            Assert.That(split.Follows, Is.EqualTo(whole.Follows));
            Assert.That(split.ChatMessages, Is.EqualTo(whole.ChatMessages));
            Assert.That(splitDonations, Is.EqualTo(wholeDonations));
        }

        [Test]
        public void DailyPeakLiesInPrimeTimeAndNightIsWeakerButViable()
        {
            var tuning = new AudienceTuning();
            int peakMinute = Enumerable.Range(0, 1440).OrderByDescending(minute => AudienceFactors.DailyMultiplier(minute, tuning)).First();
            Assert.That(peakMinute, Is.InRange(15 * 60, 21 * 60));
            Assert.That(AudienceFactors.DailyMultiplier(Prime, tuning), Is.GreaterThan(AudienceFactors.DailyMultiplier(Noon, tuning)));
            Assert.That(AudienceFactors.DailyMultiplier(Noon, tuning), Is.GreaterThan(AudienceFactors.DailyMultiplier(Night, tuning)));
            Assert.That(AudienceFactors.DailyMultiplier(Night, tuning), Is.GreaterThan(0), "streaming at night remains possible");
            Assert.That(AudienceFactors.DailyMultiplier(1439, tuning), Is.EqualTo(AudienceFactors.DailyMultiplier(0, tuning)).Within(.01));
        }

        [Test]
        public void PrimeTimeImprovesPotentialAndNightLowersIt()
        {
            double prime = MeanAverage(Conditions(Prime), 30);
            double noon = MeanAverage(Conditions(Noon), 30);
            double night = MeanAverage(Conditions(Night), 30);
            TestContext.WriteLine($"average viewers prime={prime:0.00} noon={noon:0.00} night={night:0.00}");
            Assert.That(prime, Is.GreaterThan(noon));
            Assert.That(noon, Is.GreaterThan(night));
            Assert.That(night, Is.GreaterThan(0), "outside prime time a stream still gets viewers");
        }

        [Test]
        public void BadInternetHurtsRetention()
        {
            var tuning = new AudienceTuning();
            AudienceFactors minimum = AudienceFactors.Evaluate(Conditions(Prime, upload: 3), tuning);
            AudienceFactors comfortable = AudienceFactors.Evaluate(Conditions(Prime, upload: 6), tuning);
            Assert.That(minimum.Stability, Is.LessThan(comfortable.Stability));
            Assert.That(minimum.Retention, Is.LessThan(comfortable.Retention));
            Assert.That(MeanAverage(Conditions(Prime, upload: 3), 30), Is.LessThan(MeanAverage(Conditions(Prime, upload: 6), 30)));
        }

        [Test]
        public void HardwareChangesStabilityAndQualityNeverViewersDirectly()
        {
            var tuning = new AudienceTuning();
            AudienceFactors processor = AudienceFactors.Evaluate(Conditions(Prime, gpu: false), tuning);
            AudienceFactors graphics = AudienceFactors.Evaluate(Conditions(Prime, gpu: true), tuning);
            Assert.That(processor.Encoding, Is.LessThan(graphics.Encoding), "a graphics card stabilizes Medium encoding");
            Assert.That(processor.TimeOfDay, Is.EqualTo(graphics.TimeOfDay));
            Assert.That(processor.QualityAttraction, Is.EqualTo(graphics.QualityAttraction), "a GPU is not a viewer bonus");
            Assert.That(AudienceFactors.Evaluate(Conditions(Prime, StreamQuality.Low, 5, false), tuning).Encoding, Is.EqualTo(1),
                "Low quality does not strain the processor");
            // With a graphics card the higher quality it allows attracts more viewers than Medium.
            Assert.That(AudienceFactors.Evaluate(Conditions(Prime, StreamQuality.High, 12, true), tuning).QualityAttraction,
                Is.GreaterThan(graphics.QualityAttraction));
        }

        [Test]
        public void MissingInGameMicrophoneDegradesAudioButTheAudienceContinues()
        {
            var tuning = new AudienceTuning();
            AudienceFactors withMic = AudienceFactors.Evaluate(Conditions(Prime), tuning);
            AudienceFactors withoutMic = AudienceFactors.Evaluate(Conditions(Prime, microphone: false), tuning);
            Assert.That(withoutMic.Audio, Is.LessThan(withMic.Audio));
            Assert.That(withoutMic.Retention, Is.LessThan(withMic.Retention));
            var audience = Live(3, 30, false);
            Series(audience, 300, Conditions(Prime, microphone: false));
            Assert.That(audience.SimulatedSeconds, Is.EqualTo(300));
            Assert.That(audience.PeakViewers, Is.GreaterThan(0));
        }

        [Test]
        public void WebcamIsASmallOptionalEngagementModifier()
        {
            var tuning = new AudienceTuning();
            Assert.That(AudienceFactors.Evaluate(Conditions(Prime, webcam: true), tuning).Engagement, Is.EqualTo(tuning.WebcamEngagement).Within(1e-6));
            Assert.That(AudienceFactors.Evaluate(Conditions(Prime, webcam: false), tuning).Engagement, Is.EqualTo(1));
            var audience = Live(3, 30, false);
            Series(audience, 300, Conditions(Prime, webcam: false));
            Assert.That(audience.PeakViewers, Is.GreaterThan(0), "no webcam still streams");
        }

        [Test]
        public void FirstStreamBoostIsTemporaryAndOnlyWhenEligible()
        {
            double boosted = 0, regular = 0;
            for (ulong seed = 1; seed <= 20; seed++)
            {
                var first = Live(seed, 0, true);
                var later = Live(seed, 0, false);
                Assert.That(first.FirstStreamBoost, Is.True);
                Assert.That(later.FirstStreamBoost, Is.False);
                Series(first, 60, Conditions(Prime));
                Series(later, 60, Conditions(Prime));
                boosted += first.AverageViewers;
                regular += later.AverageViewers;
            }
            Assert.That(boosted, Is.GreaterThan(regular), "the onboarding boost gives early discovery");
            var tuning = new AudienceTuning();
            var longRun = Live(8, 0, true);
            Series(longRun, 1800, Conditions(Prime));
            double decayed = tuning.FirstStreamBoostViewers * Math.Exp(-longRun.SimulatedSeconds / tuning.FirstStreamBoostSeconds);
            Assert.That(decayed, Is.LessThan(.01), "the discovery boost fades instead of persisting");
        }

        [Test]
        public void OutcomesAreDrawnFromViewersOnly()
        {
            var tuning = new AudienceTuning { DiscoveryViewers = 0, FollowerTurnout = 0, FirstStreamBoostViewers = 0 };
            var empty = new AudienceSimulation(tuning, 1, 0, false);
            empty.GoLive();
            var donations = new List<long>();
            AudienceAdvance advance = empty.Advance(3600, Conditions(Prime), donations);
            Assert.That(advance.Steps, Is.EqualTo(3600));
            Assert.That(empty.PeakViewers, Is.Zero);
            Assert.That(empty.Follows + empty.Subscriptions + empty.ChatMessages, Is.Zero);
            Assert.That(donations, Is.Empty, "no audience, no money");
        }

        [Test]
        public void HugeDeltaIsBoundedAndKeepsAverageFinite()
        {
            var audience = Live(2, 50, false);
            var donations = new List<long>();
            AudienceAdvance advance = audience.Advance(double.MaxValue / 4, Conditions(Prime), donations);
            Assert.That(advance.Steps, Is.EqualTo(AudienceSimulation.MaximumStepsPerAdvance));
            Assert.That(double.IsNaN(audience.AverageViewers) || double.IsInfinity(audience.AverageViewers), Is.False);
            Assert.That(audience.SimulatedSeconds, Is.GreaterThan(1e300));
            Assert.That(donations.Count, Is.LessThan(100000));
        }

        [Test]
        public void FinishFreezesTheResult()
        {
            var audience = Live(4, 20, false);
            Series(audience, 120, Conditions(Prime));
            audience.Finish();
            Assert.That(audience.IsFinished, Is.True);
            Assert.Throws<InvalidOperationException>(() => audience.Advance(1, Conditions(Prime), new List<long>()));
            Assert.Throws<InvalidOperationException>(() => audience.GoLive());
        }

        [Test]
        public void InvalidTuningAndInputsAreRejected()
        {
            Assert.That(new AudienceTuning().Validate(), Is.Null);
            Assert.That(new AudienceTuning { MeanWatchSeconds = 0 }.Validate(), Is.Not.Null);
            Assert.That(new AudienceTuning { DailyCurve = new float[3] }.Validate(), Is.Not.Null);
            Assert.That(new AudienceTuning { StabilityAtMinimumUpload = 1.5f }.Validate(), Is.Not.Null);
            Assert.That(new AudienceTuning { DonationRate = float.NaN }.Validate(), Is.Not.Null);
            Assert.That(new AudienceTuning { DonationWeights = new[] { 1f } }.Validate(), Is.Not.Null);
            var audience = Live(1, 5, false);
            Assert.Throws<ArgumentOutOfRangeException>(() => audience.Advance(-1, Conditions(Prime), new List<long>()));
            Assert.Throws<ArgumentOutOfRangeException>(() => audience.Advance(double.NaN, Conditions(Prime), new List<long>()));
            Assert.Throws<ArgumentOutOfRangeException>(() => AudienceFactors.DailyMultiplier(1440, new AudienceTuning()));
        }

        [Test]
        public void DonationCreditsTheWalletExactlyOnce()
        {
            var account = new DonationAccount();
            var wallet = new Wallet(1000);
            int received = 0;
            account.Received += _ => received++;
            using (new DonationPayout(account, wallet))
            {
                Assert.That(account.Receive("stream-a.donation.1", "PixelFox", 500), Is.Null);
                Assert.That(account.Receive("stream-a.donation.1", "PixelFox", 500), Is.Null, "a repeated callback is idempotent");
                Assert.That(wallet.BalanceCents, Is.EqualTo(1500));
                Assert.That(received, Is.EqualTo(1));
                var restored = new DonationAccount();
                using (new DonationPayout(restored, wallet)) restored.Restore(account.Capture());
                Assert.That(wallet.BalanceCents, Is.EqualTo(1500), "restoring saved history never pays again");
            }
            Assert.That(account.Receive("stream-a.donation.2", "NightOwl", 200), Is.Null);
            Assert.That(wallet.BalanceCents, Is.EqualTo(1500), "a disposed payout no longer credits");
        }

        private static AudienceSimulation Live(ulong seed, long followers, bool firstStream)
        {
            var audience = new AudienceSimulation(new AudienceTuning(), seed, followers, firstStream);
            audience.GoLive();
            return audience;
        }

        private static List<int> Series(AudienceSimulation audience, int seconds, AudienceConditions conditions)
        {
            var donations = new List<long>();
            var series = new List<int>(seconds);
            for (int i = 0; i < seconds; i++)
            {
                audience.Advance(1, conditions, donations);
                series.Add(audience.CurrentViewers);
            }
            return series;
        }

        private static double MeanAverage(AudienceConditions conditions, int streams)
        {
            double total = 0;
            for (ulong seed = 1; seed <= (ulong)streams; seed++)
            {
                var audience = Live(seed, 60, false);
                Series(audience, 1200, conditions);
                total += audience.AverageViewers;
            }
            return total / streams;
        }

        internal static AudienceConditions Conditions(int minute, StreamQuality quality = StreamQuality.Medium, float upload = 5,
            bool gpu = false, bool microphone = true, bool webcam = false)
            => new(minute, quality, upload, gpu, microphone, webcam);
    }
}
