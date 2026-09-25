using System;
using System.Collections.Generic;
using System.Globalization;
using GoLive.GameTime;
using GoLive.Items;
using GoLive.Localization;
using GoLive.Shop;
using UnityEngine;

namespace GoLive.Phone
{
    // The Shop screen's coordinator: which Shop page is open and which product is selected, the requests of the pages
    // (sent to ShopBehaviour, which owns the cart, the orders and the one checkout) and the display data of the page on
    // screen. A page shows only while the phone shows the Shop screen and it is the current page, and every Shop control
    // lives inside the Shop screen, so nothing of the Shop can be seen or clicked on another screen.
    [DisallowMultipleComponent]
    public sealed class PhoneShopView : MonoBehaviour
    {
        private enum Page
        {
            Storefront,
            ProductDetails,
            Cart,
            Orders
        }

        [Header("Core")]
        [SerializeField] private PhoneBehaviour _phone;
        [SerializeField] private LocalizationContext _localization;
        [SerializeField] private GameClockBehaviour _clock;
        [SerializeField] private ShopBehaviour _shop;

        [Header("Pages")]
        [SerializeField] private ShopStorefrontView _storefront;
        [SerializeField] private ShopProductDetailsView _details;
        [SerializeField] private ShopCartView _cart;
        [SerializeField] private ShopOrdersView _orders;

        public string TitleKey =>
            _page switch
            {
                Page.ProductDetails => ShopText.DetailsTitleKey,
                Page.Cart => ShopText.CartTitleKey,
                Page.Orders => ShopText.OrdersTitleKey,
                _ => ShopText.ShopTitleKey
            };

        public event Action PageChanged;

        private readonly List<ShopProductCardData> _cards = new();
        private readonly List<ShopCartRowData> _cartRows = new();
        private readonly List<ShopOrderRowData> _orderRows = new();

        private Page _page;
        private string _selectedProductId;
        private GameClock _subscribedClock;
        private bool _bound;

        private void Awake()
        {
            if (!ValidateConfiguration())
                enabled = false;
        }

        private void OnEnable()
        {
            Bind();
            ShowCurrentPage();
        }

        private void OnDisable()
        {
            Unbind();

            // Leaving the Shop screen closes whatever Shop page was open: the next visit starts at the storefront.
            _page = Page.Storefront;
            _selectedProductId = null;
        }

        private void Bind()
        {
            if (_bound)
                return;

            _phone.ScreenChanged += ShowCurrentPage;
            _phone.ScreenBackRequested += TryBack;
            _localization.LanguageChanged += HandleLanguageChanged;
            _shop.Changed += Refresh;

            _storefront.ProductSelected += OpenProductDetails;
            _storefront.AddToCartRequested += AddToCartFromStorefront;
            _storefront.CartRequested += OpenCart;
            _storefront.OrdersRequested += OpenOrders;
            _details.AddToCartRequested += AddSelectedProductToCart;
            _cart.AddOneRequested += AddOneFromCart;
            _cart.RemoveOneRequested += RemoveOneFromCart;
            _cart.RemoveAllRequested += RemoveAllFromCart;
            _cart.CheckoutRequested += Checkout;

            _subscribedClock = _clock.Clock;

            if (_subscribedClock != null)
                _subscribedClock.MinuteChanged += HandleMinuteChanged;

            _bound = true;
        }

        private void Unbind()
        {
            if (!_bound)
                return;

            _phone.ScreenChanged -= ShowCurrentPage;
            _phone.ScreenBackRequested -= TryBack;
            _localization.LanguageChanged -= HandleLanguageChanged;
            _shop.Changed -= Refresh;

            _storefront.ProductSelected -= OpenProductDetails;
            _storefront.AddToCartRequested -= AddToCartFromStorefront;
            _storefront.CartRequested -= OpenCart;
            _storefront.OrdersRequested -= OpenOrders;
            _details.AddToCartRequested -= AddSelectedProductToCart;
            _cart.AddOneRequested -= AddOneFromCart;
            _cart.RemoveOneRequested -= RemoveOneFromCart;
            _cart.RemoveAllRequested -= RemoveAllFromCart;
            _cart.CheckoutRequested -= Checkout;

            if (_subscribedClock != null)
                _subscribedClock.MinuteChanged -= HandleMinuteChanged;

            _subscribedClock = null;
            _bound = false;
        }

        private void ShowPage(Page page)
        {
            _page = page;
            ShowCurrentPage();
            PageChanged?.Invoke();
        }

