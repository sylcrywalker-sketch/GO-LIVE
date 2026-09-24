using System.Collections.Generic;
using GoLive.Interaction;
using UnityEngine;

namespace GoLive.Items
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class WorldItem : MonoBehaviour
    {
        [SerializeField] private ItemDefinition definition;
        [SerializeField, HideInInspector] private string authoredInstanceId;

        public ItemDefinition Definition => definition;
        public ItemInstance Instance { get; private set; }
        public string AuthoredInstanceId => authoredInstanceId;
        public bool IsRuntime => string.IsNullOrWhiteSpace(authoredInstanceId);
        public bool CanBeCarried => isActiveAndEnabled && Instance != null && Instance.Location == ItemLocation.World;
        public bool IsInstalled => Instance != null && Instance.Location == ItemLocation.Installed;

        internal bool IsDiscarded { get; private set; }

        private readonly List<MonoBehaviour> _interactionCandidates = new();

        private Rigidbody _body;
        private Collider[] _colliders;
        private bool[] _colliderEnabledStates;
        private Renderer[] _hiddenRenderers;
        private bool[] _hiddenRendererStates;

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _colliders = GetComponentsInChildren<Collider>(true);
            GetComponents(_interactionCandidates);

            if (!ValidateConfiguration())
            {
                enabled = false;
                return;
            }

            _colliderEnabledStates = new bool[_colliders.Length];

            for (int i = 0; i < _colliders.Length; i++)
                _colliderEnabledStates[i] = _colliders[i].enabled;

            if (!string.IsNullOrWhiteSpace(authoredInstanceId))
                Instance = new ItemInstance(authoredInstanceId, definition.ItemId, ItemLocation.World);
        }

        private void Start()
        {
            if (Instance != null)
                return;

            Debug.LogError($"{nameof(WorldItem)} on {name} has no persistent scene ID and was not initialized as a runtime item.", this);
            enabled = false;
        }

        public static WorldItem SpawnRuntime(ItemDefinition definition, ItemInstance instance, Vector3 position, Quaternion rotation)
        {
            if (definition == null ||
                instance == null ||
                instance.DefinitionId != definition.ItemId ||
                !definition.TryGetRuntimePrefab(out WorldItem prefab))
            {
                return null;
            }

            WorldItem item = Instantiate(prefab, position, rotation);

            if (item.TryInitializeRuntime(instance))
                return item;

            item.DestroyRuntime();
            return null;
        }

        public bool TryInitializeRuntime(ItemInstance instance)
        {
            if (Instance != null || instance == null || instance.DefinitionId != definition.ItemId)
                return false;

            Instance = instance;
            return true;
        }

        internal void DestroyRuntime()
        {
            if (!IsRuntime || IsDiscarded)
                return;

            IsDiscarded = true;
            gameObject.SetActive(false);

            if (Application.isPlaying)
                Destroy(gameObject);
            else
                DestroyImmediate(gameObject);
        }

        public bool TryInteract(in InteractionContext context)
        {
            if (!isActiveAndEnabled || Instance == null)
                return false;

            return InteractionResolver.TryInteract(_interactionCandidates, in context, this);
        }

        public bool TryGetInteractionPrompt(in InteractionContext context, out string key)
        {
            key = null;

            if (!isActiveAndEnabled || Instance == null)
                return false;

            return InteractionResolver.TryGetPromptKey(_interactionCandidates, in context, out key);
        }

        internal bool TryBeginCarry(Transform anchor)
        {
            if (!CanBeCarried || anchor == null)
                return false;

            if (!Instance.TryMove(ItemLocation.World, ItemLocation.Carried))
                return false;

            AttachToCarry(anchor);
            return true;
        }

        internal bool TryBeginCarryFromInventory(Transform anchor)
        {
            if (Instance == null || Instance.Location != ItemLocation.Inventory || anchor == null)
                return false;

            if (!Instance.TryMove(ItemLocation.Inventory, ItemLocation.Carried))
                return false;

            gameObject.SetActive(true);
            AttachToCarry(anchor);

            return true;
        }

        internal bool TryStoreInInventory(Transform storageRoot)
        {
            if (Instance == null || Instance.Location != ItemLocation.Carried || storageRoot == null)
                return false;

            if (!Instance.TryMove(ItemLocation.Carried, ItemLocation.Inventory))
                return false;

            transform.SetParent(storageRoot, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;

            FreezeBody();
            SetCollidersEnabled(false);
            gameObject.SetActive(false);

            return true;
        }

        // Hands -> PC slot. The same object becomes part of the PC; which slot holds it is the PC assembly's record.
        internal bool TryInstall(Transform installAnchor)
        {
            if (Instance == null || Instance.Location != ItemLocation.Carried || installAnchor == null)
                return false;

            if (!Instance.TryMove(ItemLocation.Carried, ItemLocation.Installed))
                return false;

            AttachToInstallAnchor(installAnchor);
            return true;
        }

        internal bool TryBeginCarryFromInstalled(Transform anchor)
        {
            if (Instance == null || Instance.Location != ItemLocation.Installed || anchor == null)
                return false;

            if (!Instance.TryMove(ItemLocation.Installed, ItemLocation.Carried))
                return false;

            AttachToCarry(anchor);
            return true;
        }

        internal bool TryDrop(Vector3 velocity)
        {
            if (Instance == null || Instance.Location != ItemLocation.Carried)
                return false;

            if (!Instance.TryMove(ItemLocation.Carried, ItemLocation.World))
                return false;

            transform.SetParent(null, true);

            _body.isKinematic = false;
            _body.useGravity = true;
            _body.linearVelocity = velocity;
            _body.angularVelocity = Vector3.zero;

            RestoreColliderStates();

            return true;
        }

        internal bool TryPlace(Vector3 position, Quaternion rotation)
        {
            if (!TryDrop(Vector3.zero))
                return false;

            transform.SetPositionAndRotation(position, rotation);
            return true;
        }

        internal bool TryRemoveFromGame(ItemLocation expectedLocation)
        {
            if (Instance == null || !Instance.TryMove(expectedLocation, ItemLocation.Removed))
                return false;

            RestoreAsRemoved(Instance);
            return true;
        }

        internal bool RestoreAsWorld(ItemInstance instance, Vector3 position, Quaternion rotation)
        {
            if (!CanRestore(instance, ItemLocation.World))
                return false;

            Instance = instance;

            gameObject.SetActive(true);
            transform.SetParent(null, true);
            transform.SetPositionAndRotation(position, rotation);

            _body.isKinematic = false;
            _body.useGravity = true;
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;

            RestoreColliderStates();

            return true;
        }

        internal bool RestoreAsInventory(ItemInstance instance, Transform storageRoot)
        {
            if (!CanRestore(instance, ItemLocation.Inventory) || storageRoot == null)
                return false;

            Instance = instance;

            transform.SetParent(storageRoot, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;

            FreezeBody();
            SetCollidersEnabled(false);
            gameObject.SetActive(false);

            return true;
        }

        internal bool RestoreAsInstalled(ItemInstance instance, Transform installAnchor)
        {
            if (!CanRestore(instance, ItemLocation.Installed) || installAnchor == null)
                return false;

            Instance = instance;

            gameObject.SetActive(true);
            AttachToInstallAnchor(installAnchor);

            return true;
        }

        internal bool RestoreAsCarried(ItemInstance instance, Transform anchor)
        {
            if (!CanRestore(instance, ItemLocation.Carried) || anchor == null)
                return false;

            Instance = instance;

            gameObject.SetActive(true);
            AttachToCarry(anchor);

            return true;
        }

        internal bool RestoreAsRemoved(ItemInstance instance)
        {
            if (!CanRestore(instance, ItemLocation.Removed))
                return false;

            Instance = instance;

            FreezeBody();
            transform.SetParent(null, true);
            SetCollidersEnabled(false);
            gameObject.SetActive(false);

            return true;
        }

        private void AttachToCarry(Transform anchor)
        {
            FreezeBody();
            SetCollidersEnabled(false);

            transform.SetParent(anchor, false);
            transform.localPosition = definition.CarryLocalPosition;
            transform.localRotation = Quaternion.Euler(definition.CarryLocalEulerAngles);
        }

        // Installed hardware is part of the PC: it sits exactly on the slot's install anchor, never simulates and has
        // no active colliders, so it neither falls nor answers the normal E pickup ray.
        private void AttachToInstallAnchor(Transform anchor)
        {
            FreezeBody();
            SetCollidersEnabled(false);

            transform.SetParent(anchor, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        // Presentation only, for a view with its own camera (PC Build Mode) that must not show what the hands hold.
        // Item state, physics and colliders are untouched; the renderers come back exactly as they were.
        internal void SetPresentationHidden(bool hidden)
        {
            if (hidden == (_hiddenRenderers != null))
                return;

            if (hidden)
            {
                _hiddenRenderers = GetComponentsInChildren<Renderer>(true);
                _hiddenRendererStates = new bool[_hiddenRenderers.Length];

                for (int i = 0; i < _hiddenRenderers.Length; i++)
                {
                    _hiddenRendererStates[i] = _hiddenRenderers[i].enabled;
                    _hiddenRenderers[i].enabled = false;
                }

                return;
            }

            for (int i = 0; i < _hiddenRenderers.Length; i++)
            {
                if (_hiddenRenderers[i] != null)
                    _hiddenRenderers[i].enabled = _hiddenRendererStates[i];
            }

            _hiddenRenderers = null;
            _hiddenRendererStates = null;
        }

        // Only a free world item simulates; a kinematic body's velocity is left alone because Unity rejects writing it.
        private void FreezeBody()
        {
            if (!_body.isKinematic)
            {
                _body.linearVelocity = Vector3.zero;
                _body.angularVelocity = Vector3.zero;
            }

            _body.isKinematic = true;
            _body.useGravity = false;
        }

        private bool CanRestore(ItemInstance instance, ItemLocation expectedLocation)
        {
            return instance != null &&
                   instance.Location == expectedLocation &&
                   string.Equals(instance.DefinitionId, definition.ItemId, System.StringComparison.Ordinal);
        }

        private void SetCollidersEnabled(bool value)
        {
            for (int i = 0; i < _colliders.Length; i++)
                _colliders[i].enabled = value;
        }

        private void RestoreColliderStates()
        {
            for (int i = 0; i < _colliders.Length; i++)
                _colliders[i].enabled = _colliderEnabledStates[i];
        }

        private bool ValidateConfiguration()
        {
            if (definition == null)
            {
                Debug.LogError($"{nameof(WorldItem)} on {name} requires an Item Definition.", this);
                return false;
            }

            if (!ItemDefinition.IsValidItemId(definition.ItemId))
            {
                Debug.LogError($"{nameof(ItemDefinition)} assigned to {name} has an invalid Item ID.", definition);
                return false;
            }

            if (_colliders.Length == 0)
            {
                Debug.LogError($"{nameof(WorldItem)} on {name} requires at least one Collider.", this);
                return false;
            }

            return true;
        }
    }
}