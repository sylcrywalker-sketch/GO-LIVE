using System.Collections.Generic;
using GoLive.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GoLive.Phone
{
    // The orders page: the paid orders, newest first, or an empty state.
    [DisallowMultipleComponent]
    public sealed class ShopOrdersView : MonoBehaviour
    {
        [SerializeField] private ScrollRect _list;
        [SerializeField] private ShopOrderRowView _rowTemplate;
        [SerializeField] private TMP_Text _empty;

        private readonly List<ShopOrderRowView> _rows = new();

        public bool IsConfigured =>
            _list != null &&
            _list.content != null &&
            _rowTemplate != null &&
            _rowTemplate.IsConfigured &&
            !_rowTemplate.gameObject.activeSelf &&
            _empty != null;

        private void OnEnable()
        {
            _list.StopMovement();
            _list.content.anchoredPosition = new Vector2(_list.content.anchoredPosition.x, 0f);
        }

        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible)
                gameObject.SetActive(visible);
        }

        public void Show(IReadOnlyList<ShopOrderRowData> rows, LocalizationContext localization)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                ShopOrderRowView row = GetRow(i);

                row.Show(rows[i]);

                if (!row.gameObject.activeSelf)
                    row.gameObject.SetActive(true);
            }

            for (int i = rows.Count; i < _rows.Count; i++)
                _rows[i].gameObject.SetActive(false);

            _empty.text = localization.Text(ShopText.OrdersEmptyKey);
            _empty.gameObject.SetActive(rows.Count == 0);
        }

        private ShopOrderRowView GetRow(int index)
        {
            while (_rows.Count <= index)
            {
                ShopOrderRowView row = Instantiate(_rowTemplate, _list.content);
                row.name = $"OrderRow{_rows.Count}";
                _rows.Add(row);
            }

            return _rows[index];
        }
    }
}
