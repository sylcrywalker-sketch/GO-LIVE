using System;
using System.Collections;
using System.Collections.Generic;
using GoLive.Interaction;
using GoLive.Inventory;
using GoLive.Items;
using GoLive.PcBuilding;
using GoLive.Player;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GoLive.Tests
{
    public sealed class PcPeripheralsPlayModeTests
    {
        private readonly List<Object> _owned = new();
        private SceneSetup[] _previousScenes;
        private PcPeripheralsBehaviour _rig;
        private PcPeripheralSocket _microphoneSocket;
        private PcPeripheralSocket _webcamSocket;
        private PlayerController _player;
        private PlayerCarry _carry;
        private PlayerInventory _inventory;
        private WorldItem _starter;

        [OneTimeSetUp]
        public void IsolateScene() => _previousScenes = SaveTestWorld.IsolateScene();

        [OneTimeTearDown]
        public void RestoreScene() => SaveTestWorld.RestoreScene(_previousScenes);

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int i = _owned.Count - 1; i >= 0; i--)
                if (_owned[i] != null) Object.DestroyImmediate(_owned[i]);
            _owned.Clear();
            _starter = null;
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator InventoryDeviceConnectsOnlyAfterTheSameItemMovesThroughHands()
        {
            yield return new EnterPlayMode(false);
            yield return BuildRig();
            WorldItem mic = CreateItem("microphone", PcPeripheralKind.Microphone);
            ItemInstance instance = mic.Instance;

            Assert.That(_carry.TryCarry(mic), Is.True);
            Assert.That(_rig.State.HasMicrophone, Is.False, "holding a device is not connecting it");
            Assert.That(_inventory.TryStoreCarriedItem(), Is.True);
            Assert.That(_rig.TryConnectCarried(PcPeripheralKind.Microphone, _carry), Is.False);
            Assert.That(_rig.State.HasMicrophone, Is.False);
            Assert.That(_inventory.TryTakeToCarry(instance.InstanceId), Is.True);
            Assert.That(_rig.TryConnectCarried(PcPeripheralKind.Microphone, _carry), Is.True);

            Assert.That(mic.Instance, Is.SameAs(instance));
            Assert.That(_carry.HasItem, Is.False);
            Assert.That(_inventory.Inventory.Contains(instance.InstanceId), Is.False);
            AssertInstalled(mic, _microphoneSocket);
            Assert.That(_rig.TryGetConnectedItem(PcPeripheralKind.Microphone, out WorldItem connected), Is.True);
            Assert.That(connected, Is.SameAs(mic));
            Assert.That(_carry.TryCarry(mic), Is.False, "installed devices cannot be ordinary world pickups");

            Assert.That(_rig.TryDisconnectToCarry(PcPeripheralKind.Microphone, _carry), Is.True);
            Assert.That(_carry.CarriedItem, Is.SameAs(mic));
            Assert.That(mic.Instance, Is.SameAs(instance));
            Assert.That(instance.Location, Is.EqualTo(ItemLocation.Carried));
            Assert.That(_rig.State.HasMicrophone, Is.False);
        }

        [UnityTest]
        public IEnumerator WrongDeviceOccupiedSocketAndBusyHandsDoNotMoveItems()
        {
            yield return new EnterPlayMode(false);
            yield return BuildRig();
            WorldItem mic = CreateItem("microphone", PcPeripheralKind.Microphone);
            WorldItem webcam = CreateItem("webcam", PcPeripheralKind.Webcam);
            WorldItem secondMic = CreateItem("second-microphone", PcPeripheralKind.Microphone);

            Assert.That(_carry.TryCarry(webcam), Is.True);
            Assert.That(_rig.TryConnectCarried(PcPeripheralKind.Microphone, _carry), Is.False);
            Assert.That(_carry.CarriedItem, Is.SameAs(webcam));
            Assert.That(_rig.TryConnectCarried(PcPeripheralKind.Webcam, _carry), Is.True);
            Assert.That(_carry.TryCarry(mic), Is.True);
            Assert.That(_rig.TryDisconnectToCarry(PcPeripheralKind.Webcam, _carry), Is.False);
            Assert.That(_rig.TryConnectCarried(PcPeripheralKind.Microphone, _carry), Is.True);
            Assert.That(_carry.TryCarry(secondMic), Is.True);
            Assert.That(_rig.TryConnectCarried(PcPeripheralKind.Microphone, _carry), Is.False);
            Assert.That(_carry.CarriedItem, Is.SameAs(secondMic));
            AssertInstalled(mic, _microphoneSocket);
            AssertInstalled(webcam, _webcamSocket);
            Assert.That(_rig.State.GetConnectedId(PcPeripheralKind.Microphone), Is.EqualTo(mic.Instance.InstanceId));
        }

        [UnityTest]
        public IEnumerator SocketUsesPrimaryActionAndOnlyTheExplicitPlayer()
        {
            yield return new EnterPlayMode(false);
            yield return BuildRig();
            WorldItem mic = CreateItem("microphone", PcPeripheralKind.Microphone);
            Assert.That(_carry.TryCarry(mic), Is.True);
            InteractionContext primary = new(_player.gameObject, _player.transform, InteractionAction.Primary);
            InteractionContext use = new(_player.gameObject, _player.transform, InteractionAction.Use);
            InteractionContext stranger = new(_carry.gameObject, _carry.transform, InteractionAction.Primary);

            Assert.That(_microphoneSocket.CanInteract(in use), Is.False);
            Assert.That(_microphoneSocket.CanInteract(in stranger), Is.False);
            Assert.That(_microphoneSocket.GetPromptKey(in primary), Is.EqualTo("pc.peripheral.connect"));
            using (_player.Controls.Block(PlayerControlMask.Interaction))
            {
                Assert.That(_microphoneSocket.CanInteract(in primary), Is.False);
                Assert.That(_rig.TryConnectCarried(PcPeripheralKind.Microphone, _carry), Is.False);
            }
            _microphoneSocket.Interact(in primary);
            AssertInstalled(mic, _microphoneSocket);
            Assert.That(_microphoneSocket.GetPromptKey(in primary), Is.EqualTo("pc.peripheral.disconnect"));
            _microphoneSocket.Interact(in primary);
            Assert.That(_carry.CarriedItem, Is.SameAs(mic));
        }

        [UnityTest]
        public IEnumerator ExplicitStarterInstallsOnceAndReturnsTheActualSceneItem()
        {
            yield return new EnterPlayMode(false);
            yield return BuildRig(true);
            AssertInstalled(_starter, _microphoneSocket);
            Assert.That(_rig.State.HasWebcam, Is.False);
            Assert.That(_rig.TryDisconnectToCarry(PcPeripheralKind.Microphone, _carry), Is.True);
            Assert.That(_carry.CarriedItem, Is.SameAs(_starter));
            _rig.enabled = false;
            _rig.enabled = true;
            yield return PlayModeWait.Frames(1);
            Assert.That(_rig.State.HasMicrophone, Is.False, "re-enabling does not conjure the starter back into the socket");
            Assert.That(_carry.CarriedItem, Is.SameAs(_starter));
        }

        [UnityTest]
        public IEnumerator ChangedListenersSeeCompletePhysicalTransfersAndRestoredReferences()
        {
            yield return new EnterPlayMode(false);
            yield return BuildRig();
            WorldItem mic = CreateItem("microphone", PcPeripheralKind.Microphone);
            Assert.That(_carry.TryCarry(mic), Is.True);
            int events = 0;
            _rig.State.Changed += () =>
            {
                events++;
                if (_rig.State.HasMicrophone)
                {
                    Assert.That(_rig.TryGetConnectedItem(PcPeripheralKind.Microphone, out WorldItem item), Is.True);
                    Assert.That(item, Is.SameAs(mic));
                    AssertInstalled(item, _microphoneSocket);
                }
                else
                {
                    Assert.That(_rig.TryGetConnectedItem(PcPeripheralKind.Microphone, out _), Is.False);
                    Assert.That(_carry.CarriedItem, Is.SameAs(mic));
                }
            };
            Assert.That(_rig.TryConnectCarried(PcPeripheralKind.Microphone, _carry), Is.True);
            PcPeripheralsSnapshot snapshot = _rig.State.Capture();
            Dictionary<string, WorldItem> items = new() { [mic.Instance.InstanceId] = mic };
            for (int i = 0; i < 3; i++)
            {
                _rig.Restore(snapshot, items);
                Assert.That(_rig.TryGetRestoreAnchor(snapshot, mic.Instance.InstanceId, out Transform anchor), Is.True);
                Assert.That(anchor, Is.SameAs(_microphoneSocket.InstallAnchor));
                Assert.That(anchor.childCount, Is.EqualTo(1));
            }
            Assert.That(_rig.TryDisconnectToCarry(PcPeripheralKind.Microphone, _carry), Is.True);
            Assert.That(events, Is.GreaterThanOrEqualTo(2));
        }

        [UnityTest]
        public IEnumerator RestoreRejectsWrongTypesAndUnrecordedInstalledPeripheralsWithoutChangingState()
        {
            yield return new EnterPlayMode(false);
            yield return BuildRig(true);
            string id = _starter.Instance.InstanceId;
            Dictionary<string, WorldItem> items = new() { [id] = _starter };
            Assert.Throws<ArgumentException>(() => _rig.Restore(new PcPeripheralsSnapshot { WebcamId = id }, items));
            Assert.Throws<ArgumentException>(() => _rig.Restore(new PcPeripheralsSnapshot(), items));
            Assert.That(_rig.State.GetConnectedId(PcPeripheralKind.Microphone), Is.EqualTo(id));
            Assert.That(_rig.TryGetConnectedItem(PcPeripheralKind.Microphone, out WorldItem item), Is.True);
            Assert.That(item, Is.SameAs(_starter));
            AssertInstalled(_starter, _microphoneSocket);
        }

        [UnityTest]
        public IEnumerator AForeignCarryCannotConnectOrRemoveThePlayersDevices()
        {
            yield return new EnterPlayMode(false);
            yield return BuildRig(true);
            GameObject other = Own(new GameObject("Foreign hands"));
            other.SetActive(false);
            PlayerCarry carry = other.AddComponent<PlayerCarry>();
            SaveTestWorld.SetField(carry, "carryAnchor", Child(other.transform, "Carry anchor"));
            other.SetActive(true);
            Assert.That(_rig.TryDisconnectToCarry(PcPeripheralKind.Microphone, carry), Is.False);
            WorldItem webcam = CreateItem("webcam", PcPeripheralKind.Webcam);
            Assert.That(carry.TryCarry(webcam), Is.True);
            Assert.That(_rig.TryConnectCarried(PcPeripheralKind.Webcam, carry), Is.False);
            Assert.That(carry.CarriedItem, Is.SameAs(webcam));
            AssertInstalled(_starter, _microphoneSocket);
        }

        private IEnumerator BuildRig(bool starter = false)
        {
            // The input owner is explicit; this transfer fixture needs its control state, not movement/input lifecycle.
            GameObject actor = Own(new GameObject("Peripheral test actor"));
            actor.SetActive(false);
            _player = actor.AddComponent<PlayerController>();
            GameObject hands = Own(new GameObject("Peripheral test hands"));
            hands.SetActive(false);
            _carry = hands.AddComponent<PlayerCarry>();
            SaveTestWorld.SetField(_carry, "carryAnchor", Child(hands.transform, "Carry anchor"));
            _inventory = hands.AddComponent<PlayerInventory>();
            SaveTestWorld.SetField(_inventory, "storedItemsRoot", Child(hands.transform, "Inventory storage"));
            hands.SetActive(true);

            GameObject root = Own(new GameObject("Peripheral test rig"));
            root.SetActive(false);
            _rig = root.AddComponent<PcPeripheralsBehaviour>();
            _microphoneSocket = CreateSocket(root.transform, PcPeripheralKind.Microphone);
            _webcamSocket = CreateSocket(root.transform, PcPeripheralKind.Webcam);
            SaveTestWorld.SetField(_rig, "microphoneSocket", _microphoneSocket);
            SaveTestWorld.SetField(_rig, "webcamSocket", _webcamSocket);
            SaveTestWorld.SetField(_rig, "playerCarry", _carry);
            SaveTestWorld.SetField(_rig, "playerController", _player);
            if (starter)
            {
                _starter = CreateItem("starter-microphone", PcPeripheralKind.Microphone, _microphoneSocket.InstallAnchor);
                SaveTestWorld.SetField(_rig, "initialMicrophone", _starter);
            }
            root.SetActive(true);
            yield return PlayModeWait.Frames(1);
            Assert.That(_rig.IsReady, Is.True);
        }

        private PcPeripheralSocket CreateSocket(Transform parent, PcPeripheralKind kind)
        {
            Transform root = Child(parent, kind + " socket");
            PcPeripheralSocket socket = root.gameObject.AddComponent<PcPeripheralSocket>();
            SaveTestWorld.SetField(socket, "kind", kind);
            SaveTestWorld.SetField(socket, "peripherals", _rig);
            SaveTestWorld.SetField(socket, "installAnchor", Child(root, "Install anchor"));
            SaveTestWorld.SetField(socket, "interactionCollider", root.gameObject.AddComponent<BoxCollider>());
            return socket;
        }

        private WorldItem CreateItem(string id, PcPeripheralKind kind, Transform parent = null)
        {
            ItemDefinition definition = Own(ShopTestData.CreateItem(id, ItemCategory.Electronics));
            SaveTestWorld.SetField(definition, "peripheralKind", kind);
            GameObject root = Own(new GameObject(id));
            root.SetActive(false);
            root.transform.SetParent(parent, false);
            root.AddComponent<BoxCollider>();
            root.AddComponent<Rigidbody>();
            WorldItem item = root.AddComponent<WorldItem>();
            SaveTestWorld.SetField(item, "definition", definition);
            SaveTestWorld.SetField(item, "authoredInstanceId", id + "-instance");
            root.SetActive(true);
            return item;
        }

        private T Own<T>(T value) where T : Object
        {
            _owned.Add(value);
            return value;
        }

        private static Transform Child(Transform parent, string name)
        {
            Transform child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static void AssertInstalled(WorldItem item, PcPeripheralSocket socket)
        {
            Assert.That(item.Instance.Location, Is.EqualTo(ItemLocation.Installed));
            Assert.That(item.transform.parent, Is.SameAs(socket.InstallAnchor));
            Assert.That(item.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(item.GetComponent<Rigidbody>().isKinematic, Is.True);
            foreach (Collider collider in item.GetComponentsInChildren<Collider>(true))
                Assert.That(collider.enabled, Is.False);
        }
    }
}
