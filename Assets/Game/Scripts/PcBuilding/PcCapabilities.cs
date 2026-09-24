using System;
using System.Collections.Generic;

namespace GoLive.PcBuilding
{
    public enum PcDiagnosticSeverity
    {
        // The PC works, but something it could do is limited.
        Limitation,

        // The PC cannot do what the player expects (for example, start).
        Blocking
    }

    public enum PcDiagnosticCode
    {
        NoDedicatedGpu
    }

    // One human-readable finding about the PC, as localization keys: a short title and one line of detail.
    public readonly struct PcDiagnostic
    {
        public PcDiagnosticCode Code { get; }
        public PcDiagnosticSeverity Severity { get; }
        public string TitleKey { get; }
        public string DetailKey { get; }

        public PcDiagnostic(PcDiagnosticCode code, PcDiagnosticSeverity severity, string titleKey, string detailKey)
        {
            Code = code;
            Severity = severity;
            TitleKey = titleKey;
            DetailKey = detailKey;
        }
    }

    // What the assembled PC can do, derived from its installed hardware and nothing else. Only hardware the assembly
    // owns authoritatively is judged: today that is the graphics card. CPU, memory, power supply and storage join
    // here (CanPowerOn, CanUseDesktop, CanStream, ...) when they become real installed items; until then this
    // layer makes no claim about them.
    public readonly struct PcCapabilities
    {
        public bool HasDedicatedGpu { get; }

        // Games and streams get the graphics card's quality. A later GPU tier/driver/power rule narrows this.
        public bool GamingGraphicsAvailable { get; }

        public IReadOnlyList<PcDiagnostic> Diagnostics { get; }

        private PcCapabilities(bool hasDedicatedGpu, IReadOnlyList<PcDiagnostic> diagnostics)
        {
            HasDedicatedGpu = hasDedicatedGpu;
            GamingGraphicsAvailable = hasDedicatedGpu;
            Diagnostics = diagnostics;
        }

        public static PcCapabilities Evaluate(PcAssembly assembly)
        {
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));

            bool hasGpu = assembly.HasComponent(PcComponentType.Gpu);

            PcDiagnostic[] diagnostics = hasGpu
                ? Array.Empty<PcDiagnostic>()
                : new[]
                {
                    new PcDiagnostic(PcDiagnosticCode.NoDedicatedGpu, PcDiagnosticSeverity.Limitation, "pc.diagnostic.no_gpu.title", "pc.diagnostic.no_gpu.detail")
                };

            return new PcCapabilities(hasGpu, diagnostics);
        }
    }
}
