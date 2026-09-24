using System;
using System.Collections.Generic;
using GoLive.Items;
using GoLive.Player;
using UnityEngine;

namespace GoLive.PcBuilding
{
    // The physical PC in the scene: its authored slots, the PcAssembly record of what is installed where, and the
    // two transactions that move one real item between the player's hands and a slot. Presentation of the slots
    // follows the record; the Workbench only asks and calls.
    [DisallowMultipleComponent]
    public sealed class PcAssemblyBehaviour : MonoBehaviour
    {
        [SerializeField] private PcComponentSlot[] slots = Array.Empty<PcComponentSlot>();

        public PcAssembly Assembly { get; private set; }
        public IReadOnlyList<PcComponentSlot> Slots => slots;
        public PcCapabilities Capabilities => PcCapabilities.Evaluate(Assembly);

        // The installed WorldItems by instance ID, for the slots the Assembly says are filled.
        private readonly Dictionary<string, WorldItem> _installedItems = new(StringComparer.Ordinal);

        private void Awake()
        {
            if (Assembly != null)
                return;

            if (!TryBuildAssembly(out string error))
            {
                Debug.LogError($"{nameof(PcAssemblyBehaviour)} on {name} {error}.", this);
                enabled = false;
            }
        }

        public bool TryGetSlot(string slotId, out PcComponentSlot slot)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (string.Equals(slots[i].SlotId, slotId, StringComparison.Ordinal))
                {
                    slot = slots[i];
                    return true;
                }
            }

