using UnityEngine;

namespace GoLive.PcBuilding
{
    // Projects the existing selection into the build UI. Picking and compatibility remain in the workbench/assembly.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(50)]
    public sealed class PcBuildSlotMarkerView : MonoBehaviour
    {
        [SerializeField] private PcWorkbenchBehaviour workbench;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Canvas canvas;
        [SerializeField] private RectTransform placementRoot;
        [SerializeField] private RectTransform placementArea;
        [SerializeField] private RectTransform card;
        [SerializeField] private RectTransform targetMarker;

        private readonly Vector3[] _corners = new Vector3[4];
        private Vector2 _restPosition;

        private void Awake()
        {
            if (workbench == null || worldCamera == null || canvas == null || placementRoot == null || placementArea == null || card == null || targetMarker == null)
            {
                Debug.LogError($"{nameof(PcBuildSlotMarkerView)} on {name} requires explicit workbench, camera, canvas and card references.", this);
                enabled = false;
                return;
            }
            _restPosition = card.anchoredPosition;
            targetMarker.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            PcComponentSlot slot = workbench.TargetSlot;
            if (!workbench.IsInteractive || slot == null)
            {
                Restore();
                return;
            }

            Camera projection = canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera != null ? canvas.worldCamera : worldCamera;
            Vector3 world = slot.transform.TransformPoint(slot.TargetBounds.center);
            if (projection.WorldToViewportPoint(world).z <= 0f)
            {
                Restore();
                return;
            }

            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(projection, world);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(placementRoot, screen, uiCamera, out Vector2 target))
                return;

            placementArea.GetWorldCorners(_corners);
            Vector3 min = placementRoot.InverseTransformPoint(_corners[0]);
            Vector3 max = placementRoot.InverseTransformPoint(_corners[2]);
            float width = card.rect.width;
            float height = card.rect.height;
            const float gap = 22f;
            Bounds bounds = slot.TargetBounds;
            float slotLeft = target.x;
            float slotRight = target.x;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                Vector2 projected = RectTransformUtility.WorldToScreenPoint(projection, slot.transform.TransformPoint(corner));
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(placementRoot, projected, uiCamera, out Vector2 edge)) continue;
                slotLeft = Mathf.Min(slotLeft, edge.x);
                slotRight = Mathf.Max(slotRight, edge.x);
            }

            // Clear the whole physical slot, not only its centre; wide graphics cards keep their ghost visible.
            Vector2 position = new(slotRight + gap, target.y - height * 0.5f);
            if (position.x + width > max.x)
                position.x = slotLeft - width - gap;
            position.x = Mathf.Clamp(position.x, min.x, Mathf.Max(min.x, max.x - width));
            position.y = Mathf.Clamp(position.y, min.y, Mathf.Max(min.y, max.y - height));
            card.localPosition = new Vector3(position.x, position.y, card.localPosition.z);
            targetMarker.localPosition = new Vector3(target.x, target.y, 0f);
            targetMarker.gameObject.SetActive(true);
        }

        private void OnDisable() => Restore();

        private void Restore()
        {
            if (card != null) card.anchoredPosition = _restPosition;
            if (targetMarker != null) targetMarker.gameObject.SetActive(false);
        }
    }
}
