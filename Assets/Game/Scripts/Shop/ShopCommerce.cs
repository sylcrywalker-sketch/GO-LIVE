using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using GoLive.GameTime;

namespace GoLive.Shop
{
    internal static class ShopId
    {
        public static bool IsValid(string value)
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

    public enum ShopOrderStatus
    {
        Placed = 0,
        Delivered = 1
    }

    public enum ShopPurchaseResultCode
    {
        Success = 0,
        NotReady = 1,
        ProductNotFound = 2,
        Unavailable = 3,
        PurchaseLimitReached = 4,
        InsufficientFunds = 5,
        Busy = 6,
        EmptyRequest = 7,
        InvalidRequest = 8
    }

    public readonly struct ShopPurchaseOffer
    {
        public string ProductId { get; }
        public long PriceCents { get; }
        public int MaxPurchases { get; }
        public int DeliveryDelayMinutes { get; }
        public bool IsAvailable { get; }

        public ShopPurchaseOffer(
            string productId,
            long priceCents,
            int maxPurchases,
            int deliveryDelayMinutes,
            bool isAvailable)
        {
            if (!ShopId.IsValid(productId))
                throw new ArgumentException("Product ID is invalid.", nameof(productId));

            if (priceCents <= 0)
                throw new ArgumentOutOfRangeException(nameof(priceCents));

            if (maxPurchases < 0)
                throw new ArgumentOutOfRangeException(nameof(maxPurchases));

            if (isAvailable && deliveryDelayMinutes <= 0)
                throw new ArgumentOutOfRangeException(nameof(deliveryDelayMinutes));

            ProductId = productId;
            PriceCents = priceCents;
            MaxPurchases = maxPurchases;
            DeliveryDelayMinutes = deliveryDelayMinutes;
            IsAvailable = isAvailable;
        }

        public GameTimeSnapshot GetDeliveryDueAt(GameTimeSnapshot placedAt)
        {
            long delaySeconds = checked(
                DeliveryDelayMinutes * GameTimeSnapshot.SecondsPerMinute);

            return new GameTimeSnapshot(
                checked(placedAt.TotalSeconds + delaySeconds));
        }
    }

    public sealed class ShopOrder
    {
        public string OrderId { get; }
        public string ProductId { get; }
        public long PaidPriceCents { get; }
        public GameTimeSnapshot PlacedAt { get; }
        public GameTimeSnapshot DeliveryDueAt { get; }
        public ShopOrderStatus Status { get; private set; }
        public bool IsActive => Status == ShopOrderStatus.Placed;

        internal ShopOrder(
            string orderId,
            string productId,
            long paidPriceCents,
            GameTimeSnapshot placedAt,
            GameTimeSnapshot deliveryDueAt,
            ShopOrderStatus status)
        {
            if (!ShopId.IsValid(orderId))
                throw new ArgumentException("Order ID is invalid.", nameof(orderId));

            if (!ShopId.IsValid(productId))
                throw new ArgumentException("Product ID is invalid.", nameof(productId));

            if (paidPriceCents <= 0)
                throw new ArgumentOutOfRangeException(nameof(paidPriceCents));

            if (deliveryDueAt.TotalSeconds < placedAt.TotalSeconds)
                throw new ArgumentException("Delivery time cannot be earlier than placement time.");

            if (!Enum.IsDefined(typeof(ShopOrderStatus), status))
                throw new ArgumentOutOfRangeException(nameof(status));

            OrderId = orderId;
            ProductId = productId;
            PaidPriceCents = paidPriceCents;
            PlacedAt = placedAt;
            DeliveryDueAt = deliveryDueAt;
            Status = status;
        }

        internal static ShopOrder CreateNew(
            string productId,
            long paidPriceCents,
            GameTimeSnapshot placedAt,
            GameTimeSnapshot deliveryDueAt)
        {
            return new ShopOrder(
                Guid.NewGuid().ToString("N"),
                productId,
                paidPriceCents,
                placedAt,
                deliveryDueAt,
                ShopOrderStatus.Placed);
        }

        public bool IsDeliveryDue(GameTimeSnapshot currentTime)
        {
            return Status == ShopOrderStatus.Placed &&
                   currentTime.TotalSeconds >= DeliveryDueAt.TotalSeconds;
        }

        internal bool TryMarkDelivered(GameTimeSnapshot currentTime)
        {
            if (!IsDeliveryDue(currentTime))
                return false;

            Status = ShopOrderStatus.Delivered;
            return true;
        }
    }

    public sealed class ShopOrderBook
    {
        public IReadOnlyList<ShopOrder> Orders { get; private set; }

        public event Action Changed;

        private List<ShopOrder> _orders = new();
        private Dictionary<string, ShopOrder> _ordersById = new(StringComparer.Ordinal);

        public ShopOrderBook()
        {
            Orders = new ReadOnlyCollection<ShopOrder>(_orders);
        }

        public bool TryGetOrder(string orderId, out ShopOrder order)
        {
            order = null;

            if (!ShopId.IsValid(orderId))
                return false;

            return _ordersById.TryGetValue(orderId, out order);
        }

        public int CountForProduct(string productId)
        {
            if (!ShopId.IsValid(productId))
                return 0;

            int count = 0;

            for (int i = 0; i < _orders.Count; i++)
            {
                if (string.Equals(_orders[i].ProductId, productId, StringComparison.Ordinal))
                    count++;
            }

            return count;
        }

