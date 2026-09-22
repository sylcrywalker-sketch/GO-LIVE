using System;
using TMPro;
using UnityEngine;
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
        [SerializeField] private TMP_Text _description;
        [SerializeField] private GameObject _badge;
        [SerializeField] private TMP_Text _badgeLabel;
        [SerializeField, Range(0f, 1f)] private float _unavailableAlpha = 0.55f;

        public event Action Clicked;

        public bool IsConfigured =>
            _button != null &&
            _canvasGroup != null &&
            _thumbnail != null &&
            _image != null &&
            _name != null &&
            _price != null &&
            _status != null &&
            _description != null &&
            _badge != null &&
            _badgeLabel != null;

        private void Awake()
        {
            _button.onClick.AddListener(HandleClick);
        }

        private void OnDestroy()
        {
            _button.onClick.RemoveListener(HandleClick);
        }

        public void ShowAvailable(Sprite image, string productName, string description, string price, string badge)
        {
            Show(image, productName, description, true);

            _price.text = price;

            bool hasBadge = !string.IsNullOrEmpty(badge);

            _badgeLabel.text = hasBadge ? badge : string.Empty;
            _badge.SetActive(hasBadge);
        }

        public void ShowUnavailable(Sprite image, string productName, string description, string status)
        {
            Show(image, productName, description, false);

            _status.text = status;
            _badge.SetActive(false);
        }

        private void Show(Sprite image, string productName, string description, bool available)
        {
            _image.sprite = image;
            _thumbnail.SetActive(image != null);

            _name.text = productName;
            _description.text = description;

            _price.gameObject.SetActive(available);
            _status.gameObject.SetActive(!available);

            _canvasGroup.alpha = available ? 1f : _unavailableAlpha;
        }

        private void HandleClick()
        {
            Clicked?.Invoke();
        }
    }
}
