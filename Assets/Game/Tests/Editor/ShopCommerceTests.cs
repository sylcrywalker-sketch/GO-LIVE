using GoLive.Economy;
using GoLive.GameTime;
using GoLive.Shop;
using NUnit.Framework;

namespace GoLive.Tests
{
    // The order book: paid orders, their persistence and their delivery status. Orders are placed the only way the game
    // places them, through ShopCheckout.
    public sealed class ShopCommerceTests
    {
        private const string BudgetGpuId = "budget-gpu";

        private static readonly ShopPurchaseOffer BudgetGpu = new(BudgetGpuId, 1500, 1, 150, true);

        private Wallet _wallet;
        private ShopOrderBook _orders;
        private GameClock _clock;
        private ShopCheckout _checkout;

        [SetUp]
        public void SetUp()
        {
            _wallet = new Wallet(2500);
            _orders = new ShopOrderBook();
            _clock = new GameClock(1, 7, 3);
            _checkout = new ShopCheckout(_wallet, _orders, _clock);
        }

        [Test]
        public void RestoredStateKeepsOrderAndPurchaseLimit()
        {
            ShopOrder placed = Place(_checkout);
            long savedBalance = _wallet.BalanceCents;
            ShopOrdersSnapshot savedOrders = _orders.CaptureSnapshot();

            Wallet wallet = new(2500);
            ShopOrderBook orders = new();
            wallet.Restore(savedBalance);
            orders.Restore(savedOrders);
            ShopCheckout checkout = new(wallet, orders, _clock);

            Assert.That(wallet.BalanceCents, Is.EqualTo(1000));
            Assert.That(orders.Orders, Has.Count.EqualTo(1));
            Assert.That(orders.Orders[0].OrderId, Is.EqualTo(placed.OrderId));
            Assert.That(orders.Orders[0].DeliveryDueAt.TotalSeconds, Is.EqualTo(placed.DeliveryDueAt.TotalSeconds));
            Assert.That(checkout.EvaluateQuantity(BudgetGpu, 1), Is.EqualTo(ShopPurchaseResultCode.PurchaseLimitReached));
            Assert.That(checkout.TryCheckout(new[] { new ShopCheckoutLine(BudgetGpu, 1) }).Succeeded, Is.False);
            Assert.That(wallet.BalanceCents, Is.EqualTo(1000));
        }

        [Test]
        public void DeliveredOrdersStayInHistoryButStopCountingAsActive()
        {
            ShopOrder order = Place(_checkout);

            Assert.That(_orders.CountActive(), Is.EqualTo(1));
            Assert.That(_orders.TryGetLatestActiveOrder(BudgetGpuId, out ShopOrder active), Is.True);
            Assert.That(active, Is.SameAs(order));

            _clock.AdvanceMinutes(150);
            _orders.TryMarkDelivered(order.OrderId, _clock.Current);

            Assert.That(_orders.Orders, Has.Count.EqualTo(1));
            Assert.That(_orders.Orders[0].Status, Is.EqualTo(ShopOrderStatus.Delivered));
            Assert.That(_orders.CountActive(), Is.Zero);
            Assert.That(_orders.TryGetLatestActiveOrder(BudgetGpuId, out _), Is.False);
            Assert.That(_checkout.EvaluateQuantity(BudgetGpu, 1), Is.EqualTo(ShopPurchaseResultCode.PurchaseLimitReached));
        }

        [Test]
        public void DeliveryStatusIsOwnedByOrderBook()
        {
            ShopOrder order = Place(_checkout);
            int changes = 0;
            _orders.Changed += () => changes++;

            Assert.That(_orders.TryMarkDelivered(order.OrderId, _clock.Current), Is.False);

            _clock.AdvanceMinutes(150);

            Assert.That(_orders.TryMarkDelivered(order.OrderId, _clock.Current), Is.True);
            Assert.That(order.Status, Is.EqualTo(ShopOrderStatus.Delivered));
            Assert.That(changes, Is.EqualTo(1));
        }

        private static ShopOrder Place(ShopCheckout checkout)
        {
            ShopCheckoutResult result = checkout.TryCheckout(new[] { new ShopCheckoutLine(BudgetGpu, 1) });
            Assert.That(result.Succeeded, Is.True, result.Code.ToString());
            return result.Orders[0];
        }
    }
}
