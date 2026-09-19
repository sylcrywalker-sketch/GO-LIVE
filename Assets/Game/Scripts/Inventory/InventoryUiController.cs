using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using GoLive.Items;
using GoLive.Player;

namespace GoLive.Inventory
{
    [DisallowMultipleComponent]
    public sealed class InventoryUiController : MonoBehaviour
    {
        [Header("Player")]
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private PlayerCarry playerCarry;
        [SerializeField] private PlayerController playerController;

        [Header("Input")]
        [SerializeField] private InputActionReference inventoryAction;
        [SerializeField] private InputActionReference cancelAction;

        [Header("Root")]
        [SerializeField] private CanvasGroup overlay;
        [SerializeField] private TMP_Text inventoryHintText;

        [Header("Inventory")]
        [SerializeField] private TMP_Text capacityText;
        [SerializeField] private TMP_Text emptyText;
        [SerializeField] private Button[] slotButtons = Array.Empty<Button>();
        [SerializeField] private Image[] slotIcons = Array.Empty<Image>();
        [SerializeField] private TMP_Text[] slotNames = Array.Empty<TMP_Text>();
        [SerializeField] private TMP_Text[] slotQuantities = Array.Empty<TMP_Text>();
        [SerializeField] private TMP_Text[] slotIconFallbacks = Array.Empty<TMP_Text>();
        [SerializeField] private Image[] slotSelections = Array.Empty<Image>();

        [Header("Categories")]
        [SerializeField] private Button[] categoryButtons = Array.Empty<Button>();
        [SerializeField] private Image[] categoryFocusLines = Array.Empty<Image>();

        [Header("Details")]
        [SerializeField] private Image itemIcon;
        [SerializeField] private TMP_Text itemIconFallback;
        [SerializeField] private TMP_Text itemNameText;
        [SerializeField] private TMP_Text categoryText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text propertiesText;
        [SerializeField] private Button takeButton;
        [SerializeField] private Button dropSelectedButton;

        [Header("Held Item")]
        [SerializeField] private GameObject heldPanel;
        [SerializeField] private Image heldIcon;
        [SerializeField] private TMP_Text heldNameText;
        [SerializeField] private TMP_Text heldInfoText;
        [SerializeField] private Button storeHeldButton;
        [SerializeField] private Button useHeldButton;
        [SerializeField] private Button dropHeldButton;

        [Header("Close")]
        [SerializeField] private Button closeButton;

        private readonly List<ItemInstance> _visibleItems = new(12);

        private UnityAction[] _slotHandlers;
        private UnityAction[] _categoryHandlers;
        private IDisposable _controlBlock;
        private CursorLockMode _previousCursorLockMode;
        private bool _previousCursorVisible;
        private bool _started;
        private bool _bound;
        private bool _isOpen;
        private string _selectedInstanceId;
        private string _emptyItemName;
        private int _categoryIndex;

        private void Awake()
        {
            if (!ValidateConfiguration())
            {
                enabled = false;
                return;
            }

            _emptyItemName = itemNameText.text;
            _slotHandlers = new UnityAction[slotButtons.Length];
            _categoryHandlers = new UnityAction[categoryButtons.Length];

            for (int i = 0; i < _slotHandlers.Length; i++)
            {
                int index = i;
                _slotHandlers[i] = () => SelectSlot(index);
            }

            for (int i = 0; i < _categoryHandlers.Length; i++)
            {
                int index = i;
                _categoryHandlers[i] = () => SelectCategory(index);
            }

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

            if (playerInventory.Inventory.Capacity > slotButtons.Length)
            {
                Debug.LogError($"{nameof(InventoryUiController)} has fewer visual slots than the Inventory capacity.", this);
                enabled = false;
                return;
            }

            _started = true;
            Bind();
            Refresh();
        }

        private void OnEnable()
        {
            SetActionEnabled(inventoryAction, true);
            SetActionEnabled(cancelAction, true);

            if (_started)
                Bind();
        }

        private void OnDisable()
        {
            if (_isOpen)
                Close();

            Unbind();
            SetActionEnabled(inventoryAction, false);
            SetActionEnabled(cancelAction, false);
        }

        private void Update()
        {
            if (inventoryAction.action.WasPressedThisFrame())
            {
                if (_isOpen)
                    Close();
                else
                    Open();

                return;
            }

            if (_isOpen && cancelAction.action.WasPressedThisFrame())
                Close();
        }

        private void Open()
        {
            if (_isOpen)
                return;

            _isOpen = true;
            _previousCursorLockMode = Cursor.lockState;
            _previousCursorVisible = Cursor.visible;
            _controlBlock = playerController.Controls.Block(PlayerControlMask.All);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            SetOverlayVisible(true);
            Refresh();
        }

