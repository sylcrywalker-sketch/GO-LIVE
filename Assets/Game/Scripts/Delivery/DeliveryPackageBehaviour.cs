using System;
using GoLive.Interaction;
using GoLive.Items;
using GoLive.Player;
using UnityEngine;

namespace GoLive.Delivery
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WorldItem))]
    public sealed class DeliveryPackageBehaviour : MonoBehaviour, IInteractable
    {
        private const string OpenPromptKey = "interaction.open";

        [SerializeField] private Transform contentsAnchor;
        [SerializeField] private GameObject[] closedParts = Array.Empty<GameObject>();

        public WorldItem Item { get; private set; }
        public Transform ContentsAnchor => contentsAnchor;

        private DeliveryBehaviour _owner;

        private void Awake()
        {
            Item = GetComponent<WorldItem>();

            if (contentsAnchor != null)
                return;

            Debug.LogError($"{nameof(DeliveryPackageBehaviour)} on {name} requires a Contents Anchor.", this);
            enabled = false;
        }

        public bool CanInteract(in InteractionContext context)
        {
            return context.Action == InteractionAction.Use &&
                   _owner != null &&
                   context.Actor != null &&
                   context.Actor.TryGetComponent(out PlayerCarry carry) &&
                   carry.CarriedItem == Item &&
                   _owner.CanOpen(Item);
        }

        public string GetPromptKey(in InteractionContext context)
        {
            return CanInteract(in context) ? OpenPromptKey : null;
        }

        public void Interact(in InteractionContext context)
        {
            if (CanInteract(in context) && context.Actor.TryGetComponent(out PlayerCarry carry))
                _owner.TryOpen(this, carry);
        }

        internal void Bind(DeliveryBehaviour owner, bool opened)
        {
            _owner = owner;
            SetOpened(opened);
        }

        internal void SetOpened(bool opened)
        {
            for (int i = 0; i < closedParts.Length; i++)
            {
                if (closedParts[i] != null)
                    closedParts[i].SetActive(!opened);
            }
        }
    }
}
