using System.Collections.Generic;
using System.Linq;
using GoLive.PcBuilding;
using NUnit.Framework;
using UnityEngine;

namespace GoLive.Tests
{
    // The plain PC assembly record: which installed instance occupies which slot, its rules and its snapshot.
    public sealed class PcAssemblyTests
    {
        private readonly List<Object> _created = new();

        private PcComponentSpec _gpu;
        private PcComponentSpec _ram;

        [SetUp]
        public void SetUp()
        {
            _gpu = Spec(PcComponentType.Gpu, PcConnector.PcieX16);
            _ram = Spec(PcComponentType.Ram, PcConnector.PcieX16);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created)
                Object.DestroyImmediate(created);

            _created.Clear();
        }

        [Test]
        public void EmptySlotAcceptsACompatibleGpu()
        {
            PcAssembly pc = Pc();

            Assert.That(pc.CheckInstall("gpu-0", "card", _gpu), Is.EqualTo(PcSlotCheck.Allowed));
            Assert.That(pc.TryRecordInstall("gpu-0", "card", _gpu), Is.True);
            Assert.That(pc.IsSlotOccupied("gpu-0"), Is.True);
        }

        [Test]
        public void WrongComponentTypeWrongConnectorAndNonHardwareAreRejected()
        {
            PcAssembly pc = Pc();
            PcComponentSpec otherConnector = Spec(PcComponentType.Gpu, (PcConnector)99);
            PcSlotSpec futureSocket = new("gpu-9", PcComponentType.Gpu, (PcConnector)2);

            Assert.That(PcAssembly.CheckCompatibility(futureSocket, _gpu), Is.EqualTo(PcSlotCheck.WrongConnector), "same type, different connector");

            Assert.That(pc.CheckInstall("gpu-0", "stick", _ram), Is.EqualTo(PcSlotCheck.WrongComponentType));
            Assert.That(pc.CheckInstall("gpu-0", "banana", null), Is.EqualTo(PcSlotCheck.NotPcHardware));
            Assert.That(pc.CheckInstall("gpu-0", "odd", otherConnector), Is.EqualTo(PcSlotCheck.NotPcHardware), "an undefined connector is not valid data");
            Assert.That(PcAssembly.CheckCompatibility(new PcSlotSpec("gpu-9", PcComponentType.Gpu, PcConnector.PcieX16), _gpu), Is.EqualTo(PcSlotCheck.Allowed));
            Assert.That(pc.TryRecordInstall("gpu-0", "stick", _ram), Is.False);
            Assert.That(pc.InstalledComponents, Is.Empty);
        }

        [Test]
        public void OccupiedSlotIsRejected()
        {
            PcAssembly pc = Pc();
            pc.TryRecordInstall("gpu-0", "first", _gpu);

            Assert.That(pc.CheckInstall("gpu-0", "second", _gpu), Is.EqualTo(PcSlotCheck.SlotOccupied));
            Assert.That(pc.TryRecordInstall("gpu-0", "second", _gpu), Is.False);
            Assert.That(pc.TryGetInstalled("gpu-0", out PcInstalledComponent installed) && installed.InstanceId == "first", Is.True);
        }

        [Test]
        public void OneItemCannotOccupyTwoSlots()
        {
            PcAssembly pc = Pc(("gpu-0", PcComponentType.Gpu), ("gpu-1", PcComponentType.Gpu));
            pc.TryRecordInstall("gpu-0", "card", _gpu);

            Assert.That(pc.CheckInstall("gpu-1", "card", _gpu), Is.EqualTo(PcSlotCheck.AlreadyInstalled));
            Assert.That(pc.TryRecordInstall("gpu-1", "card", _gpu), Is.False);
            Assert.That(pc.IsSlotOccupied("gpu-1"), Is.False);
        }

