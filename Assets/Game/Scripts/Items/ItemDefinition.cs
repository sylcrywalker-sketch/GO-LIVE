using UnityEngine;

namespace GoLive.Items
{
    public enum ItemCarryStyle
    {
        OneHanded,
        TwoHanded
    }

    [CreateAssetMenu(fileName = "ItemDefinition", menuName = "GO! LIVE/Items/Item Definition")]
    public sealed class ItemDefinition : ScriptableObject
    {
        [field: SerializeField] public string ItemId { get; private set; }
        [field: SerializeField] public ItemCarryStyle CarryStyle { get; private set; } = ItemCarryStyle.OneHanded;
        [field: SerializeField] public Vector3 CarryLocalPosition { get; private set; }
        [field: SerializeField] public Vector3 CarryLocalEulerAngles { get; private set; }
    }
}