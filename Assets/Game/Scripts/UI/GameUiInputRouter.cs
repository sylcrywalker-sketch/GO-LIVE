using GoLive.Inventory;
using GoLive.Phone;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GoLive.UI
{
    [DisallowMultipleComponent]
    public sealed class GameUiInputRouter : MonoBehaviour
    {
        [SerializeField] private InventoryUiController inventory;
        [SerializeField] private GamePauseController pause;
        [SerializeField] private PhoneBehaviour phone;

        [Header("Input")]
        [SerializeField] private InputActionReference inventoryAction;
        [SerializeField] private InputActionReference phoneAction;
        [SerializeField] private InputActionReference backAction;

        private void Awake()
        {
            if (!ValidateConfiguration())
                enabled = false;
        }

        private void OnEnable()
        {
            SetActionEnabled(inventoryAction, true);
            SetActionEnabled(phoneAction, true);
            SetActionEnabled(backAction, true);
        }

        private void OnDisable()
        {
            SetActionEnabled(inventoryAction, false);
            SetActionEnabled(phoneAction, false);
            SetActionEnabled(backAction, false);
        }

        private void Update()
        {
            if (backAction.action.WasPressedThisFrame())
            {
                HandleBack();
                return;
            }

            if (phoneAction.action.WasPressedThisFrame())
            {
                HandlePhone();
                return;
            }

            if (inventoryAction.action.WasPressedThisFrame())
                HandleInventory();
        }

        private void HandleBack()
        {
            if (inventory.IsOpen)
            {
                inventory.Close();
                return;
            }

            if (phone.IsOpen)
            {
                phone.HandleBack();
                return;
            }

            if (pause.IsPaused)
            {
                pause.Close();
                return;
            }

            pause.Open();
        }

        private void HandlePhone()
        {
            if (pause.IsPaused || inventory.IsOpen)
                return;

            if (phone.IsOpen)
                phone.RequestClose();
            else
                phone.Open();
        }

        private void HandleInventory()
        {
            if (pause.IsPaused || phone.IsOpen)
                return;

            if (inventory.IsOpen)
                inventory.Close();
            else
                inventory.Open();
        }

        private bool ValidateConfiguration()
        {
            if (inventory != null &&
                pause != null &&
                phone != null &&
                HasAction(inventoryAction) &&
                HasAction(phoneAction) &&
                HasAction(backAction))
            {
                return true;
            }

            Debug.LogError($"{nameof(GameUiInputRouter)} on {name} has incomplete configuration.", this);
            return false;
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