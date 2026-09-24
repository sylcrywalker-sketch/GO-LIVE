using System;
using UnityEngine;

namespace GoLive.PcBuilding
{
    public enum PcSlotHighlight
    {
        None,

        // A faint neutral outline: an empty place where a part can go.
        Empty,

        // Green: the held part fits here.
        Compatible,
        Targeted
    }

    // One place in a PC where a component goes, authored in the PC prefab. Presentation only: where the installed
    // part sits (InstallAnchor, also the ghost pose), where the pointer can pick the slot, its highlight and the case
    // parts the component replaces. Which item fills it is the PcAssembly's record, never this object's.
    [DisallowMultipleComponent]
    public sealed class PcComponentSlot : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private string slotId;
        [SerializeField] private PcComponentType componentType;
        [SerializeField] private PcConnector connector;
        [SerializeField] private string nameLocalizationKey;

        [Tooltip("Industry name shown under the friendly slot name, e.g. PCIe x16. Not translated.")]
        [SerializeField] private string technicalLabel;

        [Tooltip("The installed component's root sits exactly here; the install ghost uses the same pose.")]
        [SerializeField] private Transform installAnchor;

        [Tooltip("Where the pointer picks this slot, in this transform's local space.")]
        [SerializeField] private Bounds targetBounds = new(Vector3.zero, Vector3.one * 0.1f);

        [SerializeField] private Renderer highlight;
        [SerializeField] private Color emptyColor = new(0.85f, 0.9f, 1f, 0.05f);
        [SerializeField] private Color compatibleColor = new(0.35f, 0.85f, 0.5f, 0.1f);
        [SerializeField] private Color targetedColor = new(0.35f, 0.85f, 0.5f, 0.15f);

        [Tooltip("Case parts the installed component replaces (e.g. expansion slot covers behind a graphics card bracket).")]
        [SerializeField] private GameObject[] hiddenWhileFilled = Array.Empty<GameObject>();

        [Tooltip("The installed part can't be taken out for now. The motherboard: the processor and memory slots sit on it and do not move with it yet.")]
        [SerializeField] private bool fixedInPlace;

        public string SlotId => slotId;
        public PcComponentType ComponentType => componentType;
        public PcConnector Connector => connector;
        public string NameLocalizationKey => nameLocalizationKey;
        public string TechnicalLabel => technicalLabel;
        public Transform InstallAnchor => installAnchor;
        public Bounds TargetBounds => targetBounds;
        public bool IsFixed => fixedInPlace;
        public PcSlotSpec Spec => new(slotId, componentType, connector);

        private MaterialPropertyBlock _highlightBlock;
        private bool _occupied;
        private bool _previewing;

        private void Awake()
        {
            SetHighlight(PcSlotHighlight.None);
        }

        public bool IsConfigured(out string error)
        {
            if (!PcAssembly.IsValidSlotId(slotId))
                error = "needs a lowercase Slot ID";
            else if (connector == PcConnector.None)
                error = "needs a connector";
            else if (string.IsNullOrWhiteSpace(nameLocalizationKey))
                error = "needs a name localization key";
            else if (installAnchor == null || !installAnchor.IsChildOf(transform))
                error = "needs an Install Anchor inside the slot";
            else if (targetBounds.size.x <= 0f || targetBounds.size.y <= 0f || targetBounds.size.z <= 0f)
                error = "needs a pointer target volume";
            else if (highlight == null)
                error = "needs a highlight renderer";
            else if (Array.IndexOf(hiddenWhileFilled, null) >= 0)
                error = "has an empty Hidden While Filled entry";
            else
                error = null;

            return error == null;
        }

        // Distance along the world-space ray to this slot's pointer volume.
        public bool Raycast(Ray ray, out float distance)
        {
            distance = 0f;

            Ray local = new(transform.InverseTransformPoint(ray.origin), transform.InverseTransformDirection(ray.direction));

            if (!targetBounds.IntersectRay(local, out float localDistance))
                return false;

            distance = Vector3.Distance(ray.origin, transform.TransformPoint(local.GetPoint(localDistance)));
            return true;
        }

        public void SetHighlight(PcSlotHighlight state)
        {
            if (highlight == null)
                return;

            highlight.enabled = state != PcSlotHighlight.None;

            if (state == PcSlotHighlight.None)
                return;

            _highlightBlock ??= new MaterialPropertyBlock();
            _highlightBlock.SetColor(BaseColorId, state switch
            {
                PcSlotHighlight.Targeted => targetedColor,
                PcSlotHighlight.Compatible => compatibleColor,
                _ => emptyColor
            });
            highlight.SetPropertyBlock(_highlightBlock);
        }

        // The assembly's truth: a component is installed here.
        internal void SetOccupied(bool occupied)
        {
            _occupied = occupied;
            ApplyFilledLook();
        }

        // The Workbench shows the install ghost here.
        public void SetPreview(bool previewing)
        {
            _previewing = previewing;
            ApplyFilledLook();
        }

        private void ApplyFilledLook()
        {
            bool filled = _occupied || _previewing;

            for (int i = 0; i < hiddenWhileFilled.Length; i++)
            {
                if (hiddenWhileFilled[i] != null)
                    hiddenWhileFilled[i].SetActive(!filled);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.35f, 0.85f, 0.5f, 0.8f);
            Gizmos.DrawWireCube(targetBounds.center, targetBounds.size);
        }
    }
}
