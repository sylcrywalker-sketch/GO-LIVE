using System;
using System.IO;
using System.Linq;
using GoLive.Desktop;
using GoLive.Items;
using GoLive.PcBuilding;
using GoLive.Persistence;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GoLive.Tests
{
    public sealed class PeripheralSaveTests
    {
        private SaveTestWorld _world;
        private SceneSetup[] _previousScenes;

        [OneTimeSetUp] public void Isolate() => _previousScenes = SaveTestWorld.IsolateScene();
        [OneTimeTearDown] public void RestoreScene() => SaveTestWorld.RestoreScene(_previousScenes);
        [SetUp] public void SetUp() => _world = SaveTestWorld.Create(4000);
        [TearDown] public void TearDown() => _world?.Dispose();

        [Test]
        public void GeneralSaveFixtureHasNoImplicitConnectedOrLoosePeripheral()
        {
            Assert.That(_world.Peripherals.IsReady, Is.True);
            Assert.That(_world.Peripherals.State.HasMicrophone, Is.False);
            Assert.That(_world.Peripherals.State.HasWebcam, Is.False);
            Assert.That(_world.TrySave(), Is.True);
            GameSaveData saved = _world.ReadSave();
            Assert.That(saved.Items, Has.Length.EqualTo(5));
            Assert.That(saved.Items.Select(item => item.InstanceId), Is.EquivalentTo(_world.StarterItemIds));
            Assert.That(saved.Peripherals.MicrophoneId, Is.Empty);
            Assert.That(saved.Peripherals.WebcamId, Is.Empty);
        }

        [Test]
        public void MicrophoneAndWebcamRoundTripRepeatedlyWithoutCopiesOrInternalPcOwnership()
        {
            WorldItem microphone = _world.ConnectMicrophone();
            WorldItem webcam = _world.ConnectPeripheral(PcPeripheralKind.Webcam);
            string microphoneId = microphone.Instance.InstanceId;
            string webcamId = webcam.Instance.InstanceId;
            Assert.That(_world.TrySave(), Is.True);
            GameSaveData saved = _world.ReadSave();
            Assert.That(saved.Peripherals.MicrophoneId, Is.EqualTo(microphoneId));
            Assert.That(saved.Peripherals.WebcamId, Is.EqualTo(webcamId));
            Assert.That(saved.PcAssembly.InstalledSlots.Select(item => item.ItemInstanceId), Is.EquivalentTo(_world.StarterItemIds));
            Assert.That(saved.Items.Single(item => item.InstanceId == microphoneId).Location, Is.EqualTo(ItemLocation.Installed));
            Assert.That(saved.Items.Single(item => item.InstanceId == webcamId).Location, Is.EqualTo(ItemLocation.Installed));

            for (int i = 0; i < 3; i++)
            {
                Assert.That(_world.Peripherals.TryDisconnectToCarry(PcPeripheralKind.Microphone, _world.Carry), Is.True);
                Assert.That(_world.Inventory.TryStoreCarriedItem(), Is.True);
                Assert.That(_world.Peripherals.TryDisconnectToCarry(PcPeripheralKind.Webcam, _world.Carry), Is.True);
                Assert.That(_world.Carry.Drop(), Is.True);
                Assert.That(_world.TryLoad(), Is.True, "load #" + i);
                AssertConnected(PcPeripheralKind.Microphone, microphone, microphoneId);
                AssertConnected(PcPeripheralKind.Webcam, webcam, webcamId);
                Assert.That(_world.Carry.HasItem, Is.False);
                Assert.That(_world.Inventory.Inventory.Count, Is.Zero);
                Assert.That(AllItems().Length, Is.EqualTo(7));
                Assert.That(AllItems().Select(item => item.Instance.InstanceId).Distinct().Count(), Is.EqualTo(7));
            }

            Assert.That(_world.Peripherals.TryDisconnectToCarry(PcPeripheralKind.Microphone, _world.Carry), Is.True);
            Assert.That(_world.Carry.CarriedItem, Is.SameAs(microphone));
            Assert.That(_world.Peripherals.TryConnectCarried(PcPeripheralKind.Microphone, _world.Carry), Is.True);
            AssertConnected(PcPeripheralKind.Microphone, microphone, microphoneId);
        }

        [TestCase(ItemLocation.World)]
        [TestCase(ItemLocation.Carried)]
        [TestCase(ItemLocation.Inventory)]
        public void DisconnectedMicrophoneKeepsItsActualLocationAfterLoad(ItemLocation location)
        {
            WorldItem microphone = _world.ConnectMicrophone();
            string id = microphone.Instance.InstanceId;
            Assert.That(_world.Peripherals.TryDisconnectToCarry(PcPeripheralKind.Microphone, _world.Carry), Is.True);
            if (location == ItemLocation.World) Assert.That(_world.Carry.Drop(), Is.True);
            if (location == ItemLocation.Inventory) Assert.That(_world.Inventory.TryStoreCarriedItem(), Is.True);
            Assert.That(_world.TrySave(), Is.True);
            Assert.That(_world.ReadSave().Peripherals.MicrophoneId, Is.Empty);
            if (location == ItemLocation.World) Assert.That(_world.Carry.TryCarry(microphone), Is.True);
            if (location == ItemLocation.Inventory) Assert.That(_world.Inventory.TryTakeToCarry(id), Is.True);
            Assert.That(_world.Peripherals.TryConnectCarried(PcPeripheralKind.Microphone, _world.Carry), Is.True);

            Assert.That(_world.TryLoad(), Is.True);
            Assert.That(_world.Peripherals.State.HasMicrophone, Is.False);
            Assert.That(microphone.Instance.InstanceId, Is.EqualTo(id));
            Assert.That(microphone.Instance.Location, Is.EqualTo(location));
            Assert.That(_world.Carry.HasItem, Is.EqualTo(location == ItemLocation.Carried));
            Assert.That(_world.Inventory.Inventory.Contains(id), Is.EqualTo(location == ItemLocation.Inventory));
            Assert.That(AllItems().Count(item => item.Instance.InstanceId == id), Is.EqualTo(1));
        }

        [TestCase(6)]
        [TestCase(7)]
        public void OlderSaveWithoutPeripheralSectionRestoresEmptyConnectionsWithoutRoomInference(int version)
        {
            WorldItem microphone = _world.ConnectMicrophone();
            string id = microphone.Instance.InstanceId;
            Assert.That(_world.Peripherals.TryDisconnectToCarry(PcPeripheralKind.Microphone, _world.Carry), Is.True);
            Assert.That(_world.Carry.Drop(), Is.True);
            Assert.That(_world.TrySave(), Is.True);
            GameSaveData saved = _world.ReadSave();
            saved.Version = version;
            string json = RemovePeripheralSection(saved);
            Assert.That(json, Does.Not.Contain("\"Peripherals\""));
            File.WriteAllText(_world.SavePath, json);
            Assert.That(_world.Carry.TryCarry(microphone), Is.True);
            Assert.That(_world.Peripherals.TryConnectCarried(PcPeripheralKind.Microphone, _world.Carry), Is.True);

            Assert.That(_world.TryLoad(), Is.True);
            Assert.That(_world.Peripherals.State.HasMicrophone, Is.False);
            Assert.That(_world.Peripherals.State.HasWebcam, Is.False);
            Assert.That(microphone.Instance.Location, Is.EqualTo(ItemLocation.World));
            Assert.That(microphone.Instance.InstanceId, Is.EqualTo(id));
            Assert.That(_world.TrySave(), Is.True);
            Assert.That(_world.ReadSave().Peripherals.MicrophoneId, Is.Empty);
            Assert.That(_world.ReadSave().Version, Is.EqualTo(7));
        }

        [TestCase(6)]
        [TestCase(7)]
        public void SavePredatingTheAuthoredDeskMicDoesNotInventAConnection(int version)
        {
            Assert.That(_world.TrySave(), Is.True);
            GameSaveData saved = _world.ReadSave();
            saved.Version = version;
            File.WriteAllText(_world.SavePath, RemovePeripheralSection(saved));
            WorldItem microphone = _world.ConnectMicrophone();
            Assert.That(_world.TryLoad(), Is.True);
            Assert.That(_world.Peripherals.State.HasMicrophone, Is.False);
            Assert.That(microphone.Instance.Location, Is.EqualTo(ItemLocation.Removed));
            Assert.That(_world.TrySave(), Is.True);
            Assert.That(_world.ReadSave().Peripherals.MicrophoneId, Is.Empty);
        }

        [TestCase("orphan microphone")]
        [TestCase("duplicate connection")]
        [TestCase("wrong device type")]
        [TestCase("world item connection")]
        [TestCase("inventory item connection")]
        [TestCase("missing physical item")]
        [TestCase("internal component connection")]
        [TestCase("peripheral in internal slot")]
        [TestCase("duplicate item identity")]
        [TestCase("missing section with installed device")]
        public void InvalidPeripheralGraphRejectsTheEntireSaveBeforeMutation(string corruption)
        {
            WorldItem microphone = _world.ConnectMicrophone();
            WorldItem webcam = _world.ConnectPeripheral(PcPeripheralKind.Webcam);
            Assert.That(_world.TrySave(), Is.True);
            GameSaveData saved = _world.ReadSave();
            ItemSaveData mic = saved.Items.Single(item => item.InstanceId == microphone.Instance.InstanceId);
            string json = null;
            switch (corruption)
            {
                case "orphan microphone": saved.Peripherals.MicrophoneId = ""; break;
                case "duplicate connection": saved.Peripherals.WebcamId = mic.InstanceId; break;
                case "wrong device type":
                    saved.Peripherals.MicrophoneId = webcam.Instance.InstanceId;
                    saved.Peripherals.WebcamId = mic.InstanceId;
                    break;
                case "world item connection": mic.Location = ItemLocation.World; break;
                case "inventory item connection": mic.Location = ItemLocation.Inventory; mic.InventoryIndex = 0; break;
                case "missing physical item": saved.Items = saved.Items.Where(item => item != mic).ToArray(); break;
                case "internal component connection":
                    saved.Peripherals.MicrophoneId = saved.PcAssembly.InstalledSlots.Single(item => item.SlotId == "cpu-0").ItemInstanceId;
                    mic.Location = ItemLocation.World;
                    break;
                case "peripheral in internal slot":
                    saved.PcAssembly.InstalledSlots = saved.PcAssembly.InstalledSlots.Append(new PcInstalledSlotSnapshot
                    { SlotId = "gpu-0", ItemInstanceId = mic.InstanceId }).ToArray();
                    break;
                case "duplicate item identity": saved.Items = saved.Items.Append(mic).ToArray(); break;
                case "missing section with installed device": json = RemovePeripheralSection(saved); break;
            }
            File.WriteAllText(_world.SavePath, json ?? JsonUtility.ToJson(saved));
            AssertRejectedWithoutMutation();
            AssertConnected(PcPeripheralKind.Microphone, microphone, mic.InstanceId);
            AssertConnected(PcPeripheralKind.Webcam, webcam, webcam.Instance.InstanceId);
        }

        private void AssertRejectedWithoutMutation()
        {
            _world.Wallet.Wallet.Restore(321);
            _world.Clock.Clock.AdvanceMinutes(7);
            _world.Root.transform.position = new Vector3(4, 5, 6);
            _world.Desktop.State.Windows.Open(DesktopAppId.Hub);
            string items = DescribeItems();
            string assembly = JsonUtility.ToJson(_world.Pc.CaptureSnapshot());
            string peripherals = JsonUtility.ToJson(_world.Peripherals.State.Capture());
            string desktop = JsonUtility.ToJson(_world.Desktop.State.Capture());
            long time = _world.Clock.Clock.Current.TotalSeconds;
            int changes = 0;
            _world.Peripherals.State.Changed += () => changes++;
            _world.Desktop.State.Storage.Changed += () => changes++;
            _world.Carry.CarriedItemChanged += () => changes++;

            LogAssert.Expect(LogType.Error, $"Save validation failed: {_world.SavePath}");
            Assert.That(_world.TryLoad(), Is.False);

            Assert.That(DescribeItems(), Is.EqualTo(items));
            Assert.That(JsonUtility.ToJson(_world.Pc.CaptureSnapshot()), Is.EqualTo(assembly));
            Assert.That(JsonUtility.ToJson(_world.Peripherals.State.Capture()), Is.EqualTo(peripherals));
            Assert.That(JsonUtility.ToJson(_world.Desktop.State.Capture()), Is.EqualTo(desktop));
            Assert.That(_world.Desktop.State.Windows.IsOpen(DesktopAppId.Hub), Is.True);
            Assert.That(_world.Wallet.Wallet.BalanceCents, Is.EqualTo(321));
            Assert.That(_world.Clock.Clock.Current.TotalSeconds, Is.EqualTo(time));
            Assert.That(_world.Root.transform.position, Is.EqualTo(new Vector3(4, 5, 6)));
            Assert.That(changes, Is.Zero);
        }

        private void AssertConnected(PcPeripheralKind kind, WorldItem expected, string id)
        {
            Assert.That(_world.Peripherals.State.GetConnectedId(kind), Is.EqualTo(id));
            Assert.That(_world.Peripherals.TryGetConnectedItem(kind, out WorldItem actual), Is.True);
            Assert.That(actual, Is.SameAs(expected));
            Assert.That(actual.Instance.InstanceId, Is.EqualTo(id));
            Assert.That(actual.Instance.Location, Is.EqualTo(ItemLocation.Installed));
            Assert.That(_world.Peripherals.TryGetRestoreAnchor(_world.Peripherals.State.Capture(), id, out Transform anchor), Is.True);
            Assert.That(actual.transform.parent, Is.SameAs(anchor));
            Assert.That(actual.GetComponent<Rigidbody>().isKinematic, Is.True);
            Assert.That(actual.GetComponentsInChildren<Collider>(true).All(collider => !collider.enabled), Is.True);
        }

        private static string RemovePeripheralSection(GameSaveData saved)
        {
            string json = JsonUtility.ToJson(saved);
            string field = "\"Peripherals\":" + JsonUtility.ToJson(saved.Peripherals) + ",";
            Assert.That(json, Does.Contain(field));
            return json.Replace(field, "");
        }

        private static WorldItem[] AllItems() => Object.FindObjectsByType<WorldItem>(FindObjectsInactive.Include);

        private static string DescribeItems() => string.Join("\n", AllItems()
            .OrderBy(item => item.Instance.InstanceId, StringComparer.Ordinal)
            .Select(item => $"{item.GetEntityId()}|{item.Instance.InstanceId}|{item.Instance.Location}|{item.transform.parent?.GetEntityId()}|{item.transform.position}|{item.gameObject.activeSelf}"));
    }
}
