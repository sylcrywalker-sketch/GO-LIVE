using System;
using System.IO;
using System.Reflection;
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
    // Explicit EditMode composition of the current (v3) save runtime, not a substitute for Awake/Start or Play Mode verification.
    internal sealed class SaveTestWorld : IDisposable
    {
        public const string BudgetGpuId = "budget-gpu";
        public const string SnackId = "test-snack";

        public GameObject Root { get; }
        public GameSaveController Save { get; }
        public PhoneMessagesBehaviour Messages { get; }
        public GameClockBehaviour Clock { get; }
        public WalletBehaviour Wallet { get; }
        public ShopBehaviour Shop { get; }
        public string Directory { get; }
        public string SavePath { get; }

        private readonly ShopCatalogConfig _catalog;
        private readonly ItemDefinition[] _items;

        private SaveTestWorld(long walletCents)
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

            PlayerCarry carry = Root.AddComponent<PlayerCarry>();
            PlayerInventory inventory = Root.AddComponent<PlayerInventory>();
            SetField(carry, "carryAnchor", pivot);
            SetField(inventory, "storedItemsRoot", pivot);
            SetProperty(inventory, "Inventory", new GoLive.Inventory.Inventory(12));

            Clock = Root.AddComponent<GameClockBehaviour>();
            SetProperty(Clock, "Clock", new GameClock(1, 7, 12));

            PlayerNeedsBehaviour needs = Root.AddComponent<PlayerNeedsBehaviour>();
            SetProperty(needs, "Needs", new PlayerNeeds(new PlayerNeedsRules(100, 20, 4, 100, 100)));

            Wallet = Root.AddComponent<WalletBehaviour>();
            SetProperty(Wallet, "Wallet", new Wallet(walletCents));

            RentBehaviour rent = Root.AddComponent<RentBehaviour>();
            SetField(rent, "_rent", new RentAccount(new RentRules(5000, 5000, 2000, 3, 6, 6), Clock.Clock.Current));

            PlayerSleepController sleep = Root.AddComponent<PlayerSleepController>();
            Messages = Root.AddComponent<PhoneMessagesBehaviour>();

            ItemDefinition gpu = ShopTestData.CreateItem(BudgetGpuId, ItemCategory.Electronics);
            ItemDefinition snack = ShopTestData.CreateItem(SnackId, ItemCategory.Food);
            _items = new[] { gpu, snack };

            _catalog = ShopTestData.CreateCatalog(
                ShopTestData.CreateProduct(BudgetGpuId, 1500, ItemCategory.Electronics, gpu, maxPurchases: 1, deliveryDelayMinutes: 150),
                ShopTestData.CreateProduct(SnackId, 200, ItemCategory.Food, snack, maxPurchases: 0, deliveryDelayMinutes: 90));

            Shop = Root.AddComponent<ShopBehaviour>();
            SetField(Shop, "wallet", Wallet);
            SetField(Shop, "gameClock", Clock);
            SetField(Shop, "catalog", _catalog);
            SetField(Shop, "_purchase", new ShopPurchase(Wallet.Wallet, Shop.Orders, Clock.Clock));

            Save = Root.AddComponent<GameSaveController>();
            SetField(Save, "_player", player);
            SetField(Save, "_carry", carry);
            SetField(Save, "_inventory", inventory);
            SetField(Save, "_gameClock", Clock);
            SetField(Save, "_needs", needs);
            SetField(Save, "_wallet", Wallet);
            SetField(Save, "_rent", rent);
            SetField(Save, "_sleep", sleep);
            SetField(Save, "_phoneMessages", Messages);
            SetField(Save, "_shop", Shop);
        }

        public static SaveTestWorld Create(long walletCents)
        {
            return new SaveTestWorld(walletCents);
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
            if (Root != null)
                Object.DestroyImmediate(Root);

            if (_catalog != null)
                Object.DestroyImmediate(_catalog);

            foreach (ItemDefinition item in _items)
            {
                if (item != null)
                    Object.DestroyImmediate(item);
            }

            if (System.IO.Directory.Exists(Directory))
                System.IO.Directory.Delete(Directory, true);
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
