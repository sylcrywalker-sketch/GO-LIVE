using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using GoLive.Items;

namespace GoLive.Inventory
{
    public sealed class Inventory
    {
        public int Capacity { get; }
        public int Count => _items.Count;
        public bool IsFull => Count >= Capacity;
        public IReadOnlyList<ItemInstance> Items => _readOnlyItems;

        public event Action Changed;

        private readonly List<ItemInstance> _items;
        private readonly ReadOnlyCollection<ItemInstance> _readOnlyItems;
        private readonly HashSet<string> _instanceIds;

        public Inventory(int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            Capacity = capacity;
            _items = new List<ItemInstance>(capacity);
            _readOnlyItems = _items.AsReadOnly();
            _instanceIds = new HashSet<string>(StringComparer.Ordinal);
        }

        public bool Contains(string instanceId)
        {
            return !string.IsNullOrWhiteSpace(instanceId) && _instanceIds.Contains(instanceId);
        }

        public bool TryAdd(ItemInstance item)
        {
            if (item == null || item.Location != ItemLocation.Inventory || IsFull || Contains(item.InstanceId))
                return false;

            _items.Add(item);
            _instanceIds.Add(item.InstanceId);
            Changed?.Invoke();

            return true;
        }

        public bool TryGet(string instanceId, out ItemInstance item)
        {
            item = null;

            if (string.IsNullOrWhiteSpace(instanceId))
                return false;

            for (int i = 0; i < _items.Count; i++)
            {
                if (!string.Equals(_items[i].InstanceId, instanceId, StringComparison.Ordinal))
                    continue;

                item = _items[i];
                return true;
            }

            return false;
        }

        public bool TryRemove(string instanceId, out ItemInstance item)
        {
            if (!TryGet(instanceId, out item))
                return false;

            _items.Remove(item);
            _instanceIds.Remove(item.InstanceId);
            Changed?.Invoke();

            return true;
        }
    }
}