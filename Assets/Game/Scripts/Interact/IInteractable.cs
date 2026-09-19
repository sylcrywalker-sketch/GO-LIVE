using System.Collections.Generic;
using UnityEngine;

namespace GoLive.Interaction
{
    public enum InteractionAction
    {
        Use,
        Special
    }

    public readonly struct InteractionContext
    {
        public GameObject Actor { get; }
        public Transform ViewOrigin { get; }
        public InteractionAction Action { get; }

        public InteractionContext(GameObject actor, Transform viewOrigin, InteractionAction action)
        {
            Actor = actor;
            ViewOrigin = viewOrigin;
            Action = action;
        }
    }

    public interface IInteractable
    {
        bool CanInteract(in InteractionContext context);
        void Interact(in InteractionContext context);
    }

    public static class InteractionResolver
    {
        public static bool TryInteract(IReadOnlyList<MonoBehaviour> candidates, in InteractionContext context, Object logContext)
        {
            IInteractable selected = null;

            for (int i = 0; i < candidates.Count; i++)
            {
                MonoBehaviour candidate = candidates[i];

                if (candidate == null || !candidate.isActiveAndEnabled || candidate is not IInteractable interactable)
                    continue;

                if (!interactable.CanInteract(in context))
                    continue;

                if (selected != null)
                {
                    Debug.LogError($"Multiple interactables can handle {context.Action} on the same target.", logContext);
                    return false;
                }

                selected = interactable;
            }

            if (selected == null)
                return false;

            selected.Interact(in context);
            return true;
        }
    }
}