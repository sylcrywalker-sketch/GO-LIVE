using System;
using System.Collections.Generic;
using GoLive.PcBuilding;
using GoLive.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GoLive.Desktop
{
    [DisallowMultipleComponent]
    public sealed class PcSessionBehaviour : MonoBehaviour
    {
        private const float SeatTransitionSeconds = 0.35f;
        [SerializeField] private PcAssemblyBehaviour pc;
        [SerializeField] private PlayerController playerController;
        [SerializeField] private PlayerCarry playerCarry;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private Transform seatViewAnchor;
        [SerializeField] private Collider monitorCollider;
        [SerializeField] private InputActionReference monitorAction;
        [SerializeField] private InputActionReference focusAction;

        public PcSession Session { get; } = new();
        public bool IsSeated => Session.Usage != PcUsageState.Standing;
        public event Action<IReadOnlyList<PcDiagnostic>> PowerOnRejected;

        private PcAssembly _assembly;
        private InputAction _focusInput;
        private IDisposable _controlBlock;
        private bool _configured;
        private bool _started;
        private bool _ownsSeat;
        private bool _playerWasEnabled;
        private PlayerPoseSnapshot _worldPose;
        private Vector3 _cameraLocalPosition;
        private Quaternion _cameraLocalRotation;
        private float _cameraFieldOfView;
        private Vector3 _cameraEntryPosition;
        private Quaternion _cameraEntryRotation;
        private CursorLockMode _cursorLock;
        private bool _cursorVisible;
        private float _seatElapsed;
        private int _seatedFrame;

        private void Awake()
        {
            _configured = pc != null && playerController != null && playerCarry != null && playerCamera != null &&
                          seatViewAnchor != null && monitorCollider != null && HasAction(monitorAction) && HasAction(focusAction);
            if (_configured)
            {
                // The authored binding is shared configuration; the phone owns its action's enabled state.
                // A private clone gives this session an independent input lifetime.
                _focusInput = focusAction.action.Clone();
                return;
            }
            Debug.LogError($"{nameof(PcSessionBehaviour)} on {name} requires explicit PC, player, carry, camera, seat, monitor collider and input references.", this);
            enabled = false;
        }

        private void OnEnable()
        {
            if (!_configured)
                return;
            Session.Changed += HandleSessionChanged;
            // PlayerInteractor owns the shared monitor action. This component owns the focus clone.
            monitorAction.action.Enable();
            _focusInput.Enable();
            if (_started)
                BindAssembly();
        }

        private void Start()
        {
            _started = true;
            BindAssembly();
        }

        private void BindAssembly()
        {
            if (_assembly != null)
                return;
            if (pc.Assembly == null)
            {
                Debug.LogError($"{nameof(PcSessionBehaviour)} on {name} requires an initialized PC assembly.", this);
                enabled = false;
                return;
            }
            _assembly = pc.Assembly;
            _assembly.Changed += HandleHardwareChanged;
            HandleHardwareChanged();
        }

        private void OnDisable()
        {
            _focusInput?.Disable();
            if (_assembly != null)
                _assembly.Changed -= HandleHardwareChanged;
            _assembly = null;
            Reset();
            Session.Changed -= HandleSessionChanged;
        }

        private void OnDestroy()
        {
            _focusInput?.Dispose();
            _focusInput = null;
        }

        private void Update()
        {
            Session.Tick(Time.deltaTime);
            if (Session.Usage != PcUsageState.Seated || Time.frameCount == _seatedFrame)
                return;
            if (monitorAction.action.WasPressedThisFrame())
                ToggleMonitor();
            if (_focusInput.WasPressedThisFrame() && Session.ScreenActive)
            {
                Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
                if (Physics.Raycast(ray, out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Ignore) && hit.collider == monitorCollider)
                    TryFocus();
            }
        }

        private void LateUpdate()
        {
            if (!_ownsSeat || playerCamera == null || seatViewAnchor == null)
                return;
            _seatElapsed = Mathf.Min(SeatTransitionSeconds, _seatElapsed + Time.unscaledDeltaTime);
            float amount = Mathf.SmoothStep(0f, 1f, _seatElapsed / SeatTransitionSeconds);
            playerCamera.transform.SetPositionAndRotation(
                Vector3.Lerp(_cameraEntryPosition, seatViewAnchor.position, amount),
                Quaternion.Slerp(_cameraEntryRotation, seatViewAnchor.rotation, amount));
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && Session.Usage == PcUsageState.Focused)
                Session.Back();
        }

        public bool HandleBack() => isActiveAndEnabled && Session.Back();

        public void PrepareForBuild()
        {
            Session.PowerOff();
            while (Session.Back()) { }
        }

        public bool TryTogglePower()
        {
            if (!isActiveAndEnabled || !pc.IsReady || (!IsSeated && !playerController.Controls.IsAllowed(PlayerControlMask.Interaction)))
                return false;
            if (Session.Power != PcPowerState.Off)
            {
                Session.PowerOff();
                return true;
            }
            if (Session.TryPowerOn(pc.Capabilities))
                return true;
            // The full diagnostic route owns this feedback, including watt formatting and every blocker.
            PowerOnRejected?.Invoke(Session.LastPowerOnDiagnostics);
            return false;
        }

        public bool TrySit() => CanInteract(playerController != null ? playerController.gameObject : null) && Session.Sit();
        public bool TryFocus() => isActiveAndEnabled && Session.Focus();

        public void ToggleMonitor()
        {
            if (isActiveAndEnabled)
                Session.ToggleMonitor();
        }

        public void Reset()
        {
            Session.Reset();
            ReleaseSeat();
        }

        public PlayerPoseSnapshot CaptureWorldPose() => _ownsSeat ? _worldPose : playerController.CapturePose();

        public bool CanInteract(GameObject actor)
        {
            return isActiveAndEnabled && _configured && pc.IsReady && !IsSeated &&
                   actor == playerController.gameObject && playerController.Controls.IsAllowed(PlayerControlMask.Interaction);
        }

        private void HandleHardwareChanged() => Session.HardwareChanged(pc.Capabilities);

        private void HandleSessionChanged()
        {
            if (!IsSeated)
            {
                ReleaseSeat();
                return;
            }
            if (!_ownsSeat)
                CaptureSeat();
            Cursor.lockState = Session.Usage == PcUsageState.Focused ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = Session.Usage == PcUsageState.Focused;
        }

        private void CaptureSeat()
        {
            _ownsSeat = true;
            _worldPose = playerController.CapturePose();
            _cameraLocalPosition = playerCamera.transform.localPosition;
            _cameraLocalRotation = playerCamera.transform.localRotation;
            _cameraFieldOfView = playerCamera.fieldOfView;
            _cameraEntryPosition = playerCamera.transform.position;
            _cameraEntryRotation = playerCamera.transform.rotation;
            _cursorLock = Cursor.lockState;
            _cursorVisible = Cursor.visible;
            _playerWasEnabled = playerController.enabled;
            _seatElapsed = 0f;
            _seatedFrame = Time.frameCount;
            _controlBlock = playerController.Controls.Block(PlayerControlMask.All);
            // Movement blocks do not suspend PlayerController's gravity or eye-height updates.
            // Suspend that lifecycle while the seated camera owns the pose, retaining the original stance.
            playerController.enabled = false;
            playerCarry.SetHeldItemHidden(true);
        }

        private void ReleaseSeat()
        {
            if (!_ownsSeat)
                return;
            _ownsSeat = false;
            if (playerCamera != null)
            {
                playerCamera.transform.localPosition = _cameraLocalPosition;
                playerCamera.transform.localRotation = _cameraLocalRotation;
                playerCamera.fieldOfView = _cameraFieldOfView;
            }
            if (playerCarry != null)
                playerCarry.SetHeldItemHidden(false);
            _controlBlock?.Dispose();
            _controlBlock = null;
            if (playerController != null)
                playerController.enabled = _playerWasEnabled;
            Cursor.lockState = _cursorLock;
            Cursor.visible = _cursorVisible;
        }

        private static bool HasAction(InputActionReference reference) => reference != null && reference.action != null;
    }
}
