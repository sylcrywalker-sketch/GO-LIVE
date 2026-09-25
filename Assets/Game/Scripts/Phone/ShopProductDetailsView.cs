using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GoLive.Phone
{
    // What the product page shows, ready to draw. Delivery is null when there is nothing to say about it.
    public readonly struct ShopProductDetailsData
    {
        public Sprite Image { get; }
        public string Name { get; }
        public string CategoryName { get; }
        public string Price { get; }
        public string Description { get; }
        public string Delivery { get; }
        public string AddToCartLabel { get; }
        public bool CanAddToCart { get; }

        public ShopProductDetailsData(
            Sprite image,
            string name,
            string categoryName,
            string price,
            string description,
            string delivery,
            string addToCartLabel,
            bool canAddToCart)
        {
            Image = image;
            Name = name;
            CategoryName = categoryName;
            Price = price;
            Description = description;
            Delivery = delivery;
            AddToCartLabel = addToCartLabel;
            CanAddToCart = canAddToCart;
        }
    }

    // The product page: image, name, category, price, delivery, description and "add to cart". It never buys.
    [DisallowMultipleComponent]
    public sealed class ShopProductDetailsView : MonoBehaviour
    {
        [SerializeField] private GameObject _media;
        [SerializeField] private Image _image;
        [SerializeField] private TMP_Text _category;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _price;
        [SerializeField] private TMP_Text _delivery;
        [SerializeField] private TMP_Text _description;
        [SerializeField] private Button _addToCart;
        [SerializeField] private TMP_Text _addToCartLabel;
        [SerializeField] private Color _addToCartLabelColor = new(0.082f, 0.125f, 0.165f, 1f);
        [SerializeField] private Color _addToCartLabelDisabledColor = new(0.557f, 0.616f, 0.667f, 1f);

        public event Action AddToCartRequested;

        public bool IsConfigured =>
            _media != null &&
            _image != null &&
            _category != null &&
            _name != null &&
            _price != null &&
            _delivery != null &&
            _description != null &&
            _addToCart != null &&
            _addToCartLabel != null;

        private void OnEnable()
        {
            _addToCart.onClick.AddListener(HandleAddToCart);
        }

        private void OnDisable()
        {
            _addToCart.onClick.RemoveListener(HandleAddToCart);
        }

        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible)
                gameObject.SetActive(visible);
        }

        public void Show(in ShopProductDetailsData data)
        {
            _image.sprite = data.Image;
            _media.SetActive(data.Image != null);

            _category.text = data.CategoryName;
            _name.text = data.Name;
            _price.text = data.Price;
            _description.text = data.Description;

            bool hasDelivery = !string.IsNullOrEmpty(data.Delivery);
            _delivery.text = hasDelivery ? data.Delivery : string.Empty;
            _delivery.gameObject.SetActive(hasDelivery);

            _addToCartLabel.text = data.AddToCartLabel;
            _addToCartLabel.color = data.CanAddToCart ? _addToCartLabelColor : _addToCartLabelDisabledColor;
            _addToCart.interactable = data.CanAddToCart;
        }

        private void HandleAddToCart()
        {
            AddToCartRequested?.Invoke();
        }
    }
}
