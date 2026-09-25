using System;
using GoLive.Items;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GoLive.Phone
{
    // What one storefront card shows, ready to draw. Category and Featured are the storefront's filter keys.
    public readonly struct ShopProductCardData
    {
        public string ProductId { get; }
        public ItemCategory Category { get; }
        public bool Featured { get; }
        public Sprite Image { get; }
        public string Name { get; }
        public string CategoryName { get; }
        public string Price { get; }
        public string Badge { get; }
        public bool IsAvailable { get; }
        public bool CanAddToCart { get; }

        public ShopProductCardData(
            string productId,
            ItemCategory category,
            bool featured,
            Sprite image,
            string name,
            string categoryName,
            string price,
            string badge,
            bool isAvailable,
            bool canAddToCart)
        {
            ProductId = productId;
            Category = category;
            Featured = featured;
            Image = image;
            Name = name;
            CategoryName = categoryName;
            Price = price;
            Badge = badge;
            IsAvailable = isAvailable;
            CanAddToCart = canAddToCart;
        }
    }

    // One storefront card: [image] name / category / price [+]. The card itself opens the product, [+] asks to add one to
    // the cart; neither changes anything by itself.
    [DisallowMultipleComponent]
    public sealed class ShopProductCardView : MonoBehaviour
    {
        [SerializeField] private Button _open;
        [SerializeField] private Button _add;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Image _image;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _category;
        [SerializeField] private TMP_Text _price;
        [SerializeField] private GameObject _badge;
        [SerializeField] private TMP_Text _badgeLabel;
        [SerializeField, Range(0f, 1f)] private float _unavailableAlpha = 0.55f;

        public string ProductId { get; private set; }

        public event Action<string> Selected;
        public event Action<string> AddRequested;

        public bool IsConfigured =>
            _open != null &&
            _add != null &&
            _canvasGroup != null &&
            _image != null &&
            _name != null &&
            _category != null &&
            _price != null &&
            _badge != null &&
            _badgeLabel != null;

        private void Awake()
        {
            _open.onClick.AddListener(HandleOpen);
            _add.onClick.AddListener(HandleAdd);
        }

        private void OnDestroy()
        {
            _open.onClick.RemoveListener(HandleOpen);
            _add.onClick.RemoveListener(HandleAdd);
        }

        public void Show(in ShopProductCardData data)
        {
            ProductId = data.ProductId;

            _image.sprite = data.Image;
            _image.enabled = data.Image != null;

            _name.text = data.Name;
            _category.text = data.CategoryName;
            _price.text = data.Price;

            bool hasBadge = !string.IsNullOrEmpty(data.Badge);
            _badgeLabel.text = hasBadge ? data.Badge : string.Empty;
            _badge.SetActive(hasBadge);

            _add.interactable = data.CanAddToCart;
            _canvasGroup.alpha = data.IsAvailable ? 1f : _unavailableAlpha;
        }

        private void HandleOpen()
        {
            Selected?.Invoke(ProductId);
        }

        private void HandleAdd()
        {
            AddRequested?.Invoke(ProductId);
        }
    }
}
