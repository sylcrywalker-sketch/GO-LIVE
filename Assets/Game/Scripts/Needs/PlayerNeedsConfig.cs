using UnityEngine;

namespace GoLive.Needs
{
    [CreateAssetMenu(fileName = "PlayerNeedsConfig", menuName = "GO! LIVE/Needs/Player Needs Config")]
    public sealed class PlayerNeedsConfig : ScriptableObject
    {
        [Header("Hunger")]
        [field: SerializeField, Min(1f)] public float MaxHunger { get; private set; } = 100f;
        [field: SerializeField, Min(0f)] public float StartingHunger { get; private set; } = 20f;
        [field: SerializeField, Min(0f)] public float HungerPerGameHour { get; private set; } = 4f;

        [Header("Concentration")]
        [field: SerializeField, Min(1f)] public float MaxConcentration { get; private set; } = 100f;
        [field: SerializeField, Min(0f)] public float StartingConcentration { get; private set; } = 100f;

        public bool IsValid =>
            MaxHunger > 0f &&
            StartingHunger >= 0f &&
            StartingHunger <= MaxHunger &&
            HungerPerGameHour >= 0f &&
            MaxConcentration > 0f &&
            StartingConcentration >= 0f &&
            StartingConcentration <= MaxConcentration;

        public PlayerNeedsRules CreateRules()
        {
            return new PlayerNeedsRules(
                MaxHunger,
                StartingHunger,
                HungerPerGameHour,
                MaxConcentration,
                StartingConcentration);
        }
    }
}