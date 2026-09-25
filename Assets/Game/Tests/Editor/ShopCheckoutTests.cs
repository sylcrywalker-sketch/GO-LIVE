using System;
using System.Linq;
using GoLive.Economy;
using GoLive.GameTime;
using GoLive.Shop;
using NUnit.Framework;

namespace GoLive.Tests
{
    // ShopCheckout, the one owner of the purchase rules, without Unity: a request is validated as a whole before
    // anything changes, and a committed checkout changes the wallet once and the order book once.
    public sealed class ShopCheckoutTests
    {
        private static readonly ShopPurchaseOffer BudgetGpu = new("budget-gpu", 1500, 1, 150, true);
        private static readonly ShopPurchaseOffer Microphone = new("used-microphone", 900, 1, 150, true);
        private static readonly ShopPurchaseOffer Banana = new("banana", 60, 0, 90, true);
        private static readonly ShopPurchaseOffer Mug = new("mug", 200, 0, 120, true);
        private static readonly ShopPurchaseOffer Keyboard = new("basic-keyboard", 800, 1, 0, false);

        private Wallet _wallet;
        private ShopOrderBook _orders;
        private GameClock _clock;
        private ShopCheckout _checkout;
        private int _balanceChanges;
        private int _orderChanges;

        [SetUp]
        public void SetUp()
        {
            _wallet = new Wallet(2500);
            _orders = new ShopOrderBook();
            _clock = new GameClock(1, 7, 3);
            _checkout = new ShopCheckout(_wallet, _orders, _clock);

            _balanceChanges = 0;
            _orderChanges = 0;
            _wallet.BalanceChanged += _ => _balanceChanges++;
            _orders.Changed += () => _orderChanges++;
        }

        [Test]
        public void SingleProductCheckoutChargesOnceAndPlacesOneOrder()
        {
            ShopCheckoutResult result = _checkout.TryCheckout(Lines((BudgetGpu, 1)));

            Assert.That(result.Succeeded, Is.True, result.Code.ToString());
            Assert.That(result.ChargedCents, Is.EqualTo(1500));
            Assert.That(_wallet.BalanceCents, Is.EqualTo(1000));
            Assert.That(_balanceChanges, Is.EqualTo(1));
            Assert.That(_orders.Orders, Has.Count.EqualTo(1));
            Assert.That(result.Orders, Is.EqualTo(_orders.Orders));

            ShopOrder order = result.Orders[0];
            Assert.That(order.ProductId, Is.EqualTo("budget-gpu"));
            Assert.That(order.PaidPriceCents, Is.EqualTo(1500));
            Assert.That(order.Status, Is.EqualTo(ShopOrderStatus.Placed));
            Assert.That(order.DeliveryDueAt.Hour, Is.EqualTo(9));
            Assert.That(order.DeliveryDueAt.Minute, Is.EqualTo(33));
        }

        [Test]
        public void MultiProductCheckoutChargesTheTotalOnceAndPlacesEveryOrder()
        {
            ShopCheckoutResult result = _checkout.TryCheckout(Lines((BudgetGpu, 1), (Microphone, 1)));

            Assert.That(result.Succeeded, Is.True, result.Code.ToString());
            Assert.That(result.ChargedCents, Is.EqualTo(2400));
            Assert.That(_wallet.BalanceCents, Is.EqualTo(100));
            Assert.That(_balanceChanges, Is.EqualTo(1), "one charge for the whole cart");
            Assert.That(_orderChanges, Is.EqualTo(1), "one Orders notification for the whole cart");
            Assert.That(_orders.Orders.Select(order => order.ProductId), Is.EqualTo(new[] { "budget-gpu", "used-microphone" }));
            Assert.That(_orders.Orders.Select(order => order.PaidPriceCents), Is.EqualTo(new[] { 1500L, 900L }));
            Assert.That(_orders.Orders.Select(order => order.OrderId).Distinct().Count(), Is.EqualTo(2));
        }

