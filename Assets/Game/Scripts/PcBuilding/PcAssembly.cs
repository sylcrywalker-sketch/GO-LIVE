using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using GoLive.Items;

namespace GoLive.PcBuilding
{
    // Why a component can or cannot go into (or come out of) a slot right now. Each value has exactly one
    // localized message (MessageKey), so no screen builds its own error strings.
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

        // The installed part is fixed in this PC for now (PcComponentSlot.IsFixed): it can't be taken out.
        FixedInPlace,
        NoMatchingSlot,
        NothingInHands,
        HandsBusy,
        Unavailable
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
                PcSlotCheck.FixedInPlace => "pc.reject.fixed",
                PcSlotCheck.NoMatchingSlot => "pc.reject.no_matching_slot",
                PcSlotCheck.NothingInHands => "pc.reject.nothing_in_hands",
                PcSlotCheck.HandsBusy => "pc.reject.hands_busy",
                _ => "pc.reject.unavailable"
            };
        }
    }

    // One slot as the assembly rules see it. Authored on a PcComponentSlot in the PC prefab.
    public readonly struct PcSlotSpec
    {
        public string SlotId { get; }
        public PcComponentType ComponentType { get; }
        public PcConnector Connector { get; }

        public PcSlotSpec(string slotId, PcComponentType componentType, PcConnector connector)
        {
            SlotId = slotId;
            ComponentType = componentType;
            Connector = connector;
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

        // Where could this part go in this PC right now? Allowed comes with the first free compatible slot.
        public PcSlotCheck CheckPart(PcComponentSpec component, out string freeSlotId)
        {
            freeSlotId = null;

            if (component == null || !component.IsValid)
                return PcSlotCheck.NotPcHardware;

            bool fits = false;

            for (int i = 0; i < Slots.Count; i++)
            {
                if (CheckCompatibility(Slots[i], component) != PcSlotCheck.Allowed)
                    continue;

                fits = true;

                if (_bySlot.ContainsKey(Slots[i].SlotId))
                    continue;

                freeSlotId = Slots[i].SlotId;
                return PcSlotCheck.Allowed;
            }

            return fits ? PcSlotCheck.SlotOccupied : PcSlotCheck.NoMatchingSlot;
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

            return _bySlot.ContainsKey(slotId) ? PcSlotCheck.Allowed : PcSlotCheck.SlotEmpty;
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
        // this PC and nothing else anywhere.
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
