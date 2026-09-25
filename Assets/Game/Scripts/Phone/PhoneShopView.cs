using System;
using System.Collections.Generic;
using System.Globalization;
using GoLive.GameTime;
using GoLive.Items;
using GoLive.Localization;
using GoLive.Shop;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace GoLive.Phone
{
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

        [Serializable]
        private sealed class CategoryTab
        {
            [field: SerializeField]
            public Button Button { get; private set; }

            [field: SerializeField]
            public TMP_Text Label { get; private set; }

            [field: SerializeField]
            public string LabelKey { get; private set; }

            [field: SerializeField]
            public bool ShowsFeatured { get; private set; }

            [field: SerializeField]
            public ItemCategory Category { get; private set; }

            public bool Lists(ShopProductDefinition product)
            {
                return ShowsFeatured
                    ? product.ShowInFeatured
                    : product.Category == Category;
            }
        }

        private readonly struct ProductCard
        {
            public ShopProductDefinition Product { get; }
            public ShopProductCardView View { get; }

            public ProductCard(
                ShopProductDefinition product,
                ShopProductCardView view)
            {
                Product = product;
                View = view;
            }
        }

        [Header("Core")]
        [SerializeField] private PhoneBehaviour _phone;
        [SerializeField] private LocalizationContext _localization;
        [SerializeField] private GameClockBehaviour _clock;
        [SerializeField] private ShopBehaviour _shop;

        [Header("Storefront")]
        [SerializeField] private GameObject _storefront;

        [SerializeField]
        private CategoryTab[] _tabs = Array.Empty<CategoryTab>();

        [SerializeField]
        private Color _tabColor =
            new(0.133f, 0.192f, 0.247f, 1f);

        [SerializeField]
        private Color _tabLabelColor =
            new(0.702f, 0.749f, 0.784f, 1f);

        [SerializeField]
        private Color _selectedTabColor =
            new(0.278f, 0.388f, 0.494f, 1f);

        [SerializeField]
        private Color _selectedTabLabelColor =
            new(0.933f, 0.945f, 0.933f, 1f);

        [SerializeField] private ScrollRect _catalog;
        [SerializeField] private ShopProductCardView _productCardTemplate;
        [SerializeField] private TMP_Text _catalogEmpty;

        [SerializeField] private Button _openCart;
        [SerializeField] private TMP_Text _openCartLabel;
        [SerializeField] private GameObject _cartBadge;
        [SerializeField] private TMP_Text _cartCount;

        [SerializeField] private Button _openOrders;
        [SerializeField] private TMP_Text _openOrdersLabel;
        [SerializeField] private GameObject _activeOrdersBadge;
        [SerializeField] private TMP_Text _activeOrdersCount;

        [Header("Product Details")]
        [SerializeField] private GameObject _productDetails;
        [SerializeField] private GameObject _detailsMedia;
        [SerializeField] private Image _detailsImage;
        [SerializeField] private TMP_Text _detailsCategory;
        [SerializeField] private TMP_Text _detailsName;
        [SerializeField] private TMP_Text _detailsPrice;
        [SerializeField] private TMP_Text _detailsDelivery;
        [SerializeField] private TMP_Text _detailsDescription;

        [FormerlySerializedAs("_detailsBuy")]
        [SerializeField] private Button _detailsAddToCart;

        [FormerlySerializedAs("_detailsBuyLabel")]
        [SerializeField] private TMP_Text _detailsAddToCartLabel;

        [SerializeField]
        private Color _detailsAddToCartLabelColor =
            new(0.082f, 0.125f, 0.165f, 1f);

        [SerializeField]
        private Color _detailsAddToCartLabelDisabledColor =
            new(0.557f, 0.616f, 0.667f, 1f);

        [Header("Cart")]
        [SerializeField] private ShopCartView _cartView;

        [Header("Orders")]
        [SerializeField] private GameObject _orders;
        [SerializeField] private ScrollRect _orderList;
        [SerializeField] private ShopOrderRowView _orderRowTemplate;
        [SerializeField] private TMP_Text _ordersEmpty;

        public string TitleKey =>
            _page switch
            {
                Page.ProductDetails => "phone.product_details",
                Page.Cart => "phone.cart",
                Page.Orders => "phone.orders",
                _ => "phone.shop"
            };

        public event Action PageChanged;

        private readonly List<ProductCard> _productCards = new();
        private readonly List<ShopOrderRowView> _orderRows = new();
        private readonly List<ShopCartLineViewData> _cartLines = new();

        private UnityAction[] _tabClicks =
            Array.Empty<UnityAction>();

        private Page _page;
        private int _selectedTab;
        private ShopProductDefinition _selectedProduct;
        private GameClock _subscribedClock;
        private bool _bound;

        private void Awake()
        {
            if (!ValidateConfiguration())
            {
                enabled = false;
                return;
            }

            _productCardTemplate.gameObject.SetActive(false);
            _orderRowTemplate.gameObject.SetActive(false);

            _tabClicks =
                new UnityAction[_tabs.Length];

            for (int i = 0; i < _tabs.Length; i++)
            {
                int index = i;
                _tabClicks[i] = () => SelectTab(index);
            }

            BuildCatalog();
        }

        private void OnEnable()
        {
            Bind();
        }

        private void OnDisable()
        {
            SetShortcutVisibility(false);
            Unbind();

            _page = Page.Storefront;
            _selectedProduct = null;

            if (_cartView != null)
                _cartView.gameObject.SetActive(false);
        }

        private void Bind()
        {
            if (_bound)
                return;

            _phone.ScreenBackRequested += TryBack;
            _phone.ScreenChanged += HandlePhoneScreenChanged;
            _localization.LanguageChanged += HandleLanguageChanged;
            _shop.Changed += Refresh;

            _openCart.onClick.AddListener(OpenCart);
            _openOrders.onClick.AddListener(OpenOrders);
            _detailsAddToCart.onClick.AddListener(AddSelectedProductToCart);

            _cartView.AddOneRequested += AddOneToCart;
            _cartView.RemoveOneRequested += RemoveOneFromCart;
            _cartView.RemoveAllRequested += RemoveFromCart;
            _cartView.CheckoutRequested += CheckoutCart;

            for (int i = 0; i < _tabs.Length; i++)
            {
                _tabs[i]
                    .Button
                    .onClick
                    .AddListener(_tabClicks[i]);
            }

            _subscribedClock = _clock.Clock;

            if (_subscribedClock != null)
                _subscribedClock.MinuteChanged += HandleMinuteChanged;

            _bound = true;

            ShowPage(Page.Storefront);
        }

        private void Unbind()
        {
            if (!_bound)
                return;

            _phone.ScreenBackRequested -= TryBack;
            _phone.ScreenChanged -= HandlePhoneScreenChanged;
            _localization.LanguageChanged -= HandleLanguageChanged;
            _shop.Changed -= Refresh;

            _openCart.onClick.RemoveListener(OpenCart);
            _openOrders.onClick.RemoveListener(OpenOrders);
            _detailsAddToCart.onClick.RemoveListener(AddSelectedProductToCart);

            _cartView.AddOneRequested -= AddOneToCart;
            _cartView.RemoveOneRequested -= RemoveOneFromCart;
            _cartView.RemoveAllRequested -= RemoveFromCart;
            _cartView.CheckoutRequested -= CheckoutCart;

            for (int i = 0; i < _tabs.Length; i++)
            {
                _tabs[i]
                    .Button
                    .onClick
                    .RemoveListener(_tabClicks[i]);
            }

            if (_subscribedClock != null)
                _subscribedClock.MinuteChanged -= HandleMinuteChanged;

            _subscribedClock = null;
            _bound = false;
        }

        // ==========================================================
        // CATALOG BUILD
        // ==========================================================

        private void BuildCatalog()
        {
            IReadOnlyList<ShopProductDefinition> products =
                _shop.Products;

            Transform content =
                _catalog.content;

            for (int i = 0; i < products.Count; i++)
            {
                ShopProductDefinition product =
                    products[i];

                ShopProductCardView card =
                    Instantiate(
                        _productCardTemplate,
                        content);

                card.name = product.ProductId;

                card.Clicked +=
                    () => OpenProductDetails(product);

                card.AddToCartClicked +=
                    () => AddProductToCart(product);

                card.gameObject.SetActive(true);

                _productCards.Add(
                    new ProductCard(
                        product,
                        card));
            }
        }

        // ==========================================================
        // NAVIGATION
        // ==========================================================

        private void ShowPage(Page page)
        {
            _page = page;

            _storefront.SetActive(
                page == Page.Storefront);

            _productDetails.SetActive(
                page == Page.ProductDetails);

            _cartView.gameObject.SetActive(
                page == Page.Cart);

            _orders.SetActive(
                page == Page.Orders);

            Refresh();
            RefreshShortcuts();

            PageChanged?.Invoke();
        }

        private void SelectTab(int index)
        {
            if (!_phone.IsInteractive ||
                _page != Page.Storefront)
            {
                return;
            }

            if (index < 0 || index >= _tabs.Length)
                return;

            _selectedTab = index;

            RefreshCatalog();
            ScrollToTop(_catalog);
        }

        private void OpenProductDetails(
            ShopProductDefinition product)
        {
            if (!_phone.IsInteractive ||
                _page != Page.Storefront ||
                product == null)
            {
                return;
            }

            _selectedProduct = product;
            ShowPage(Page.ProductDetails);
        }

        private void OpenCart()
        {
            if (!_phone.IsInteractive ||
                _page != Page.Storefront)
            {
                return;
            }

            ShowPage(Page.Cart);
            _cartView.ScrollToTop();
        }

        private void OpenOrders()
        {
            if (!_phone.IsInteractive ||
                _page != Page.Storefront)
            {
                return;
            }

            ShowPage(Page.Orders);
            ScrollToTop(_orderList);
        }

        private bool TryBack()
        {
            if (_page == Page.Storefront)
                return false;

            _selectedProduct = null;
            ShowPage(Page.Storefront);

            return true;
        }

        // ==========================================================
        // CART COMMANDS
        // ==========================================================

        private void AddProductToCart(
            ShopProductDefinition product)
        {
            if (!_phone.IsInteractive ||
                product == null)
            {
                return;
            }

            _shop.TryAddToCart(product.ProductId);
        }

        private void AddSelectedProductToCart()
        {
            if (!_phone.IsInteractive ||
                _page != Page.ProductDetails ||
                _selectedProduct == null)
            {
                return;
            }

            _shop.TryAddToCart(
                _selectedProduct.ProductId);
        }

        private void AddOneToCart(string productId)
        {
            if (!_phone.IsInteractive ||
                _page != Page.Cart)
            {
                return;
            }

            _shop.TryAddToCart(productId);
        }

        private void RemoveOneFromCart(string productId)
        {
            if (!_phone.IsInteractive ||
                _page != Page.Cart)
            {
                return;
            }

            _shop.TryRemoveOneFromCart(productId);
        }

        private void RemoveFromCart(string productId)
        {
            if (!_phone.IsInteractive ||
                _page != Page.Cart)
            {
                return;
            }

            _shop.TryRemoveFromCart(productId);
        }

        private void CheckoutCart()
        {
            if (!_phone.IsInteractive ||
                _page != Page.Cart)
            {
                return;
            }

            ShopCheckoutResult result =
                _shop.TryCheckoutCart();

            if (!result.Succeeded)
            {
                RefreshCart();
                return;
            }

            ShowPage(Page.Orders);
            ScrollToTop(_orderList);
        }

        // ==========================================================
        // EVENTS
        // ==========================================================

        private void HandlePhoneScreenChanged()
        {
            if (_phone.CurrentScreen != PhoneScreenId.Shop)
            {
                SetShortcutVisibility(false);
                return;
            }

            RefreshShortcuts();
        }

        private void HandleLanguageChanged(
            GameLanguage language)
        {
            Refresh();
        }

        private void HandleMinuteChanged(
            GameTimeSnapshot time)
        {
            if (_page == Page.ProductDetails)
                RefreshProductDetails();
            else if (_page == Page.Orders)
                RefreshOrders();
        }

        private void Refresh()
        {
            RefreshCatalog();
            RefreshProductDetails();
            RefreshCart();
            RefreshOrders();
            RefreshShortcuts();
        }

        // ==========================================================
        // STOREFRONT
        // ==========================================================

        private void RefreshCatalog()
        {
            if (_selectedTab < 0 ||
                _selectedTab >= _tabs.Length)
            {
                _selectedTab = 0;
            }

            for (int i = 0; i < _tabs.Length; i++)
            {
                CategoryTab tab =
                    _tabs[i];

                bool selected =
                    i == _selectedTab;

                tab.Label.text =
                    _localization.Text(
                        tab.LabelKey);

                tab.Label.color =
                    selected
                        ? _selectedTabLabelColor
                        : _tabLabelColor;

                tab.Button.image.color =
                    selected
                        ? _selectedTabColor
                        : _tabColor;
            }

            CategoryTab shelf =
                _tabs[_selectedTab];

            int listedCount = 0;

            for (int i = 0; i < _productCards.Count; i++)
            {
                ShopProductDefinition product =
                    _productCards[i].Product;

                ShopProductCardView view =
                    _productCards[i].View;

                bool listed =
                    shelf.Lists(product);

                if (view.gameObject.activeSelf != listed)
                    view.gameObject.SetActive(listed);

                if (!listed)
                    continue;

                listedCount++;

                string productName =
                    _localization.Text(
                        product.NameLocalizationKey);

                string category =
                    _localization.Text(
                        GetBroadCategoryLocalizationKey(
                            product.Category));

                Sprite image =
                    ResolveProductImage(product);

                if (!product.IsAvailable)
                {
                    view.ShowUnavailable(
                        image,
                        productName,
                        category,
                        _localization.Text(
                            "phone.unavailable"));

                    continue;
                }

                bool canAddToCart =
                    _shop.EvaluateAddToCart(
                        product.ProductId) ==
                    ShopCartAddResultCode.Success;

                view.ShowAvailable(
                    image,
                    productName,
                    category,
                    FormatMoney(product.PriceCents),
                    GetCardBadge(product),
                    canAddToCart);
            }

            _catalogEmpty.text =
                _localization.Text(
                    "phone.catalog_empty");

            _catalogEmpty.gameObject.SetActive(
                listedCount == 0);
        }

        // ==========================================================
        // PRODUCT DETAILS
        // ==========================================================

        private void RefreshProductDetails()
        {
            if (_selectedProduct == null)
                return;

            ShopProductDefinition product =
                _selectedProduct;

            Sprite image =
                ResolveProductImage(product);

            _detailsImage.sprite = image;
            _detailsMedia.SetActive(image != null);

            _detailsCategory.text =
                _localization.Text(
                    GetBroadCategoryLocalizationKey(
                        product.Category));

            _detailsName.text =
                _localization.Text(
                    product.NameLocalizationKey);

            _detailsPrice.text =
                FormatMoney(product.PriceCents);

            _detailsDescription.text =
                _localization.Text(
                    product.DescriptionLocalizationKey);

            bool hasActiveOrder =
                _shop.Orders.TryGetLatestActiveOrder(
                    product.ProductId,
                    out ShopOrder activeOrder);

            string delivery =
                GetDetailsDelivery(
                    product,
                    activeOrder);

            _detailsDelivery.text =
                delivery ?? string.Empty;

            _detailsDelivery.gameObject.SetActive(
                delivery != null);

            ShopCartAddResultCode addState =
                _shop.EvaluateAddToCart(
                    product.ProductId);

            bool canAdd =
                addState ==
                ShopCartAddResultCode.Success;

            _detailsAddToCartLabel.text =
                _localization.Text(
                    GetAddToCartButtonKey(
                        product,
                        addState,
                        hasActiveOrder));

            _detailsAddToCartLabel.color =
                canAdd
                    ? _detailsAddToCartLabelColor
                    : _detailsAddToCartLabelDisabledColor;

            _detailsAddToCart.interactable =
                canAdd;
        }

        // ==========================================================
        // CART
        // ==========================================================

        private void RefreshCart()
        {
            _cartLines.Clear();

            IReadOnlyList<ShopCartEntry> entries =
                _shop.Cart.Entries;

            for (int i = 0; i < entries.Count; i++)
            {
                ShopCartEntry entry = entries[i];

                if (!_shop.TryGetProduct(
                        entry.ProductId,
                        out ShopProductDefinition product))
                {
                    continue;
                }

                long linePrice =
                    checked(
                        product.PriceCents *
                        entry.Quantity);

                _cartLines.Add(
                    new ShopCartLineViewData(
                        product.ProductId,
                        ResolveProductImage(product),
                        _localization.Text(
                            product.NameLocalizationKey),
                        _localization.Text(
                            GetBroadCategoryLocalizationKey(
                                product.Category)),
                        FormatMoney(linePrice),
                        entry.Quantity,
                        _shop.EvaluateAddToCart(
                            product.ProductId) ==
                        ShopCartAddResultCode.Success));
            }

            bool hasTotal =
                _shop.TryGetCartTotal(
                    out long totalCents);

            ShopCheckoutResultCode checkoutState =
                _shop.EvaluateCartCheckout();

            string checkoutKey =
                GetCheckoutButtonKey(
                    checkoutState);

            string statusText =
                GetCheckoutStatusText(
                    checkoutState);

            _cartView.Render(
                _cartLines,
                _localization.Text(
                    "phone.cart_empty"),
                _localization.Format(
                    "phone.cart_total",
                    FormatMoney(
                        hasTotal
                            ? totalCents
                            : 0)),
                _localization.Text(
                    checkoutKey),
                statusText,
                checkoutState ==
                    ShopCheckoutResultCode.Success);
        }

        // ==========================================================
        // ORDERS
        // ==========================================================

        private void RefreshOrders()
        {
            IReadOnlyList<ShopOrder> orders =
                _shop.Orders.Orders;

            for (int i = 0; i < orders.Count; i++)
            {
                ShopOrder order =
                    orders[orders.Count - 1 - i];

                _shop.TryGetProduct(
                    order.ProductId,
                    out ShopProductDefinition product);

                ShopOrderRowView row =
                    GetOrderRow(i);

                Sprite image =
                    product != null
                        ? ResolveProductImage(product)
                        : null;

                row.Show(
                    image,
                    product != null
                        ? _localization.Text(
                            product.NameLocalizationKey)
                        : order.ProductId,
                    FormatMoney(
                        order.PaidPriceCents),
                    _localization.Text(
                        GetOrderStatusKey(
                            order.Status)),
                    order.IsActive
                        ? FormatDelivery(
                            order.DeliveryDueAt)
                        : null);

                row.gameObject.SetActive(true);
            }

            for (int i = orders.Count;
                 i < _orderRows.Count;
                 i++)
            {
                _orderRows[i]
                    .gameObject
                    .SetActive(false);
            }

            _ordersEmpty.text =
                _localization.Text(
                    "phone.orders_empty");

            _ordersEmpty.gameObject.SetActive(
                orders.Count == 0);
        }

        private ShopOrderRowView GetOrderRow(int index)
        {
            while (_orderRows.Count <= index)
            {
                _orderRows.Add(
                    Instantiate(
                        _orderRowTemplate,
                        _orderList.content));
            }

            return _orderRows[index];
        }

        // ==========================================================
        // SHORTCUTS
        // ==========================================================

        private void RefreshShortcuts()
        {
            bool visible =
                _phone.IsOpen &&
                _phone.CurrentScreen ==
                    PhoneScreenId.Shop &&
                _page ==
                    Page.Storefront;

            SetShortcutVisibility(visible);

            _openCartLabel.text =
                _localization.Text(
                    "phone.cart");

            int cartCount =
                _shop.Cart.TotalQuantity;

            _cartBadge.SetActive(
                cartCount > 0);

            _cartCount.text =
                FormatCount(cartCount);

            _openOrdersLabel.text =
                _localization.Text(
                    "phone.orders");

            int activeOrders =
                _shop.Orders.CountActive();

            _activeOrdersBadge.SetActive(
                activeOrders > 0);

            _activeOrdersCount.text =
                FormatCount(activeOrders);
        }

        private void SetShortcutVisibility(bool visible)
        {
            if (_openCart != null)
                _openCart.gameObject.SetActive(visible);

            if (_openOrders != null)
                _openOrders.gameObject.SetActive(visible);
        }

        // ==========================================================
        // PRESENTATION HELPERS
        // ==========================================================

        private static Sprite ResolveProductImage(
            ShopProductDefinition product)
        {
            if (product == null)
                return null;

            if (product.Image != null)
                return product.Image;

            return product.FulfillmentItem != null
                ? product.FulfillmentItem.InventoryIcon
                : null;
        }

        private static string GetBroadCategoryLocalizationKey(
            ItemCategory category)
        {
            return category switch
            {
                ItemCategory.Food =>
                    "shop.tab.food",

                ItemCategory.Electronics =>
                    "shop.tab.electronics",

                ItemCategory.Household =>
                    "shop.tab.household",

                _ =>
                    throw new ArgumentOutOfRangeException(
                        nameof(category),
                        category,
                        "Unsupported Shop item category.")
            };
        }

        private string GetCardBadge(
            ShopProductDefinition product)
        {
            if (_shop.Orders.TryGetLatestActiveOrder(
                    product.ProductId,
                    out _))
            {
                return _localization.Text(
                    "phone.ordered");
            }

            if (product.MaxPurchases > 0 &&
                _shop.Orders.CountForProduct(
                    product.ProductId) >=
                product.MaxPurchases)
            {
                return _localization.Text(
                    "phone.purchased");
            }

            return null;
        }

        private string GetDetailsDelivery(
            ShopProductDefinition product,
            ShopOrder activeOrder)
        {
            if (activeOrder != null)
            {
                return _localization.Format(
                    "phone.ordered_with_eta",
                    FormatDelivery(
                        activeOrder.DeliveryDueAt));
            }

            return _shop.TryEstimateDelivery(
                    product.ProductId,
                    out GameTimeSnapshot deliveryDueAt)
                ? FormatDelivery(deliveryDueAt)
                : null;
        }

        private string FormatDelivery(
            GameTimeSnapshot dueAt)
        {
            string time =
                dueAt.Hour.ToString(
                    "00",
                    CultureInfo.InvariantCulture) +
                ":" +
                dueAt.Minute.ToString(
                    "00",
                    CultureInfo.InvariantCulture);

            GameClock clock =
                _clock.Clock;

            if (clock == null ||
                clock.Current.Day == dueAt.Day)
            {
                return _localization.Format(
                    "phone.order_eta",
                    time);
            }

            return _localization.Format(
                "phone.order_eta_day",
                dueAt.Day,
                time);
        }

        private string GetAddToCartButtonKey(
            ShopProductDefinition product,
            ShopCartAddResultCode state,
            bool hasActiveOrder)
        {
            if (hasActiveOrder)
                return "phone.ordered";

            if (state ==
                ShopCartAddResultCode.PurchaseLimitReached)
            {
                return "phone.purchased";
            }

            return state ==
                   ShopCartAddResultCode.Success
                ? "phone.add_to_cart"
                : "phone.unavailable";
        }

        private string GetCheckoutStatusText(
            ShopCheckoutResultCode state)
        {
            return state switch
            {
                ShopCheckoutResultCode.InsufficientFunds =>
                    _localization.Text(
                        "phone.not_enough_money"),

                ShopCheckoutResultCode.PurchaseLimitReached =>
                    _localization.Text(
                        "phone.purchased"),

                ShopCheckoutResultCode.ProductNotFound or
                ShopCheckoutResultCode.Unavailable or
                ShopCheckoutResultCode.InvalidRequest or
                ShopCheckoutResultCode.NotReady =>
                    _localization.Text(
                        "phone.unavailable"),

                _ =>
                    string.Empty
            };
        }

        private static string GetCheckoutButtonKey(
            ShopCheckoutResultCode state)
        {
            return state switch
            {
                ShopCheckoutResultCode.InsufficientFunds =>
                    "phone.not_enough_money",

                ShopCheckoutResultCode.PurchaseLimitReached =>
                    "phone.purchased",

                ShopCheckoutResultCode.ProductNotFound or
                ShopCheckoutResultCode.Unavailable or
                ShopCheckoutResultCode.InvalidRequest or
                ShopCheckoutResultCode.NotReady =>
                    "phone.unavailable",

                _ =>
                    "phone.checkout"
            };
        }

        private static string GetOrderStatusKey(
            ShopOrderStatus status)
        {
            return status switch
            {
                ShopOrderStatus.Delivered =>
                    "phone.order_delivered",

                _ =>
                    "phone.order_placed"
            };
        }

        private static void ScrollToTop(
            ScrollRect scrollRect)
        {
            if (scrollRect == null ||
                scrollRect.content == null)
            {
                return;
            }

            scrollRect.StopMovement();

            RectTransform content =
                scrollRect.content;

            content.anchoredPosition =
                new Vector2(
                    content.anchoredPosition.x,
                    0f);
        }

        private static string FormatMoney(long cents)
        {
            decimal dollars =
                cents / 100m;

            string amount =
                dollars.ToString(
                    "0.00",
                    CultureInfo.InvariantCulture);

            return "$" + amount;
        }

        private static string FormatCount(int count)
        {
            return count > 99
                ? "99+"
                : count.ToString(
                    CultureInfo.InvariantCulture);
        }

        // ==========================================================
        // VALIDATION
        // ==========================================================

        private bool ValidateConfiguration()
        {
            if (_phone == null ||
                _localization == null ||
                _clock == null ||
                _shop == null ||
                _storefront == null ||
                _tabs == null ||
                _tabs.Length == 0 ||
                _catalog == null ||
                _catalog.content == null ||
                _productCardTemplate == null ||
                _catalogEmpty == null ||
                _openCart == null ||
                _openCartLabel == null ||
                _cartBadge == null ||
                _cartCount == null ||
                _openOrders == null ||
                _openOrdersLabel == null ||
                _activeOrdersBadge == null ||
                _activeOrdersCount == null ||
                _productDetails == null ||
                _detailsMedia == null ||
                _detailsImage == null ||
                _detailsCategory == null ||
                _detailsName == null ||
                _detailsPrice == null ||
                _detailsDelivery == null ||
                _detailsDescription == null ||
                _detailsAddToCart == null ||
                _detailsAddToCartLabel == null ||
                _cartView == null ||
                !_cartView.IsConfigured ||
                _orders == null ||
                _orderList == null ||
                _orderList.content == null ||
                _orderRowTemplate == null ||
                _ordersEmpty == null)
            {
                Debug.LogError(
                    $"{nameof(PhoneShopView)} on {name} has incomplete configuration.",
                    this);

                return false;
            }

            for (int i = 0; i < _tabs.Length; i++)
            {
                CategoryTab tab =
                    _tabs[i];

                if (tab != null &&
                    tab.Button != null &&
                    tab.Button.image != null &&
                    tab.Label != null &&
                    !string.IsNullOrWhiteSpace(
                        tab.LabelKey))
                {
                    continue;
                }

                Debug.LogError(
                    $"{nameof(PhoneShopView)} on {name} contains an incomplete category tab at index {i}.",
                    this);

                return false;
            }

            if (!_productCardTemplate.IsConfigured ||
                !_orderRowTemplate.IsConfigured)
            {
                Debug.LogError(
                    $"{nameof(PhoneShopView)} on {name} references an incomplete product card or order row template.",
                    this);

                return false;
            }

            return true;
        }
    }
}
