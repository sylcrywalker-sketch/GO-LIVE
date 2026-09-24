using System;

namespace GoLive.PcBuilding
{
    public enum PcPowerOnOutcome
    {
        // The PC stays dark: a part it needs to start is missing or the power supply cannot carry the load.
        Blocked,

        // The PC starts. Whether it also reaches a usable desktop is ReachesDesktop.
        Started
    }

    // The answer to pressing the PC's power button, from its installed hardware at that moment. Stateless on purpose:
    // nothing in the game keeps a running PC yet, so there is no on/off state to own or save. The future Desktop asks
    // this when the player powers the PC on and owns its own running state.
    public readonly struct PcPowerOnResult
    {
        public PcPowerOnOutcome Outcome { get; }
        public PcCapabilities Capabilities { get; }
        public bool Started => Outcome == PcPowerOnOutcome.Started;
        public bool ReachesDesktop => Started && Capabilities.CanUseDesktop;

        private PcPowerOnResult(PcCapabilities capabilities)
        {
            Capabilities = capabilities;
            Outcome = capabilities.CanPowerOn ? PcPowerOnOutcome.Started : PcPowerOnOutcome.Blocked;
        }

        public static PcPowerOnResult Attempt(PcAssembly assembly)
        {
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));

            return new PcPowerOnResult(PcCapabilities.Evaluate(assembly));
        }
    }
}
