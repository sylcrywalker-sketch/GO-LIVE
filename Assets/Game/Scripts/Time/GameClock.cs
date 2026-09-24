using System;

namespace GoLive.GameTime
{
    public enum DayPhase
    {
        Morning,
        Day,
        Evening,
        Night
    }

    public readonly struct DayPhaseSchedule
    {
        public int MorningStartMinute { get; }
        public int DayStartMinute { get; }
        public int EveningStartMinute { get; }
        public int NightStartMinute { get; }

        public DayPhaseSchedule(int morningStartMinute, int dayStartMinute, int eveningStartMinute, int nightStartMinute)
        {
            if (morningStartMinute < 0 || morningStartMinute >= GameTimeSnapshot.MinutesPerDay)
                throw new ArgumentOutOfRangeException(nameof(morningStartMinute));

            if (dayStartMinute <= morningStartMinute || dayStartMinute >= GameTimeSnapshot.MinutesPerDay)
                throw new ArgumentOutOfRangeException(nameof(dayStartMinute));

            if (eveningStartMinute <= dayStartMinute || eveningStartMinute >= GameTimeSnapshot.MinutesPerDay)
                throw new ArgumentOutOfRangeException(nameof(eveningStartMinute));

            if (nightStartMinute <= eveningStartMinute || nightStartMinute >= GameTimeSnapshot.MinutesPerDay)
                throw new ArgumentOutOfRangeException(nameof(nightStartMinute));

            MorningStartMinute = morningStartMinute;
            DayStartMinute = dayStartMinute;
            EveningStartMinute = eveningStartMinute;
            NightStartMinute = nightStartMinute;
        }

        public DayPhase GetPhase(int minuteOfDay)
        {
            if (minuteOfDay < 0 || minuteOfDay >= GameTimeSnapshot.MinutesPerDay)
                throw new ArgumentOutOfRangeException(nameof(minuteOfDay));

            if (minuteOfDay >= NightStartMinute || minuteOfDay < MorningStartMinute)
                return DayPhase.Night;

            if (minuteOfDay < DayStartMinute)
                return DayPhase.Morning;

            if (minuteOfDay < EveningStartMinute)
                return DayPhase.Day;

            return DayPhase.Evening;
        }

        public DayPhase GetNextPhase(DayPhase phase)
        {
            return phase switch
            {
                DayPhase.Morning => DayPhase.Day,
                DayPhase.Day => DayPhase.Evening,
                DayPhase.Evening => DayPhase.Night,
                DayPhase.Night => DayPhase.Morning,
                _ => throw new ArgumentOutOfRangeException(nameof(phase))
            };
        }

        public float GetPhaseProgress(int minuteOfDay)
        {
            DayPhase phase = GetPhase(minuteOfDay);

            return phase switch
            {
                DayPhase.Morning => InverseLerp(MorningStartMinute, DayStartMinute, minuteOfDay),
                DayPhase.Day => InverseLerp(DayStartMinute, EveningStartMinute, minuteOfDay),
                DayPhase.Evening => InverseLerp(EveningStartMinute, NightStartMinute, minuteOfDay),
                DayPhase.Night => GetNightProgress(minuteOfDay),
                _ => 0f
            };
        }

        private float GetNightProgress(int minuteOfDay)
        {
            int nightDuration = GameTimeSnapshot.MinutesPerDay - NightStartMinute + MorningStartMinute;
            int elapsed = minuteOfDay >= NightStartMinute
                ? minuteOfDay - NightStartMinute
                : GameTimeSnapshot.MinutesPerDay - NightStartMinute + minuteOfDay;

            return elapsed / (float)nightDuration;
        }

        private static float InverseLerp(int from, int to, int value)
        {
            if (from == to)
                return 0f;

            return Math.Clamp((value - from) / (float)(to - from), 0f, 1f);
        }
    }

