using System;
using System.Collections.Generic;
using GoLive.Player;
using UnityEngine;
using UnityEngine.UI;

namespace GoLive.Phone
{
    public enum PhonePresentationState
    {
        Hidden,
        Presenting,
        Held,
        Hiding
    }

    [DisallowMultipleComponent]
    public sealed class PhoneBehaviour : MonoBehaviour
    {
        [Serializable]
        private sealed class ScreenBinding
        {
            [field: SerializeField] public PhoneScreenId Id { get; private set; }
            [field: SerializeField] public GameObject Root { get; private set; }
        }

        [Serializable]
        private sealed class OpenScreenButtonBinding
        {
            [field: SerializeField] public Button Button { get; private set; }
            [field: SerializeField] public PhoneScreenId Target { get; private set; }

            private PhoneBehaviour _owner;

            public void Bind(PhoneBehaviour owner)
            {
                _owner = owner;
                Button.onClick.AddListener(HandleClick);
            }

            public void Unbind()
            {
                if (Button != null)
                    Button.onClick.RemoveListener(HandleClick);

                _owner = null;
            }

            private void HandleClick()
            {
                _owner?.TryOpenScreen(Target);
            }
        }

        [Header("Dependencies")]
        [SerializeField] private PlayerController playerController;
        [SerializeField] private PhonePointerBehaviour pointer;

        [Header("Rig")]
        [SerializeField] private Transform viewRoot;
        [SerializeField] private Transform hiddenPose;
        [SerializeField] private Transform heldPose;

        [Header("UI")]
        [SerializeField] private Canvas phoneCanvas;
        [SerializeField] private CanvasGroup phoneCanvasGroup;
        [SerializeField] private ScreenBinding[] screens = Array.Empty<ScreenBinding>();
        [SerializeField] private OpenScreenButtonBinding[] openScreenButtons = Array.Empty<OpenScreenButtonBinding>();
        [SerializeField] private Button[] backButtons = Array.Empty<Button>();

        [Header("Presentation")]
        [SerializeField, Min(0.01f)] private float presentDuration = 0.28f;
        [SerializeField, Min(0.01f)] private float hideDuration = 0.22f;

        [Header("Physical Follow")]
        [SerializeField, Min(0.001f)] private float followSmoothTime = 0.1f;
        [SerializeField] private Vector2 followPositionRange = new(0.016f, 0.02f);
        [SerializeField] private Vector3 followRotationRange = new(1.4f, 1.9f, 1.1f);

        public bool IsOpen => _session != null && _session.IsOpen;
        public bool IsInteractive => _presentationState == PhonePresentationState.Held;
        public PhoneScreenId CurrentScreen => _session?.CurrentScreen ?? PhoneScreenId.Home;
        public PhonePresentationState PresentationState => _presentationState;
        public float PresentationProgress => _presentationProgress;

        public event Action ScreenChanged;
        public event Func<bool> ScreenBackRequested;

        private readonly Dictionary<PhoneScreenId, GameObject> _screenRoots = new();

        private PhoneSession _session;
        private PhonePresentationState _presentationState = PhonePresentationState.Hidden;
        private IDisposable _controlBlock;

        private Vector2 _followCurrent;
        private Vector2 _followVelocity;
        private float _presentationProgress;

        private void Awake()
        {
            if (!ValidateConfiguration() || !BuildScreenMap() || !ValidateNavigation())
            {
                enabled = false;
                return;
            }

            _session = new PhoneSession();
            _session.Changed += HandleSessionChanged;

            BindButtons();

            SetUiInteractive(false);
            SetHiddenPose();
            RefreshScreens();

            viewRoot.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!IsOpen)
                return;

            UpdatePhysicalFollow();
            UpdatePresentation();

            if (IsOpen && viewRoot.gameObject.activeSelf)
                ApplyPose();
        }

        private void OnDisable()
        {
            ForceCloseImmediate();
        }

        private void OnDestroy()
        {
            UnbindButtons();

            if (_session != null)
                _session.Changed -= HandleSessionChanged;
        }

        public bool Open()
        {
            if (!isActiveAndEnabled ||
                _presentationState != PhonePresentationState.Hidden ||
                !_session.Open())
            {
                return false;
            }

            _controlBlock = playerController.Controls.Block(PlayerControlMask.All);

            _followCurrent = Vector2.zero;
            _followVelocity = Vector2.zero;
            _presentationProgress = 0f;

            pointer.ResetToCenter();
            pointer.SetInteractionEnabled(false);

            viewRoot.gameObject.SetActive(true);

            SetUiInteractive(false);
            SetHiddenPose();

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            _presentationState = PhonePresentationState.Presenting;
            return true;
        }

