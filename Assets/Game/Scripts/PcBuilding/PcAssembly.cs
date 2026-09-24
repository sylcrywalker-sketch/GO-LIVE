using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using GoLive.Items;

namespace GoLive.PcBuilding
{
    // Why a component can or cannot go into (or come out of) a slot right now. Each value has exactly one
    // localized message (MessageKey), so no screen builds its own error strings. The two mounting checks name the parts
    // involved: their messages take {0}, those parts' names (PcComponentType.ObjectNameKey).
    public enum PcSlotCheck
    {
        Allowed,
        UnknownSlot,
        NotPcHardware,
        WrongComponentType,
        WrongConnector,
        SlotOccupied,
        AlreadyInstalled,
        SlotEmpty,
        NoMatchingSlot,
        NothingInHands,
        HandsBusy,
        Unavailable,

        // The slot sits on a part that is not installed: no processor socket without a motherboard. {0}: that part.
        HostMissing,

        // Parts mounted on this one are still installed: the motherboard comes out after the processor. {0}: those parts.
        MountedPartsInstalled
    }

    public static class PcSlotCheckExtensions
    {
        public static string MessageKey(this PcSlotCheck check)
        {
            return check switch
            {
                PcSlotCheck.NotPcHardware => "pc.reject.not_hardware",
                PcSlotCheck.WrongComponentType => "pc.reject.wrong_type",
                PcSlotCheck.WrongConnector => "pc.reject.wrong_connector",
                PcSlotCheck.SlotOccupied => "pc.reject.slot_occupied",
                PcSlotCheck.SlotEmpty => "pc.reject.slot_empty",
                PcSlotCheck.NoMatchingSlot => "pc.reject.no_matching_slot",
                PcSlotCheck.NothingInHands => "pc.reject.nothing_in_hands",
                PcSlotCheck.HandsBusy => "pc.reject.hands_busy",
                PcSlotCheck.HostMissing => "pc.reject.install_first",
                PcSlotCheck.MountedPartsInstalled => "pc.reject.remove_first",
                _ => "pc.reject.unavailable"
            };
        }
    }

    // One slot as the assembly rules see it. Authored on a PcComponentSlot in the PC prefab. HostSlotId is the slot it is
    // mounted on (the processor socket sits on the motherboard), null for a slot on the case itself.
    public readonly struct PcSlotSpec
    {
        public string SlotId { get; }
        public PcComponentType ComponentType { get; }
        public PcConnector Connector { get; }
        public string HostSlotId { get; }

        public PcSlotSpec(string slotId, PcComponentType componentType, PcConnector connector, string hostSlotId = null)
        {
            SlotId = slotId;
            ComponentType = componentType;
            Connector = connector;
            HostSlotId = hostSlotId;
        }
    }

    public readonly struct PcInstalledComponent
    {
        public string SlotId { get; }
        public string InstanceId { get; }
        public PcComponentSpec Spec { get; }
        public PcComponentType ComponentType => Spec.ComponentType;

        public PcInstalledComponent(string slotId, string instanceId, PcComponentSpec spec)
        {
            SlotId = slotId;
            InstanceId = instanceId;
            Spec = spec;
        }
    }

    [Serializable]
    public sealed class PcAssemblySnapshot
    {
        public int Version;
        public PcInstalledSlotSnapshot[] InstalledSlots = Array.Empty<PcInstalledSlotSnapshot>();
    }

    [Serializable]
    public sealed class PcInstalledSlotSnapshot
    {
        public string SlotId;
        public string ItemInstanceId;
    }

    // Which installed ItemInstance occupies which slot of one PC. The item system owns identity and the coarse
    // location (ItemLocation.Installed); this owns only "slot -> instance", so neither side stores the other's fact.
    // A slot can be mounted on another slot's part (PcSlotSpec.HostSlotId): it is only there while that part is installed,
    // and that part comes out only after every part mounted on it.
    public sealed class PcAssembly
    {
        public const int SnapshotVersion = 1;

