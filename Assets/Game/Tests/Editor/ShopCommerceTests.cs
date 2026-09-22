using GoLive.Economy;
using GoLive.GameTime;
using GoLive.Shop;
using NUnit.Framework;

namespace GoLive.Tests
{
    public sealed class ShopCommerceTests
    {
        private const string BudgetGpuId = "budget-gpu";
        private const string BasicKeyboardId = "basic-keyboard";

        private static readonly ShopPurchaseOffer BudgetGpu = new(BudgetGpuId, 1500, 1, 150, true);
        private static readonly ShopPurchaseOffer BasicKeyboard = new(BasicKeyboardId, 800, 1, 0, false);

        private Wallet _wallet;
        private ShopOrderBook _orders;
        private GameClock _clock;
        private ShopPurchase _purchase;

        [SetUp]
        public void SetUp()
        {
            _wallet = new Wallet(2500);
            _orders = new ShopOrderBook();
            _clock = new GameClock(1, 7, 3);
            _purchase = new ShopPurchase(_wallet, _orders, _clock);
        }

        [Test]
        public void PurchaseSpendsPriceOnceAndPlacesSingleOrder()
        {
            ShopPurchaseResult result = _purchase.TryPurchase(BudgetGpu);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(_wallet.BalanceCents, Is.EqualTo(1000));
            Assert.That(_orders.Orders, Has.Count.EqualTo(1));
            Assert.That(result.Order.ProductId, Is.EqualTo(BudgetGpuId));
            Assert.That(result.Order.PaidPriceCents, Is.EqualTo(1500));
            Assert.That(result.Order.Status, Is.EqualTo(ShopOrderStatus.Placed));
            Assert.That(result.Order.DeliveryDueAt.Hour, Is.EqualTo(9));
            Assert.That(result.Order.DeliveryDueAt.Minute, Is.EqualTo(33));
        }

        [Test]
        public void RepeatedPurchaseOfLimitedProductChangesNothing()
        {
            _purchase.TryPurchase(BudgetGpu);

            ShopPurchaseResult second = _purchase.TryPurchase(BudgetGpu);

            Assert.That(second.Code, Is.EqualTo(ShopPurchaseResultCode.PurchaseLimitReached));
            Assert.That(_purchase.Evaluate(BudgetGpu), Is.EqualTo(ShopPurchaseResultCode.PurchaseLimitReached));
            Assert.That(_wallet.BalanceCents, Is.EqualTo(1000));
            Assert.That(_orders.Orders, Has.Count.EqualTo(1));
        }

        [Test]
        public void InsufficientFundsKeepsWalletAndOrdersUntouched()
        {
            _wallet.Restore(1000);

            ShopPurchaseResult result = _purchase.TryPurchase(BudgetGpu);

            Assert.That(result.Code, Is.EqualTo(ShopPurchaseResultCode.InsufficientFunds));
            Assert.That(_wallet.BalanceCents, Is.EqualTo(1000));
            Assert.That(_orders.Orders, Is.Empty);
        }

        [Test]
        public void UnavailableProductCannotBePurchased()
        {
            ShopPurchaseResult result = _purchase.TryPurchase(BasicKeyboard);

            Assert.That(result.Code, Is.EqualTo(ShopPurchaseResultCode.Unavailable));
            Assert.That(_wallet.BalanceCents, Is.EqualTo(2500));
            Assert.That(_orders.Orders, Is.Empty);
        }

        [Test]
        public void RestoredStateKeepsOrderAndPurchaseLimit()
        {
            ShopOrder placed = _purchase.TryPurchase(BudgetGpu).Order;
            long savedBalance = _wallet.BalanceCents;
            ShopOrdersSnapshot savedOrders = _orders.CaptureSnapshot();

            Wallet wallet = new(2500);
            ShopOrderBook orders = new();
            wallet.Restore(savedBalance);
            orders.Restore(savedOrders);
            ShopPurchase purchase = new(wallet, orders, _clock);

            Assert.That(wallet.BalanceCents, Is.EqualTo(1000));
            Assert.That(orders.Orders, Has.Count.EqualTo(1));
            Assert.That(orders.Orders[0].OrderId, Is.EqualTo(placed.OrderId));
            Assert.That(orders.Orders[0].DeliveryDueAt.TotalSeconds, Is.EqualTo(placed.DeliveryDueAt.TotalSeconds));
            Assert.That(purchase.Evaluate(BudgetGpu), Is.EqualTo(ShopPurchaseResultCode.PurchaseLimitReached));
            Assert.That(purchase.TryPurchase(BudgetGpu).Succeeded, Is.False);
            Assert.That(wallet.BalanceCents, Is.EqualTo(1000));
        }

        [Test]
        public void DeliveryStatusIsOwnedByOrderBook()
        {
            ShopOrder order = _purchase.TryPurchase(BudgetGpu).Order;
            int changes = 0;
            _orders.Changed += () => changes++;

            Assert.That(_orders.TryMarkDelivered(order.OrderId, _clock.Current), Is.False);

            _clock.AdvanceMinutes(150);

            Assert.That(_orders.TryMarkDelivered(order.OrderId, _clock.Current), Is.True);
            Assert.That(order.Status, Is.EqualTo(ShopOrderStatus.Delivered));
            Assert.That(changes, Is.EqualTo(1));
        }
    }
}
