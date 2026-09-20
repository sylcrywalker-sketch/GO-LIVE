using UnityEngine;

namespace GoLive.Economy
{
    [CreateAssetMenu(fileName = "EconomyConfig", menuName = "GO! LIVE/Economy/Economy Config")]
    public sealed class EconomyConfig : ScriptableObject
    {
        [field: SerializeField] public long StartingBalanceCents { get; private set; } = 2500;

        public bool IsValid => StartingBalanceCents >= 0;
    }
}