using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GoLive.Inventory
{
    // Pointer-following card of the item being dragged, with the drop hint pill under it. Presentation only:
    // it copies the look of the source card and never refers to item state. Authored inactive; Show activates it.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class InventoryDragGhost : MonoBehaviour
    {
        public enum HintTone
        {
            Neutral,
            Accepted,
            Rejected
        }

        [SerializeField] private Image icon;
        [SerializeField] private GameObject iconFallback;
        [SerializeField] private TMP_Text title;
        [SerializeField] private Graphic hintBackground;
        [SerializeField] private TMP_Text hint;
        [SerializeField] private Color neutralHintColor = new(0.1f, 0.11f, 0.13f, 0.92f);
        [SerializeField] private Color acceptedHintColor = new(0.16f, 0.43f, 0.27f, 0.95f);
        [SerializeField] private Color rejectedHintColor = new(0.56f, 0.17f, 0.16f, 0.95f);

        private RectTransform _rect;
        private RectTransform _parent;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _parent = transform.parent as RectTransform;

            if (icon != null && iconFallback != null && title != null && hintBackground != null && hint != null && _parent != null)
                return;

            Debug.LogError($"{nameof(InventoryDragGhost)} on {name} has incomplete configuration.", this);
            enabled = false;
        }

        public void Show(InventoryItemView source)
        {
            icon.sprite = source.Icon;
            icon.enabled = icon.sprite != null;
            iconFallback.SetActive(icon.sprite == null);
            title.text = source.Title;
            SetHint(string.Empty, HintTone.Neutral);

            transform.SetAsLastSibling();
            gameObject.SetActive(true);
        }

        public void Follow(PointerEventData eventData)
        {
            if (enabled && RectTransformUtility.ScreenPointToLocalPointInRectangle(_parent, eventData.position, eventData.pressEventCamera, out Vector2 local))
                _rect.localPosition = local;
        }

        public void SetHint(string text, HintTone tone)
        {
            hint.text = text;
            hintBackground.gameObject.SetActive(!string.IsNullOrEmpty(text));
            hintBackground.color = tone switch
            {
                HintTone.Accepted => acceptedHintColor,
                HintTone.Rejected => rejectedHintColor,
                _ => neutralHintColor
            };
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
