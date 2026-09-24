using System.Collections.Generic;
using System.Linq;
using GoLive.PcBuilding;
using NUnit.Framework;
using UnityEngine;

namespace GoLive.Tests
{
    // What a PC can do from its installed hardware alone: plain C# over PcAssembly, no scene. The layout mirrors the
    // Student PC (one part per slot, two memory slots so a second module can be tested); the parts are test data.
    public sealed class PcCapabilitiesTests
    {
        private const int BoardWatts = 25;
        private const int CpuWatts = 65;
        private const int RamWatts = 3;
        private const int DriveWatts = 6;
        private const int GpuWatts = 75;
        private const int StarterDraw = BoardWatts + CpuWatts + RamWatts + DriveWatts;

        private readonly List<Object> _created = new();

        private PcComponentSpec _board;
        private PcComponentSpec _cpu;
        private PcComponentSpec _ram;
        private PcComponentSpec _drive;
        private PcComponentSpec _gpu;
        private PcComponentSpec _psu;
        private PcComponentSpec _weakPsu;

        [SetUp]
        public void SetUp()
        {
            _board = Part(PcComponentType.Motherboard, PcConnector.MotherboardTray, BoardWatts);
            _cpu = Part(PcComponentType.Cpu, PcConnector.CpuSocket, CpuWatts);
            _ram = Part(PcComponentType.Ram, PcConnector.MemorySlot, RamWatts);
            _drive = Part(PcComponentType.Storage, PcConnector.SataStorage, DriveWatts);
            _gpu = Part(PcComponentType.Gpu, PcConnector.PcieX16, GpuWatts);
            _psu = Part(PcComponentType.Psu, PcConnector.PowerSupplyBay, 0, 300);
            _weakPsu = Part(PcComponentType.Psu, PcConnector.PowerSupplyBay, 0, 150);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created)
                Object.DestroyImmediate(created);

            _created.Clear();
        }

        // 1. How Day 1 begins: everything but the graphics card.
        [Test]
        public void CompleteStarterInternalsWithoutAGraphicsCardStartAndReachTheDesktopButCannotGame()
        {
            PcCapabilities pc = PcCapabilities.Evaluate(StarterPc());

            Assert.That(pc.HasMotherboard && pc.HasCpu && pc.HasMemory && pc.HasPowerSupply && pc.HasStorage, Is.True);
            Assert.That(pc.HasDedicatedGpu, Is.False);
            Assert.That(pc.CanPowerOn, Is.True, "no graphics card never stops the PC from starting");
            Assert.That(pc.CanUseDesktop, Is.True);
            Assert.That(pc.GamingGraphicsAvailable, Is.False);
            Assert.That(pc.CanPlayCriticalStrike, Is.False);

            PcDiagnostic limitation = pc.Diagnostics.Single();
            Assert.That(limitation.Code, Is.EqualTo(PcDiagnosticCode.NoDedicatedGpu));
            Assert.That(limitation.Severity, Is.EqualTo(PcDiagnosticSeverity.Limitation));
            Assert.That(limitation.Affects, Is.EqualTo(PcFunction.Gaming));
        }

        // 2-5. Each part the PC needs to start.
        [TestCase("motherboard-0", PcDiagnosticCode.MissingMotherboard)]
        [TestCase("cpu-0", PcDiagnosticCode.MissingCpu)]
        [TestCase("ram-0", PcDiagnosticCode.MissingMemory)]
        [TestCase("psu-0", PcDiagnosticCode.MissingPowerSupply)]
        public void AMissingPartThePcNeedsToStartBlocksPowerOn(string slotId, PcDiagnosticCode expected)
        {
            PcAssembly assembly = StarterPc();
            Assert.That(assembly.TryRecordRemoval(slotId, out _), Is.True);

            PcCapabilities pc = PcCapabilities.Evaluate(assembly);

            Assert.That(pc.CanPowerOn, Is.False);
            Assert.That(pc.CanUseDesktop, Is.False);
            Assert.That(pc.CanPlayCriticalStrike, Is.False);
            Assert.That(pc.Has(expected), Is.True);

            PcDiagnostic blocker = pc.Diagnostics.First();
            Assert.That(blocker.Code, Is.EqualTo(expected), "the reason it cannot start comes first");
            Assert.That(blocker.Severity, Is.EqualTo(PcDiagnosticSeverity.Blocker));
            Assert.That(blocker.Affects, Is.EqualTo(PcFunction.PowerOn));
        }

