using GoLive.Inventory;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace GoLive.UI
{
    [DisallowMultipleComponent]
    public sealed class GameUiInputRouter : MonoBehaviour
    {
        [FormerlySerializedAs("inventory")]
        [SerializeField] private InventoryUiController _inventory;

        [FormerlySerializedAs("pause")]
        [SerializeField] private GamePauseController _pause;

        [FormerlySerializedAs("inventoryAction")]
        [SerializeField] private InputActionReference _inventoryAction;

        [FormerlySerializedAs("backAction")]
        [SerializeField] private InputActionReference _backAction;

        private void Awake()
        {
            if (!ValidateConfiguration())
                enabled = false;
        }

        private void OnEnable()
        {
            SetActionEnabled(_inventoryAction, true);
            SetActionEnabled(_backAction, true);
        }

        private void OnDisable()
        {
            SetActionEnabled(_inventoryAction, false);
            SetActionEnabled(_backAction, false);
        }

        private void Update()
        {
            if (_backAction.action.WasPressedThisFrame())
            {
                HandleBack();
                return;
            }

            if (_inventoryAction.action.WasPressedThisFrame())
                HandleInventory();
        }

        private void HandleBack()
        {
            if (_inventory.IsOpen)
            {
                _inventory.Close();
                return;
            }

            if (_pause.IsPaused)
            {
                _pause.Close();
                return;
            }

            _pause.Open();
        }

        private void HandleInventory()
        {
            if (_pause.IsPaused)
                return;

            if (_inventory.IsOpen)
                _inventory.Close();
            else
                _inventory.Open();
        }

        private bool ValidateConfiguration()
        {
            if (_inventory == null || _pause == null)
            {
                Debug.LogError($"{nameof(GameUiInputRouter)} on {name} requires Inventory and Pause controllers.", this);
                return false;
            }

            if (!HasAction(_inventoryAction) || !HasAction(_backAction))
            {
                Debug.LogError($"{nameof(GameUiInputRouter)} on {name} requires Inventory and Back input actions.", this);
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