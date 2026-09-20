using GoLive.Interaction;
using GoLive.Items;
using UnityEngine;

namespace GoLive.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerCarry : MonoBehaviour
    {
        [SerializeField] private Transform carryAnchor;
        [SerializeField, Min(0f)] private float dropForwardSpeed = 1.5f;

        public bool HasItem => _carriedItem != null;
        public WorldItem CarriedItem => _carriedItem;

        internal Transform CarryAnchor => carryAnchor;

        private WorldItem _carriedItem;

        private void Awake()
        {
            if (carryAnchor != null)
                return;

            Debug.LogError($"{nameof(PlayerCarry)} on {name} requires a Carry Anchor.", this);
            enabled = false;
        }

        public bool TryCarry(WorldItem item)
        {
            if (!isActiveAndEnabled || HasItem || item == null || !item.CanBeCarried)
                return false;

            if (!item.TryBeginCarry(carryAnchor))
                return false;

            _carriedItem = item;
            return true;
        }

        public bool Drop()
        {
            if (!HasItem)
                return false;

            WorldItem item = _carriedItem;
            Vector3 velocity = carryAnchor.forward * dropForwardSpeed;

            if (!item.TryDrop(velocity))
                return false;

            _carriedItem = null;
            return true;
        }

        public bool TryInteractCarried(in InteractionContext context)
        {
            if (!HasItem)
                return false;

            return _carriedItem.TryInteract(in context);
        }

        public bool TryRemoveCarriedItem(WorldItem item)
        {
            if (!HasItem || item == null || _carriedItem != item)
                return false;

            if (!item.TryRemoveFromGame(ItemLocation.Carried))
                return false;

            _carriedItem = null;
            return true;
        }

        internal bool TryStoreCarriedItem(Transform storageRoot, out WorldItem storedItem)
        {
            storedItem = null;

            if (!HasItem)
                return false;

            WorldItem item = _carriedItem;

            if (!item.TryStoreInInventory(storageRoot))
                return false;

            _carriedItem = null;
            storedItem = item;

            return true;
        }

        internal bool TryCarryFromInventory(WorldItem item)
        {
            if (HasItem || item == null)
                return false;

            if (!item.TryBeginCarryFromInventory(carryAnchor))
                return false;

            _carriedItem = item;
            return true;
        }

        internal bool RestoreCarriedItem(WorldItem item)
        {
            if (item != null && (item.Instance == null || item.Instance.Location != ItemLocation.Carried))
                return false;

            _carriedItem = item;
            return true;
        }
    }
}