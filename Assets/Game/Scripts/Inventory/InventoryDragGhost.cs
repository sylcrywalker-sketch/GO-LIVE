using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GoLive.Inventory
{
    // Pointer-following preview of the item being dragged, with the drop hint under it. Presentation only:
    // it copies the look of the source card and never refers to item state. Authored inactive; Show activates it.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class InventoryDragGhost : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text iconFallback;
        [SerializeField] private TMP_Text hint;
        [SerializeField] private Color acceptedHintColor = new(0.72f, 0.95f, 0.78f, 1f);
        [SerializeField] private Color rejectedHintColor = new(1f, 0.62f, 0.55f, 1f);

        private RectTransform _rect;
        private RectTransform _parent;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _parent = transform.parent as RectTransform;

            if (icon != null && iconFallback != null && hint != null && _parent != null)
                return;

            Debug.LogError($"{nameof(InventoryDragGhost)} on {name} has incomplete configuration.", this);
            enabled = false;
        }

        public void Show(InventoryItemView source)
        {
            icon.sprite = source.Icon;
            icon.enabled = icon.sprite != null;
            iconFallback.text = source.IconFallbackText;
            SetHint(string.Empty, true);

            transform.SetAsLastSibling();
            gameObject.SetActive(true);
        }

        public void Follow(PointerEventData eventData)
        {
            if (enabled && RectTransformUtility.ScreenPointToLocalPointInRectangle(_parent, eventData.position, eventData.pressEventCamera, out Vector2 local))
                _rect.localPosition = local;
        }

        public void SetHint(string text, bool accepted)
        {
            hint.text = text;
            hint.color = accepted ? acceptedHintColor : rejectedHintColor;
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
