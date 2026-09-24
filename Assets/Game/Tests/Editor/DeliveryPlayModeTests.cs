using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using GoLive.Delivery;
using GoLive.Interaction;
using GoLive.Items;
using GoLive.Persistence;
using GoLive.Phone;
using GoLive.Shop;
using GoLive.Sleep;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GoLive.Tests
{
    // Runs the real delivery runtime in Play Mode: Awake/Start, Instantiate, physics, carry and the v5 save pipeline.
    public sealed class DeliveryPlayModeTests
    {
        private const string GpuId = SaveTestWorld.BudgetGpuId;
        private const string SnackId = SaveTestWorld.SnackId;
        private const string GpuItemId = "budget-gpu";
        private const string BananaItemId = "banana";
        private const string PackageItemId = "delivery-package";

        private SaveTestWorld _world;
        private SceneSetup[] _previousScenes;

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
            _world?.Dispose();
            _world = null;

            if (Application.isPlaying)
                yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator PackageArrivesOnceAtTheDropPointWhenDue()
        {
            yield return new EnterPlayMode(false);
            StartWorld();
            yield return null;

            ShopOrder order = Buy(GpuId);
            Advance(149);

            Assert.That(Packages(), Is.Empty);
            Assert.That(_world.Delivery.State.Records, Is.Empty);
            Assert.That(order.Status, Is.EqualTo(ShopOrderStatus.Placed));

            Advance(1);

            DeliveryPackageBehaviour package = Packages().Single();
            Assert.That(Vector3.Distance(package.transform.position, _world.DropPoint.position), Is.LessThan(0.01f));
            Assert.That(package.Item.Instance.Location, Is.EqualTo(ItemLocation.World));
            Assert.That(order.Status, Is.EqualTo(ShopOrderStatus.Delivered));
            Assert.That(_world.Shop.Orders.CountActive(), Is.Zero);

            DeliveryRecord record = _world.Delivery.State.Records.Single();
            Assert.That(record.OrderId, Is.EqualTo(order.OrderId));
            Assert.That(record.PackageInstanceId, Is.EqualTo(package.Item.Instance.InstanceId));
            Assert.That(record.Stage, Is.EqualTo(DeliveryStage.PackageAvailable));

            for (int i = 0; i < 5; i++)
                Advance(1);

            _world.Delivery.enabled = false;
            _world.Delivery.enabled = true;
            yield return null;

            Assert.That(Packages(), Has.Count.EqualTo(1));
            Assert.That(_world.Delivery.State.Records, Has.Count.EqualTo(1));
            Assert.That(_world.Wallet.Wallet.BalanceCents, Is.EqualTo(1000));
            Assert.That(_world.Shop.Orders.Orders, Has.Count.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator SleepingAcrossTheDueTimeDeliversExactlyOnePackage()
        {
            yield return new EnterPlayMode(false);
            StartWorld();
            yield return null;

            ShopOrder order = Buy(GpuId);
            SleepService sleep = new(_world.Clock.Clock, _world.Needs.Needs, new SleepRules(1, 12, 8, 12.5f));

            Assert.That(sleep.TrySleep(8, out SleepResult result), Is.True);
            Assert.That(result.StartedAt.TotalSeconds, Is.LessThan(order.DeliveryDueAt.TotalSeconds));
            Assert.That(result.FinishedAt.TotalSeconds, Is.GreaterThan(order.DeliveryDueAt.TotalSeconds));

            Assert.That(Packages(), Has.Count.EqualTo(1));
            Assert.That(_world.Delivery.State.Records, Has.Count.EqualTo(1));
            Assert.That(order.Status, Is.EqualTo(ShopOrderStatus.Delivered));
        }

        [UnityTest]
        public IEnumerator EveryDueOrderGetsItsOwnPackage()
        {
            yield return new EnterPlayMode(false);
            StartWorld();
            yield return null;

            Buy(GpuId);
            Buy(SnackId);
            Buy(SnackId);
            Advance(151);

            List<DeliveryPackageBehaviour> packages = Packages();
            Assert.That(packages, Has.Count.EqualTo(3));
            Assert.That(_world.Delivery.State.Records.Select(record => record.OrderId), Is.EquivalentTo(_world.Shop.Orders.Orders.Select(order => order.OrderId)));
            Assert.That(_world.Shop.Orders.Orders.All(order => order.Status == ShopOrderStatus.Delivered), Is.True);

            for (int i = 0; i < packages.Count; i++)
            {
                for (int j = i + 1; j < packages.Count; j++)
                    Assert.That(Vector3.Distance(packages[i].transform.position, packages[j].transform.position), Is.GreaterThan(0.3f));
            }
        }

        [UnityTest]
        public IEnumerator OrderThatCannotBePhysicallyDeliveredStaysPlacedAndIsReportedOnce()
        {
            yield return new EnterPlayMode(false);

            ShopCatalogConfig catalog = ShopTestData.CreateCatalog(
                ShopTestData.CreateProduct(GpuId, 1500, ItemCategory.Electronics, ShopTestData.LoadItem(ShopTestData.BudgetGpuItem), maxPurchases: 1),
                ShopTestData.CreateProduct("retired-widget", 300, ItemCategory.Household, null, available: false, deliveryDelayMinutes: 0));

            StartWorld(catalog);
            yield return null;

            long now = _world.Clock.Clock.Current.TotalSeconds;
            _world.Shop.RestoreOrders(new ShopOrdersSnapshot
            {
                Orders = new[]
                {
                    new ShopOrderSnapshot
                    {
                        OrderId = "retired-order",
                        ProductId = "retired-widget",
                        PaidPriceCents = 300,
                        PlacedAtGameTimeSeconds = now,
                        DeliveryDueGameTimeSeconds = now + 60,
                        Status = ShopOrderStatus.Placed
                    }
                }
            });

            LogAssert.Expect(LogType.Error, new Regex("Order retired-order \\(retired-widget\\) was not delivered"));
            Advance(2);
            Advance(2);
            LogAssert.NoUnexpectedReceived();

            Assert.That(Packages(), Is.Empty);
            Assert.That(_world.Delivery.State.Records, Is.Empty);
            Assert.That(_world.Shop.Orders.Orders.Single().Status, Is.EqualTo(ShopOrderStatus.Placed));
        }

        [UnityTest]
        public IEnumerator OpeningTheCarriedPackageProducesExactlyOneCatalogItem()
        {
            yield return new EnterPlayMode(false);
            StartWorld();
            yield return null;

            ShopOrder order = Buy(SnackId);
            Advance(90);
            DeliveryPackageBehaviour package = Packages().Single();

            Assert.That(_world.Carry.TryCarry(package.Item), Is.True);
            Assert.That(OpenPrompt(), Is.EqualTo("interaction.open"));
            Assert.That(_world.Inventory.TryStoreCarriedItem(), Is.False);
            Assert.That(_world.Carry.CarriedItem, Is.SameAs(package.Item));

            Assert.That(_world.Carry.Drop(), Is.True);
            Assert.That(package.Item.Instance.Location, Is.EqualTo(ItemLocation.World));
            Assert.That(_world.Carry.TryCarry(package.Item), Is.True);

            Vector3 carriedAt = package.transform.position;
            Assert.That(PressUse(), Is.True);

            Assert.That(_world.Carry.HasItem, Is.False);
            Assert.That(package.Item.Instance.Location, Is.EqualTo(ItemLocation.World));
            Assert.That(package.transform.position.y, Is.EqualTo(0f).Within(0.01f));
            Assert.That(new Vector2(package.transform.position.x - carriedAt.x, package.transform.position.z - carriedAt.z).magnitude, Is.LessThan(0.01f));

            WorldItem banana = ItemsOf(BananaItemId).Single();
            DeliveryRecord record = _world.Delivery.State.Records.Single();
            Assert.That(record.Stage, Is.EqualTo(DeliveryStage.Opened));
            Assert.That(record.FulfillmentInstanceId, Is.EqualTo(banana.Instance.InstanceId));
            Assert.That(banana.Instance.DefinitionId, Is.Not.EqualTo(order.ProductId), "fulfillment comes from the catalog item, not the product ID");
            Assert.That(banana.Instance.Location, Is.EqualTo(ItemLocation.World));
            Assert.That(Vector3.Distance(banana.transform.position, package.ContentsAnchor.position), Is.LessThan(0.01f));
            Assert.That(ClosedPartsActive(package), Is.False);

            Assert.That(_world.Carry.TryCarry(package.Item), Is.True);
            Assert.That(OpenPrompt(), Is.Null);
            Assert.That(PressUse(), Is.False);
            Assert.That(PressUse(), Is.False);

            Assert.That(ItemsOf(BananaItemId), Has.Count.EqualTo(1));
            Assert.That(_world.Delivery.State.Records.Single().FulfillmentInstanceId, Is.EqualTo(banana.Instance.InstanceId));
            Assert.That(_world.Wallet.Wallet.BalanceCents, Is.EqualTo(2300));
            Assert.That(_world.Shop.Orders.Orders, Has.Count.EqualTo(1));

            Assert.That(_world.Carry.Drop(), Is.True);
            Assert.That(_world.Carry.TryCarry(banana), Is.True);
            Assert.That(banana.Instance.Location, Is.EqualTo(ItemLocation.Carried));
        }

        [UnityTest]
        public IEnumerator UnopenedPackageLyingInTheWorldOpensWhereItLies()
        {
            yield return new EnterPlayMode(false);
            StartWorld();
            yield return null;

            ShopOrder order = Buy(SnackId);
            Advance(90);
            DeliveryPackageBehaviour package = Packages().Single();

            Assert.That(WorldPrompt(package), Is.EqualTo("interaction.open"), "F offers Open while the package lies in the world");
            Assert.That(_world.Carry.TryCarry(package.Item), Is.True, "E still takes the unopened package");
            Assert.That(OpenPrompt(), Is.EqualTo("interaction.open"));
            Assert.That(_world.Carry.Drop(), Is.True, "G still drops it");
            Assert.That(package.Item.Instance.Location, Is.EqualTo(ItemLocation.World));

            package.transform.SetPositionAndRotation(new Vector3(1.5f, 0f, 3f), Quaternion.Euler(0f, 35f, 0f));
            Vector3 position = package.transform.position;
            Quaternion rotation = package.transform.rotation;

            Assert.That(WorldPrompt(package), Is.EqualTo("interaction.open"));
            Assert.That(PressUseLookingAt(package), Is.True);

            Assert.That(_world.Carry.HasItem, Is.False);
            Assert.That(package.Item.Instance.Location, Is.EqualTo(ItemLocation.World));
            Assert.That(package.transform.position, Is.EqualTo(position), "opened exactly where it lies");
            Assert.That(package.transform.rotation, Is.EqualTo(rotation));
            Assert.That(ClosedPartsActive(package), Is.False);

            WorldItem banana = ItemsOf(BananaItemId).Single();
            DeliveryRecord record = _world.Delivery.State.Records.Single();
            Assert.That(record.OrderId, Is.EqualTo(order.OrderId));
            Assert.That(record.Stage, Is.EqualTo(DeliveryStage.Opened));
            Assert.That(record.FulfillmentInstanceId, Is.EqualTo(banana.Instance.InstanceId));
            Assert.That(Vector3.Distance(banana.transform.position, package.ContentsAnchor.position), Is.LessThan(0.01f));

            Assert.That(WorldPrompt(package), Is.Null, "no Open prompt once opened");
            Assert.That(PressUseLookingAt(package), Is.False);
            Assert.That(PressUseLookingAt(package), Is.False);

            Assert.That(ItemsOf(BananaItemId), Has.Count.EqualTo(1));
            Assert.That(Packages(), Has.Count.EqualTo(1));
            Assert.That(_world.Wallet.Wallet.BalanceCents, Is.EqualTo(2300));
            Assert.That(_world.Shop.Orders.Orders, Has.Count.EqualTo(1));

            Assert.That(_world.Carry.TryCarry(package.Item), Is.True, "the opened box is still a physical item");
            Assert.That(OpenPrompt(), Is.Null);
            Assert.That(_world.Carry.Drop(), Is.True);
        }

        [UnityTest]
        public IEnumerator HoldingAnotherItemDoesNotStopOpeningAPackageInTheWorld()
        {
            yield return new EnterPlayMode(false);
            StartWorld();
            yield return null;

            Buy(GpuId);
            Buy(SnackId);
            Advance(151);

            DeliveryPackageBehaviour gpuPackage = Packages().Single(package => IsPackageFor(package, GpuId));
            DeliveryPackageBehaviour snackPackage = Packages().Single(package => package != gpuPackage);

            Assert.That(_world.Carry.TryCarry(snackPackage.Item), Is.True);
            Assert.That(PressUseLookingAt(gpuPackage), Is.True, "the carried package opens first");
            Assert.That(ItemsOf(BananaItemId), Has.Count.EqualTo(1));
            Assert.That(ItemsOf(GpuItemId), Is.Empty);

            WorldItem banana = ItemsOf(BananaItemId).Single();
            Assert.That(_world.Carry.TryCarry(banana), Is.True);
            Assert.That(PressUseLookingAt(gpuPackage), Is.True, "hands busy with an item that has no Use: the looked-at package opens");
            Assert.That(ItemsOf(GpuItemId), Has.Count.EqualTo(1));
            Assert.That(_world.Carry.CarriedItem, Is.SameAs(banana));
        }

        [UnityTest]
        public IEnumerator PackageOpenedInTheWorldSurvivesSaveAndRepeatedLoad()
        {
            yield return new EnterPlayMode(false);
            StartWorld();
            yield return null;

            Buy(GpuId);
            Advance(151);
            DeliveryPackageBehaviour opened = Packages().Single();
            string packageId = opened.Item.Instance.InstanceId;
            Assert.That(PressUseLookingAt(opened), Is.True);

            string gpuId = ItemsOf(GpuItemId).Single().Instance.InstanceId;
            Assert.That(_world.TrySave(), Is.True);
            string[] expectedIds = RuntimeItems().Select(item => item.Instance.InstanceId).OrderBy(id => id).ToArray();

            SimulateFreshSession();

            for (int i = 0; i < 3; i++)
            {
                Assert.That(_world.TryLoad(), Is.True);
                AssertRuntimeItems(expectedIds);
            }

            DeliveryPackageBehaviour package = Packages().Single();
            DeliveryRecord record = _world.Delivery.State.Records.Single();
            Assert.That(package.Item.Instance.InstanceId, Is.EqualTo(packageId));
            Assert.That(record.Stage, Is.EqualTo(DeliveryStage.Opened));
            Assert.That(record.FulfillmentInstanceId, Is.EqualTo(gpuId));
            Assert.That(ClosedPartsActive(package), Is.False);

            Assert.That(WorldPrompt(package), Is.Null, "a loaded opened package offers no Open");
            Assert.That(PressUseLookingAt(package), Is.False);
            Assert.That(ItemsOf(GpuItemId), Has.Count.EqualTo(1));
            Assert.That(_world.Wallet.Wallet.BalanceCents, Is.EqualTo(1000));
            Assert.That(_world.Shop.Orders.Orders, Has.Count.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator PackageLoadedUnopenedOpensInTheWorldOnce()
        {
            yield return new EnterPlayMode(false);
            StartWorld();
            yield return null;

            Buy(GpuId);
            Advance(151);
            string packageId = Packages().Single().Item.Instance.InstanceId;
            Assert.That(_world.TrySave(), Is.True);

            SimulateFreshSession();
            Assert.That(_world.TryLoad(), Is.True);

            DeliveryPackageBehaviour package = Packages().Single();
            Assert.That(package.Item.Instance.InstanceId, Is.EqualTo(packageId));
            Assert.That(WorldPrompt(package), Is.EqualTo("interaction.open"));
            Assert.That(PressUseLookingAt(package), Is.True);
            Assert.That(PressUseLookingAt(package), Is.False);

            Assert.That(ItemsOf(GpuItemId), Has.Count.EqualTo(1));
            Assert.That(_world.Delivery.State.Records.Single().FulfillmentInstanceId, Is.EqualTo(ItemsOf(GpuItemId).Single().Instance.InstanceId));
        }

        [UnityTest]
        public IEnumerator SaveBeforeArrivalThenLoadAndAdvanceDeliversOnce()
        {
            yield return new EnterPlayMode(false);
            StartWorld();
            yield return null;

            ShopOrder order = Buy(GpuId);
            Assert.That(_world.TrySave(), Is.True);

            Advance(151);
            DeliveryPackageBehaviour latePackage = Packages().Single();
            string latePackageId = latePackage.Item.Instance.InstanceId;

            Assert.That(_world.TryLoad(), Is.True);
            Assert.That(latePackage.gameObject.activeSelf, Is.False, "discarded at once, destroyed at the end of the frame");
            Assert.That(latePackage.Item.CanBeCarried, Is.False);
            Assert.That(_world.Delivery.State.Records, Is.Empty);
            Assert.That(_world.Shop.Orders.Orders.Single().Status, Is.EqualTo(ShopOrderStatus.Placed));

            yield return null;

            Assert.That(latePackage == null, Is.True);
            Assert.That(Packages(), Is.Empty, "the package that arrived after the save is not part of the loaded game");

            Advance(151);

            DeliveryPackageBehaviour package = Packages().Single();
            Assert.That(package.Item.Instance.InstanceId, Is.Not.EqualTo(latePackageId));
            Assert.That(_world.Delivery.State.Records.Single().OrderId, Is.EqualTo(order.OrderId));
        }

        [UnityTest]
        public IEnumerator SavingInTheSameFrameAsALoadNeverCapturesDiscardedItems()
        {
            yield return new EnterPlayMode(false);
            StartWorld();
            yield return null;

            Buy(GpuId);
            Assert.That(_world.TrySave(), Is.True);

            Advance(151);
            DeliveryPackageBehaviour latePackage = Packages().Single();
            string latePackageId = latePackage.Item.Instance.InstanceId;

            Assert.That(_world.TryLoad(), Is.True);
            Assert.That(latePackage != null, Is.True, "Object.Destroy only runs at the end of the frame");
            Assert.That(_world.TrySave(), Is.True);

            GameSaveData saved = _world.ReadSave();
            Assert.That(saved.Items.Any(item => item.InstanceId == latePackageId), Is.False);
            Assert.That(saved.Delivery.Deliveries, Is.Empty);
            Assert.That(_world.TryLoad(), Is.True);

            yield return null;

            Assert.That(latePackage == null, Is.True);
            Assert.That(Packages(), Is.Empty);

            Advance(151);
            Assert.That(Packages(), Has.Count.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator RuntimeSpawnWithAMismatchedInstanceCreatesNothing()
        {
            yield return new EnterPlayMode(false);
            StartWorld();
            yield return null;

            ItemDefinition gpu = ShopTestData.LoadItem(ShopTestData.BudgetGpuItem);
            ItemInstance banana = new("mismatched-item", BananaItemId, ItemLocation.World);

            Assert.That(WorldItem.SpawnRuntime(gpu, banana, Vector3.zero, Quaternion.identity), Is.Null);
            Assert.That(RuntimeItems(), Is.Empty);
            Assert.That(_world.TrySave(), Is.True);
            Assert.That(_world.ReadSave().Items, Is.Empty);
        }

        [UnityTest]
        public IEnumerator LoadedGameAlreadyPastTheDueTimeDeliversExactlyOnce()
        {
            yield return new EnterPlayMode(false);
            StartWorld();
            yield return null;

            ShopOrder order = Buy(GpuId);
            Assert.That(_world.TrySave(), Is.True);

            GameSaveData data = _world.ReadSave();
            data.GameTimeSeconds = order.DeliveryDueAt.TotalSeconds + 3600;
            File.WriteAllText(_world.SavePath, JsonUtility.ToJson(data));

            Assert.That(_world.TryLoad(), Is.True);
            Assert.That(_world.TryLoad(), Is.True);
            Assert.That(Packages(), Is.Empty);

            Advance(1);
            Advance(1);

            Assert.That(Packages(), Has.Count.EqualTo(1));
            Assert.That(_world.Delivery.State.Records, Has.Count.EqualTo(1));
            Assert.That(_world.Shop.Orders.Orders.Single().Status, Is.EqualTo(ShopOrderStatus.Delivered));
        }

        [UnityTest]
        public IEnumerator WaitingPackageSurvivesSaveAndLoadInAFreshSession()
        {
            yield return new EnterPlayMode(false);
            StartWorld();
            yield return null;

            Buy(GpuId);
            Advance(151);
            DeliveryPackageBehaviour saved = Packages().Single();
            string packageId = saved.Item.Instance.InstanceId;
            Vector3 savedPosition = saved.transform.position;
            Assert.That(_world.TrySave(), Is.True);

            SimulateFreshSession();
            Assert.That(Packages(), Is.Empty);

            Assert.That(_world.TryLoad(), Is.True);

            DeliveryPackageBehaviour package = Packages().Single();
            Assert.That(package.Item.Instance.InstanceId, Is.EqualTo(packageId));
            Assert.That(package.Item.Instance.Location, Is.EqualTo(ItemLocation.World));
            Assert.That(Vector3.Distance(package.transform.position, savedPosition), Is.LessThan(0.001f));
            Assert.That(ClosedPartsActive(package), Is.True);
            Assert.That(_world.Delivery.State.Records.Single().Stage, Is.EqualTo(DeliveryStage.PackageAvailable));
            Assert.That(_world.Shop.Orders.Orders.Single().Status, Is.EqualTo(ShopOrderStatus.Delivered));

            Assert.That(_world.TryLoad(), Is.True);
            Assert.That(Packages().Single(), Is.SameAs(package));

            Assert.That(_world.Carry.TryCarry(package.Item), Is.True);
            Assert.That(PressUse(), Is.True);
            Assert.That(ItemsOf(GpuItemId), Has.Count.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator CarriedPackageSurvivesSaveAndLoadAndOpensOnce()
        {
            yield return new EnterPlayMode(false);
            StartWorld();
            yield return null;

            Buy(GpuId);
            Advance(151);
            string packageId = Packages().Single().Item.Instance.InstanceId;
            Assert.That(_world.Carry.TryCarry(Packages().Single().Item), Is.True);
            Assert.That(_world.TrySave(), Is.True);

            SimulateFreshSession();
            Assert.That(_world.TryLoad(), Is.True);

            DeliveryPackageBehaviour package = Packages().Single();
            Assert.That(package.Item.Instance.InstanceId, Is.EqualTo(packageId));
            Assert.That(package.Item.Instance.Location, Is.EqualTo(ItemLocation.Carried));
            Assert.That(_world.Carry.CarriedItem, Is.SameAs(package.Item));
            Assert.That(OpenPrompt(), Is.EqualTo("interaction.open"));

            Assert.That(PressUse(), Is.True);
            Assert.That(_world.Carry.TryCarry(package.Item), Is.True);
            Assert.That(PressUse(), Is.False);
            Assert.That(ItemsOf(GpuItemId), Has.Count.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator OpenedPackageWithUncollectedItemSurvivesSaveAndLoad()
        {
            yield return new EnterPlayMode(false);
            StartWorld();
            yield return null;

            Buy(GpuId);
            Advance(151);
            Assert.That(_world.Carry.TryCarry(Packages().Single().Item), Is.True);
            Assert.That(PressUse(), Is.True);

            WorldItem gpu = ItemsOf(GpuItemId).Single();
            string gpuId = gpu.Instance.InstanceId;
            Vector3 gpuPosition = gpu.transform.position;
            Assert.That(_world.TrySave(), Is.True);

            SimulateFreshSession();
            Assert.That(_world.TryLoad(), Is.True);

            WorldItem loaded = ItemsOf(GpuItemId).Single();
            Assert.That(loaded.Instance.InstanceId, Is.EqualTo(gpuId));
            Assert.That(loaded.Instance.Location, Is.EqualTo(ItemLocation.World));
            Assert.That(Vector3.Distance(loaded.transform.position, gpuPosition), Is.LessThan(0.001f));

            DeliveryPackageBehaviour package = Packages().Single();
            DeliveryRecord record = _world.Delivery.State.Records.Single();
            Assert.That(record.Stage, Is.EqualTo(DeliveryStage.Opened));
            Assert.That(record.FulfillmentInstanceId, Is.EqualTo(gpuId));
            Assert.That(ClosedPartsActive(package), Is.False);

            Assert.That(_world.Carry.TryCarry(package.Item), Is.True);
            Assert.That(OpenPrompt(), Is.Null);
            Assert.That(PressUse(), Is.False);

            Assert.That(_world.TryLoad(), Is.True);
            Assert.That(ItemsOf(GpuItemId), Has.Count.EqualTo(1));
            Assert.That(Packages(), Has.Count.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator CollectedItemKeepsItsSavedInventoryLocation()
        {
            yield return new EnterPlayMode(false);
            StartWorld();
            yield return null;

            Buy(GpuId);
            Advance(151);
            Assert.That(_world.Carry.TryCarry(Packages().Single().Item), Is.True);
            Assert.That(PressUse(), Is.True);

            WorldItem gpu = ItemsOf(GpuItemId).Single();
            string gpuId = gpu.Instance.InstanceId;
            Assert.That(_world.Carry.TryCarry(gpu), Is.True);
            Assert.That(_world.Inventory.TryStoreCarriedItem(), Is.True);
            Assert.That(_world.TrySave(), Is.True);

            SimulateFreshSession();
            Assert.That(_world.TryLoad(), Is.True);

            WorldItem loaded = ItemsOf(GpuItemId).Single();
            Assert.That(loaded.Instance.InstanceId, Is.EqualTo(gpuId));
            Assert.That(loaded.Instance.Location, Is.EqualTo(ItemLocation.Inventory));
            Assert.That(loaded.gameObject.activeSelf, Is.False);
            Assert.That(_world.Inventory.Inventory.Contains(gpuId), Is.True);

            Advance(600);
            Assert.That(ItemsOf(GpuItemId), Has.Count.EqualTo(1));

            Assert.That(_world.Inventory.TryTakeToCarry(gpuId), Is.True);
            Assert.That(_world.Carry.CarriedItem, Is.SameAs(loaded));
        }

        [UnityTest]
        public IEnumerator RepeatedLoadNeverDuplicatesPackagesOrItems()
        {
            yield return new EnterPlayMode(false);
            StartWorld();
            yield return null;

            Buy(GpuId);
            Buy(SnackId);
            Advance(151);
            DeliveryPackageBehaviour gpuPackage = Packages().Single(package => IsPackageFor(package, GpuId));
            Assert.That(_world.Carry.TryCarry(gpuPackage.Item), Is.True);
            Assert.That(PressUse(), Is.True);
            Assert.That(_world.TrySave(), Is.True);

            string[] expectedIds = RuntimeItems().Select(item => item.Instance.InstanceId).OrderBy(id => id).ToArray();
            Assert.That(expectedIds, Has.Length.EqualTo(3));

            for (int i = 0; i < 3; i++)
            {
                Assert.That(_world.TryLoad(), Is.True);
                AssertRuntimeItems(expectedIds);
            }

            SimulateFreshSession();

            for (int i = 0; i < 2; i++)
            {
                Assert.That(_world.TryLoad(), Is.True);
                AssertRuntimeItems(expectedIds);
            }

            Assert.That(Packages(), Has.Count.EqualTo(2));
            Assert.That(ItemsOf(GpuItemId), Has.Count.EqualTo(1));
            Assert.That(ItemsOf(BananaItemId), Is.Empty);
            Assert.That(_world.Delivery.State.Records, Has.Count.EqualTo(2));
            Assert.That(_world.Wallet.Wallet.BalanceCents, Is.EqualTo(800));
            Assert.That(_world.Shop.Orders.Orders, Has.Count.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator ConsumedFulfillmentItemNeverRespawns()
        {
            yield return new EnterPlayMode(false);
            StartWorld();
            yield return null;

            Buy(SnackId);
            Advance(90);
            Assert.That(_world.Carry.TryCarry(Packages().Single().Item), Is.True);
            Assert.That(PressUse(), Is.True);

            WorldItem banana = ItemsOf(BananaItemId).Single();
            string bananaId = banana.Instance.InstanceId;
            Assert.That(_world.Carry.TryCarry(banana), Is.True);
            Assert.That(_world.Carry.TryRemoveCarriedItem(banana), Is.True);
            Assert.That(_world.TrySave(), Is.True);

            SimulateFreshSession();
            Assert.That(_world.TryLoad(), Is.True);

            WorldItem removed = ItemsOf(BananaItemId).Single();
            Assert.That(removed.Instance.InstanceId, Is.EqualTo(bananaId));
            Assert.That(removed.Instance.Location, Is.EqualTo(ItemLocation.Removed));
            Assert.That(removed.gameObject.activeSelf, Is.False);
            Assert.That(_world.Delivery.State.Records.Single().FulfillmentInstanceId, Is.EqualTo(bananaId));

            Advance(600);
            Assert.That(_world.TryLoad(), Is.True);

            Assert.That(ItemsOf(BananaItemId), Has.Count.EqualTo(1));
            Assert.That(ItemsOf(BananaItemId).Count(item => item.Instance.Location != ItemLocation.Removed), Is.Zero);
            Assert.That(Packages(), Has.Count.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator CorruptDeliveryDataRejectsTheWholeSaveBeforeAnyStateChanges(
            [Values(
                "unsupported delivery version",
                "duplicate order",
                "unknown order",
                "package for a placed order",
                "delivered order without package",
                "blank package id",
                "opened without fulfillment item",
                "fulfillment item on unopened package",
                "item id shared by two deliveries",
                "package missing from items",
                "package item has another definition",
                "fulfillment item missing from items",
                "orphan runtime item",
                "live item ids swapped between package and item",
                "delivery package saved in inventory")]
            string corruption)
        {
            yield return new EnterPlayMode(false);
            StartWorld();
            yield return null;

            Buy(GpuId);
            Buy(SnackId);
            Advance(151);
            DeliveryPackageBehaviour gpuPackage = Packages().Single(package => IsPackageFor(package, GpuId));
            Assert.That(_world.Carry.TryCarry(gpuPackage.Item), Is.True);
            Assert.That(PressUse(), Is.True);
            _world.Messages.Messages.TryAddIncoming("saved-message", "landlord", MessageContent.FromText("Тестовое сообщение"), _world.Clock.Clock.Current);
            Assert.That(_world.TrySave(), Is.True);

            GameSaveData data = _world.ReadSave();
            List<DeliveryRecordSnapshot> deliveries = data.Delivery.Deliveries.ToList();
            DeliveryRecordSnapshot opened = deliveries.Single(record => record.Stage == DeliveryStage.Opened);
            DeliveryRecordSnapshot waiting = deliveries.Single(record => record.Stage == DeliveryStage.PackageAvailable);
            List<ItemSaveData> items = data.Items.ToList();

            switch (corruption)
            {
                case "unsupported delivery version": data.Delivery.Version = 2; break;
                case "duplicate order": deliveries.Add(Copy(waiting, waiting.OrderId, "package-copy")); break;
                case "unknown order": waiting.OrderId = "retired-order"; break;
                case "package for a placed order": data.Orders.Orders.Single(order => order.OrderId == waiting.OrderId).Status = ShopOrderStatus.Placed; break;
                case "delivered order without package": deliveries.Remove(waiting); break;
                case "blank package id": waiting.PackageInstanceId = " "; break;
                case "opened without fulfillment item": opened.FulfillmentInstanceId = string.Empty; break;
                case "fulfillment item on unopened package": waiting.FulfillmentInstanceId = opened.FulfillmentInstanceId; break;
                case "item id shared by two deliveries": waiting.PackageInstanceId = opened.FulfillmentInstanceId; break;
                case "package missing from items": items.RemoveAll(item => item.InstanceId == waiting.PackageInstanceId); break;
                case "package item has another definition": items.Single(item => item.InstanceId == waiting.PackageInstanceId).DefinitionId = GpuItemId; break;
                case "fulfillment item missing from items": items.RemoveAll(item => item.InstanceId == opened.FulfillmentInstanceId); break;
                case "orphan runtime item": items.Add(new ItemSaveData { InstanceId = "orphan-gpu", DefinitionId = GpuItemId, Location = ItemLocation.World }); break;
                case "live item ids swapped between package and item":
                    string packageId = waiting.PackageInstanceId;
                    string itemId = opened.FulfillmentInstanceId;
                    ItemSaveData savedPackage = items.Single(item => item.InstanceId == packageId);
                    ItemSaveData savedItem = items.Single(item => item.InstanceId == itemId);
                    waiting.PackageInstanceId = itemId;
                    opened.FulfillmentInstanceId = packageId;
                    savedPackage.InstanceId = itemId;
                    savedItem.InstanceId = packageId;
                    break;
                case "delivery package saved in inventory":
                    Assert.That(items.Any(item => item.Location == ItemLocation.Inventory), Is.False, "index 0 is the only inventory slot in use");
                    ItemSaveData storedPackage = items.Single(item => item.InstanceId == waiting.PackageInstanceId);
                    storedPackage.Location = ItemLocation.Inventory;
                    storedPackage.InventoryIndex = 0;
                    break;
            }

            data.Delivery.Deliveries = deliveries.ToArray();
            data.Items = items.ToArray();
            File.WriteAllText(_world.SavePath, JsonUtility.ToJson(data));

            _world.Wallet.Wallet.Restore(777);
            _world.Clock.Clock.AdvanceMinutes(5);
            _world.Root.transform.position = new Vector3(1, 2, 3);
            _world.Messages.Messages.TryAddIncoming("live-message", "landlord", MessageContent.FromText("Живое сообщение"), _world.Clock.Clock.Current);
            gpuPackage.transform.position += Vector3.right;

            long clockBefore = _world.Clock.Clock.Current.TotalSeconds;
            string messagesBefore = JsonUtility.ToJson(_world.Messages.Messages.CaptureSnapshot());
            string ordersBefore = JsonUtility.ToJson(_world.Shop.CaptureOrders());
            string deliveryBefore = JsonUtility.ToJson(_world.Delivery.CaptureSnapshot());
            string itemsBefore = DescribeItems();
            int orderChanges = 0;
            _world.Shop.Orders.Changed += () => orderChanges++;

            LogAssert.Expect(LogType.Error, $"Save validation failed: {_world.SavePath}");
            Assert.That(_world.TryLoad(), Is.False);

            Assert.That(_world.Wallet.Wallet.BalanceCents, Is.EqualTo(777));
            Assert.That(_world.Clock.Clock.Current.TotalSeconds, Is.EqualTo(clockBefore));
            Assert.That(_world.Root.transform.position, Is.EqualTo(new Vector3(1, 2, 3)));
            Assert.That(JsonUtility.ToJson(_world.Messages.Messages.CaptureSnapshot()), Is.EqualTo(messagesBefore));
            Assert.That(JsonUtility.ToJson(_world.Shop.CaptureOrders()), Is.EqualTo(ordersBefore));
            Assert.That(orderChanges, Is.Zero);
            Assert.That(JsonUtility.ToJson(_world.Delivery.CaptureSnapshot()), Is.EqualTo(deliveryBefore));
            Assert.That(DescribeItems(), Is.EqualTo(itemsBefore));
        }

        private void StartWorld(ShopCatalogConfig catalog = null)
        {
            _world = SaveTestWorld.Create(2500, catalog);
            _world.StartPlayModeRuntime();
        }

        private ShopOrder Buy(string productId)
        {
            ShopPurchaseResult result = _world.Shop.TryPurchase(productId);
            Assert.That(result.Succeeded, Is.True, $"{productId}: {result.Code}");
            return result.Order;
        }

        private void Advance(double minutes)
        {
            _world.Clock.Clock.AdvanceMinutes(minutes);
        }

        private bool PressUse()
        {
            InteractionContext context = new(_world.Hands, _world.Hands.transform, InteractionAction.Use);
            return _world.Carry.TryInteractCarried(in context);
        }

        private string OpenPrompt()
        {
            InteractionContext context = new(_world.Hands, _world.Hands.transform, InteractionAction.Use);
            _world.Carry.CarriedItem.TryGetInteractionPrompt(in context, out string key);
            return key;
        }

        // F as PlayerInteractor resolves it: the carried item first, then whatever the player is looking at.
        private bool PressUseLookingAt(Component target)
        {
            InteractionContext context = new(_world.Hands, _world.Hands.transform, InteractionAction.Use);

            if (_world.Carry.TryInteractCarried(in context))
                return true;

            return InteractionResolver.TryInteract(Candidates(target), in context, target);
        }

        private string WorldPrompt(Component target)
        {
            InteractionContext context = new(_world.Hands, _world.Hands.transform, InteractionAction.Use);
            InteractionResolver.TryGetPromptKey(Candidates(target), in context, out string key);
            return key;
        }

        private static List<MonoBehaviour> Candidates(Component target)
        {
            List<MonoBehaviour> candidates = new();
            target.GetComponentInChildren<Collider>(true).GetComponentsInParent(false, candidates);
            return candidates;
        }

        private bool IsPackageFor(DeliveryPackageBehaviour package, string productId)
        {
            _world.Delivery.State.TryGetRecordForPackage(package.Item.Instance.InstanceId, out DeliveryRecord record);
            _world.Shop.TryGetOrder(record.OrderId, out ShopOrder order);
            return order.ProductId == productId;
        }

        private void SimulateFreshSession()
        {
            foreach (WorldItem item in RuntimeItems())
                Object.DestroyImmediate(item.gameObject);

            _world.Delivery.State.Restore(new DeliverySnapshot());
            _world.Shop.RestoreOrders(new ShopOrdersSnapshot());
            _world.Wallet.Wallet.Restore(2500);
        }

        private void AssertRuntimeItems(string[] expectedIds)
        {
            Assert.That(RuntimeItems().Select(item => item.Instance.InstanceId).OrderBy(id => id), Is.EqualTo(expectedIds));
        }

        private string DescribeItems()
        {
            return string.Join("\n", RuntimeItems()
                .Select(item => $"{item.Instance.InstanceId}|{item.Instance.DefinitionId}|{item.Instance.Location}|{item.transform.position:F4}|{item.gameObject.activeSelf}")
                .OrderBy(line => line));
        }

        private static bool ClosedPartsActive(DeliveryPackageBehaviour package)
        {
            string[] names = { "FlapFront_LOD0", "FlapLeft_LOD0", "FlapRear_LOD0", "FlapRight_LOD0", "SealTape_LOD0" };
            return package.GetComponentsInChildren<Transform>(true).Where(part => names.Contains(part.name)).All(part => part.gameObject.activeSelf);
        }

        private static List<DeliveryPackageBehaviour> Packages()
        {
            return Object.FindObjectsByType<DeliveryPackageBehaviour>(FindObjectsInactive.Include).ToList();
        }

        private static List<WorldItem> ItemsOf(string definitionId)
        {
            return RuntimeItems().Where(item => item.Instance.DefinitionId == definitionId).ToList();
        }

        private static List<WorldItem> RuntimeItems()
        {
            return Object.FindObjectsByType<WorldItem>(FindObjectsInactive.Include)
                .Where(item => item.IsRuntime && item.Instance != null)
                .ToList();
        }

        private static DeliveryRecordSnapshot Copy(DeliveryRecordSnapshot source, string orderId, string packageId)
        {
            return new DeliveryRecordSnapshot
            {
                OrderId = orderId,
                PackageInstanceId = packageId,
                FulfillmentInstanceId = source.FulfillmentInstanceId,
                Stage = source.Stage
            };
        }
    }
}
