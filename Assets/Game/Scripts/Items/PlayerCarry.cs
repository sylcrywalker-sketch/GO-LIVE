using System;
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

        public event Action CarriedItemChanged;

        internal Transform CarryAnchor => carryAnchor;

        private WorldItem _carriedItem;
        private bool _heldItemHidden;

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

            SetCarriedItem(item);
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

            SetCarriedItem(null);
            return true;
        }

        public bool TryPlace(Vector3 position, Quaternion rotation)
        {
            if (!HasItem || !_carriedItem.TryPlace(position, rotation))
                return false;

            SetCarriedItem(null);
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

            SetCarriedItem(null);
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

            SetCarriedItem(null);
            storedItem = item;

            return true;
        }

        internal bool TryCarryFromInventory(WorldItem item)
        {
            if (HasItem || item == null)
                return false;

            if (!item.TryBeginCarryFromInventory(carryAnchor))
                return false;

            SetCarriedItem(item);
            return true;
        }

        // Hands -> PC slot: the held item becomes Installed on the anchor and the hands are empty afterwards.
        internal bool TryInstallCarriedItem(Transform installAnchor, out WorldItem installedItem)
        {
            installedItem = null;

            if (!HasItem)
                return false;

            WorldItem item = _carriedItem;

            if (!item.TryInstall(installAnchor))
                return false;

            SetCarriedItem(null);
            installedItem = item;

            return true;
        }

        // PC slot -> hands, only into empty hands.
        internal bool TryCarryFromInstalled(WorldItem item)
        {
            if (HasItem || item == null)
                return false;

            if (!item.TryBeginCarryFromInstalled(carryAnchor))
                return false;

            SetCarriedItem(item);
            return true;
        }

        internal bool RestoreCarriedItem(WorldItem item)
        {
            if (item != null && (item.Instance == null || item.Instance.Location != ItemLocation.Carried))
                return false;

            SetCarriedItem(item);
            return true;
        }

        // Presentation only: PC Build Mode looks through its own camera, so whatever the hands hold is not drawn
        // meanwhile. An item that leaves the hands is always drawn again.
        public void SetHeldItemHidden(bool hidden)
        {
            if (_heldItemHidden == hidden)
                return;

            _heldItemHidden = hidden;

            if (_carriedItem != null)
                _carriedItem.SetPresentationHidden(hidden);
        }

        private void SetCarriedItem(WorldItem item)
        {
            if (ReferenceEquals(_carriedItem, item))
                return;

            if (_heldItemHidden)
            {
                if (_carriedItem != null)
                    _carriedItem.SetPresentationHidden(false);

                if (item != null)
                    item.SetPresentationHidden(true);
            }

            _carriedItem = item;
            CarriedItemChanged?.Invoke();
        }
    }
}
