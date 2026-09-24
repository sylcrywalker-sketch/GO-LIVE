using System;
using System.Collections.Generic;
using GoLive.Items;
using GoLive.Player;
using UnityEngine;

namespace GoLive.PcBuilding
{
    // The physical PC in the scene: its authored slots, the PcAssembly record of what is installed where, the parts it
    // comes with in a new game, and the two transactions that move one real item between the player's hands and a
    // slot. Presentation of the slots follows the record; the Workbench only asks and calls.
    [DisallowMultipleComponent]
    public sealed class PcAssemblyBehaviour : MonoBehaviour
    {
        // One part the PC comes with: a scene-authored item that sits on this slot's Install Anchor.
        [Serializable]
        private struct PreinstalledPart
        {
            public PcComponentSlot slot;
            public WorldItem item;
        }

        [SerializeField] private PcComponentSlot[] slots = Array.Empty<PcComponentSlot>();

        [Tooltip("What this PC has installed when a new game starts. Each item is a scene item with a persistent ID, placed on its slot's Install Anchor.")]
        [SerializeField] private PreinstalledPart[] preinstalled = Array.Empty<PreinstalledPart>();

        public PcAssembly Assembly { get; private set; }
        public IReadOnlyList<PcComponentSlot> Slots => slots;

        // Computed from the record on every read, so it is never stale after an install or removal.
        public PcCapabilities Capabilities => PcCapabilities.Evaluate(Assembly);

        // The record describes the game: the new-game parts are installed, or a save has been restored since.
        public bool IsReady { get; private set; }

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

        // Unity runs every Awake of the loaded scene before any Start, so each preinstalled item already holds its scene
        // ItemInstance here. Start runs once per lifetime; a later load replaces the record through Restore.
        private void Start()
        {
            if (Assembly == null || IsReady)
                return;

            if (!TryInstallPreinstalled(out string error))
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

        // Pressing the power button now: answered from the installed hardware, changes nothing.
        public PcPowerOnResult TryPowerOn()
        {
            return PcPowerOnResult.Attempt(Assembly);
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

            ApplyRecord(snapshot, specs, installed);
        }

        // The new-game record, validated as a whole before any item moves: the same snapshot rules a save must pass
        // (known slots, one item per slot, one slot per item, matching component type and connector, every part on an
        // installed host part), plus the scene rules only authored content can break.
        private bool TryInstallPreinstalled(out string error)
        {
            PcInstalledSlotSnapshot[] records = new PcInstalledSlotSnapshot[preinstalled.Length];
            Dictionary<string, PcComponentSpec> specs = new(StringComparer.Ordinal);
            Dictionary<string, WorldItem> items = new(StringComparer.Ordinal);

            for (int i = 0; i < preinstalled.Length; i++)
            {
                PcComponentSlot slot = preinstalled[i].slot;
                WorldItem item = preinstalled[i].item;

                if (!Owns(slot))
                {
                    error = $"has preinstalled entry {i} without one of its own slots";
                    return false;
                }

                // A scene item gets its ItemInstance in its own Awake, only when it is configured; one without had none.
                if (item == null || item.IsRuntime || item.Instance == null || item.Instance.Location != ItemLocation.World)
                {
                    error = $"has preinstalled entry {i} ({slot.SlotId}) without a scene item that has a persistent ID and is still untouched";
                    return false;
                }

                if (item.transform.parent != slot.InstallAnchor)
                {
                    error = $"has preinstalled item {item.name} away from the Install Anchor of slot {slot.SlotId}";
                    return false;
                }

                if (!items.TryAdd(item.Instance.InstanceId, item))
                {
                    error = $"has preinstalled item {item.name} listed twice or sharing the ID {item.Instance.InstanceId}";
                    return false;
                }

                specs.Add(item.Instance.InstanceId, item.Definition.PcComponent);
                records[i] = new PcInstalledSlotSnapshot { SlotId = slot.SlotId, ItemInstanceId = item.Instance.InstanceId };
            }

            // An item left on an anchor but not listed would be a loose world item inside the PC with no slot record.
            for (int i = 0; i < slots.Length; i++)
            {
                foreach (WorldItem child in slots[i].InstallAnchor.GetComponentsInChildren<WorldItem>(true))
                {
                    if (child.Instance == null || !items.TryGetValue(child.Instance.InstanceId, out WorldItem listed) || listed != child)
                    {
                        error = $"has item {child.name} on slot {slots[i].SlotId} that is not a preinstalled part";
                        return false;
                    }
                }
            }

            PcAssemblySnapshot record = new() { Version = PcAssembly.SnapshotVersion, InstalledSlots = records };

            if (!Assembly.IsValidSnapshot(record, specs))
            {
                error = "has preinstalled parts that do not fit their slots (a slot used twice, a wrong component type or connector, or a part without the part it is mounted on)";
                return false;
            }

            for (int i = 0; i < preinstalled.Length; i++)
            {
                if (!preinstalled[i].item.TryStartInstalled(preinstalled[i].slot.InstallAnchor))
                    throw new InvalidOperationException($"Validated preinstalled item {preinstalled[i].item.name} refused its slot.");
            }

            ApplyRecord(record, specs, items);
            error = null;
            return true;
        }

        // The one way a whole record is taken over (new game and load): the items already sit where it says.
        private void ApplyRecord(PcAssemblySnapshot snapshot, IReadOnlyDictionary<string, PcComponentSpec> specs, IReadOnlyDictionary<string, WorldItem> installed)
        {
            Assembly.Restore(snapshot, specs);

            _installedItems.Clear();

            foreach (KeyValuePair<string, WorldItem> pair in installed)
                _installedItems.Add(pair.Key, pair.Value);

            for (int i = 0; i < slots.Length; i++)
                slots[i].SetOccupied(Assembly.IsSlotOccupied(slots[i].SlotId));

            IsReady = true;
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