        public bool RequestClose()
        {
            if (!IsOpen ||
                _presentationState == PhonePresentationState.Hidden ||
                _presentationState == PhonePresentationState.Hiding)
            {
                return false;
            }

            SetUiInteractive(false);
            _presentationState = PhonePresentationState.Hiding;

            return true;
        }

        public bool HandleBack()
        {
            if (!IsOpen)
                return false;

            if (_presentationState == PhonePresentationState.Held && TryHandleScreenBack())
                return true;

            if (_presentationState == PhonePresentationState.Held && _session.TryBack())
                return true;

            return RequestClose();
        }

        public bool TryOpenScreen(PhoneScreenId screen)
        {
            if (_presentationState != PhonePresentationState.Held ||
                !_screenRoots.ContainsKey(screen))
            {
                return false;
            }

            return _session.TryNavigate(screen);
        }

        private void UpdatePhysicalFollow()
        {
            Vector2 target = _presentationState == PhonePresentationState.Held
                ? pointer.NormalizedPosition
                : Vector2.zero;

            _followCurrent = Vector2.SmoothDamp(
                _followCurrent,
                target,
                ref _followVelocity,
                followSmoothTime,
                Mathf.Infinity,
                Time.unscaledDeltaTime);
        }

        private void UpdatePresentation()
        {
            if (_presentationState == PhonePresentationState.Presenting)
            {
                _presentationProgress = Mathf.MoveTowards(
                    _presentationProgress,
                    1f,
                    Time.unscaledDeltaTime / presentDuration);

                if (_presentationProgress < 1f)
                    return;

                _presentationState = PhonePresentationState.Held;
                SetUiInteractive(true);

                return;
            }

            if (_presentationState != PhonePresentationState.Hiding)
                return;

            _presentationProgress = Mathf.MoveTowards(
                _presentationProgress,
                0f,
                Time.unscaledDeltaTime / hideDuration);

            if (_presentationProgress > 0f)
                return;

            CompleteClose();
        }

        private void ApplyPose()
        {
            float presentation = Mathf.SmoothStep(0f, 1f, _presentationProgress);

            Vector3 followOffset = new(
                _followCurrent.x * followPositionRange.x,
                _followCurrent.y * followPositionRange.y,
                0f);

            Quaternion followRotation = Quaternion.Euler(
                -_followCurrent.y * followRotationRange.x,
                _followCurrent.x * followRotationRange.y,
                -_followCurrent.x * followRotationRange.z);

            Vector3 targetPosition = heldPose.localPosition + followOffset;
            Quaternion targetRotation = heldPose.localRotation * followRotation;

            viewRoot.localPosition = Vector3.Lerp(
                hiddenPose.localPosition,
                targetPosition,
                presentation);

            viewRoot.localRotation = Quaternion.Slerp(
                hiddenPose.localRotation,
                targetRotation,
                presentation);
        }

