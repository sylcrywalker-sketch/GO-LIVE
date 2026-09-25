using System;
using System.Collections.Generic;
using GoLive.Interaction;
using GoLive.Items;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GoLive.Player
{
    public readonly struct InteractionPromptState : IEquatable<InteractionPromptState>
    {
        public string TakeKey { get; }
        public string WorldUseKey { get; }
        public string HeldUseKey { get; }
        public string DropKey { get; }
        public string SpecialKey { get; }
        public string ObjectNameKey { get; }
        public string PrimaryKey { get; }

        public InteractionPromptState(string takeKey, string worldUseKey, string heldUseKey, string dropKey, string specialKey)
            : this(takeKey, worldUseKey, heldUseKey, dropKey, specialKey, null, null)
        {
        }

        public InteractionPromptState(string takeKey, string worldUseKey, string heldUseKey, string dropKey, string specialKey, string objectNameKey, string primaryKey)
        {
            TakeKey = takeKey;
            WorldUseKey = worldUseKey;
            HeldUseKey = heldUseKey;
            DropKey = dropKey;
            SpecialKey = specialKey;
            ObjectNameKey = objectNameKey;
            PrimaryKey = primaryKey;
        }

        public bool Equals(InteractionPromptState other)
        {
            return TakeKey == other.TakeKey &&
                   WorldUseKey == other.WorldUseKey &&
                   HeldUseKey == other.HeldUseKey &&
                   DropKey == other.DropKey &&
                   SpecialKey == other.SpecialKey &&
                   ObjectNameKey == other.ObjectNameKey &&
                   PrimaryKey == other.PrimaryKey;
        }

        public override bool Equals(object obj)
        {
            return obj is InteractionPromptState other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(TakeKey, WorldUseKey, HeldUseKey, DropKey, SpecialKey, ObjectNameKey, PrimaryKey);
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(PlayerCarry))]
    public sealed class PlayerInteractor : MonoBehaviour
    {
        private const string TakePromptKey = "interaction.take";
        private const string DropPromptKey = "interaction.drop";

        [Header("Interaction")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private InputActionReference takeAction;
        [SerializeField] private InputActionReference useAction;
        [SerializeField] private InputActionReference dropAction;
        [SerializeField] private InputActionReference specialAction;
        [SerializeField, Min(0.1f)] private float interactionDistance = 2.5f;
        [SerializeField] private LayerMask interactionMask = ~0;

        public InteractionPromptState Prompts { get; private set; }

        public event Action<InteractionPromptState> PromptsChanged;

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
            SetPrompts(default);

            SetActionEnabled(takeAction, false);
            SetActionEnabled(useAction, false);
            SetActionEnabled(dropAction, false);
            SetActionEnabled(specialAction, false);
        }

        private void Update()
        {
            if (!_playerController.Controls.IsAllowed(PlayerControlMask.Interaction))
            {
                SetPrompts(default);
                return;
            }

            bool hasTarget = TryRaycast(out RaycastHit hit);

            RefreshPrompts(hasTarget, in hit);

            if (dropAction.action.WasPressedThisFrame())
            {
                _playerCarry.Drop();
                return;
            }

            if (takeAction.action.WasPressedThisFrame())
            {
                if (!TryTakeTarget(hasTarget, in hit))
                    TryInteract(InteractionAction.Primary, hasTarget, in hit);
                return;
            }

            if (useAction.action.WasPressedThisFrame())
            {
                TryInteract(InteractionAction.Use, hasTarget, in hit);
                return;
            }

            if (specialAction.action.WasPressedThisFrame())
                TryInteract(InteractionAction.Special, hasTarget, in hit);
        }

        public string GetTakeBinding() => KeyHint(takeAction);
        public string GetUseBinding() => KeyHint(useAction);
        public string GetDropBinding() => KeyHint(dropAction);
        public string GetSpecialBinding() => KeyHint(specialAction);

        private bool TryTakeTarget(bool hasTarget, in RaycastHit hit)
        {
            if (_playerCarry.HasItem || !hasTarget)
                return false;

            WorldItem item = hit.collider.GetComponentInParent<WorldItem>();

            if (item == null)
                return false;

            return _playerCarry.TryCarry(item);
        }

        private bool TryInteract(InteractionAction action, bool hasTarget, in RaycastHit hit)
        {
            InteractionContext context = new(gameObject, playerCamera.transform, action);

            if (action == InteractionAction.Use && _playerCarry.TryInteractCarried(in context))
                return true;

            if (!hasTarget)
                return false;

            FillInteractionCandidates(hit.collider);

            return InteractionResolver.TryInteract(_interactionCandidates, in context, hit.collider);
        }

        private void RefreshPrompts(bool hasTarget, in RaycastHit hit)
        {
            string takeKey = null;
            string worldUseKey = null;
            string heldUseKey = null;
            string dropKey = null;
            string specialKey = null;
            string objectNameKey = null;
            string primaryKey = null;

            InteractionContext primaryContext = new(gameObject, playerCamera.transform, InteractionAction.Primary);
            InteractionContext useContext = new(gameObject, playerCamera.transform, InteractionAction.Use);
            InteractionContext specialContext = new(gameObject, playerCamera.transform, InteractionAction.Special);

            if (_playerCarry.HasItem)
            {
                dropKey = DropPromptKey;
                _playerCarry.CarriedItem.TryGetInteractionPrompt(in useContext, out heldUseKey);
            }

            if (hasTarget)
            {
                WorldItem worldItem = hit.collider.GetComponentInParent<WorldItem>();

                if (!_playerCarry.HasItem && worldItem != null && worldItem.CanBeCarried)
                    takeKey = TakePromptKey;

                FillInteractionCandidates(hit.collider);

                if (takeKey == null)
                    InteractionResolver.TryGetPromptKey(_interactionCandidates, in primaryContext, out primaryKey);

                if (!InteractionResolver.TryGetObjectNameKey(_interactionCandidates, in primaryContext, out objectNameKey) && worldItem != null && worldItem.Definition != null)
                    objectNameKey = worldItem.Definition.NameLocalizationKey;

                if (heldUseKey == null)
                    InteractionResolver.TryGetPromptKey(_interactionCandidates, in useContext, out worldUseKey);

                InteractionResolver.TryGetPromptKey(_interactionCandidates, in specialContext, out specialKey);
            }

            SetPrompts(new InteractionPromptState(takeKey, worldUseKey, heldUseKey, dropKey, specialKey, objectNameKey, primaryKey));
        }

        private void FillInteractionCandidates(Collider collider)
        {
            _interactionCandidates.Clear();
            collider.GetComponentsInParent(false, _interactionCandidates);
        }

        private void SetPrompts(InteractionPromptState prompts)
        {
            if (Prompts.Equals(prompts))
                return;

            Prompts = prompts;
            PromptsChanged?.Invoke(prompts);
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

        private static string KeyHint(InputActionReference reference)
        {
            return HasAction(reference) ? InputHints.Key(reference.action) : string.Empty;
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