        [Test]
        public void WithoutAPowerSupplyThereIsNoPowerBudgetToExceed()
        {
            PcAssembly assembly = StarterPc();
            assembly.TryRecordRemoval("psu-0", out _);

            PcCapabilities pc = PcCapabilities.Evaluate(assembly);

            Assert.That(pc.HasEnoughPower, Is.False);
            Assert.That(pc.PowerSupplyCapacityWatts, Is.Zero);
            Assert.That(pc.Has(PcDiagnosticCode.MissingPowerSupply), Is.True);
            Assert.That(pc.Has(PcDiagnosticCode.InsufficientPower), Is.False, "a missing supply is reported as missing, not as weak");
        }

        // 6. It starts, but there is nothing to boot from.
        [Test]
        public void MissingStorageStillStartsButGivesNoDesktop()
        {
            PcAssembly assembly = StarterPc();
            assembly.TryRecordRemoval("storage-0", out _);

            PcCapabilities pc = PcCapabilities.Evaluate(assembly);

            Assert.That(pc.CanPowerOn, Is.True);
            Assert.That(pc.CanUseDesktop, Is.False);
            Assert.That(pc.CanPlayCriticalStrike, Is.False);

            PcDiagnostic storage = pc.Diagnostics.Single(diagnostic => diagnostic.Code == PcDiagnosticCode.MissingStorage);
            Assert.That(storage.Severity, Is.EqualTo(PcDiagnosticSeverity.Blocker));
            Assert.That(storage.Affects, Is.EqualTo(PcFunction.Desktop), "it blocks the desktop, not the power button");
        }

        // 7 and F.
        [Test]
        public void ADedicatedGraphicsCardMakesGamingGraphicsAvailable()
        {
            PcAssembly assembly = StarterPc();
            Assert.That(assembly.TryRecordInstall("gpu-0", "gpu", _gpu), Is.True);

            PcCapabilities pc = PcCapabilities.Evaluate(assembly);

            Assert.That(pc.HasDedicatedGpu, Is.True);
            Assert.That(pc.GamingGraphicsAvailable, Is.True);
            Assert.That(pc.CanPlayCriticalStrike, Is.True);
            Assert.That(pc.Diagnostics, Is.Empty);
            Assert.That(pc.TotalPowerDrawWatts, Is.EqualTo(StarterDraw + GpuWatts));
        }

        [Test]
        public void AGraphicsCardInAPcThatCannotStartGivesNoGamingGraphics()
        {
            PcAssembly assembly = StarterPc();
            assembly.TryRecordInstall("gpu-0", "gpu", _gpu);
            assembly.TryRecordRemoval("cpu-0", out _);

            PcCapabilities pc = PcCapabilities.Evaluate(assembly);

            Assert.That(pc.HasDedicatedGpu, Is.True);
            Assert.That(pc.GamingGraphicsAvailable, Is.False);
            Assert.That(pc.CanPlayCriticalStrike, Is.False);
        }

