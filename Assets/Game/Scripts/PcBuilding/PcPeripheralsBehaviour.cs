using System;
using System.Collections.Generic;
using GoLive.Items;
using GoLive.Player;
using UnityEngine;

namespace GoLive.PcBuilding
{
    // Unity adapter for two explicit physical connections. The domain owns IDs; this owner moves the same WorldItems
    // between hands and anchors, and publishes domain changes only after their physical location is settled.
    [DisallowMultipleComponent]
    public sealed class PcPeripheralsBehaviour : MonoBehaviour
    {
        [SerializeField] private PcPeripheralSocket microphoneSocket;
        [SerializeField] private PcPeripheralSocket webcamSocket;
        [Tooltip("Optional scene-authored microphone on the microphone anchor, connected once when a new game starts.")]
        [SerializeField] private WorldItem initialMicrophone;
        [SerializeField] private PlayerCarry playerCarry;
        [SerializeField] private PlayerController playerController;

        public PcPeripherals State { get; } = new();
        public bool IsReady { get; private set; }

        private readonly Dictionary<string, WorldItem> _connectedItems = new(StringComparer.Ordinal);
        private bool _configured;
        private bool _transferring;

        private void Awake()
        {
            _configured = IsConfigured(out string error);
            if (_configured) return;
            Debug.LogError($"{nameof(PcPeripheralsBehaviour)} on {name} {error}.", this);
            enabled = false;
        }

        // Every authored WorldItem has completed Awake. Restore may already have supplied the saved record.
        private void Start()
        {
            if (!_configured || IsReady) return;
            if (initialMicrophone == null)
            {
                IsReady = true;
                return;
            }

            if (!IsInitialMicrophoneValid())
            {
                Debug.LogError($"{nameof(PcPeripheralsBehaviour)} on {name} requires its initial microphone to be an untouched, active scene microphone with a persistent ID on its microphone anchor.", this);
                enabled = false;
                return;
            }

            string id = initialMicrophone.Instance.InstanceId;
            PcPeripheralsSnapshot snapshot = new() { MicrophoneId = id };
            Dictionary<string, PcPeripheralKind> kinds = new() { [id] = PcPeripheralKind.Microphone };
            string error = State.Validate(snapshot, kinds);
            if (error != null)
                throw new InvalidOperationException(error);
            if (!initialMicrophone.TryStartInstalled(microphoneSocket.InstallAnchor))
                throw new InvalidOperationException("The validated initial microphone refused its install anchor.");

            _connectedItems.Add(id, initialMicrophone);
            IsReady = true;
            State.Restore(snapshot, kinds);
        }

        public bool TryConnectCarried(PcPeripheralKind kind, PlayerCarry carry)
        {
            if (!CanConnectCarried(kind, carry)) return false;
            PcPeripheralSocket socket = Socket(kind);
            string id = carry.CarriedItem.Instance.InstanceId;
            _transferring = true;
            try
            {
                if (!carry.TryInstallCarriedItem(socket.InstallAnchor, out WorldItem installed)) return false;
                _connectedItems.Add(id, installed);
                if (State.TryConnect(kind, id)) return true;

                _connectedItems.Remove(id);
                if (!carry.TryCarryFromInstalled(installed))
                    Debug.LogError($"Failed to roll back connecting peripheral {id}.", this);
                return false;
            }
            finally { _transferring = false; }
        }

        public bool TryDisconnectToCarry(PcPeripheralKind kind, PlayerCarry carry)
        {
            if (!CanDisconnectToCarry(kind, carry) || !TryGetConnectedItem(kind, out WorldItem item)) return false;
            string id = item.Instance.InstanceId;
            _transferring = true;
            try
            {
                if (!carry.TryCarryFromInstalled(item)) return false;
                _connectedItems.Remove(id);
                if (State.TryDisconnect(kind)) return true;

                _connectedItems.Add(id, item);
                if (!carry.TryInstallCarriedItem(Socket(kind).InstallAnchor, out _))
                    Debug.LogError($"Failed to roll back disconnecting peripheral {id}.", this);
                return false;
            }
            finally { _transferring = false; }
        }

