using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace GoLive.Shop
{
    public readonly struct ShopCartLine
    {
        public string ProductId { get; }
        public int Quantity { get; }

        public ShopCartLine(string productId, int quantity)
        {
            ProductId = productId;
            Quantity = quantity;
        }
    }

    // The player's basket for this session: Product ID → quantity, in the order products were first added. It knows
    // nothing about prices, stock or purchase limits (ShopCheckout owns those rules) and is never saved.
    public sealed class ShopCart
    {
        public IReadOnlyList<ShopCartLine> Lines { get; }
        public int TotalQuantity { get; private set; }
        public bool IsEmpty => _lines.Count == 0;

        public event Action Changed;

        private readonly List<ShopCartLine> _lines = new();

        public ShopCart()
        {
            Lines = new ReadOnlyCollection<ShopCartLine>(_lines);
        }

        public int GetQuantity(string productId)
        {
            int index = IndexOf(productId);
            return index >= 0 ? _lines[index].Quantity : 0;
        }

        public bool TryAdd(string productId)
        {
            if (!ShopId.IsValid(productId) || TotalQuantity == int.MaxValue)
                return false;

            int index = IndexOf(productId);

            if (index < 0)
                _lines.Add(new ShopCartLine(productId, 1));
            else
                _lines[index] = new ShopCartLine(productId, _lines[index].Quantity + 1);

            TotalQuantity++;
            Changed?.Invoke();
            return true;
        }

        public bool TryRemoveOne(string productId)
        {
            int index = IndexOf(productId);

            if (index < 0)
                return false;

            int quantity = _lines[index].Quantity;

            if (quantity == 1)
                _lines.RemoveAt(index);
            else
                _lines[index] = new ShopCartLine(productId, quantity - 1);

            TotalQuantity--;
            Changed?.Invoke();
            return true;
        }

        public bool TryRemoveAll(string productId)
        {
            int index = IndexOf(productId);

            if (index < 0)
                return false;

            TotalQuantity -= _lines[index].Quantity;
            _lines.RemoveAt(index);

            Changed?.Invoke();
            return true;
        }

        public bool Clear()
        {
            if (_lines.Count == 0)
                return false;

            _lines.Clear();
            TotalQuantity = 0;

            Changed?.Invoke();
            return true;
        }

        private int IndexOf(string productId)
        {
            if (!ShopId.IsValid(productId))
                return -1;

            for (int i = 0; i < _lines.Count; i++)
            {
                if (string.Equals(_lines[i].ProductId, productId, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }
    }
}
