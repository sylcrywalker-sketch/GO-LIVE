using System;
using System.Collections.Generic;
using GoLive.Economy;
using GoLive.GameTime;
using UnityEngine;

namespace GoLive.Shop
{
    [DisallowMultipleComponent]
    public sealed class ShopBehaviour : MonoBehaviour
    {
        [SerializeField] private WalletBehaviour wallet;
        [SerializeField] private GameClockBehaviour gameClock;
        [SerializeField] private ShopCatalogConfig catalog;

        public ShopOrderBook Orders { get; } = new();
        public ShopCart Cart { get; } = new();

        public bool IsReady => Checkout != null;

        public IReadOnlyList<ShopProductDefinition> Products =>
            catalog != null
                ? catalog.Products
                : Array.Empty<ShopProductDefinition>();

        public event Action Changed;

        // Kept for compatibility with the existing test/runtime setup.
        // ShopPurchase is now only an adapter over one ShopCheckout instance.
        private ShopPurchase _purchase;

        private bool _started;
        private bool _bound;

        private ShopCheckout Checkout =>
            _purchase?.Checkout;

        private void Awake()
        {
            if (!ValidateConfiguration())
                enabled = false;
        }

        private void Start()
        {
            if (!isActiveAndEnabled)
                return;

            if (wallet.Wallet == null ||
                gameClock.Clock == null)
            {
                Debug.LogError(
                    $"{nameof(ShopBehaviour)} could not access initialized Wallet or Game Clock.",
                    this);

                enabled = false;
                return;
            }

            _purchase = new ShopPurchase(
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

        // ==========================================================
        // CATALOG
        // ==========================================================

        public bool TryGetProduct(
            string productId,
            out ShopProductDefinition product)
        {
            product = null;

            if (catalog == null)
                return false;

            return catalog.TryGetProduct(
                productId,
                out product);
        }

        // ==========================================================
        // SINGLE-PRODUCT COMPATIBILITY API
        // ==========================================================

        public ShopPurchaseResultCode EvaluatePurchase(
            string productId)
        {
            if (!TryGetProduct(
                    productId,
                    out ShopProductDefinition product))
            {
                return ShopPurchaseResultCode.ProductNotFound;
            }

            if (_purchase == null)
                return ShopPurchaseResultCode.NotReady;

            ShopPurchaseOffer offer =
                product.CreatePurchaseOffer();

            return _purchase.Evaluate(in offer);
        }

        public ShopPurchaseResult TryPurchase(
            string productId)
        {
            if (!TryGetProduct(
                    productId,
                    out ShopProductDefinition product))
            {
                return ShopPurchaseResult.Failure(
                    ShopPurchaseResultCode.ProductNotFound);
            }

            if (_purchase == null)
            {
                return ShopPurchaseResult.Failure(
                    ShopPurchaseResultCode.NotReady);
            }

            ShopPurchaseOffer offer =
                product.CreatePurchaseOffer();

            return _purchase.TryPurchase(in offer);
        }

        // ==========================================================
        // CART
        // ==========================================================

        public ShopCartAddResultCode EvaluateAddToCart(
            string productId)
        {
            if (!IsReady)
                return ShopCartAddResultCode.NotReady;

            if (!TryGetProduct(
                    productId,
                    out ShopProductDefinition product))
            {
                return ShopCartAddResultCode.ProductNotFound;
            }

            if (!product.IsAvailable)
                return ShopCartAddResultCode.Unavailable;

            if (product.MaxPurchases > 0)
            {
                int alreadyPurchased =
                    Orders.CountForProduct(productId);

                int inCart =
                    Cart.GetQuantity(productId);

                if (alreadyPurchased + inCart >=
                    product.MaxPurchases)
                {
                    return ShopCartAddResultCode.PurchaseLimitReached;
                }
            }

            return ShopCartAddResultCode.Success;
        }

        public ShopCartAddResultCode TryAddToCart(
            string productId)
        {
            ShopCartAddResultCode result =
                EvaluateAddToCart(productId);

            if (result != ShopCartAddResultCode.Success)
                return result;

            Cart.AddOne(productId);
            return ShopCartAddResultCode.Success;
        }

        public bool TryRemoveOneFromCart(
            string productId)
        {
            return Cart.RemoveOne(productId);
        }

        public bool TryRemoveFromCart(
            string productId)
        {
            return Cart.RemoveAll(productId);
        }

        public bool TryGetCartTotal(
            out long totalCents)
        {
            totalCents = 0;

            IReadOnlyList<ShopCartEntry> entries =
                Cart.Entries;

            for (int i = 0; i < entries.Count; i++)
            {
                ShopCartEntry entry = entries[i];

                if (!TryGetProduct(
                        entry.ProductId,
                        out ShopProductDefinition product))
                {
                    totalCents = 0;
                    return false;
                }

                totalCents = checked(
                    totalCents +
                    checked(product.PriceCents * entry.Quantity));
            }

            return true;
        }

        public ShopCheckoutResultCode EvaluateCartCheckout()
        {
            if (!IsReady)
                return ShopCheckoutResultCode.NotReady;

            ShopCheckoutResultCode buildResult =
                TryBuildCheckoutLines(
                    out ShopCheckoutLine[] lines);

            if (buildResult != ShopCheckoutResultCode.Success)
                return buildResult;

            return Checkout.Evaluate(lines);
        }

        public ShopCheckoutResult TryCheckoutCart()
        {
            if (!IsReady)
            {
                return ShopCheckoutResult.Failure(
                    ShopCheckoutResultCode.NotReady);
            }

            ShopCheckoutResultCode buildResult =
                TryBuildCheckoutLines(
                    out ShopCheckoutLine[] lines);

            if (buildResult != ShopCheckoutResultCode.Success)
                return ShopCheckoutResult.Failure(buildResult);

            ShopCheckoutResult result =
                Checkout.TryCheckout(lines);

            if (result.Succeeded)
                Cart.Clear();

            return result;
        }

        private ShopCheckoutResultCode TryBuildCheckoutLines(
            out ShopCheckoutLine[] lines)
        {
            lines = Array.Empty<ShopCheckoutLine>();

            IReadOnlyList<ShopCartEntry> entries =
                Cart.Entries;

            if (entries.Count == 0)
                return ShopCheckoutResultCode.EmptyCart;

            ShopCheckoutLine[] result =
                new ShopCheckoutLine[entries.Count];

            for (int i = 0; i < entries.Count; i++)
            {
                ShopCartEntry entry = entries[i];

                if (!TryGetProduct(
                        entry.ProductId,
                        out ShopProductDefinition product))
                {
                    return ShopCheckoutResultCode.ProductNotFound;
                }

                result[i] = new ShopCheckoutLine(
                    product.CreatePurchaseOffer(),
                    entry.Quantity);
            }

            lines = result;
            return ShopCheckoutResultCode.Success;
        }

        // ==========================================================
        // DELIVERY / ORDERS
        // ==========================================================

        public bool TryEstimateDelivery(
            string productId,
            out GameTimeSnapshot deliveryDueAt)
        {
            deliveryDueAt = default;

            if (!TryGetProduct(
                    productId,
                    out ShopProductDefinition product) ||
                !product.IsAvailable ||
                gameClock.Clock == null)
            {
                return false;
            }

            ShopPurchaseOffer offer =
                product.CreatePurchaseOffer();

            deliveryDueAt =
                offer.GetDeliveryDueAt(
                    gameClock.Clock.Current);

            return true;
        }

        public ShopOrdersSnapshot CaptureOrders()
        {
            return Orders.CaptureSnapshot();
        }

        public void RestoreOrders(
            ShopOrdersSnapshot snapshot)
        {
            if (gameClock.Clock == null)
            {
                throw new InvalidOperationException(
                    "Game Clock is not initialized.");
            }

            long currentGameTimeSeconds =
                gameClock.Clock.Current.TotalSeconds;

            if (!ValidateOrdersSnapshot(
                    snapshot,
                    currentGameTimeSeconds))
            {
                throw new ArgumentException(
                    "Shop orders snapshot is invalid.",
                    nameof(snapshot));
            }

            Orders.Restore(snapshot);

            // Cart is intentionally transient and never survives Load.
            Cart.Clear();
        }

        public bool ValidateOrdersSnapshot(
            ShopOrdersSnapshot snapshot,
            long currentGameTimeSeconds)
        {
            if (snapshot == null ||
                currentGameTimeSeconds < 0)
            {
                return false;
            }

            ShopOrderBook candidate = new();

            try
            {
                candidate.Restore(snapshot);
            }
            catch (ArgumentException)
            {
                return false;
            }

            for (int i = 0;
                 i < candidate.Orders.Count;
                 i++)
            {
                ShopOrder order =
                    candidate.Orders[i];

                if (!catalog.TryGetProduct(
                        order.ProductId,
                        out ShopProductDefinition product))
                {
                    return false;
                }

                if (order.PlacedAt.TotalSeconds >
                    currentGameTimeSeconds)
                {
                    return false;
                }

                if (order.Status == ShopOrderStatus.Delivered &&
                    order.DeliveryDueAt.TotalSeconds >
                    currentGameTimeSeconds)
                {
                    return false;
                }

                if (product.MaxPurchases > 0 &&
                    candidate.CountForProduct(order.ProductId) >
                    product.MaxPurchases)
                {
                    return false;
                }
            }

            return true;
        }

        public bool TryGetOrder(
            string orderId,
            out ShopOrder order)
        {
            return Orders.TryGetOrder(
                orderId,
                out order);
        }

        public bool TryMarkDelivered(
            string orderId)
        {
            if (gameClock.Clock == null)
                return false;

            return Orders.TryMarkDelivered(
                orderId,
                gameClock.Clock.Current);
        }

        // ==========================================================
        // EVENTS / LIFECYCLE
        // ==========================================================

        private void Bind()
        {
            if (_bound ||
                wallet.Wallet == null)
            {
                return;
            }

            wallet.Wallet.BalanceChanged +=
                HandleBalanceChanged;

            Orders.Changed +=
                HandleOrdersChanged;

            Cart.Changed +=
                HandleCartChanged;

            _bound = true;
        }

        private void Unbind()
        {
            if (!_bound)
                return;

            if (wallet != null &&
                wallet.Wallet != null)
            {
                wallet.Wallet.BalanceChanged -=
                    HandleBalanceChanged;
            }

            Orders.Changed -=
                HandleOrdersChanged;

            Cart.Changed -=
                HandleCartChanged;

            _bound = false;
        }

        private void HandleBalanceChanged(
            long balanceCents)
        {
            Changed?.Invoke();
        }

        private void HandleOrdersChanged()
        {
            Changed?.Invoke();
        }

        private void HandleCartChanged()
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
