using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace GoLive.Shop
{
    public enum ShopCartAddResultCode
    {
        Success = 0,
        NotReady = 1,
        ProductNotFound = 2,
        Unavailable = 3,
        PurchaseLimitReached = 4
    }

    public readonly struct ShopCartEntry
    {
        public string ProductId { get; }
        public int Quantity { get; }

        public ShopCartEntry(
            string productId,
            int quantity)
        {
            if (!ShopId.IsValid(productId))
                throw new ArgumentException("Product ID is invalid.", nameof(productId));

            if (quantity <= 0)
                throw new ArgumentOutOfRangeException(nameof(quantity));

            ProductId = productId;
            Quantity = quantity;
        }
    }

    /// <summary>
    /// Transient cart state for the current game session.
    /// Owns only ProductId -> Quantity. It never owns prices, money or orders.
    /// </summary>
    public sealed class ShopCart
    {
        private readonly List<ShopCartEntry> _entries = new();
        private readonly Dictionary<string, int> _indices =
            new(StringComparer.Ordinal);
        private readonly ReadOnlyCollection<ShopCartEntry> _readOnlyEntries;

        public IReadOnlyList<ShopCartEntry> Entries => _readOnlyEntries;
        public bool IsEmpty => _entries.Count == 0;

        public event Action Changed;

        public int TotalQuantity
        {
            get
            {
                int total = 0;

                for (int i = 0; i < _entries.Count; i++)
                    total = checked(total + _entries[i].Quantity);

                return total;
            }
        }

        public ShopCart()
        {
            _readOnlyEntries =
                new ReadOnlyCollection<ShopCartEntry>(_entries);
        }

        public int GetQuantity(string productId)
        {
            if (!ShopId.IsValid(productId))
                return 0;

            return _indices.TryGetValue(productId, out int index)
                ? _entries[index].Quantity
                : 0;
        }

        internal void AddOne(string productId)
        {
            if (!ShopId.IsValid(productId))
                throw new ArgumentException("Product ID is invalid.", nameof(productId));

            if (_indices.TryGetValue(productId, out int index))
            {
                ShopCartEntry current = _entries[index];

                _entries[index] = new ShopCartEntry(
                    current.ProductId,
                    checked(current.Quantity + 1));
            }
            else
            {
                _indices.Add(productId, _entries.Count);
                _entries.Add(new ShopCartEntry(productId, 1));
            }

            Changed?.Invoke();
        }

        internal bool RemoveOne(string productId)
        {
            if (!ShopId.IsValid(productId) ||
                !_indices.TryGetValue(productId, out int index))
            {
                return false;
            }

            ShopCartEntry current = _entries[index];

            if (current.Quantity > 1)
            {
                _entries[index] = new ShopCartEntry(
                    current.ProductId,
                    current.Quantity - 1);

                Changed?.Invoke();
                return true;
            }

            RemoveAt(index);
            Changed?.Invoke();
            return true;
        }

        internal bool RemoveAll(string productId)
        {
            if (!ShopId.IsValid(productId) ||
                !_indices.TryGetValue(productId, out int index))
            {
                return false;
            }

            RemoveAt(index);
            Changed?.Invoke();
            return true;
        }

        internal void Clear()
        {
            if (_entries.Count == 0)
                return;

            _entries.Clear();
            _indices.Clear();
            Changed?.Invoke();
        }

        private void RemoveAt(int index)
        {
            string productId = _entries[index].ProductId;

            _entries.RemoveAt(index);
            _indices.Remove(productId);

            for (int i = index; i < _entries.Count; i++)
                _indices[_entries[i].ProductId] = i;
        }
    }
}