        public int CountActive()
        {
            int count = 0;

            for (int i = 0; i < _orders.Count; i++)
            {
                if (_orders[i].IsActive)
                    count++;
            }

            return count;
        }

        public bool TryGetLatestActiveOrder(string productId, out ShopOrder order)
        {
            order = null;

            if (!ShopId.IsValid(productId))
                return false;

            for (int i = _orders.Count - 1; i >= 0; i--)
            {
                if (!_orders[i].IsActive ||
                    !string.Equals(_orders[i].ProductId, productId, StringComparison.Ordinal))
                {
                    continue;
                }

                order = _orders[i];
                return true;
            }

            return false;
        }

        public bool TryMarkDelivered(string orderId, GameTimeSnapshot currentTime)
        {
            if (!TryGetOrder(orderId, out ShopOrder order))
                return false;

            if (!order.TryMarkDelivered(currentTime))
                return false;

            Changed?.Invoke();
            return true;
        }

        public ShopOrdersSnapshot CaptureSnapshot()
        {
            ShopOrderSnapshot[] orders = new ShopOrderSnapshot[_orders.Count];

            for (int i = 0; i < _orders.Count; i++)
            {
                ShopOrder order = _orders[i];

                orders[i] = new ShopOrderSnapshot
                {
                    OrderId = order.OrderId,
                    ProductId = order.ProductId,
                    PaidPriceCents = order.PaidPriceCents,
                    PlacedAtGameTimeSeconds = order.PlacedAt.TotalSeconds,
                    DeliveryDueGameTimeSeconds = order.DeliveryDueAt.TotalSeconds,
                    Status = order.Status
                };
            }

            return new ShopOrdersSnapshot
            {
                Orders = orders
            };
        }

        public void Restore(ShopOrdersSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            if (snapshot.Version != ShopOrdersSnapshot.CurrentVersion ||
                snapshot.Orders == null)
            {
                throw new ArgumentException(
                    "Unsupported or incomplete Shop orders snapshot.",
                    nameof(snapshot));
            }

            List<ShopOrder> restoredOrders = new(snapshot.Orders.Length);
            Dictionary<string, ShopOrder> restoredById =
                new(snapshot.Orders.Length, StringComparer.Ordinal);

            for (int i = 0; i < snapshot.Orders.Length; i++)
            {
                ShopOrderSnapshot savedOrder = snapshot.Orders[i];

                if (savedOrder == null)
                    throw new ArgumentException("Saved Shop order is missing.", nameof(snapshot));

                ShopOrder order = RestoreOrder(savedOrder);

                if (!restoredById.TryAdd(order.OrderId, order))
                {
                    throw new ArgumentException(
                        $"Duplicate Order ID '{order.OrderId}'.",
                        nameof(snapshot));
                }

                restoredOrders.Add(order);
            }

            _orders = restoredOrders;
            _ordersById = restoredById;
            Orders = new ReadOnlyCollection<ShopOrder>(_orders);

            Changed?.Invoke();
        }

        // All or nothing, without notifying: ShopCheckout publishes once the wallet has been charged.
        internal bool TryReserveAll(IReadOnlyList<ShopOrder> orders)
        {
            HashSet<string> batch = new(StringComparer.Ordinal);

            for (int i = 0; i < orders.Count; i++)
            {
                ShopOrder order = orders[i];

                if (order == null || _ordersById.ContainsKey(order.OrderId) || !batch.Add(order.OrderId))
                    return false;
            }

            for (int i = 0; i < orders.Count; i++)
            {
                _orders.Add(orders[i]);
                _ordersById.Add(orders[i].OrderId, orders[i]);
            }

            return true;
        }

        internal void CancelReservations(IReadOnlyList<ShopOrder> orders)
        {
            for (int i = 0; i < orders.Count; i++)
            {
                if (_ordersById.Remove(orders[i].OrderId))
                    _orders.Remove(orders[i]);
            }
        }

        internal void PublishChanged()
        {
            Changed?.Invoke();
        }

        private static ShopOrder RestoreOrder(ShopOrderSnapshot savedOrder)
        {
            if (savedOrder.PlacedAtGameTimeSeconds < 0 ||
                savedOrder.DeliveryDueGameTimeSeconds < 0)
            {
                throw new ArgumentException("Saved Shop order contains invalid game time.");
            }

            return new ShopOrder(
                savedOrder.OrderId,
                savedOrder.ProductId,
                savedOrder.PaidPriceCents,
                new GameTimeSnapshot(savedOrder.PlacedAtGameTimeSeconds),
                new GameTimeSnapshot(savedOrder.DeliveryDueGameTimeSeconds),
                savedOrder.Status);
        }
    }

    [Serializable]
    public sealed class ShopOrdersSnapshot
    {
        public const int CurrentVersion = 1;

        public int Version = CurrentVersion;
        public ShopOrderSnapshot[] Orders = Array.Empty<ShopOrderSnapshot>();
    }

    [Serializable]
    public sealed class ShopOrderSnapshot
    {
        public string OrderId;
        public string ProductId;
        public long PaidPriceCents;
        public long PlacedAtGameTimeSeconds;
        public long DeliveryDueGameTimeSeconds;
        public ShopOrderStatus Status;
    }
}