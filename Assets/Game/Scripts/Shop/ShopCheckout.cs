using System;
using System.Collections.Generic;
using GoLive.Economy;
using GoLive.GameTime;

namespace GoLive.Shop
{
    public readonly struct ShopCheckoutLine
    {
        public ShopPurchaseOffer Offer { get; }
        public int Quantity { get; }

        public ShopCheckoutLine(in ShopPurchaseOffer offer, int quantity)
        {
            Offer = offer;
            Quantity = quantity;
        }
    }

    public sealed class ShopCheckoutResult
    {
        public ShopPurchaseResultCode Code { get; }
        public IReadOnlyList<ShopOrder> Orders { get; }
        public long ChargedCents { get; }
        public bool Succeeded => Code == ShopPurchaseResultCode.Success;

        private ShopCheckoutResult(ShopPurchaseResultCode code, IReadOnlyList<ShopOrder> orders, long chargedCents)
        {
            Code = code;
            Orders = orders;
            ChargedCents = chargedCents;
        }

        public static ShopCheckoutResult Failure(ShopPurchaseResultCode code)
        {
            if (code == ShopPurchaseResultCode.Success)
                throw new ArgumentOutOfRangeException(nameof(code));

            return new ShopCheckoutResult(code, Array.Empty<ShopOrder>(), 0);
        }

        internal static ShopCheckoutResult Success(ShopOrder[] orders, long chargedCents)
        {
            return new ShopCheckoutResult(ShopPurchaseResultCode.Success, Array.AsReadOnly(orders), chargedCents);
        }
    }

    // The one owner of the Shop's purchase rules and the one way to pay for goods: availability, quantities, purchase
    // limits (earlier orders included), the total price and the wallet balance. A request is validated as a whole before
    // anything changes. The commit reserves every order, then charges the wallet once: Wallet refuses without side effects
    // or changes the balance before it notifies, so the wallet and the order book never end up in different states.
    public sealed class ShopCheckout
    {
        public const int MaxQuantityPerProduct = 99;

        private readonly Wallet _wallet;
        private readonly ShopOrderBook _orders;
        private readonly GameClock _clock;

        private bool _busy;

