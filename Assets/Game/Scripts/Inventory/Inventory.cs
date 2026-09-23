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
            int index = IndexOf(instanceId);

            item = index >= 0 ? _items[index] : null;
            return item != null;
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

        // Puts the replacement into the exact slot of instanceId, so a swap keeps the Inventory order.
        public bool TryReplace(string instanceId, ItemInstance replacement)
        {
            if (replacement == null || replacement.Location != ItemLocation.Inventory || Contains(replacement.InstanceId))
                return false;

            int index = IndexOf(instanceId);

            if (index < 0)
                return false;

            _instanceIds.Remove(_items[index].InstanceId);
            _items[index] = replacement;
            _instanceIds.Add(replacement.InstanceId);

            Changed?.Invoke();
            return true;
        }

        internal bool RestoreItems(IReadOnlyList<ItemInstance> items)
        {
            if (items == null || items.Count > Capacity)
                return false;

            HashSet<string> ids = new(StringComparer.Ordinal);

            for (int i = 0; i < items.Count; i++)
            {
                ItemInstance item = items[i];

                if (item == null || item.Location != ItemLocation.Inventory || !ids.Add(item.InstanceId))
                    return false;
            }

            _items.Clear();
            _instanceIds.Clear();

            for (int i = 0; i < items.Count; i++)
            {
                _items.Add(items[i]);
                _instanceIds.Add(items[i].InstanceId);
            }

            Changed?.Invoke();
            return true;
        }

        private int IndexOf(string instanceId)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
                return -1;

            for (int i = 0; i < _items.Count; i++)
            {
                if (string.Equals(_items[i].InstanceId, instanceId, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }
    }
}