        private void Close()
        {
            if (!_isOpen)
                return;

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

            _bound = true;
            playerInventory.Inventory.Changed += OnInventoryChanged;

            for (int i = 0; i < slotButtons.Length; i++)
                slotButtons[i].onClick.AddListener(_slotHandlers[i]);

            for (int i = 0; i < categoryButtons.Length; i++)
                categoryButtons[i].onClick.AddListener(_categoryHandlers[i]);

            takeButton.onClick.AddListener(TakeSelected);
            dropSelectedButton.onClick.AddListener(DropSelected);
            storeHeldButton.onClick.AddListener(StoreHeld);
            dropHeldButton.onClick.AddListener(DropHeld);
            closeButton.onClick.AddListener(Close);
        }

        private void Unbind()
        {
            if (!_bound)
                return;

            _bound = false;
            playerInventory.Inventory.Changed -= OnInventoryChanged;

            for (int i = 0; i < slotButtons.Length; i++)
                slotButtons[i].onClick.RemoveListener(_slotHandlers[i]);

            for (int i = 0; i < categoryButtons.Length; i++)
                categoryButtons[i].onClick.RemoveListener(_categoryHandlers[i]);

            takeButton.onClick.RemoveListener(TakeSelected);
            dropSelectedButton.onClick.RemoveListener(DropSelected);
            storeHeldButton.onClick.RemoveListener(StoreHeld);
            dropHeldButton.onClick.RemoveListener(DropHeld);
            closeButton.onClick.RemoveListener(Close);
        }

        private void OnInventoryChanged()
        {
            if (_isOpen)
                Refresh();
        }

        private void SelectSlot(int index)
        {
            if (index < 0 || index >= _visibleItems.Count)
                return;

            _selectedInstanceId = _visibleItems[index].InstanceId;
            Refresh();
        }

        private void SelectCategory(int index)
        {
            if (index < 0 || index >= categoryButtons.Length)
                return;

            _categoryIndex = index;
            _selectedInstanceId = null;
            Refresh();
        }

        private void StoreHeld()
        {
            if (!playerCarry.HasItem)
                return;

            string instanceId = playerCarry.CarriedItem.Instance.InstanceId;

            if (!playerInventory.TryStoreCarriedItem())
                return;

            _selectedInstanceId = instanceId;
            Refresh();
        }

        private void TakeSelected()
        {
            if (string.IsNullOrWhiteSpace(_selectedInstanceId))
                return;

            if (!playerInventory.TryTakeToCarry(_selectedInstanceId))
                return;

            _selectedInstanceId = null;
            Refresh();
        }

        private void DropSelected()
        {
            if (string.IsNullOrWhiteSpace(_selectedInstanceId) || playerCarry.HasItem)
                return;

            if (!playerInventory.TryTakeToCarry(_selectedInstanceId))
                return;

            if (!playerCarry.Drop())
            {
                Debug.LogError($"Failed to drop Inventory item {_selectedInstanceId}.", this);
                return;
            }

            _selectedInstanceId = null;
            Refresh();
        }

        private void DropHeld()
        {
            if (!playerCarry.Drop())
                return;

            Refresh();
        }

        private void Refresh()
        {
            RebuildVisibleItems();
            ValidateSelection();
            RenderSlots();
            RenderCategories();
            RenderDetails();
            RenderHeldItem();
            RenderActions();

            capacityText.text = $"{playerInventory.Inventory.Count} / {playerInventory.Inventory.Capacity}";
            emptyText.gameObject.SetActive(_visibleItems.Count == 0);
        }

        private void RebuildVisibleItems()
        {
            _visibleItems.Clear();

            IReadOnlyList<ItemInstance> items = playerInventory.Inventory.Items;

            for (int i = 0; i < items.Count; i++)
            {
                ItemInstance item = items[i];

                if (_categoryIndex == 0)
                {
                    _visibleItems.Add(item);
                    continue;
                }

                if (!playerInventory.TryGetDefinition(item.InstanceId, out ItemDefinition definition))
                    continue;

                if (MatchesCategory(definition, _categoryIndex))
                    _visibleItems.Add(item);
            }
        }

        private void ValidateSelection()
        {
            if (!string.IsNullOrWhiteSpace(_selectedInstanceId))
            {
                for (int i = 0; i < _visibleItems.Count; i++)
                {
                    if (_visibleItems[i].InstanceId == _selectedInstanceId)
                        return;
                }
            }

            _selectedInstanceId = _visibleItems.Count > 0 ? _visibleItems[0].InstanceId : null;
        }

        private void RenderSlots()
        {
            for (int i = 0; i < slotButtons.Length; i++)
            {
                bool occupied = i < _visibleItems.Count;

                slotButtons[i].interactable = occupied;
                slotSelections[i].enabled = occupied && _visibleItems[i].InstanceId == _selectedInstanceId;

                if (!occupied)
                {
                    slotIcons[i].enabled = false;
                    slotNames[i].text = string.Empty;
                    slotQuantities[i].text = string.Empty;
                    slotIconFallbacks[i].gameObject.SetActive(false);
                    continue;
                }

                ItemInstance item = _visibleItems[i];
                playerInventory.TryGetDefinition(item.InstanceId, out ItemDefinition definition);

                Sprite icon = definition != null ? definition.InventoryIcon : null;

                slotIcons[i].sprite = icon;
                slotIcons[i].enabled = icon != null;
                slotNames[i].text = definition != null ? FormatItemId(definition.ItemId) : item.DefinitionId;
                slotQuantities[i].text = string.Empty;
                slotIconFallbacks[i].gameObject.SetActive(icon == null);
            }
        }

