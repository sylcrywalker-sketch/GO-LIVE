using UnityEngine;

namespace GoLive.GameTime
{
    [CreateAssetMenu(fileName = "GameTimeConfig", menuName = "GO! LIVE/Time/Game Time Config")]
    public sealed class GameTimeConfig : ScriptableObject
    {
        [Header("Start")]
        [field: SerializeField, Min(1)] public int StartingDay { get; private set; } = 1;
        [field: SerializeField, Range(0, 23)] public int StartingHour { get; private set; } = 7;
        [field: SerializeField, Range(0, 59)] public int StartingMinute { get; private set; }

        [Header("Scale")]
        [field: SerializeField, Min(0.01f)] public float GameMinutesPerRealMinute { get; private set; } = 15f;

        [Header("Day Phases")]
        [field: SerializeField, Range(0, 23)] public int MorningStartsAt { get; private set; } = 6;
        [field: SerializeField, Range(0, 23)] public int DayStartsAt { get; private set; } = 10;
        [field: SerializeField, Range(0, 23)] public int EveningStartsAt { get; private set; } = 18;
        [field: SerializeField, Range(0, 23)] public int NightStartsAt { get; private set; } = 22;

        public bool HasValidPhaseOrder =>
            MorningStartsAt < DayStartsAt &&
            DayStartsAt < EveningStartsAt &&
            EveningStartsAt < NightStartsAt;

        public DayPhaseSchedule CreatePhaseSchedule()
        {
            return new DayPhaseSchedule(
                MorningStartsAt * 60,
                DayStartsAt * 60,
                EveningStartsAt * 60,
                NightStartsAt * 60);
        }
    }
}