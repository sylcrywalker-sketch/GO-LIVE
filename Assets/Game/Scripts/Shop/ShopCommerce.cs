using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using GoLive.Economy;
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
        Busy = 6
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
    }

    public readonly struct ShopPurchaseResult
    {
        public ShopPurchaseResultCode Code { get; }
        public ShopOrder Order { get; }
        public bool Succeeded => Code == ShopPurchaseResultCode.Success;

        private ShopPurchaseResult(ShopPurchaseResultCode code, ShopOrder order)
        {
            Code = code;
            Order = order;
        }

        public static ShopPurchaseResult Success(ShopOrder order)
        {
            if (order == null)
                throw new ArgumentNullException(nameof(order));

            return new ShopPurchaseResult(ShopPurchaseResultCode.Success, order);
        }

        public static ShopPurchaseResult Failure(ShopPurchaseResultCode code)
        {
            if (code == ShopPurchaseResultCode.Success)
                throw new ArgumentOutOfRangeException(nameof(code));

            return new ShopPurchaseResult(code, null);
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

        public bool HasReachedPurchaseLimit(string productId, int maxPurchases)
        {
            if (maxPurchases <= 0)
                return false;

            return CountForProduct(productId) >= maxPurchases;
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

        internal bool TryReserve(ShopOrder order)
        {
            if (order == null || _ordersById.ContainsKey(order.OrderId))
                return false;

            _orders.Add(order);
            _ordersById.Add(order.OrderId, order);
            return true;
        }

        internal bool TryCancelReservation(string orderId)
        {
            if (!_ordersById.TryGetValue(orderId, out ShopOrder order))
                return false;

            _ordersById.Remove(orderId);
            _orders.Remove(order);
            return true;
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

    public sealed class ShopPurchase
    {
        private readonly Wallet _wallet;
        private readonly ShopOrderBook _orders;
        private readonly GameClock _clock;

        private bool _busy;

        public ShopPurchase(Wallet wallet, ShopOrderBook orders, GameClock clock)
        {
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _orders = orders ?? throw new ArgumentNullException(nameof(orders));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public ShopPurchaseResultCode Evaluate(in ShopPurchaseOffer offer)
        {
            if (!offer.IsAvailable)
                return ShopPurchaseResultCode.Unavailable;

            if (_orders.HasReachedPurchaseLimit(offer.ProductId, offer.MaxPurchases))
                return ShopPurchaseResultCode.PurchaseLimitReached;

            if (_wallet.BalanceCents < offer.PriceCents)
                return ShopPurchaseResultCode.InsufficientFunds;

            return ShopPurchaseResultCode.Success;
        }

        public ShopPurchaseResult TryPurchase(in ShopPurchaseOffer offer)
        {
            if (_busy)
                return ShopPurchaseResult.Failure(ShopPurchaseResultCode.Busy);

            ShopPurchaseResultCode evaluation = Evaluate(in offer);

            if (evaluation != ShopPurchaseResultCode.Success)
                return ShopPurchaseResult.Failure(evaluation);

            _busy = true;

            try
            {
                GameTimeSnapshot placedAt = _clock.Current;

                long delaySeconds = checked(
                    offer.DeliveryDelayMinutes * GameTimeSnapshot.SecondsPerMinute);

                GameTimeSnapshot deliveryDueAt = new(
                    checked(placedAt.TotalSeconds + delaySeconds));

                ShopOrder order = ShopOrder.CreateNew(
                    offer.ProductId,
                    offer.PriceCents,
                    placedAt,
                    deliveryDueAt);

                if (!_orders.TryReserve(order))
                    throw new InvalidOperationException("Failed to reserve a unique Shop order.");

                if (!_wallet.TrySpend(offer.PriceCents))
                {
                    _orders.TryCancelReservation(order.OrderId);
                    return ShopPurchaseResult.Failure(ShopPurchaseResultCode.InsufficientFunds);
                }

                _orders.PublishChanged();

                return ShopPurchaseResult.Success(order);
            }
            finally
            {
                _busy = false;
            }
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