        public bool TryGetConnectedItem(PcPeripheralKind kind, out WorldItem item)
        {
            item = null;
            PcPeripheralSocket socket = Socket(kind);
            if (!_configured || socket == null) return false;
            string id = State.GetConnectedId(kind);
            if (string.IsNullOrEmpty(id) ||
                !_connectedItems.TryGetValue(id, out WorldItem candidate) ||
                !IsInstalledOn(candidate, id, socket)) return false;
            item = candidate;
            return true;
        }

        // The save owner has already validated the complete item graph before using these anchors.
        public bool TryGetRestoreAnchor(PcPeripheralsSnapshot snapshot, string instanceId, out Transform anchor)
        {
            anchor = null;
            if (!_configured || snapshot == null || string.IsNullOrWhiteSpace(instanceId)) return false;
            bool microphone = string.Equals(snapshot.MicrophoneId, instanceId, StringComparison.Ordinal);
            bool webcam = string.Equals(snapshot.WebcamId, instanceId, StringComparison.Ordinal);
            if (microphone == webcam) return false;
            anchor = microphone ? microphoneSocket.InstallAnchor : webcamSocket.InstallAnchor;
            return true;
        }

        // Apply only after the save owner has restored item identities and Installed transforms. Internal PC parts in
        // the complete item dictionary belong to PcAssemblyBehaviour; every Installed peripheral must belong here.
        public void Restore(PcPeripheralsSnapshot snapshot, IReadOnlyDictionary<string, WorldItem> items)
        {
            if (!_configured || _transferring)
                throw new InvalidOperationException("The peripheral bridge is unconfigured or a transfer is in progress.");
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (items == null) throw new ArgumentNullException(nameof(items));
            Dictionary<string, PcPeripheralKind> kinds = new(StringComparer.Ordinal);
            Dictionary<string, WorldItem> connected = new(StringComparer.Ordinal);
            foreach (KeyValuePair<string, WorldItem> pair in items)
            {
                WorldItem item = pair.Value;
                if (item == null || !item.IsInstalled) continue;
                bool recorded = string.Equals(snapshot.MicrophoneId, pair.Key, StringComparison.Ordinal) ||
                                string.Equals(snapshot.WebcamId, pair.Key, StringComparison.Ordinal);
                if (!recorded && item.Definition.PeripheralKind == PcPeripheralKind.None) continue;
                if (!string.Equals(pair.Key, item.Instance.InstanceId, StringComparison.Ordinal))
                    throw new ArgumentException("An installed peripheral has a mismatched instance ID.", nameof(items));
                kinds.Add(pair.Key, item.Definition.PeripheralKind);
                connected.Add(pair.Key, item);
            }

            string error = State.Validate(snapshot, kinds);
            if (error != null) throw new ArgumentException(error, nameof(snapshot));
            foreach (KeyValuePair<string, WorldItem> pair in connected)
            {
                PcPeripheralSocket socket = Socket(pair.Value.Definition.PeripheralKind);
                if (!IsInstalledOn(pair.Value, pair.Key, socket))
                    throw new ArgumentException("An installed peripheral is not on its explicitly owned anchor.", nameof(items));
            }

            _transferring = true;
            try
            {
                _connectedItems.Clear();
                foreach (KeyValuePair<string, WorldItem> pair in connected) _connectedItems.Add(pair.Key, pair.Value);
                IsReady = true;
                State.Restore(snapshot, kinds);
            }
            finally { _transferring = false; }
        }

        internal bool OwnsSocket(PcPeripheralSocket socket) => socket != null &&
            (socket == microphoneSocket || socket == webcamSocket);

