using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GoLive.Delivery;
using GoLive.Desktop;
using GoLive.Economy;
using GoLive.GameTime;
using GoLive.Inventory;
using GoLive.Items;
using GoLive.Needs;
using GoLive.PcBuilding;
using GoLive.Persistence;
using GoLive.Phone;
using GoLive.Player;
using GoLive.Shop;
using GoLive.Sleep;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace GoLive.Tests
{
    // Explicit EditMode composition of the current save runtime, not a substitute for Awake/Start or Play Mode verification.
    // Hands, Delivery and the PC stay inactive unless a Play Mode test calls StartPlayModeRuntime().
    internal sealed class SaveTestWorld : IDisposable
    {
        public const string BudgetGpuId = "budget-gpu";
        public const string SnackId = "test-snack";
        public const string StudentPcPrefab = "Assets/Game/Prefab/PC/StudentPC.prefab";

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
        public PcAssemblyBehaviour Pc { get; }
        public DesktopRuntimeBehaviour Desktop { get; }
        public PcSessionBehaviour PcSession { get; }
        public PlayerController Player { get; }
        public PcPeripheralsBehaviour Peripherals { get; }

        // The persistent scene IDs of the parts the Student PC comes with (never runtime items).
        public string[] StarterItemIds { get; }
        public string Directory { get; }
        public string SavePath { get; }

        private readonly ShopCatalogConfig _catalog;
        private readonly GameObject _deliveryRoot;
        private readonly GameObject _pcRoot;
        private readonly GameObject _peripheralRoot;
        private readonly List<WorldItem> _peripheralItems = new();
        private readonly List<GameObject> _peripheralItemRoots = new();
        private readonly GameObject _floor;
        private readonly WorldItem[] _starterItems;
        private readonly DesktopAppCatalog _desktopCatalog;
        private readonly InputActionAsset _desktopInputs;
        private readonly InputAction _monitorInput;
        private readonly InputAction _focusInput;
        private readonly InputActionReference _monitorReference;
        private readonly InputActionReference _focusReference;

        private SaveTestWorld(long walletCents, ShopCatalogConfig catalog)
        {
            Directory = Path.Combine(Path.GetTempPath(), "GoLiveSaveTests", Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(Directory);
            SavePath = Path.Combine(Directory, "save.json");

            Root = new GameObject("Save test runtime");
            Root.SetActive(false);

            Transform pivot = new GameObject("Test look pivot").transform;
            pivot.SetParent(Root.transform, false);
            pivot.localPosition = new Vector3(0f, 1.6f, 0f);

            PlayerController player = Root.AddComponent<PlayerController>();
            Player = player;
            CharacterController body = Root.GetComponent<CharacterController>();
            body.height = 1.9f;
            body.radius = 0.3f;
            body.center = new Vector3(0f, 0.95f, 0f);

            SetField(player, "lookPivot", pivot);
            SetField(player, "_characterController", body);
            SetField(player, "_lookPivotBaseRotation", Quaternion.identity);
            typeof(PlayerController).GetMethod("CaptureStandingShape", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(player, null);

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
            if (!Application.isPlaying)
            {
                InvokeLifecycle(Carry, "Awake");
                InvokeLifecycle(Inventory, "Awake");
            }

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
            SetField(Shop, "_checkout", new ShopCheckout(Wallet.Wallet, Shop.Orders, Clock.Clock));

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

            // The real Student PC prefab (case, slots, assembly record); the GL scene adds the Workbench on top of it. A plain
            // copy, like the PC in a running game: an Editor prefab instance would refuse to let its starter parts leave it.
            _pcRoot = new GameObject("Test PC holder");
            _pcRoot.SetActive(false);
            GameObject pc = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(StudentPcPrefab), _pcRoot.transform);
            pc.name = "StudentPC";
            pc.transform.position = new Vector3(3f, 0.8f, 0f);
            Pc = pc.GetComponent<PcAssemblyBehaviour>();
            typeof(PcAssemblyBehaviour).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Pc, null);
            _starterItems = pc.GetComponentsInChildren<WorldItem>(true);
            StarterItemIds = _starterItems.Select(item => item.AuthoredInstanceId).OrderBy(id => id, StringComparer.Ordinal).ToArray();

            // EditMode runs no Awake/Start, so compose the PC's new game the way Play Mode does: its scene items first,
            // then the PC installs them. Play Mode gets the real lifecycle from StartPlayModeRuntime instead.
            if (!Application.isPlaying)
            {
                foreach (WorldItem item in pc.GetComponentsInChildren<WorldItem>(true))
                    typeof(WorldItem).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(item, null);

                typeof(PcAssemblyBehaviour).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Pc, null);
                Assert.That(Pc.IsReady, Is.True, "the Student PC's new-game hardware is installed");
            }

            // External devices are opt-in. The general fixture still contains exactly the original five PC parts.
            _peripheralRoot = new GameObject("Test peripherals");
            _peripheralRoot.SetActive(false);
            Peripherals = _peripheralRoot.AddComponent<PcPeripheralsBehaviour>();
            PcPeripheralSocket microphone = CreatePeripheralSocket(PcPeripheralKind.Microphone);
            PcPeripheralSocket webcam = CreatePeripheralSocket(PcPeripheralKind.Webcam);
            SetField(Peripherals, "microphoneSocket", microphone);
            SetField(Peripherals, "webcamSocket", webcam);
            SetField(Peripherals, "playerCarry", Carry);
            SetField(Peripherals, "playerController", Player);
            InvokeLifecycle(Peripherals, "Awake");
            if (!Application.isPlaying) InvokeLifecycle(Peripherals, "Start");

            _desktopCatalog = ScriptableObject.CreateInstance<DesktopAppCatalog>();
            SetField(_desktopCatalog, "apps", DesktopStorageTests.Definitions());
            PcSession = Root.AddComponent<PcSessionBehaviour>();
            var cameraObject = new GameObject("Desktop test camera");
            cameraObject.transform.SetParent(pivot, false);
            var camera = cameraObject.AddComponent<Camera>();
            var seat = new GameObject("Desktop test seat").transform;
            seat.SetParent(Root.transform, false);
            var monitorCollider = cameraObject.AddComponent<BoxCollider>();
            _desktopInputs = ScriptableObject.CreateInstance<InputActionAsset>();
            var desktopMap = new InputActionMap("Desktop test");
            _desktopInputs.AddActionMap(desktopMap);
            _monitorInput = desktopMap.AddAction("Monitor", InputActionType.Button);
            _focusInput = desktopMap.AddAction("Focus", InputActionType.Button);
            _monitorReference = InputActionReference.Create(_monitorInput);
            _focusReference = InputActionReference.Create(_focusInput);
            SetField(PcSession, "pc", Pc);
            SetField(PcSession, "playerController", player);
            SetField(PcSession, "playerCarry", Carry);
            SetField(PcSession, "playerCamera", camera);
            SetField(PcSession, "seatViewAnchor", seat);
            SetField(PcSession, "monitorCollider", monitorCollider);
            SetField(PcSession, "monitorAction", _monitorReference);
            SetField(PcSession, "focusAction", _focusReference);
            InvokeLifecycle(PcSession, "Awake");
            InvokeLifecycle(PcSession, "OnEnable");

            Desktop = Root.AddComponent<DesktopRuntimeBehaviour>();
            SetField(Desktop, "pc", Pc);
            SetField(Desktop, "session", PcSession);
            SetField(Desktop, "peripherals", Peripherals);
            SetField(Desktop, "catalog", _desktopCatalog);
            SetField(Desktop, "clock", Clock);
            SetField(Desktop, "wallet", Wallet);
            SetField(Desktop, "audienceTuning", AssetDatabase.LoadAssetAtPath<AudienceTuningConfig>(StreamRuntimeConfigTests.AudienceTuningPath));
            SetField(Desktop, "viewerCore", AssetDatabase.LoadAssetAtPath<GoLive.Viewers.ViewerCoreConfig>(StreamRuntimeConfigTests.ViewerCorePath));
            InvokeLifecycle(Desktop, "Awake");
            BindDesktopWhenPcReady();

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
            SetField(Save, "_pc", Pc);
            SetField(Save, "_desktop", Desktop);
        }

        public static SaveTestWorld Create(long walletCents)
        {
            return new SaveTestWorld(walletCents, null);
        }

        public static SaveTestWorld Create(long walletCents, ShopCatalogConfig catalog)
        {
            return new SaveTestWorld(walletCents, catalog);
        }

        // Real Awake/OnEnable/Start for the hands (carry + inventory), the delivery owner and the PC; Start runs on the next frame.
        public void StartPlayModeRuntime()
        {
            Assert.That(Application.isPlaying, Is.True, "Delivery runtime tests must run in Play Mode.");

            Hands.SetActive(true);
            _deliveryRoot.SetActive(true);
            _pcRoot.SetActive(true);
            _peripheralRoot.SetActive(true);
        }

        // Creates the real authored definition/prefab as a persistent scene item, without inferring a connection.
        public WorldItem CreatePeripheral(PcPeripheralKind kind)
        {
            string definitionName = kind switch
            {
                PcPeripheralKind.Microphone => "Item_UsedMicrophone",
                PcPeripheralKind.Webcam => "Item_UsedWebcam",
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
            ItemDefinition definition = ShopTestData.LoadItem(definitionName);
            Assert.That(definition, Is.Not.Null, definitionName);
            Assert.That(definition.PeripheralKind, Is.EqualTo(kind));
            Assert.That(definition.TryGetRuntimePrefab(out WorldItem prefab), Is.True);
            GameObject holder = new("Test peripheral item holder");
            holder.SetActive(false);
            _peripheralItemRoots.Add(holder);
            WorldItem item = Object.Instantiate(prefab, holder.transform);
            SetField(item, "authoredInstanceId", "test-peripheral-" + kind.ToString().ToLowerInvariant() + "-" + _peripheralItems.Count);
            _peripheralItems.Add(item);
            if (!Application.isPlaying) InvokeLifecycle(item, "Awake");
            holder.SetActive(true);
            return item;
        }

        public WorldItem ConnectMicrophone() => ConnectPeripheral(PcPeripheralKind.Microphone);

        public WorldItem ConnectPeripheral(PcPeripheralKind kind)
        {
            // In EditMode the fixture explicitly composes lifecycle; PlayMode callers start and await the runtime.
            if (!Application.isPlaying)
            {
                Hands.SetActive(true);
                _peripheralRoot.SetActive(true);
            }
            Assert.That(Peripherals.IsReady, Is.True);
            WorldItem item = CreatePeripheral(kind);
            Assert.That(Carry.TryCarry(item), Is.True);
            Assert.That(Peripherals.TryConnectCarried(kind, Carry), Is.True);
            return item;
        }

        private PcPeripheralSocket CreatePeripheralSocket(PcPeripheralKind kind)
        {
            var socketObject = new GameObject(kind + " socket");
            socketObject.transform.SetParent(_peripheralRoot.transform, false);
            var anchor = new GameObject("Install anchor").transform;
            anchor.SetParent(socketObject.transform, false);
            var socket = socketObject.AddComponent<PcPeripheralSocket>();
            SetField(socket, "kind", kind);
            SetField(socket, "installAnchor", anchor);
            SetField(socket, "peripherals", Peripherals);
            SetField(socket, "interactionCollider", socketObject.AddComponent<BoxCollider>());
            return socket;
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
            if (Desktop != null) InvokeLifecycle(Desktop, "OnDisable");
            if (PcSession != null)
            {
                InvokeLifecycle(PcSession, "OnDisable");
                InvokeLifecycle(PcSession, "OnDestroy");
            }
            foreach (WorldItem item in Object.FindObjectsByType<WorldItem>(FindObjectsInactive.Include))
            {
                if (item.IsRuntime)
                    Object.DestroyImmediate(item.gameObject);
            }

            // RestoreAsWorld can detach a starter part from the PC root. The fixture still owns that exact
            // scene item and must remove it before another fixture creates the same persistent identity.
            foreach (WorldItem item in _starterItems)
                if (item != null)
                    Object.DestroyImmediate(item.gameObject);
            foreach (WorldItem item in _peripheralItems)
                if (item != null)
                    Object.DestroyImmediate(item.gameObject);
            foreach (GameObject holder in _peripheralItemRoots) DestroyIfAlive(holder);

            DestroyIfAlive(Root);
            DestroyIfAlive(Hands);
            DestroyIfAlive(_deliveryRoot);
            DestroyIfAlive(_pcRoot);
            DestroyIfAlive(_peripheralRoot);
            DestroyIfAlive(_floor);

            if (DropPoint != null)
                Object.DestroyImmediate(DropPoint.gameObject);

            if (_catalog != null)
                Object.DestroyImmediate(_catalog);
            if (_desktopCatalog != null)
                Object.DestroyImmediate(_desktopCatalog);
            Object.DestroyImmediate(_monitorReference);
            Object.DestroyImmediate(_focusReference);
            _monitorInput.Dispose();
            _focusInput.Dispose();
            Object.DestroyImmediate(_desktopInputs);

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
            BindDesktopWhenPcReady();
            return (bool)typeof(GameSaveController)
                .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(Save, new object[] { SavePath });
        }

        private void BindDesktopWhenPcReady()
        {
            if (Pc.IsReady && Peripherals.IsReady && !Desktop.IsReady)
                InvokeLifecycle(Desktop, "Bind");
        }

        private static void InvokeLifecycle(object target, string method)
            => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

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
