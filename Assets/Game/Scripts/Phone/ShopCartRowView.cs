using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GoLive.Phone
{
    [DisallowMultipleComponent]
    public sealed class ShopCartRowView : MonoBehaviour
    {
        [SerializeField] private GameObject _thumbnail;
        [SerializeField] private Image _image;

        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _category;
        [SerializeField] private TMP_Text _price;
        [SerializeField] private TMP_Text _quantity;

        [SerializeField] private Button _addOneButton;
        [SerializeField] private Button _removeOneButton;
        [SerializeField] private Button _removeAllButton;

        public event Action<string> AddOneClicked;
        public event Action<string> RemoveOneClicked;
        public event Action<string> RemoveAllClicked;

        public bool IsConfigured =>
            _thumbnail != null &&
            _image != null &&
            _name != null &&
            _category != null &&
            _price != null &&
            _quantity != null &&
            _addOneButton != null &&
            _removeOneButton != null &&
            _removeAllButton != null;

        private string _productId;

        private void Awake()
        {
            if (!IsConfigured)
            {
                Debug.LogError(
                    $"{nameof(ShopCartRowView)} on {name} has incomplete configuration.",
                    this);

                enabled = false;
                return;
            }

            _addOneButton.onClick.AddListener(HandleAddOne);
            _removeOneButton.onClick.AddListener(HandleRemoveOne);
            _removeAllButton.onClick.AddListener(HandleRemoveAll);
        }

        private void OnDestroy()
        {
            if (_addOneButton != null)
                _addOneButton.onClick.RemoveListener(HandleAddOne);

            if (_removeOneButton != null)
                _removeOneButton.onClick.RemoveListener(HandleRemoveOne);

            if (_removeAllButton != null)
                _removeAllButton.onClick.RemoveListener(HandleRemoveAll);
        }

        public void Show(
            string productId,
            Sprite image,
            string productName,
            string category,
            string linePrice,
            int quantity,
            bool canAddOne)
        {
            if (string.IsNullOrWhiteSpace(productId))
            {
                throw new ArgumentException(
                    "Product ID is required.",
                    nameof(productId));
            }

            if (quantity <= 0)
                throw new ArgumentOutOfRangeException(nameof(quantity));

            _productId = productId;

            _image.sprite = image;
            _thumbnail.SetActive(image != null);

            _name.text = productName;
            _category.text = category;
            _price.text = linePrice;
            _quantity.text = quantity.ToString(CultureInfo.InvariantCulture);

            _addOneButton.interactable = canAddOne;
            _removeOneButton.interactable = true;
            _removeAllButton.interactable = true;
        }

        private void HandleAddOne()
        {
            if (!string.IsNullOrWhiteSpace(_productId))
                AddOneClicked?.Invoke(_productId);
        }

        private void HandleRemoveOne()
        {
            if (!string.IsNullOrWhiteSpace(_productId))
                RemoveOneClicked?.Invoke(_productId);
        }

        private void HandleRemoveAll()
        {
            if (!string.IsNullOrWhiteSpace(_productId))
                RemoveAllClicked?.Invoke(_productId);
        }
    }
}