        private void RenderCategories()
        {
            for (int i = 0; i < categoryFocusLines.Length; i++)
                categoryFocusLines[i].enabled = i == _categoryIndex;
        }

        private void RenderDetails()
        {
            if (string.IsNullOrWhiteSpace(_selectedInstanceId) || !playerInventory.TryGetDefinition(_selectedInstanceId, out ItemDefinition definition))
            {
                itemIcon.enabled = false;
                itemIconFallback.gameObject.SetActive(false);
                itemNameText.text = _emptyItemName;
                categoryText.text = string.Empty;
                descriptionText.text = string.Empty;
                propertiesText.text = string.Empty;
                return;
            }

            Sprite icon = definition.InventoryIcon;

            itemIcon.sprite = icon;
            itemIcon.enabled = icon != null;
            itemIconFallback.gameObject.SetActive(icon == null);
            itemNameText.text = FormatItemId(definition.ItemId);
            categoryText.text = definition.Category.ToString();
            descriptionText.text = string.Empty;
            propertiesText.text = definition.CarryStyle.ToString();
        }

        private void RenderHeldItem()
        {
            bool hasItem = playerCarry.HasItem;

            heldPanel.SetActive(hasItem);

            if (!hasItem)
                return;

            ItemDefinition definition = playerCarry.CarriedItem.Definition;
            Sprite icon = definition.InventoryIcon;

            heldIcon.sprite = icon;
            heldIcon.enabled = icon != null;
            heldNameText.text = FormatItemId(definition.ItemId);
            heldInfoText.text = definition.CarryStyle.ToString();
        }

        private void RenderActions()
        {
            bool hasSelection = !string.IsNullOrWhiteSpace(_selectedInstanceId);
            bool handsFree = !playerCarry.HasItem;

            takeButton.interactable = hasSelection && handsFree;
            dropSelectedButton.interactable = hasSelection && handsFree;

            bool canStoreHeld = playerCarry.HasItem && playerCarry.CarriedItem.Definition.CanStoreInInventory && !playerInventory.Inventory.IsFull;

            storeHeldButton.interactable = canStoreHeld;
            useHeldButton.interactable = false;
            dropHeldButton.interactable = playerCarry.HasItem;
        }

        private void SetOverlayVisible(bool visible)
        {
            overlay.gameObject.SetActive(visible);
            overlay.alpha = visible ? 1f : 0f;
            overlay.interactable = visible;
            overlay.blocksRaycasts = visible;
            inventoryHintText.gameObject.SetActive(!visible);
        }

        private bool ValidateConfiguration()
        {
            if (playerInventory == null || playerCarry == null || playerController == null || overlay == null || inventoryHintText == null)
                return LogInvalidConfiguration();

            if (!HasAction(inventoryAction) || !HasAction(cancelAction))
                return LogInvalidConfiguration();

            int slotCount = slotButtons.Length;

            if (slotCount == 0 || slotIcons.Length != slotCount || slotNames.Length != slotCount || slotQuantities.Length != slotCount || slotIconFallbacks.Length != slotCount || slotSelections.Length != slotCount)
                return LogInvalidConfiguration();

            if (categoryButtons.Length != 4 || categoryFocusLines.Length != categoryButtons.Length)
                return LogInvalidConfiguration();

            if (capacityText == null || emptyText == null || itemIcon == null || itemIconFallback == null || itemNameText == null || categoryText == null || descriptionText == null || propertiesText == null)
                return LogInvalidConfiguration();

            if (heldPanel == null || heldIcon == null || heldNameText == null || heldInfoText == null || takeButton == null || dropSelectedButton == null || storeHeldButton == null || useHeldButton == null || dropHeldButton == null || closeButton == null)
                return LogInvalidConfiguration();

            return true;
        }

        private bool LogInvalidConfiguration()
        {
            Debug.LogError($"{nameof(InventoryUiController)} on {name} has incomplete configuration.", this);
            return false;
        }

        private static bool MatchesCategory(ItemDefinition definition, int categoryIndex)
        {
            return categoryIndex switch
            {
                1 => definition.Category == ItemCategory.Food,
                2 => definition.Category == ItemCategory.Electronics,
                3 => definition.Category == ItemCategory.Household,
                _ => true
            };
        }

        private static string FormatItemId(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return string.Empty;

            string value = itemId.Replace('_', ' ').Replace('-', ' ');

            return char.ToUpperInvariant(value[0]) + value[1..];
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