        [Test]
        public void QuantityPlacesOneOrderPerUnit()
        {
            ShopCheckoutResult result = _checkout.TryCheckout(Lines((Banana, 3), (Mug, 2)));

            Assert.That(result.Succeeded, Is.True, result.Code.ToString());
            Assert.That(result.ChargedCents, Is.EqualTo(3 * 60 + 2 * 200));
            Assert.That(_wallet.BalanceCents, Is.EqualTo(2500 - 580));
            Assert.That(_orders.CountForProduct("banana"), Is.EqualTo(3));
            Assert.That(_orders.CountForProduct("mug"), Is.EqualTo(2));
            Assert.That(_orders.CountActive(), Is.EqualTo(5));
            Assert.That(_balanceChanges, Is.EqualTo(1));
            Assert.That(_orderChanges, Is.EqualTo(1));
        }

        [Test]
        public void InsufficientAggregateFundsChangesNothing()
        {
            _wallet.Restore(2000);
            _balanceChanges = 0;

            // Each product alone is affordable, together they are not.
            ShopPurchaseResultCode evaluation = _checkout.Evaluate(Lines((BudgetGpu, 1), (Microphone, 1)));
            ShopCheckoutResult result = _checkout.TryCheckout(Lines((BudgetGpu, 1), (Microphone, 1)));

            Assert.That(evaluation, Is.EqualTo(ShopPurchaseResultCode.InsufficientFunds));
            Assert.That(result.Code, Is.EqualTo(ShopPurchaseResultCode.InsufficientFunds));
            Assert.That(result.Orders, Is.Empty);
            AssertNothingChanged(2000);
        }

        [Test]
        public void UnavailableProductChangesNothing()
        {
            ShopCheckoutResult result = _checkout.TryCheckout(Lines((Banana, 1), (Keyboard, 1)));

            Assert.That(result.Code, Is.EqualTo(ShopPurchaseResultCode.Unavailable));
            AssertNothingChanged(2500);
        }

        [Test]
        public void PurchaseLimitCountsExistingOrdersAndTheRequest()
        {
            ShopPurchaseOffer twoPerCustomer = new("used-ram", 100, 2, 150, true);

            Assert.That(_checkout.EvaluateQuantity(twoPerCustomer, 2), Is.EqualTo(ShopPurchaseResultCode.Success));
            Assert.That(_checkout.EvaluateQuantity(twoPerCustomer, 3), Is.EqualTo(ShopPurchaseResultCode.PurchaseLimitReached));

            Assert.That(_checkout.TryCheckout(Lines((twoPerCustomer, 1))).Succeeded, Is.True);
            long balance = _wallet.BalanceCents;
            _balanceChanges = 0;
            _orderChanges = 0;

            Assert.That(_checkout.EvaluateQuantity(twoPerCustomer, 1), Is.EqualTo(ShopPurchaseResultCode.Success));
            Assert.That(_checkout.EvaluateQuantity(twoPerCustomer, 2), Is.EqualTo(ShopPurchaseResultCode.PurchaseLimitReached));

            ShopCheckoutResult overLimit = _checkout.TryCheckout(Lines((twoPerCustomer, 2), (Banana, 1)));

            Assert.That(overLimit.Code, Is.EqualTo(ShopPurchaseResultCode.PurchaseLimitReached));
            Assert.That(_orders.CountForProduct("used-ram"), Is.EqualTo(1));
            Assert.That(_orders.CountForProduct("banana"), Is.Zero, "no partial checkout");
            AssertNothingChanged(balance, expectedOrders: 1);
        }

        [Test]
        public void QuantityAboveTheCartMaximumIsRefused()
        {
            Assert.That(_checkout.EvaluateQuantity(Banana, ShopCheckout.MaxQuantityPerProduct), Is.EqualTo(ShopPurchaseResultCode.Success));
            Assert.That(_checkout.EvaluateQuantity(Banana, ShopCheckout.MaxQuantityPerProduct + 1), Is.EqualTo(ShopPurchaseResultCode.PurchaseLimitReached));
            Assert.That(_checkout.TryCheckout(Lines((Banana, ShopCheckout.MaxQuantityPerProduct + 1))).Code, Is.EqualTo(ShopPurchaseResultCode.PurchaseLimitReached));
            AssertNothingChanged(2500);
        }