    public readonly struct GameTimeSnapshot
    {
        public const long SecondsPerMinute = 60;
        public const long SecondsPerHour = 3600;
        public const long SecondsPerDay = 86400;
        public const int MinutesPerDay = 1440;

        public long TotalSeconds { get; }
        public int Day => (int)(TotalSeconds / SecondsPerDay) + 1;
        public int Hour => (int)(TotalSeconds % SecondsPerDay / SecondsPerHour);
        public int Minute => (int)(TotalSeconds % SecondsPerHour / SecondsPerMinute);
        public int Second => (int)(TotalSeconds % SecondsPerMinute);
        public int MinuteOfDay => Hour * 60 + Minute;

        // The game calendar starts on a Monday: Day 1 is Monday, Day 7 Sunday.
        public DayOfWeek DayOfWeek => (DayOfWeek)(Day % 7);

        public GameTimeSnapshot(long totalSeconds)
        {
            if (totalSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(totalSeconds));

            TotalSeconds = totalSeconds;
        }
    }

    public readonly struct GameTimeAdvance
    {
        public GameTimeSnapshot Previous { get; }
        public GameTimeSnapshot Current { get; }
        public long AdvancedSeconds => Current.TotalSeconds - Previous.TotalSeconds;

        public GameTimeAdvance(GameTimeSnapshot previous, GameTimeSnapshot current)
        {
            Previous = previous;
            Current = current;
        }
    }

    public sealed class GameClock
    {
        public GameTimeSnapshot Current { get; private set; }

        public event Action<GameTimeAdvance> Advanced;
        public event Action<GameTimeSnapshot> MinuteChanged;
        public event Action<int, int> DayChanged;

        private double _fractionalSeconds;

        public GameClock(int startingDay, int startingHour, int startingMinute)
        {
            if (startingDay < 1)
                throw new ArgumentOutOfRangeException(nameof(startingDay));

            if (startingHour is < 0 or > 23)
                throw new ArgumentOutOfRangeException(nameof(startingHour));

            if (startingMinute is < 0 or > 59)
                throw new ArgumentOutOfRangeException(nameof(startingMinute));

            long totalSeconds =
                (startingDay - 1L) * GameTimeSnapshot.SecondsPerDay +
                startingHour * GameTimeSnapshot.SecondsPerHour +
                startingMinute * GameTimeSnapshot.SecondsPerMinute;

            Current = new GameTimeSnapshot(totalSeconds);
        }

        public void AdvanceSeconds(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0d)
                throw new ArgumentOutOfRangeException(nameof(seconds));

            if (seconds == 0d)
                return;

            _fractionalSeconds += seconds;

            long wholeSeconds = (long)Math.Floor(_fractionalSeconds);

            if (wholeSeconds <= 0)
                return;

            _fractionalSeconds -= wholeSeconds;

            GameTimeSnapshot previous = Current;
            GameTimeSnapshot current = new(checked(previous.TotalSeconds + wholeSeconds));

            Current = current;

            Advanced?.Invoke(new GameTimeAdvance(previous, current));

            if (previous.Day != current.Day)
                DayChanged?.Invoke(previous.Day, current.Day);

            if (previous.TotalSeconds / GameTimeSnapshot.SecondsPerMinute != current.TotalSeconds / GameTimeSnapshot.SecondsPerMinute)
                MinuteChanged?.Invoke(current);
        }

        public void AdvanceMinutes(double minutes)
        {
            if (double.IsNaN(minutes) || double.IsInfinity(minutes) || minutes < 0d)
                throw new ArgumentOutOfRangeException(nameof(minutes));

            AdvanceSeconds(minutes * GameTimeSnapshot.SecondsPerMinute);
        }

        public void Restore(GameTimeSnapshot snapshot)
        {
            GameTimeSnapshot previous = Current;

            Current = snapshot;
            _fractionalSeconds = 0d;

            if (previous.Day != snapshot.Day)
                DayChanged?.Invoke(previous.Day, snapshot.Day);

            MinuteChanged?.Invoke(snapshot);
        }
    }
}