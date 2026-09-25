using System;
using System.Collections.Generic;
using GoLive.PcBuilding;

namespace GoLive.Desktop
{
    public enum PcPowerState { Off, Booting, Running }
    public enum PcUsageState { Standing, Seated, Focused }

    public sealed class PcSession
    {
        private const float BootSeconds = 1.5f;
        private float _bootRemaining;

        public PcPowerState Power { get; private set; }
        public PcUsageState Usage { get; private set; }
        public bool MonitorOn { get; private set; }
        public bool ScreenActive => MonitorOn && Power == PcPowerState.Running;
        public IReadOnlyList<PcDiagnostic> LastPowerOnDiagnostics { get; private set; } = Array.Empty<PcDiagnostic>();
        public event Action Changed;

        public bool TryPowerOn(PcCapabilities capabilities)
        {
            if (Power != PcPowerState.Off)
                return false;

            LastPowerOnDiagnostics = capabilities.Diagnostics ?? Array.Empty<PcDiagnostic>();
            if (!capabilities.CanUseDesktop)
                return false;

            _bootRemaining = BootSeconds;
            Power = PcPowerState.Booting;
            Changed?.Invoke();
            return true;
        }

        public void PowerOff()
        {
            if (Power == PcPowerState.Off)
                return;

            Power = PcPowerState.Off;
            _bootRemaining = 0f;
            if (Usage == PcUsageState.Focused)
                Usage = PcUsageState.Seated;
            Changed?.Invoke();
        }

        public void ToggleMonitor()
        {
            MonitorOn = !MonitorOn;
            if (!MonitorOn && Usage == PcUsageState.Focused)
                Usage = PcUsageState.Seated;
            Changed?.Invoke();
        }

        public bool Sit()
        {
            if (Usage != PcUsageState.Standing)
                return false;
            Usage = PcUsageState.Seated;
            Changed?.Invoke();
            return true;
        }

        public bool Focus()
        {
            if (Usage != PcUsageState.Seated || !ScreenActive)
                return false;
            Usage = PcUsageState.Focused;
            Changed?.Invoke();
            return true;
        }

        public bool Back()
        {
            if (Usage == PcUsageState.Standing)
                return false;
            Usage = Usage == PcUsageState.Focused ? PcUsageState.Seated : PcUsageState.Standing;
            Changed?.Invoke();
            return true;
        }

        public void Reset()
        {
            bool changed = Power != PcPowerState.Off || Usage != PcUsageState.Standing || MonitorOn;
            Power = PcPowerState.Off;
            Usage = PcUsageState.Standing;
            MonitorOn = false;
            _bootRemaining = 0f;
            LastPowerOnDiagnostics = Array.Empty<PcDiagnostic>();
            if (changed) Changed?.Invoke();
        }

        public void Tick(float seconds)
        {
            if (Power != PcPowerState.Booting || float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0f)
                return;
            _bootRemaining -= seconds;
            if (_bootRemaining > 0f)
                return;
            _bootRemaining = 0f;
            Power = PcPowerState.Running;
            Changed?.Invoke();
        }

        public void HardwareChanged(PcCapabilities capabilities)
        {
            if (Power == PcPowerState.Off || capabilities.CanUseDesktop)
                return;
            LastPowerOnDiagnostics = capabilities.Diagnostics ?? Array.Empty<PcDiagnostic>();
            PowerOff();
        }
    }
}
