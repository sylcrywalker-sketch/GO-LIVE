using System.Collections.Generic;
using GoLive.Interaction;
using GoLive.Items;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GoLive.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(PlayerCarry))]
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [Header("Interaction")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private InputActionReference takeAction;
        [SerializeField] private InputActionReference useAction;
        [SerializeField] private InputActionReference dropAction;
        [SerializeField] private InputActionReference specialAction;
        [SerializeField, Min(0.1f)] private float interactionDistance = 2.5f;
        [SerializeField] private LayerMask interactionMask = ~0;

        private readonly List<MonoBehaviour> _interactionCandidates = new();

        private PlayerController _playerController;
        private PlayerCarry _playerCarry;

        private void Awake()
        {
            _playerController = GetComponent<PlayerController>();
            _playerCarry = GetComponent<PlayerCarry>();

            if (!ValidateConfiguration())
                enabled = false;
        }

        private void OnEnable()
        {
            SetActionEnabled(takeAction, true);
            SetActionEnabled(useAction, true);
            SetActionEnabled(dropAction, true);
            SetActionEnabled(specialAction, true);
        }

        private void OnDisable()
        {
            SetActionEnabled(takeAction, false);
            SetActionEnabled(useAction, false);
            SetActionEnabled(dropAction, false);
            SetActionEnabled(specialAction, false);
        }

        private void Update()
        {
            if (!_playerController.Controls.IsAllowed(PlayerControlMask.Interaction))
                return;

            if (dropAction.action.WasPressedThisFrame())
            {
                _playerCarry.Drop();
                return;
            }

            if (takeAction.action.WasPressedThisFrame())
            {
                TryTakeTarget();
                return;
            }

            if (useAction.action.WasPressedThisFrame())
            {
                TryInteract(InteractionAction.Use);
                return;
            }

            if (specialAction.action.WasPressedThisFrame())
                TryInteract(InteractionAction.Special);
        }

        private bool TryTakeTarget()
        {
            if (_playerCarry.HasItem || !TryRaycast(out RaycastHit hit))
                return false;

            WorldItem item = hit.collider.GetComponentInParent<WorldItem>();

            if (item == null)
                return false;

            return _playerCarry.TryCarry(item);
        }

        private bool TryInteract(InteractionAction action)
        {
            InteractionContext context = new(gameObject, playerCamera.transform, action);

            if (_playerCarry.TryInteractCarried(in context))
                return true;

            if (!TryRaycast(out RaycastHit hit))
                return false;

            _interactionCandidates.Clear();
            hit.collider.GetComponentsInParent(false, _interactionCandidates);

            return InteractionResolver.TryInteract(_interactionCandidates, in context, hit.collider);
        }

        private bool TryRaycast(out RaycastHit hit)
        {
            Transform cameraTransform = playerCamera.transform;
            Ray ray = new(cameraTransform.position, cameraTransform.forward);

            return Physics.Raycast(ray, out hit, interactionDistance, interactionMask, QueryTriggerInteraction.Ignore);
        }

        private bool ValidateConfiguration()
        {
            if (playerCamera == null)
            {
                Debug.LogError($"{nameof(PlayerInteractor)} on {name} requires a Player Camera.", this);
                return false;
            }

            if (!HasAction(takeAction) || !HasAction(useAction) || !HasAction(dropAction) || !HasAction(specialAction))
            {
                Debug.LogError($"{nameof(PlayerInteractor)} on {name} requires Take, Use, Drop and Special input actions.", this);
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