using System;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace GoLive.Phone
{
    [DisallowMultipleComponent]
    public sealed class ShopProductCardView : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private CanvasGroup _canvasGroup;

        [SerializeField] private GameObject _thumbnail;
        [SerializeField] private Image _image;

        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _price;
        [SerializeField] private TMP_Text _status;

        [FormerlySerializedAs("_description")]
        [SerializeField] private TMP_Text _category;

        [SerializeField] private GameObject _badge;
        [SerializeField] private TMP_Text _badgeLabel;

        [FormerlySerializedAs("_quickBuyButton")]
        [SerializeField] private Button _addToCartButton;

        [SerializeField, Range(0f, 1f)]
        private float _unavailableAlpha = 0.55f;

        public event Action Clicked;
        public event Action AddToCartClicked;

        public bool IsConfigured =>
            _button != null &&
            _canvasGroup != null &&
            _thumbnail != null &&
            _image != null &&
            _name != null &&
            _price != null &&
            _status != null &&
            _category != null &&
            _badge != null &&
            _badgeLabel != null &&
            _addToCartButton != null;

        private void Awake()
        {
            if (!IsConfigured)
            {
                Debug.LogError(
                    $"{nameof(ShopProductCardView)} on {name} has incomplete configuration.",
                    this);

                enabled = false;
                return;
            }

            _button.onClick.AddListener(HandleClick);
            _addToCartButton.onClick.AddListener(HandleAddToCart);
        }

        private void OnDestroy()
        {
            if (_button != null)
                _button.onClick.RemoveListener(HandleClick);

            if (_addToCartButton != null)
                _addToCartButton.onClick.RemoveListener(HandleAddToCart);
        }

        public void ShowAvailable(
            Sprite image,
            string productName,
            string category,
            string price,
            string badge,
            bool canAddToCart)
        {
            ShowBase(
                image,
                productName,
                category,
                available: true);

            _price.text = price;

            bool hasBadge = !string.IsNullOrEmpty(badge);

            _badgeLabel.text =
                hasBadge
                    ? badge
                    : string.Empty;

            _badge.SetActive(hasBadge);

            _addToCartButton.gameObject.SetActive(true);
            _addToCartButton.interactable = canAddToCart;
        }

        public void ShowUnavailable(
            Sprite image,
            string productName,
            string category,
            string status)
        {
            ShowBase(
                image,
                productName,
                category,
                available: false);

            _status.text = status;

            _badge.SetActive(false);

            _addToCartButton.gameObject.SetActive(false);
            _addToCartButton.interactable = false;
        }

        private void ShowBase(
            Sprite image,
            string productName,
            string category,
            bool available)
        {
            _image.sprite = image;
            _thumbnail.SetActive(image != null);

            _name.text = productName;
            _category.text = category;

            _price.gameObject.SetActive(available);
            _status.gameObject.SetActive(!available);

            _canvasGroup.alpha =
                available
                    ? 1f
                    : _unavailableAlpha;
        }

        private void HandleClick()
        {
            Clicked?.Invoke();
        }

        private void HandleAddToCart()
        {
            AddToCartClicked?.Invoke();
        }
    }
}
