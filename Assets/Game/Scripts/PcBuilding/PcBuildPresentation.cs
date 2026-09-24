using UnityEngine;

namespace GoLive.PcBuilding
{
    // How this PC presents itself in PC Build Mode. The PC's gameplay root (its place in the world, its collider, its
    // record) never moves: only the Presentation Root under it - the case, the internals, the slots and whatever is
    // installed on them - is brought to the player's eye and taken back. The Build View Anchor inside it is where the
    // eye ends up relative to the PC, so looking through it in the Scene view previews the build view.
    // Every pose is a pure function of one amount, so any timeline can drive it and amount 0 is always exactly the
    // authored pose. Side panel offsets are in the presentation root's space: -X is out of the open side, +Z the rear.
    [DisallowMultipleComponent]
    public sealed class PcBuildPresentation : MonoBehaviour
    {
        // The PC starts to turn a moment after it has started to move, so it clears its neighbours on the desk first.
        private const float TurnDelay = 0.15f;
        private const float UnhookEnd = 0.2f;
        private const float PullEnd = 0.45f;

        [Header("Presentation")]
        [Tooltip("Everything that is brought to the player: case, internals, slots. Keep its scale uniform.")]
        [SerializeField] private Transform presentationRoot;
        [Tooltip("Where the player's eye ends up relative to the PC. Look through it in the Scene view to preview the build view.")]
        [SerializeField] private Transform viewAnchor;

        [Header("Side panel")]
        [SerializeField] private Transform sidePanel;
        [Tooltip("Slide toward the rear, off the hooks.")]
        [SerializeField] private Vector3 unhookOffset = new(0f, 0f, 0.02f);
        [Tooltip("Pull just clear of the case.")]
        [SerializeField] private Vector3 pullOffset = new(-0.035f, 0f, 0f);
        [Tooltip("How far the panel keeps coming out of the open side before it swings past the eye.")]
        [SerializeField, Min(0f)] private float outwardReach = 0.2f;
        [Tooltip("Where the panel swings past the eye, in the Build View Anchor's space: to the side, still in front.")]
        [SerializeField] private Vector3 passOffset = new(0.55f, 0f, 0.55f);
        [Tooltip("Where the panel leaves the view, in the Build View Anchor's space: beside and behind the eye.")]
        [SerializeField] private Vector3 exitOffset = new(0.45f, -0.05f, -0.3f);
        [Tooltip("How far the panel turns about the case's vertical axis on its way out, in degrees. Positive turns its inner edge away from the eye, so it never cuts through the near plane.")]
        [SerializeField, Range(-90f, 90f)] private float exitTurn = 45f;

        public Transform PresentationRoot => presentationRoot;
        public Transform ViewAnchor => viewAnchor;
        public Transform SidePanel => sidePanel;
        public Vector3 RestLocalPosition => _restPosition;
        public Quaternion RestLocalRotation => _restRotation;
        public Vector3 BuildLocalPosition => _buildPosition;
        public Quaternion BuildLocalRotation => _buildRotation;

        // The eye's aim once the PC has arrived: the player's own heading with the build view's downward look.
        public Quaternion BuildViewRotation { get; private set; }

        // Authored poses. The presentation root's in its parent; the panel's in its parent and, like the anchor, in the
        // presentation root's space (which does not change while the root moves).
        private Vector3 _restPosition;
        private Quaternion _restRotation;
        private Vector3 _panelLocalPosition;
        private Quaternion _panelLocalRotation;
        private Vector3 _panelPosition;
        private Quaternion _panelRotation;
        private Vector3 _anchorPosition;
        private Quaternion _anchorRotation;
        private bool _captured;

        private Vector3 _buildPosition;
        private Quaternion _buildRotation;
        private bool _planned;

        private void Awake()
        {
            if (!IsConfigured(out string error))
            {
                Debug.LogError($"{nameof(PcBuildPresentation)} on {name} {error}.", this);
                enabled = false;
                return;
            }

            CaptureRest();
        }

        public bool IsConfigured(out string error)
        {
            if (presentationRoot == null || presentationRoot == transform || !presentationRoot.IsChildOf(transform))
                error = "needs a Presentation Root inside the PC";
            else if (viewAnchor == null || !viewAnchor.IsChildOf(presentationRoot))
                error = "needs a Build View Anchor inside the Presentation Root";
            else if (sidePanel == null || sidePanel == presentationRoot || !sidePanel.IsChildOf(presentationRoot))
                error = "needs the removable Side Panel inside the Presentation Root";
            else
                error = null;

            return error == null;
        }

        // Where the PC comes to for this eye: the pose that puts the Build View Anchor exactly on the eye, turned to the
        // eye's heading. Called while the PC is in its place, when the mode starts.
        public void PlanApproach(Transform eye)
        {
            if (!CaptureRest())
                return;

            Transform parent = presentationRoot.parent;
            Quaternion restRotation = parent.rotation * _restRotation;
            float turn = Mathf.DeltaAngle(Heading(restRotation * _anchorRotation), Heading(eye.rotation));
            Quaternion rotation = Quaternion.AngleAxis(turn, Vector3.up) * restRotation;
            Vector3 position = eye.position - rotation * Vector3.Scale(presentationRoot.lossyScale, _anchorPosition);

            BuildViewRotation = rotation * _anchorRotation;
            _buildPosition = parent.InverseTransformPoint(position);
            _buildRotation = Quaternion.Inverse(parent.rotation) * rotation;
            _planned = true;
        }

