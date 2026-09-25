using System;

namespace GoLive.Desktop
{
    // Deterministic, allocation-free random stream for audience outcomes (SplitMix64). Each broadcast gets
    // its own seed, so the same seed always reproduces the same audience; nothing else draws from it.
    public sealed class AudienceRandom
    {
        private const double MaximumMean = 1e8;
        private ulong _state;

        public AudienceRandom(ulong seed) => _state = seed;

        public static AudienceRandom FromEntropy()
        {
            byte[] bytes = Guid.NewGuid().ToByteArray();
            return new AudienceRandom(BitConverter.ToUInt64(bytes, 0) ^ BitConverter.ToUInt64(bytes, 8));
        }

        public ulong NextUInt64()
        {
            _state += 0x9E3779B97F4A7C15UL;
            return Mix(_state);
        }

        // Uniform in [0, 1).
        public double NextDouble() => (NextUInt64() >> 11) * (1.0 / 9007199254740992.0);

        // Uniform in [0, maxExclusive).
        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            return (int)(NextUInt64() % (ulong)maxExclusive);
        }

        // Number of independent events in one interval with the given expected count.
        public int Poisson(double mean)
        {
            if (!(mean > 0)) return 0;
            if (mean > MaximumMean) mean = MaximumMean;
            if (mean < 30)
            {
                double limit = Math.Exp(-mean);
                double product = NextDouble();
                int count = 0;
                while (product > limit)
                {
                    count++;
                    product *= NextDouble();
                }
                return count;
            }
            return (int)Math.Max(0, Math.Round(mean + Math.Sqrt(mean) * Normal()));
        }

        // Successes among independent trials; exact for small groups, normal approximation for crowds.
        public int Binomial(int trials, double probability)
        {
            if (trials <= 0 || !(probability > 0)) return 0;
            if (probability >= 1) return trials;
            if (trials <= 64)
            {
                int count = 0;
                for (int i = 0; i < trials; i++)
                    if (NextDouble() < probability) count++;
                return count;
            }
            double mean = trials * probability;
            double deviation = Math.Sqrt(mean * (1 - probability));
            return (int)Math.Min(trials, Math.Max(0, Math.Round(mean + deviation * Normal())));
        }

        public double Normal()
        {
            double radius = Math.Sqrt(-2.0 * Math.Log(1.0 - NextDouble()));
            return radius * Math.Cos(2.0 * Math.PI * NextDouble());
        }

        // Stable per-item variation (chat line, sender) that does not consume the random stream, so it is
        // identical however many frames the same simulated time was split into.
        public static ulong Hash(ulong seed, ulong value) => Mix(seed ^ (value * 0x9E3779B97F4A7C15UL + 0x632BE59BD9B4E019UL));

        private static ulong Mix(ulong z)
        {
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}
