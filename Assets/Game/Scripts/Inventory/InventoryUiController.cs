using System;
using GoLive.Items;
using GoLive.Localization;
using GoLive.Player;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GoLive.Inventory
{
    // TAB Inventory: a compact panel over the live world plus a card for the held item.
    // A drag is transactional: while the pointer moves only the preview changes, and the single domain call
    // (PlayerInventory.TryTakeToCarry / TryStoreCarriedItem) happens on a valid release. Closing, an invalid
    // release or any item change during the drag leave every item exactly where it was.
    [DisallowMultipleComponent]
    public sealed class InventoryUiController : MonoBehaviour
    {
        private const string TitleKey = "inventory.title";
        private const string ClosedHintKey = "inventory.hint.closed";
        private const string InstructionsKey = "inventory.hint.open";
        private const string HandsKey = "inventory.hands";
        private const string HandsEmptyKey = "inventory.hands_empty";
        private const string DropTakeKey = "inventory.drop.take";
        private const string DropSwapKey = "inventory.drop.swap";
        private const string DropStoreKey = "inventory.drop.store";
        private const string DropOutsideKey = "inventory.drop.outside";
        private const string DropOnPanelKey = "inventory.drop.on_panel";
        private const string NotStorableKey = "inventory.reject.not_storable";
        private const string FullKey = "inventory.reject.full";
        private const string HandsBusyKey = "inventory.reject.hands_busy";
        private const string FailedKey = "inventory.reject.failed";

        [Header("Player")]
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private PlayerCarry playerCarry;
        [SerializeField] private PlayerController playerController;
        [SerializeField] private LocalizationContext localization;

        [Header("Overlay")]
        [SerializeField] private CanvasGroup overlay;
        [SerializeField] private RectTransform panel;
        [SerializeField] private InventoryGridView grid;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text capacityText;
        [SerializeField] private TMP_Text hintText;
        [SerializeField] private TMP_Text closedHintText;

        [Header("Hands")]
        [SerializeField] private InventoryItemView heldItem;
        [SerializeField] private TMP_Text heldTitleText;

        [Header("Drag")]
        [SerializeField] private InventoryDragGhost dragGhost;

        [Header("Behaviour")]
        [SerializeField] private PlayerControlMask blockedWhileOpen = PlayerControlMask.Look | PlayerControlMask.Jump | PlayerControlMask.Interaction;
        [SerializeField, Min(0.5f)] private float feedbackSeconds = 2.5f;
        [SerializeField] private Color feedbackColor = new(1f, 0.66f, 0.58f, 1f);

        public bool IsOpen => _isOpen;
        public bool IsDragging => _dragSource != DragSource.None;

        private enum DragSource
        {
            None,
            Inventory,
            Hands
        }

        private IDisposable _controlBlock;
        private CursorLockMode _previousCursorLockMode;
        private bool _previousCursorVisible;
        private Color _instructionsColor;
        private float _feedbackUntil;
        private bool _started;
        private bool _bound;
        private bool _isOpen;

        private DragSource _dragSource;
        private InventoryItemView _dragView;
        private string _dragInstanceId;
        private InventoryTransfer? _dropTransfer;

        private void Awake()
        {
            if (!ValidateConfiguration())
            {
                enabled = false;
                return;
            }

            _instructionsColor = hintText.color;
            SetOverlayVisible(false);
        }

        private void Start()
        {
            if (playerInventory.Inventory == null)
            {
                Debug.LogError($"{nameof(InventoryUiController)} could not access the Player Inventory.", this);
                enabled = false;
                return;
            }

            _started = true;
            Bind();
        }

        private void OnEnable()
        {
            if (_started)
                Bind();
        }

        private void OnDisable()
        {
            Close();
            Unbind();
        }

        private void Update()
        {
            if (_feedbackUntil > 0f && Time.unscaledTime >= _feedbackUntil)
                ShowInstructions();
        }

        public void Open()
        {
            if (!_started || !_bound || !isActiveAndEnabled || _isOpen)
                return;

            _isOpen = true;
            _previousCursorLockMode = Cursor.lockState;
            _previousCursorVisible = Cursor.visible;
            _controlBlock = playerController.Controls.Block(blockedWhileOpen);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            grid.Render();
            RenderCapacity();
            RenderHands();
            ShowInstructions();
            SetOverlayVisible(true);
        }

        public void Close()
        {
            if (!_isOpen)
                return;

            CancelDrag();
            _isOpen = false;

            _controlBlock?.Dispose();
            _controlBlock = null;

            Cursor.lockState = _previousCursorLockMode;
            Cursor.visible = _previousCursorVisible;

            SetOverlayVisible(false);
        }

        private void Bind()
        {
            if (_bound)
                return;

            if (!grid.Bind(playerInventory))
            {
                enabled = false;
                return;
            }

            _bound = true;

            grid.ItemDragStarted += HandleInventoryDragStarted;
            grid.ItemDragged += HandleDragged;
            grid.ItemDragEnded += HandleDragEnded;
            heldItem.DragStarted += HandleHandsDragStarted;
            heldItem.Dragged += HandleDragged;
            heldItem.DragEnded += HandleDragEnded;
            playerInventory.Inventory.Changed += HandleInventoryChanged;
            playerCarry.CarriedItemChanged += HandleCarriedItemChanged;
            localization.LanguageChanged += HandleLanguageChanged;

            RefreshTexts();
            RenderCapacity();
            RenderHands();
        }

        private void Unbind()
        {
            if (!_bound)
                return;

            _bound = false;

            grid.ItemDragStarted -= HandleInventoryDragStarted;
            grid.ItemDragged -= HandleDragged;
            grid.ItemDragEnded -= HandleDragEnded;
            heldItem.DragStarted -= HandleHandsDragStarted;
            heldItem.Dragged -= HandleDragged;
            heldItem.DragEnded -= HandleDragEnded;
            playerInventory.Inventory.Changed -= HandleInventoryChanged;
            playerCarry.CarriedItemChanged -= HandleCarriedItemChanged;
            localization.LanguageChanged -= HandleLanguageChanged;

            grid.Unbind();
        }

        private void HandleInventoryDragStarted(InventoryItemView view, PointerEventData eventData)
        {
            BeginDrag(DragSource.Inventory, view, eventData);
        }

        private void HandleHandsDragStarted(InventoryItemView view, PointerEventData eventData)
        {
            BeginDrag(DragSource.Hands, view, eventData);
        }

        private void BeginDrag(DragSource source, InventoryItemView view, PointerEventData eventData)
        {
            if (!_isOpen || IsDragging || view.IsEmpty)
                return;

            _dragSource = source;
            _dragView = view;
            _dragInstanceId = view.InstanceId;

            view.SetLifted(true);
            dragGhost.Show(view);
            UpdateDrop(eventData);
        }

        private void HandleDragged(InventoryItemView view, PointerEventData eventData)
        {
            if (view == _dragView)
                UpdateDrop(eventData);
        }

        private void HandleDragEnded(InventoryItemView view, PointerEventData eventData)
        {
            if (view != _dragView)
                return;

            UpdateDrop(eventData);

            DragSource source = _dragSource;
            string instanceId = _dragInstanceId;
            InventoryTransfer? transfer = _dropTransfer;

            EndDrag();
            Commit(source, instanceId, transfer);
        }

        private void UpdateDrop(PointerEventData eventData)
        {
            bool overPanel = RectTransformUtility.RectangleContainsScreenPoint(panel, eventData.position, eventData.pressEventCamera);

            _dropTransfer = EvaluateDrop(overPanel);

            dragGhost.Follow(eventData);
            dragGhost.SetHint(Text(DropHintKey()), _dropTransfer?.IsAllowed() ?? true);
        }

        // null means the item simply stays where it is when released here.
        private InventoryTransfer? EvaluateDrop(bool overPanel)
        {
            if (_dragSource == DragSource.Inventory)
                return overPanel ? null : playerInventory.CheckTakeToCarry(_dragInstanceId);

            if (!overPanel)
                return null;

            return IsCarrying(_dragInstanceId) ? playerInventory.CheckStoreCarried() : InventoryTransfer.Unavailable;
        }

        private string DropHintKey()
        {
            if (_dropTransfer == null)
                return _dragSource == DragSource.Inventory ? DropOutsideKey : DropOnPanelKey;

            return _dropTransfer.Value switch
            {
                InventoryTransfer.Take => DropTakeKey,
                InventoryTransfer.Swap => DropSwapKey,
                InventoryTransfer.Store => DropStoreKey,
                InventoryTransfer rejected => RejectionKey(rejected)
            };
        }

        private void Commit(DragSource source, string instanceId, InventoryTransfer? transfer)
        {
            if (transfer == null)
                return;

            if (!transfer.Value.IsAllowed())
            {
                ShowFeedback(RejectionKey(transfer.Value));
                return;
            }

            bool moved = source == DragSource.Inventory
                ? playerInventory.TryTakeToCarry(instanceId)
                : IsCarrying(instanceId) && playerInventory.TryStoreCarriedItem();

            if (!moved)
                ShowFeedback(FailedKey);
        }

        private void CancelDrag()
        {
            if (IsDragging)
                EndDrag();
        }

        private void EndDrag()
        {
            if (_dragView != null)
                _dragView.SetLifted(false);

            dragGhost.Hide();

            _dragSource = DragSource.None;
            _dragView = null;
            _dragInstanceId = null;
            _dropTransfer = null;
        }

        // Any change to the authoritative items while a drag is in flight makes its preview stale.
        private void HandleInventoryChanged()
        {
            RenderCapacity();
            CancelDrag();
        }

        private void HandleCarriedItemChanged()
        {
            RenderHands();
            CancelDrag();
        }

        private void HandleLanguageChanged(GameLanguage language)
        {
            RefreshTexts();
            RenderHands();
        }

        private void RenderHands()
        {
            if (!playerCarry.HasItem)
            {
                heldItem.Clear();
                heldTitleText.text = Text(HandsEmptyKey);
                return;
            }

            WorldItem item = playerCarry.CarriedItem;

            heldItem.Show(item.Instance.InstanceId, item.Definition);
            heldTitleText.text = Text(HandsKey);
        }

        private void RenderCapacity()
        {
            capacityText.text = $"{playerInventory.Inventory.Count} / {playerInventory.Inventory.Capacity}";
        }

        private void RefreshTexts()
        {
            titleText.text = Text(TitleKey);
            closedHintText.text = Text(ClosedHintKey);

            if (_feedbackUntil <= 0f)
                ShowInstructions();
        }

        private void ShowInstructions()
        {
            _feedbackUntil = 0f;
            hintText.text = Text(InstructionsKey);
            hintText.color = _instructionsColor;
        }

        private void ShowFeedback(string key)
        {
            _feedbackUntil = Time.unscaledTime + feedbackSeconds;
            hintText.text = Text(key);
            hintText.color = feedbackColor;
        }

        private void SetOverlayVisible(bool visible)
        {
            overlay.gameObject.SetActive(visible);
            overlay.alpha = visible ? 1f : 0f;
            overlay.interactable = visible;
            overlay.blocksRaycasts = visible;
            closedHintText.gameObject.SetActive(!visible);
        }

        private bool IsCarrying(string instanceId)
        {
            return playerCarry.HasItem &&
                   playerCarry.CarriedItem.Instance != null &&
                   string.Equals(playerCarry.CarriedItem.Instance.InstanceId, instanceId, StringComparison.Ordinal);
        }

        private string Text(string key)
        {
            return localization.Text(key);
        }

        private static string RejectionKey(InventoryTransfer transfer)
        {
            return transfer switch
            {
                InventoryTransfer.NotStorable => NotStorableKey,
                InventoryTransfer.InventoryFull => FullKey,
                InventoryTransfer.HandsBusy => HandsBusyKey,
                _ => FailedKey
            };
        }

        private bool ValidateConfiguration()
        {
            if (playerInventory != null &&
                playerCarry != null &&
                playerController != null &&
                localization != null &&
                overlay != null &&
                panel != null &&
                grid != null &&
                titleText != null &&
                capacityText != null &&
                hintText != null &&
                closedHintText != null &&
                heldItem != null &&
                heldTitleText != null &&
                dragGhost != null &&
                blockedWhileOpen != PlayerControlMask.None)
            {
                return true;
            }

            Debug.LogError($"{nameof(InventoryUiController)} on {name} has incomplete configuration.", this);
            return false;
        }
    }
}
