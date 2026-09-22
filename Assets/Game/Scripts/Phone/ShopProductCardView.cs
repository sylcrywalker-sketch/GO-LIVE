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
        [SerializeField] private Image _image;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _price;
        [SerializeField] private TMP_Text _status;
        [SerializeField] private GameObject _action;
        [SerializeField] private TMP_Text _actionLabel;
        [SerializeField, Range(0f, 1f)] private float _unavailableAlpha = 0.45f;

        public event Action Clicked;

        public bool IsConfigured =>
            _button != null &&
            _canvasGroup != null &&
            _image != null &&
            _name != null &&
            _price != null &&
            _status != null &&
            _action != null &&
            _actionLabel != null;

        private void Awake()
        {
            _button.onClick.AddListener(HandleClick);
        }

        private void OnDestroy()
        {
            _button.onClick.RemoveListener(HandleClick);
        }

        public void ShowAvailable(Sprite image, string productName, string price, string action)
        {
            Show(image, productName, true);

            _price.text = price;
            _actionLabel.text = action;
        }

        public void ShowUnavailable(Sprite image, string productName, string status)
        {
            Show(image, productName, false);

            _status.text = status;
        }

        private void Show(Sprite image, string productName, bool available)
        {
            _image.sprite = image;
            _image.gameObject.SetActive(image != null);

            _name.text = productName;

            _price.gameObject.SetActive(available);
            _action.SetActive(available);
            _status.gameObject.SetActive(!available);

            _button.interactable = available;
            _canvasGroup.alpha = available ? 1f : _unavailableAlpha;
        }

        private void HandleClick()
        {
            Clicked?.Invoke();
        }
    }
}