        [Test]
        public void DuplicateLinesWithTheSameTermsAddUp()
        {
            ShopCheckoutResult result = _checkout.TryCheckout(Lines((Banana, 2), (Mug, 1), (Banana, 1)));

            Assert.That(result.Succeeded, Is.True, result.Code.ToString());
            Assert.That(result.ChargedCents, Is.EqualTo(3 * 60 + 200));
            Assert.That(_orders.CountForProduct("banana"), Is.EqualTo(3));
            Assert.That(ShopCheckout.TryCalculateTotal(Lines((Banana, 2), (Banana, 1)), out long total), Is.True);
            Assert.That(total, Is.EqualTo(180));
        }

        [Test]
        public void DuplicateLinesOfALimitedProductAreLimitedTogether()
        {
            ShopCheckoutResult result = _checkout.TryCheckout(Lines((BudgetGpu, 1), (BudgetGpu, 1)));

            Assert.That(result.Code, Is.EqualTo(ShopPurchaseResultCode.PurchaseLimitReached));
            AssertNothingChanged(2500);
        }

        [TestCase("price")]
        [TestCase("purchase limit")]
        [TestCase("delivery delay")]
        [TestCase("availability")]
        public void DuplicateLinesWithConflictingTermsAreRejected(string term)
        {
            ShopPurchaseOffer conflicting = term switch
            {
                "price" => new ShopPurchaseOffer("banana", 70, 0, 90, true),
                "purchase limit" => new ShopPurchaseOffer("banana", 60, 5, 90, true),
                "delivery delay" => new ShopPurchaseOffer("banana", 60, 0, 30, true),
                _ => new ShopPurchaseOffer("banana", 60, 0, 90, false)
            };

            ShopCheckoutResult result = _checkout.TryCheckout(Lines((Banana, 1), (conflicting, 1)));

            Assert.That(result.Code, Is.EqualTo(ShopPurchaseResultCode.InvalidRequest));
            Assert.That(ShopCheckout.TryCalculateTotal(Lines((Banana, 1), (conflicting, 1)), out _), Is.False);
            AssertNothingChanged(2500);
        }