        public ShopCheckout(Wallet wallet, ShopOrderBook orders, GameClock clock)
        {
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _orders = orders ?? throw new ArgumentNullException(nameof(orders));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        // Whether this many units of one offer could be bought now, the wallet aside: the rule behind "add to cart".
        public ShopPurchaseResultCode EvaluateQuantity(in ShopPurchaseOffer offer, int quantity)
        {
            if (!IsValidOffer(offer) || quantity <= 0)
                return ShopPurchaseResultCode.InvalidRequest;

            if (!offer.IsAvailable)
                return ShopPurchaseResultCode.Unavailable;

            if (quantity > MaxQuantityPerProduct)
                return ShopPurchaseResultCode.PurchaseLimitReached;

            if (offer.MaxPurchases > 0 &&
                (long)_orders.CountForProduct(offer.ProductId) + quantity > offer.MaxPurchases)
            {
                return ShopPurchaseResultCode.PurchaseLimitReached;
            }

            return ShopPurchaseResultCode.Success;
        }

        public ShopPurchaseResultCode Evaluate(IReadOnlyList<ShopCheckoutLine> lines)
        {
            return Evaluate(lines, out _, out _);
        }

        public ShopCheckoutResult TryCheckout(IReadOnlyList<ShopCheckoutLine> lines)
        {
            if (_busy)
                return ShopCheckoutResult.Failure(ShopPurchaseResultCode.Busy);

            ShopPurchaseResultCode evaluation = Evaluate(lines, out List<ShopCheckoutLine> products, out long totalCents);

            if (evaluation != ShopPurchaseResultCode.Success)
                return ShopCheckoutResult.Failure(evaluation);

            _busy = true;

            try
            {
                ShopOrder[] orders = CreateOrders(products, _clock.Current);

                if (!_orders.TryReserveAll(orders))
                    throw new InvalidOperationException("Failed to reserve unique Shop orders.");

                bool reserved = true;

                try
                {
                    if (!_wallet.TrySpend(totalCents))
                    {
                        _orders.CancelReservations(orders);
                        reserved = false;

                        return ShopCheckoutResult.Failure(ShopPurchaseResultCode.InsufficientFunds);
                    }
                }
                finally
                {
                    // The orders stay reserved exactly when the balance was charged, including when a BalanceChanged
                    // subscriber threw after the charge: announce them once, either way.
                    if (reserved)
                        _orders.PublishChanged();
                }

                return ShopCheckoutResult.Success(orders, totalCents);
            }
            finally
            {
                _busy = false;
            }
        }

        // The price of one line: the unit price times the quantity.
        public static long GetLineTotalCents(long unitPriceCents, int quantity)
        {
            if (unitPriceCents < 0)
                throw new ArgumentOutOfRangeException(nameof(unitPriceCents));

            if (quantity < 0)
                throw new ArgumentOutOfRangeException(nameof(quantity));

            return checked(unitPriceCents * quantity);
        }

        // The price of a request: every line's price times its quantity. False for a request that is not well formed.
        public static bool TryCalculateTotal(IReadOnlyList<ShopCheckoutLine> lines, out long totalCents)
        {
            totalCents = 0;

            return lines != null &&
                   TryAggregate(lines, out List<ShopCheckoutLine> products) &&
                   TrySum(products, out totalCents);
        }

        private ShopPurchaseResultCode Evaluate(
            IReadOnlyList<ShopCheckoutLine> lines,
            out List<ShopCheckoutLine> products,
            out long totalCents)
        {
            products = null;
            totalCents = 0;

            if (lines == null || lines.Count == 0)
                return ShopPurchaseResultCode.EmptyRequest;

            if (!TryAggregate(lines, out products) || !TrySum(products, out totalCents))
                return ShopPurchaseResultCode.InvalidRequest;

            for (int i = 0; i < products.Count; i++)
            {
                ShopPurchaseResultCode code = EvaluateQuantity(products[i].Offer, products[i].Quantity);

                if (code != ShopPurchaseResultCode.Success)
                    return code;
            }

            return _wallet.BalanceCents < totalCents
                ? ShopPurchaseResultCode.InsufficientFunds
                : ShopPurchaseResultCode.Success;
        }

        // Lines of one product add up, but only when they quote the same terms: two lines with different prices, limits,
        // delivery times or availability for one product are a malformed request, not something to reconcile.
        private static bool TryAggregate(IReadOnlyList<ShopCheckoutLine> lines, out List<ShopCheckoutLine> products)
        {
            products = new List<ShopCheckoutLine>(lines.Count);

            for (int i = 0; i < lines.Count; i++)
            {
                ShopCheckoutLine line = lines[i];

                if (line.Quantity <= 0 || !IsValidOffer(line.Offer))
                    return false;

                int index = IndexOf(products, line.Offer.ProductId);

                if (index < 0)
                {
                    products.Add(line);
                    continue;
                }

                ShopCheckoutLine existing = products[index];

                if (!HaveSameTerms(existing.Offer, line.Offer))
                    return false;

                long quantity = (long)existing.Quantity + line.Quantity;

                if (quantity > int.MaxValue)
                    return false;

                products[index] = new ShopCheckoutLine(existing.Offer, (int)quantity);
            }

            return true;
        }

        private static bool TrySum(List<ShopCheckoutLine> products, out long totalCents)
        {
            totalCents = 0;

            try
            {
                for (int i = 0; i < products.Count; i++)
                    totalCents = checked(totalCents + GetLineTotalCents(products[i].Offer.PriceCents, products[i].Quantity));

                return true;
            }
            catch (OverflowException)
            {
                totalCents = 0;
                return false;
            }
        }

        // One order per unit: the order book counts purchase limits per order, and each order is delivered as one package.
        private static ShopOrder[] CreateOrders(List<ShopCheckoutLine> products, GameTimeSnapshot placedAt)
        {
            int count = 0;

            for (int i = 0; i < products.Count; i++)
                count = checked(count + products[i].Quantity);

            ShopOrder[] orders = new ShopOrder[count];
            int next = 0;

            for (int i = 0; i < products.Count; i++)
            {
                ShopPurchaseOffer offer = products[i].Offer;
                GameTimeSnapshot deliveryDueAt = offer.GetDeliveryDueAt(placedAt);

                for (int unit = 0; unit < products[i].Quantity; unit++)
                    orders[next++] = ShopOrder.CreateNew(offer.ProductId, offer.PriceCents, placedAt, deliveryDueAt);
            }

            return orders;
        }

        private static int IndexOf(List<ShopCheckoutLine> products, string productId)
        {
            for (int i = 0; i < products.Count; i++)
            {
                if (string.Equals(products[i].Offer.ProductId, productId, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        // A default (never constructed) offer has no Product ID and no price.
        private static bool IsValidOffer(in ShopPurchaseOffer offer)
        {
            return ShopId.IsValid(offer.ProductId) && offer.PriceCents > 0;
        }

        private static bool HaveSameTerms(in ShopPurchaseOffer left, in ShopPurchaseOffer right)
        {
            return left.PriceCents == right.PriceCents &&
                   left.MaxPurchases == right.MaxPurchases &&
                   left.DeliveryDelayMinutes == right.DeliveryDelayMinutes &&
                   left.IsAvailable == right.IsAvailable;
        }
    }
}
