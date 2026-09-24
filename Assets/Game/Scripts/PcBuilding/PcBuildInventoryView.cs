using System;
using GoLive.Inventory;
using GoLive.Items;
using GoLive.Localization;
using GoLive.Player;
using TMPro;
using UnityEngine;

namespace GoLive.PcBuilding
{
    // The parts panel of PC Build Mode: the same Inventory as TAB, shown as a compact side list whose rows say what
    // each item means for this PC, plus the part in the hands. A click on a row or on the hands card is reported to
    // the Workbench, which decides what happens; this view never changes item state.
    [DisallowMultipleComponent]
    public sealed class PcBuildInventoryView : MonoBehaviour
    {
        private const string TitleKey = "inventory.title";
        private const string HandsKey = "inventory.hands";
        private const string HandsEmptyKey = "inventory.hands_empty";
        private const string HintKey = "pc.parts.hint";

        [SerializeField] private InventoryListView list;
        [SerializeField] private InventoryItemView heldItem;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text capacityText;
        [SerializeField] private TMP_Text handsTitleText;
        [SerializeField] private TMP_Text handsEmptyText;
        [SerializeField] private TMP_Text hintText;

        public event Action<string> PartClicked;
        public event Action HeldClicked;

        public bool IsBound => _inventory != null;

        private PlayerInventory _inventory;
        private PlayerCarry _carry;
        private LocalizationContext _localization;
        private Action<InventoryItemView, ItemDefinition> _annotate;

        private void Awake()
        {
            if (list != null && heldItem != null && titleText != null && capacityText != null && handsTitleText != null && handsEmptyText != null && hintText != null)
                return;

            Debug.LogError($"{nameof(PcBuildInventoryView)} on {name} has incomplete configuration.", this);
            enabled = false;
        }

        private void OnDestroy()
        {
            Unbind();
        }

        public bool Bind(PlayerInventory inventory, PlayerCarry carry, LocalizationContext localization, Action<InventoryItemView, ItemDefinition> annotate)
        {
            if (IsBound)
                return true;

            if (!enabled || inventory == null || carry == null || localization == null || !list.Bind(inventory, localization))
                return false;

            _inventory = inventory;
            _carry = carry;
            _localization = localization;
            _annotate = annotate;

            list.ItemClicked += HandleRowClicked;
            heldItem.Clicked += HandleHeldClicked;
            _inventory.Inventory.Changed += RenderCapacity;
            _carry.CarriedItemChanged += RenderHands;

            list.SetRowAnnotator(annotate);
            Render();
            return true;
        }

        public void Unbind()
        {
            if (!IsBound)
                return;

            list.ItemClicked -= HandleRowClicked;
            heldItem.Clicked -= HandleHeldClicked;
            _inventory.Inventory.Changed -= RenderCapacity;
            _carry.CarriedItemChanged -= RenderHands;
            list.Unbind();

            _inventory = null;
            _carry = null;
            _localization = null;
            _annotate = null;
        }

        // Everything again, e.g. after the PC changed and the notes on the rows changed with it.
        public void Render()
        {
            if (!IsBound)
                return;

            titleText.text = _localization.Text(TitleKey);
            handsTitleText.text = _localization.Text(HandsKey);
            handsEmptyText.text = _localization.Text(HandsEmptyKey);
            hintText.text = _localization.Text(HintKey);

            list.Render();
            RenderCapacity();
            RenderHands();
        }

        private void RenderCapacity()
        {
            capacityText.text = $"{_inventory.Inventory.Count} / {_inventory.Inventory.Capacity}";
        }

        private void RenderHands()
        {
            if (!_carry.HasItem)
            {
                heldItem.Clear();
                return;
            }

            WorldItem item = _carry.CarriedItem;
            heldItem.Show(item.Instance.InstanceId, item.Definition, _localization);
            _annotate?.Invoke(heldItem, item.Definition);
        }

        private void HandleRowClicked(InventoryItemView row)
        {
            PartClicked?.Invoke(row.InstanceId);
        }

        private void HandleHeldClicked(InventoryItemView view)
        {
            HeldClicked?.Invoke();
        }
    }
}
