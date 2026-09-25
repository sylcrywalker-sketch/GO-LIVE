using GoLive.Phone;
using GoLive.Shop;
using NUnit.Framework;

namespace GoLive.Tests
{
    // Which words the Phone Shop uses for each state, without Unity.
    public sealed class ShopTextTests
    {
        [TestCase(1500, "$15.00")]
        [TestCase(5, "$0.05")]
        [TestCase(123456, "$1234.56")]
        [TestCase(0, "$0.00")]
        public void MoneyIsDollarsWithCents(long cents, string expected)
        {
            Assert.That(ShopText.FormatMoney(cents), Is.EqualTo(expected));
        }

        [TestCase(1, "1")]
        [TestCase(99, "99")]
        [TestCase(100, "99+")]
        public void BadgeCountsStopAt99(int count, string expected)
        {
            Assert.That(ShopText.FormatBadgeCount(count), Is.EqualTo(expected));
        }

        [Test]
        public void CardBadgeSaysWhatMattersMost()
        {
            Assert.That(ShopText.CardBadgeKey(false, 1, true, ShopPurchaseResultCode.Unavailable), Is.EqualTo(ShopText.UnavailableKey));
            Assert.That(ShopText.CardBadgeKey(true, 1, true, ShopPurchaseResultCode.PurchaseLimitReached), Is.EqualTo(ShopText.InCartKey));
            Assert.That(ShopText.CardBadgeKey(true, 0, true, ShopPurchaseResultCode.PurchaseLimitReached), Is.EqualTo(ShopText.OrderedKey));
            Assert.That(ShopText.CardBadgeKey(true, 0, false, ShopPurchaseResultCode.PurchaseLimitReached), Is.EqualTo(ShopText.PurchasedKey));
            Assert.That(ShopText.CardBadgeKey(true, 0, false, ShopPurchaseResultCode.Success), Is.Null);
        }

        [Test]
        public void ProductButtonAddsToTheCartOrSaysWhyNot()
        {
            Assert.That(ShopText.AddToCartButtonKey(ShopPurchaseResultCode.Success, 3, true), Is.EqualTo(ShopText.AddToCartKey));
            Assert.That(ShopText.AddToCartButtonKey(ShopPurchaseResultCode.PurchaseLimitReached, 1, true), Is.EqualTo(ShopText.InCartKey));
            Assert.That(ShopText.AddToCartButtonKey(ShopPurchaseResultCode.PurchaseLimitReached, 0, true), Is.EqualTo(ShopText.OrderedKey));
            Assert.That(ShopText.AddToCartButtonKey(ShopPurchaseResultCode.PurchaseLimitReached, 0, false), Is.EqualTo(ShopText.PurchasedKey));
            Assert.That(ShopText.AddToCartButtonKey(ShopPurchaseResultCode.Unavailable, 0, false), Is.EqualTo(ShopText.UnavailableKey));
            Assert.That(ShopText.AddToCartButtonKey(ShopPurchaseResultCode.NotReady, 0, false), Is.EqualTo(ShopText.UnavailableKey));
        }

        [TestCase(ShopPurchaseResultCode.Success, null)]
        [TestCase(ShopPurchaseResultCode.EmptyRequest, null)]
        [TestCase(ShopPurchaseResultCode.InsufficientFunds, ShopText.NotEnoughMoneyKey)]
        [TestCase(ShopPurchaseResultCode.Unavailable, ShopText.CheckoutUnavailableKey)]
        [TestCase(ShopPurchaseResultCode.ProductNotFound, ShopText.CheckoutUnavailableKey)]
        [TestCase(ShopPurchaseResultCode.PurchaseLimitReached, ShopText.CheckoutLimitKey)]
        [TestCase(ShopPurchaseResultCode.InvalidRequest, ShopText.CheckoutFailedKey)]
        [TestCase(ShopPurchaseResultCode.Busy, ShopText.CheckoutFailedKey)]
        [TestCase(ShopPurchaseResultCode.NotReady, ShopText.CheckoutFailedKey)]
        public void CheckoutProblemExplainsEveryRefusal(ShopPurchaseResultCode code, string expectedKey)
        {
            Assert.That(ShopText.CheckoutProblemKey(code), Is.EqualTo(expectedKey));
        }

        [Test]
        public void OrderStatusFollowsTheOrder()
        {
            Assert.That(ShopText.OrderStatusKey(ShopOrderStatus.Placed), Is.EqualTo(ShopText.OrderPlacedKey));
            Assert.That(ShopText.OrderStatusKey(ShopOrderStatus.Delivered), Is.EqualTo(ShopText.OrderDeliveredKey));
        }
    }
}