        public IReadOnlyList<PcSlotSpec> Slots { get; }

        // In slot layout order.
        public IReadOnlyList<PcInstalledComponent> InstalledComponents { get; }

        public event Action Changed;

        private readonly Dictionary<string, PcSlotSpec> _slots = new(StringComparer.Ordinal);
        private readonly Dictionary<string, PcInstalledComponent> _bySlot = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _slotByInstance = new(StringComparer.Ordinal);
        private readonly List<PcInstalledComponent> _installed = new();

        public PcAssembly(IReadOnlyList<PcSlotSpec> slots)
        {
            if (slots == null)
                throw new ArgumentNullException(nameof(slots));

            List<PcSlotSpec> layout = new(slots.Count);

            for (int i = 0; i < slots.Count; i++)
            {
                PcSlotSpec slot = slots[i];

                if (!IsValidSlotId(slot.SlotId) ||
                    !Enum.IsDefined(typeof(PcComponentType), slot.ComponentType) ||
                    !Enum.IsDefined(typeof(PcConnector), slot.Connector) ||
                    slot.Connector == PcConnector.None)
                {
                    throw new ArgumentException($"PC slot at index {i} is not a valid slot.", nameof(slots));
                }

                if (!_slots.TryAdd(slot.SlotId, slot))
                    throw new ArgumentException($"PC slot ID '{slot.SlotId}' is used twice.", nameof(slots));

                layout.Add(slot);
            }

            for (int i = 0; i < layout.Count; i++)
            {
                if (!HostsEndAtTheCase(layout[i]))
                    throw new ArgumentException($"PC slot '{layout[i].SlotId}' is mounted on a slot this PC does not have, or on itself.", nameof(slots));
            }

            Slots = new ReadOnlyCollection<PcSlotSpec>(layout);
            InstalledComponents = new ReadOnlyCollection<PcInstalledComponent>(_installed);
        }

        public bool TryGetSlot(string slotId, out PcSlotSpec slot)
        {
            slot = default;
            return slotId != null && _slots.TryGetValue(slotId, out slot);
        }

        public bool IsSlotOccupied(string slotId)
        {
            return slotId != null && _bySlot.ContainsKey(slotId);
        }

        // Is the slot there right now? A slot on the case always is; one mounted on another part only while that part is
        // installed (no processor socket without a motherboard).
        public bool IsSlotPresent(string slotId)
        {
            return TryGetSlot(slotId, out PcSlotSpec slot) && (slot.HostSlotId == null || _bySlot.ContainsKey(slot.HostSlotId));
        }

        // The installed parts mounted on this slot's part, in slot layout order: they come out before it does.
        public List<PcInstalledComponent> InstalledOn(string slotId)
        {
            List<PcInstalledComponent> mounted = new();

            for (int i = 0; i < _installed.Count; i++)
            {
                if (_slots[_installed[i].SlotId].HostSlotId == slotId)
                    mounted.Add(_installed[i]);
            }

            return mounted;
        }

        public bool TryGetInstalled(string slotId, out PcInstalledComponent component)
        {
            component = default;
            return slotId != null && _bySlot.TryGetValue(slotId, out component);
        }

        public bool TryFindSlotOf(string instanceId, out string slotId)
        {
            slotId = null;
            return instanceId != null && _slotByInstance.TryGetValue(instanceId, out slotId);
        }

        public bool HasComponent(PcComponentType type)
        {
            return TryGetInstalled(type, out _);
        }

        public bool TryGetInstalled(PcComponentType type, out PcInstalledComponent component)
        {
            for (int i = 0; i < _installed.Count; i++)
            {
                if (_installed[i].ComponentType != type)
                    continue;

                component = _installed[i];
                return true;
            }

            component = default;
            return false;
        }

