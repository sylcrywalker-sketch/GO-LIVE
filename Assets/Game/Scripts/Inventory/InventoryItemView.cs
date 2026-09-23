using System;
using GoLive.Items;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GoLive.Inventory
{
    // One item card: an Inventory slot or the held-item card. It shows an item and reports left-button drags
    // to its owner; it never changes item state itself.
    [DisallowMultipleComponent]
    public sealed class InventoryItemView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private CanvasGroup content;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text iconFallback;
        [SerializeField] private TMP_Text label;

        public string InstanceId { get; private set; }
        public bool IsEmpty => InstanceId == null;
        public Sprite Icon => icon.enabled ? icon.sprite : null;
        public string IconFallbackText => iconFallback.text;

        public event Action<InventoryItemView, PointerEventData> DragStarted;
        public event Action<InventoryItemView, PointerEventData> Dragged;
        public event Action<InventoryItemView, PointerEventData> DragEnded;

        private bool _dragging;

        private void Awake()
        {
            if (content != null && icon != null && iconFallback != null && label != null)
                return;

            Debug.LogError($"{nameof(InventoryItemView)} on {name} has incomplete configuration.", this);
            enabled = false;
        }

        private void OnDisable()
        {
            _dragging = false;
        }

        public void Show(string instanceId, ItemDefinition definition)
        {
            InstanceId = instanceId;

            Sprite sprite = definition.InventoryIcon;
            string displayName = DisplayName(definition.ItemId);

            icon.sprite = sprite;
            icon.enabled = sprite != null;
            iconFallback.text = sprite != null ? string.Empty : Initials(displayName);
            label.text = displayName;
            content.alpha = 1f;
        }

        public void Clear()
        {
            InstanceId = null;

            icon.sprite = null;
            icon.enabled = false;
            iconFallback.text = string.Empty;
            label.text = string.Empty;
            content.alpha = 1f;
        }

        public void SetLifted(bool lifted)
        {
            content.alpha = lifted ? 0.35f : 1f;
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

        public static string DisplayName(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return string.Empty;

            string value = itemId.Replace('_', ' ').Replace('-', ' ');

            return char.ToUpperInvariant(value[0]) + value[1..];
        }

        private static string Initials(string displayName)
        {
            string[] words = displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (words.Length == 0)
                return "?";

            return words.Length == 1
                ? words[0][..Math.Min(2, words[0].Length)].ToUpperInvariant()
                : $"{char.ToUpperInvariant(words[0][0])}{char.ToUpperInvariant(words[1][0])}";
        }
    }
}
