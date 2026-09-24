using System;

namespace GoLive.PcBuilding
{
    public enum PcBuildModePhase
    {
        Closed,
        Opening,
        Open,
        Closing
    }

    // The fixed timeline of entering and leaving PC Build Mode: first the PC is brought to the player, then the side
    // panel comes off (slightly overlapping); leaving plays the same timeline backwards. One time value drives every
    // amount, so reversing half-way (Esc while opening) is always continuous. Parts can only be worked on in Open.
    public sealed class PcBuildModeSequence
    {
        public PcBuildModePhase Phase { get; private set; } = PcBuildModePhase.Closed;
        public bool IsActive => Phase != PcBuildModePhase.Closed;
        public bool IsInteractive => Phase == PcBuildModePhase.Open;

        // 0 = the PC in its place on the desk, 1 = the PC in front of the player. Every amount is exact at the ends.
        public float ApproachAmount => _time >= _approachSeconds ? 1f : Clamp01(_time / _approachSeconds);

        // 0 = side panel on, 1 = side panel removed.
        public float CoverAmount => _time >= _totalSeconds ? 1f : Clamp01((_time - _coverStart) / _coverSeconds);

        // The whole timeline, 0..1: the HUD swap follows it.
        public float Progress => _time >= _totalSeconds ? 1f : Clamp01(_time / _totalSeconds);

        private readonly float _approachSeconds;
        private readonly float _coverSeconds;
        private readonly float _coverStart;
        private readonly float _totalSeconds;
        private float _time;

        public PcBuildModeSequence(float approachSeconds, float coverSeconds, float overlapSeconds)
        {
            if (approachSeconds <= 0f || coverSeconds <= 0f || overlapSeconds < 0f || overlapSeconds >= Math.Min(approachSeconds, coverSeconds))
                throw new ArgumentOutOfRangeException(nameof(overlapSeconds), "Durations must be positive and the overlap shorter than both.");

            _approachSeconds = approachSeconds;
            _coverSeconds = coverSeconds;
            _coverStart = approachSeconds - overlapSeconds;
            _totalSeconds = _coverStart + coverSeconds;
        }

        public bool Begin()
        {
            if (Phase is PcBuildModePhase.Opening or PcBuildModePhase.Open)
                return false;

            Phase = PcBuildModePhase.Opening;
            return true;
        }

        public bool End()
        {
            if (Phase is PcBuildModePhase.Closed or PcBuildModePhase.Closing)
                return false;

            Phase = PcBuildModePhase.Closing;
            return true;
        }

        public PcBuildModePhase Tick(float deltaSeconds)
        {
            if (deltaSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));

            if (Phase == PcBuildModePhase.Opening)
            {
                _time = Math.Min(_totalSeconds, _time + deltaSeconds);

                if (_time >= _totalSeconds)
                    Phase = PcBuildModePhase.Open;
            }
            else if (Phase == PcBuildModePhase.Closing)
            {
                _time = Math.Max(0f, _time - deltaSeconds);

                if (_time <= 0f)
                    Phase = PcBuildModePhase.Closed;
            }

            return Phase;
        }

        // Straight back to the player's view with nothing left open (teardown only).
        public void Reset()
        {
            _time = 0f;
            Phase = PcBuildModePhase.Closed;
        }

        private static float Clamp01(float value)
        {
            return value < 0f ? 0f : value > 1f ? 1f : value;
        }
    }
}