        // Does this kind of part belong in this kind of slot at all? Occupancy is not considered.
        public static PcSlotCheck CheckCompatibility(PcSlotSpec slot, PcComponentSpec component)
        {
            if (component == null || !component.IsValid)
                return PcSlotCheck.NotPcHardware;

            if (component.ComponentType != slot.ComponentType)
                return PcSlotCheck.WrongComponentType;

            return component.Connector == slot.Connector ? PcSlotCheck.Allowed : PcSlotCheck.WrongConnector;
        }

        // Where could this part go in this PC right now? Allowed comes with the first free compatible slot; HostMissing
        // with the first compatible slot that only lacks the part it is mounted on.
        public PcSlotCheck CheckPart(PcComponentSpec component, out string slotId)
        {
            slotId = null;

            if (component == null || !component.IsValid)
                return PcSlotCheck.NotPcHardware;

            bool occupied = false;
            string waitingForHost = null;

            for (int i = 0; i < Slots.Count; i++)
            {
                string candidate = Slots[i].SlotId;

                if (CheckCompatibility(Slots[i], component) != PcSlotCheck.Allowed)
                    continue;

                if (_bySlot.ContainsKey(candidate))
                {
                    occupied = true;
                }
                else if (!IsSlotPresent(candidate))
                {
                    waitingForHost ??= candidate;
                }
                else
                {
                    slotId = candidate;
                    return PcSlotCheck.Allowed;
                }
            }

            slotId = waitingForHost;
            return waitingForHost != null ? PcSlotCheck.HostMissing : occupied ? PcSlotCheck.SlotOccupied : PcSlotCheck.NoMatchingSlot;
        }

        public PcSlotCheck CheckInstall(string slotId, string instanceId, PcComponentSpec component)
        {
            if (!TryGetSlot(slotId, out PcSlotSpec slot))
                return PcSlotCheck.UnknownSlot;

            PcSlotCheck compatibility = CheckCompatibility(slot, component);

            if (compatibility != PcSlotCheck.Allowed)
                return compatibility;

            if (string.IsNullOrWhiteSpace(instanceId))
                return PcSlotCheck.Unavailable;

            if (_slotByInstance.ContainsKey(instanceId))
                return PcSlotCheck.AlreadyInstalled;

            if (!IsSlotPresent(slotId))
                return PcSlotCheck.HostMissing;

            return _bySlot.ContainsKey(slotId) ? PcSlotCheck.SlotOccupied : PcSlotCheck.Allowed;
        }

        public bool TryRecordInstall(string slotId, string instanceId, PcComponentSpec component)
        {
            if (CheckInstall(slotId, instanceId, component) != PcSlotCheck.Allowed)
                return false;

            _bySlot.Add(slotId, new PcInstalledComponent(slotId, instanceId, component));
            _slotByInstance.Add(instanceId, slotId);
            RebuildInstalledList();

            Changed?.Invoke();
            return true;
        }

        public PcSlotCheck CheckRemove(string slotId)
        {
            if (!TryGetSlot(slotId, out _))
                return PcSlotCheck.UnknownSlot;

            if (!_bySlot.ContainsKey(slotId))
                return PcSlotCheck.SlotEmpty;

            return InstalledOn(slotId).Count > 0 ? PcSlotCheck.MountedPartsInstalled : PcSlotCheck.Allowed;
        }

        public bool TryRecordRemoval(string slotId, out string instanceId)
        {
            instanceId = null;

            if (CheckRemove(slotId) != PcSlotCheck.Allowed)
                return false;

            instanceId = _bySlot[slotId].InstanceId;
            _bySlot.Remove(slotId);
            _slotByInstance.Remove(instanceId);
            RebuildInstalledList();

            Changed?.Invoke();
            return true;
        }

