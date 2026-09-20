using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace GoLive.Phone
{
    [DisallowMultipleComponent]
    public sealed class PhonePointerBehaviour : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private InputActionReference aimAction;
        [SerializeField] private InputActionReference submitAction;

        [Header("UI")]
        [SerializeField] private Canvas phoneCanvas;
        [SerializeField] private RectTransform cursorVisual;
        [SerializeField] private InputSystemUIInputModule uiInputModule;

        [Header("Pointer")]
        [SerializeField] private Vector2 sensitivity = new(0.0025f, 0.0025f);
        [SerializeField] private Vector2 edgePadding = new(55f, 75f);

        public Vector2 NormalizedPosition => _normalizedPosition;
        public bool IsInteractionEnabled => _interactionEnabled;

        private readonly List<RaycastResult> _raycastResults = new();

        private GraphicRaycaster _raycaster;
        private RectTransform _canvasRect;
        private EventSystem _eventSystem;
        private PointerEventData _pointerEvent;

        private Vector2 _normalizedPosition;
        private GameObject _hoverTarget;
        private GameObject _pressedTarget;
        private RaycastResult _currentRaycast;

        private bool _interactionEnabled;
        private bool _uiInputModuleWasEnabled;

        private void Awake()
        {
            if (!ValidateConfiguration())
            {
                enabled = false;
                return;
            }

            _canvasRect = (RectTransform)phoneCanvas.transform;
            _raycaster = phoneCanvas.GetComponent<GraphicRaycaster>();

            cursorVisual.anchorMin = new Vector2(0.5f, 0.5f);
            cursorVisual.anchorMax = new Vector2(0.5f, 0.5f);
            cursorVisual.pivot = new Vector2(0.5f, 0.5f);

            Graphic cursorGraphic = cursorVisual.GetComponent<Graphic>();
            cursorGraphic.raycastTarget = false;

            ResetToCenter();
            cursorVisual.gameObject.SetActive(false);
        }

        private void Start()
        {
            _eventSystem = EventSystem.current;

            if (_eventSystem == null)
            {
                Debug.LogError($"{nameof(PhonePointerBehaviour)} requires an active EventSystem.", this);
                enabled = false;
                return;
            }

            _pointerEvent = new PointerEventData(_eventSystem)
            {
                button = PointerEventData.InputButton.Left,
                pointerId = -1
            };
        }

        private void Update()
        {
            if (!_interactionEnabled || _pointerEvent == null)
                return;

            UpdatePosition();
            UpdateRaycast();

            if (submitAction.action.WasPressedThisFrame())
                Press();

            if (submitAction.action.WasReleasedThisFrame())
                Release();
        }

        private void OnDisable()
        {
            SetInteractionEnabled(false);
        }

        public void SetInteractionEnabled(bool value)
        {
            if (_interactionEnabled == value)
                return;

            _interactionEnabled = value;

            if (value)
            {
                _uiInputModuleWasEnabled = uiInputModule.enabled;
                uiInputModule.enabled = false;

                submitAction.action.Enable();

                cursorVisual.gameObject.SetActive(true);
                UpdateCursorVisual();

                if (_eventSystem != null)
                    _eventSystem.SetSelectedGameObject(null);

                return;
            }

            ClearPointerTargets();

            cursorVisual.gameObject.SetActive(false);

            if (submitAction != null && submitAction.action != null)
                submitAction.action.Disable();

            if (uiInputModule != null)
                uiInputModule.enabled = _uiInputModuleWasEnabled;
        }

        public void ResetToCenter()
        {
            _normalizedPosition = Vector2.zero;
            ClearPointerTargets();

            if (cursorVisual != null && _canvasRect != null)
                UpdateCursorVisual();
        }

        private void UpdatePosition()
        {
            Vector2 delta = aimAction.action.ReadValue<Vector2>();

            _normalizedPosition += Vector2.Scale(delta, sensitivity);
            _normalizedPosition.x = Mathf.Clamp(_normalizedPosition.x, -1f, 1f);
            _normalizedPosition.y = Mathf.Clamp(_normalizedPosition.y, -1f, 1f);

            UpdateCursorVisual();
        }

        private void UpdateCursorVisual()
        {
            Vector2 halfSize = _canvasRect.rect.size * 0.5f;

            halfSize.x = Mathf.Max(0f, halfSize.x - edgePadding.x);
            halfSize.y = Mathf.Max(0f, halfSize.y - edgePadding.y);

            cursorVisual.anchoredPosition = new Vector2(
                _normalizedPosition.x * halfSize.x,
                _normalizedPosition.y * halfSize.y);
        }

        private void UpdateRaycast()
        {
            Camera eventCamera = phoneCanvas.worldCamera;

            _pointerEvent.position = RectTransformUtility.WorldToScreenPoint(
                eventCamera,
                cursorVisual.position);

            _pointerEvent.delta = Vector2.zero;

            _raycastResults.Clear();
            _raycaster.Raycast(_pointerEvent, _raycastResults);

            GameObject target = null;
            _currentRaycast = default;

            for (int i = 0; i < _raycastResults.Count; i++)
            {
                RaycastResult result = _raycastResults[i];
                GameObject handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(result.gameObject);

                if (handler == null || !handler.transform.IsChildOf(phoneCanvas.transform))
                    continue;

                target = handler;
                _currentRaycast = result;
                break;
            }

            SetHoverTarget(target);
        }

        private void SetHoverTarget(GameObject target)
        {
            if (_hoverTarget == target)
                return;

            if (_hoverTarget != null)
                ExecuteEvents.Execute(_hoverTarget, _pointerEvent, ExecuteEvents.pointerExitHandler);

            _hoverTarget = target;

            if (_hoverTarget == null)
                return;

            _pointerEvent.pointerCurrentRaycast = _currentRaycast;
            ExecuteEvents.Execute(_hoverTarget, _pointerEvent, ExecuteEvents.pointerEnterHandler);
        }

        private void Press()
        {
            if (_hoverTarget == null)
                return;

            _pressedTarget = _hoverTarget;

            _pointerEvent.pressPosition = _pointerEvent.position;
            _pointerEvent.pointerPressRaycast = _currentRaycast;
            _pointerEvent.pointerPress = _pressedTarget;

            ExecuteEvents.Execute(_pressedTarget, _pointerEvent, ExecuteEvents.pointerDownHandler);
        }

        private void Release()
        {
            if (_pressedTarget == null)
                return;

            GameObject pressed = _pressedTarget;

            ExecuteEvents.Execute(pressed, _pointerEvent, ExecuteEvents.pointerUpHandler);

            if (pressed == _hoverTarget)
                ExecuteEvents.Execute(pressed, _pointerEvent, ExecuteEvents.pointerClickHandler);

            _pointerEvent.pointerPress = null;
            _pressedTarget = null;
        }

        private void ClearPointerTargets()
        {
            if (_pointerEvent != null && _pressedTarget != null)
                ExecuteEvents.Execute(_pressedTarget, _pointerEvent, ExecuteEvents.pointerUpHandler);

            if (_pointerEvent != null && _hoverTarget != null)
                ExecuteEvents.Execute(_hoverTarget, _pointerEvent, ExecuteEvents.pointerExitHandler);

            _pressedTarget = null;
            _hoverTarget = null;
            _currentRaycast = default;

            if (_pointerEvent != null)
                _pointerEvent.pointerPress = null;
        }

        private bool ValidateConfiguration()
        {
            if (aimAction == null ||
                aimAction.action == null ||
                submitAction == null ||
                submitAction.action == null ||
                phoneCanvas == null ||
                cursorVisual == null ||
                uiInputModule == null)
            {
                Debug.LogError($"{nameof(PhonePointerBehaviour)} on {name} has incomplete configuration.", this);
                return false;
            }

            if (phoneCanvas.renderMode != RenderMode.WorldSpace ||
                phoneCanvas.worldCamera == null ||
                phoneCanvas.GetComponent<GraphicRaycaster>() == null)
            {
                Debug.LogError($"{nameof(PhonePointerBehaviour)} requires a configured World Space Canvas with GraphicRaycaster and Event Camera.", this);
                return false;
            }

            if (cursorVisual.parent != phoneCanvas.transform ||
                cursorVisual.GetComponent<Graphic>() == null)
            {
                Debug.LogError($"{nameof(PhonePointerBehaviour)} requires Cursor Visual to be a direct child of the phone Canvas with a Graphic component.", this);
                return false;
            }

            if (sensitivity.x <= 0f ||
                sensitivity.y <= 0f ||
                edgePadding.x < 0f ||
                edgePadding.y < 0f)
            {
                Debug.LogError($"{nameof(PhonePointerBehaviour)} on {name} contains invalid pointer settings.", this);
                return false;
            }

            return true;
        }
    }
}