        private void CompleteClose()
        {
            SetUiInteractive(false);
            SetHiddenPose();

            viewRoot.gameObject.SetActive(false);

            ReleasePlayerControl();

            _followCurrent = Vector2.zero;
            _followVelocity = Vector2.zero;
            _presentationProgress = 0f;
            _presentationState = PhonePresentationState.Hidden;

            _session.Close();

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void ForceCloseImmediate()
        {
            SetUiInteractive(false);
            ReleasePlayerControl();

            _followCurrent = Vector2.zero;
            _followVelocity = Vector2.zero;
            _presentationProgress = 0f;
            _presentationState = PhonePresentationState.Hidden;

            if (viewRoot != null && hiddenPose != null)
            {
                SetHiddenPose();
                viewRoot.gameObject.SetActive(false);
            }

            if (_session != null && _session.IsOpen)
                _session.Close();
        }

        private void HandleSessionChanged()
        {
            RefreshScreens();

            if (pointer != null)
                pointer.ResetToCenter();

            ScreenChanged?.Invoke();
        }

        private void RefreshScreens()
        {
            if (_session == null)
                return;

            foreach (KeyValuePair<PhoneScreenId, GameObject> pair in _screenRoots)
                pair.Value.SetActive(pair.Key == _session.CurrentScreen);
        }

        private void BindButtons()
        {
            for (int i = 0; i < openScreenButtons.Length; i++)
                openScreenButtons[i].Bind(this);

            for (int i = 0; i < backButtons.Length; i++)
                backButtons[i].onClick.AddListener(HandleBackButton);
        }

        private void UnbindButtons()
        {
            for (int i = 0; i < openScreenButtons.Length; i++)
                openScreenButtons[i]?.Unbind();

            for (int i = 0; i < backButtons.Length; i++)
            {
                if (backButtons[i] != null)
                    backButtons[i].onClick.RemoveListener(HandleBackButton);
            }
        }

        private void HandleBackButton()
        {
            if (_presentationState == PhonePresentationState.Held)
                HandleBack();
        }

        private void SetUiInteractive(bool interactive)
        {
            if (phoneCanvasGroup != null)
            {
                phoneCanvasGroup.interactable = interactive;
                phoneCanvasGroup.blocksRaycasts = interactive;
            }

            if (pointer != null)
                pointer.SetInteractionEnabled(interactive);
        }

        private void SetHiddenPose()
        {
            viewRoot.localPosition = hiddenPose.localPosition;
            viewRoot.localRotation = hiddenPose.localRotation;
        }

        private void ReleasePlayerControl()
        {
            _controlBlock?.Dispose();
            _controlBlock = null;
        }

        private bool TryHandleScreenBack()
        {
            if (ScreenBackRequested == null)
                return false;

            Delegate[] handlers = ScreenBackRequested.GetInvocationList();

            for (int i = 0; i < handlers.Length; i++)
            {
                if (handlers[i] is Func<bool> handler && handler())
                    return true;
            }

            return false;
        }

        private bool BuildScreenMap()
        {
            _screenRoots.Clear();

            for (int i = 0; i < screens.Length; i++)
            {
                ScreenBinding binding = screens[i];

                if (binding == null ||
                    binding.Root == null ||
                    !_screenRoots.TryAdd(binding.Id, binding.Root))
                {
                    Debug.LogError($"{nameof(PhoneBehaviour)} on {name} contains an invalid or duplicate screen binding.", this);
                    return false;
                }
            }

            if (_screenRoots.ContainsKey(PhoneScreenId.Home))
                return true;

            Debug.LogError($"{nameof(PhoneBehaviour)} on {name} requires a Home screen.", this);
            return false;
        }

        private bool ValidateNavigation()
        {
            for (int i = 0; i < openScreenButtons.Length; i++)
            {
                OpenScreenButtonBinding binding = openScreenButtons[i];

                if (binding == null ||
                    binding.Button == null ||
                    !_screenRoots.ContainsKey(binding.Target))
                {
                    Debug.LogError($"{nameof(PhoneBehaviour)} on {name} contains an invalid navigation button.", this);
                    return false;
                }
            }

            for (int i = 0; i < backButtons.Length; i++)
            {
                if (backButtons[i] != null)
                    continue;

                Debug.LogError($"{nameof(PhoneBehaviour)} on {name} contains an empty Back button reference.", this);
                return false;
            }

            return true;
        }

        private bool ValidateConfiguration()
        {
            if (playerController == null ||
                pointer == null ||
                viewRoot == null ||
                hiddenPose == null ||
                heldPose == null ||
                phoneCanvas == null ||
                phoneCanvasGroup == null)
            {
                Debug.LogError($"{nameof(PhoneBehaviour)} on {name} has incomplete configuration.", this);
                return false;
            }

            if (viewRoot.parent != hiddenPose.parent ||
                viewRoot.parent != heldPose.parent)
            {
                Debug.LogError($"{nameof(PhoneBehaviour)} requires View Root, Hidden Pose and Held Pose to share the same parent.", this);
                return false;
            }

            if (phoneCanvas.renderMode != RenderMode.WorldSpace)
            {
                Debug.LogError($"{nameof(PhoneBehaviour)} requires a World Space phone Canvas.", phoneCanvas);
                return false;
            }

            if (phoneCanvas.GetComponent<GraphicRaycaster>() == null)
            {
                Debug.LogError($"{nameof(PhoneBehaviour)} requires a GraphicRaycaster on the phone Canvas.", phoneCanvas);
                return false;
            }

            if (followPositionRange.x < 0f ||
                followPositionRange.y < 0f ||
                followRotationRange.x < 0f ||
                followRotationRange.y < 0f ||
                followRotationRange.z < 0f)
            {
                Debug.LogError($"{nameof(PhoneBehaviour)} on {name} contains invalid physical follow settings.", this);
                return false;
            }

            return true;
        }
    }
}