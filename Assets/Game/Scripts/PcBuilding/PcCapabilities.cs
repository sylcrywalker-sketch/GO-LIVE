using System;
using System.Collections.Generic;

namespace GoLive.PcBuilding
{
    public enum PcDiagnosticSeverity
    {
        // The PC works, but something it could do is limited.
        Limitation,

        // Something stops working entirely: PcDiagnostic.Affects says what.
        Blocker
    }

    // What a PC does, in the order it gets there: it powers on, then reaches a usable desktop, then runs games.
    // A diagnostic names the first of these it stops or limits; everything after it is affected too.
    public enum PcFunction
    {
        PowerOn,
        Desktop,
        Gaming
    }

    // Declaration order is the order diagnostics are reported in: what stops power, what stops the desktop, then
    // what only limits the PC.
    public enum PcDiagnosticCode
    {
        MissingMotherboard,
        MissingCpu,
        MissingMemory,
        MissingPowerSupply,
        InsufficientPower,
        MissingStorage,
        NoDedicatedGpu
    }

    // One human-readable finding about the PC: the part it is about, a short title and one line of detail as
    // localization keys. The detail of InsufficientPower is a format string that takes {0} = RequiredWatts and
    // {1} = AvailableWatts.
    public readonly struct PcDiagnostic
    {
        public PcDiagnosticCode Code { get; }
        public PcDiagnosticSeverity Severity { get; }
        public PcFunction Affects { get; }

        // The missing part, or the part that falls short (the power supply for InsufficientPower).
        public PcComponentType Component { get; }
        public string TitleKey { get; }
        public string DetailKey { get; }

        // Only InsufficientPower carries watts; every other diagnostic has 0 for both.
        public int RequiredWatts { get; }
        public int AvailableWatts { get; }

        private PcDiagnostic(PcDiagnosticCode code, PcDiagnosticSeverity severity, PcFunction affects, PcComponentType component, string token, int requiredWatts, int availableWatts)
        {
            Code = code;
            Severity = severity;
            Affects = affects;
            Component = component;
            TitleKey = $"pc.diagnostic.{token}.title";
            DetailKey = $"pc.diagnostic.{token}.detail";
            RequiredWatts = requiredWatts;
            AvailableWatts = availableWatts;
        }

        // The one table of what each code means.
        public static PcDiagnostic For(PcDiagnosticCode code, int requiredWatts = 0, int availableWatts = 0)
        {
            return code switch
            {
                PcDiagnosticCode.MissingMotherboard => new PcDiagnostic(code, PcDiagnosticSeverity.Blocker, PcFunction.PowerOn, PcComponentType.Motherboard, "no_motherboard", 0, 0),
                PcDiagnosticCode.MissingCpu => new PcDiagnostic(code, PcDiagnosticSeverity.Blocker, PcFunction.PowerOn, PcComponentType.Cpu, "no_cpu", 0, 0),
                PcDiagnosticCode.MissingMemory => new PcDiagnostic(code, PcDiagnosticSeverity.Blocker, PcFunction.PowerOn, PcComponentType.Ram, "no_memory", 0, 0),
                PcDiagnosticCode.MissingPowerSupply => new PcDiagnostic(code, PcDiagnosticSeverity.Blocker, PcFunction.PowerOn, PcComponentType.Psu, "no_psu", 0, 0),
                PcDiagnosticCode.InsufficientPower => new PcDiagnostic(code, PcDiagnosticSeverity.Blocker, PcFunction.PowerOn, PcComponentType.Psu, "weak_psu", requiredWatts, availableWatts),
                PcDiagnosticCode.MissingStorage => new PcDiagnostic(code, PcDiagnosticSeverity.Blocker, PcFunction.Desktop, PcComponentType.Storage, "no_storage", 0, 0),
                PcDiagnosticCode.NoDedicatedGpu => new PcDiagnostic(code, PcDiagnosticSeverity.Limitation, PcFunction.Gaming, PcComponentType.Gpu, "no_gpu", 0, 0),
                _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Unknown PC diagnostic.")
            };
        }
    }

