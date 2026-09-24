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

    // The physical interface a component plugs into or mounts on; a slot accepts exactly one. CPU sockets, memory
    // generations, SATA and fan mounts join here when their components become real items.
    public enum PcConnector
    {
        None = 0,
        PcieX16 = 1,
        MotherboardTray = 2,
        PowerSupplyBay = 3
    }

    public static class PcComponentTypeExtensions
    {
        // One install/remove prompt per component type ("pc.install.gpu"), so each language can inflect the part name.
        public static string InstallPromptKey(this PcComponentType type) => "pc.install." + Token(type);
        public static string RemovePromptKey(this PcComponentType type) => "pc.remove." + Token(type);
        public static string NameKey(this PcComponentType type) => "pc.component." + Token(type);

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

    // PC hardware data an ItemDefinition can point to. Everything that is not PC hardware has none.
    [CreateAssetMenu(fileName = "PcComponentSpec", menuName = "GO! LIVE/PC Building/Component Spec")]
    public sealed class PcComponentSpec : ScriptableObject
    {
        [SerializeField] private PcComponentType componentType;
        [SerializeField] private PcConnector connector;

        [Tooltip("Power the part draws under load. Read by the future power-supply budget, not by slot compatibility.")]
        [SerializeField, Min(0)] private int powerDrawWatts;

        public PcComponentType ComponentType => componentType;
        public PcConnector Connector => connector;
        public int PowerDrawWatts => powerDrawWatts;

        public bool IsValid =>
            Enum.IsDefined(typeof(PcComponentType), componentType) &&
            Enum.IsDefined(typeof(PcConnector), connector) &&
            connector != PcConnector.None &&
            powerDrawWatts >= 0;
    }
}