        [Test]
        public void InstallRecordsTheSameInstanceIdAndAnswersTypeQueries()
        {
            PcAssembly pc = Pc(("gpu-0", PcComponentType.Gpu), ("ram-0", PcComponentType.Ram));
            int changes = 0;
            pc.Changed += () => changes++;

            Assert.That(pc.HasComponent(PcComponentType.Gpu), Is.False);
            pc.TryRecordInstall("gpu-0", "card-7", _gpu);

            Assert.That(changes, Is.EqualTo(1));
            Assert.That(pc.TryFindSlotOf("card-7", out string slot) && slot == "gpu-0", Is.True);
            Assert.That(pc.HasComponent(PcComponentType.Gpu), Is.True);
            Assert.That(pc.HasComponent(PcComponentType.Ram), Is.False);
            Assert.That(pc.TryGetInstalled(PcComponentType.Gpu, out PcInstalledComponent gpu), Is.True);
            Assert.That(gpu.InstanceId, Is.EqualTo("card-7"));
            Assert.That(gpu.Spec, Is.SameAs(_gpu));
            Assert.That(pc.InstalledComponents.Select(c => c.SlotId), Is.EqualTo(new[] { "gpu-0" }));
        }

        [Test]
        public void RemovalClearsTheSlotAndReturnsTheInstance()
        {
            PcAssembly pc = Pc();
            pc.TryRecordInstall("gpu-0", "card", _gpu);

            Assert.That(pc.CheckRemove("gpu-0"), Is.EqualTo(PcSlotCheck.Allowed));
            Assert.That(pc.TryRecordRemoval("gpu-0", out string removed), Is.True);
            Assert.That(removed, Is.EqualTo("card"));
            Assert.That(pc.IsSlotOccupied("gpu-0"), Is.False);
            Assert.That(pc.TryFindSlotOf("card", out _), Is.False);
            Assert.That(pc.CheckRemove("gpu-0"), Is.EqualTo(PcSlotCheck.SlotEmpty));
            Assert.That(pc.CheckRemove("psu-0"), Is.EqualTo(PcSlotCheck.UnknownSlot));
            Assert.That(pc.TryRecordRemoval("gpu-0", out _), Is.False);
        }

        [Test]
        public void SnapshotRoundTripsIntoAFreshPc()
        {
            PcAssembly pc = Pc();
            pc.TryRecordInstall("gpu-0", "card", _gpu);

            PcAssemblySnapshot snapshot = JsonUtility.FromJson<PcAssemblySnapshot>(JsonUtility.ToJson(pc.CaptureSnapshot()));
            Dictionary<string, PcComponentSpec> installed = new() { ["card"] = _gpu };
            PcAssembly loaded = Pc();

            Assert.That(snapshot.Version, Is.EqualTo(PcAssembly.SnapshotVersion));
            Assert.That(loaded.IsValidSnapshot(snapshot, installed), Is.True);

            loaded.Restore(snapshot, installed);

            Assert.That(loaded.TryGetInstalled("gpu-0", out PcInstalledComponent component) && component.InstanceId == "card", Is.True);
            Assert.That(JsonUtility.ToJson(loaded.CaptureSnapshot()), Is.EqualTo(JsonUtility.ToJson(pc.CaptureSnapshot())));

            Pc().Restore(new PcAssemblySnapshot { Version = PcAssembly.SnapshotVersion }, new Dictionary<string, PcComponentSpec>());
        }