        private void ShowCurrentPage()
        {
            bool shopShown = _phone.CurrentScreen == PhoneScreenId.Shop;

            _storefront.SetVisible(shopShown && _page == Page.Storefront);
            _details.SetVisible(shopShown && _page == Page.ProductDetails);
            _cart.SetVisible(shopShown && _page == Page.Cart);
            _orders.SetVisible(shopShown && _page == Page.Orders);

            Refresh();
        }

        private bool IsUsable(Page page)
        {
            return _phone.IsInteractive &&
                   _phone.CurrentScreen == PhoneScreenId.Shop &&
                   _page == page;
        }

        private void OpenProductDetails(string productId)
        {
            if (!IsUsable(Page.Storefront) || !_shop.TryGetProduct(productId, out _))
                return;

            _selectedProductId = productId;
            ShowPage(Page.ProductDetails);
        }

        private void OpenCart()
        {
            if (IsUsable(Page.Storefront))
                ShowPage(Page.Cart);
        }

        private void OpenOrders()
        {
            if (IsUsable(Page.Storefront))
                ShowPage(Page.Orders);
        }

        private void AddToCartFromStorefront(string productId)
        {
            AddToCart(Page.Storefront, productId);
        }

        private void AddSelectedProductToCart()
        {
            AddToCart(Page.ProductDetails, _selectedProductId);
        }

        private void AddOneFromCart(string productId)
        {
            AddToCart(Page.Cart, productId);
        }

        // Only puts the product in the cart: nothing is paid and no order is placed until checkout. A refused add
        // changes nothing, and the page already shows why it is not possible.
        private void AddToCart(Page from, string productId)
        {
            if (IsUsable(from) && productId != null)
                _shop.TryAddToCart(productId);
        }

        private void RemoveOneFromCart(string productId)
        {
            if (IsUsable(Page.Cart))
                _shop.TryRemoveOneFromCart(productId);
        }

        private void RemoveAllFromCart(string productId)
        {
            if (IsUsable(Page.Cart))
                _shop.TryRemoveFromCart(productId);
        }

        private void Checkout()
        {
            if (!IsUsable(Page.Cart))
                return;

            if (_shop.TryCheckout().Succeeded)
                ShowPage(Page.Orders);
            else
                Refresh();
        }

        private bool TryBack()
        {
            if (_page == Page.Storefront)
                return false;

            _selectedProductId = null;
            ShowPage(Page.Storefront);

            return true;
        }

        private void HandleLanguageChanged(GameLanguage language)
        {
            Refresh();
        }

        private void HandleMinuteChanged(GameTimeSnapshot time)
        {
            if (_page is Page.ProductDetails or Page.Orders)
                Refresh();
        }

        // Composes the display data of the page on screen only; the other pages are composed when they open.
        private void Refresh()
        {
            if (_phone.CurrentScreen != PhoneScreenId.Shop)
                return;

            switch (_page)
            {
                case Page.Storefront:
                    RefreshStorefront();
                    break;
                case Page.ProductDetails:
                    RefreshProductDetails();
                    break;
                case Page.Cart:
                    RefreshCart();
                    break;
                case Page.Orders:
                    RefreshOrders();
                    break;
            }
        }

        private void RefreshStorefront()
        {
            IReadOnlyList<ShopProductDefinition> products = _shop.Products;

            _cards.Clear();

            for (int i = 0; i < products.Count; i++)
            {
                ShopProductDefinition product = products[i];
                ShopPurchaseResultCode addToCart = _shop.EvaluateAddToCart(product.ProductId);

                string badgeKey = ShopText.CardBadgeKey(
                    product.IsAvailable,
                    _shop.GetCartQuantity(product.ProductId),
                    _shop.Orders.TryGetLatestActiveOrder(product.ProductId, out _),
                    addToCart);

                _cards.Add(new ShopProductCardData(
                    product.ProductId,
                    product.Category,
                    product.ShowInFeatured,
                    product.DisplayImage,
                    _localization.Text(product.NameLocalizationKey),
                    CategoryName(product.Category),
                    ShopText.FormatMoney(product.PriceCents),
                    badgeKey != null ? _localization.Text(badgeKey) : null,
                    product.IsAvailable,
                    addToCart == ShopPurchaseResultCode.Success));
            }

            _storefront.Show(_cards, _shop.CartItemCount, _shop.Orders.CountActive(), _localization);
        }

