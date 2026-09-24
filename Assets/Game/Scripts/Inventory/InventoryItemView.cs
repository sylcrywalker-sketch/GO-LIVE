using System;
using GoLive.Items;
using GoLive.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GoLive.Inventory
{
    // One item card: an Inventory row or the held-item row. It shows an item from its definition data and
    // reports left-button drags and clicks to its owner; it never changes item state itself.
    [DisallowMultipleComponent]
    public sealed class InventoryItemView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [SerializeField] private Graphic background;
        [SerializeField] private CanvasGroup content;
        [SerializeField] private Image icon;
        [SerializeField] private GameObject iconFallback;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text detail;
        [SerializeField] private GameObject emptyState;
        [SerializeField] private Color normalColor = new(1f, 1f, 1f, 0.05f);
        [SerializeField] private Color hoverColor = new(1f, 1f, 1f, 0.1f);

        public string InstanceId { get; private set; }
        public bool IsEmpty => InstanceId == null;
        public Sprite Icon => icon.enabled ? icon.sprite : null;
        public string Title => title.text;

        public event Action<InventoryItemView, PointerEventData> DragStarted;
        public event Action<InventoryItemView, PointerEventData> Dragged;
        public event Action<InventoryItemView, PointerEventData> DragEnded;
        public event Action<InventoryItemView> Clicked;

        private bool _dragging;
        private bool _hovered;
        private Color? _detailColor; // the authored category-line colour, remembered before a note recolours it

        private void Awake()
        {
            if (background != null && content != null && icon != null && iconFallback != null && title != null && detail != null)
                return;

            Debug.LogError($"{nameof(InventoryItemView)} on {name} has incomplete configuration.", this);
            enabled = false;
        }

        private void OnDisable()
        {
            _dragging = false;
            _hovered = false;

            if (background != null)
                background.color = normalColor;
        }

        public void Show(string instanceId, ItemDefinition definition, LocalizationContext localization)
        {
            InstanceId = instanceId;

            Sprite sprite = definition.InventoryIcon;

            icon.sprite = sprite;
            icon.enabled = sprite != null;
            iconFallback.SetActive(sprite == null);
            title.text = ItemName(definition, localization);
            detail.text = localization.Text(CategoryKey(definition.Category));
            _detailColor ??= detail.color;
            detail.color = _detailColor.Value;

            SetFilled(true);
        }

        public void Clear()
        {
            InstanceId = null;

            icon.sprite = null;
            icon.enabled = false;
            iconFallback.SetActive(false);
            title.text = string.Empty;
            detail.text = string.Empty;

            SetFilled(false);
        }

        public void SetLifted(bool lifted)
        {
            content.alpha = lifted ? 0.35f : 1f;
        }

        // A screen may replace the category line with its own note about the item (e.g. whether a part fits a PC)
        // and fade items that do not matter there. Show resets both.
        public void SetNote(string text, Color color)
        {
            _detailColor ??= detail.color;
            detail.text = text;
            detail.color = color;
        }

        public void SetDimmed(bool dimmed)
        {
            content.alpha = dimmed ? 0.45f : 1f;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (isActiveAndEnabled && !IsEmpty && eventData.button == PointerEventData.InputButton.Left && !eventData.dragging)
                Clicked?.Invoke(this);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!isActiveAndEnabled || IsEmpty || eventData.button != PointerEventData.InputButton.Left)
                return;

            _dragging = true;
            DragStarted?.Invoke(this, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_dragging)
                Dragged?.Invoke(this, eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging)
                return;

            _dragging = false;
            DragEnded?.Invoke(this, eventData);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            ApplyBackground();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            ApplyBackground();
        }

        private void SetFilled(bool filled)
        {
            content.alpha = 1f;
            content.gameObject.SetActive(filled);

            if (emptyState != null)
                emptyState.SetActive(!filled);

            ApplyBackground();
        }

        private void ApplyBackground()
        {
            background.color = _hovered && !IsEmpty ? hoverColor : normalColor;
        }

        private static string ItemName(ItemDefinition definition, LocalizationContext localization)
        {
            if (!string.IsNullOrWhiteSpace(definition.NameLocalizationKey))
                return localization.Text(definition.NameLocalizationKey);

            string value = definition.ItemId.Replace('_', ' ').Replace('-', ' ');
            return char.ToUpperInvariant(value[0]) + value[1..];
        }

        private static string CategoryKey(ItemCategory category)
        {
            return category switch
            {
                ItemCategory.Food => "inventory.food",
                ItemCategory.Electronics => "inventory.electronics",
                _ => "inventory.household"
            };
        }
    }
}
