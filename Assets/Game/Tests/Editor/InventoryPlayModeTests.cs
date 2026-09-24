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
using GoLive.PcBuilding;
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
        private readonly List<Object> _createdAssets = new();

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

            foreach (Object asset in _createdAssets)
                Object.DestroyImmediate(asset);

            _createdAssets.Clear();
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

            MoveTo(card, pointer, StoredItemsPoint());
            Assert.That(GhostHint(), Is.EqualTo(Text("inventory.drop.store")));
            Assert.That(_world.Carry.CarriedItem, Is.SameAs(mug), "dragging alone never moves the item");
            Assert.That(_world.Inventory.Inventory.Count, Is.Zero);

            card.OnEndDrag(pointer);

            Assert.That(_ui.IsDragging, Is.False);
            Assert.That(_world.Carry.HasItem, Is.False);
            Assert.That(InventoryIds(), Is.EqualTo(new[] { "mug-a" }));
            Assert.That(mug.Instance.Location, Is.EqualTo(ItemLocation.Inventory));
            Assert.That(Row(0).InstanceId, Is.EqualTo("mug-a"));
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

            PointerEventData pointer = BeginDrag(Row(1));
            MoveTo(Row(1), pointer, StoredItemsPoint());
            Assert.That(GhostHint(), Is.EqualTo(Text("inventory.drop.outside")));
            MoveTo(Row(1), pointer, OutsidePanel());
            Assert.That(GhostHint(), Is.EqualTo(Text("inventory.drop.take")));
            Assert.That(_world.Carry.HasItem, Is.False, "dragging alone never moves the item");
            Row(1).OnEndDrag(pointer);

            Assert.That(_world.Carry.CarriedItem, Is.SameAs(mug));
            Assert.That(InventoryIds(), Is.EqualTo(new[] { "banana-a" }));
            Assert.That(HeldCard().InstanceId, Is.EqualTo("mug-b"));

            pointer = BeginDrag(Row(0));
            MoveTo(Row(0), pointer, OutsidePanel());
            Assert.That(GhostHint(), Is.EqualTo(Text("inventory.drop.swap")));
            Row(0).OnEndDrag(pointer);

            Assert.That(_world.Carry.CarriedItem.Instance.InstanceId, Is.EqualTo("banana-a"));
            Assert.That(InventoryIds(), Is.EqualTo(new[] { "mug-b" }));
            Assert.That(Row(0).InstanceId, Is.EqualTo("mug-b"));
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
            MoveTo(HeldCard(), pointer, StoredItemsPoint());
            Assert.That(GhostHint(), Is.EqualTo(Text("inventory.reject.not_storable")));
            HeldCard().OnEndDrag(pointer);
            Assert.That(Describe(), Is.EqualTo(before), "delivery package released over the panel");
            Assert.That(PanelHint(), Is.EqualTo(Text("inventory.reject.not_storable")));
            _localization.SetLanguage(GameLanguage.English);
            Assert.That(PanelHint(), Is.EqualTo(Text("inventory.reject.not_storable")), "visible feedback follows a language switch");
            _localization.SetLanguage(GameLanguage.Russian);

            pointer = BeginDrag(Row(0));
            MoveTo(Row(0), pointer, OutsidePanel());
            Assert.That(GhostHint(), Is.EqualTo(Text("inventory.reject.hands_busy")));
            Row(0).OnEndDrag(pointer);
            Assert.That(Describe(), Is.EqualTo(before), "row released over the world while the hands hold the package");
            Assert.That(PanelHint(), Is.EqualTo(Text("inventory.reject.hands_busy")));

            pointer = BeginDrag(Row(0));
            MoveTo(Row(0), pointer, StoredItemsPoint());
            Row(0).OnEndDrag(pointer);
            Assert.That(Describe(), Is.EqualTo(before), "row released over the stored items");

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

            PointerEventData pointer = BeginDrag(Row(0));
            MoveTo(Row(0), pointer, OutsidePanel());
            Row(0).OnEndDrag(pointer);
            Assert.That(Row(0).InstanceId, Is.EqualTo("mug-b"));
            Assert.That(HeldCard().InstanceId, Is.EqualTo("banana-a"));

            Assert.That(_world.TryLoad(), Is.True);

            Assert.That(InventoryIds(), Is.EqualTo(new[] { "banana-a" }));
            Assert.That(Row(0).InstanceId, Is.EqualTo("banana-a"), "the list redraws from the loaded Inventory");
            Assert.That(HeldCard().InstanceId, Is.EqualTo("mug-b"), "the held card redraws from the loaded hands");
            Assert.That(Field<TMP_Text>(_ui, "capacityText").text, Is.EqualTo("1 / 12"));
        }

        [UnityTest]
        public IEnumerator ListShowsOnlyStoredItemsFromTheirDefinitionData()
        {
            yield return new EnterPlayMode(false);
            yield return StartWorld();
            yield return CreateUi();

            _ui.Open();
            Assert.That(Field<GameObject>(Field<InventoryListView>(_ui, "list"), "emptyState").activeSelf, Is.True, "empty Inventory shows the empty state");
            Assert.That(Enumerable.Range(0, 12).Count(i => Row(i).gameObject.activeSelf), Is.Zero, "no placeholder slots");
            Assert.That(Field<TMP_Text>(_ui, "capacityText").text, Is.EqualTo("0 / 12"));

            ItemDefinition gadget = ShopTestData.CreateItem("test-gadget", ItemCategory.Household);
            _createdAssets.Add(gadget);

            Store(SceneItem("banana-a", ShopTestData.BananaItem));
            Store(SceneItem("mug-b", ShopTestData.MugItem));
            Store(SceneItem("gadget-c", gadget));

            Assert.That(Field<GameObject>(Field<InventoryListView>(_ui, "list"), "emptyState").activeSelf, Is.False);
            Assert.That(Enumerable.Range(0, 12).Count(i => Row(i).gameObject.activeSelf), Is.EqualTo(3), "one row per stored item");
            Assert.That(Field<TMP_Text>(_ui, "capacityText").text, Is.EqualTo("3 / 12"));

            Assert.That(Row(0).Title, Is.EqualTo(Text("shop.product.banana.name")), "names come from the definition's localization key");
            Assert.That(Field<TMP_Text>(Row(0), "detail").text, Is.EqualTo(Text("inventory.food")));
            Assert.That(Row(1).Title, Is.EqualTo(Text("shop.product.mug.name")));
            Assert.That(Row(2).Title, Is.EqualTo("Test gadget"), "a definition without a name key still gets a readable name");
            Assert.That(Field<TMP_Text>(Row(2), "detail").text, Is.EqualTo(Text("inventory.household")));
            Assert.That(Field<GameObject>(Row(2), "iconFallback").activeSelf, Is.True, "missing icon falls back to the neutral glyph");
            Assert.That(Row(2).Icon, Is.Null);
        }

        [UnityTest]
        public IEnumerator RowsHandsAndDragCardShowTheDefinitionsRealIcon()
        {
            yield return new EnterPlayMode(false);
            yield return StartWorld();
            yield return CreateUi();

            ItemDefinition banana = ShopTestData.LoadItem(ShopTestData.BananaItem);
            ItemDefinition mug = ShopTestData.LoadItem(ShopTestData.MugItem);
            ItemDefinition package = ShopTestData.LoadItem(ShopTestData.DeliveryPackageItem);

            Store(SceneItem("banana-a", banana));
            Assert.That(_world.Carry.TryCarry(SceneItem("mug-b", mug)), Is.True);
            _ui.Open();

            Assert.That(banana.InventoryIcon, Is.Not.Null);
            Assert.That(Row(0).Icon, Is.SameAs(banana.InventoryIcon), "the row shows the definition's icon");
            Assert.That(Field<GameObject>(Row(0), "iconFallback").activeSelf, Is.False);
            Assert.That(HeldCard().Icon, Is.SameAs(mug.InventoryIcon), "In Hands uses the same icon source");
            Assert.That(Field<GameObject>(HeldCard(), "iconFallback").activeSelf, Is.False);

            InventoryDragGhost ghost = Field<InventoryDragGhost>(_ui, "dragGhost");
            Image ghostIcon = Field<Image>(ghost, "icon");
            TMP_Text ghostTitle = Field<TMP_Text>(ghost, "title");

            PointerEventData pointer = BeginDrag(Row(0));
            MoveTo(Row(0), pointer, OutsidePanel());
            Canvas.ForceUpdateCanvases();

            Assert.That(ghostIcon.enabled && ghostIcon.sprite == banana.InventoryIcon, Is.True, "the drag card shows the real item icon");
            Assert.That(Field<GameObject>(ghost, "iconFallback").activeSelf, Is.False);
            Assert.That(ghostTitle.text, Is.EqualTo(Text("shop.product.banana.name")));
            Assert.That(GhostHint(), Is.EqualTo(Text("inventory.drop.swap")));

            float ghostIconSize = ghostIcon.rectTransform.rect.height;
            Assert.That(ghostIconSize, Is.GreaterThanOrEqualTo(Field<Image>(Row(0), "icon").rectTransform.rect.height * 2f), "the item image dominates the card");
            Assert.That(ghostIconSize, Is.GreaterThan(ghostTitle.rectTransform.rect.height * 2.5f), "not a text row: the name is a caption under the item");
            Assert.That(Vector2.Distance(ScreenPoint(ghostIcon.rectTransform), pointer.position), Is.LessThan(2f), "the pointer holds the item itself");

            Row(0).OnEndDrag(pointer);
            Assert.That(_world.Carry.CarriedItem.Instance.InstanceId, Is.EqualTo("banana-a"));
            Assert.That(HeldCard().Icon, Is.SameAs(banana.InventoryIcon));
            Assert.That(Row(0).Icon, Is.SameAs(mug.InventoryIcon));

            Assert.That(_world.Carry.Drop(), Is.True);
            DeliveryPackageBehaviour box = DeliverPackage(SaveTestWorld.BudgetGpuId);
            Assert.That(_world.Carry.TryCarry(box.Item), Is.True);
            Assert.That(package.InventoryIcon, Is.Not.Null);
            Assert.That(HeldCard().Icon, Is.SameAs(package.InventoryIcon), "a carry-only item still has its icon in the hands");
        }

        [UnityTest]
        public IEnumerator HandsSectionTakesInventoryItemsAndIsNotAStoreTarget()
        {
            yield return new EnterPlayMode(false);
            yield return StartWorld();
            yield return CreateUi();

            Store(SceneItem("banana-a", ShopTestData.BananaItem));
            _ui.Open();

            PointerEventData pointer = BeginDrag(Row(0));
            MoveTo(Row(0), pointer, HandsPoint());
            Assert.That(GhostHint(), Is.EqualTo(Text("inventory.drop.take")));
            Assert.That(Highlighted("handsHighlight"), Is.True, "the hands light up as the destination");
            Assert.That(Highlighted("storeHighlight"), Is.False);
            Row(0).OnEndDrag(pointer);

            Assert.That(_world.Carry.CarriedItem.Instance.InstanceId, Is.EqualTo("banana-a"));
            Assert.That(Highlighted("handsHighlight"), Is.False);
            string before = Describe();

            pointer = BeginDrag(HeldCard());
            MoveTo(HeldCard(), pointer, HandsPoint());
            Assert.That(GhostHint(), Is.EqualTo(Text("inventory.drop.on_panel")));
            Assert.That(Highlighted("handsHighlight") || Highlighted("storeHighlight"), Is.False);
            HeldCard().OnEndDrag(pointer);
            Assert.That(Describe(), Is.EqualTo(before), "releasing the held card on its own section does nothing");

            pointer = BeginDrag(HeldCard());
            MoveTo(HeldCard(), pointer, StoredItemsPoint());
            Assert.That(Highlighted("storeHighlight"), Is.True, "the list lights up as the destination");
            HeldCard().OnEndDrag(pointer);
            Assert.That(InventoryIds(), Is.EqualTo(new[] { "banana-a" }));
            Assert.That(Highlighted("storeHighlight"), Is.False);
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

            InventoryItemView slot = Row(0);
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
            MoveTo(HeldCard(), pointer, StoredItemsPoint());
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

            Assert.That(inventoryListeners, Is.EqualTo(2), "list and overlay");
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
            Assert.That(Row(0).InstanceId, Is.EqualTo("mug-a"));
        }

        // Call right after a top-level "yield return new EnterPlayMode(false)" (nested enumerators cannot enter Play Mode).
        private IEnumerator StartWorld()
        {
            _world = SaveTestWorld.Create(2500);
            _world.StartPlayModeRuntime();

            yield return PlayModeWait.Frames(1);
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
            yield return PlayModeWait.Frames(1);

            Assert.That(_ui.isActiveAndEnabled, Is.True, "the prefab UI wires up without configuration errors");
        }

        private WorldItem SceneItem(string authoredId, string definitionAsset)
        {
            return SceneItem(authoredId, ShopTestData.LoadItem(definitionAsset));
        }

        private WorldItem SceneItem(string authoredId, ItemDefinition definition)
        {
            GameObject go = new($"Test item {authoredId}");
            go.SetActive(false);
            go.transform.position = new Vector3(_created.Count * 0.3f, 0.3f, 1f);
            go.AddComponent<BoxCollider>().size = Vector3.one * 0.1f;
            go.AddComponent<Rigidbody>();

            WorldItem item = go.AddComponent<WorldItem>();
            SaveTestWorld.SetField(item, "definition", definition);
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

        // Over the panel's stored-items side (its title), away from the "In hands" section.
        private Vector2 StoredItemsPoint()
        {
            Canvas.ForceUpdateCanvases();
            Vector2 point = ScreenPoint(Field<TMP_Text>(_ui, "titleText").rectTransform);
            Assert.That(RectTransformUtility.RectangleContainsScreenPoint(Field<RectTransform>(_ui, "panel"), point, null), Is.True);
            Assert.That(RectTransformUtility.RectangleContainsScreenPoint(Field<RectTransform>(_ui, "handsZone"), point, null), Is.False);
            return point;
        }

        private Vector2 HandsPoint()
        {
            Canvas.ForceUpdateCanvases();
            return ScreenPoint(Field<RectTransform>(_ui, "handsZone"));
        }

        private Vector2 OutsidePanel()
        {
            Vector2 point = new(Screen.width * 0.08f, Screen.height * 0.5f);
            Assert.That(RectTransformUtility.RectangleContainsScreenPoint(Field<RectTransform>(_ui, "panel"), point, null), Is.False);
            return point;
        }

        private bool Highlighted(string field)
        {
            return Field<Graphic>(_ui, field).enabled;
        }

        private static Vector2 ScreenPoint(RectTransform rect)
        {
            return RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        }

        private InventoryItemView HeldCard()
        {
            return Field<InventoryItemView>(_ui, "heldItem");
        }

        private InventoryItemView Row(int index)
        {
            return Field<InventoryItemView[]>(Field<InventoryListView>(_ui, "list"), "rows")[index];
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

        // The items these cases create and move. The Student PC's own starter parts stay installed throughout.
        private static List<WorldItem> Items()
        {
            return Object.FindObjectsByType<WorldItem>(FindObjectsInactive.Include)
                .Where(item => item.Instance != null && item.GetComponentInParent<PcAssemblyBehaviour>(true) == null)
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