        // 8. The card fits its slot; the build as a whole needs more than the supply gives.
        [Test]
        public void InsufficientPowerSupplyKeepsTheAssemblyButBlocksPowerOnWithBothValues()
        {
            PcAssembly assembly = StarterPc(_weakPsu);
            Assert.That(PcCapabilities.Evaluate(assembly).CanPowerOn, Is.True, "the starter parts alone fit the weak supply");

            Assert.That(assembly.CheckInstall("gpu-0", "gpu", _gpu), Is.EqualTo(PcSlotCheck.Allowed), "power never decides whether a part fits");
            Assert.That(assembly.TryRecordInstall("gpu-0", "gpu", _gpu), Is.True);
            Assert.That(assembly.IsSlotOccupied("gpu-0"), Is.True);

            PcCapabilities pc = PcCapabilities.Evaluate(assembly);

            Assert.That(pc.HasPowerSupply, Is.True);
            Assert.That(pc.HasEnoughPower, Is.False);
            Assert.That(pc.CanPowerOn, Is.False);
            Assert.That(pc.CanUseDesktop, Is.False);
            Assert.That(pc.GamingGraphicsAvailable, Is.False);

            PcDiagnostic power = pc.Diagnostics.Single();
            Assert.That(power.Code, Is.EqualTo(PcDiagnosticCode.InsufficientPower));
            Assert.That(power.Severity, Is.EqualTo(PcDiagnosticSeverity.Blocker));
            Assert.That(power.Affects, Is.EqualTo(PcFunction.PowerOn));
            Assert.That(power.RequiredWatts, Is.EqualTo(StarterDraw + GpuWatts));
            Assert.That(power.AvailableWatts, Is.EqualTo(150));
        }

        // 9.
        [Test]
        public void AStrongEnoughPowerSupplyCarriesTheWholeBuild()
        {
            PcAssembly assembly = StarterPc();
            assembly.TryRecordInstall("gpu-0", "gpu", _gpu);

            PcCapabilities pc = PcCapabilities.Evaluate(assembly);

            Assert.That(pc.PowerSupplyCapacityWatts, Is.EqualTo(300));
            Assert.That(pc.TotalPowerDrawWatts, Is.LessThanOrEqualTo(pc.PowerSupplyCapacityWatts));
            Assert.That(pc.HasEnoughPower, Is.True);
            Assert.That(pc.CanPowerOn, Is.True);
        }

        [Test]
        public void ADrawExactlyAtCapacityIsEnough()
        {
            PcAssembly assembly = StarterPc(Part(PcComponentType.Psu, PcConnector.PowerSupplyBay, 0, StarterDraw));

            Assert.That(PcCapabilities.Evaluate(assembly).HasEnoughPower, Is.True);
            Assert.That(PcCapabilities.Evaluate(assembly).CanPowerOn, Is.True);
        }

        // 10.
        [Test]
        public void TwoMemoryModulesAreValidAndBothDrawPower()
        {
            PcAssembly assembly = StarterPc();
            Assert.That(assembly.TryRecordInstall("ram-1", "ram-b", _ram), Is.True);

            PcCapabilities pc = PcCapabilities.Evaluate(assembly);

            Assert.That(pc.HasMemory, Is.True);
            Assert.That(pc.CanPowerOn, Is.True);
            Assert.That(pc.TotalPowerDrawWatts, Is.EqualTo(StarterDraw + RamWatts));

            assembly.TryRecordRemoval("ram-0", out _);
            Assert.That(PcCapabilities.Evaluate(assembly).HasMemory, Is.True, "any one module is enough");
        }

        // 11. Nothing is cached: every evaluation reads the record as it is now.
        [Test]
        public void CapabilitiesFollowEveryInstallAndRemovalImmediately()
        {
            PcAssembly assembly = StarterPc();
            Assert.That(PcCapabilities.Evaluate(assembly).CanPowerOn, Is.True);

            assembly.TryRecordRemoval("ram-0", out _);
            Assert.That(PcCapabilities.Evaluate(assembly).CanPowerOn, Is.False);

            assembly.TryRecordInstall("ram-0", "ram", _ram);
            Assert.That(PcCapabilities.Evaluate(assembly).CanPowerOn, Is.True);

            assembly.TryRecordRemoval("storage-0", out _);
            Assert.That(PcCapabilities.Evaluate(assembly).CanPowerOn, Is.True);
            Assert.That(PcCapabilities.Evaluate(assembly).CanUseDesktop, Is.False);

            assembly.TryRecordInstall("storage-0", "drive", _drive);
            assembly.TryRecordInstall("gpu-0", "gpu", _gpu);
            Assert.That(PcCapabilities.Evaluate(assembly).GamingGraphicsAvailable, Is.True);

            assembly.TryRecordRemoval("gpu-0", out _);
            Assert.That(PcCapabilities.Evaluate(assembly).CanPowerOn, Is.True);
            Assert.That(PcCapabilities.Evaluate(assembly).GamingGraphicsAvailable, Is.False);
        }