            slot = null;
            return false;
        }

        public bool TryGetInstalledItem(PcComponentSlot slot, out WorldItem item)
        {
            item = null;

            return Owns(slot) &&
                   Assembly.TryGetInstalled(slot.SlotId, out PcInstalledComponent component) &&
                   _installedItems.TryGetValue(component.InstanceId, out item);
        }

        // Read-only: what TryInstallCarried would do right now.
        public PcSlotCheck CheckInstall(PcComponentSlot slot, PlayerCarry carry)
        {
            if (!isActiveAndEnabled || !Owns(slot) || carry == null)
                return PcSlotCheck.Unavailable;

            if (!carry.HasItem)
                return PcSlotCheck.NothingInHands;

            WorldItem item = carry.CarriedItem;

            return item.Instance == null
                ? PcSlotCheck.Unavailable
                : Assembly.CheckInstall(slot.SlotId, item.Instance.InstanceId, item.Definition.PcComponent);
        }

        // Hands -> slot. The item moves first (Carried -> Installed on the anchor) and the slot record last, so
        // Assembly.Changed listeners always see the item where the record says it is. If the record is refused the
        // same item goes straight back into the same hands.
        public bool TryInstallCarried(PcComponentSlot slot, PlayerCarry carry)
        {
            if (CheckInstall(slot, carry) != PcSlotCheck.Allowed)
                return false;

            WorldItem held = carry.CarriedItem;
            string instanceId = held.Instance.InstanceId;

            if (!carry.TryInstallCarriedItem(slot.InstallAnchor, out WorldItem installed))
                return false;

            _installedItems.Add(instanceId, installed);

            if (Assembly.TryRecordInstall(slot.SlotId, instanceId, installed.Definition.PcComponent))
            {
                slot.SetOccupied(true);
                return true;
            }

            _installedItems.Remove(instanceId);

            if (!carry.TryCarryFromInstalled(installed))
                Debug.LogError($"Failed to roll back installing item {instanceId} into slot {slot.SlotId}.", this);

            return false;
        }

        // Read-only: what TryRemoveToCarry would do right now.
        public PcSlotCheck CheckRemove(PcComponentSlot slot, PlayerCarry carry)
        {
            if (!isActiveAndEnabled || !Owns(slot) || carry == null)
                return PcSlotCheck.Unavailable;

            PcSlotCheck check = Assembly.CheckRemove(slot.SlotId);

            if (check != PcSlotCheck.Allowed)
                return check;

            return carry.HasItem ? PcSlotCheck.HandsBusy : PcSlotCheck.Allowed;
        }

        // Slot -> empty hands, the same item. Mirrors TryInstallCarried: item first, record last, explicit rollback.
        public bool TryRemoveToCarry(PcComponentSlot slot, PlayerCarry carry)
        {
            if (CheckRemove(slot, carry) != PcSlotCheck.Allowed || !TryGetInstalledItem(slot, out WorldItem item))
                return false;

            string instanceId = item.Instance.InstanceId;

            if (!carry.TryCarryFromInstalled(item))
                return false;

            _installedItems.Remove(instanceId);

            if (Assembly.TryRecordRemoval(slot.SlotId, out _))
            {
                slot.SetOccupied(false);
                return true;
            }

            _installedItems.Add(instanceId, item);

            if (!carry.TryInstallCarriedItem(slot.InstallAnchor, out _))
                Debug.LogError($"Failed to roll back removing item {instanceId} from slot {slot.SlotId}.", this);

            return false;
        }

        public PcAssemblySnapshot CaptureSnapshot()
        {
            return Assembly.CaptureSnapshot();
        }

        // Load preflight. installedItems: every saved item marked Installed, with its definition's PC data.
        public bool ValidateSnapshot(PcAssemblySnapshot snapshot, IReadOnlyDictionary<string, PcComponentSpec> installedItems)
        {
            return Assembly != null && Assembly.IsValidSnapshot(snapshot, installedItems);
        }

        // Load apply, while items are restored: the anchor a validated snapshot puts this item on.
        public bool TryGetInstallAnchor(PcAssemblySnapshot snapshot, string instanceId, out Transform anchor)
        {
            anchor = null;

            for (int i = 0; i < snapshot.InstalledSlots.Length; i++)
            {
                PcInstalledSlotSnapshot record = snapshot.InstalledSlots[i];

                if (string.Equals(record.ItemInstanceId, instanceId, StringComparison.Ordinal) && TryGetSlot(record.SlotId, out PcComponentSlot slot))
                {
                    anchor = slot.InstallAnchor;
                    return true;
                }
            }

            return false;
        }

        // Load apply, after every item holds its restored ItemInstance and the installed ones sit on their anchors.
        public void Restore(PcAssemblySnapshot snapshot, IReadOnlyDictionary<string, WorldItem> items)
        {
            Dictionary<string, WorldItem> installed = new(StringComparer.Ordinal);
            Dictionary<string, PcComponentSpec> specs = new(StringComparer.Ordinal);

            foreach (KeyValuePair<string, WorldItem> pair in items)
            {
                if (!pair.Value.IsInstalled)
                    continue;

                installed.Add(pair.Key, pair.Value);
                specs.Add(pair.Key, pair.Value.Definition.PcComponent);
            }

            Assembly.Restore(snapshot, specs);

            _installedItems.Clear();

            foreach (KeyValuePair<string, WorldItem> pair in installed)
                _installedItems.Add(pair.Key, pair.Value);

            for (int i = 0; i < slots.Length; i++)
                slots[i].SetOccupied(Assembly.IsSlotOccupied(slots[i].SlotId));
        }

        private bool Owns(PcComponentSlot slot)
        {
            return Assembly != null && slot != null && Array.IndexOf(slots, slot) >= 0;
        }

        private bool TryBuildAssembly(out string error)
        {
            PcSlotSpec[] specs = new PcSlotSpec[slots.Length];

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null)
                {
                    error = $"has an empty slot entry at index {i}";
                    return false;
                }

                if (!slots[i].IsConfigured(out string slotError))
                {
                    error = $"slot {slots[i].name} {slotError}";
                    return false;
                }

                specs[i] = slots[i].Spec;
            }

            try
            {
                Assembly = new PcAssembly(specs);
            }
            catch (ArgumentException exception)
            {
                error = exception.Message;
                return false;
            }

            for (int i = 0; i < slots.Length; i++)
                slots[i].SetOccupied(false);

            error = null;
            return true;
        }
    }
}
