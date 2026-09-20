using System;
using GoLive.GameTime;
using GoLive.Needs;

namespace GoLive.Sleep
{
    public readonly struct SleepRules
    {
        public double MinimumHours { get; }
        public double MaximumHours { get; }
        public double DefaultHours { get; }
        public float ConcentrationRestorePerGameHour { get; }

        public SleepRules(double minimumHours, double maximumHours, double defaultHours, float concentrationRestorePerGameHour)
        {
            if (!IsFinitePositive(minimumHours))
                throw new ArgumentOutOfRangeException(nameof(minimumHours));

            if (!IsFinitePositive(maximumHours) || maximumHours < minimumHours)
                throw new ArgumentOutOfRangeException(nameof(maximumHours));

            if (!IsFinitePositive(defaultHours) || defaultHours < minimumHours || defaultHours > maximumHours)
                throw new ArgumentOutOfRangeException(nameof(defaultHours));

            if (float.IsNaN(concentrationRestorePerGameHour) || float.IsInfinity(concentrationRestorePerGameHour) || concentrationRestorePerGameHour < 0f)
                throw new ArgumentOutOfRangeException(nameof(concentrationRestorePerGameHour));

            MinimumHours = minimumHours;
            MaximumHours = maximumHours;
            DefaultHours = defaultHours;
            ConcentrationRestorePerGameHour = concentrationRestorePerGameHour;
        }

        private static bool IsFinitePositive(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value > 0d;
        }
    }

    public readonly struct SleepResult
    {
        public GameTimeSnapshot StartedAt { get; }
        public GameTimeSnapshot FinishedAt { get; }
        public double SleptMinutes { get; }
        public float ConcentrationRestored { get; }

        public SleepResult(GameTimeSnapshot startedAt, GameTimeSnapshot finishedAt, double sleptMinutes, float concentrationRestored)
        {
            StartedAt = startedAt;
            FinishedAt = finishedAt;
            SleptMinutes = sleptMinutes;
            ConcentrationRestored = concentrationRestored;
        }
    }

    public sealed class SleepService
    {
        public double DefaultSleepHours => _rules.DefaultHours;

        private readonly GameClock _clock;
        private readonly PlayerNeeds _needs;
        private readonly SleepRules _rules;

        public SleepService(GameClock clock, PlayerNeeds needs, SleepRules rules)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _needs = needs ?? throw new ArgumentNullException(nameof(needs));
            _rules = rules;
        }

        public bool CanSleep(double hours)
        {
            return !double.IsNaN(hours) &&
                   !double.IsInfinity(hours) &&
                   hours >= _rules.MinimumHours &&
                   hours <= _rules.MaximumHours;
        }

        public bool TrySleep(double hours, out SleepResult result)
        {
            result = default;

            if (!CanSleep(hours))
                return false;

            double minutes = hours * 60d;
            GameTimeSnapshot startedAt = _clock.Current;
            float concentrationBefore = _needs.Current.Concentration;

            _clock.AdvanceMinutes(minutes);

            float requestedRestore = _rules.ConcentrationRestorePerGameHour * (float)hours;

            if (requestedRestore > 0f)
                _needs.RestoreConcentration(requestedRestore);

            float concentrationRestored = _needs.Current.Concentration - concentrationBefore;

            result = new SleepResult(
                startedAt,
                _clock.Current,
                minutes,
                concentrationRestored);

            return true;
        }
    }
}