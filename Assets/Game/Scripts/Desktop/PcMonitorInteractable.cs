using GoLive.Interaction;
using UnityEngine;

namespace GoLive.Desktop
{
    [DisallowMultipleComponent]
    public sealed class PcMonitorInteractable : MonoBehaviour, IInteractable, IInteractionLabel
    {
        [SerializeField] private PcSessionBehaviour session;

        private void Awake()
        {
            if (session != null) return;
            Debug.LogError($"{nameof(PcMonitorInteractable)} on {name} requires an explicit PC session.", this);
            enabled = false;
        }

        public bool CanInteract(in InteractionContext context) => isActiveAndEnabled && session != null &&
            (context.Action == InteractionAction.Primary || context.Action == InteractionAction.Use) && session.CanInteract(context.Actor);

        public string GetPromptKey(in InteractionContext context)
        {
            if (!CanInteract(in context)) return null;
            if (context.Action == InteractionAction.Primary) return "pc.session.sit";
            return session.Session.MonitorOn ? "pc.monitor.off" : "pc.monitor.on";
        }

        public string GetObjectNameKey(in InteractionContext context) => session != null && session.CanInteract(context.Actor) ? "pc.object.monitor" : null;

        public void Interact(in InteractionContext context)
        {
            if (!CanInteract(in context)) return;
            if (context.Action == InteractionAction.Primary) session.TrySit();
            else session.ToggleMonitor();
        }
    }
}
