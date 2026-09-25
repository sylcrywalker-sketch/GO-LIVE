using System.Globalization;
using GoLive.Shop;

namespace GoLive.Phone
{
    // Which words the Phone Shop uses for a state of the Shop: plain C# without state, so the rules are testable without
    // Unity. PhoneShopView resolves the keys; the page views only draw the result.
    public static class ShopText
    {
        public const string ShopTitleKey = "phone.shop";
        public const string DetailsTitleKey = "phone.product_details";
        public const string CartTitleKey = "phone.cart";
        public const string OrdersTitleKey = "phone.orders";
        public const string FeaturedTabKey = "shop.tab.featured";
        public const string CatalogEmptyKey = "phone.catalog_empty";
        public const string CartEmptyKey = "phone.cart_empty";
        public const string OrdersEmptyKey = "phone.orders_empty";
        public const string AddToCartKey = "phone.add_to_cart";
        public const string InCartKey = "phone.in_cart";
        public const string OrderedKey = "phone.ordered";
        public const string PurchasedKey = "phone.purchased";
        public const string UnavailableKey = "phone.unavailable";
        public const string CheckoutKey = "phone.checkout";
        public const string CartTotalKey = "phone.cart_total";
        public const string NotEnoughMoneyKey = "phone.not_enough_money";
        public const string CheckoutUnavailableKey = "phone.checkout_unavailable";
        public const string CheckoutLimitKey = "phone.checkout_limit";
        public const string CheckoutFailedKey = "phone.checkout_failed";
        public const string OrderPlacedKey = "phone.order_placed";
        public const string OrderDeliveredKey = "phone.order_delivered";
        public const string OrderEtaKey = "phone.order_eta";
        public const string OrderEtaDayKey = "phone.order_eta_day";
        public const string OrderedWithEtaKey = "phone.ordered_with_eta";

        public static string FormatMoney(long cents)
        {
            decimal dollars = cents / 100m;
            return $"${dollars.ToString("0.00", CultureInfo.InvariantCulture)}";
        }

        public static string FormatBadgeCount(int count)
        {
            return count > 99 ? "99+" : count.ToString(CultureInfo.InvariantCulture);
        }

        // The small status beside a storefront price, or null for none.
        public static string CardBadgeKey(bool isAvailable, int cartQuantity, bool hasActiveOrder, ShopPurchaseResultCode addToCart)
        {
            if (!isAvailable)
                return UnavailableKey;

            if (cartQuantity > 0)
                return InCartKey;

            if (hasActiveOrder)
                return OrderedKey;

            return addToCart == ShopPurchaseResultCode.PurchaseLimitReached ? PurchasedKey : null;
        }

        // The product page's button: "add to cart" when one more unit can go in, otherwise why not.
        public static string AddToCartButtonKey(ShopPurchaseResultCode addToCart, int cartQuantity, bool hasActiveOrder)
        {
            return addToCart switch
            {
                ShopPurchaseResultCode.Success => AddToCartKey,
                ShopPurchaseResultCode.PurchaseLimitReached when cartQuantity > 0 => InCartKey,
                ShopPurchaseResultCode.PurchaseLimitReached when hasActiveOrder => OrderedKey,
                ShopPurchaseResultCode.PurchaseLimitReached => PurchasedKey,
                _ => UnavailableKey
            };
        }

        // Why the cart cannot be ordered, or null when it can (or is simply empty).
        public static string CheckoutProblemKey(ShopPurchaseResultCode checkout)
        {
            return checkout switch
            {
                ShopPurchaseResultCode.Success => null,
                ShopPurchaseResultCode.EmptyRequest => null,
                ShopPurchaseResultCode.InsufficientFunds => NotEnoughMoneyKey,
                ShopPurchaseResultCode.Unavailable => CheckoutUnavailableKey,
                ShopPurchaseResultCode.ProductNotFound => CheckoutUnavailableKey,
                ShopPurchaseResultCode.PurchaseLimitReached => CheckoutLimitKey,
                _ => CheckoutFailedKey
            };
        }

        public static string OrderStatusKey(ShopOrderStatus status)
        {
            return status == ShopOrderStatus.Delivered ? OrderDeliveredKey : OrderPlacedKey;
        }
    }
}
