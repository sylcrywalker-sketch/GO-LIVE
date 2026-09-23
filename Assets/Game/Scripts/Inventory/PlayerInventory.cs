using System;
using System.Collections.Generic;
using GoLive.Items;
using GoLive.Player;
using UnityEngine;

namespace GoLive.Inventory
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerCarry))]
    public sealed class PlayerInventory : MonoBehaviour
    {
        [SerializeField, Min(1)] private int capacity = 12;
        [SerializeField] private Transform storedItemsRoot;

        public Inventory Inventory { get; private set; }

        internal Transform StoredItemsRoot => storedItemsRoot;

        private readonly Dictionary<string, WorldItem> _storedWorldItems = new(StringComparer.Ordinal);

        private PlayerCarry _playerCarry;

        private void Awake()
        {
            _playerCarry = GetComponent<PlayerCarry>();

            if (storedItemsRoot == null)
            {
                Debug.LogError($"{nameof(PlayerInventory)} on {name} requires a Stored Items Root.", this);
                enabled = false;
                return;
            }

            Inventory = new Inventory(capacity);
        }

        // Read-only: what TryStoreCarriedItem would do right now.
        public InventoryTransfer CheckStoreCarried()
        {
            if (!isActiveAndEnabled || Inventory == null)
                return InventoryTransfer.Unavailable;

            if (!_playerCarry.HasItem)
                return InventoryTransfer.NothingCarried;

            WorldItem item = _playerCarry.CarriedItem;

            if (!item.Definition.CanStoreInInventory)
                return InventoryTransfer.NotStorable;

            if (Inventory.IsFull)
                return InventoryTransfer.InventoryFull;

            return IsTracked(item.Instance.InstanceId) ? InventoryTransfer.Unavailable : InventoryTransfer.Store;
        }

        // Read-only: what TryTakeToCarry would do right now (Take into empty hands, or Swap with a storable held item).
        public InventoryTransfer CheckTakeToCarry(string instanceId)
        {
            if (!isActiveAndEnabled || Inventory == null)
                return InventoryTransfer.Unavailable;

            if (!Inventory.Contains(instanceId) || !_storedWorldItems.TryGetValue(instanceId, out WorldItem stored) || stored == null)
                return InventoryTransfer.NotInInventory;

            if (!_playerCarry.HasItem)
                return InventoryTransfer.Take;

            WorldItem held = _playerCarry.CarriedItem;

            if (!held.Definition.CanStoreInInventory)
                return InventoryTransfer.HandsBusy;

            return IsTracked(held.Instance.InstanceId) ? InventoryTransfer.Unavailable : InventoryTransfer.Swap;
        }

        public bool TryStoreCarriedItem()
        {
            if (CheckStoreCarried() != InventoryTransfer.Store)
                return false;

            string instanceId = _playerCarry.CarriedItem.Instance.InstanceId;

            if (!_playerCarry.TryStoreCarriedItem(storedItemsRoot, out WorldItem storedItem))
                return false;

            _storedWorldItems.Add(instanceId, storedItem);

            if (Inventory.TryAdd(storedItem.Instance))
                return true;

            _storedWorldItems.Remove(instanceId);

            if (!_playerCarry.TryCarryFromInventory(storedItem))
                Debug.LogError($"Failed to roll back inventory storage for item {instanceId}.", this);

            return false;
        }

        // Moves an Inventory item into the hands. Empty hands take it; hands holding a storable item swap
        // with it atomically (the held item takes the freed slot). Anything else changes nothing.
        public bool TryTakeToCarry(string instanceId)
        {
            return CheckTakeToCarry(instanceId) switch
            {
                InventoryTransfer.Take => TryTake(instanceId),
                InventoryTransfer.Swap => TrySwap(instanceId),
                _ => false
            };
        }

        public bool TryGetDefinition(string instanceId, out ItemDefinition definition)
        {
            definition = null;

            if (string.IsNullOrWhiteSpace(instanceId) || !_storedWorldItems.TryGetValue(instanceId, out WorldItem item) || item == null)
                return false;

            definition = item.Definition;
            return definition != null;
        }

        internal bool RestoreStoredItems(IReadOnlyList<WorldItem> items)
        {
            if (items == null || items.Count > Inventory.Capacity)
                return false;

            Dictionary<string, WorldItem> restoredItems = new(StringComparer.Ordinal);
            List<ItemInstance> instances = new(items.Count);

            for (int i = 0; i < items.Count; i++)
            {
                WorldItem item = items[i];

                if (item == null ||
                    item.Instance == null ||
                    item.Instance.Location != ItemLocation.Inventory ||
                    !restoredItems.TryAdd(item.Instance.InstanceId, item))
                {
                    return false;
                }

                instances.Add(item.Instance);
            }

            // Like the transfers below: the map first, so Inventory.Changed listeners see the loaded items.
            Dictionary<string, WorldItem> previousItems = new(_storedWorldItems, StringComparer.Ordinal);
            ReplaceStoredItems(restoredItems);

            if (Inventory.RestoreItems(instances))
                return true;

            ReplaceStoredItems(previousItems);
            return false;
        }

        // The Inventory list changes last in both transfers below, after the stored-item map, so Changed
        // listeners always see a consistent state. If it fails, every earlier step is undone: a failure never
        // reorders, loses or duplicates an item.
        private bool TryTake(string instanceId)
        {
            WorldItem item = _storedWorldItems[instanceId];

            if (!_playerCarry.TryCarryFromInventory(item))
                return false;

            _storedWorldItems.Remove(instanceId);

            if (Inventory.TryRemove(instanceId, out _))
                return true;

            _storedWorldItems.Add(instanceId, item);

            if (!_playerCarry.TryStoreCarriedItem(storedItemsRoot, out _))
                Debug.LogError($"Failed to roll back taking item {instanceId} from the inventory.", this);

            return false;
        }

        private bool TrySwap(string instanceId)
        {
            WorldItem taken = _storedWorldItems[instanceId];
            WorldItem held = _playerCarry.CarriedItem;
            string heldId = held.Instance.InstanceId;

            if (!_playerCarry.TryStoreCarriedItem(storedItemsRoot, out _))
                return false;

            if (!_playerCarry.TryCarryFromInventory(taken))
            {
                RollBackSwap(held, null, instanceId);
                return false;
            }

            // The map must already describe the new slot when Inventory.Changed reaches its listeners.
            _storedWorldItems.Remove(instanceId);
            _storedWorldItems.Add(heldId, held);

            if (Inventory.TryReplace(instanceId, held.Instance))
                return true;

            _storedWorldItems.Remove(heldId);
            _storedWorldItems.Add(instanceId, taken);
            RollBackSwap(held, taken, instanceId);
            return false;
        }

        private void RollBackSwap(WorldItem held, WorldItem taken, string takenId)
        {
            bool restored = (taken == null || _playerCarry.TryStoreCarriedItem(storedItemsRoot, out _)) &&
                            _playerCarry.TryCarryFromInventory(held);

            if (!restored)
                Debug.LogError($"Failed to roll back swapping item {held.Instance.InstanceId} with inventory item {takenId}.", this);
        }

        private void ReplaceStoredItems(Dictionary<string, WorldItem> items)
        {
            _storedWorldItems.Clear();

            foreach (KeyValuePair<string, WorldItem> pair in items)
                _storedWorldItems.Add(pair.Key, pair.Value);
        }

        private bool IsTracked(string instanceId)
        {
            return Inventory.Contains(instanceId) || _storedWorldItems.ContainsKey(instanceId);
        }
    }
}