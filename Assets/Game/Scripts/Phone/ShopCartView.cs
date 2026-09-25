using System;
using System.Collections.Generic;
using GoLive.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GoLive.Phone
{
    // The cart page: one row per cart line, the total and "place order". It owns no cart state; every button raises a
    // request and PhoneShopView sends it to ShopBehaviour.
    [DisallowMultipleComponent]
    public sealed class ShopCartView : MonoBehaviour
    {
        [SerializeField] private ScrollRect _list;
        [SerializeField] private ShopCartRowView _rowTemplate;
        [SerializeField] private TMP_Text _empty;
        [SerializeField] private GameObject _footer;
        [SerializeField] private TMP_Text _total;
        [SerializeField] private TMP_Text _problem;
        [SerializeField] private Button _checkout;
        [SerializeField] private TMP_Text _checkoutLabel;
        [SerializeField] private Color _checkoutLabelColor = new(0.082f, 0.125f, 0.165f, 1f);
        [SerializeField] private Color _checkoutLabelDisabledColor = new(0.557f, 0.616f, 0.667f, 1f);

        public event Action<string> AddOneRequested;
        public event Action<string> RemoveOneRequested;
        public event Action<string> RemoveAllRequested;
        public event Action CheckoutRequested;

        private readonly List<ShopCartRowView> _rows = new();

        public bool IsConfigured =>
            _list != null &&
            _list.content != null &&
            _rowTemplate != null &&
            _rowTemplate.IsConfigured &&
            !_rowTemplate.gameObject.activeSelf &&
            _empty != null &&
            _footer != null &&
            _total != null &&
            _problem != null &&
            _checkout != null &&
            _checkoutLabel != null;

        private void OnEnable()
        {
            _checkout.onClick.AddListener(HandleCheckout);

            _list.StopMovement();
            _list.content.anchoredPosition = new Vector2(_list.content.anchoredPosition.x, 0f);
        }

        private void OnDisable()
        {
            _checkout.onClick.RemoveListener(HandleCheckout);
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i] == null)
                    continue;

                _rows[i].AddOneRequested -= HandleAddOne;
                _rows[i].RemoveOneRequested -= HandleRemoveOne;
                _rows[i].RemoveAllRequested -= HandleRemoveAll;
            }
        }

        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible)
                gameObject.SetActive(visible);
        }

        // problem: why the cart cannot be ordered right now, or null.
        public void Show(IReadOnlyList<ShopCartRowData> rows, string total, bool canCheckout, string problem, LocalizationContext localization)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                ShopCartRowView row = GetRow(i);

                row.Show(rows[i]);

                if (!row.gameObject.activeSelf)
                    row.gameObject.SetActive(true);
            }

            for (int i = rows.Count; i < _rows.Count; i++)
                _rows[i].gameObject.SetActive(false);

            bool hasLines = rows.Count > 0;

            _empty.text = localization.Text(ShopText.CartEmptyKey);
            _empty.gameObject.SetActive(!hasLines);
            _footer.SetActive(hasLines);

            _total.text = total;
            _checkoutLabel.text = localization.Text(ShopText.CheckoutKey);
            _checkoutLabel.color = canCheckout ? _checkoutLabelColor : _checkoutLabelDisabledColor;
            _checkout.interactable = canCheckout;

            bool hasProblem = hasLines && !string.IsNullOrEmpty(problem);
            _problem.text = hasProblem ? problem : string.Empty;
            _problem.gameObject.SetActive(hasProblem);
        }

        private ShopCartRowView GetRow(int index)
        {
            while (_rows.Count <= index)
            {
                ShopCartRowView row = Instantiate(_rowTemplate, _list.content);
                row.name = $"CartRow{_rows.Count}";
                row.AddOneRequested += HandleAddOne;
                row.RemoveOneRequested += HandleRemoveOne;
                row.RemoveAllRequested += HandleRemoveAll;

                _rows.Add(row);
            }

            return _rows[index];
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