        [Test]
        public void ReplacingThePowerSupplyWithAWeakerOneTurnsTheSamePartsIntoABlocker()
        {
            PcAssembly assembly = StarterPc();
            assembly.TryRecordInstall("gpu-0", "gpu", _gpu);
            Assert.That(PcCapabilities.Evaluate(assembly).CanPowerOn, Is.True);

            assembly.TryRecordRemoval("psu-0", out _);
            assembly.TryRecordInstall("psu-0", "weak-psu", _weakPsu);

            Assert.That(PcCapabilities.Evaluate(assembly).Has(PcDiagnosticCode.InsufficientPower), Is.True);
            Assert.That(PcCapabilities.Evaluate(assembly).CanPowerOn, Is.False);
        }

        // 12.
        [Test]
        public void EveryDiagnosticAppearsAtMostOnce()
        {
            PcCapabilities empty = PcCapabilities.Evaluate(Pc());
            PcAssembly doubleMemory = StarterPc(_weakPsu);
            doubleMemory.TryRecordInstall("ram-1", "ram-b", _ram);
            doubleMemory.TryRecordInstall("gpu-0", "gpu", _gpu);
            doubleMemory.TryRecordRemoval("storage-0", out _);
            PcCapabilities crowded = PcCapabilities.Evaluate(doubleMemory);

            Assert.That(empty.Diagnostics.Select(diagnostic => diagnostic.Code), Is.Unique);
            Assert.That(crowded.Diagnostics.Select(diagnostic => diagnostic.Code), Is.Unique);
            Assert.That(crowded.Diagnostics.Select(diagnostic => diagnostic.Code), Is.EqualTo(new[] { PcDiagnosticCode.InsufficientPower, PcDiagnosticCode.MissingStorage }));
        }

        // 13. Power first, then desktop, then limitations, whatever order the parts went in.
        [Test]
        public void DiagnosticsComeInOneFixedOrder()
        {
            PcCapabilities empty = PcCapabilities.Evaluate(Pc());

            Assert.That(empty.Diagnostics.Select(diagnostic => diagnostic.Code), Is.EqualTo(new[]
            {
                PcDiagnosticCode.MissingMotherboard,
                PcDiagnosticCode.MissingCpu,
                PcDiagnosticCode.MissingMemory,
                PcDiagnosticCode.MissingPowerSupply,
                PcDiagnosticCode.MissingStorage,
                PcDiagnosticCode.NoDedicatedGpu
            }));

            PcAssembly forward = Pc();
            forward.TryRecordInstall("cpu-0", "cpu", _cpu);
            forward.TryRecordInstall("storage-0", "drive", _drive);

            PcAssembly backward = Pc();
            backward.TryRecordInstall("storage-0", "drive", _drive);
            backward.TryRecordInstall("cpu-0", "cpu", _cpu);

            Assert.That(Describe(PcCapabilities.Evaluate(forward)), Is.EqualTo(Describe(PcCapabilities.Evaluate(backward))));
            Assert.That(Describe(PcCapabilities.Evaluate(forward)), Is.EqualTo(Describe(PcCapabilities.Evaluate(forward))), "the same record gives the same answer");
        }

