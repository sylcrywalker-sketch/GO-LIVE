using System;
using System.Collections.Generic;
using System.Linq;
using GoLive.Desktop;
using GoLive.PcBuilding;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GoLive.Tests
{
    public sealed class PcSessionTests
    {
        private readonly List<Object> _created = new();
        private PcAssembly _assembly;
        private PcSession _session;

        [SetUp]
        public void SetUp()
        {
            _assembly = new PcAssembly(new[]
            {
                new PcSlotSpec("board", PcComponentType.Motherboard, PcConnector.MotherboardTray),
                new PcSlotSpec("cpu", PcComponentType.Cpu, PcConnector.CpuSocket),
                new PcSlotSpec("ram", PcComponentType.Ram, PcConnector.MemorySlot),
                new PcSlotSpec("psu", PcComponentType.Psu, PcConnector.PowerSupplyBay),
                new PcSlotSpec("drive", PcComponentType.Storage, PcConnector.SataStorage)
            });
            Install("board", PcComponentType.Motherboard, PcConnector.MotherboardTray, 25);
            Install("cpu", PcComponentType.Cpu, PcConnector.CpuSocket, 65);
            Install("ram", PcComponentType.Ram, PcConnector.MemorySlot, 3);
            Install("psu", PcComponentType.Psu, PcConnector.PowerSupplyBay, 0, 300);
            Install("drive", PcComponentType.Storage, PcConnector.SataStorage, 6);
            _session = new PcSession();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object item in _created) Object.DestroyImmediate(item);
            _created.Clear();
        }

        [Test]
        public void NewSessionHasNoPowerMonitorOrSeatOwnership()
        {
            Assert.That(_session.Power, Is.EqualTo(PcPowerState.Off));
            Assert.That(_session.Usage, Is.EqualTo(PcUsageState.Standing));
            Assert.That(_session.MonitorOn || _session.ScreenActive, Is.False);
        }

        [Test]
        public void StarterWithoutGpuBootsInOneAndAHalfSeconds()
        {
            Assert.That(_session.TryPowerOn(Capabilities), Is.True);
            Assert.That(_session.Power, Is.EqualTo(PcPowerState.Booting));
            Assert.That(_session.ScreenActive, Is.False, "monitor power is independent");
            _session.ToggleMonitor();
            _session.Tick(1.49f);
            Assert.That(_session.Power, Is.EqualTo(PcPowerState.Booting));
            Assert.That(_session.ScreenActive, Is.False, "the desktop cannot focus during boot");
            _session.Tick(0.02f);
            Assert.That(_session.Power, Is.EqualTo(PcPowerState.Running));
            Assert.That(_session.ScreenActive, Is.True);
        }

        [TestCase("ram", PcDiagnosticCode.MissingMemory)]
        [TestCase("drive", PcDiagnosticCode.MissingStorage)]
        [TestCase("psu", PcDiagnosticCode.MissingPowerSupply)]
        public void MissingDesktopHardwareRejectsPowerWithExistingDiagnostics(string slot, PcDiagnosticCode diagnostic)
        {
            _assembly.TryRecordRemoval(slot, out _);
            Assert.That(_session.TryPowerOn(Capabilities), Is.False);
            Assert.That(_session.Power, Is.EqualTo(PcPowerState.Off));
            Assert.That(_session.LastPowerOnDiagnostics.Any(value => value.Code == diagnostic), Is.True);
        }

        [Test]
        public void SittingAtAnOffMonitorDoesNotTurnAnythingOn()
        {
            Assert.That(_session.Sit(), Is.True);
            Assert.That(_session.Sit(), Is.False);
            Assert.That(_session.Usage, Is.EqualTo(PcUsageState.Seated));
            Assert.That(_session.Focus(), Is.False);
            Assert.That(_session.Power, Is.EqualTo(PcPowerState.Off));
            Assert.That(_session.MonitorOn, Is.False);
        }

        [Test]
        public void FocusRequiresSeatAndRunningVisibleDesktop()
        {
            Boot();
            Assert.That(_session.Focus(), Is.False);
            _session.Sit();
            Assert.That(_session.Focus(), Is.False);
            _session.ToggleMonitor();
            Assert.That(_session.Focus(), Is.True);
            Assert.That(_session.Focus(), Is.False);
            Assert.That(_session.Usage, Is.EqualTo(PcUsageState.Focused));
        }

        [Test]
        public void BackConsumesFocusThenSeatAndNothingWhenStanding()
        {
            Focus();
            Assert.That(_session.Back(), Is.True);
            Assert.That(_session.Usage, Is.EqualTo(PcUsageState.Seated));
            Assert.That(_session.Back(), Is.True);
            Assert.That(_session.Usage, Is.EqualTo(PcUsageState.Standing));
            Assert.That(_session.Back(), Is.False);
            Assert.That(_session.Power, Is.EqualTo(PcPowerState.Running));
        }

        [Test]
        public void MonitorOffUnfocusesWithoutPoweringDownAndNeverRefocusesByItself()
        {
            Focus();
            _session.ToggleMonitor();
            Assert.That(_session.Usage, Is.EqualTo(PcUsageState.Seated));
            Assert.That(_session.Power, Is.EqualTo(PcPowerState.Running));
            Assert.That(_session.ScreenActive, Is.False);
            _session.ToggleMonitor();
            Assert.That(_session.ScreenActive, Is.True);
            Assert.That(_session.Usage, Is.EqualTo(PcUsageState.Seated));
        }

        [Test]
        public void PowerOffUnfocusesButPreservesSeatAndMonitorPower()
        {
            Focus();
            _session.PowerOff();
            Assert.That(_session.Power, Is.EqualTo(PcPowerState.Off));
            Assert.That(_session.Usage, Is.EqualTo(PcUsageState.Seated));
            Assert.That(_session.MonitorOn, Is.True);
            Assert.That(_session.ScreenActive, Is.False);
        }

        [Test]
        public void RepeatedPowerRequestsCannotRestartTheBootTimer()
        {
            Assert.That(_session.TryPowerOn(Capabilities), Is.True);
            _session.Tick(1f);
            Assert.That(_session.TryPowerOn(Capabilities), Is.False);
            _session.Tick(0.5f);
            Assert.That(_session.Power, Is.EqualTo(PcPowerState.Running));
            Assert.That(_session.TryPowerOn(Capabilities), Is.False);
        }

        [Test]
        public void CancelledBootDoesNotFinishLaterAndNewBootGetsItsWholeDuration()
        {
            _session.TryPowerOn(Capabilities);
            _session.Tick(1f);
            _session.PowerOff();
            _session.Tick(10f);
            Assert.That(_session.Power, Is.EqualTo(PcPowerState.Off));
            _session.TryPowerOn(Capabilities);
            _session.Tick(0.6f);
            Assert.That(_session.Power, Is.EqualTo(PcPowerState.Booting));
        }

        [Test]
        public void HardwareLossStopsBootAndRunningDesktopImmediately()
        {
            Focus();
            _assembly.TryRecordRemoval("drive", out _);
            _session.HardwareChanged(Capabilities);
            Assert.That(_session.Power, Is.EqualTo(PcPowerState.Off));
            Assert.That(_session.Usage, Is.EqualTo(PcUsageState.Seated));
            Assert.That(_session.MonitorOn, Is.True);
            Assert.That(_session.LastPowerOnDiagnostics.Any(value => value.Code == PcDiagnosticCode.MissingStorage), Is.True);
        }

        [Test]
        public void ChangesNotifyExactlyOnceForEachAcceptedTransition()
        {
            int count = 0;
            _session.Changed += () => count++;
            _session.PowerOff();
            _session.Back();
            _session.Focus();
            _session.Sit();
            _session.Sit();
            _session.ToggleMonitor();
            _session.TryPowerOn(Capabilities);
            _session.Tick(0.5f);
            Assert.That(count, Is.EqualTo(3));
            _session.Tick(1f);
            _session.Focus();
            _session.PowerOff();
            Assert.That(count, Is.EqualTo(6), "power and focus release notify atomically");
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(-1f)]
        public void InvalidTimeCannotFinishOrPoisonBoot(float seconds)
        {
            _session.TryPowerOn(Capabilities);
            _session.Tick(seconds);
            Assert.That(_session.Power, Is.EqualTo(PcPowerState.Booting));
            _session.Tick(1.5f);
            Assert.That(_session.Power, Is.EqualTo(PcPowerState.Running));
        }

        [Test]
        public void ResetReleasesEveryTransientStateAndCanBeRepeated()
        {
            for (int i = 0; i < 5; i++)
            {
                Focus();
                _session.Reset();
                _session.Reset();
                Assert.That(_session.Power, Is.EqualTo(PcPowerState.Off));
                Assert.That(_session.Usage, Is.EqualTo(PcUsageState.Standing));
                Assert.That(_session.MonitorOn, Is.False);
            }
        }

        private PcCapabilities Capabilities => PcCapabilities.Evaluate(_assembly);
        private void Boot() { Assert.That(_session.TryPowerOn(Capabilities), Is.True); _session.Tick(1.5f); }
        private void Focus() { Boot(); _session.ToggleMonitor(); _session.Sit(); Assert.That(_session.Focus(), Is.True); }

        private void Install(string slot, PcComponentType type, PcConnector connector, int watts, int capacity = 0)
        {
            PcComponentSpec spec = ScriptableObject.CreateInstance<PcComponentSpec>();
            ShopTestData.Set(spec, "componentType", type);
            ShopTestData.Set(spec, "connector", connector);
            ShopTestData.Set(spec, "powerDrawWatts", watts);
            ShopTestData.Set(spec, "powerCapacityWatts", capacity);
            _created.Add(spec);
            Assert.That(_assembly.TryRecordInstall(slot, slot, spec), Is.True);
        }
    }
}