        internal bool CanInteract(GameObject actor) => CanUseCarry(playerCarry) && actor == playerController.gameObject;

        internal bool CanTransfer(PcPeripheralKind kind) => playerCarry != null && (playerCarry.HasItem
            ? CanConnectCarried(kind, playerCarry)
            : CanDisconnectToCarry(kind, playerCarry));

        internal bool TryTransfer(PcPeripheralKind kind) => playerCarry != null && (playerCarry.HasItem
            ? TryConnectCarried(kind, playerCarry)
            : TryDisconnectToCarry(kind, playerCarry));

        private bool CanConnectCarried(PcPeripheralKind kind, PlayerCarry carry)
        {
            if (!CanUseCarry(carry) || Socket(kind) == null || !carry.HasItem) return false;
            WorldItem item = carry.CarriedItem;
            return item != null && item.isActiveAndEnabled && item.Instance != null &&
                   item.Instance.Location == ItemLocation.Carried && item.Definition.PeripheralKind == kind &&
                   item.Definition.PcComponent == null && !_connectedItems.ContainsKey(item.Instance.InstanceId) &&
                   State.CanConnect(kind, item.Instance.InstanceId);
        }

        private bool CanDisconnectToCarry(PcPeripheralKind kind, PlayerCarry carry) =>
            CanUseCarry(carry) && !carry.HasItem && TryGetConnectedItem(kind, out _);

        private bool CanUseCarry(PlayerCarry carry) => _configured && IsReady && !_transferring &&
            isActiveAndEnabled && carry != null && carry == playerCarry && carry.isActiveAndEnabled &&
            playerController != null && playerController.Controls.IsAllowed(PlayerControlMask.Interaction);

        private PcPeripheralSocket Socket(PcPeripheralKind kind) => kind switch
        {
            PcPeripheralKind.Microphone => microphoneSocket,
            PcPeripheralKind.Webcam => webcamSocket,
            _ => null
        };

        private static bool IsInstalledOn(WorldItem item, string id, PcPeripheralSocket socket) =>
            item != null && socket != null && socket.InstallAnchor != null && item.isActiveAndEnabled && item.IsInstalled &&
            string.Equals(item.Instance.InstanceId, id, StringComparison.Ordinal) &&
            item.Definition.PeripheralKind == socket.Kind && item.Definition.PcComponent == null &&
            item.transform.parent == socket.InstallAnchor;

        private bool IsInitialMicrophoneValid() => initialMicrophone.isActiveAndEnabled &&
            !initialMicrophone.IsRuntime && initialMicrophone.Instance != null &&
            initialMicrophone.Instance.Location == ItemLocation.World &&
            initialMicrophone.Definition.PeripheralKind == PcPeripheralKind.Microphone &&
            initialMicrophone.Definition.PcComponent == null &&
            initialMicrophone.transform.parent == microphoneSocket.InstallAnchor;

        private bool IsConfigured(out string error)
        {
            if (playerCarry == null || playerController == null)
                error = "requires explicit player carry and controller references";
            else if (microphoneSocket == null || webcamSocket == null || microphoneSocket == webcamSocket ||
                     microphoneSocket.Kind != PcPeripheralKind.Microphone || webcamSocket.Kind != PcPeripheralKind.Webcam)
                error = "requires distinct, correctly typed microphone and webcam sockets";
            else if (!microphoneSocket.BelongsTo(this) || !webcamSocket.BelongsTo(this))
                error = "requires both sockets to explicitly reference this owner";
            else if (!microphoneSocket.IsConfigured(out error) || !webcamSocket.IsConfigured(out error))
                return false;
            else if (microphoneSocket.InstallAnchor.IsChildOf(webcamSocket.InstallAnchor) ||
                     webcamSocket.InstallAnchor.IsChildOf(microphoneSocket.InstallAnchor))
                error = "requires separate install anchors";
            else
                error = null;
            return error == null;
        }
    }
}
