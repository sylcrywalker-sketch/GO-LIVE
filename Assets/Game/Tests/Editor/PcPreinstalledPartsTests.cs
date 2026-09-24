using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using GoLive.Items;
using GoLive.PcBuilding;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GoLive.Tests
{
    // The Student PC's new-game install (PcAssemblyBehaviour.Start) on the real prefab, with the lifecycle called in
    // Unity's order: every scene item's Awake, then the PC's Awake and Start. Each authoring mistake is refused as a
    // whole: the PC stays unready and no item moves.
    public sealed class PcPreinstalledPartsTests
    {
        private static readonly Regex Refused = new($"{nameof(PcAssemblyBehaviour)} on StudentPC has .*");

        private SceneSetup[] _previousScenes;
        private GameObject _holder;
        private PcAssemblyBehaviour _pc;

        [OneTimeSetUp]
        public void IsolateTestScene()
        {
            _previousScenes = SaveTestWorld.IsolateScene();
        }

        [OneTimeTearDown]
        public void RestoreEditorSceneSetup()
        {
            SaveTestWorld.RestoreScene(_previousScenes);
        }

        [SetUp]
        public void SetUp()
        {
            // A plain copy of the prefab, like the PC in a running game: the cases below move starter parts around, which an
            // Editor prefab instance refuses.
            _holder = new GameObject("Test PC holder");
            _holder.SetActive(false);
            GameObject pc = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SaveTestWorld.StudentPcPrefab), _holder.transform);
            pc.name = "StudentPC";
            _pc = pc.GetComponent<PcAssemblyBehaviour>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_holder);
        }

        [Test]
        public void AuthoredPartsInstallIntoTheirSlotsOnce()
        {
            StartNewGame();

            Assert.That(_pc.IsReady, Is.True);
            Assert.That(_pc.Assembly.InstalledComponents.Select(component => component.SlotId), Is.EqualTo(new[] { "motherboard-0", "cpu-0", "ram-0", "psu-0", "storage-0" }));

            foreach (WorldItem item in Items())
            {
                Assert.That(item.Instance.Location, Is.EqualTo(ItemLocation.Installed), item.name);
                Assert.That(item.Instance.InstanceId, Is.EqualTo(item.AuthoredInstanceId), "the scene identity, never a new one");
                Assert.That(_pc.Assembly.TryFindSlotOf(item.Instance.InstanceId, out string slotId) && _pc.TryGetSlot(slotId, out PcComponentSlot slot) && item.transform.parent == slot.InstallAnchor, Is.True, item.name);
                Assert.That(item.GetComponent<Rigidbody>().isKinematic, Is.True, $"{item.name} does not simulate inside the PC");
                Assert.That(item.GetComponentsInChildren<Collider>(true).All(collider => !collider.enabled), Is.True, $"{item.name} does not answer the pickup ray");
            }

            Assert.That(_pc.TryPowerOn().Outcome, Is.EqualTo(PcPowerOnOutcome.Started));
            Assert.That(_pc.TryPowerOn().ReachesDesktop, Is.True);

            string record = JsonUtility.ToJson(_pc.CaptureSnapshot());
            Invoke(_pc, "Start");
            Assert.That(JsonUtility.ToJson(_pc.CaptureSnapshot()), Is.EqualTo(record), "a second Start changes nothing");
        }

        [Test]
        public void APartInASlotOfAnotherComponentTypeIsRefused()
        {
            WorldItem cpu = Item("cpu-0"), ram = Item("ram-0");
            ram.transform.SetParent(Slot("cpu-0").InstallAnchor, false);
            cpu.transform.SetParent(Slot("ram-0").InstallAnchor, false);
            SetEntry(1, Slot("cpu-0"), ram);
            SetEntry(2, Slot("ram-0"), cpu);

            AssertRefused();
        }

        [Test]
        public void APartWithTheWrongConnectorIsRefused()
        {
            SerializedObject slot = new(Slot("ram-0"));
            slot.FindProperty("connector").intValue = (int)PcConnector.PcieX16;
            slot.ApplyModifiedPropertiesWithoutUndo();

            AssertRefused();
        }

        [Test]
        public void TwoPartsInOneSlotAreRefused()
        {
            WorldItem ram = Item("ram-0");
            SetEntry(4, Slot("ram-0"), Item("storage-0"));
            Item("storage-0").transform.SetParent(Slot("ram-0").InstallAnchor, false);

            AssertRefused();
            Assert.That(ram.Instance.Location, Is.EqualTo(ItemLocation.World));
        }

        [Test]
        public void OnePartListedTwiceIsRefused()
        {
            SetEntry(4, Slot("ram-0"), Item("ram-0"));

            AssertRefused();
        }

        [Test]
        public void TwoPartsSharingOneSceneIdAreRefused()
        {
            SerializedObject ram = new(Item("ram-0"));
            ram.FindProperty("authoredInstanceId").stringValue = Item("cpu-0").AuthoredInstanceId;
            ram.ApplyModifiedPropertiesWithoutUndo();

            AssertRefused();
        }

        [Test]
        public void AnItemLeftOnASlotWithoutAnEntryIsRefused()
        {
            SerializedObject behaviour = new(_pc);
            behaviour.FindProperty("preinstalled").DeleteArrayElementAtIndex(2);
            behaviour.ApplyModifiedPropertiesWithoutUndo();

            AssertRefused();
        }

        [Test]
        public void AListedPartAwayFromItsSlotIsRefused()
        {
            Item("ram-0").transform.SetParent(_pc.transform, false);

            AssertRefused();
        }

        // Without the board among the starter parts, the processor and memory would sit on nothing.
        [Test]
        public void PartsWhoseHostPartIsNotPreinstalledAreRefused()
        {
            Item("motherboard-0").transform.SetParent(_pc.transform, false);
            SerializedObject behaviour = new(_pc);
            behaviour.FindProperty("preinstalled").DeleteArrayElementAtIndex(0);
            behaviour.ApplyModifiedPropertiesWithoutUndo();

            AssertRefused();
        }

        [Test]
        public void APartWithoutAPersistentSceneIdIsRefused()
        {
            SerializedObject ram = new(Item("ram-0"));
            ram.FindProperty("authoredInstanceId").stringValue = string.Empty;
            ram.ApplyModifiedPropertiesWithoutUndo();

            AssertRefused();
        }

        private void AssertRefused()
        {
            LogAssert.Expect(LogType.Error, Refused);
            StartNewGame();

            Assert.That(_pc.IsReady, Is.False);
            Assert.That(_pc.enabled, Is.False, "a PC with broken new-game content stays out of play");
            Assert.That(_pc.Assembly.InstalledComponents, Is.Empty, "nothing is recorded");
            Assert.That(Items().Where(item => item.Instance != null).Select(item => item.Instance.Location), Is.All.EqualTo(ItemLocation.World), "no item moved");
        }

        // Unity's order for a loaded scene: every Awake (items included) before any Start.
        private void StartNewGame()
        {
            foreach (WorldItem item in Items())
                Invoke(item, "Awake");

            Invoke(_pc, "Awake");
            Invoke(_pc, "Start");
        }

        private WorldItem[] Items()
        {
            return _pc.GetComponentsInChildren<WorldItem>(true);
        }

        private PcComponentSlot Slot(string slotId)
        {
            Assert.That(_pc.Slots.Any(slot => slot.SlotId == slotId), Is.True, slotId);
            return _pc.Slots.Single(slot => slot.SlotId == slotId);
        }

        // The item authored on a slot's anchor.
        private WorldItem Item(string slotId)
        {
            return Slot(slotId).InstallAnchor.GetComponentInChildren<WorldItem>(true);
        }

        private void SetEntry(int index, PcComponentSlot slot, WorldItem item)
        {
            SerializedObject behaviour = new(_pc);
            SerializedProperty entry = behaviour.FindProperty("preinstalled").GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("slot").objectReferenceValue = slot;
            entry.FindPropertyRelative("item").objectReferenceValue = item;
            behaviour.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Invoke(object target, string method)
        {
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        }
    }
}