        [Test]
        public void EachDiagnosticCodeHasOneMeaning()
        {
            Dictionary<PcDiagnosticCode, (PcDiagnosticSeverity, PcFunction, PcComponentType)> expected = new()
            {
                [PcDiagnosticCode.MissingMotherboard] = (PcDiagnosticSeverity.Blocker, PcFunction.PowerOn, PcComponentType.Motherboard),
                [PcDiagnosticCode.MissingCpu] = (PcDiagnosticSeverity.Blocker, PcFunction.PowerOn, PcComponentType.Cpu),
                [PcDiagnosticCode.MissingMemory] = (PcDiagnosticSeverity.Blocker, PcFunction.PowerOn, PcComponentType.Ram),
                [PcDiagnosticCode.MissingPowerSupply] = (PcDiagnosticSeverity.Blocker, PcFunction.PowerOn, PcComponentType.Psu),
                [PcDiagnosticCode.InsufficientPower] = (PcDiagnosticSeverity.Blocker, PcFunction.PowerOn, PcComponentType.Psu),
                [PcDiagnosticCode.MissingStorage] = (PcDiagnosticSeverity.Blocker, PcFunction.Desktop, PcComponentType.Storage),
                [PcDiagnosticCode.NoDedicatedGpu] = (PcDiagnosticSeverity.Limitation, PcFunction.Gaming, PcComponentType.Gpu)
            };

            PcDiagnosticCode[] codes = System.Enum.GetValues(typeof(PcDiagnosticCode)).Cast<PcDiagnosticCode>().ToArray();
            Assert.That(codes, Is.EquivalentTo(expected.Keys), "every code is covered");

            foreach (PcDiagnosticCode code in codes)
            {
                PcDiagnostic diagnostic = PcDiagnostic.For(code);
                Assert.That((diagnostic.Severity, diagnostic.Affects, diagnostic.Component), Is.EqualTo(expected[code]), code.ToString());
                Assert.That(diagnostic.TitleKey, Does.StartWith("pc.diagnostic.").And.EndWith(".title"));
                Assert.That(diagnostic.DetailKey, Does.StartWith("pc.diagnostic.").And.EndWith(".detail"));
            }

            Assert.That(codes.Select(code => PcDiagnostic.For(code).TitleKey), Is.Unique);
            Assert.That(PcDiagnostic.For(PcDiagnosticCode.NoDedicatedGpu).TitleKey, Is.EqualTo("pc.diagnostic.no_gpu.title"), "the existing key keeps its name");
        }

        [Test]
        public void PowerOnAttemptAnswersFromTheHardwareWithoutChangingIt()
        {
            PcAssembly assembly = StarterPc();
            int changes = 0;
            assembly.Changed += () => changes++;

            PcPowerOnResult starter = PcPowerOnResult.Attempt(assembly);
            Assert.That(starter.Outcome, Is.EqualTo(PcPowerOnOutcome.Started), "no graphics card does not block power-on");
            Assert.That(starter.ReachesDesktop, Is.True);
            Assert.That(starter.Capabilities.Has(PcDiagnosticCode.NoDedicatedGpu), Is.True);

            assembly.TryRecordRemoval("storage-0", out _);
            PcPowerOnResult noDrive = PcPowerOnResult.Attempt(assembly);
            Assert.That(noDrive.Started, Is.True, "it powers on");
            Assert.That(noDrive.ReachesDesktop, Is.False, "but finds nothing to boot");

            assembly.TryRecordRemoval("ram-0", out _);
            PcPowerOnResult noMemory = PcPowerOnResult.Attempt(assembly);
            Assert.That(noMemory.Outcome, Is.EqualTo(PcPowerOnOutcome.Blocked));
            Assert.That(noMemory.ReachesDesktop, Is.False);
            Assert.That(noMemory.Capabilities.Diagnostics.First().Code, Is.EqualTo(PcDiagnosticCode.MissingMemory));

            changes = 0;
            PcPowerOnResult.Attempt(assembly);
            Assert.That(changes, Is.Zero, "an attempt only reads");
        }

        [Test]
        public void PowerOnIsBlockedByAnOverloadedPowerSupply()
        {
            PcAssembly assembly = StarterPc(_weakPsu);
            assembly.TryRecordInstall("gpu-0", "gpu", _gpu);

            PcPowerOnResult result = PcPowerOnResult.Attempt(assembly);

            Assert.That(result.Outcome, Is.EqualTo(PcPowerOnOutcome.Blocked));
            Assert.That(result.Capabilities.Has(PcDiagnosticCode.InsufficientPower), Is.True);
        }