        [TestCase("duplicate slot")]
        [TestCase("duplicate installed item")]
        [TestCase("unknown slot")]
        [TestCase("installed item without a slot")]
        [TestCase("slot points at an item that is not installed")]
        [TestCase("wrong component type in slot")]
        [TestCase("not pc hardware")]
        [TestCase("unsupported version")]
        [TestCase("missing records")]
        [TestCase("blank item id")]
        [TestCase("null record")]
        public void InvalidSnapshotIsRejectedAndLeavesTheRecordAlone(string corruption)
        {
            PcAssembly pc = Pc(("gpu-0", PcComponentType.Gpu), ("gpu-1", PcComponentType.Gpu));
            pc.TryRecordInstall("gpu-1", "live-card", _gpu);

            PcAssemblySnapshot snapshot = new()
            {
                Version = PcAssembly.SnapshotVersion,
                InstalledSlots = new[] { Record("gpu-0", "card") }
            };
            Dictionary<string, PcComponentSpec> installed = new() { ["card"] = _gpu };

            switch (corruption)
            {
                case "duplicate slot":
                    snapshot.InstalledSlots = new[] { Record("gpu-0", "card"), Record("gpu-0", "card-b") };
                    installed["card-b"] = _gpu;
                    break;
                case "duplicate installed item":
                    snapshot.InstalledSlots = new[] { Record("gpu-0", "card"), Record("gpu-1", "card") };
                    installed["card-b"] = _gpu;
                    break;
                case "unknown slot": snapshot.InstalledSlots[0].SlotId = "gpu-7"; break;
                case "installed item without a slot": installed["orphan"] = _gpu; break;
                case "slot points at an item that is not installed": installed.Remove("card"); break;
                case "wrong component type in slot": installed["card"] = _ram; break;
                case "not pc hardware": installed["card"] = null; break;
                case "unsupported version": snapshot.Version = 2; break;
                case "missing records": snapshot.InstalledSlots = null; break;
                case "blank item id": snapshot.InstalledSlots[0].ItemInstanceId = " "; installed[" "] = _gpu; installed.Remove("card"); break;
                case "null record": snapshot.InstalledSlots[0] = null; break;
            }

            string before = JsonUtility.ToJson(pc.CaptureSnapshot());

            Assert.That(pc.IsValidSnapshot(snapshot, installed), Is.False);
            Assert.That(() => pc.Restore(snapshot, installed), Throws.ArgumentException);
            Assert.That(JsonUtility.ToJson(pc.CaptureSnapshot()), Is.EqualTo(before));
        }

        [Test]
        public void CheckPartSaysWhereAPartCouldGoInThisPc()
        {
            PcAssembly pc = Pc(("gpu-0", PcComponentType.Gpu), ("gpu-1", PcComponentType.Gpu));
            PcComponentSpec motherboard = Spec(PcComponentType.Motherboard, PcConnector.MotherboardTray);

            Assert.That(pc.CheckPart(_gpu, out string slot), Is.EqualTo(PcSlotCheck.Allowed));
            Assert.That(slot, Is.EqualTo("gpu-0"), "the first free compatible slot");

            pc.TryRecordInstall("gpu-0", "card-a", _gpu);
            Assert.That(pc.CheckPart(_gpu, out slot), Is.EqualTo(PcSlotCheck.Allowed));
            Assert.That(slot, Is.EqualTo("gpu-1"));

            pc.TryRecordInstall("gpu-1", "card-b", _gpu);
            Assert.That(pc.CheckPart(_gpu, out slot), Is.EqualTo(PcSlotCheck.SlotOccupied));
            Assert.That(slot, Is.Null);

            Assert.That(pc.CheckPart(motherboard, out _), Is.EqualTo(PcSlotCheck.NoMatchingSlot), "a PC part this PC has no place for");
            Assert.That(pc.CheckPart(null, out _), Is.EqualTo(PcSlotCheck.NotPcHardware));
        }

        [Test]
        public void SlotLayoutMustHaveUniqueValidIds()
        {
            Assert.That(() => Pc(("gpu-0", PcComponentType.Gpu), ("gpu-0", PcComponentType.Gpu)), Throws.ArgumentException);
            Assert.That(() => Pc(("GPU 0", PcComponentType.Gpu)), Throws.ArgumentException);
            Assert.That(() => new PcAssembly(new[] { new PcSlotSpec("gpu-0", PcComponentType.Gpu, PcConnector.None) }), Throws.ArgumentException);
        }

