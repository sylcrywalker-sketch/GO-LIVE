using System;
using System.Collections.Generic;
using GoLive.Economy;
using GoLive.GameTime;
using UnityEngine;

namespace GoLive.Shop
{
    // Unity side of the Shop: wires the catalog, the wallet and the clock to the plain C# commerce objects it owns (the
    // cart, the order book and the one checkout) and exposes them as commands and queries. The purchase rules live in
    // ShopCheckout; this class only turns cart lines and catalog products into checkout lines.
    [DisallowMultipleComponent]
    public sealed class ShopBehaviour : MonoBehaviour
    {
        [SerializeField] private WalletBehaviour wallet;
        [SerializeField] private GameClockBehaviour gameClock;
        [SerializeField] private ShopCatalogConfig catalog;

        // Paid orders: persistent, saved through CaptureOrders/RestoreOrders and consumed by Delivery.
        public ShopOrderBook Orders { get; } = new();
        public bool IsReady => _checkout != null;

        public IReadOnlyList<ShopProductDefinition> Products =>
            catalog != null ? catalog.Products : Array.Empty<ShopProductDefinition>();

        // The session's cart. Transient: never saved, emptied by a load.
        public IReadOnlyList<ShopCartLine> CartLines => _cart.Lines;
        public int CartItemCount => _cart.TotalQuantity;

        // The balance, the orders or the cart changed.
        public event Action Changed;

        private readonly ShopCart _cart = new();

        private ShopCheckout _checkout;
        private bool _started;
        private bool _bound;

        private void Awake()
        {
            if (!ValidateConfiguration())
                enabled = false;
        }

        private void Start()
        {
            if (!isActiveAndEnabled)
                return;

            if (wallet.Wallet == null || gameClock.Clock == null)
            {
                Debug.LogError(
                    $"{nameof(ShopBehaviour)} could not access initialized Wallet or Game Clock.",
                    this);

                enabled = false;
                return;
            }

            _checkout = new ShopCheckout(
                wallet.Wallet,
                Orders,
                gameClock.Clock);

            _started = true;

            Bind();
            Changed?.Invoke();
        }

        private void OnEnable()
        {
            if (_started)
                Bind();
        }

        private void OnDisable()
        {
            Unbind();
        }

        public bool TryGetProduct(string productId, out ShopProductDefinition product)
        {
            product = null;

            if (catalog == null)
                return false;

            return catalog.TryGetProduct(productId, out product);
        }

        public int GetCartQuantity(string productId)
        {
            return _cart.GetQuantity(productId);
        }

        // Whether one more unit fits the product's terms, counting what the cart already holds. Funds are checked at
        // checkout, not here.
        public ShopPurchaseResultCode EvaluateAddToCart(string productId)
        {
            if (!TryGetProduct(productId, out ShopProductDefinition product))
                return ShopPurchaseResultCode.ProductNotFound;

            if (_checkout == null)
                return ShopPurchaseResultCode.NotReady;

            long requested = (long)_cart.GetQuantity(productId) + 1;

            if (requested > int.MaxValue)
                return ShopPurchaseResultCode.PurchaseLimitReached;

            return _checkout.EvaluateQuantity(product.CreatePurchaseOffer(), (int)requested);
        }

        // Adds one unit to the cart. Never charges the wallet and never places an order.
        public ShopPurchaseResultCode TryAddToCart(string productId)
        {
            ShopPurchaseResultCode evaluation = EvaluateAddToCart(productId);

            if (evaluation != ShopPurchaseResultCode.Success)
                return evaluation;

            return _cart.TryAdd(productId)
                ? ShopPurchaseResultCode.Success
                : ShopPurchaseResultCode.InvalidRequest;
        }

        public bool TryRemoveOneFromCart(string productId)
        {
            return _cart.TryRemoveOne(productId);
        }

        public bool TryRemoveFromCart(string productId)
        {
            return _cart.TryRemoveAll(productId);
        }

        public ShopPurchaseResultCode EvaluateCheckout()
        {
            if (_checkout == null)
                return ShopPurchaseResultCode.NotReady;

            return TryCreateCartLines(out ShopCheckoutLine[] lines, out ShopPurchaseResultCode code)
                ? _checkout.Evaluate(lines)
                : code;
        }

        // The cart at the catalog's current prices.
        public long GetCartTotalCents()
        {
            return TryCreateCartLines(out ShopCheckoutLine[] lines, out _) &&
                   ShopCheckout.TryCalculateTotal(lines, out long totalCents)
                ? totalCents
                : 0;
        }

        // Pays for the whole cart in one transaction. The cart empties only once the purchase has been committed.
        public ShopCheckoutResult TryCheckout()
        {
            if (_checkout == null)
                return ShopCheckoutResult.Failure(ShopPurchaseResultCode.NotReady);

            if (!TryCreateCartLines(out ShopCheckoutLine[] lines, out ShopPurchaseResultCode code))
                return ShopCheckoutResult.Failure(code);

            int ordersBefore = Orders.Orders.Count;

            try
            {
                return _checkout.TryCheckout(lines);
            }
            finally
            {
                // ShopCheckout is the only writer of new orders: a grown order book means the cart has been paid for,
                // also when a Wallet or Orders subscriber threw after the commit. Paid lines never stay in the cart.
                if (Orders.Orders.Count != ordersBefore)
                    _cart.Clear();
            }
        }

