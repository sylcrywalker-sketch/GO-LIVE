using System;
using System.Collections.Generic;
using GoLive.Items;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GoLive.Inventory
{
    // Mirrors the one authoritative Inventory into slot views, in Inventory order. Any screen that shows the
    // Inventory (the TAB overlay now, the PC Workbench later) binds a grid and listens to its drag events.
    [DisallowMultipleComponent]
    public sealed class InventoryGridView : MonoBehaviour
    {
        [SerializeField] private InventoryItemView[] slots = Array.Empty<InventoryItemView>();

        public event Action<InventoryItemView, PointerEventData> ItemDragStarted;
        public event Action<InventoryItemView, PointerEventData> ItemDragged;
        public event Action<InventoryItemView, PointerEventData> ItemDragEnded;

        private PlayerInventory _owner;

        private void OnDestroy()
        {
            Unbind();
        }

        public bool Bind(PlayerInventory owner)
        {
            if (owner == _owner)
                return owner != null;

            Unbind();

            if (owner == null || owner.Inventory == null || owner.Inventory.Capacity > slots.Length || Array.IndexOf(slots, null) >= 0)
            {
                Debug.LogError($"{nameof(InventoryGridView)} on {name} needs an initialized Player Inventory and at least one slot view per Inventory slot.", this);
                return false;
            }

            _owner = owner;
            _owner.Inventory.Changed += Render;

            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].DragStarted += ForwardDragStarted;
                slots[i].Dragged += ForwardDragged;
                slots[i].DragEnded += ForwardDragEnded;
                slots[i].gameObject.SetActive(i < _owner.Inventory.Capacity);
            }

            Render();
            return true;
        }

        public void Unbind()
        {
            if (_owner == null)
                return;

            _owner.Inventory.Changed -= Render;

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null)
                    continue;

                slots[i].DragStarted -= ForwardDragStarted;
                slots[i].Dragged -= ForwardDragged;
                slots[i].DragEnded -= ForwardDragEnded;
            }

            _owner = null;
        }

        public void Render()
        {
            if (_owner == null)
                return;

            IReadOnlyList<ItemInstance> items = _owner.Inventory.Items;

            for (int i = 0; i < slots.Length; i++)
            {
                if (i < items.Count && _owner.TryGetDefinition(items[i].InstanceId, out ItemDefinition definition))
                    slots[i].Show(items[i].InstanceId, definition);
                else
                    slots[i].Clear();
            }
        }

        private void ForwardDragStarted(InventoryItemView slot, PointerEventData eventData) => ItemDragStarted?.Invoke(slot, eventData);
        private void ForwardDragged(InventoryItemView slot, PointerEventData eventData) => ItemDragged?.Invoke(slot, eventData);
        private void ForwardDragEnded(InventoryItemView slot, PointerEventData eventData) => ItemDragEnded?.Invoke(slot, eventData);
    }
}