        // 0 = in its place, 1 = in front of the eye given to PlanApproach.
        public void SetApproach(float amount)
        {
            if (!CaptureRest())
                return;

            if (amount <= 0f || !_planned)
            {
                presentationRoot.SetLocalPositionAndRotation(_restPosition, _restRotation);
                return;
            }

            if (amount >= 1f)
            {
                presentationRoot.SetLocalPositionAndRotation(_buildPosition, _buildRotation);
                return;
            }

            float move = Mathf.SmoothStep(0f, 1f, amount);
            float turn = Mathf.SmoothStep(0f, 1f, (amount - TurnDelay) / (1f - TurnDelay));
            presentationRoot.SetLocalPositionAndRotation(Vector3.Lerp(_restPosition, _buildPosition, move), Quaternion.Slerp(_restRotation, _buildRotation, turn));
        }

        // 0 = on the case, 1 = gone past the eye and hidden. Unhook and pull ease in and out; then the panel keeps
        // coming out of the open side, swings across the view and past the eye, speeding up and turning away as it
        // goes. It is hidden only at the end of that path, beside and behind the eye, out of the view.
        public void SetCoverOpen(float amount)
        {
            if (!CaptureRest())
                return;

            if (amount <= 0f)
            {
                sidePanel.SetLocalPositionAndRotation(_panelLocalPosition, _panelLocalRotation);
                sidePanel.gameObject.SetActive(true);
                return;
            }

            amount = Mathf.Min(amount, 1f);
            Vector3 pulled = unhookOffset + pullOffset;
            Vector3 offset;
            float turn = 0f;

            if (amount < UnhookEnd)
            {
                offset = unhookOffset * Mathf.SmoothStep(0f, 1f, amount / UnhookEnd);
            }
            else if (amount < PullEnd)
            {
                offset = unhookOffset + pullOffset * Mathf.SmoothStep(0f, 1f, (amount - UnhookEnd) / (PullEnd - UnhookEnd));
            }
            else
            {
                float way = Square((amount - PullEnd) / (1f - PullEnd));
                Vector3 pass = _anchorPosition + _anchorRotation * passOffset - _panelPosition;
                Vector3 exit = _anchorPosition + _anchorRotation * exitOffset - _panelPosition;
                offset = Bezier(pulled, pulled + Vector3.left * outwardReach, pass, exit, way);
                turn = exitTurn * way;
            }

            Vector3 position = presentationRoot.TransformPoint(_panelPosition + offset);
            Quaternion rotation = presentationRoot.rotation * Quaternion.AngleAxis(turn, Vector3.up) * _panelRotation;
            Transform parent = sidePanel.parent;

            sidePanel.SetLocalPositionAndRotation(parent.InverseTransformPoint(position), Quaternion.Inverse(parent.rotation) * rotation);
            sidePanel.gameObject.SetActive(amount < 1f);
        }

        // Exactly the authored closed pose, whatever state the presentation was left in.
        public void ResetToRest()
        {
            SetApproach(0f);
            SetCoverOpen(0f);
        }

        private bool CaptureRest()
        {
            if (_captured)
                return true;

            if (!IsConfigured(out _))
                return false;

            _restPosition = presentationRoot.localPosition;
            _restRotation = presentationRoot.localRotation;
            _panelLocalPosition = sidePanel.localPosition;
            _panelLocalRotation = sidePanel.localRotation;

            Quaternion toRoot = Quaternion.Inverse(presentationRoot.rotation);
            _panelPosition = presentationRoot.InverseTransformPoint(sidePanel.position);
            _panelRotation = toRoot * sidePanel.rotation;
            _anchorPosition = presentationRoot.InverseTransformPoint(viewAnchor.position);
            _anchorRotation = toRoot * viewAnchor.rotation;
            _captured = true;
            return true;
        }

        private static float Heading(Quaternion rotation)
        {
            Vector3 forward = rotation * Vector3.forward;
            return Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        }

        private static Vector3 Bezier(Vector3 start, Vector3 first, Vector3 second, Vector3 end, float t)
        {
            float u = 1f - t;
            return u * u * u * start + 3f * u * u * t * first + 3f * u * t * t * second + t * t * t * end;
        }

        private static float Square(float value)
        {
            return value * value;
        }

        // The panel's way out, from its authored place to the exit beside and behind the build view's eye.
        private void OnDrawGizmosSelected()
        {
            if (!IsConfigured(out _))
                return;

            Vector3 pass = viewAnchor.TransformPoint(passOffset);
            Vector3 exit = viewAnchor.TransformPoint(exitOffset);
            Gizmos.color = new Color(1f, 0.75f, 0.3f, 0.9f);
            Gizmos.DrawLine(sidePanel.position, pass);
            Gizmos.DrawLine(pass, exit);
            Gizmos.DrawWireSphere(pass, 0.03f);
            Gizmos.DrawWireSphere(exit, 0.05f);
        }
    }
}