    // What the assembled PC can do, derived from its installed hardware and nothing else: a value computed on demand
    // from the PcAssembly, never cached, so it cannot go stale after an install or removal. Internal hardware only;
    // Internet, peripherals and streaming are external setup and join on top of this later.
    public readonly struct PcCapabilities
    {
        public bool HasMotherboard { get; }
        public bool HasCpu { get; }
        public bool HasMemory { get; }
        public bool HasPowerSupply { get; }
        public bool HasStorage { get; }
        public bool HasDedicatedGpu { get; }

        // What every installed part draws together; a power supply draws nothing and provides the capacity instead.
        public int TotalPowerDrawWatts { get; }
        public int PowerSupplyCapacityWatts { get; }
        public bool HasEnoughPower => HasPowerSupply && TotalPowerDrawWatts <= PowerSupplyCapacityWatts;

        // It starts electrically. Storage is not needed for that, a dedicated graphics card neither.
        public bool CanPowerOn => HasMotherboard && HasCpu && HasMemory && HasEnoughPower;

        // It starts and boots an operating system from its storage.
        public bool CanUseDesktop => CanPowerOn && HasStorage;

        // Games and streams get a real graphics card instead of the processor's basic graphics.
        public bool GamingGraphicsAvailable => CanPowerOn && HasDedicatedGpu;

        // The hardware side of Critical Strike; network and peripheral requirements are layered on later.
        public bool CanPlayCriticalStrike => CanUseDesktop && GamingGraphicsAvailable;

        // At most one per code, in PcDiagnosticCode order.
        public IReadOnlyList<PcDiagnostic> Diagnostics { get; }

        private PcCapabilities(int motherboards, int cpus, int memoryModules, int powerSupplies, int storageDevices, int gpus, int drawWatts, int capacityWatts)
        {
            HasMotherboard = motherboards > 0;
            HasCpu = cpus > 0;
            HasMemory = memoryModules > 0;
            HasPowerSupply = powerSupplies > 0;
            HasStorage = storageDevices > 0;
            HasDedicatedGpu = gpus > 0;
            TotalPowerDrawWatts = drawWatts;
            PowerSupplyCapacityWatts = capacityWatts;
            Diagnostics = Diagnose(HasMotherboard, HasCpu, HasMemory, HasPowerSupply, drawWatts <= capacityWatts, HasStorage, HasDedicatedGpu, drawWatts, capacityWatts);
        }

        public bool Has(PcDiagnosticCode code)
        {
            for (int i = 0; i < Diagnostics.Count; i++)
            {
                if (Diagnostics[i].Code == code)
                    return true;
            }

            return false;
        }

        public static PcCapabilities Evaluate(PcAssembly assembly)
        {
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));

            int motherboards = 0, cpus = 0, memoryModules = 0, powerSupplies = 0, storageDevices = 0, gpus = 0;
            int drawWatts = 0, capacityWatts = 0;
            IReadOnlyList<PcInstalledComponent> installed = assembly.InstalledComponents;

            for (int i = 0; i < installed.Count; i++)
            {
                PcComponentSpec spec = installed[i].Spec;

                drawWatts += spec.PowerDrawWatts;
                capacityWatts += spec.PowerCapacityWatts;

                switch (spec.ComponentType)
                {
                    case PcComponentType.Motherboard: motherboards++; break;
                    case PcComponentType.Cpu: cpus++; break;
                    case PcComponentType.Ram: memoryModules++; break;
                    case PcComponentType.Psu: powerSupplies++; break;
                    case PcComponentType.Storage: storageDevices++; break;
                    case PcComponentType.Gpu: gpus++; break;
                }
            }

            return new PcCapabilities(motherboards, cpus, memoryModules, powerSupplies, storageDevices, gpus, drawWatts, capacityWatts);
        }

        private static IReadOnlyList<PcDiagnostic> Diagnose(bool motherboard, bool cpu, bool memory, bool powerSupply, bool powerFits, bool storage, bool gpu, int drawWatts, int capacityWatts)
        {
            List<PcDiagnostic> diagnostics = new();

            if (!motherboard)
                diagnostics.Add(PcDiagnostic.For(PcDiagnosticCode.MissingMotherboard));

            if (!cpu)
                diagnostics.Add(PcDiagnostic.For(PcDiagnosticCode.MissingCpu));

            if (!memory)
                diagnostics.Add(PcDiagnostic.For(PcDiagnosticCode.MissingMemory));

            if (!powerSupply)
                diagnostics.Add(PcDiagnostic.For(PcDiagnosticCode.MissingPowerSupply));
            else if (!powerFits)
                diagnostics.Add(PcDiagnostic.For(PcDiagnosticCode.InsufficientPower, drawWatts, capacityWatts));

            if (!storage)
                diagnostics.Add(PcDiagnostic.For(PcDiagnosticCode.MissingStorage));

            if (!gpu)
                diagnostics.Add(PcDiagnostic.For(PcDiagnosticCode.NoDedicatedGpu));

            return diagnostics.AsReadOnly();
        }
    }
}
