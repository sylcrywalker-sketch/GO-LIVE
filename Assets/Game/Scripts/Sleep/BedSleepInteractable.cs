using GoLive.Interaction;
using UnityEngine;

namespace GoLive.Sleep
{
    [DisallowMultipleComponent]
    public sealed class BedSleepInteractable : MonoBehaviour, IInteractable
    {
        private const string SleepPromptKey = "interaction.sleep";

        public bool CanInteract(in InteractionContext context)
        {
            if (context.Action != InteractionAction.Use)
                return false;

            return context.Actor.TryGetComponent(out PlayerSleepController sleep) && sleep.CanSleepDefault();
        }

        public string GetPromptKey(in InteractionContext context)
        {
            return CanInteract(in context) ? SleepPromptKey : null;
        }

        public void Interact(in InteractionContext context)
        {
            if (context.Actor.TryGetComponent(out PlayerSleepController sleep))
                sleep.TrySleepDefault();
        }
    }
}