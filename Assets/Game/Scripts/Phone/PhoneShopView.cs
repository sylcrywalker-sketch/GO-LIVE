using System;
using System.Collections.Generic;
using System.Globalization;
using GoLive.GameTime;
using GoLive.Localization;
using GoLive.Shop;
using TMPro;
using UnityEngine;
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

        private readonly struct CategoryLabel
        {
            public string LocalizationKey { get; }
            public TMP_Text Text { get; }

            public CategoryLabel(string localizationKey, TMP_Text text)
            {
                LocalizationKey = localizationKey;
                Text = text;
            }
        }

        [Header("Core")]
        [SerializeField] private PhoneBehaviour _phone;
        [SerializeField] private LocalizationContext _localization;
        [SerializeField] private GameClockBehaviour _clock;
        [SerializeField] private ShopBehaviour _shop;

        [Header("Storefront")]
        [SerializeField] private GameObject _storefront;
        [SerializeField] private ScrollRect _catalog;
        [SerializeField] private TMP_Text _categoryTemplate;
        [SerializeField] private ShopProductCardView _productCardTemplate;
        [SerializeField] private Button _openOrders;
        [SerializeField] private TMP_Text _openOrdersLabel;
        [SerializeField] private GameObject _activeOrdersBadge;
        [SerializeField] private TMP_Text _activeOrdersCount;

        [Header("Product Details")]
        [SerializeField] private GameObject _productDetails;
        [SerializeField] private Image _detailsImage;
        [SerializeField] private TMP_Text _detailsCategory;
        [SerializeField] private TMP_Text _detailsName;
        [SerializeField] private TMP_Text _detailsPrice;
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

        private readonly List<CategoryLabel> _categoryLabels = new();
        private readonly List<ProductCard> _productCards = new();
        private readonly List<ShopOrderRowView> _orderRows = new();

        private Page _page;
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

            _categoryTemplate.gameObject.SetActive(false);
            _productCardTemplate.gameObject.SetActive(false);
            _orderRowTemplate.gameObject.SetActive(false);

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

        private void Bind()
        {
            if (_bound)
                return;

            _phone.ScreenBackRequested += TryBack;
            _localization.LanguageChanged += HandleLanguageChanged;
            _shop.Changed += Refresh;

            _openOrders.onClick.AddListener(OpenOrders);
            _detailsBuy.onClick.AddListener(BuySelectedProduct);

            _subscribedClock = _clock.Clock;

            if (_subscribedClock != null)
                _subscribedClock.DayChanged += HandleDayChanged;

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

            if (_subscribedClock != null)
                _subscribedClock.DayChanged -= HandleDayChanged;

            _subscribedClock = null;
            _bound = false;
        }

        private void BuildCatalog()
        {
            IReadOnlyList<ShopProductDefinition> products = _shop.Products;
            List<string> categories = new();

            for (int i = 0; i < products.Count; i++)
            {
                if (!categories.Contains(products[i].CategoryLocalizationKey))
                    categories.Add(products[i].CategoryLocalizationKey);
            }

            Transform content = _catalog.content;

            for (int c = 0; c < categories.Count; c++)
            {
                TMP_Text label = Instantiate(_categoryTemplate, content);
                label.name = categories[c];
                label.gameObject.SetActive(true);

                _categoryLabels.Add(new CategoryLabel(categories[c], label));

                for (int i = 0; i < products.Count; i++)
                {
                    ShopProductDefinition product = products[i];

                    if (!string.Equals(product.CategoryLocalizationKey, categories[c], StringComparison.Ordinal))
                        continue;

                    ShopProductCardView card = Instantiate(_productCardTemplate, content);
                    card.name = product.ProductId;
                    card.Clicked += () => OpenProductDetails(product);
                    card.gameObject.SetActive(true);

                    _productCards.Add(new ProductCard(product, card));
                }
            }
        }

        private void ShowPage(Page page)
        {
            _page = page;

            _storefront.SetActive(page == Page.Storefront);
            _productDetails.SetActive(page == Page.ProductDetails);
            _orders.SetActive(page == Page.Orders);

            Refresh();
            PageChanged?.Invoke();
        }

        private void OpenProductDetails(ShopProductDefinition product)
        {
            if (!_phone.IsInteractive ||
                _page != Page.Storefront ||
                !product.IsAvailable)
            {
                return;
            }

            _selectedProduct = product;
            ShowPage(Page.ProductDetails);
        }

        private void OpenOrders()
        {
            if (!_phone.IsInteractive || _page != Page.Storefront)
                return;

            ShowPage(Page.Orders);
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

        private void HandleDayChanged(int previousDay, int currentDay)
        {
            RefreshOrders();
        }

        private void Refresh()
        {
            RefreshCatalog();
            RefreshProductDetails();
            RefreshOrders();
        }

        private void RefreshCatalog()
        {
            for (int i = 0; i < _categoryLabels.Count; i++)
            {
                _categoryLabels[i].Text.text =
                    _localization.Text(_categoryLabels[i].LocalizationKey);
            }

            for (int i = 0; i < _productCards.Count; i++)
            {
                ShopProductDefinition product = _productCards[i].Product;
                ShopProductCardView view = _productCards[i].View;

                string productName =
                    _localization.Text(product.NameLocalizationKey);

                if (!product.IsAvailable)
                {
                    view.ShowUnavailable(
                        product.Image,
                        productName,
                        _localization.Text("phone.unavailable"));

                    continue;
                }

                bool ordered =
                    _shop.EvaluatePurchase(product.ProductId) ==
                    ShopPurchaseResultCode.PurchaseLimitReached;

                view.ShowAvailable(
                    product.Image,
                    productName,
                    FormatMoney(product.PriceCents),
                    _localization.Text(ordered ? "phone.ordered" : "phone.details"));
            }
        }

        private void RefreshProductDetails()
        {
            if (_selectedProduct == null)
                return;

            ShopProductDefinition product = _selectedProduct;

            _detailsImage.sprite = product.Image;
            _detailsImage.gameObject.SetActive(product.Image != null);

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

            bool canPurchase =
                purchaseState == ShopPurchaseResultCode.Success;

            _detailsBuyLabel.text =
                _localization.Text(GetPurchaseButtonKey(purchaseState));

            _detailsBuyLabel.color =
                canPurchase
                    ? _detailsBuyLabelColor
                    : _detailsBuyLabelDisabledColor;

            _detailsBuy.interactable = canPurchase;
        }

        private void RefreshOrders()
        {
            IReadOnlyList<ShopOrder> orders = _shop.Orders.Orders;
            int activeOrders = 0;

            for (int i = 0; i < orders.Count; i++)
            {
                ShopOrder order = orders[orders.Count - 1 - i];
                bool delivered = order.Status == ShopOrderStatus.Delivered;

                if (!delivered)
                    activeOrders++;

                _shop.TryGetProduct(order.ProductId, out ShopProductDefinition product);

                ShopOrderRowView row = GetOrderRow(i);

                row.Show(
                    product?.Image,
                    product != null
                        ? _localization.Text(product.NameLocalizationKey)
                        : order.ProductId,
                    FormatMoney(order.PaidPriceCents),
                    _localization.Text(GetOrderStatusKey(order.Status)),
                    delivered ? null : FormatDelivery(order.DeliveryDueAt));

                row.gameObject.SetActive(true);
            }

            for (int i = orders.Count; i < _orderRows.Count; i++)
                _orderRows[i].gameObject.SetActive(false);

            _ordersEmpty.text = _localization.Text("phone.orders_empty");
            _ordersEmpty.gameObject.SetActive(orders.Count == 0);

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

        private string FormatDelivery(GameTimeSnapshot dueAt)
        {
            string time = $"{dueAt.Hour:00}:{dueAt.Minute:00}";
            GameClock clock = _clock.Clock;

            if (clock == null || clock.Current.Day == dueAt.Day)
                return _localization.Format("phone.order_eta", time);

            return _localization.Format("phone.order_eta_day", dueAt.Day, time);
        }

        private static string GetPurchaseButtonKey(ShopPurchaseResultCode state)
        {
            return state switch
            {
                ShopPurchaseResultCode.Success => "phone.buy",
                ShopPurchaseResultCode.Busy => "phone.buy",
                ShopPurchaseResultCode.PurchaseLimitReached => "phone.ordered",
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
                _catalog == null ||
                _catalog.content == null ||
                _categoryTemplate == null ||
                _productCardTemplate == null ||
                _openOrders == null ||
                _openOrdersLabel == null ||
                _activeOrdersBadge == null ||
                _activeOrdersCount == null ||
                _productDetails == null ||
                _detailsImage == null ||
                _detailsCategory == null ||
                _detailsName == null ||
                _detailsPrice == null ||
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
