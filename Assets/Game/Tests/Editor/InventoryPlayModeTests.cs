using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GoLive.Delivery;
using GoLive.Interaction;
using GoLive.Inventory;
using GoLive.Items;
using GoLive.Localization;
using GoLive.Player;
using GoLive.Shop;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace GoLive.Tests
{
    // Carried <-> Inventory in Play Mode: the same ItemInstance moves between the hands and the slots, a swap is
    // atomic, Save/Load keeps identity and order, and the TAB overlay (the real [HUD] prefab UI) commits only on a
    // valid release.
    public sealed class InventoryPlayModeTests
    {
        private const string HudPrefab = "Assets/Game/Prefab/HUD/[HUD].prefab";
        private const string CatalogPath = "Assets/Game/Scripts/Localization/Catalog/GameLocalizationCatalog.asset";

        private readonly List<GameObject> _created = new();

        private SaveTestWorld _world;
        private SceneSetup[] _previousScenes;
        private InventoryUiController _ui;
        private LocalizationContext _localization;

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
            for (int i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                    Object.DestroyImmediate(_created[i]);
            }

            _created.Clear();
            _ui = null;
            _localization = null;

            _world?.Dispose();
            _world = null;

            if (Application.isPlaying)
                yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator CarriedItemMovesIntoTheInventoryAsTheSameInstance()
        {
            yield return new EnterPlayMode(false);
            yield return StartWorld();

            WorldItem mug = SceneItem("mug-a", ShopTestData.MugItem);
            ItemInstance instance = mug.Instance;

            Assert.That(_world.Carry.TryCarry(mug), Is.True);
            Assert.That(_world.Inventory.CheckStoreCarried(), Is.EqualTo(InventoryTransfer.Store));
            Assert.That(_world.Inventory.TryStoreCarriedItem(), Is.True);

            Assert.That(_world.Carry.HasItem, Is.False);
            Assert.That(_world.Inventory.Inventory.Items.Single(), Is.SameAs(instance));
            Assert.That(instance.Location, Is.EqualTo(ItemLocation.Inventory));
            Assert.That(mug.gameObject.activeSelf, Is.False);
            Assert.That(Items(), Has.Count.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator InventoryItemMovesIntoEmptyHandsAsTheSameInstance()
        {
            yield return new EnterPlayMode(false);
            yield return StartWorld();

            WorldItem mug = SceneItem("mug-a", ShopTestData.MugItem);
            ItemInstance instance = mug.Instance;
            Store(mug);

            Assert.That(_world.Inventory.CheckTakeToCarry("mug-a"), Is.EqualTo(InventoryTransfer.Take));
            Assert.That(_world.Inventory.TryTakeToCarry("mug-a"), Is.True);

            Assert.That(_world.Carry.CarriedItem, Is.SameAs(mug));
            Assert.That(mug.Instance, Is.SameAs(instance));
            Assert.That(instance.Location, Is.EqualTo(ItemLocation.Carried));
            Assert.That(mug.gameObject.activeSelf, Is.True);
            Assert.That(_world.Inventory.Inventory.Count, Is.Zero);
            Assert.That(Items(), Has.Count.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DeliveryPackageCannotBeStoredAndStaysCarried()
        {
            yield return new EnterPlayMode(false);
            yield return StartWorld();

            DeliveryPackageBehaviour package = DeliverPackage(SaveTestWorld.BudgetGpuId);
            Assert.That(_world.Carry.TryCarry(package.Item), Is.True);
            string before = Describe();

            Assert.That(_world.Inventory.CheckStoreCarried(), Is.EqualTo(InventoryTransfer.NotStorable));
            Assert.That(_world.Inventory.TryStoreCarriedItem(), Is.False);

            Assert.That(_world.Carry.CarriedItem, Is.SameAs(package.Item));
            Assert.That(package.Item.Instance.Location, Is.EqualTo(ItemLocation.Carried));
            Assert.That(_world.Inventory.Inventory.Count, Is.Zero);
            Assert.That(Describe(), Is.EqualTo(before));
        }

        [UnityTest]
        public IEnumerator FullInventoryBlocksStoringButAllowsASwap()
        {
            yield return new EnterPlayMode(false);
            yield return StartWorld();

            List<WorldItem> stored = new();

            for (int i = 0; i < 12; i++)
            {
                WorldItem banana = SceneItem($"banana-{i:00}", ShopTestData.BananaItem);
                Store(banana);
                stored.Add(banana);
            }

            WorldItem mug = SceneItem("mug-extra", ShopTestData.MugItem);
            Assert.That(_world.Carry.TryCarry(mug), Is.True);
            string before = Describe();

            Assert.That(_world.Inventory.CheckStoreCarried(), Is.EqualTo(InventoryTransfer.InventoryFull));
            Assert.That(_world.Inventory.TryStoreCarriedItem(), Is.False);
            Assert.That(Describe(), Is.EqualTo(before));

            Assert.That(_world.Inventory.CheckTakeToCarry("banana-05"), Is.EqualTo(InventoryTransfer.Swap), "the taken item frees the slot");
            Assert.That(_world.Inventory.TryTakeToCarry("banana-05"), Is.True);

            Assert.That(_world.Carry.CarriedItem, Is.SameAs(stored[5]));
            Assert.That(_world.Inventory.Inventory.Items[5], Is.SameAs(mug.Instance));
            Assert.That(_world.Inventory.Inventory.Count, Is.EqualTo(12));
            Assert.That(Items(), Has.Count.EqualTo(13));
        }

        [UnityTest]
        public IEnumerator SwapExchangesHandsAndSlotAndKeepsBothInstances()
        {
            yield return new EnterPlayMode(false);
            yield return StartWorld();

            Store(SceneItem("banana-a", ShopTestData.BananaItem));
            WorldItem mug = SceneItem("mug-b", ShopTestData.MugItem);
            Store(mug);
            Store(SceneItem("gpu-c", ShopTestData.BudgetGpuItem));

            WorldItem held = SceneItem("mug-held", ShopTestData.MugItem);
            Assert.That(_world.Carry.TryCarry(held), Is.True);
            ItemInstance heldInstance = held.Instance;
            ItemInstance mugInstance = mug.Instance;

            Assert.That(_world.Inventory.CheckTakeToCarry("mug-b"), Is.EqualTo(InventoryTransfer.Swap));
            Assert.That(_world.Inventory.TryTakeToCarry("mug-b"), Is.True);

            Assert.That(_world.Carry.CarriedItem, Is.SameAs(mug));
            Assert.That(mug.Instance, Is.SameAs(mugInstance));
            Assert.That(mugInstance.Location, Is.EqualTo(ItemLocation.Carried));
            Assert.That(mug.gameObject.activeSelf, Is.True);
            Assert.That(held.Instance, Is.SameAs(heldInstance));
            Assert.That(heldInstance.Location, Is.EqualTo(ItemLocation.Inventory));
            Assert.That(held.gameObject.activeSelf, Is.False);
            Assert.That(InventoryIds(), Is.EqualTo(new[] { "banana-a", "mug-held", "gpu-c" }), "the held item takes the freed slot");
            AssertConsistent(4);

            Assert.That(_world.Inventory.TryTakeToCarry("mug-held"), Is.True);
            Assert.That(InventoryIds(), Is.EqualTo(new[] { "banana-a", "mug-b", "gpu-c" }));
            Assert.That(_world.Carry.CarriedItem, Is.SameAs(held));
            AssertConsistent(4);
        }

        [UnityTest]
        public IEnumerator SwapWithANonStorableHeldItemChangesNothing()
        {
            yield return new EnterPlayMode(false);
            yield return StartWorld();

            Store(SceneItem("banana-a", ShopTestData.BananaItem));
            DeliveryPackageBehaviour package = DeliverPackage(SaveTestWorld.SnackId);
            Assert.That(_world.Carry.TryCarry(package.Item), Is.True);
            string before = Describe();

            Assert.That(_world.Inventory.CheckTakeToCarry("banana-a"), Is.EqualTo(InventoryTransfer.HandsBusy));
            Assert.That(_world.Inventory.TryTakeToCarry("banana-a"), Is.False);

            Assert.That(Describe(), Is.EqualTo(before));
            Assert.That(_world.Carry.CarriedItem, Is.SameAs(package.Item));
        }

        [UnityTest]
        public IEnumerator RepeatedTransfersNeverDuplicateItems()
        {
            yield return new EnterPlayMode(false);
            yield return StartWorld();

            Store(SceneItem("banana-a", ShopTestData.BananaItem));
            Store(SceneItem("mug-b", ShopTestData.MugItem));
            Assert.That(_world.Carry.TryCarry(SceneItem("gpu-c", ShopTestData.BudgetGpuItem)), Is.True);

            for (int round = 0; round < 20; round++)
            {
                Assert.That(_world.Inventory.TryTakeToCarry(_world.Inventory.Inventory.Items[0].InstanceId), Is.True, $"swap, round {round}");
                AssertConsistent(3);

                Assert.That(_world.Inventory.TryStoreCarriedItem(), Is.True, $"store, round {round}");
                AssertConsistent(3);

                Assert.That(_world.Inventory.TryTakeToCarry(_world.Inventory.Inventory.Items[^1].InstanceId), Is.True, $"take, round {round}");
                AssertConsistent(3);
            }
        }

        [UnityTest]
        public IEnumerator SaveAndLoadKeepIdentityAndOrderAfterATransfer(
            [Values("carried to inventory", "inventory to carried", "swap")] string transfer)
        {
            yield return new EnterPlayMode(false);
            yield return StartWorld();

            Store(SceneItem("banana-a", ShopTestData.BananaItem));
            Store(SceneItem("mug-b", ShopTestData.MugItem));
            WorldItem gpu = SceneItem("gpu-c", ShopTestData.BudgetGpuItem);
            Assert.That(_world.Carry.TryCarry(gpu), Is.True);

            switch (transfer)
            {
                case "carried to inventory":
                    Assert.That(_world.Inventory.TryStoreCarriedItem(), Is.True);
                    break;
                case "inventory to carried":
                    Assert.That(_world.Carry.Drop(), Is.True);
                    Assert.That(_world.Inventory.TryTakeToCarry("banana-a"), Is.True);
                    break;
                case "swap":
                    Assert.That(_world.Inventory.TryTakeToCarry("banana-a"), Is.True);
                    break;
            }

            string expected = Describe();
            Dictionary<string, WorldItem> objects = Items().ToDictionary(item => item.Instance.InstanceId);
            Assert.That(_world.TrySave(), Is.True);

            MoveEverythingToTheWorld();
            Assert.That(Describe(), Is.Not.EqualTo(expected));

            Assert.That(_world.TryLoad(), Is.True);
            Assert.That(Describe(), Is.EqualTo(expected));

            foreach (WorldItem item in Items())
                Assert.That(item, Is.SameAs(objects[item.Instance.InstanceId]), "scene items keep their objects and IDs");

            Assert.That(_world.TryLoad(), Is.True);
            Assert.That(Describe(), Is.EqualTo(expected), "loading twice changes nothing");
        }

        [UnityTest]
        public IEnumerator DeliveredGpuBehavesLikeAnyOtherInventoryItem()
        {
            yield return new EnterPlayMode(false);
            yield return StartWorld();

            DeliveryPackageBehaviour package = DeliverPackage(SaveTestWorld.BudgetGpuId);
            Assert.That(_world.Carry.TryCarry(package.Item), Is.True);
            Assert.That(PressUse(), Is.True);

            WorldItem gpu = RuntimeItemsOf("budget-gpu").Single();
            string gpuId = gpu.Instance.InstanceId;

            Assert.That(_world.Carry.TryCarry(gpu), Is.True);
            Assert.That(_world.Inventory.TryStoreCarriedItem(), Is.True);
            Assert.That(_world.Inventory.TryTakeToCarry(gpuId), Is.True);
            Assert.That(_world.Carry.CarriedItem, Is.SameAs(gpu));

            WorldItem banana = SceneItem("banana-a", ShopTestData.BananaItem);
            Assert.That(_world.Carry.Drop(), Is.True);
            Assert.That(_world.Carry.TryCarry(banana), Is.True);
            Assert.That(_world.Inventory.TryStoreCarriedItem(), Is.True);
            Assert.That(_world.Carry.TryCarry(gpu), Is.True);
            Assert.That(_world.Inventory.TryTakeToCarry("banana-a"), Is.True, "swap the delivered GPU into the banana's slot");
            Assert.That(InventoryIds(), Is.EqualTo(new[] { gpuId }));
            Assert.That(_world.Carry.CarriedItem, Is.SameAs(banana));

            string expected = Describe();
            Assert.That(_world.TrySave(), Is.True);

            SimulateFreshSession();
            Assert.That(_world.TryLoad(), Is.True);
            Assert.That(Describe(), Is.EqualTo(expected));

            WorldItem loaded = RuntimeItemsOf("budget-gpu").Single();
            Assert.That(loaded.Instance.InstanceId, Is.EqualTo(gpuId));
            Assert.That(loaded.Instance.Location, Is.EqualTo(ItemLocation.Inventory));
            Assert.That(loaded.gameObject.activeSelf, Is.False);

            Assert.That(_world.Inventory.TryTakeToCarry(gpuId), Is.True, "swap back after the load");
            Assert.That(_world.Carry.CarriedItem, Is.SameAs(loaded));
            Assert.That(InventoryIds(), Is.EqualTo(new[] { "banana-a" }));
            Assert.That(RuntimeItemsOf("budget-gpu"), Has.Count.EqualTo(1));
            Assert.That(_world.Delivery.State.Records.Single().FulfillmentInstanceId, Is.EqualTo(gpuId));
        }

        [UnityTest]
        public IEnumerator DraggingTheHeldCardOntoThePanelStoresTheItem()
        {
            yield return new EnterPlayMode(false);
            yield return StartWorld();
            yield return CreateUi();

            WorldItem mug = SceneItem("mug-a", ShopTestData.MugItem);
            Assert.That(_world.Carry.TryCarry(mug), Is.True);
            _ui.Open();

            InventoryItemView card = HeldCard();
            Assert.That(card.InstanceId, Is.EqualTo("mug-a"));

            PointerEventData pointer = BeginDrag(card);
            MoveTo(card, pointer, OutsidePanel());
            Assert.That(GhostHint(), Is.EqualTo(Text("inventory.drop.on_panel")));

            MoveTo(card, pointer, PanelCentre());
            Assert.That(GhostHint(), Is.EqualTo(Text("inventory.drop.store")));
            Assert.That(_world.Carry.CarriedItem, Is.SameAs(mug), "dragging alone never moves the item");
            Assert.That(_world.Inventory.Inventory.Count, Is.Zero);

            card.OnEndDrag(pointer);

            Assert.That(_ui.IsDragging, Is.False);
            Assert.That(_world.Carry.HasItem, Is.False);
            Assert.That(InventoryIds(), Is.EqualTo(new[] { "mug-a" }));
            Assert.That(mug.Instance.Location, Is.EqualTo(ItemLocation.Inventory));
            Assert.That(Slot(0).InstanceId, Is.EqualTo("mug-a"));
            Assert.That(card.IsEmpty, Is.True);
        }

        [UnityTest]
        public IEnumerator DraggingASlotOutOfThePanelTakesOrSwapsTheItem()
        {
            yield return new EnterPlayMode(false);
            yield return StartWorld();
            yield return CreateUi();

            Store(SceneItem("banana-a", ShopTestData.BananaItem));
            WorldItem mug = SceneItem("mug-b", ShopTestData.MugItem);
            Store(mug);
            _ui.Open();

            PointerEventData pointer = BeginDrag(Slot(1));
            MoveTo(Slot(1), pointer, PanelCentre());
            Assert.That(GhostHint(), Is.EqualTo(Text("inventory.drop.outside")));
            MoveTo(Slot(1), pointer, OutsidePanel());
            Assert.That(GhostHint(), Is.EqualTo(Text("inventory.drop.take")));
            Assert.That(_world.Carry.HasItem, Is.False, "dragging alone never moves the item");
            Slot(1).OnEndDrag(pointer);

            Assert.That(_world.Carry.CarriedItem, Is.SameAs(mug));
            Assert.That(InventoryIds(), Is.EqualTo(new[] { "banana-a" }));
            Assert.That(HeldCard().InstanceId, Is.EqualTo("mug-b"));

            pointer = BeginDrag(Slot(0));
            MoveTo(Slot(0), pointer, OutsidePanel());
            Assert.That(GhostHint(), Is.EqualTo(Text("inventory.drop.swap")));
            Slot(0).OnEndDrag(pointer);

            Assert.That(_world.Carry.CarriedItem.Instance.InstanceId, Is.EqualTo("banana-a"));
            Assert.That(InventoryIds(), Is.EqualTo(new[] { "mug-b" }));
            Assert.That(Slot(0).InstanceId, Is.EqualTo("mug-b"));
            AssertConsistent(2);
        }

        [UnityTest]
        public IEnumerator RejectedAndInvalidDropsChangeNothingAndSurviveSaveLoad()
        {
            yield return new EnterPlayMode(false);
            yield return StartWorld();
            yield return CreateUi();

            Store(SceneItem("banana-a", ShopTestData.BananaItem));
            DeliveryPackageBehaviour package = DeliverPackage(SaveTestWorld.BudgetGpuId);
            Assert.That(_world.Carry.TryCarry(package.Item), Is.True);
            _ui.Open();
            string before = Describe();

            PointerEventData pointer = BeginDrag(HeldCard());
            MoveTo(HeldCard(), pointer, PanelCentre());
            Assert.That(GhostHint(), Is.EqualTo(Text("inventory.reject.not_storable")));
            HeldCard().OnEndDrag(pointer);
            Assert.That(Describe(), Is.EqualTo(before), "delivery package released over the panel");
            Assert.That(PanelHint(), Is.EqualTo(Text("inventory.reject.not_storable")));

            pointer = BeginDrag(Slot(0));
            MoveTo(Slot(0), pointer, OutsidePanel());
            Assert.That(GhostHint(), Is.EqualTo(Text("inventory.reject.hands_busy")));
            Slot(0).OnEndDrag(pointer);
            Assert.That(Describe(), Is.EqualTo(before), "slot released over the world while the hands hold the package");
            Assert.That(PanelHint(), Is.EqualTo(Text("inventory.reject.hands_busy")));

            pointer = BeginDrag(Slot(0));
            MoveTo(Slot(0), pointer, PanelCentre());
            Slot(0).OnEndDrag(pointer);
            Assert.That(Describe(), Is.EqualTo(before), "slot released inside the panel");

            pointer = BeginDrag(HeldCard());
            MoveTo(HeldCard(), pointer, OutsidePanel());
            HeldCard().OnEndDrag(pointer);
            Assert.That(Describe(), Is.EqualTo(before), "held card released over the world");
            Assert.That(_ui.IsDragging, Is.False);

            Assert.That(_world.TrySave(), Is.True);
            Assert.That(_world.Carry.Drop(), Is.True);
            Assert.That(_world.TryLoad(), Is.True);
            Assert.That(Describe(), Is.EqualTo(before));
        }

        [UnityTest]
        public IEnumerator OverlayMirrorsTheLoadedInventoryAndHands()
        {
            yield return new EnterPlayMode(false);
            yield return StartWorld();
            yield return CreateUi();

            Store(SceneItem("banana-a", ShopTestData.BananaItem));
            Assert.That(_world.Carry.TryCarry(SceneItem("mug-b", ShopTestData.MugItem)), Is.True);
            _ui.Open();
            Assert.That(_world.TrySave(), Is.True);

            PointerEventData pointer = BeginDrag(Slot(0));
            MoveTo(Slot(0), pointer, OutsidePanel());
            Slot(0).OnEndDrag(pointer);
            Assert.That(Slot(0).InstanceId, Is.EqualTo("mug-b"));
            Assert.That(HeldCard().InstanceId, Is.EqualTo("banana-a"));

            Assert.That(_world.TryLoad(), Is.True);

            Assert.That(InventoryIds(), Is.EqualTo(new[] { "banana-a" }));
            Assert.That(Slot(0).InstanceId, Is.EqualTo("banana-a"), "the grid redraws from the loaded Inventory");
            Assert.That(HeldCard().InstanceId, Is.EqualTo("mug-b"), "the held card redraws from the loaded hands");
            Assert.That(Field<TMP_Text>(_ui, "capacityText").text, Is.EqualTo("1 / 12"));
        }

        [UnityTest]
        public IEnumerator ClosingOrChangingItemsMidDragCancelsWithoutChanges()
        {
            yield return new EnterPlayMode(false);
            yield return StartWorld();
            yield return CreateUi();

            Store(SceneItem("banana-a", ShopTestData.BananaItem));
            WorldItem mug = SceneItem("mug-b", ShopTestData.MugItem);
            Assert.That(_world.Carry.TryCarry(mug), Is.True);
            _ui.Open();
            string before = Describe();

            InventoryItemView slot = Slot(0);
            PointerEventData pointer = BeginDrag(slot);
            MoveTo(slot, pointer, OutsidePanel());
            Assert.That(GhostHint(), Is.EqualTo(Text("inventory.drop.swap")));

            _ui.Close();
            Assert.That(_ui.IsDragging, Is.False);
            Assert.That(Describe(), Is.EqualTo(before));

            slot.OnEndDrag(pointer);
            Assert.That(Describe(), Is.EqualTo(before), "a late release after closing does nothing");

            _ui.Open();
            pointer = BeginDrag(HeldCard());
            MoveTo(HeldCard(), pointer, PanelCentre());
            Assert.That(_world.Inventory.TryStoreCarriedItem(), Is.True, "items change while the preview is on screen");
            Assert.That(_ui.IsDragging, Is.False, "the stale drag is cancelled");

            string afterStore = Describe();
            HeldCard().OnEndDrag(pointer);
            Assert.That(Describe(), Is.EqualTo(afterStore));
            Assert.That(InventoryIds(), Is.EqualTo(new[] { "banana-a", "mug-b" }));
        }

        [UnityTest]
        public IEnumerator OpeningAndClosingRepeatedlyKeepsOneSubscriptionAndRestoresControls()
        {
            yield return new EnterPlayMode(false);
            yield return StartWorld();
            yield return CreateUi();

            PlayerControlState controls = _world.Root.GetComponent<PlayerController>().Controls;
            int inventoryListeners = Listeners(_world.Inventory.Inventory, "Changed");
            int carryListeners = Listeners(_world.Carry, "CarriedItemChanged");
            CursorLockMode lockBefore = Cursor.lockState;

            Assert.That(inventoryListeners, Is.EqualTo(2), "grid and overlay");
            Assert.That(carryListeners, Is.EqualTo(1), "overlay");

            for (int i = 0; i < 10; i++)
            {
                _ui.Open();
                _ui.Open();
                Assert.That(_ui.IsOpen, Is.True);
                Assert.That(controls.IsAllowed(PlayerControlMask.Look), Is.False);
                Assert.That(controls.IsAllowed(PlayerControlMask.Interaction), Is.False);
                Assert.That(controls.IsAllowed(PlayerControlMask.Movement), Is.True, "the world stays live");
                Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));

                _ui.Close();
                _ui.Close();
                Assert.That(_ui.IsOpen, Is.False);
                Assert.That(controls.IsAllowed(PlayerControlMask.All), Is.True);
                Assert.That(Cursor.lockState, Is.EqualTo(lockBefore));
            }

            _ui.Open();
            _ui.gameObject.SetActive(false);
            Assert.That(controls.IsAllowed(PlayerControlMask.All), Is.True, "disabling while open releases the controls");
            _ui.gameObject.SetActive(true);

            _ui.enabled = false;
            _ui.enabled = true;

            Assert.That(Listeners(_world.Inventory.Inventory, "Changed"), Is.EqualTo(inventoryListeners));
            Assert.That(Listeners(_world.Carry, "CarriedItemChanged"), Is.EqualTo(carryListeners));

            WorldItem mug = SceneItem("mug-a", ShopTestData.MugItem);
            Assert.That(_world.Carry.TryCarry(mug), Is.True);
            _ui.Open();
            Assert.That(HeldCard().InstanceId, Is.EqualTo("mug-a"));
            Assert.That(_world.Inventory.TryStoreCarriedItem(), Is.True);
            Assert.That(HeldCard().IsEmpty, Is.True, "the held card follows the hands");
            Assert.That(Slot(0).InstanceId, Is.EqualTo("mug-a"));
        }

        // Call right after a top-level "yield return new EnterPlayMode(false)" (nested enumerators cannot enter Play Mode).
        private IEnumerator StartWorld()
        {
            _world = SaveTestWorld.Create(2500);
            _world.StartPlayModeRuntime();

            yield return null;
        }

        // The real Inventory UI subtree of [HUD].prefab under a test canvas, wired to the test player.
        private IEnumerator CreateUi()
        {
            GameObject eventSystem = new("Test event system", typeof(EventSystem));
            _created.Add(eventSystem);

            GameObject localization = new("Test localization");
            localization.SetActive(false);
            _localization = localization.AddComponent<LocalizationContext>();
            SaveTestWorld.SetField(_localization, "_catalog", AssetDatabase.LoadAssetAtPath<LocalizationCatalog>(CatalogPath));
            localization.SetActive(true);
            _created.Add(localization);

            GameObject canvasRoot = new("Test HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasRoot.SetActive(false);
            canvasRoot.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _created.Add(canvasRoot);

            Transform source = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefab).transform.Find("InventoryUI");
            GameObject ui = Object.Instantiate(source.gameObject, canvasRoot.transform, false);

            _ui = ui.GetComponent<InventoryUiController>();
            SaveTestWorld.SetField(_ui, "playerInventory", _world.Inventory);
            SaveTestWorld.SetField(_ui, "playerCarry", _world.Carry);
            SaveTestWorld.SetField(_ui, "playerController", _world.Root.GetComponent<PlayerController>());
            SaveTestWorld.SetField(_ui, "localization", _localization);

            canvasRoot.SetActive(true);
            yield return null;

            Assert.That(_ui.isActiveAndEnabled, Is.True, "the prefab UI wires up without configuration errors");
        }

        private WorldItem SceneItem(string authoredId, string definitionAsset)
        {
            GameObject go = new($"Test item {authoredId}");
            go.SetActive(false);
            go.transform.position = new Vector3(_created.Count * 0.3f, 0.3f, 1f);
            go.AddComponent<BoxCollider>().size = Vector3.one * 0.1f;
            go.AddComponent<Rigidbody>();

            WorldItem item = go.AddComponent<WorldItem>();
            SaveTestWorld.SetField(item, "definition", ShopTestData.LoadItem(definitionAsset));
            SaveTestWorld.SetField(item, "authoredInstanceId", authoredId);

            go.SetActive(true);
            _created.Add(go);
            return item;
        }

        private void Store(WorldItem item)
        {
            Assert.That(_world.Carry.TryCarry(item), Is.True, $"carry {item.name}");
            Assert.That(_world.Inventory.TryStoreCarriedItem(), Is.True, $"store {item.name}");
        }

        private DeliveryPackageBehaviour DeliverPackage(string productId)
        {
            ShopPurchaseResult result = _world.Shop.TryPurchase(productId);
            Assert.That(result.Succeeded, Is.True, $"{productId}: {result.Code}");

            _world.Clock.Clock.AdvanceMinutes(151);
            return Object.FindObjectsByType<DeliveryPackageBehaviour>(FindObjectsInactive.Include).Single();
        }

        private bool PressUse()
        {
            InteractionContext context = new(_world.Hands, _world.Hands.transform, InteractionAction.Use);
            return _world.Carry.TryInteractCarried(in context);
        }

        private void MoveEverythingToTheWorld()
        {
            while (_world.Carry.HasItem || _world.Inventory.Inventory.Count > 0)
            {
                if (_world.Carry.HasItem)
                    Assert.That(_world.Carry.Drop(), Is.True);
                else
                    Assert.That(_world.Inventory.TryTakeToCarry(_world.Inventory.Inventory.Items[0].InstanceId), Is.True);
            }
        }

        private void SimulateFreshSession()
        {
            foreach (WorldItem item in Items().Where(item => item.IsRuntime))
                Object.DestroyImmediate(item.gameObject);

            _world.Delivery.State.Restore(new DeliverySnapshot());
            _world.Shop.RestoreOrders(new ShopOrdersSnapshot());
            _world.Wallet.Wallet.Restore(2500);
        }

        private PointerEventData BeginDrag(InventoryItemView view)
        {
            PointerEventData pointer = new(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = ScreenPoint((RectTransform)view.transform)
            };

            view.OnBeginDrag(pointer);
            Assert.That(_ui.IsDragging, Is.True, $"drag started on {view.name}");
            return pointer;
        }

        private static void MoveTo(InventoryItemView view, PointerEventData pointer, Vector2 position)
        {
            pointer.position = position;
            view.OnDrag(pointer);
        }

        private Vector2 PanelCentre()
        {
            return ScreenPoint(Field<RectTransform>(_ui, "panel"));
        }

        private Vector2 OutsidePanel()
        {
            Vector2 point = new(Screen.width * 0.08f, Screen.height * 0.5f);
            Assert.That(RectTransformUtility.RectangleContainsScreenPoint(Field<RectTransform>(_ui, "panel"), point, null), Is.False);
            return point;
        }

        private static Vector2 ScreenPoint(RectTransform rect)
        {
            return RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        }

        private InventoryItemView HeldCard()
        {
            return Field<InventoryItemView>(_ui, "heldItem");
        }

        private InventoryItemView Slot(int index)
        {
            return Field<InventoryItemView[]>(Field<InventoryGridView>(_ui, "grid"), "slots")[index];
        }

        private string GhostHint()
        {
            return Field<TMP_Text>(Field<InventoryDragGhost>(_ui, "dragGhost"), "hint").text;
        }

        private string PanelHint()
        {
            return Field<TMP_Text>(_ui, "hintText").text;
        }

        private string Text(string key)
        {
            return _localization.Text(key);
        }

        private string[] InventoryIds()
        {
            return _world.Inventory.Inventory.Items.Select(item => item.InstanceId).ToArray();
        }

        private void AssertConsistent(int expectedItems)
        {
            List<WorldItem> items = Items();
            Assert.That(items, Has.Count.EqualTo(expectedItems));
            Assert.That(items.Select(item => item.Instance.InstanceId).Distinct().Count(), Is.EqualTo(expectedItems));
            Assert.That(InventoryIds().Distinct().Count(), Is.EqualTo(_world.Inventory.Inventory.Count));

            int carried = items.Count(item => item.Instance.Location == ItemLocation.Carried);
            Assert.That(carried, Is.EqualTo(_world.Carry.HasItem ? 1 : 0));

            foreach (WorldItem item in items)
            {
                bool stored = _world.Inventory.Inventory.Contains(item.Instance.InstanceId);
                Assert.That(stored, Is.EqualTo(item.Instance.Location == ItemLocation.Inventory), item.Instance.InstanceId);
                Assert.That(item.gameObject.activeSelf, Is.EqualTo(!stored), item.Instance.InstanceId);
            }
        }

        // Every item's identity, location and physical state, plus the Inventory order and the hands.
        private string Describe()
        {
            IEnumerable<string> items = Items()
                .Select(item => $"{item.Instance.InstanceId}|{item.Instance.DefinitionId}|{item.Instance.Location}|{item.gameObject.activeSelf}|{(item.transform.parent != null ? item.transform.parent.name : "-")}")
                .OrderBy(line => line, StringComparer.Ordinal);

            string hands = _world.Carry.HasItem ? _world.Carry.CarriedItem.Instance.InstanceId : "-";
            return $"{string.Join("\n", items)}\ninventory={string.Join(",", InventoryIds())}\nhands={hands}";
        }

        private static List<WorldItem> Items()
        {
            return Object.FindObjectsByType<WorldItem>(FindObjectsInactive.Include)
                .Where(item => item.Instance != null)
                .ToList();
        }

        private static List<WorldItem> RuntimeItemsOf(string definitionId)
        {
            return Items().Where(item => item.IsRuntime && item.Instance.DefinitionId == definitionId).ToList();
        }

        private static int Listeners(object target, string eventName)
        {
            Delegate handlers = (Delegate)target.GetType().GetField(eventName, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
            return handlers?.GetInvocationList().Length ?? 0;
        }

        private static T Field<T>(object target, string name)
        {
            return (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        }
    }
}
