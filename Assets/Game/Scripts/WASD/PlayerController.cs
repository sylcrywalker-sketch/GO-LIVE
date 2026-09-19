using UnityEngine;
using UnityEngine.InputSystem;

namespace GoLive.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        [Header("View")]
        [SerializeField] private Transform lookPivot;

        [Header("Input")]
        [SerializeField] private InputActionReference moveAction;
        [SerializeField] private InputActionReference lookAction;
        [SerializeField] private InputActionReference jumpAction;

        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float moveSpeed = 4.5f;
        [SerializeField, Min(0f)] private float jumpHeight = 1.1f;
        [SerializeField, Min(0.1f)] private float gravity = 25f;

        [Header("Look")]
        [SerializeField, Min(0.001f)] private float lookSensitivity = 0.08f;
        [SerializeField, Range(1f, 89f)] private float maxLookAngle = 85f;

        public PlayerControlState Controls { get; } = new();

        private CharacterController _characterController;
        private Quaternion _lookPivotBaseRotation;
        private float _verticalVelocity;
        private float _pitch;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();

            if (!ValidateConfiguration())
            {
                enabled = false;
                return;
            }

            _lookPivotBaseRotation = lookPivot.localRotation;
        }

        private void OnEnable()
        {
            SetActionEnabled(moveAction, true);
            SetActionEnabled(lookAction, true);
            SetActionEnabled(jumpAction, true);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnDisable()
        {
            SetActionEnabled(moveAction, false);
            SetActionEnabled(lookAction, false);
            SetActionEnabled(jumpAction, false);
        }

        private void Update()
        {
            UpdateLook();
            UpdateMovement();
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

            Vector3 velocity = (transform.right * input.x + transform.forward * input.y) * moveSpeed;
            velocity.y = _verticalVelocity;

            _characterController.Move(velocity * Time.deltaTime);
        }

        private bool ValidateConfiguration()
        {
            if (lookPivot == null)
            {
                Debug.LogError($"{nameof(PlayerController)} on {name} requires a Look Pivot.", this);
                return false;
            }

            if (!HasAction(moveAction) || !HasAction(lookAction) || !HasAction(jumpAction))
            {
                Debug.LogError($"{nameof(PlayerController)} on {name} requires Move, Look and Jump input actions.", this);
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