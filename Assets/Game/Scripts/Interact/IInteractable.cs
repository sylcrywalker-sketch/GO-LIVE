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
        string GetPromptKey(in InteractionContext context);
        void Interact(in InteractionContext context);
    }

    public static class InteractionResolver
    {
        public static bool TryInteract(IReadOnlyList<MonoBehaviour> candidates, in InteractionContext context, Object logContext)
        {
            if (!TryResolve(candidates, in context, out IInteractable interactable, out bool ambiguous))
            {
                if (ambiguous)
                    Debug.LogError($"Multiple interactables can handle {context.Action} on the same target.", logContext);

                return false;
            }

            interactable.Interact(in context);
            return true;
        }

        public static bool TryGetPromptKey(IReadOnlyList<MonoBehaviour> candidates, in InteractionContext context, out string key)
        {
            key = null;

            if (!TryResolve(candidates, in context, out IInteractable interactable, out _))
                return false;

            key = interactable.GetPromptKey(in context);
            return !string.IsNullOrWhiteSpace(key);
        }

        private static bool TryResolve(IReadOnlyList<MonoBehaviour> candidates, in InteractionContext context, out IInteractable selected, out bool ambiguous)
        {
            selected = null;
            ambiguous = false;

            for (int i = 0; i < candidates.Count; i++)
            {
                MonoBehaviour candidate = candidates[i];

                if (candidate == null || !candidate.isActiveAndEnabled || candidate is not IInteractable interactable)
                    continue;

                if (!interactable.CanInteract(in context))
                    continue;

                if (selected != null)
                {
                    selected = null;
                    ambiguous = true;
                    return false;
                }

                selected = interactable;
            }

            return selected != null;
        }
    }
}