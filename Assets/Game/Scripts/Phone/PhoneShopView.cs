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
            Orders
        }

        [Serializable]
        private sealed class CategoryTab
        {
            [field: SerializeField] public Button Button { get; private set; }
            [field: SerializeField] public TMP_Text Label { get; private set; }
            [field: SerializeField] public string LabelKey { get; private set; }
            [field: SerializeField] public bool ShowsFeatured { get; private set; }
            [field: SerializeField] public ItemCategory Category { get; private set; }

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

            public ProductCard(ShopProductDefinition product, ShopProductCardView view)
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
        [SerializeField] private CategoryTab[] _tabs = Array.Empty<CategoryTab>();
        [SerializeField] private Color _tabColor = new(0.133f, 0.192f, 0.247f, 1f);
        [SerializeField] private Color _tabLabelColor = new(0.702f, 0.749f, 0.784f, 1f);
        [SerializeField] private Color _selectedTabColor = new(0.278f, 0.388f, 0.494f, 1f);
        [SerializeField] private Color _selectedTabLabelColor = new(0.933f, 0.945f, 0.933f, 1f);
        [SerializeField] private ScrollRect _catalog;
        [SerializeField] private ShopProductCardView _productCardTemplate;
        [SerializeField] private TMP_Text _catalogEmpty;
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
        [SerializeField] private Button _detailsBuy;
        [SerializeField] private TMP_Text _detailsBuyLabel;
        [SerializeField] private Color _detailsBuyLabelColor = new(0.082f, 0.125f, 0.165f, 1f);
        [SerializeField] private Color _detailsBuyLabelDisabledColor = new(0.557f, 0.616f, 0.667f, 1f);

        [Header("Orders")]
        [SerializeField] private GameObject _orders;
        [SerializeField] private ScrollRect _orderList;
        [SerializeField] private ShopOrderRowView _orderRowTemplate;
        [SerializeField] private TMP_Text _ordersEmpty;

        public string TitleKey =>
            _page switch
            {
                Page.ProductDetails => "phone.product_details",
                Page.Orders => "phone.orders",
                _ => "phone.shop"
            };

        public event Action PageChanged;

        private readonly List<ProductCard> _productCards = new();
        private readonly List<ShopOrderRowView> _orderRows = new();

        private UnityAction[] _tabClicks = Array.Empty<UnityAction>();
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

            _tabClicks = new UnityAction[_tabs.Length];

            for (int i = 0; i < _tabs.Length; i++)
            {
                int index = i;
                _tabClicks[i] = () => SelectTab(index);
            }

            // The Orders shortcut sits in the shared bottom bar, outside this screen, so it follows the phone screen instead of OnEnable/OnDisable.
            _phone.ScreenChanged += RefreshOrdersShortcut;

            BuildCatalog();
        }

        private void OnEnable()
        {
            Bind();
        }

        private void OnDisable()
        {
            Unbind();

            _page = Page.Storefront;
            _selectedProduct = null;
        }

        private void OnDestroy()
        {
            if (_phone != null)
                _phone.ScreenChanged -= RefreshOrdersShortcut;
        }

        private void Bind()
        {
            if (_bound)
                return;

            _phone.ScreenBackRequested += TryBack;
            _localization.LanguageChanged += HandleLanguageChanged;
            _shop.Changed += Refresh;

            _openOrders.onClick.AddListener(OpenOrders);
            _detailsBuy.onClick.AddListener(BuySelectedProduct);

            for (int i = 0; i < _tabs.Length; i++)
                _tabs[i].Button.onClick.AddListener(_tabClicks[i]);

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
            _localization.LanguageChanged -= HandleLanguageChanged;
            _shop.Changed -= Refresh;

            _openOrders.onClick.RemoveListener(OpenOrders);
            _detailsBuy.onClick.RemoveListener(BuySelectedProduct);

            for (int i = 0; i < _tabs.Length; i++)
                _tabs[i].Button.onClick.RemoveListener(_tabClicks[i]);

            if (_subscribedClock != null)
                _subscribedClock.MinuteChanged -= HandleMinuteChanged;

            _subscribedClock = null;
            _bound = false;
        }

        private void BuildCatalog()
        {
            IReadOnlyList<ShopProductDefinition> products = _shop.Products;
            Transform content = _catalog.content;

            for (int i = 0; i < products.Count; i++)
            {
                ShopProductDefinition product = products[i];

                ShopProductCardView card = Instantiate(_productCardTemplate, content);
                card.name = product.ProductId;
                card.Clicked += () => OpenProductDetails(product);
                card.gameObject.SetActive(true);

                _productCards.Add(new ProductCard(product, card));
            }
        }

        private void ShowPage(Page page)
        {
            _page = page;

            _storefront.SetActive(page == Page.Storefront);
            _productDetails.SetActive(page == Page.ProductDetails);
            _orders.SetActive(page == Page.Orders);

            Refresh();
            RefreshOrdersShortcut();

            PageChanged?.Invoke();
        }

        private void SelectTab(int index)
        {
            if (!_phone.IsInteractive || _page != Page.Storefront)
                return;

            _selectedTab = index;

            RefreshCatalog();
            ScrollToTop(_catalog);
        }

        private void OpenProductDetails(ShopProductDefinition product)
        {
            if (!_phone.IsInteractive || _page != Page.Storefront)
                return;

            _selectedProduct = product;
            ShowPage(Page.ProductDetails);
        }

        private void OpenOrders()
        {
            if (!_phone.IsInteractive || _page != Page.Storefront)
                return;

            ShowPage(Page.Orders);
            ScrollToTop(_orderList);
        }

        private void BuySelectedProduct()
        {
            if (!_phone.IsInteractive ||
                _page != Page.ProductDetails ||
                _selectedProduct == null)
            {
                return;
            }

            _shop.TryPurchase(_selectedProduct.ProductId);

            Refresh();
        }

        private bool TryBack()
        {
            if (_page == Page.Storefront)
                return false;

            _selectedProduct = null;
            ShowPage(Page.Storefront);

            return true;
        }

        private void HandleLanguageChanged(GameLanguage language)
        {
            Refresh();
        }

        private void HandleMinuteChanged(GameTimeSnapshot time)
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
            RefreshOrders();
        }

        private void RefreshOrdersShortcut()
        {
            bool visible =
                _phone.IsOpen &&
                _phone.CurrentScreen == PhoneScreenId.Shop &&
                _page == Page.Storefront;

            if (_openOrders.gameObject.activeSelf != visible)
                _openOrders.gameObject.SetActive(visible);
        }

        private void RefreshCatalog()
        {
            for (int i = 0; i < _tabs.Length; i++)
            {
                CategoryTab tab = _tabs[i];
                bool selected = i == _selectedTab;

                tab.Label.text = _localization.Text(tab.LabelKey);
                tab.Label.color = selected ? _selectedTabLabelColor : _tabLabelColor;
                tab.Button.image.color = selected ? _selectedTabColor : _tabColor;
            }

            CategoryTab shelf = _tabs[_selectedTab];
            int listedCount = 0;

            for (int i = 0; i < _productCards.Count; i++)
            {
                ShopProductDefinition product = _productCards[i].Product;
                ShopProductCardView view = _productCards[i].View;

                bool listed = shelf.Lists(product);

                if (view.gameObject.activeSelf != listed)
                    view.gameObject.SetActive(listed);

                if (!listed)
                    continue;

                listedCount++;

                string productName =
                    _localization.Text(product.NameLocalizationKey);

                string description =
                    _localization.Text(product.DescriptionLocalizationKey);

                if (!product.IsAvailable)
                {
                    view.ShowUnavailable(
                        product.Image,
                        productName,
                        description,
                        _localization.Text("phone.unavailable"));

                    continue;
                }

                view.ShowAvailable(
                    product.Image,
                    productName,
                    description,
                    FormatMoney(product.PriceCents),
                    GetCardBadge(product));
            }

            _catalogEmpty.text = _localization.Text("phone.catalog_empty");
            _catalogEmpty.gameObject.SetActive(listedCount == 0);
        }

        private void RefreshProductDetails()
        {
            if (_selectedProduct == null)
                return;

            ShopProductDefinition product = _selectedProduct;

            _detailsImage.sprite = product.Image;
            _detailsMedia.SetActive(product.Image != null);

            _detailsCategory.text =
                _localization.Text(product.CategoryLocalizationKey);

            _detailsName.text =
                _localization.Text(product.NameLocalizationKey);

            _detailsPrice.text =
                FormatMoney(product.PriceCents);

            _detailsDescription.text =
                _localization.Text(product.DescriptionLocalizationKey);

            ShopPurchaseResultCode purchaseState =
                _shop.EvaluatePurchase(product.ProductId);

            bool hasActiveOrder =
                _shop.Orders.TryGetLatestActiveOrder(product.ProductId, out ShopOrder activeOrder);

            string delivery =
                GetDetailsDelivery(product, purchaseState, activeOrder);

            _detailsDelivery.text = delivery ?? string.Empty;
            _detailsDelivery.gameObject.SetActive(delivery != null);

            bool canPurchase =
                purchaseState == ShopPurchaseResultCode.Success;

            _detailsBuyLabel.text =
                _localization.Text(GetPurchaseButtonKey(purchaseState, hasActiveOrder));

            _detailsBuyLabel.color =
                canPurchase
                    ? _detailsBuyLabelColor
                    : _detailsBuyLabelDisabledColor;

            _detailsBuy.interactable = canPurchase;
        }

        private void RefreshOrders()
        {
            IReadOnlyList<ShopOrder> orders = _shop.Orders.Orders;

            for (int i = 0; i < orders.Count; i++)
            {
                ShopOrder order = orders[orders.Count - 1 - i];

                _shop.TryGetProduct(order.ProductId, out ShopProductDefinition product);

                ShopOrderRowView row = GetOrderRow(i);

                row.Show(
                    product?.Image,
                    product != null
                        ? _localization.Text(product.NameLocalizationKey)
                        : order.ProductId,
                    FormatMoney(order.PaidPriceCents),
                    _localization.Text(GetOrderStatusKey(order.Status)),
                    order.IsActive ? FormatDelivery(order.DeliveryDueAt) : null);

                row.gameObject.SetActive(true);
            }

            for (int i = orders.Count; i < _orderRows.Count; i++)
                _orderRows[i].gameObject.SetActive(false);

            _ordersEmpty.text = _localization.Text("phone.orders_empty");
            _ordersEmpty.gameObject.SetActive(orders.Count == 0);

            int activeOrders = _shop.Orders.CountActive();

            _openOrdersLabel.text = _localization.Text("phone.orders");
            _activeOrdersBadge.SetActive(activeOrders > 0);
            _activeOrdersCount.text =
                activeOrders > 99
                    ? "99+"
                    : activeOrders.ToString(CultureInfo.InvariantCulture);
        }

        private ShopOrderRowView GetOrderRow(int index)
        {
            while (_orderRows.Count <= index)
                _orderRows.Add(Instantiate(_orderRowTemplate, _orderList.content));

            return _orderRows[index];
        }

        private string GetCardBadge(ShopProductDefinition product)
        {
            if (_shop.Orders.TryGetLatestActiveOrder(product.ProductId, out _))
                return _localization.Text("phone.ordered");

            return _shop.EvaluatePurchase(product.ProductId) == ShopPurchaseResultCode.PurchaseLimitReached
                ? _localization.Text("phone.purchased")
                : null;
        }

        private string GetDetailsDelivery(
            ShopProductDefinition product,
            ShopPurchaseResultCode purchaseState,
            ShopOrder activeOrder)
        {
            if (activeOrder != null)
            {
                return _localization.Format(
                    "phone.ordered_with_eta",
                    FormatDelivery(activeOrder.DeliveryDueAt));
            }

            bool purchasable =
                purchaseState is ShopPurchaseResultCode.Success or ShopPurchaseResultCode.InsufficientFunds;

            if (purchasable &&
                _shop.TryEstimateDelivery(product.ProductId, out GameTimeSnapshot deliveryDueAt))
            {
                return FormatDelivery(deliveryDueAt);
            }

            return null;
        }

        private string FormatDelivery(GameTimeSnapshot dueAt)
        {
            string time = $"{dueAt.Hour:00}:{dueAt.Minute:00}";
            GameClock clock = _clock.Clock;

            if (clock == null || clock.Current.Day == dueAt.Day)
                return _localization.Format("phone.order_eta", time);

            return _localization.Format("phone.order_eta_day", dueAt.Day, time);
        }

        private static void ScrollToTop(ScrollRect scrollRect)
        {
            scrollRect.StopMovement();

            RectTransform content = scrollRect.content;
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, 0f);
        }

        private static string GetPurchaseButtonKey(ShopPurchaseResultCode state, bool hasActiveOrder)
        {
            return state switch
            {
                ShopPurchaseResultCode.Success => "phone.buy",
                ShopPurchaseResultCode.Busy => "phone.buy",
                ShopPurchaseResultCode.PurchaseLimitReached => hasActiveOrder ? "phone.ordered" : "phone.purchased",
                ShopPurchaseResultCode.InsufficientFunds => "phone.not_enough_money",
                _ => "phone.unavailable"
            };
        }

        private static string GetOrderStatusKey(ShopOrderStatus status)
        {
            return status switch
            {
                ShopOrderStatus.Delivered => "phone.order_delivered",
                _ => "phone.order_placed"
            };
        }

        private static string FormatMoney(long cents)
        {
            decimal dollars = cents / 100m;

            return $"${dollars.ToString("0.00", CultureInfo.InvariantCulture)}";
        }

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
                _detailsBuy == null ||
                _detailsBuyLabel == null ||
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
                CategoryTab tab = _tabs[i];

                if (tab != null &&
                    tab.Button != null &&
                    tab.Button.image != null &&
                    tab.Label != null &&
                    !string.IsNullOrWhiteSpace(tab.LabelKey))
                {
                    continue;
                }

                Debug.LogError(
                    $"{nameof(PhoneShopView)} on {name} contains an incomplete category tab at index {i}.",
                    this);

                return false;
            }

            if (!_productCardTemplate.IsConfigured || !_orderRowTemplate.IsConfigured)
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