        [Test]
        public void EmptyRequestIsRejected()
        {
            Assert.That(_checkout.TryCheckout(Array.Empty<ShopCheckoutLine>()).Code, Is.EqualTo(ShopPurchaseResultCode.EmptyRequest));
            Assert.That(_checkout.TryCheckout(null).Code, Is.EqualTo(ShopPurchaseResultCode.EmptyRequest));
            AssertNothingChanged(2500);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void NonPositiveQuantityIsRejected(int quantity)
        {
            Assert.That(_checkout.TryCheckout(Lines((Banana, 1), (Mug, quantity))).Code, Is.EqualTo(ShopPurchaseResultCode.InvalidRequest));
            Assert.That(_checkout.EvaluateQuantity(Banana, quantity), Is.EqualTo(ShopPurchaseResultCode.InvalidRequest));
            AssertNothingChanged(2500);
        }

        [Test]
        public void LineWithoutAnOfferIsRejected()
        {
            ShopCheckoutLine[] lines = { new(Banana, 1), default };

            Assert.That(_checkout.TryCheckout(lines).Code, Is.EqualTo(ShopPurchaseResultCode.InvalidRequest));
            AssertNothingChanged(2500);
        }

        [Test]
        public void WalletSubscriberThatThrowsAfterTheChargeLeavesWalletAndOrdersCommittedTogether()
        {
            Action<long> failingHud = _ => throw new InvalidOperationException("HUD failure");
            _wallet.BalanceChanged += failingHud;

            Assert.That(() => _checkout.TryCheckout(Lines((BudgetGpu, 1), (Banana, 2))), Throws.InvalidOperationException);

            Assert.That(_wallet.BalanceCents, Is.EqualTo(2500 - 1500 - 120), "charged");
            Assert.That(_orders.Orders, Has.Count.EqualTo(3), "and paid for");
            Assert.That(_orderChanges, Is.EqualTo(1), "the orders are still announced");

            _wallet.BalanceChanged -= failingHud;
            Assert.That(_checkout.TryCheckout(Lines((Banana, 1))).Succeeded, Is.True, "the checkout is not left busy");
        }

        [Test]
        public void OrdersSubscriberThatThrowsAfterTheCommitLeavesWalletAndOrdersCommittedTogether()
        {
            _orders.Changed += () => throw new InvalidOperationException("view failure");

            Assert.That(() => _checkout.TryCheckout(Lines((Microphone, 1))), Throws.InvalidOperationException);

            Assert.That(_wallet.BalanceCents, Is.EqualTo(1600));
            Assert.That(_orders.Orders, Has.Count.EqualTo(1));
            Assert.That(_orders.Orders[0].ProductId, Is.EqualTo("used-microphone"));
        }

        [Test]
        public void CheckoutStartedFromInsideACommitIsRefusedAsBusy()
        {
            ShopCheckoutResult nested = null;
            _wallet.BalanceChanged += _ => nested ??= _checkout.TryCheckout(Lines((Banana, 1)));

            ShopCheckoutResult result = _checkout.TryCheckout(Lines((Mug, 1)));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(nested.Code, Is.EqualTo(ShopPurchaseResultCode.Busy));
            Assert.That(_orders.Orders.Select(order => order.ProductId), Is.EqualTo(new[] { "mug" }));
            Assert.That(_wallet.BalanceCents, Is.EqualTo(2300));
        }

        [Test]
        public void EveryOrderIsDueAfterItsOwnProductsDeliveryDelay()
        {
            GameTimeSnapshot placedAt = _clock.Current;

            ShopCheckoutResult result = _checkout.TryCheckout(Lines((BudgetGpu, 1), (Banana, 1), (Mug, 1)));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Orders.Select(order => order.PlacedAt.TotalSeconds), Is.All.EqualTo(placedAt.TotalSeconds));
            Assert.That(result.Orders[0].DeliveryDueAt.TotalSeconds, Is.EqualTo(BudgetGpu.GetDeliveryDueAt(placedAt).TotalSeconds));
            Assert.That(result.Orders[1].DeliveryDueAt.TotalSeconds - placedAt.TotalSeconds, Is.EqualTo(90 * GameTimeSnapshot.SecondsPerMinute));
            Assert.That(result.Orders[2].DeliveryDueAt.TotalSeconds - placedAt.TotalSeconds, Is.EqualTo(120 * GameTimeSnapshot.SecondsPerMinute));
            Assert.That(result.Orders[0].DeliveryDueAt.TotalSeconds - placedAt.TotalSeconds, Is.EqualTo(150 * GameTimeSnapshot.SecondsPerMinute));
        }

        [Test]
        public void EvaluationChangesNothing()
        {
            Assert.That(_checkout.Evaluate(Lines((BudgetGpu, 1), (Banana, 5))), Is.EqualTo(ShopPurchaseResultCode.Success));
            Assert.That(_checkout.EvaluateQuantity(BudgetGpu, 1), Is.EqualTo(ShopPurchaseResultCode.Success));

            AssertNothingChanged(2500);
        }

        [Test]
        public void TotalIsEveryLinesPriceTimesItsQuantity()
        {
            Assert.That(ShopCheckout.TryCalculateTotal(Lines((BudgetGpu, 1), (Banana, 4), (Mug, 2)), out long total), Is.True);
            Assert.That(total, Is.EqualTo(1500 + 240 + 400));
            Assert.That(ShopCheckout.GetLineTotalCents(Banana.PriceCents, 4), Is.EqualTo(240));
            Assert.That(ShopCheckout.TryCalculateTotal(Array.Empty<ShopCheckoutLine>(), out long empty), Is.True);
            Assert.That(empty, Is.Zero);
        }

        private void AssertNothingChanged(long expectedBalance, int expectedOrders = 0)
        {
            Assert.That(_wallet.BalanceCents, Is.EqualTo(expectedBalance), "wallet unchanged");
            Assert.That(_orders.Orders, Has.Count.EqualTo(expectedOrders), "orders unchanged");
            Assert.That(_balanceChanges, Is.Zero, "no balance notification");
            Assert.That(_orderChanges, Is.Zero, "no orders notification");
        }

        private static ShopCheckoutLine[] Lines(params (ShopPurchaseOffer Offer, int Quantity)[] lines)
        {
            return lines.Select(line => new ShopCheckoutLine(line.Offer, line.Quantity)).ToArray();
        }
    }
}
