using System;
using System.Linq;
using System.Reflection;
using GoLive.Economy;
using GoLive.GameTime;
using GoLive.Items;
using GoLive.Shop;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GoLive.Tests
{
    // ShopBehaviour as the Shop's facade: [+] only fills the cart, the cart is paid for only through the one checkout,
    // and the cart is session state that a load empties.
    public sealed class ShopBehaviourTests
    {
        private const string GpuId = "budget-gpu";
        private const string BananaId = "banana";
        private const string RamId = "used-ram";

        private GameObject _root;
        private ShopCatalogConfig _catalog;
        private Wallet _wallet;
        private GameClock _clock;
        private ShopBehaviour _shop;

        [SetUp]
        public void SetUp()
        {
            _catalog = ShopTestData.CreateCatalog(
                ShopTestData.CreateProduct(GpuId, 1500, ItemCategory.Electronics, ShopTestData.LoadItem(ShopTestData.BudgetGpuItem), maxPurchases: 1),
                ShopTestData.CreateProduct(BananaId, 60, ItemCategory.Food, ShopTestData.LoadItem(ShopTestData.BananaItem), deliveryDelayMinutes: 90),
                ShopTestData.CreateProduct(RamId, 600, ItemCategory.Electronics, ShopTestData.LoadItem(ShopTestData.BudgetGpuItem), maxPurchases: 2),
                ShopTestData.CreateProduct("used-cpu", 1200, ItemCategory.Electronics, null, available: false, deliveryDelayMinutes: 0));

            _root = new GameObject("Shop facade test");
            _root.SetActive(false);

            WalletBehaviour wallet = _root.AddComponent<WalletBehaviour>();
            _wallet = new Wallet(2500);
            ShopTestData.Set(wallet, "<Wallet>k__BackingField", _wallet);

            GameClockBehaviour clock = _root.AddComponent<GameClockBehaviour>();
            _clock = new GameClock(1, 7, 0);
            ShopTestData.Set(clock, "<Clock>k__BackingField", _clock);

            _shop = _root.AddComponent<ShopBehaviour>();
            ShopTestData.Set(_shop, "wallet", wallet);
            ShopTestData.Set(_shop, "gameClock", clock);
            ShopTestData.Set(_shop, "catalog", _catalog);
            ShopTestData.Set(_shop, "_checkout", new ShopCheckout(_wallet, _shop.Orders, _clock));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_catalog);
        }

        [Test]
        public void AddingToTheCartNeverChargesOrOrders()
        {
            Assert.That(_shop.TryAddToCart(GpuId), Is.EqualTo(ShopPurchaseResultCode.Success));
            Assert.That(_shop.TryAddToCart(BananaId), Is.EqualTo(ShopPurchaseResultCode.Success));
            Assert.That(_shop.TryAddToCart(BananaId), Is.EqualTo(ShopPurchaseResultCode.Success));

            Assert.That(_wallet.BalanceCents, Is.EqualTo(2500), "wallet unchanged");
            Assert.That(_shop.Orders.Orders, Is.Empty, "no order yet");
            Assert.That(_shop.CartItemCount, Is.EqualTo(3));
            Assert.That(_shop.GetCartQuantity(BananaId), Is.EqualTo(2));
            Assert.That(_shop.GetCartTotalCents(), Is.EqualTo(1500 + 2 * 60));
        }

        [Test]
        public void AddingNeedsNoMoneyButCheckoutDoes()
        {
            _wallet.Restore(100);

            Assert.That(_shop.TryAddToCart(GpuId), Is.EqualTo(ShopPurchaseResultCode.Success), "funds are checked at checkout");
            Assert.That(_shop.EvaluateCheckout(), Is.EqualTo(ShopPurchaseResultCode.InsufficientFunds));
        }

        [Test]
        public void TheCartCountsTowardsThePurchaseLimit()
        {
            Assert.That(_shop.EvaluateAddToCart(GpuId), Is.EqualTo(ShopPurchaseResultCode.Success));
            Assert.That(_shop.TryAddToCart(GpuId), Is.EqualTo(ShopPurchaseResultCode.Success));

            Assert.That(_shop.EvaluateAddToCart(GpuId), Is.EqualTo(ShopPurchaseResultCode.PurchaseLimitReached), "the one allowed unit is already in the cart");
            Assert.That(_shop.TryAddToCart(GpuId), Is.EqualTo(ShopPurchaseResultCode.PurchaseLimitReached));
            Assert.That(_shop.GetCartQuantity(GpuId), Is.EqualTo(1));
        }

        [Test]
        public void ThePurchaseLimitCountsOrdersAndTheCartTogether()
        {
            Assert.That(_shop.TryAddToCart(RamId), Is.EqualTo(ShopPurchaseResultCode.Success));
            Assert.That(_shop.TryCheckout().Succeeded, Is.True);

            Assert.That(_shop.TryAddToCart(RamId), Is.EqualTo(ShopPurchaseResultCode.Success), "one ordered, one in the cart: two of two");
            Assert.That(_shop.EvaluateAddToCart(RamId), Is.EqualTo(ShopPurchaseResultCode.PurchaseLimitReached));
        }

        [Test]
        public void UnavailableOrUnknownProductsNeverReachTheCart()
        {
            Assert.That(_shop.TryAddToCart("used-cpu"), Is.EqualTo(ShopPurchaseResultCode.Unavailable));
            Assert.That(_shop.TryAddToCart("retired-product"), Is.EqualTo(ShopPurchaseResultCode.ProductNotFound));
            Assert.That(_shop.CartLines, Is.Empty);
        }

        [Test]
        public void SuccessfulCheckoutPaysOnceAndEmptiesTheCart()
        {
            _shop.TryAddToCart(GpuId);
            _shop.TryAddToCart(BananaId);
            _shop.TryAddToCart(BananaId);
            int balanceChanges = 0;
            _wallet.BalanceChanged += _ => balanceChanges++;

            ShopCheckoutResult result = _shop.TryCheckout();

            Assert.That(result.Succeeded, Is.True, result.Code.ToString());
            Assert.That(result.ChargedCents, Is.EqualTo(1620));
            Assert.That(_wallet.BalanceCents, Is.EqualTo(2500 - 1620));
            Assert.That(balanceChanges, Is.EqualTo(1));
            Assert.That(_shop.Orders.Orders.Select(order => order.ProductId), Is.EqualTo(new[] { GpuId, BananaId, BananaId }));
            Assert.That(_shop.CartLines, Is.Empty);
            Assert.That(_shop.CartItemCount, Is.Zero);
        }

        [Test]
        public void FailedCheckoutKeepsTheCart()
        {
            _shop.TryAddToCart(GpuId);
            _shop.TryAddToCart(BananaId);
            _wallet.Restore(1000);

            ShopCheckoutResult result = _shop.TryCheckout();

            Assert.That(result.Code, Is.EqualTo(ShopPurchaseResultCode.InsufficientFunds));
            Assert.That(_wallet.BalanceCents, Is.EqualTo(1000));
            Assert.That(_shop.Orders.Orders, Is.Empty);
            Assert.That(_shop.CartLines.Select(line => (line.ProductId, line.Quantity)), Is.EqualTo(new[] { (GpuId, 1), (BananaId, 1) }));
        }

        [Test]
        public void EmptyCartCannotBeOrdered()
        {
            Assert.That(_shop.EvaluateCheckout(), Is.EqualTo(ShopPurchaseResultCode.EmptyRequest));
            Assert.That(_shop.TryCheckout().Code, Is.EqualTo(ShopPurchaseResultCode.EmptyRequest));
            Assert.That(_wallet.BalanceCents, Is.EqualTo(2500));
        }

        [Test]
        public void PaidLinesLeaveTheCartEvenWhenASubscriberThrowsAfterTheCommit()
        {
            _shop.TryAddToCart(BananaId);
            _wallet.BalanceChanged += _ => throw new InvalidOperationException("HUD failure");

            Assert.That(() => _shop.TryCheckout(), Throws.InvalidOperationException);

            Assert.That(_wallet.BalanceCents, Is.EqualTo(2440), "charged");
            Assert.That(_shop.Orders.Orders, Has.Count.EqualTo(1), "ordered");
            Assert.That(_shop.CartLines, Is.Empty, "and not left in the cart to be paid again");
        }

        [Test]
        public void CartButtonsChangeOnlyTheCart()
        {
            _shop.TryAddToCart(BananaId);
            _shop.TryAddToCart(BananaId);
            _shop.TryAddToCart(GpuId);

            Assert.That(_shop.TryRemoveOneFromCart(BananaId), Is.True);
            Assert.That(_shop.GetCartQuantity(BananaId), Is.EqualTo(1));
            Assert.That(_shop.TryRemoveFromCart(GpuId), Is.True);
            Assert.That(_shop.TryRemoveFromCart(GpuId), Is.False);
            Assert.That(_shop.CartLines.Select(line => line.ProductId), Is.EqualTo(new[] { BananaId }));
            Assert.That(_wallet.BalanceCents, Is.EqualTo(2500));
            Assert.That(_shop.Orders.Orders, Is.Empty);
        }

        [Test]
        public void RestoringOrdersEmptiesTheCart()
        {
            _shop.TryAddToCart(GpuId);
            Assert.That(_shop.TryCheckout().Succeeded, Is.True);
            ShopOrdersSnapshot saved = _shop.CaptureOrders();
            _shop.TryAddToCart(BananaId);

            _shop.RestoreOrders(saved);

            Assert.That(_shop.CartLines, Is.Empty);
            Assert.That(_shop.Orders.Orders.Select(order => order.ProductId), Is.EqualTo(new[] { GpuId }));
        }

        [Test]
        public void AnInvalidOrdersSnapshotChangesNeitherOrdersNorCart()
        {
            _shop.TryAddToCart(BananaId);

            Assert.That(() => _shop.RestoreOrders(new ShopOrdersSnapshot { Version = 99 }), Throws.ArgumentException);

            Assert.That(_shop.GetCartQuantity(BananaId), Is.EqualTo(1));
        }

        [Test]
        public void TheCheckoutIsTheOnlyWayToPay()
        {
            Assert.That(typeof(ShopBehaviour).Assembly.GetType("GoLive.Shop.ShopPurchase"), Is.Null, "no second transaction engine");

            string[] paying = typeof(ShopBehaviour)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(method => method.ReturnType == typeof(ShopCheckoutResult))
                .Select(method => method.Name)
                .ToArray();

            Assert.That(paying, Is.EqualTo(new[] { nameof(ShopBehaviour.TryCheckout) }));
            Assert.That(typeof(ShopBehaviour).GetMethod("TryPurchase"), Is.Null);
        }
    }
}
