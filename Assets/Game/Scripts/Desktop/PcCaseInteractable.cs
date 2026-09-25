using GoLive.Interaction;
using UnityEngine;

namespace GoLive.Desktop
{
    [DisallowMultipleComponent]
    public sealed class PcCaseInteractable : MonoBehaviour, IInteractable, IInteractionLabel
    {
        [SerializeField] private PcSessionBehaviour session;

        private void Awake()
        {
            if (session != null) return;
            Debug.LogError($"{nameof(PcCaseInteractable)} on {name} requires an explicit PC session.", this);
            enabled = false;
        }

        public bool CanInteract(in InteractionContext context) => isActiveAndEnabled && session != null &&
            context.Action == InteractionAction.Use && session.CanInteract(context.Actor);

        public string GetPromptKey(in InteractionContext context) => CanInteract(in context)
            ? session.Session.Power == PcPowerState.Off ? "pc.power.on" : "pc.power.off"
            : null;

        public string GetObjectNameKey(in InteractionContext context) => session != null && session.CanInteract(context.Actor) ? "pc.object.case" : null;

        public void Interact(in InteractionContext context)
        {
            if (CanInteract(in context)) session.TryTogglePower();
        }
    }
}
