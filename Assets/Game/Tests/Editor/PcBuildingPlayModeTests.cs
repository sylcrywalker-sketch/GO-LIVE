using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GoLive.Delivery;
using GoLive.Interaction;
using GoLive.Items;
using GoLive.PcBuilding;
using GoLive.Persistence;
using GoLive.Phone;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GoLive.Tests
{
    // The real Student PC prefab and the real Budget GPU, bought and delivered through Shop + Delivery, in Play Mode:
    // the one physical item moves Inventory -> hands -> GPU slot -> hands, and the v5 save keeps it where it is.
    public sealed class PcBuildingPlayModeTests
    {
        private const string GpuId = SaveTestWorld.BudgetGpuId;
        private const string SnackId = SaveTestWorld.SnackId;
        private const string GpuItemId = "budget-gpu";
        private const string BananaItemId = "banana";
        private const string GpuSlotId = "gpu-0";

        private SaveTestWorld _world;
        private SceneSetup[] _previousScenes;
        private ItemDefinition _banana;
        private PcComponentSpec _ramSpec;

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

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_banana != null)
                ShopTestData.Set(_banana, "pcComponent", null);

            if (_ramSpec != null)
                Object.DestroyImmediate(_ramSpec);

            _banana = null;
            _ramSpec = null;
            _world?.Dispose();
            _world = null;

            if (Application.isPlaying)
                yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator BudgetGpuGoesFromInventoryThroughTheHandsIntoTheGpuSlot()
        {
            yield return new EnterPlayMode(false);
            yield return StartWorld();

            WorldItem gpu = DeliverGpu();
            ItemInstance instance = gpu.Instance;
            string id = instance.InstanceId;
            PcComponentSlot slot = GpuSlot();

            Assert.That(_world.Carry.TryCarry(gpu), Is.True);
            Assert.That(_world.Inventory.TryStoreCarriedItem(), Is.True);
            Assert.That(instance.Location, Is.EqualTo(ItemLocation.Inventory));
            Assert.That(_world.Pc.CheckInstall(slot, _world.Carry), Is.EqualTo(PcSlotCheck.NothingInHands), "no magical Inventory -> Installed shortcut");

            Assert.That(_world.Inventory.TryTakeToCarry(id), Is.True);
            Assert.That(_world.Carry.CarriedItem, Is.SameAs(gpu));
            Assert.That(_world.Pc.CheckInstall(slot, _world.Carry), Is.EqualTo(PcSlotCheck.Allowed));

            Assert.That(_world.Pc.TryInstallCarried(slot, _world.Carry), Is.True);

            Assert.That(gpu.Instance, Is.SameAs(instance), "the same ItemInstance, never a copy");
            Assert.That(instance.InstanceId, Is.EqualTo(id));
            Assert.That(instance.Location, Is.EqualTo(ItemLocation.Installed));
            Assert.That(_world.Carry.HasItem, Is.False, "hands are empty after installing");
            Assert.That(_world.Inventory.Inventory.Contains(id), Is.False);
            AssertInstalledAt(gpu, slot);
            Assert.That(_world.Pc.Assembly.TryGetInstalled(GpuSlotId, out PcInstalledComponent record) && record.InstanceId == id, Is.True);
            Assert.That(_world.Pc.Capabilities.HasDedicatedGpu, Is.True);
            Assert.That(ItemsOf(GpuItemId), Has.Count.EqualTo(1), "no duplicate GPU");

            Vector3 installedAt = gpu.transform.position;
            yield return Frames(15);
            Assert.That(Vector3.Distance(gpu.transform.position, installedAt), Is.LessThan(1e-5f), "installed hardware never falls");
        }

        [UnityTest]
        public IEnumerator InstalledGpuIsNotAWorldPickup()
        {
            yield return new EnterPlayMode(false);
            yield return StartWorld();

            WorldItem gpu = InstallDeliveredGpu();

            Assert.That(gpu.CanBeCarried, Is.False);
            Assert.That(_world.Carry.TryCarry(gpu), Is.False, "E pickup refuses installed hardware");
            Assert.That(gpu.GetComponentsInChildren<Collider>(true).All(collider => !collider.enabled), Is.True, "no collider answers the pickup ray");
            Assert.That(gpu.GetComponent<Rigidbody>().isKinematic, Is.True);
            Assert.That(gpu.Instance.Location, Is.EqualTo(ItemLocation.Installed));
        }

        [UnityTest]
        public IEnumerator RemovingTheInstalledGpuPutsTheSameItemBackIntoEmptyHands()
        {
            yield return new EnterPlayMode(false);
            yield return StartWorld();

            WorldItem gpu = InstallDeliveredGpu();
            ItemInstance instance = gpu.Instance;
            PcComponentSlot slot = GpuSlot();

            Assert.That(_world.Pc.CheckRemove(slot, _world.Carry), Is.EqualTo(PcSlotCheck.Allowed));
            Assert.That(_world.Pc.TryRemoveToCarry(slot, _world.Carry), Is.True);

            Assert.That(_world.Carry.CarriedItem, Is.SameAs(gpu));
            Assert.That(gpu.Instance, Is.SameAs(instance));
            Assert.That(instance.Location, Is.EqualTo(ItemLocation.Carried));
            Assert.That(gpu.transform.parent, Is.SameAs(_world.Carry.transform.GetChild(0)), "attached to the carry anchor");
            Assert.That(_world.Pc.Assembly.IsSlotOccupied(GpuSlotId), Is.False);
            Assert.That(_world.Pc.Capabilities.HasDedicatedGpu, Is.False);
            Assert.That(CoversVisible(slot), Is.True, "the slot covers come back with the card gone");
            Assert.That(gpu.GetComponent<Rigidbody>().isKinematic, Is.True, "carry-safe physics");
            Assert.That(ItemsOf(GpuItemId), Has.Count.EqualTo(1));

            Assert.That(_world.Pc.TryInstallCarried(slot, _world.Carry), Is.True, "and it goes straight back in");
            AssertInstalledAt(gpu, slot);
        }

        [UnityTest]
        public IEnumerator RemovalWithBusyHandsIsRejectedAndChangesNothing()
        {
            yield return new EnterPlayMode(false);
            yield return StartWorld();

            WorldItem gpu = InstallDeliveredGpu();
            WorldItem banana = SpawnBanana();
            PcComponentSlot slot = GpuSlot();
            Assert.That(_world.Carry.TryCarry(banana), Is.True);

            Assert.That(_world.Pc.CheckRemove(slot, _world.Carry), Is.EqualTo(PcSlotCheck.HandsBusy));
            Assert.That(PcSlotCheck.HandsBusy.MessageKey(), Is.EqualTo("pc.reject.hands_busy"));
            Assert.That(_world.Pc.TryRemoveToCarry(slot, _world.Carry), Is.False);

            Assert.That(_world.Carry.CarriedItem, Is.SameAs(banana));
            AssertInstalledAt(gpu, slot);
            Assert.That(_world.Pc.Assembly.TryGetInstalled(GpuSlotId, out PcInstalledComponent record) && record.InstanceId == gpu.Instance.InstanceId, Is.True);
        }

        [UnityTest]
        public IEnumerator BananaHasNoCompatiblePcSlot()
        {
            yield return new EnterPlayMode(false);
            yield return StartWorld();

            WorldItem banana = SpawnBanana();
            PcComponentSlot slot = GpuSlot();
            Assert.That(_world.Carry.TryCarry(banana), Is.True);

            Assert.That(banana.Definition.PcComponent, Is.Null);
            Assert.That(_world.Pc.CheckInstall(slot, _world.Carry), Is.EqualTo(PcSlotCheck.NotPcHardware));
            Assert.That(_world.Pc.TryInstallCarried(slot, _world.Carry), Is.False);
            Assert.That(_world.Carry.CarriedItem, Is.SameAs(banana));
            Assert.That(banana.Instance.Location, Is.EqualTo(ItemLocation.Carried));
            Assert.That(_world.Pc.Assembly.IsSlotOccupied(GpuSlotId), Is.False);
        }

        // Cases A-D: the GPU is saved where it is and loaded back there, as the same instance.
        [UnityTest]
        public IEnumerator GpuLocationSurvivesSaveAndLoad([Values("World", "Carried", "Inventory", "Installed")] string location)
        {
            yield return new EnterPlayMode(false);
            yield return StartWorld();

            WorldItem gpu = DeliverGpu();
            string id = gpu.Instance.InstanceId;
            PcComponentSlot slot = GpuSlot();

            if (location != "World")
                Assert.That(_world.Carry.TryCarry(gpu), Is.True);

            if (location == "Inventory")
                Assert.That(_world.Inventory.TryStoreCarriedItem(), Is.True);

            if (location == "Installed")
                Assert.That(_world.Pc.TryInstallCarried(slot, _world.Carry), Is.True);

            Vector3 worldPosition = gpu.transform.position;
            Assert.That(_world.TrySave(), Is.True);

            GameSaveData saved = _world.ReadSave();
            Assert.That(saved.Items.Single(item => item.InstanceId == id).Location.ToString(), Is.EqualTo(location));
            Assert.That(saved.PcAssembly.InstalledSlots.Select(record => $"{record.SlotId}={record.ItemInstanceId}"), Is.EqualTo(location == "Installed" ? new[] { $"{GpuSlotId}={id}" } : new string[0]));

            ScrambleGpu(gpu, slot);
            Assert.That(_world.TryLoad(), Is.True);
            yield return null;

            WorldItem loaded = ItemsOf(GpuItemId).Single();
            Assert.That(loaded.Instance.InstanceId, Is.EqualTo(id));
            Assert.That(loaded.Instance.Location.ToString(), Is.EqualTo(location));
            Assert.That(_world.Pc.Assembly.IsSlotOccupied(GpuSlotId), Is.EqualTo(location == "Installed"));

            switch (location)
            {
                case "World": Assert.That(Vector3.Distance(loaded.transform.position, worldPosition), Is.LessThan(0.001f)); break;
                case "Carried": Assert.That(_world.Carry.CarriedItem, Is.SameAs(loaded)); break;
                case "Inventory": Assert.That(_world.Inventory.Inventory.Items.Single().InstanceId, Is.EqualTo(id)); break;
                case "Installed": AssertInstalledAt(loaded, slot); break;
            }
        }

        // Cases D and F: the installed GPU loads into the same slot, and loading again never duplicates anything.
        [UnityTest]
        public IEnumerator InstalledGpuSurvivesRepeatedLoadsWithoutDuplicates()
        {
            yield return new EnterPlayMode(false);
            yield return StartWorld();

            WorldItem gpu = InstallDeliveredGpu();
            string id = gpu.Instance.InstanceId;
            PcComponentSlot slot = GpuSlot();
            int worldItems = RuntimeItems().Count;
            Assert.That(_world.TrySave(), Is.True);

            for (int i = 0; i < 3; i++)
            {
                Assert.That(_world.TryLoad(), Is.True, $"load #{i + 1}");
                yield return null;

                WorldItem loaded = ItemsOf(GpuItemId).Single();
                Assert.That(loaded.Instance.InstanceId, Is.EqualTo(id));
                AssertInstalledAt(loaded, slot);
                Assert.That(RuntimeItems(), Has.Count.EqualTo(worldItems), "no duplicate runtime object");
                Assert.That(_world.Pc.Assembly.InstalledComponents.Select(c => c.InstanceId), Is.EqualTo(new[] { id }));
                Assert.That(slot.InstallAnchor.GetComponentsInChildren<WorldItem>(true), Has.Length.EqualTo(1));
                Assert.That(_world.Carry.HasItem, Is.False);
            }
        }

        // Case E: after removal the save follows the item to its new place and the slot stays empty.
        [UnityTest]
        public IEnumerator RemovedGpuIsSavedWhereverItWentNext()
        {
            yield return new EnterPlayMode(false);
            yield return StartWorld();

            WorldItem gpu = InstallDeliveredGpu();
            string id = gpu.Instance.InstanceId;
            PcComponentSlot slot = GpuSlot();
            Assert.That(_world.Pc.TryRemoveToCarry(slot, _world.Carry), Is.True);

            Assert.That(_world.TrySave(), Is.True);
            Assert.That(_world.TryLoad(), Is.True);
            yield return null;
            Assert.That(_world.Carry.CarriedItem.Instance.InstanceId, Is.EqualTo(id));
            Assert.That(_world.Pc.Assembly.IsSlotOccupied(GpuSlotId), Is.False);

            Assert.That(_world.Inventory.TryStoreCarriedItem(), Is.True);
            Assert.That(_world.TrySave(), Is.True);
            Assert.That(_world.TryLoad(), Is.True);
            yield return null;

            Assert.That(ItemsOf(GpuItemId).Single().Instance.Location, Is.EqualTo(ItemLocation.Inventory));
            Assert.That(_world.Inventory.Inventory.Contains(id), Is.True);
            Assert.That(_world.ReadSave().PcAssembly.InstalledSlots, Is.Empty, "no PC slot owns an item in the Inventory");
            Assert.That(_world.Pc.Assembly.IsSlotOccupied(GpuSlotId), Is.False);
            Assert.That(CoversVisible(slot), Is.True);
        }

        // Cases G-L: any PC record the game could never produce rejects the whole save before anything changes.
        [UnityTest]
        public IEnumerator CorruptPcDataRejectsTheWholeSaveBeforeAnyStateChanges(
            [Values(
                "installed item without slot record",
                "slot record points at a carried item",
                "slot record points at a world item",
                "slot record points at an inventory item",
                "slot record points at a removed item",
                "same item in two slots",
                "two items in one slot",
                "non-hardware item in the gpu slot",
                "wrong component type in the gpu slot",
                "unknown slot id",
                "unsupported pc version",
                "missing pc section",
                "blank item id")]
            string corruption)
        {
            yield return new EnterPlayMode(false);
            yield return StartWorld();

            Buy(SnackId);
            WorldItem gpu = InstallDeliveredGpu();
            WorldItem banana = ItemsOf(BananaItemId).Single();
            string gpuId = gpu.Instance.InstanceId;
            string packageId = Packages().Select(package => package.Item.Instance.InstanceId).First(packageIdCandidate => IsPackageFor(packageIdCandidate, GpuId));
            _world.Messages.Messages.TryAddIncoming("saved-message", "landlord", MessageContent.FromText("Saved"), _world.Clock.Clock.Current);
            Assert.That(_world.TrySave(), Is.True);

            GameSaveData data = _world.ReadSave();
            List<ItemSaveData> items = data.Items.ToList();
            ItemSaveData savedGpu = items.Single(item => item.InstanceId == gpuId);
            PcInstalledSlotSnapshot record = data.PcAssembly.InstalledSlots.Single();
            string json = null;

            switch (corruption)
            {
                case "installed item without slot record": data.PcAssembly.InstalledSlots = new PcInstalledSlotSnapshot[0]; break;
                case "slot record points at a carried item": savedGpu.Location = ItemLocation.Carried; break;
                case "slot record points at a world item": savedGpu.Location = ItemLocation.World; break;
                case "slot record points at an inventory item": savedGpu.Location = ItemLocation.Inventory; savedGpu.InventoryIndex = 0; break;
                case "slot record points at a removed item": savedGpu.Location = ItemLocation.Removed; break;
                case "same item in two slots": data.PcAssembly.InstalledSlots = new[] { record, new PcInstalledSlotSnapshot { SlotId = "gpu-1", ItemInstanceId = gpuId } }; break;
                case "two items in one slot":
                    items.Single(item => item.InstanceId == packageId).Location = ItemLocation.Installed;
                    data.PcAssembly.InstalledSlots = new[] { record, new PcInstalledSlotSnapshot { SlotId = GpuSlotId, ItemInstanceId = packageId } };
                    break;
                case "non-hardware item in the gpu slot":
                    savedGpu.Location = ItemLocation.World;
                    items.Single(item => item.InstanceId == packageId).Location = ItemLocation.Installed;
                    record.ItemInstanceId = packageId;
                    break;
                case "wrong component type in the gpu slot":
                    UseBananaAsRam();
                    savedGpu.Location = ItemLocation.World;
                    items.Single(item => item.InstanceId == banana.Instance.InstanceId).Location = ItemLocation.Installed;
                    record.ItemInstanceId = banana.Instance.InstanceId;
                    break;
                case "unknown slot id": record.SlotId = "gpu-7"; break;
                case "unsupported pc version": data.PcAssembly.Version = 2; break;
                case "missing pc section": json = JsonUtility.ToJson(data).Replace("\"PcAssembly\":" + JsonUtility.ToJson(data.PcAssembly) + ",", string.Empty); break;
                case "blank item id": record.ItemInstanceId = " "; break;
            }

            data.Items = items.ToArray();
            File.WriteAllText(_world.SavePath, json ?? JsonUtility.ToJson(data));

            // Live state moves on after the save, so a partial apply would be visible everywhere.
            Assert.That(_world.Pc.TryRemoveToCarry(GpuSlot(), _world.Carry), Is.True);
            Assert.That(_world.Inventory.TryStoreCarriedItem(), Is.True);
            _world.Wallet.Wallet.Restore(777);
            _world.Clock.Clock.AdvanceMinutes(5);
            _world.Root.transform.position = new Vector3(1, 2, 3);
            _world.Messages.Messages.TryAddIncoming("live-message", "landlord", MessageContent.FromText("Live"), _world.Clock.Clock.Current);

            long clockBefore = _world.Clock.Clock.Current.TotalSeconds;
            string messagesBefore = JsonUtility.ToJson(_world.Messages.Messages.CaptureSnapshot());
            string ordersBefore = JsonUtility.ToJson(_world.Shop.CaptureOrders());
            string deliveryBefore = JsonUtility.ToJson(_world.Delivery.CaptureSnapshot());
            string pcBefore = JsonUtility.ToJson(_world.Pc.CaptureSnapshot());
            string itemsBefore = DescribeItems();
            string[] inventoryBefore = _world.Inventory.Inventory.Items.Select(item => item.InstanceId).ToArray();

            LogAssert.Expect(LogType.Error, $"Save validation failed: {_world.SavePath}");
            Assert.That(_world.TryLoad(), Is.False);

            Assert.That(_world.Wallet.Wallet.BalanceCents, Is.EqualTo(777));
            Assert.That(_world.Clock.Clock.Current.TotalSeconds, Is.EqualTo(clockBefore));
            Assert.That(_world.Root.transform.position, Is.EqualTo(new Vector3(1, 2, 3)));
            Assert.That(JsonUtility.ToJson(_world.Messages.Messages.CaptureSnapshot()), Is.EqualTo(messagesBefore));
            Assert.That(JsonUtility.ToJson(_world.Shop.CaptureOrders()), Is.EqualTo(ordersBefore));
            Assert.That(JsonUtility.ToJson(_world.Delivery.CaptureSnapshot()), Is.EqualTo(deliveryBefore));
            Assert.That(JsonUtility.ToJson(_world.Pc.CaptureSnapshot()), Is.EqualTo(pcBefore));
            Assert.That(_world.Inventory.Inventory.Items.Select(item => item.InstanceId), Is.EqualTo(inventoryBefore));
            Assert.That(_world.Carry.HasItem, Is.False);
            Assert.That(DescribeItems(), Is.EqualTo(itemsBefore));
            Assert.That(GpuSlot().InstallAnchor.GetComponentsInChildren<WorldItem>(true), Is.Empty);
        }

        // ---------------------------------------------------------------- helpers

        private IEnumerator StartWorld()
        {
            _world = SaveTestWorld.Create(2500);
            _world.StartPlayModeRuntime();
            yield return null;
        }

        private WorldItem DeliverGpu()
        {
            Buy(GpuId);
            _world.Clock.Clock.AdvanceMinutes(151);

            DeliveryPackageBehaviour package = Packages().Single(candidate => IsPackageFor(candidate.Item.Instance.InstanceId, GpuId));
            Assert.That(_world.Carry.TryCarry(package.Item), Is.True);
            Assert.That(PressUse(), Is.True, "F opens the carried package");

            WorldItem gpu = ItemsOf(GpuItemId).Single();
            Assert.That(gpu.Instance.Location, Is.EqualTo(ItemLocation.World));
            Assert.That(_world.Carry.HasItem, Is.False);
            return gpu;
        }

        private WorldItem InstallDeliveredGpu()
        {
            WorldItem gpu = DeliverGpu();
            Assert.That(_world.Carry.TryCarry(gpu), Is.True);
            Assert.That(_world.Pc.TryInstallCarried(GpuSlot(), _world.Carry), Is.True);
            return gpu;
        }

        private void Buy(string productId)
        {
            Assert.That(_world.Shop.TryPurchase(productId).Succeeded, Is.True, productId);

            if (productId == SnackId)
            {
                _world.Clock.Clock.AdvanceMinutes(91);
                DeliveryPackageBehaviour package = Packages().Single(candidate => IsPackageFor(candidate.Item.Instance.InstanceId, SnackId));
                Assert.That(_world.Carry.TryCarry(package.Item), Is.True);
                Assert.That(PressUse(), Is.True);
            }
        }

        private bool PressUse()
        {
            InteractionContext context = new(_world.Hands, _world.Hands.transform, InteractionAction.Use);
            return _world.Carry.TryInteractCarried(in context);
        }

        private WorldItem SpawnBanana()
        {
            ItemDefinition banana = ShopTestData.LoadItem(ShopTestData.BananaItem);
            WorldItem item = WorldItem.SpawnRuntime(banana, ItemInstance.CreateNew(banana.ItemId), new Vector3(0f, 0.2f, 1f), Quaternion.identity);
            Assert.That(item, Is.Not.Null);
            return item;
        }

        // Test-only: the banana pretends to be a memory stick for one case, then TearDown clears it again.
        private void UseBananaAsRam()
        {
            _banana = ShopTestData.LoadItem(ShopTestData.BananaItem);
            _ramSpec = ScriptableObject.CreateInstance<PcComponentSpec>();
            ShopTestData.Set(_ramSpec, "componentType", PcComponentType.Ram);
            ShopTestData.Set(_ramSpec, "connector", PcConnector.PcieX16);
            ShopTestData.Set(_banana, "pcComponent", _ramSpec);
        }

        // Moves the live GPU somewhere else entirely, so a load has to put it back.
        private void ScrambleGpu(WorldItem gpu, PcComponentSlot slot)
        {
            if (gpu.Instance.Location == ItemLocation.Installed)
                Assert.That(_world.Pc.TryRemoveToCarry(slot, _world.Carry), Is.True);

            if (gpu.Instance.Location == ItemLocation.Inventory)
                Assert.That(_world.Inventory.TryTakeToCarry(gpu.Instance.InstanceId), Is.True);

            if (gpu.Instance.Location == ItemLocation.World)
                Assert.That(_world.Carry.TryCarry(gpu), Is.True);

            Assert.That(_world.Carry.TryPlace(new Vector3(2f, 0.1f, -2f), Quaternion.identity), Is.True);
        }

        private PcComponentSlot GpuSlot()
        {
            Assert.That(_world.Pc.TryGetSlot(GpuSlotId, out PcComponentSlot slot), Is.True);
            return slot;
        }

        private static void AssertInstalledAt(WorldItem gpu, PcComponentSlot slot)
        {
            Assert.That(gpu.Instance.Location, Is.EqualTo(ItemLocation.Installed));
            Assert.That(gpu.gameObject.activeInHierarchy, Is.True);
            Assert.That(gpu.transform.parent, Is.SameAs(slot.InstallAnchor));
            Assert.That(gpu.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(gpu.transform.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(Vector3.Distance(gpu.transform.position, slot.InstallAnchor.position), Is.LessThan(1e-5f));
            Assert.That(Quaternion.Angle(gpu.transform.rotation, slot.InstallAnchor.rotation), Is.LessThan(0.01f));
            Assert.That(gpu.GetComponent<Rigidbody>().isKinematic, Is.True);
            Assert.That(CoversVisible(slot), Is.False, "the card's bracket replaces the slot covers");
        }

        private static bool CoversVisible(PcComponentSlot slot)
        {
            GameObject[] covers = (GameObject[])typeof(PcComponentSlot)
                .GetField("hiddenWhileFilled", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetValue(slot);

            Assert.That(covers, Is.Not.Empty);
            return covers.All(cover => cover.activeSelf);
        }

        private bool IsPackageFor(string packageInstanceId, string productId)
        {
            _world.Delivery.State.TryGetRecordForPackage(packageInstanceId, out DeliveryRecord record);
            _world.Shop.TryGetOrder(record.OrderId, out var order);
            return order.ProductId == productId;
        }

        private string DescribeItems()
        {
            return string.Join("\n", RuntimeItems()
                .Select(item => $"{item.Instance.InstanceId}|{item.Instance.DefinitionId}|{item.Instance.Location}|{item.transform.position:F4}|{item.gameObject.activeSelf}|{(item.transform.parent != null ? item.transform.parent.name : "-")}")
                .OrderBy(line => line));
        }

        private static List<DeliveryPackageBehaviour> Packages()
        {
            return Object.FindObjectsByType<DeliveryPackageBehaviour>(FindObjectsInactive.Include).Where(package => package.Item != null && package.Item.Instance != null).ToList();
        }

        private static List<WorldItem> ItemsOf(string definitionId)
        {
            return RuntimeItems().Where(item => item.Instance.DefinitionId == definitionId).ToList();
        }

        private static List<WorldItem> RuntimeItems()
        {
            return Object.FindObjectsByType<WorldItem>(FindObjectsInactive.Include)
                .Where(item => item != null && item.IsRuntime && item.Instance != null)
                .ToList();
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
                yield return null;
        }
    }
}