        // Mounting is slot data, not a motherboard rule: here a drive bay carries two expansion cards.
        [Test]
        public void APartMountedOnAnotherGoesInAfterItAndComesOutBeforeIt()
        {
            PcComponentSpec drive = Spec(PcComponentType.Storage, PcConnector.PcieX16);
            PcAssembly pc = BayWithTwoCards();

            Assert.That(pc.IsSlotPresent("bay-0"), Is.True, "a slot on the case is always there");
            Assert.That(pc.IsSlotPresent("card-0"), Is.False, "no host part, no slot");
            Assert.That(pc.CheckInstall("card-0", "card-a", _gpu), Is.EqualTo(PcSlotCheck.HostMissing));
            Assert.That(pc.TryRecordInstall("card-0", "card-a", _gpu), Is.False);
            Assert.That(pc.CheckPart(_gpu, out string slot), Is.EqualTo(PcSlotCheck.HostMissing));
            Assert.That(slot, Is.EqualTo("card-0"), "the slot that waits for its host");
            Assert.That(pc.CheckInstall("card-0", "card-a", _ram), Is.EqualTo(PcSlotCheck.WrongComponentType), "a part that could never go there says so first");

            Assert.That(pc.TryRecordInstall("bay-0", "drive", drive), Is.True);
            Assert.That(pc.IsSlotPresent("card-0") && pc.IsSlotPresent("card-1"), Is.True, "the host part brings its slots");
            Assert.That(pc.CheckPart(_gpu, out slot), Is.EqualTo(PcSlotCheck.Allowed));
            Assert.That(slot, Is.EqualTo("card-0"));
            Assert.That(pc.TryRecordInstall("card-0", "card-a", _gpu), Is.True);
            Assert.That(pc.TryRecordInstall("card-1", "card-b", _gpu), Is.True);

            Assert.That(pc.CheckRemove("bay-0"), Is.EqualTo(PcSlotCheck.MountedPartsInstalled));
            Assert.That(pc.InstalledOn("bay-0").Select(component => component.InstanceId), Is.EqualTo(new[] { "card-a", "card-b" }), "what has to come out first, in slot order");
            Assert.That(pc.TryRecordRemoval("bay-0", out _), Is.False);
            Assert.That(pc.IsSlotOccupied("bay-0"), Is.True, "a refused removal changes nothing");

            Assert.That(pc.TryRecordRemoval("card-0", out _), Is.True);
            Assert.That(pc.CheckRemove("bay-0"), Is.EqualTo(PcSlotCheck.MountedPartsInstalled), "one card is still on it");
            Assert.That(pc.InstalledOn("bay-0").Select(component => component.InstanceId), Is.EqualTo(new[] { "card-b" }));

            Assert.That(pc.TryRecordRemoval("card-1", out _), Is.True);
            Assert.That(pc.CheckRemove("bay-0"), Is.EqualTo(PcSlotCheck.Allowed));
            Assert.That(pc.TryRecordRemoval("bay-0", out string removed) && removed == "drive", Is.True);
            Assert.That(pc.IsSlotPresent("card-0"), Is.False, "its slots went with it");
            Assert.That(pc.CheckInstall("card-0", "card-a", _gpu), Is.EqualTo(PcSlotCheck.HostMissing));
        }

        [Test]
        public void ASnapshotWithAPartOffItsHostIsRejected()
        {
            PcComponentSpec drive = Spec(PcComponentType.Storage, PcConnector.PcieX16);
            PcAssembly pc = BayWithTwoCards();
            Dictionary<string, PcComponentSpec> cardOnly = new() { ["card-a"] = _gpu };
            Dictionary<string, PcComponentSpec> both = new() { ["drive"] = drive, ["card-a"] = _gpu };

            Assert.That(pc.IsValidSnapshot(Snapshot(Record("card-0", "card-a")), cardOnly), Is.False, "a card in a bay that holds nothing");
            Assert.That(() => pc.Restore(Snapshot(Record("card-0", "card-a")), cardOnly), Throws.ArgumentException);
            Assert.That(pc.InstalledComponents, Is.Empty);

            Assert.That(pc.IsValidSnapshot(Snapshot(Record("card-0", "card-a"), Record("bay-0", "drive")), both), Is.True, "record order does not matter");
        }