        // Authored PC data that could never describe a real part.
        [TestCase(PcComponentType.Gpu, PcConnector.PcieX16, -1, 0, TestName = "negative power draw")]
        [TestCase(PcComponentType.Psu, PcConnector.PowerSupplyBay, 0, -300, TestName = "negative power capacity")]
        [TestCase(PcComponentType.Cpu, PcConnector.CpuSocket, 65, 300, TestName = "a processor declaring power capacity")]
        [TestCase(PcComponentType.Psu, PcConnector.PowerSupplyBay, 0, 0, TestName = "a power supply without capacity")]
        [TestCase(PcComponentType.Psu, PcConnector.PowerSupplyBay, 20, 300, TestName = "a power supply drawing power")]
        [TestCase((PcComponentType)99, PcConnector.PcieX16, 10, 0, TestName = "undefined component type")]
        [TestCase(PcComponentType.Ram, (PcConnector)99, 3, 0, TestName = "undefined connector")]
        [TestCase(PcComponentType.Ram, PcConnector.None, 3, 0, TestName = "no connector")]
        public void ImpossibleComponentDataIsInvalidAndFitsNowhere(PcComponentType type, PcConnector connector, int draw, int capacity)
        {
            PcComponentSpec spec = Part(type, connector, draw, capacity);

            Assert.That(spec.IsValid, Is.False);
            Assert.That(spec.ValidationError, Is.Not.Empty);
            Assert.That(Pc().CheckPart(spec, out _), Is.EqualTo(PcSlotCheck.NotPcHardware), "invalid data is never installable");
        }

        [Test]
        public void EveryFixtureAndPlainPartIsValid()
        {
            foreach (PcComponentSpec spec in new[] { _board, _cpu, _ram, _drive, _gpu, _psu, _weakPsu })
                Assert.That(spec.ValidationError, Is.Null, spec.ComponentType.ToString());
        }

        private PcAssembly StarterPc(PcComponentSpec psu = null)
        {
            PcAssembly assembly = Pc();

            Assert.That(assembly.TryRecordInstall("motherboard-0", "board", _board), Is.True);
            Assert.That(assembly.TryRecordInstall("cpu-0", "cpu", _cpu), Is.True);
            Assert.That(assembly.TryRecordInstall("ram-0", "ram", _ram), Is.True);
            Assert.That(assembly.TryRecordInstall("psu-0", "psu", psu ?? _psu), Is.True);
            Assert.That(assembly.TryRecordInstall("storage-0", "drive", _drive), Is.True);
            return assembly;
        }

        private static PcAssembly Pc()
        {
            return new PcAssembly(new[]
            {
                new PcSlotSpec("motherboard-0", PcComponentType.Motherboard, PcConnector.MotherboardTray),
                new PcSlotSpec("cpu-0", PcComponentType.Cpu, PcConnector.CpuSocket),
                new PcSlotSpec("ram-0", PcComponentType.Ram, PcConnector.MemorySlot),
                new PcSlotSpec("ram-1", PcComponentType.Ram, PcConnector.MemorySlot),
                new PcSlotSpec("psu-0", PcComponentType.Psu, PcConnector.PowerSupplyBay),
                new PcSlotSpec("storage-0", PcComponentType.Storage, PcConnector.SataStorage),
                new PcSlotSpec("gpu-0", PcComponentType.Gpu, PcConnector.PcieX16)
            });
        }

        private PcComponentSpec Part(PcComponentType type, PcConnector connector, int drawWatts, int capacityWatts = 0)
        {
            PcComponentSpec spec = ScriptableObject.CreateInstance<PcComponentSpec>();
            ShopTestData.Set(spec, "componentType", type);
            ShopTestData.Set(spec, "connector", connector);
            ShopTestData.Set(spec, "powerDrawWatts", drawWatts);
            ShopTestData.Set(spec, "powerCapacityWatts", capacityWatts);
            _created.Add(spec);
            return spec;
        }

        private static string Describe(PcCapabilities pc)
        {
            return $"{pc.CanPowerOn}|{pc.CanUseDesktop}|{pc.GamingGraphicsAvailable}|{pc.TotalPowerDrawWatts}|{pc.PowerSupplyCapacityWatts}|" +
                   string.Join(",", pc.Diagnostics.Select(diagnostic => $"{diagnostic.Code}:{diagnostic.RequiredWatts}/{diagnostic.AvailableWatts}"));
        }
    }
}
