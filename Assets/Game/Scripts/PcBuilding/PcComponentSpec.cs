using System;
using UnityEngine;

namespace GoLive.PcBuilding
{
    // Values are serialized in data assets and scenes: append new ones, never renumber.
    public enum PcComponentType
    {
        Motherboard = 0,
        Cpu = 1,
        CpuCooler = 2,
        Ram = 3,
        Gpu = 4,
        Psu = 5,
        Storage = 6,
        CaseFan = 7
    }

    // The physical interface a component plugs into or mounts on; a slot accepts exactly one. Deliberately generic
    // (a CPU socket, a memory slot, a SATA drive bay): the project does not model real socket standards or memory
    // generations until parts that differ in them exist. Serialized: append new values, never renumber.
    public enum PcConnector
    {
        None = 0,
        PcieX16 = 1,
        MotherboardTray = 2,
        PowerSupplyBay = 3,
        CpuSocket = 4,
        MemorySlot = 5,
        SataStorage = 6
    }

    public static class PcComponentTypeExtensions
    {
        // One install/remove prompt per component type ("pc.install.gpu"), so each language can inflect the part name.
        public static string InstallPromptKey(this PcComponentType type) => "pc.install." + Token(type);
        public static string RemovePromptKey(this PcComponentType type) => "pc.remove." + Token(type);
        public static string NameKey(this PcComponentType type) => "pc.component." + Token(type);

        // The part's name inside a sentence, as the thing acted on: "Сначала снимите {0}" / "Remove {0} first".
        public static string ObjectNameKey(this PcComponentType type) => "pc.component." + Token(type) + ".object";

        private static string Token(PcComponentType type)
        {
            return type switch
            {
                PcComponentType.Motherboard => "motherboard",
                PcComponentType.Cpu => "cpu",
                PcComponentType.CpuCooler => "cpu_cooler",
                PcComponentType.Ram => "ram",
                PcComponentType.Gpu => "gpu",
                PcComponentType.Psu => "psu",
                PcComponentType.Storage => "storage",
                _ => "case_fan"
            };
        }
    }

    // PC hardware data an ItemDefinition can point to. Everything that is not PC hardware has none. Read-only at
    // runtime: what is installed where is the PcAssembly's state, never this asset's.
    [CreateAssetMenu(fileName = "PcComponentSpec", menuName = "GO! LIVE/PC Building/Component Spec")]
    public sealed class PcComponentSpec : ScriptableObject
    {
        [SerializeField] private PcComponentType componentType;
        [SerializeField] private PcConnector connector;

        [Tooltip("Power the part draws under load; the installed parts' total must fit the power supply's capacity. 0 for a power supply.")]
        [SerializeField, Min(0)] private int powerDrawWatts;

        [Tooltip("Only a power supply: the load it can carry. 0 for every other part.")]
        [SerializeField, Min(0)] private int powerCapacityWatts;

        public PcComponentType ComponentType => componentType;
        public PcConnector Connector => connector;
        public int PowerDrawWatts => powerDrawWatts;
        public int PowerCapacityWatts => powerCapacityWatts;

        public bool IsValid => ValidationError == null;

        // Why this authored data is impossible, for content tests and logs; null when it is valid.
        public string ValidationError => Validate(componentType, connector, powerDrawWatts, powerCapacityWatts);

        public static string Validate(PcComponentType type, PcConnector connector, int powerDrawWatts, int powerCapacityWatts)
        {
            if (!Enum.IsDefined(typeof(PcComponentType), type))
                return "has an undefined component type";

            if (!Enum.IsDefined(typeof(PcConnector), connector) || connector == PcConnector.None)
                return "needs a defined connector";

            if (powerDrawWatts < 0 || powerCapacityWatts < 0)
                return "has negative wattage";

            if (type != PcComponentType.Psu)
                return powerCapacityWatts == 0 ? null : "is not a power supply but declares a power capacity";

            if (powerCapacityWatts == 0)
                return "is a power supply without a power capacity";

            return powerDrawWatts == 0 ? null : "is a power supply that declares its own power draw";
        }
    }
}
