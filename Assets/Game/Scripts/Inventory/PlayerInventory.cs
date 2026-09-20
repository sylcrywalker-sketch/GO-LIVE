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

        public bool TryStoreCarriedItem()
        {
            if (!isActiveAndEnabled || !_playerCarry.HasItem)
                return false;

            WorldItem item = _playerCarry.CarriedItem;
            string instanceId = item.Instance.InstanceId;

            if (!item.Definition.CanStoreInInventory || Inventory.IsFull || Inventory.Contains(instanceId) || _storedWorldItems.ContainsKey(instanceId))
                return false;

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

        public bool TryTakeToCarry(string instanceId)
        {
            if (!isActiveAndEnabled || _playerCarry.HasItem || string.IsNullOrWhiteSpace(instanceId))
                return false;

            if (!_storedWorldItems.TryGetValue(instanceId, out WorldItem worldItem))
                return false;

            if (!Inventory.TryRemove(instanceId, out ItemInstance instance))
                return false;

            if (!_playerCarry.TryCarryFromInventory(worldItem))
            {
                Inventory.TryAdd(instance);
                return false;
            }

            _storedWorldItems.Remove(instanceId);
            return true;
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

            if (!Inventory.RestoreItems(instances))
                return false;

            _storedWorldItems.Clear();

            foreach (KeyValuePair<string, WorldItem> pair in restoredItems)
                _storedWorldItems.Add(pair.Key, pair.Value);

            return true;
        }
    }
}