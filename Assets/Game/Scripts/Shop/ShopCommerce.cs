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

    // Compatibility result for existing single-product callers.
    // Actual transaction rules live only in ShopCheckout.
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

    public enum ShopCheckoutResultCode
    {
        Success = 0,
        EmptyCart = 1,
        NotReady = 2,
        ProductNotFound = 3,
        Unavailable = 4,
        PurchaseLimitReached = 5,
        InsufficientFunds = 6,
        Busy = 7,
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

        internal bool HasSameTerms(in ShopPurchaseOffer other)
        {
            return string.Equals(ProductId, other.ProductId, StringComparison.Ordinal) &&
                   PriceCents == other.PriceCents &&
                   MaxPurchases == other.MaxPurchases &&
                   DeliveryDelayMinutes == other.DeliveryDelayMinutes &&
                   IsAvailable == other.IsAvailable;
        }
    }

    public readonly struct ShopPurchaseResult
    {
        public ShopPurchaseResultCode Code { get; }
        public ShopOrder Order { get; }
        public bool Succeeded => Code == ShopPurchaseResultCode.Success;

        private ShopPurchaseResult(
            ShopPurchaseResultCode code,
            ShopOrder order)
        {
            Code = code;
            Order = order;
        }

        public static ShopPurchaseResult Success(ShopOrder order)
        {
            if (order == null)
                throw new ArgumentNullException(nameof(order));

            return new ShopPurchaseResult(
                ShopPurchaseResultCode.Success,
                order);
        }

        public static ShopPurchaseResult Failure(ShopPurchaseResultCode code)
        {
            if (code == ShopPurchaseResultCode.Success)
                throw new ArgumentOutOfRangeException(nameof(code));

            return new ShopPurchaseResult(code, null);
        }
    }

    public readonly struct ShopCheckoutLine
    {
        public ShopPurchaseOffer Offer { get; }
        public int Quantity { get; }

        public ShopCheckoutLine(
            ShopPurchaseOffer offer,
            int quantity)
        {
            if (quantity <= 0)
                throw new ArgumentOutOfRangeException(nameof(quantity));

            Offer = offer;
            Quantity = quantity;
        }
    }

    public readonly struct ShopCheckoutResult
    {
        private static readonly IReadOnlyList<ShopOrder> EmptyOrders =
            Array.Empty<ShopOrder>();

        public ShopCheckoutResultCode Code { get; }
        public IReadOnlyList<ShopOrder> Orders { get; }
        public bool Succeeded => Code == ShopCheckoutResultCode.Success;

        private ShopCheckoutResult(
            ShopCheckoutResultCode code,
            IReadOnlyList<ShopOrder> orders)
        {
            Code = code;
            Orders = orders ?? EmptyOrders;
        }

        public static ShopCheckoutResult Success(ShopOrder[] orders)
        {
            if (orders == null || orders.Length == 0)
            {
                throw new ArgumentException(
                    "A successful checkout requires at least one order.",
                    nameof(orders));
            }

            return new ShopCheckoutResult(
                ShopCheckoutResultCode.Success,
                orders);
        }

        public static ShopCheckoutResult Failure(ShopCheckoutResultCode code)
        {
            if (code == ShopCheckoutResultCode.Success)
                throw new ArgumentOutOfRangeException(nameof(code));

            return new ShopCheckoutResult(code, EmptyOrders);
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
        private Dictionary<string, ShopOrder> _ordersById =
            new(StringComparer.Ordinal);

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
                if (string.Equals(
                        _orders[i].ProductId,
                        productId,
                        StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        public bool HasReachedPurchaseLimit(
            string productId,
            int maxPurchases)
        {
            if (maxPurchases <= 0)
                return false;

            return CountForProduct(productId) >= maxPurchases;
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

        public bool TryGetLatestActiveOrder(
            string productId,
            out ShopOrder order)
        {
            order = null;

            if (!ShopId.IsValid(productId))
                return false;

            for (int i = _orders.Count - 1; i >= 0; i--)
            {
                if (!_orders[i].IsActive ||
                    !string.Equals(
                        _orders[i].ProductId,
                        productId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                order = _orders[i];
                return true;
            }

            return false;
        }

        public bool TryMarkDelivered(
            string orderId,
            GameTimeSnapshot currentTime)
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
            ShopOrderSnapshot[] orders =
                new ShopOrderSnapshot[_orders.Count];

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

            List<ShopOrder> restoredOrders =
                new(snapshot.Orders.Length);

            Dictionary<string, ShopOrder> restoredById =
                new(snapshot.Orders.Length, StringComparer.Ordinal);

            for (int i = 0; i < snapshot.Orders.Length; i++)
            {
                ShopOrderSnapshot savedOrder = snapshot.Orders[i];

                if (savedOrder == null)
                {
                    throw new ArgumentException(
                        "Saved Shop order is missing.",
                        nameof(snapshot));
                }

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
            if (order == null ||
                _ordersById.ContainsKey(order.OrderId))
            {
                return false;
            }

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
                throw new ArgumentException(
                    "Saved Shop order contains invalid game time.");
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

    /// <summary>
    /// The only owner of Shop transaction rules.
    /// It validates the complete request before mutating Wallet or Orders.
    /// </summary>
    public sealed class ShopCheckout
    {
        private readonly Wallet _wallet;
        private readonly ShopOrderBook _orders;
        private readonly GameClock _clock;

        private bool _busy;

        public ShopCheckout(
            Wallet wallet,
            ShopOrderBook orders,
            GameClock clock)
        {
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _orders = orders ?? throw new ArgumentNullException(nameof(orders));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public ShopCheckoutResultCode EvaluateSingle(
            in ShopPurchaseOffer offer)
        {
            ShopCheckoutResultCode offerState =
                EvaluateOffer(in offer, 1);

            if (offerState != ShopCheckoutResultCode.Success)
                return offerState;

            return _wallet.BalanceCents >= offer.PriceCents
                ? ShopCheckoutResultCode.Success
                : ShopCheckoutResultCode.InsufficientFunds;
        }

        public ShopCheckoutResultCode Evaluate(
            IReadOnlyList<ShopCheckoutLine> lines)
        {
            return TryCreatePlan(
                    lines,
                    out _,
                    out ShopCheckoutResultCode failure)
                ? ShopCheckoutResultCode.Success
                : failure;
        }

        public ShopCheckoutResult TryCheckoutSingle(
            in ShopPurchaseOffer offer)
        {
            ShopCheckoutLine[] lines =
            {
                new ShopCheckoutLine(offer, 1)
            };

            return TryCheckout(lines);
        }

        public ShopCheckoutResult TryCheckout(
            IReadOnlyList<ShopCheckoutLine> lines)
        {
            if (_busy)
            {
                return ShopCheckoutResult.Failure(
                    ShopCheckoutResultCode.Busy);
            }

            if (!TryCreatePlan(
                    lines,
                    out CheckoutPlan plan,
                    out ShopCheckoutResultCode failure))
            {
                return ShopCheckoutResult.Failure(failure);
            }

            _busy = true;

            try
            {
                ShopOrder[] createdOrders =
                    CreateOrders(in plan);

                int reservedCount = 0;

                for (int i = 0; i < createdOrders.Length; i++)
                {
                    if (_orders.TryReserve(createdOrders[i]))
                    {
                        reservedCount++;
                        continue;
                    }

                    RollBackReservations(
                        createdOrders,
                        reservedCount);

                    throw new InvalidOperationException(
                        "Failed to reserve a unique Shop order.");
                }

                if (!_wallet.TrySpendSilently(plan.TotalPriceCents))
                {
                    RollBackReservations(
                        createdOrders,
                        reservedCount);

                    return ShopCheckoutResult.Failure(
                        ShopCheckoutResultCode.InsufficientFunds);
                }

                // Both owners are already committed before notifications.
                // Subscriber exceptions may propagate, but cannot leave money/orders
                // in different commit states. Both notification streams are attempted.
                try
                {
                    _wallet.PublishChanged();
                }
                finally
                {
                    _orders.PublishChanged();
                }

                return ShopCheckoutResult.Success(createdOrders);
            }
            finally
            {
                _busy = false;
            }
        }

        private bool TryCreatePlan(
            IReadOnlyList<ShopCheckoutLine> lines,
            out CheckoutPlan plan,
            out ShopCheckoutResultCode failure)
        {
            plan = default;

            if (lines == null || lines.Count == 0)
            {
                failure = ShopCheckoutResultCode.EmptyCart;
                return false;
            }

            ShopCheckoutLine[] snapshot =
                new ShopCheckoutLine[lines.Count];

            Dictionary<string, RequestedProduct> requested =
                new(StringComparer.Ordinal);

            long totalPriceCents = 0;
            int orderCount = 0;

            for (int i = 0; i < lines.Count; i++)
            {
                ShopCheckoutLine line = lines[i];
                ShopPurchaseOffer offer = line.Offer;

                snapshot[i] = line;

                if (requested.TryGetValue(
                        offer.ProductId,
                        out RequestedProduct existing))
                {
                    if (!existing.Offer.HasSameTerms(in offer))
                    {
                        failure = ShopCheckoutResultCode.InvalidRequest;
                        return false;
                    }

                    int combinedQuantity =
                        checked(existing.Quantity + line.Quantity);

                    requested[offer.ProductId] =
                        new RequestedProduct(offer, combinedQuantity);

                    ShopCheckoutResultCode offerState =
                        EvaluateOffer(in offer, combinedQuantity);

                    if (offerState != ShopCheckoutResultCode.Success)
                    {
                        failure = offerState;
                        return false;
                    }
                }
                else
                {
                    requested.Add(
                        offer.ProductId,
                        new RequestedProduct(offer, line.Quantity));

                    ShopCheckoutResultCode offerState =
                        EvaluateOffer(in offer, line.Quantity);

                    if (offerState != ShopCheckoutResultCode.Success)
                    {
                        failure = offerState;
                        return false;
                    }
                }

                orderCount =
                    checked(orderCount + line.Quantity);

                totalPriceCents = checked(
                    totalPriceCents +
                    checked(offer.PriceCents * line.Quantity));
            }

            if (_wallet.BalanceCents < totalPriceCents)
            {
                failure = ShopCheckoutResultCode.InsufficientFunds;
                return false;
            }

            plan = new CheckoutPlan(
                snapshot,
                totalPriceCents,
                orderCount,
                _clock.Current);

            failure = ShopCheckoutResultCode.Success;
            return true;
        }

        private ShopCheckoutResultCode EvaluateOffer(
            in ShopPurchaseOffer offer,
            int requestedQuantity)
        {
            if (!offer.IsAvailable)
                return ShopCheckoutResultCode.Unavailable;

            if (requestedQuantity <= 0)
                return ShopCheckoutResultCode.InvalidRequest;

            if (offer.MaxPurchases > 0 &&
                _orders.CountForProduct(offer.ProductId) + requestedQuantity >
                offer.MaxPurchases)
            {
                return ShopCheckoutResultCode.PurchaseLimitReached;
            }

            return ShopCheckoutResultCode.Success;
        }

        private static ShopOrder[] CreateOrders(
            in CheckoutPlan plan)
        {
            ShopOrder[] orders =
                new ShopOrder[plan.OrderCount];

            int orderIndex = 0;

            for (int lineIndex = 0;
                 lineIndex < plan.Lines.Length;
                 lineIndex++)
            {
                ShopCheckoutLine line =
                    plan.Lines[lineIndex];

                ShopPurchaseOffer offer =
                    line.Offer;

                GameTimeSnapshot deliveryDueAt =
                    offer.GetDeliveryDueAt(plan.PlacedAt);

                for (int quantityIndex = 0;
                     quantityIndex < line.Quantity;
                     quantityIndex++)
                {
                    orders[orderIndex++] =
                        ShopOrder.CreateNew(
                            offer.ProductId,
                            offer.PriceCents,
                            plan.PlacedAt,
                            deliveryDueAt);
                }
            }

            return orders;
        }

        private void RollBackReservations(
            ShopOrder[] orders,
            int reservedCount)
        {
            for (int i = reservedCount - 1; i >= 0; i--)
            {
                _orders.TryCancelReservation(
                    orders[i].OrderId);
            }
        }

        private readonly struct RequestedProduct
        {
            public ShopPurchaseOffer Offer { get; }
            public int Quantity { get; }

            public RequestedProduct(
                ShopPurchaseOffer offer,
                int quantity)
            {
                Offer = offer;
                Quantity = quantity;
            }
        }

        private readonly struct CheckoutPlan
        {
            public ShopCheckoutLine[] Lines { get; }
            public long TotalPriceCents { get; }
            public int OrderCount { get; }
            public GameTimeSnapshot PlacedAt { get; }

            public CheckoutPlan(
                ShopCheckoutLine[] lines,
                long totalPriceCents,
                int orderCount,
                GameTimeSnapshot placedAt)
            {
                Lines = lines;
                TotalPriceCents = totalPriceCents;
                OrderCount = orderCount;
                PlacedAt = placedAt;
            }
        }
    }

    /// <summary>
    /// Compatibility adapter for existing single-product callers/tests.
    /// It contains no purchase rules; every decision delegates to ShopCheckout.
    /// </summary>
    public sealed class ShopPurchase
    {
        private readonly ShopCheckout _checkout;

        internal ShopCheckout Checkout => _checkout;

        public ShopPurchase(
            Wallet wallet,
            ShopOrderBook orders,
            GameClock clock)
        {
            _checkout = new ShopCheckout(
                wallet,
                orders,
                clock);
        }

        public ShopPurchaseResultCode Evaluate(
            in ShopPurchaseOffer offer)
        {
            return ToPurchaseCode(
                _checkout.EvaluateSingle(in offer));
        }

        public ShopPurchaseResult TryPurchase(
            in ShopPurchaseOffer offer)
        {
            ShopCheckoutResult result =
                _checkout.TryCheckoutSingle(in offer);

            if (result.Succeeded)
                return ShopPurchaseResult.Success(result.Orders[0]);

            return ShopPurchaseResult.Failure(
                ToPurchaseCode(result.Code));
        }

        private static ShopPurchaseResultCode ToPurchaseCode(
            ShopCheckoutResultCode code)
        {
            return code switch
            {
                ShopCheckoutResultCode.Success =>
                    ShopPurchaseResultCode.Success,

                ShopCheckoutResultCode.NotReady =>
                    ShopPurchaseResultCode.NotReady,

                ShopCheckoutResultCode.ProductNotFound =>
                    ShopPurchaseResultCode.ProductNotFound,

                ShopCheckoutResultCode.Unavailable =>
                    ShopPurchaseResultCode.Unavailable,

                ShopCheckoutResultCode.PurchaseLimitReached =>
                    ShopPurchaseResultCode.PurchaseLimitReached,

                ShopCheckoutResultCode.InsufficientFunds =>
                    ShopPurchaseResultCode.InsufficientFunds,

                ShopCheckoutResultCode.Busy =>
                    ShopPurchaseResultCode.Busy,

                _ => throw new InvalidOperationException(
                    $"Checkout result {code} cannot occur for a single-product purchase.")
            };
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
