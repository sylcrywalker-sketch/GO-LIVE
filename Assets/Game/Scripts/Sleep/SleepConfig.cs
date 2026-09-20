using UnityEngine;

namespace GoLive.Sleep
{
    [CreateAssetMenu(fileName = "SleepConfig", menuName = "GO! LIVE/Sleep/Sleep Config")]
    public sealed class SleepConfig : ScriptableObject
    {
        [field: SerializeField, Min(0.1f)] public float MinimumSleepHours { get; private set; } = 1f;
        [field: SerializeField, Min(0.1f)] public float MaximumSleepHours { get; private set; } = 12f;
        [field: SerializeField, Min(0.1f)] public float DefaultSleepHours { get; private set; } = 8f;
        [field: SerializeField, Min(0f)] public float ConcentrationRestorePerGameHour { get; private set; } = 12.5f;

        public bool IsValid =>
            MinimumSleepHours > 0f &&
            MaximumSleepHours >= MinimumSleepHours &&
            DefaultSleepHours >= MinimumSleepHours &&
            DefaultSleepHours <= MaximumSleepHours &&
            ConcentrationRestorePerGameHour >= 0f;

        public SleepRules CreateRules()
        {
            return new SleepRules(
                MinimumSleepHours,
                MaximumSleepHours,
                DefaultSleepHours,
                ConcentrationRestorePerGameHour);
        }
    }
}