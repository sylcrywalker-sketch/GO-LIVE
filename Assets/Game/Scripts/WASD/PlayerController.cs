using UnityEngine;
using UnityEngine.InputSystem;

namespace GoLive.Player
{
    public readonly struct PlayerPoseSnapshot
    {
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public float Pitch { get; }

        public PlayerPoseSnapshot(Vector3 position, Quaternion rotation, float pitch)
        {
            Position = position;
            Rotation = rotation;
            Pitch = pitch;
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        private const int MaxClearanceHits = 8;

        [Header("View")]
        [SerializeField] private Transform lookPivot;

        [Header("Input")]
        [SerializeField] private InputActionReference moveAction;
        [SerializeField] private InputActionReference lookAction;
        [SerializeField] private InputActionReference jumpAction;
        [SerializeField] private InputActionReference crouchAction;

        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float moveSpeed = 4.5f;
        [SerializeField, Min(0f)] private float jumpHeight = 1.1f;
        [SerializeField, Min(0.1f)] private float gravity = 25f;

        [Header("Crouch")]
        [SerializeField, Min(0.1f)] private float crouchHeight = 1.3f;
        [SerializeField, Min(0.1f)] private float crouchMoveSpeed = 2.4f;
        [SerializeField, Min(0.01f)] private float crouchTransitionTime = 0.15f;

        [Header("Look")]
        [SerializeField, Min(0.001f)] private float lookSensitivity = 0.08f;
        [SerializeField, Range(1f, 89f)] private float maxLookAngle = 85f;

        public PlayerControlState Controls { get; } = new();
        public bool IsCrouching => _crouching;

        private readonly Collider[] _clearanceHits = new Collider[MaxClearanceHits];

        private CharacterController _characterController;
        private Quaternion _lookPivotBaseRotation;
        private float _verticalVelocity;
        private float _pitch;

        // The authored CharacterController and look pivot describe the standing player; crouching is derived from them
        // so repeated crouch/stand cycles always return to exactly the same shape.
        private float _standingHeight;
        private Vector3 _standingCenter;
        private float _standingEyeHeight;
        private int _collisionMask;
        private bool _crouching;

        private float TargetEyeHeight => _crouching ? _standingEyeHeight - (_standingHeight - crouchHeight) : _standingEyeHeight;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();

            if (!ValidateConfiguration())
            {
                enabled = false;
                return;
            }

            _lookPivotBaseRotation = lookPivot.localRotation;
            CaptureStandingShape();
        }

        private void OnEnable()
        {
            SetActionEnabled(moveAction, true);
            SetActionEnabled(lookAction, true);
            SetActionEnabled(jumpAction, true);
            SetActionEnabled(crouchAction, true);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnDisable()
        {
            SetActionEnabled(moveAction, false);
            SetActionEnabled(lookAction, false);
            SetActionEnabled(jumpAction, false);
            SetActionEnabled(crouchAction, false);
        }

        private void Update()
        {
            UpdateLook();
            UpdateStance();
            UpdateMovement();
            UpdateEyeHeight();
        }

        public PlayerPoseSnapshot CapturePose()
        {
            return new PlayerPoseSnapshot(transform.position, transform.rotation, _pitch);
        }

        public void RestorePose(PlayerPoseSnapshot pose)
        {
            bool controllerWasEnabled = _characterController.enabled;

            _characterController.enabled = false;

            transform.SetPositionAndRotation(pose.Position, pose.Rotation);

            _pitch = Mathf.Clamp(pose.Pitch, -maxLookAngle, maxLookAngle);
            lookPivot.localRotation = _lookPivotBaseRotation * Quaternion.Euler(_pitch, 0f, 0f);
            _verticalVelocity = 0f;

            // The stance is not saved: stand up where there is room, otherwise arrive crouched instead of inside the ceiling.
            SetCrouching(!HasStandingClearance());
            SetEyeHeight(TargetEyeHeight);

            _characterController.enabled = controllerWasEnabled;
        }

        private void CaptureStandingShape()
        {
            _standingHeight = _characterController.height;
            _standingCenter = _characterController.center;
            _standingEyeHeight = lookPivot.localPosition.y;
            _collisionMask = BuildCollisionMask();
        }

        private void UpdateLook()
        {
            if (!Controls.IsAllowed(PlayerControlMask.Look))
                return;

            Vector2 input = lookAction.action.ReadValue<Vector2>();

            transform.Rotate(0f, input.x * lookSensitivity, 0f);

            _pitch = Mathf.Clamp(_pitch - input.y * lookSensitivity, -maxLookAngle, maxLookAngle);
            lookPivot.localRotation = _lookPivotBaseRotation * Quaternion.Euler(_pitch, 0f, 0f);
        }

        // Hold to crouch. Whoever blocks movement or crouching (Phone, Pause, a seated PC session) freezes the
        // stance until the block is released; standing up waits until the full standing capsule has room.
        private void UpdateStance()
        {
            if (!Controls.IsAllowed(PlayerControlMask.Movement | PlayerControlMask.Crouch))
                return;

            bool wantsCrouch = crouchAction.action.IsPressed();

            if (wantsCrouch != _crouching && (wantsCrouch || HasStandingClearance()))
                SetCrouching(wantsCrouch);
        }

        private void UpdateMovement()
        {
            bool grounded = _characterController.isGrounded;

            if (grounded && _verticalVelocity < 0f)
                _verticalVelocity = -2f;

            Vector2 input = Controls.IsAllowed(PlayerControlMask.Movement) ? moveAction.action.ReadValue<Vector2>() : Vector2.zero;
            input = Vector2.ClampMagnitude(input, 1f);

            if (grounded && Controls.IsAllowed(PlayerControlMask.Jump) && jumpAction.action.WasPressedThisFrame())
                _verticalVelocity = Mathf.Sqrt(jumpHeight * 2f * gravity);

            _verticalVelocity -= gravity * Time.deltaTime;

            Vector3 velocity = (transform.right * input.x + transform.forward * input.y) * (_crouching ? crouchMoveSpeed : moveSpeed);
            velocity.y = _verticalVelocity;

            _characterController.Move(velocity * Time.deltaTime);
        }

        private void UpdateEyeHeight()
        {
            float step = (_standingHeight - crouchHeight) / crouchTransitionTime * Time.deltaTime;
            SetEyeHeight(Mathf.MoveTowards(lookPivot.localPosition.y, TargetEyeHeight, step));
        }

        // The capsule keeps its feet where they are: only its top moves.
        private void SetCrouching(bool crouching)
        {
            _crouching = crouching;

            float height = crouching ? crouchHeight : _standingHeight;

            _characterController.height = height;
            _characterController.center = _standingCenter + Vector3.down * ((_standingHeight - height) * 0.5f);
        }

        private void SetEyeHeight(float height)
        {
            Vector3 position = lookPivot.localPosition;
            position.y = height;
            lookPivot.localPosition = position;
        }

        // Tests only the band the capsule grows into when standing up, ignoring the player's own colliders.
        private bool HasStandingClearance()
        {
            float radius = _characterController.radius;
            float bottom = _standingCenter.y - _standingHeight * 0.5f;
            Vector3 axis = new(_standingCenter.x, 0f, _standingCenter.z);

            Vector3 from = transform.TransformPoint(axis + Vector3.up * (bottom + crouchHeight - radius));
            Vector3 to = transform.TransformPoint(axis + Vector3.up * (bottom + _standingHeight - radius + _characterController.skinWidth));

            int count = Physics.OverlapCapsuleNonAlloc(from, to, radius, _clearanceHits, _collisionMask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                if (!_clearanceHits[i].transform.IsChildOf(transform))
                    return false;
            }

            return true;
        }

        private int BuildCollisionMask()
        {
            int mask = 0;

            for (int layer = 0; layer < 32; layer++)
            {
                if (!Physics.GetIgnoreLayerCollision(gameObject.layer, layer))
                    mask |= 1 << layer;
            }

            return (mask | _characterController.includeLayers) & ~_characterController.excludeLayers;
        }

        private bool ValidateConfiguration()
        {
            if (lookPivot == null)
            {
                Debug.LogError($"{nameof(PlayerController)} on {name} requires a Look Pivot.", this);
                return false;
            }

            if (!HasAction(moveAction) || !HasAction(lookAction) || !HasAction(jumpAction) || !HasAction(crouchAction))
            {
                Debug.LogError($"{nameof(PlayerController)} on {name} requires Move, Look, Jump and Crouch input actions.", this);
                return false;
            }

            if (crouchHeight >= _characterController.height || crouchHeight < _characterController.radius * 2f)
            {
                Debug.LogError($"{nameof(PlayerController)} on {name} needs a Crouch Height below the standing height and at least the capsule diameter.", this);
                return false;
            }

            return true;
        }

        private static bool HasAction(InputActionReference reference)
        {
            return reference != null && reference.action != null;
        }

        private static void SetActionEnabled(InputActionReference reference, bool enabled)
        {
            if (!HasAction(reference))
                return;

            if (enabled)
                reference.action.Enable();
            else
                reference.action.Disable();
        }
    }
}
