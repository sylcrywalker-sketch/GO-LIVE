using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GoLive.Phone
{
    public readonly struct ShopCartLineViewData
    {
        public string ProductId { get; }
        public Sprite Image { get; }
        public string ProductName { get; }
        public string Category { get; }
        public string LinePrice { get; }
        public int Quantity { get; }
        public bool CanAddOne { get; }

        public ShopCartLineViewData(
            string productId,
            Sprite image,
            string productName,
            string category,
            string linePrice,
            int quantity,
            bool canAddOne)
        {
            ProductId = productId;
            Image = image;
            ProductName = productName;
            Category = category;
            LinePrice = linePrice;
            Quantity = quantity;
            CanAddOne = canAddOne;
        }
    }

    [DisallowMultipleComponent]
    public sealed class ShopCartView : MonoBehaviour
    {
        [SerializeField] private ScrollRect _list;
        [SerializeField] private ShopCartRowView _rowTemplate;
        [SerializeField] private TMP_Text _empty;
        [SerializeField] private TMP_Text _total;
        [SerializeField] private TMP_Text _status;
        [SerializeField] private Button _checkout;
        [SerializeField] private TMP_Text _checkoutLabel;

        public event Action<string> AddOneRequested;
        public event Action<string> RemoveOneRequested;
        public event Action<string> RemoveAllRequested;
        public event Action CheckoutRequested;

        public bool IsConfigured =>
            _list != null &&
            _list.content != null &&
            _rowTemplate != null &&
            _rowTemplate.IsConfigured &&
            _empty != null &&
            _total != null &&
            _status != null &&
            _checkout != null &&
            _checkoutLabel != null;

        private readonly List<ShopCartRowView> _rows = new();
        private bool _bound;

        private void Awake()
        {
            if (!IsConfigured)
            {
                Debug.LogError(
                    $"{nameof(ShopCartView)} on {name} has incomplete configuration.",
                    this);

                enabled = false;
                return;
            }

            _rowTemplate.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            Bind();
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void OnDestroy()
        {
            Unbind();

            for (int i = 0; i < _rows.Count; i++)
                UnbindRow(_rows[i]);
        }

        public void Render(
            IReadOnlyList<ShopCartLineViewData> lines,
            string emptyText,
            string totalText,
            string checkoutText,
            string statusText,
            bool checkoutInteractable)
        {
            if (lines == null)
                throw new ArgumentNullException(nameof(lines));

            for (int i = 0; i < lines.Count; i++)
            {
                ShopCartLineViewData line = lines[i];
                ShopCartRowView row = GetRow(i);

                row.Show(
                    line.ProductId,
                    line.Image,
                    line.ProductName,
                    line.Category,
                    line.LinePrice,
                    line.Quantity,
                    line.CanAddOne);

                row.gameObject.SetActive(true);
            }

            for (int i = lines.Count; i < _rows.Count; i++)
                _rows[i].gameObject.SetActive(false);

            bool isEmpty = lines.Count == 0;

            _empty.text = emptyText;
            _empty.gameObject.SetActive(isEmpty);

            _total.text = totalText;
            _total.gameObject.SetActive(!isEmpty);

            _checkoutLabel.text = checkoutText;
            _checkout.interactable = !isEmpty && checkoutInteractable;

            bool hasStatus = !string.IsNullOrWhiteSpace(statusText);
            _status.text = hasStatus ? statusText : string.Empty;
            _status.gameObject.SetActive(hasStatus);
        }

        public void ScrollToTop()
        {
            if (_list == null || _list.content == null)
                return;

            _list.StopMovement();

            RectTransform content = _list.content;
            content.anchoredPosition =
                new Vector2(content.anchoredPosition.x, 0f);
        }

        private ShopCartRowView GetRow(int index)
        {
            while (_rows.Count <= index)
            {
                ShopCartRowView row =
                    Instantiate(_rowTemplate, _list.content);

                row.name = $"CartRow_{_rows.Count:00}";
                BindRow(row);
                _rows.Add(row);
            }

            return _rows[index];
        }

        private void Bind()
        {
            if (_bound || _checkout == null)
                return;

            _checkout.onClick.AddListener(HandleCheckout);
            _bound = true;
        }

        private void Unbind()
        {
            if (!_bound)
                return;

            if (_checkout != null)
                _checkout.onClick.RemoveListener(HandleCheckout);

            _bound = false;
        }

        private void BindRow(ShopCartRowView row)
        {
            row.AddOneClicked += HandleAddOne;
            row.RemoveOneClicked += HandleRemoveOne;
            row.RemoveAllClicked += HandleRemoveAll;
        }

        private void UnbindRow(ShopCartRowView row)
        {
            if (row == null)
                return;

            row.AddOneClicked -= HandleAddOne;
            row.RemoveOneClicked -= HandleRemoveOne;
            row.RemoveAllClicked -= HandleRemoveAll;
        }

        private void HandleAddOne(string productId)
        {
            AddOneRequested?.Invoke(productId);
        }

        private void HandleRemoveOne(string productId)
        {
            RemoveOneRequested?.Invoke(productId);
        }

        private void HandleRemoveAll(string productId)
        {
            RemoveAllRequested?.Invoke(productId);
        }

        private void HandleCheckout()
        {
            CheckoutRequested?.Invoke();
        }
    }
}