        public PcAssemblySnapshot CaptureSnapshot()
        {
            PcInstalledSlotSnapshot[] slots = new PcInstalledSlotSnapshot[_installed.Count];

            for (int i = 0; i < _installed.Count; i++)
            {
                slots[i] = new PcInstalledSlotSnapshot
                {
                    SlotId = _installed[i].SlotId,
                    ItemInstanceId = _installed[i].InstanceId
                };
            }

            return new PcAssemblySnapshot
            {
                Version = SnapshotVersion,
                InstalledSlots = slots
            };
        }

        // installedItems: every item the save marks ItemLocation.Installed, with its definition's PC component data
        // (null when the definition has none). A valid snapshot puts each of them in exactly one compatible slot of
        // this PC, nothing else anywhere, and no part in a slot whose host part is not installed.
        public bool IsValidSnapshot(PcAssemblySnapshot snapshot, IReadOnlyDictionary<string, PcComponentSpec> installedItems)
        {
            if (snapshot == null ||
                snapshot.Version != SnapshotVersion ||
                snapshot.InstalledSlots == null ||
                installedItems == null ||
                snapshot.InstalledSlots.Length != installedItems.Count)
            {
                return false;
            }

            HashSet<string> slotIds = new(StringComparer.Ordinal);
            HashSet<string> instanceIds = new(StringComparer.Ordinal);

            for (int i = 0; i < snapshot.InstalledSlots.Length; i++)
            {
                PcInstalledSlotSnapshot record = snapshot.InstalledSlots[i];

                if (record == null ||
                    !TryGetSlot(record.SlotId, out PcSlotSpec slot) ||
                    string.IsNullOrWhiteSpace(record.ItemInstanceId) ||
                    !slotIds.Add(record.SlotId) ||
                    !instanceIds.Add(record.ItemInstanceId) ||
                    !installedItems.TryGetValue(record.ItemInstanceId, out PcComponentSpec component) ||
                    CheckCompatibility(slot, component) != PcSlotCheck.Allowed)
                {
                    return false;
                }
            }

            for (int i = 0; i < snapshot.InstalledSlots.Length; i++)
            {
                string host = _slots[snapshot.InstalledSlots[i].SlotId].HostSlotId;

                if (host != null && !slotIds.Contains(host))
                    return false;
            }

            // Same count and every record resolved a distinct installed item: no installed item is left without a slot.
            return true;
        }

        public void Restore(PcAssemblySnapshot snapshot, IReadOnlyDictionary<string, PcComponentSpec> installedItems)
        {
            if (!IsValidSnapshot(snapshot, installedItems))
                throw new ArgumentException("The PC assembly snapshot is not valid for this PC.", nameof(snapshot));

            _bySlot.Clear();
            _slotByInstance.Clear();

            for (int i = 0; i < snapshot.InstalledSlots.Length; i++)
            {
                PcInstalledSlotSnapshot record = snapshot.InstalledSlots[i];

                _bySlot.Add(record.SlotId, new PcInstalledComponent(record.SlotId, record.ItemInstanceId, installedItems[record.ItemInstanceId]));
                _slotByInstance.Add(record.ItemInstanceId, record.SlotId);
            }

            RebuildInstalledList();
            Changed?.Invoke();
        }

        public static bool IsValidSlotId(string value)
        {
            return ItemDefinition.IsValidItemId(value);
        }

        // Following the hosts from this slot reaches the case: every host is a slot of this PC and none is the slot itself.
        private bool HostsEndAtTheCase(PcSlotSpec slot)
        {
            string host = slot.HostSlotId;

            for (int steps = 0; host != null; steps++)
            {
                if (steps >= _slots.Count || host == slot.SlotId || !_slots.TryGetValue(host, out PcSlotSpec hostSlot))
                    return false;

                host = hostSlot.HostSlotId;
            }

            return true;
        }

        private void RebuildInstalledList()
        {
            _installed.Clear();

            for (int i = 0; i < Slots.Count; i++)
            {
                if (_bySlot.TryGetValue(Slots[i].SlotId, out PcInstalledComponent component))
                    _installed.Add(component);
            }
        }
    }
}
