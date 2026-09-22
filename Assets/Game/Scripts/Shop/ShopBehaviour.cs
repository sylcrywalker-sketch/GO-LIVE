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
        public bool IsReady => _purchase != null;

        public IReadOnlyList<ShopProductDefinition> Products =>
            catalog != null ? catalog.Products : Array.Empty<ShopProductDefinition>();

        public event Action Changed;

        private ShopPurchase _purchase;
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

        public bool TryGetProduct(string productId, out ShopProductDefinition product)
        {
            product = null;

            if (catalog == null)
                return false;

            return catalog.TryGetProduct(productId, out product);
        }

        public ShopPurchaseResultCode EvaluatePurchase(string productId)
        {
            if (!TryGetProduct(productId, out ShopProductDefinition product))
                return ShopPurchaseResultCode.ProductNotFound;

            if (_purchase == null)
                return ShopPurchaseResultCode.NotReady;

            ShopPurchaseOffer offer = product.CreatePurchaseOffer();

            return _purchase.Evaluate(in offer);
        }

        public ShopPurchaseResult TryPurchase(string productId)
        {
            if (!TryGetProduct(productId, out ShopProductDefinition product))
                return ShopPurchaseResult.Failure(ShopPurchaseResultCode.ProductNotFound);

            if (_purchase == null)
                return ShopPurchaseResult.Failure(ShopPurchaseResultCode.NotReady);

            ShopPurchaseOffer offer = product.CreatePurchaseOffer();

            return _purchase.TryPurchase(in offer);
        }

        public ShopOrdersSnapshot CaptureOrders()
        {
            return Orders.CaptureSnapshot();
        }

        public void RestoreOrders(ShopOrdersSnapshot snapshot)
        {
            if (gameClock.Clock == null)
                throw new InvalidOperationException("Game Clock is not initialized.");

            long currentGameTimeSeconds = gameClock.Clock.Current.TotalSeconds;

            if (!ValidateOrdersSnapshot(snapshot, currentGameTimeSeconds))
                throw new ArgumentException("Shop orders snapshot is invalid.", nameof(snapshot));

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

        private void Bind()
        {
            if (_bound || wallet.Wallet == null)
                return;

            wallet.Wallet.BalanceChanged += HandleBalanceChanged;
            Orders.Changed += HandleOrdersChanged;

            _bound = true;
        }

        private void Unbind()
        {
            if (!_bound)
                return;

            if (wallet != null && wallet.Wallet != null)
                wallet.Wallet.BalanceChanged -= HandleBalanceChanged;

            Orders.Changed -= HandleOrdersChanged;

            _bound = false;
        }

        private void HandleBalanceChanged(long balanceCents)
        {
            Changed?.Invoke();
        }

        private void HandleOrdersChanged()
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