        public bool TryEstimateDelivery(string productId, out GameTimeSnapshot deliveryDueAt)
        {
            deliveryDueAt = default;

            if (!TryGetProduct(productId, out ShopProductDefinition product) ||
                !product.IsAvailable ||
                gameClock.Clock == null)
            {
                return false;
            }

            ShopPurchaseOffer offer = product.CreatePurchaseOffer();

            deliveryDueAt = offer.GetDeliveryDueAt(gameClock.Clock.Current);
            return true;
        }

        public ShopOrdersSnapshot CaptureOrders()
        {
            return Orders.CaptureSnapshot();
        }

        // A load replaces the paid orders and empties the transient cart.
        public void RestoreOrders(ShopOrdersSnapshot snapshot)
        {
            if (gameClock.Clock == null)
                throw new InvalidOperationException("Game Clock is not initialized.");

            long currentGameTimeSeconds = gameClock.Clock.Current.TotalSeconds;

            if (!ValidateOrdersSnapshot(snapshot, currentGameTimeSeconds))
                throw new ArgumentException("Shop orders snapshot is invalid.", nameof(snapshot));

            _cart.Clear();
            Orders.Restore(snapshot);
        }

        public bool ValidateOrdersSnapshot(
            ShopOrdersSnapshot snapshot,
            long currentGameTimeSeconds)
        {
            if (snapshot == null || currentGameTimeSeconds < 0)
                return false;

            ShopOrderBook candidate = new();

            try
            {
                candidate.Restore(snapshot);
            }
            catch (ArgumentException)
            {
                return false;
            }

            for (int i = 0; i < candidate.Orders.Count; i++)
            {
                ShopOrder order = candidate.Orders[i];

                if (!catalog.TryGetProduct(order.ProductId, out ShopProductDefinition product))
                    return false;

                if (order.PlacedAt.TotalSeconds > currentGameTimeSeconds)
                    return false;

                if (order.Status == ShopOrderStatus.Delivered &&
                    order.DeliveryDueAt.TotalSeconds > currentGameTimeSeconds)
                {
                    return false;
                }

                if (product.MaxPurchases > 0 &&
                    candidate.CountForProduct(order.ProductId) > product.MaxPurchases)
                {
                    return false;
                }
            }

            return true;
        }

        public bool TryGetOrder(string orderId, out ShopOrder order)
        {
            return Orders.TryGetOrder(orderId, out order);
        }

        public bool TryMarkDelivered(string orderId)
        {
            if (gameClock.Clock == null)
                return false;

            return Orders.TryMarkDelivered(
                orderId,
                gameClock.Clock.Current);
        }

        private bool TryCreateCartLines(out ShopCheckoutLine[] lines, out ShopPurchaseResultCode code)
        {
            IReadOnlyList<ShopCartLine> cart = _cart.Lines;

            lines = new ShopCheckoutLine[cart.Count];

            for (int i = 0; i < cart.Count; i++)
            {
                if (!TryGetProduct(cart[i].ProductId, out ShopProductDefinition product))
                {
                    lines = null;
                    code = ShopPurchaseResultCode.ProductNotFound;
                    return false;
                }

                lines[i] = new ShopCheckoutLine(product.CreatePurchaseOffer(), cart[i].Quantity);
            }

            code = ShopPurchaseResultCode.Success;
            return true;
        }

        private void Bind()
        {
            if (_bound || wallet.Wallet == null)
                return;

            wallet.Wallet.BalanceChanged += HandleBalanceChanged;
            Orders.Changed += HandleStateChanged;
            _cart.Changed += HandleStateChanged;

            _bound = true;
        }

        private void Unbind()
        {
            if (!_bound)
                return;

            if (wallet != null && wallet.Wallet != null)
                wallet.Wallet.BalanceChanged -= HandleBalanceChanged;

            Orders.Changed -= HandleStateChanged;
            _cart.Changed -= HandleStateChanged;

            _bound = false;
        }

        private void HandleBalanceChanged(long balanceCents)
        {
            Changed?.Invoke();
        }

        private void HandleStateChanged()
        {
            Changed?.Invoke();
        }

        private bool ValidateConfiguration()
        {
            if (wallet == null)
            {
                Debug.LogError(
                    $"{nameof(ShopBehaviour)} on {name} requires a Wallet.",
                    this);

                return false;
            }

            if (gameClock == null)
            {
                Debug.LogError(
                    $"{nameof(ShopBehaviour)} on {name} requires a Game Clock.",
                    this);

                return false;
            }

            if (catalog == null)
            {
                Debug.LogError(
                    $"{nameof(ShopBehaviour)} on {name} requires a Shop Catalog.",
                    this);

                return false;
            }

            if (!catalog.Validate(out string error))
            {
                Debug.LogError(
                    $"{nameof(ShopCatalogConfig)} assigned to {name} is invalid: {error}",
                    catalog);

                return false;
            }

            return true;
        }
    }
}
