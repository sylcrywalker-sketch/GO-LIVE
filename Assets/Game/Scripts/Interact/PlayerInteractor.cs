using System.Collections.Generic;
using GoLive.Interaction;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GoLive.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [Header("Interaction")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private InputActionReference interactAction;
        [SerializeField, Min(0.1f)] private float interactionDistance = 2.5f;
        [SerializeField] private LayerMask interactionMask = ~0;

        private readonly List<MonoBehaviour> _interactionCandidates = new();

        private PlayerController _playerController;

        private void Awake()
        {
            _playerController = GetComponent<PlayerController>();

            if (!ValidateConfiguration())
                enabled = false;
        }

        private void OnEnable()
        {
            interactAction.action.Enable();
        }

        private void OnDisable()
        {
            interactAction.action.Disable();
        }

        private void Update()
        {
            if (!_playerController.Controls.IsAllowed(PlayerControlMask.Interaction))
                return;

            if (!interactAction.action.WasPressedThisFrame())
                return;

            TryInteract();
        }

        private void TryInteract()
        {
            Transform cameraTransform = playerCamera.transform;
            Ray ray = new(cameraTransform.position, cameraTransform.forward);

            if (!Physics.Raycast(ray, out RaycastHit hit, interactionDistance, interactionMask, QueryTriggerInteraction.Ignore))
                return;

            IInteractable interactable = FindInteractable(hit.collider);

            if (interactable is null)
                return;

            InteractionContext context = new(gameObject, cameraTransform);

            if (!interactable.CanInteract(in context))
                return;

            interactable.Interact(in context);
        }

        private IInteractable FindInteractable(Collider collider)
        {
            _interactionCandidates.Clear();
            collider.GetComponentsInParent(false, _interactionCandidates);

            for (int i = 0; i < _interactionCandidates.Count; i++)
            {
                if (_interactionCandidates[i] is IInteractable interactable)
                    return interactable;
            }

            return null;
        }

        private bool ValidateConfiguration()
        {
            if (playerCamera == null)
            {
                Debug.LogError($"{nameof(PlayerInteractor)} on {name} requires a Player Camera.", this);
                return false;
            }

            if (interactAction == null || interactAction.action == null)
            {
                Debug.LogError($"{nameof(PlayerInteractor)} on {name} requires an Interact input action.", this);
                return false;
            }

            return true;
        }
    }
}