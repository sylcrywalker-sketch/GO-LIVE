using UnityEngine;

namespace GoLive.Interaction
{
    public readonly struct InteractionContext
    {
        public GameObject Actor { get; }
        public Transform ViewOrigin { get; }

        public InteractionContext(GameObject actor, Transform viewOrigin)
        {
            Actor = actor;
            ViewOrigin = viewOrigin;
        }
    }

    public interface IInteractable
    {
        bool CanInteract(in InteractionContext context);
        void Interact(in InteractionContext context);
    }
}