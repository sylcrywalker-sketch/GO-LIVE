using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GoLive.Phone
{
    // What one cart row shows, ready to draw.
    public readonly struct ShopCartRowData
    {
        public string ProductId { get; }
        public Sprite Image { get; }
        public string Name { get; }
        public string CategoryName { get; }
        public string LinePrice { get; }
        public string Quantity { get; }
        public bool CanAddOne { get; }

        public ShopCartRowData(
            string productId,
            Sprite image,
            string name,
            string categoryName,
            string linePrice,
            string quantity,
            bool canAddOne)
        {
            ProductId = productId;
            Image = image;
            Name = name;
            CategoryName = categoryName;
            LinePrice = linePrice;
            Quantity = quantity;
            CanAddOne = canAddOne;
        }
    }

    // One cart line: [image] name / category / line price, then [-] quantity [+] [x]. The buttons only ask; the cart
    // itself is owned by ShopBehaviour.
    [DisallowMultipleComponent]
    public sealed class ShopCartRowView : MonoBehaviour
    {
        [SerializeField] private Image _image;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _category;
        [SerializeField] private TMP_Text _linePrice;
        [SerializeField] private TMP_Text _quantity;
        [SerializeField] private Button _removeOne;
        [SerializeField] private Button _addOne;
        [SerializeField] private Button _removeAll;

        public string ProductId { get; private set; }

        public event Action<string> AddOneRequested;
        public event Action<string> RemoveOneRequested;
        public event Action<string> RemoveAllRequested;

        public bool IsConfigured =>
            _image != null &&
            _name != null &&
            _category != null &&
            _linePrice != null &&
            _quantity != null &&
            _removeOne != null &&
            _addOne != null &&
            _removeAll != null;

        private void Awake()
        {
            _removeOne.onClick.AddListener(HandleRemoveOne);
            _addOne.onClick.AddListener(HandleAddOne);
            _removeAll.onClick.AddListener(HandleRemoveAll);
        }

        private void OnDestroy()
        {
            _removeOne.onClick.RemoveListener(HandleRemoveOne);
            _addOne.onClick.RemoveListener(HandleAddOne);
            _removeAll.onClick.RemoveListener(HandleRemoveAll);
        }

        public void Show(in ShopCartRowData data)
        {
            ProductId = data.ProductId;

            _image.sprite = data.Image;
            _image.enabled = data.Image != null;

            _name.text = data.Name;
            _category.text = data.CategoryName;
            _linePrice.text = data.LinePrice;
            _quantity.text = data.Quantity;

            _addOne.interactable = data.CanAddOne;
        }

        private void HandleAddOne()
        {
            AddOneRequested?.Invoke(ProductId);
        }

        private void HandleRemoveOne()
        {
            RemoveOneRequested?.Invoke(ProductId);
        }

        private void HandleRemoveAll()
        {
            RemoveAllRequested?.Invoke(ProductId);
        }
    }
}