        [Test]
        public void EveryHostIsASlotOfThisPcAndNoSlotHostsItself()
        {
            PcSlotSpec bay = new("bay-0", PcComponentType.Storage, PcConnector.PcieX16);

            Assert.That(() => new PcAssembly(new[] { bay, new PcSlotSpec("card-0", PcComponentType.Gpu, PcConnector.PcieX16, "bay-9") }), Throws.ArgumentException, "unknown host");
            Assert.That(() => new PcAssembly(new[] { new PcSlotSpec("card-0", PcComponentType.Gpu, PcConnector.PcieX16, "card-0") }), Throws.ArgumentException, "its own host");
            Assert.That(() => new PcAssembly(new[]
            {
                new PcSlotSpec("card-0", PcComponentType.Gpu, PcConnector.PcieX16, "card-1"),
                new PcSlotSpec("card-1", PcComponentType.Gpu, PcConnector.PcieX16, "card-0")
            }), Throws.ArgumentException, "hosts that never reach the case");
            Assert.That(() => BayWithTwoCards(), Throws.Nothing);
        }

        // The full rules live in PcCapabilitiesTests; here only that capabilities read this record.
        [Test]
        public void CapabilitiesReadTheInstalledGraphicsCardFromTheRecord()
        {
            PcAssembly pc = Pc();

            PcCapabilities without = PcCapabilities.Evaluate(pc);
            Assert.That(without.HasDedicatedGpu, Is.False);
            Assert.That(without.GamingGraphicsAvailable, Is.False);
            Assert.That(without.Has(PcDiagnosticCode.NoDedicatedGpu), Is.True);
            Assert.That(without.Diagnostics.Single(diagnostic => diagnostic.Code == PcDiagnosticCode.NoDedicatedGpu).Severity, Is.EqualTo(PcDiagnosticSeverity.Limitation), "no GPU limits games and streams, it does not stop the PC");

            pc.TryRecordInstall("gpu-0", "card", _gpu);

            PcCapabilities with = PcCapabilities.Evaluate(pc);
            Assert.That(with.HasDedicatedGpu, Is.True);
            Assert.That(with.Has(PcDiagnosticCode.NoDedicatedGpu), Is.False);
            Assert.That(with.GamingGraphicsAvailable, Is.False, "a card alone is not a PC that can start");
        }

        [Test]
        public void EveryCheckHasOneLocalizedMessageKey()
        {
            string[] keys = System.Enum.GetValues(typeof(PcSlotCheck)).Cast<PcSlotCheck>().Where(check => check != PcSlotCheck.Allowed).Select(check => check.MessageKey()).ToArray();

            Assert.That(keys, Is.All.StartsWith("pc.reject."));
            Assert.That(PcComponentType.Gpu.InstallPromptKey(), Is.EqualTo("pc.install.gpu"));
            Assert.That(PcComponentType.Gpu.RemovePromptKey(), Is.EqualTo("pc.remove.gpu"));
        }

        private PcAssembly Pc(params (string id, PcComponentType type)[] slots)
        {
            if (slots.Length == 0)
                slots = new[] { ("gpu-0", PcComponentType.Gpu) };

            return new PcAssembly(slots.Select(slot => new PcSlotSpec(slot.id, slot.type, PcConnector.PcieX16)).ToArray());
        }

        private static PcAssembly BayWithTwoCards()
        {
            return new PcAssembly(new[]
            {
                new PcSlotSpec("bay-0", PcComponentType.Storage, PcConnector.PcieX16),
                new PcSlotSpec("card-0", PcComponentType.Gpu, PcConnector.PcieX16, "bay-0"),
                new PcSlotSpec("card-1", PcComponentType.Gpu, PcConnector.PcieX16, "bay-0")
            });
        }

        private static PcAssemblySnapshot Snapshot(params PcInstalledSlotSnapshot[] records)
        {
            return new PcAssemblySnapshot { Version = PcAssembly.SnapshotVersion, InstalledSlots = records };
        }

        private PcComponentSpec Spec(PcComponentType type, PcConnector connector)
        {
            PcComponentSpec spec = ScriptableObject.CreateInstance<PcComponentSpec>();
            ShopTestData.Set(spec, "componentType", type);
            ShopTestData.Set(spec, "connector", connector);
            _created.Add(spec);
            return spec;
        }

        private static PcInstalledSlotSnapshot Record(string slotId, string instanceId)
        {
            return new PcInstalledSlotSnapshot { SlotId = slotId, ItemInstanceId = instanceId };
        }
    }
}