        private void RefreshProductDetails()
        {
            if (!_shop.TryGetProduct(_selectedProductId, out ShopProductDefinition product))
            {
                ShowPage(Page.Storefront);
                return;
            }

            string productId = product.ProductId;
            ShopPurchaseResultCode addToCart = _shop.EvaluateAddToCart(productId);
            int inCart = _shop.GetCartQuantity(productId);
            bool hasActiveOrder = _shop.Orders.TryGetLatestActiveOrder(productId, out ShopOrder activeOrder);

            _details.Show(new ShopProductDetailsData(
                product.DisplayImage,
                _localization.Text(product.NameLocalizationKey),
                CategoryName(product.Category),
                ShopText.FormatMoney(product.PriceCents),
                _localization.Text(product.DescriptionLocalizationKey),
                GetDeliveryLine(productId, addToCart, inCart, activeOrder),
                _localization.Text(ShopText.AddToCartButtonKey(addToCart, inCart, hasActiveOrder)),
                addToCart == ShopPurchaseResultCode.Success));
        }

        private void RefreshCart()
        {
            IReadOnlyList<ShopCartLine> lines = _shop.CartLines;

            _cartRows.Clear();

            for (int i = 0; i < lines.Count; i++)
            {
                ShopCartLine line = lines[i];

                // The cart only ever holds catalog products (ShopBehaviour adds nothing else).
                if (!_shop.TryGetProduct(line.ProductId, out ShopProductDefinition product))
                    continue;

                _cartRows.Add(new ShopCartRowData(
                    line.ProductId,
                    product.DisplayImage,
                    _localization.Text(product.NameLocalizationKey),
                    CategoryName(product.Category),
                    ShopText.FormatMoney(ShopCheckout.GetLineTotalCents(product.PriceCents, line.Quantity)),
                    line.Quantity.ToString(CultureInfo.InvariantCulture),
                    _shop.EvaluateAddToCart(line.ProductId) == ShopPurchaseResultCode.Success));
            }

            ShopPurchaseResultCode checkout = _shop.EvaluateCheckout();
            string problemKey = ShopText.CheckoutProblemKey(checkout);

            _cart.Show(
                _cartRows,
                _localization.Format(ShopText.CartTotalKey, ShopText.FormatMoney(_shop.GetCartTotalCents())),
                checkout == ShopPurchaseResultCode.Success,
                problemKey != null ? _localization.Text(problemKey) : null,
                _localization);
        }

        private void RefreshOrders()
        {
            IReadOnlyList<ShopOrder> orders = _shop.Orders.Orders;

            _orderRows.Clear();

            for (int i = orders.Count - 1; i >= 0; i--)
            {
                ShopOrder order = orders[i];
                bool known = _shop.TryGetProduct(order.ProductId, out ShopProductDefinition product);

                _orderRows.Add(new ShopOrderRowData(
                    known ? product.DisplayImage : null,
                    known ? _localization.Text(product.NameLocalizationKey) : order.ProductId,
                    ShopText.FormatMoney(order.PaidPriceCents),
                    _localization.Text(ShopText.OrderStatusKey(order.Status)),
                    order.IsActive ? FormatDelivery(order.DeliveryDueAt) : null));
            }

            _orders.Show(_orderRows, _localization);
        }

        // "Ordered · delivery ~09:33" while an order is on its way, the estimate while one more could still arrive.
        private string GetDeliveryLine(string productId, ShopPurchaseResultCode addToCart, int inCart, ShopOrder activeOrder)
        {
            if (activeOrder != null)
                return _localization.Format(ShopText.OrderedWithEtaKey, FormatDelivery(activeOrder.DeliveryDueAt));

            bool canArrive = addToCart == ShopPurchaseResultCode.Success || inCart > 0;

            return canArrive && _shop.TryEstimateDelivery(productId, out GameTimeSnapshot deliveryDueAt)
                ? FormatDelivery(deliveryDueAt)
                : null;
        }

        private string FormatDelivery(GameTimeSnapshot dueAt)
        {
            string time = $"{dueAt.Hour:00}:{dueAt.Minute:00}";
            GameClock clock = _clock.Clock;

            if (clock == null || clock.Current.Day == dueAt.Day)
                return _localization.Format(ShopText.OrderEtaKey, time);

            return _localization.Format(ShopText.OrderEtaDayKey, dueAt.Day, time);
        }

        private string CategoryName(ItemCategory category)
        {
            return _localization.Text(ItemCategoryLocalization.GetKey(category));
        }

        private bool ValidateConfiguration()
        {
            if (_phone != null &&
                _localization != null &&
                _clock != null &&
                _shop != null &&
                _storefront != null &&
                _storefront.IsConfigured &&
                _details != null &&
                _details.IsConfigured &&
                _cart != null &&
                _cart.IsConfigured &&
                _orders != null &&
                _orders.IsConfigured)
            {
                return true;
            }

            Debug.LogError(
                $"{nameof(PhoneShopView)} on {name} has incomplete configuration: it needs the phone, localization, clock, Shop and four configured page views.",
                this);

            return false;
        }
    }
}
