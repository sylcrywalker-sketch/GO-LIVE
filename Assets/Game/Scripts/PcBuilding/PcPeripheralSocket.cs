using GoLive.Interaction;
using UnityEngine;

namespace GoLive.PcBuilding
{
    // The stable interaction target stays on the desk when the real device leaves its install anchor.
    [DisallowMultipleComponent]
    public sealed class PcPeripheralSocket : MonoBehaviour, IInteractable, IInteractionLabel
    {
        [SerializeField] private PcPeripheralKind kind;
        [SerializeField] private Transform installAnchor;
        [SerializeField] private PcPeripheralsBehaviour peripherals;
        [SerializeField] private Collider interactionCollider;

        public PcPeripheralKind Kind => kind;
        public Transform InstallAnchor => installAnchor;

        private void Awake()
        {
            if (IsConfigured(out string error)) return;
            Debug.LogError($"{nameof(PcPeripheralSocket)} on {name} {error}.", this);
            enabled = false;
        }

        public bool CanInteract(in InteractionContext context)
        {
            return isActiveAndEnabled && interactionCollider != null && interactionCollider.enabled &&
                   context.Action == InteractionAction.Primary && peripherals != null &&
                   peripherals.CanInteract(context.Actor) && peripherals.CanTransfer(kind);
        }

        public string GetPromptKey(in InteractionContext context)
        {
            if (!CanInteract(in context)) return null;
            return peripherals.State.GetConnectedId(kind).Length == 0
                ? "pc.peripheral.connect"
                : "pc.peripheral.disconnect";
        }

        public string GetObjectNameKey(in InteractionContext context)
        {
            if (!isActiveAndEnabled || peripherals == null || !peripherals.CanInteract(context.Actor)) return null;
            return kind == PcPeripheralKind.Microphone ? "pc.peripheral.microphone" : "pc.peripheral.webcam";
        }

        public void Interact(in InteractionContext context)
        {
            if (CanInteract(in context)) peripherals.TryTransfer(kind);
        }

        internal bool BelongsTo(PcPeripheralsBehaviour owner) => peripherals == owner;

        internal bool IsConfigured(out string error)
        {
            if (kind != PcPeripheralKind.Microphone && kind != PcPeripheralKind.Webcam)
                error = "requires a microphone or webcam kind";
            else if (peripherals == null || !peripherals.OwnsSocket(this))
                error = "requires an explicit peripheral owner that lists this socket";
            else if (installAnchor == null || installAnchor == transform || !installAnchor.IsChildOf(transform))
                error = "requires a dedicated Install Anchor inside the socket";
            else if (interactionCollider == null || !interactionCollider.transform.IsChildOf(transform) ||
                     interactionCollider.transform.IsChildOf(installAnchor) || interactionCollider.isTrigger)
                error = "requires a non-trigger interaction collider on the socket, outside its Install Anchor";
            else
                error = null;
            return error == null;
        }
    }
}
