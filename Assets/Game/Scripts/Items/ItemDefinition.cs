using GoLive.PcBuilding;
using UnityEngine;

namespace GoLive.Items
{
    public enum ItemCarryStyle
    {
        OneHanded,
        TwoHanded
    }

    public enum ItemCategory
    {
        Food,
        Electronics,
        Household
    }

    [CreateAssetMenu(fileName = "ItemDefinition", menuName = "GO! LIVE/Items/Item Definition")]
    public sealed class ItemDefinition : ScriptableObject
    {
        [SerializeField] private string itemId;
        [SerializeField] private string nameLocalizationKey;
        [SerializeField] private ItemCategory category;
        [SerializeField] private GameObject worldPrefab;
        [SerializeField] private Sprite iconOverride;
        [SerializeField, HideInInspector] private Sprite generatedIcon;
        [SerializeField] private bool canStoreInInventory = true;
        [SerializeField] private ItemCarryStyle carryStyle = ItemCarryStyle.OneHanded;
        [SerializeField] private Vector3 carryLocalPosition;
        [SerializeField] private Vector3 carryLocalEulerAngles;

        [Tooltip("Only for PC hardware: what kind of component this is and what it plugs into. Empty for everything else.")]
        [SerializeField] private PcComponentSpec pcComponent;

        public string ItemId => itemId;
        public string NameLocalizationKey => nameLocalizationKey;
        public ItemCategory Category => category;
        public GameObject WorldPrefab => worldPrefab;
        public Sprite InventoryIcon => iconOverride != null ? iconOverride : generatedIcon;
        public bool CanStoreInInventory => canStoreInInventory;
        public ItemCarryStyle CarryStyle => carryStyle;
        public Vector3 CarryLocalPosition => carryLocalPosition;
        public Vector3 CarryLocalEulerAngles => carryLocalEulerAngles;
        public PcComponentSpec PcComponent => pcComponent;

        public bool TryGetRuntimePrefab(out WorldItem prefab)
        {
            prefab = worldPrefab != null ? worldPrefab.GetComponent<WorldItem>() : null;

            if (prefab != null &&
                prefab.Definition == this &&
                prefab.IsRuntime &&
                IsValidItemId(itemId) &&
                prefab.GetComponentInChildren<Collider>(true) != null)
            {
                return true;
            }

            prefab = null;
            return false;
        }

        public static bool IsValidItemId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 80)
                return false;

            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];

                if (character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_')
                    continue;

                return false;
            }

            return true;
        }
    }
}