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

        public ItemDefinition Definition => definition;
        public ItemInstance Instance { get; private set; }
        public bool CanBeCarried => isActiveAndEnabled && Instance != null && Instance.Location == ItemLocation.World;

        private readonly List<MonoBehaviour> _interactionCandidates = new();

        private Rigidbody _body;
        private Collider[] _colliders;
        private bool[] _colliderEnabledStates;

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

            Instance = ItemInstance.CreateNew(definition.ItemId);
        }

        public bool TryInteract(in InteractionContext context)
        {
            if (!isActiveAndEnabled || Instance == null)
                return false;

            return InteractionResolver.TryInteract(_interactionCandidates, in context, this);
        }

        internal bool TryBeginCarry(Transform anchor)
        {
            if (!CanBeCarried || anchor == null)
                return false;

            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
            _body.isKinematic = true;
            _body.useGravity = false;

            SetCollidersEnabled(false);

            transform.SetParent(anchor, true);
            transform.localPosition = definition.CarryLocalPosition;
            transform.localRotation = Quaternion.Euler(definition.CarryLocalEulerAngles);

            Instance.MoveTo(ItemLocation.Carried);
            return true;
        }

        internal bool TryDrop(Vector3 velocity)
        {
            if (Instance == null || Instance.Location != ItemLocation.Carried)
                return false;

            transform.SetParent(null, true);

            _body.isKinematic = false;
            _body.useGravity = true;
            _body.linearVelocity = velocity;
            _body.angularVelocity = Vector3.zero;

            RestoreColliderStates();

            Instance.MoveTo(ItemLocation.World);
            return true;
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

            if (string.IsNullOrWhiteSpace(definition.ItemId))
            {
                Debug.LogError($"{nameof(ItemDefinition)} assigned to {name} requires a stable Item ID.", definition);
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