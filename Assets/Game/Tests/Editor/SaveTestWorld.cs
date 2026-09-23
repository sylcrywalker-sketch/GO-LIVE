using System;
using System.IO;
using System.Reflection;
using GoLive.Delivery;
using GoLive.Economy;
using GoLive.GameTime;
using GoLive.Inventory;
using GoLive.Items;
using GoLive.Needs;
using GoLive.Persistence;
using GoLive.Phone;
using GoLive.Player;
using GoLive.Shop;
using GoLive.Sleep;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace GoLive.Tests
{
    // Explicit EditMode composition of the current (v4) save runtime, not a substitute for Awake/Start or Play Mode verification.
    // Hands and Delivery stay inactive unless a Play Mode test calls StartPlayModeRuntime().
    internal sealed class SaveTestWorld : IDisposable
    {
        public const string BudgetGpuId = "budget-gpu";
        public const string SnackId = "test-snack";

        public GameObject Root { get; }
        public GameObject Hands { get; }
        public GameSaveController Save { get; }
        public PhoneMessagesBehaviour Messages { get; }
        public GameClockBehaviour Clock { get; }
        public WalletBehaviour Wallet { get; }
        public ShopBehaviour Shop { get; }
        public PlayerCarry Carry { get; }
        public PlayerInventory Inventory { get; }
        public PlayerNeedsBehaviour Needs { get; }
        public DeliveryBehaviour Delivery { get; }
        public Transform DropPoint { get; }
        public string Directory { get; }
        public string SavePath { get; }

        private readonly ShopCatalogConfig _catalog;
        private readonly GameObject _deliveryRoot;
        private readonly GameObject _floor;

        private SaveTestWorld(long walletCents, ShopCatalogConfig catalog)
        {
            Directory = Path.Combine(Path.GetTempPath(), "GoLiveSaveTests", Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(Directory);
            SavePath = Path.Combine(Directory, "save.json");

            Root = new GameObject("Save test runtime");
            Root.SetActive(false);

            Transform pivot = new GameObject("Test look pivot").transform;
            pivot.SetParent(Root.transform, false);

            PlayerController player = Root.AddComponent<PlayerController>();
            SetField(player, "lookPivot", pivot);
            SetField(player, "_characterController", Root.GetComponent<CharacterController>());
            SetField(player, "_lookPivotBaseRotation", Quaternion.identity);

            Hands = new GameObject("Test hands");
            Hands.SetActive(false);

            Transform carryAnchor = new GameObject("Test carry anchor").transform;
            carryAnchor.SetParent(Hands.transform, false);
            carryAnchor.localPosition = new Vector3(0f, 1.4f, 0.7f);

            Transform storage = new GameObject("Test inventory storage").transform;
            storage.SetParent(Hands.transform, false);

            Carry = Hands.AddComponent<PlayerCarry>();
            Inventory = Hands.AddComponent<PlayerInventory>();
            SetField(Carry, "carryAnchor", carryAnchor);
            SetField(Inventory, "storedItemsRoot", storage);
            SetProperty(Inventory, "Inventory", new GoLive.Inventory.Inventory(12));

            Clock = Root.AddComponent<GameClockBehaviour>();
            SetProperty(Clock, "Clock", new GameClock(1, 7, 12));

            Needs = Root.AddComponent<PlayerNeedsBehaviour>();
            SetProperty(Needs, "Needs", new PlayerNeeds(new PlayerNeedsRules(100, 20, 4, 100, 100)));

            Wallet = Root.AddComponent<WalletBehaviour>();
            SetProperty(Wallet, "Wallet", new Wallet(walletCents));

            RentBehaviour rent = Root.AddComponent<RentBehaviour>();
            SetField(rent, "_rent", new RentAccount(new RentRules(5000, 5000, 2000, 3, 6, 6), Clock.Clock.Current));

            PlayerSleepController sleep = Root.AddComponent<PlayerSleepController>();
            Messages = Root.AddComponent<PhoneMessagesBehaviour>();

            _catalog = catalog != null ? catalog : CreateDefaultCatalog();

            Shop = Root.AddComponent<ShopBehaviour>();
            SetField(Shop, "wallet", Wallet);
            SetField(Shop, "gameClock", Clock);
            SetField(Shop, "catalog", _catalog);
            SetField(Shop, "_purchase", new ShopPurchase(Wallet.Wallet, Shop.Orders, Clock.Clock));

            _floor = new GameObject("Test floor");
            BoxCollider floor = _floor.AddComponent<BoxCollider>();
            floor.center = new Vector3(0f, -0.5f, 0f);
            floor.size = new Vector3(40f, 1f, 40f);

            _deliveryRoot = new GameObject("Test delivery");
            _deliveryRoot.SetActive(false);

            DropPoint = new GameObject("Test drop point").transform;
            DropPoint.position = new Vector3(0f, 0.01f, 2f);

            Delivery = _deliveryRoot.AddComponent<DeliveryBehaviour>();
            SetField(Delivery, "shop", Shop);
            SetField(Delivery, "gameClock", Clock);
            SetField(Delivery, "packageItem", ShopTestData.LoadItem(ShopTestData.DeliveryPackageItem));
            SetField(Delivery, "dropPoint", DropPoint);

            Save = Root.AddComponent<GameSaveController>();
            SetField(Save, "_player", player);
            SetField(Save, "_carry", Carry);
            SetField(Save, "_inventory", Inventory);
            SetField(Save, "_gameClock", Clock);
            SetField(Save, "_needs", Needs);
            SetField(Save, "_wallet", Wallet);
            SetField(Save, "_rent", rent);
            SetField(Save, "_sleep", sleep);
            SetField(Save, "_phoneMessages", Messages);
            SetField(Save, "_shop", Shop);
            SetField(Save, "_delivery", Delivery);
        }

        public static SaveTestWorld Create(long walletCents)
        {
            return new SaveTestWorld(walletCents, null);
        }

        public static SaveTestWorld Create(long walletCents, ShopCatalogConfig catalog)
        {
            return new SaveTestWorld(walletCents, catalog);
        }

        // Real Awake/OnEnable/Start for the hands (carry + inventory) and the delivery owner; Start runs on the next frame.
        public void StartPlayModeRuntime()
        {
            Assert.That(Application.isPlaying, Is.True, "Delivery runtime tests must run in Play Mode.");

            Hands.SetActive(true);
            _deliveryRoot.SetActive(true);
        }

        public static SceneSetup[] IsolateScene()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);

                if (scene.isDirty && scene.rootCount > 0)
                    Assert.Ignore("Save your open scene before running the isolated Save/Load fixture; unsaved scene work will not be closed.");
            }

            SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            return previous;
        }

        public static void RestoreScene(SceneSetup[] previous)
        {
            if (previous != null && previous.Length > 0)
                EditorSceneManager.RestoreSceneManagerSetup(previous);
        }

        public bool TrySave()
        {
            return InvokeSaveMethod("TrySave");
        }

        public bool TryLoad()
        {
            return InvokeSaveMethod("TryLoad");
        }

        public GameSaveData ReadSave()
        {
            return JsonUtility.FromJson<GameSaveData>(File.ReadAllText(SavePath));
        }

        public void Dispose()
        {
            foreach (WorldItem item in Object.FindObjectsByType<WorldItem>(FindObjectsInactive.Include))
            {
                if (item.IsRuntime)
                    Object.DestroyImmediate(item.gameObject);
            }

            DestroyIfAlive(Root);
            DestroyIfAlive(Hands);
            DestroyIfAlive(_deliveryRoot);
            DestroyIfAlive(_floor);

            if (DropPoint != null)
                Object.DestroyImmediate(DropPoint.gameObject);

            if (_catalog != null)
                Object.DestroyImmediate(_catalog);

            if (System.IO.Directory.Exists(Directory))
                System.IO.Directory.Delete(Directory, true);
        }

        private static ShopCatalogConfig CreateDefaultCatalog()
        {
            return ShopTestData.CreateCatalog(
                ShopTestData.CreateProduct(BudgetGpuId, 1500, ItemCategory.Electronics, ShopTestData.LoadItem(ShopTestData.BudgetGpuItem), maxPurchases: 1, deliveryDelayMinutes: 150),
                ShopTestData.CreateProduct(SnackId, 200, ItemCategory.Food, ShopTestData.LoadItem(ShopTestData.BananaItem), maxPurchases: 0, deliveryDelayMinutes: 90));
        }

        private static void DestroyIfAlive(GameObject target)
        {
            if (target != null)
                Object.DestroyImmediate(target);
        }

        private bool InvokeSaveMethod(string method)
        {
            return (bool)typeof(GameSaveController)
                .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(Save, new object[] { SavePath });
        }

        public static void SetField(object target, string name, object value)
        {
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }

        private static void SetProperty(object target, string name, object value)
        {
            target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public).SetValue(target, value);
        }
    }
}
