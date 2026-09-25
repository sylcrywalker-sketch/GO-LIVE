using System;
using System.Collections.Generic;
using GoLive.Items;
using GoLive.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace GoLive.Phone
{
    // The storefront page: category tabs, one card per product filtered by the selected tab (presentation state only),
    // and the Cart and Orders shortcuts with their badges. It lives inside the Shop screen, so nothing of it can show
    // or take clicks anywhere else. It raises requests; PhoneShopView decides what they do.
    [DisallowMultipleComponent]
    public sealed class ShopStorefrontView : MonoBehaviour
    {
        [Serializable]
        private sealed class CategoryTab
        {
            [field: SerializeField] public Button Button { get; private set; }
            [field: SerializeField] public TMP_Text Label { get; private set; }
            [field: SerializeField] public bool ShowsFeatured { get; private set; }
            [field: SerializeField] public ItemCategory Category { get; private set; }

            public string LabelKey => ShowsFeatured ? ShopText.FeaturedTabKey : ItemCategoryLocalization.GetKey(Category);

            public bool Lists(in ShopProductCardData card)
            {
                return ShowsFeatured ? card.Featured : card.Category == Category;
            }
        }

        [Header("Tabs")]
        [SerializeField] private CategoryTab[] _tabs = Array.Empty<CategoryTab>();
        [SerializeField] private Color _tabColor = new(0.133f, 0.192f, 0.247f, 1f);
        [SerializeField] private Color _tabLabelColor = new(0.702f, 0.749f, 0.784f, 1f);
        [SerializeField] private Color _selectedTabColor = new(0.278f, 0.388f, 0.494f, 1f);
        [SerializeField] private Color _selectedTabLabelColor = new(0.933f, 0.945f, 0.933f, 1f);

        [Header("Catalog")]
        [SerializeField] private ScrollRect _catalog;
        [SerializeField] private ShopProductCardView _cardTemplate;
        [SerializeField] private TMP_Text _empty;

        [Header("Shortcuts")]
        [SerializeField] private Button _openCart;
        [SerializeField] private TMP_Text _openCartLabel;
        [SerializeField] private GameObject _cartBadge;
        [SerializeField] private TMP_Text _cartCount;
        [SerializeField] private Button _openOrders;
        [SerializeField] private TMP_Text _openOrdersLabel;
        [SerializeField] private GameObject _ordersBadge;
        [SerializeField] private TMP_Text _ordersCount;

        public event Action<string> ProductSelected;
        public event Action<string> AddToCartRequested;
        public event Action CartRequested;
        public event Action OrdersRequested;

        private readonly List<ShopProductCardData> _products = new();
        private readonly List<ShopProductCardView> _cards = new();

        private UnityAction[] _tabClicks = Array.Empty<UnityAction>();
        private int _selectedTab;

        public bool IsConfigured
        {
            get
            {
                if (_tabs == null ||
                    _tabs.Length == 0 ||
                    _catalog == null ||
                    _catalog.content == null ||
                    _cardTemplate == null ||
                    !_cardTemplate.IsConfigured ||
                    _cardTemplate.gameObject.activeSelf ||
                    _empty == null ||
                    _openCart == null ||
                    _openCartLabel == null ||
                    _cartBadge == null ||
                    _cartCount == null ||
                    _openOrders == null ||
                    _openOrdersLabel == null ||
                    _ordersBadge == null ||
                    _ordersCount == null)
                {
                    return false;
                }

                for (int i = 0; i < _tabs.Length; i++)
                {
                    CategoryTab tab = _tabs[i];

                    if (tab == null ||
                        tab.Button == null ||
                        tab.Button.image == null ||
                        tab.Label == null ||
                        (!tab.ShowsFeatured && !Enum.IsDefined(typeof(ItemCategory), tab.Category)))
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        private void Awake()
        {
            _tabClicks = new UnityAction[_tabs.Length];

            for (int i = 0; i < _tabs.Length; i++)
            {
                int index = i;
                _tabClicks[i] = () => SelectTab(index);
            }
        }

        private void OnEnable()
        {
            for (int i = 0; i < _tabs.Length; i++)
                _tabs[i].Button.onClick.AddListener(_tabClicks[i]);

            _openCart.onClick.AddListener(HandleOpenCart);
            _openOrders.onClick.AddListener(HandleOpenOrders);
        }

        private void OnDisable()
        {
            for (int i = 0; i < _tabs.Length; i++)
                _tabs[i].Button.onClick.RemoveListener(_tabClicks[i]);

            _openCart.onClick.RemoveListener(HandleOpenCart);
            _openOrders.onClick.RemoveListener(HandleOpenOrders);
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                if (_cards[i] == null)
                    continue;

                _cards[i].Selected -= HandleCardSelected;
                _cards[i].AddRequested -= HandleCardAddRequested;
            }
        }

        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible)
                gameObject.SetActive(visible);
        }

        public void Show(IReadOnlyList<ShopProductCardData> products, int cartItems, int activeOrders, LocalizationContext localization)
        {
            _products.Clear();

            for (int i = 0; i < products.Count; i++)
                _products.Add(products[i]);

            for (int i = 0; i < _tabs.Length; i++)
                _tabs[i].Label.text = localization.Text(_tabs[i].LabelKey);

            _empty.text = localization.Text(ShopText.CatalogEmptyKey);
            _openCartLabel.text = localization.Text(ShopText.CartTitleKey);
            _openOrdersLabel.text = localization.Text(ShopText.OrdersTitleKey);

            SetBadge(_cartBadge, _cartCount, cartItems);
            SetBadge(_ordersBadge, _ordersCount, activeOrders);

            RenderTabs();
            RenderCards();
        }

        private void SelectTab(int index)
        {
            if (index == _selectedTab)
                return;

            _selectedTab = index;

            RenderTabs();
            RenderCards();

            _catalog.StopMovement();
            _catalog.content.anchoredPosition = new Vector2(_catalog.content.anchoredPosition.x, 0f);
        }

        private void RenderTabs()
        {
            for (int i = 0; i < _tabs.Length; i++)
            {
                bool selected = i == _selectedTab;

                _tabs[i].Label.color = selected ? _selectedTabLabelColor : _tabLabelColor;
                _tabs[i].Button.image.color = selected ? _selectedTabColor : _tabColor;
            }
        }

        private void RenderCards()
        {
            CategoryTab shelf = _tabs[_selectedTab];
            int listed = 0;

            for (int i = 0; i < _products.Count; i++)
            {
                ShopProductCardView card = GetCard(i, _products[i].ProductId);
                bool isListed = shelf.Lists(_products[i]);

                if (card.gameObject.activeSelf != isListed)
                    card.gameObject.SetActive(isListed);

                if (!isListed)
                    continue;

                card.Show(_products[i]);
                listed++;
            }

            for (int i = _products.Count; i < _cards.Count; i++)
                _cards[i].gameObject.SetActive(false);

            _empty.gameObject.SetActive(listed == 0);
        }

        private ShopProductCardView GetCard(int index, string productId)
        {
            while (_cards.Count <= index)
            {
                ShopProductCardView card = Instantiate(_cardTemplate, _catalog.content);
                card.name = $"Product {productId}";
                card.Selected += HandleCardSelected;
                card.AddRequested += HandleCardAddRequested;

                _cards.Add(card);
            }

            return _cards[index];
        }

        private void HandleCardSelected(string productId)
        {
            ProductSelected?.Invoke(productId);
        }

        private void HandleCardAddRequested(string productId)
        {
            AddToCartRequested?.Invoke(productId);
        }

        private void HandleOpenCart()
        {
            CartRequested?.Invoke();
        }

        private void HandleOpenOrders()
        {
            OrdersRequested?.Invoke();
        }

        private static void SetBadge(GameObject badge, TMP_Text count, int value)
        {
            badge.SetActive(value > 0);
            count.text = ShopText.FormatBadgeCount(value);
        }
    }
}
