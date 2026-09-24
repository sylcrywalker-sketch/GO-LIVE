using System;
using System.Collections.Generic;
using GoLive.Items;
using GoLive.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GoLive.Inventory
{
    // Mirrors the one authoritative Inventory as a list of the stored items only, in Inventory order unless the owning
    // screen ranks the rows; capacity is shown by the owning screen. Every screen that shows the Inventory (the TAB
    // overlay, the PC Build Mode parts panel) binds its own list and listens to its drag or click events.
    [DisallowMultipleComponent]
    public sealed class InventoryListView : MonoBehaviour
    {
        private const string EmptyTitleKey = "inventory.empty_title";
        private const string EmptyHintKey = "inventory.empty_hint";

        [SerializeField] private InventoryItemView[] rows = Array.Empty<InventoryItemView>();
        [SerializeField] private RectTransform rowContent;
        [SerializeField] private LayoutElement viewport;
        [SerializeField, Min(1f)] private float maxViewportHeight = 380f;
        [SerializeField] private GameObject emptyState;
        [SerializeField] private TMP_Text emptyTitle;
        [SerializeField] private TMP_Text emptyHint;

        public event Action<InventoryItemView, PointerEventData> ItemDragStarted;
        public event Action<InventoryItemView, PointerEventData> ItemDragged;
        public event Action<InventoryItemView, PointerEventData> ItemDragEnded;
        public event Action<InventoryItemView> ItemClicked;

        private readonly List<int> _order = new();

        private PlayerInventory _owner;
        private LocalizationContext _localization;
        private Action<InventoryItemView, ItemDefinition> _annotate;
        private Func<ItemDefinition, int> _rank;

        private void OnDestroy()
        {
            Unbind();
        }

        public bool Bind(PlayerInventory owner, LocalizationContext localization)
        {
            if (owner == _owner)
                return owner != null;

            Unbind();

            if (owner == null ||
                owner.Inventory == null ||
                localization == null ||
                owner.Inventory.Capacity > rows.Length ||
                Array.IndexOf(rows, null) >= 0 ||
                rowContent == null ||
                viewport == null ||
                emptyState == null ||
                emptyTitle == null ||
                emptyHint == null)
            {
                Debug.LogError($"{nameof(InventoryListView)} on {name} needs an initialized Player Inventory, localization, its layout references and one row per Inventory slot.", this);
                return false;
            }

            _owner = owner;
            _localization = localization;
            _owner.Inventory.Changed += Render;

            for (int i = 0; i < rows.Length; i++)
            {
                rows[i].DragStarted += ForwardDragStarted;
                rows[i].Dragged += ForwardDragged;
                rows[i].DragEnded += ForwardDragEnded;
                rows[i].Clicked += ForwardClicked;
            }

            Render();
            return true;
        }

        public void Unbind()
        {
            if (_owner == null)
                return;

            _owner.Inventory.Changed -= Render;

            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i] == null)
                    continue;

                rows[i].DragStarted -= ForwardDragStarted;
                rows[i].Dragged -= ForwardDragged;
                rows[i].DragEnded -= ForwardDragEnded;
                rows[i].Clicked -= ForwardClicked;
            }

            _owner = null;
            _localization = null;
            _annotate = null;
            _rank = null;
        }

        // Optional: the owning screen annotates every shown row after it is drawn (null draws plain rows).
        public void SetRowAnnotator(Action<InventoryItemView, ItemDefinition> annotate)
        {
            _annotate = annotate;
            Render();
        }

        // Optional: the owning screen ranks the rows, lowest first, Inventory order within a rank (null keeps Inventory order).
        // Presentation only: the Inventory's own order never changes.
        public void SetRowOrder(Func<ItemDefinition, int> rank)
        {
            _rank = rank;
            Render();
        }

        // Rows exist only for stored items; the viewport grows with them up to maxViewportHeight, then scrolls.
        public void Render()
        {
            if (_owner == null)
                return;

            IReadOnlyList<ItemInstance> items = _owner.Inventory.Items;
            int shown = 0;

            _order.Clear();

            for (int i = 0; i < items.Count; i++)
                _order.Add(i);

            if (_rank != null)
                _order.Sort((a, b) => Rank(items[a]) != Rank(items[b]) ? Rank(items[a]).CompareTo(Rank(items[b])) : a.CompareTo(b));

            for (int i = 0; i < rows.Length; i++)
            {
                ItemDefinition definition = null;
                ItemInstance item = i < items.Count ? items[_order[i]] : null;
                bool filled = item != null && _owner.TryGetDefinition(item.InstanceId, out definition);

                if (filled)
                {
                    rows[i].Show(item.InstanceId, definition, _localization);
                    _annotate?.Invoke(rows[i], definition);
                    shown++;
                }
                else
                {
                    rows[i].Clear();
                }

                rows[i].gameObject.SetActive(filled);
            }

            emptyTitle.text = _localization.Text(EmptyTitleKey);
            emptyHint.text = _localization.Text(EmptyHintKey);
            emptyState.SetActive(shown == 0);
            viewport.gameObject.SetActive(shown > 0);

            if (shown == 0 || !rowContent.gameObject.activeInHierarchy)
                return;

            LayoutRebuilder.ForceRebuildLayoutImmediate(rowContent);
            viewport.preferredHeight = Mathf.Min(rowContent.rect.height, maxViewportHeight);
        }

        private int Rank(ItemInstance item)
        {
            return _owner.TryGetDefinition(item.InstanceId, out ItemDefinition definition) ? _rank(definition) : int.MaxValue;
        }

        private void ForwardDragStarted(InventoryItemView row, PointerEventData eventData) => ItemDragStarted?.Invoke(row, eventData);
        private void ForwardDragged(InventoryItemView row, PointerEventData eventData) => ItemDragged?.Invoke(row, eventData);
        private void ForwardDragEnded(InventoryItemView row, PointerEventData eventData) => ItemDragEnded?.Invoke(row, eventData);
        private void ForwardClicked(InventoryItemView row) => ItemClicked?.Invoke(row);
    }
}
