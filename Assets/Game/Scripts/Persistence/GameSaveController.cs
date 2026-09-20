using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using GoLive.Economy;
using GoLive.GameTime;
using GoLive.Inventory;
using GoLive.Items;
using GoLive.Needs;
using GoLive.Player;
using GoLive.Sleep;
using UnityEngine;

namespace GoLive.Persistence
{
    [DisallowMultipleComponent]
    public sealed class GameSaveController : MonoBehaviour
    {
        private const int CurrentVersion = 1;
        private const string AutosaveFileName = "autosave.json";

        [Header("Game State")]
        [SerializeField] private PlayerController _player;
        [SerializeField] private PlayerCarry _carry;
        [SerializeField] private PlayerInventory _inventory;
        [SerializeField] private GameClockBehaviour _gameClock;
        [SerializeField] private PlayerNeedsBehaviour _needs;
        [SerializeField] private WalletBehaviour _wallet;
        [SerializeField] private RentBehaviour _rent;
        [SerializeField] private PlayerSleepController _sleep;

        private bool _bound;

        private string SaveDirectory => Path.Combine(Application.persistentDataPath, "Saves");
        private string AutosavePath => Path.Combine(SaveDirectory, AutosaveFileName);

        private void Awake()
        {
            if (!ValidateConfiguration())
                enabled = false;
        }

        private void OnEnable()
        {
            Bind();
        }

        private void OnDisable()
        {
            Unbind();
        }

        public bool SaveAutosave()
        {
            return TrySave(AutosavePath);
        }

        public bool CreateCheckpoint()
        {
            DateTime now = DateTime.UtcNow;
            string timestamp = now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
            string fileName = $"checkpoint_{timestamp}_{now.Ticks}.json";

            return TrySave(Path.Combine(SaveDirectory, fileName));
        }

        public bool LoadAutosave()
        {
            return TryLoad(AutosavePath);
        }

        public bool LoadLatestCheckpoint()
        {
            if (!EnsureRuntimeStateReady())
                return false;

            if (!Directory.Exists(SaveDirectory))
            {
                Debug.LogWarning("No checkpoint save directory exists yet.");
                return false;
            }

            string[] files = Directory.GetFiles(SaveDirectory, "checkpoint_*.json");

            if (files.Length == 0)
            {
                Debug.LogWarning("No checkpoints exist yet.");
                return false;
            }

            string latestPath = files[0];
            DateTime latestWriteTime = File.GetLastWriteTimeUtc(latestPath);

            for (int i = 1; i < files.Length; i++)
            {
                DateTime writeTime = File.GetLastWriteTimeUtc(files[i]);

                if (writeTime <= latestWriteTime)
                    continue;

                latestPath = files[i];
                latestWriteTime = writeTime;
            }

            return TryLoad(latestPath);
        }

        [ContextMenu("Create Checkpoint")]
        private void CreateCheckpointFromInspector()
        {
            if (Application.isPlaying)
                CreateCheckpoint();
        }

        [ContextMenu("Save Autosave")]
        private void SaveAutosaveFromInspector()
        {
            if (Application.isPlaying)
                SaveAutosave();
        }

        [ContextMenu("Load Latest Checkpoint")]
        private void LoadLatestCheckpointFromInspector()
        {
            if (Application.isPlaying)
                LoadLatestCheckpoint();
        }

        [ContextMenu("Load Autosave")]
        private void LoadAutosaveFromInspector()
        {
            if (Application.isPlaying)
                LoadAutosave();
        }

        private void Bind()
        {
            if (_bound || _sleep == null)
                return;

            _sleep.SleepCompleted += HandleSleepCompleted;
            _bound = true;
        }

        private void Unbind()
        {
            if (!_bound || _sleep == null)
                return;

            _sleep.SleepCompleted -= HandleSleepCompleted;
            _bound = false;
        }

        private void HandleSleepCompleted(SleepResult result)
        {
            SaveAutosave();
        }

        private bool TrySave(string path)
        {
            if (!EnsureRuntimeStateReady())
                return false;

            try
            {
                GameSaveData data = Capture();
                string json = JsonUtility.ToJson(data, true);

                WriteAtomic(path, json);

                Debug.Log($"GO! LIVE save created: {path}");
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return false;
            }
        }

        private bool TryLoad(string path)
        {
            if (!EnsureRuntimeStateReady())
                return false;

            if (!File.Exists(path))
            {
                Debug.LogWarning($"Save file does not exist: {path}");
                return false;
            }

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                GameSaveData data = JsonUtility.FromJson<GameSaveData>(json);

                if (!ValidateSaveData(data, out Dictionary<string, WorldItem> sceneItems))
                {
                    Debug.LogError($"Save validation failed: {path}");
                    return false;
                }

                Apply(data, sceneItems);

                Debug.Log($"GO! LIVE save loaded: {path}");
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return false;
            }
        }

        private GameSaveData Capture()
        {
            PlayerPoseSnapshot playerPose = _player.CapturePose();
            PlayerNeedsSnapshot needs = _needs.Needs.Current;

            if (!_rent.TryGetSnapshot(out RentSnapshot rent))
                throw new InvalidOperationException("Rent state is not initialized.");

            IReadOnlyList<ItemInstance> inventoryItems = _inventory.Inventory.Items;
            Dictionary<string, int> inventoryIndices = new(StringComparer.Ordinal);

            for (int i = 0; i < inventoryItems.Count; i++)
                inventoryIndices.Add(inventoryItems[i].InstanceId, i);

            WorldItem[] worldItems = UnityEngine.Object.FindObjectsByType<WorldItem>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            ItemSaveData[] itemData = new ItemSaveData[worldItems.Length];
            HashSet<string> instanceIds = new(StringComparer.Ordinal);

            for (int i = 0; i < worldItems.Length; i++)
            {
                WorldItem item = worldItems[i];

                if (item.Instance == null)
                    throw new InvalidOperationException($"WorldItem '{item.name}' has no runtime ItemInstance.");

                if (!instanceIds.Add(item.Instance.InstanceId))
                    throw new InvalidOperationException($"Duplicate ItemInstance ID detected: {item.Instance.InstanceId}");

                int inventoryIndex = inventoryIndices.TryGetValue(item.Instance.InstanceId, out int index) ? index : -1;

                itemData[i] = new ItemSaveData
                {
                    InstanceId = item.Instance.InstanceId,
                    DefinitionId = item.Instance.DefinitionId,
                    Location = item.Instance.Location,
                    InventoryIndex = inventoryIndex,
                    Position = item.transform.position,
                    Rotation = item.transform.rotation
                };
            }

            Array.Sort(itemData, (left, right) => string.CompareOrdinal(left.InstanceId, right.InstanceId));

            return new GameSaveData
            {
                Version = CurrentVersion,
                SavedAtUtcTicks = DateTime.UtcNow.Ticks,

                Player = new PlayerSaveData
                {
                    Position = playerPose.Position,
                    Rotation = playerPose.Rotation,
                    Pitch = playerPose.Pitch
                },

                GameTimeSeconds = _gameClock.Clock.Current.TotalSeconds,
                Hunger = needs.Hunger,
                Concentration = needs.Concentration,
                BalanceCents = _wallet.Wallet.BalanceCents,

                Rent = new RentSaveData
                {
                    AmountDueCents = rent.AmountDueCents,
                    FirstPaymentSettled = rent.FirstPaymentSettled,
                    FirstDeadlineMissed = rent.FirstDeadlineMissed,
                    SecondBillIssued = rent.SecondBillIssued,
                    Outcome = rent.Outcome,
                    ProcessedThroughSeconds = rent.ProcessedThroughSeconds
                },

                Items = itemData
            };
        }

        private void Apply(GameSaveData data, Dictionary<string, WorldItem> sceneItems)
        {
            Dictionary<int, WorldItem> inventoryByIndex = new();
            HashSet<string> savedIds = new(StringComparer.Ordinal);
            WorldItem carriedItem = null;

            for (int i = 0; i < data.Items.Length; i++)
            {
                ItemSaveData savedItem = data.Items[i];
                savedIds.Add(savedItem.InstanceId);

                WorldItem worldItem = sceneItems[savedItem.InstanceId];
                ItemInstance instance = new(savedItem.InstanceId, savedItem.DefinitionId, savedItem.Location);

                bool restored = savedItem.Location switch
                {
                    ItemLocation.World => worldItem.RestoreAsWorld(instance, savedItem.Position, savedItem.Rotation),
                    ItemLocation.Inventory => worldItem.RestoreAsInventory(instance, _inventory.StoredItemsRoot),
                    ItemLocation.Carried => worldItem.RestoreAsCarried(instance, _carry.CarryAnchor),
                    ItemLocation.Removed => worldItem.RestoreAsRemoved(instance),
                    _ => false
                };

                if (!restored)
                    throw new InvalidOperationException($"Failed to restore item {savedItem.InstanceId}.");

                if (savedItem.Location == ItemLocation.Inventory)
                    inventoryByIndex.Add(savedItem.InventoryIndex, worldItem);

                if (savedItem.Location == ItemLocation.Carried)
                    carriedItem = worldItem;
            }

            foreach (KeyValuePair<string, WorldItem> pair in sceneItems)
            {
                if (savedIds.Contains(pair.Key))
                    continue;

                WorldItem item = pair.Value;

                if (item.Instance == null)
                    continue;

                ItemInstance removed = new(
                    item.Instance.InstanceId,
                    item.Instance.DefinitionId,
                    ItemLocation.Removed);

                item.RestoreAsRemoved(removed);
            }

            List<WorldItem> orderedInventory = new(inventoryByIndex.Count);

            for (int i = 0; i < inventoryByIndex.Count; i++)
                orderedInventory.Add(inventoryByIndex[i]);

            if (!_inventory.RestoreStoredItems(orderedInventory))
                throw new InvalidOperationException("Failed to restore Inventory state.");

            if (!_carry.RestoreCarriedItem(carriedItem))
                throw new InvalidOperationException("Failed to restore Carry state.");

            _player.RestorePose(new PlayerPoseSnapshot(
                data.Player.Position,
                data.Player.Rotation,
                data.Player.Pitch));

            _gameClock.Clock.Restore(new GameTimeSnapshot(data.GameTimeSeconds));

            PlayerNeedsSnapshot currentNeeds = _needs.Needs.Current;

            _needs.Restore(new PlayerNeedsSnapshot(
                data.Hunger,
                currentNeeds.MaxHunger,
                data.Concentration,
                currentNeeds.MaxConcentration));

            _wallet.Wallet.Restore(data.BalanceCents);

            _rent.Restore(new RentSnapshot(
                data.Rent.AmountDueCents,
                data.Rent.FirstPaymentSettled,
                data.Rent.FirstDeadlineMissed,
                data.Rent.SecondBillIssued,
                data.Rent.Outcome,
                data.Rent.ProcessedThroughSeconds));
        }

        private bool ValidateSaveData(GameSaveData data, out Dictionary<string, WorldItem> sceneItems)
        {
            sceneItems = BuildSceneItemMap();

            if (data == null ||
                data.Version != CurrentVersion ||
                data.Player == null ||
                data.Rent == null ||
                data.Items == null)
            {
                return false;
            }

            if (data.GameTimeSeconds < 0 ||
                data.BalanceCents < 0 ||
                data.Rent.AmountDueCents < 0 ||
                data.Rent.ProcessedThroughSeconds < 0)
            {
                return false;
            }

            if (data.Rent.ProcessedThroughSeconds > data.GameTimeSeconds)
                return false;

            PlayerNeedsSnapshot currentNeeds = _needs.Needs.Current;

            if (!IsFiniteInRange(data.Hunger, 0f, currentNeeds.MaxHunger) ||
                !IsFiniteInRange(data.Concentration, 0f, currentNeeds.MaxConcentration))
            {
                return false;
            }

            HashSet<string> savedIds = new(StringComparer.Ordinal);
            HashSet<int> inventoryIndices = new();
            int inventoryCount = 0;
            int carriedCount = 0;

            for (int i = 0; i < data.Items.Length; i++)
            {
                ItemSaveData item = data.Items[i];

                if (item == null ||
                    string.IsNullOrWhiteSpace(item.InstanceId) ||
                    string.IsNullOrWhiteSpace(item.DefinitionId) ||
                    !savedIds.Add(item.InstanceId) ||
                    !Enum.IsDefined(typeof(ItemLocation), item.Location))
                {
                    return false;
                }

                if (!sceneItems.TryGetValue(item.InstanceId, out WorldItem worldItem))
                    return false;

                if (!string.Equals(worldItem.Definition.ItemId, item.DefinitionId, StringComparison.Ordinal))
                    return false;

                if (item.Location == ItemLocation.Inventory)
                {
                    if (item.InventoryIndex < 0 || !inventoryIndices.Add(item.InventoryIndex))
                        return false;

                    inventoryCount++;
                }
                else if (item.InventoryIndex != -1)
                {
                    return false;
                }

                if (item.Location == ItemLocation.Carried)
                    carriedCount++;
            }

            if (inventoryCount > _inventory.Inventory.Capacity || carriedCount > 1)
                return false;

            for (int i = 0; i < inventoryCount; i++)
            {
                if (!inventoryIndices.Contains(i))
                    return false;
            }

            return true;
        }

        private Dictionary<string, WorldItem> BuildSceneItemMap()
        {
            WorldItem[] items = UnityEngine.Object.FindObjectsByType<WorldItem>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            Dictionary<string, WorldItem> result = new(StringComparer.Ordinal);

            for (int i = 0; i < items.Length; i++)
            {
                WorldItem item = items[i];

                if (item.Instance == null)
                    throw new InvalidOperationException($"WorldItem '{item.name}' has no runtime ItemInstance.");

                if (!result.TryAdd(item.Instance.InstanceId, item))
                    throw new InvalidOperationException($"Duplicate ItemInstance ID detected: {item.Instance.InstanceId}");
            }

            return result;
        }

        private bool EnsureRuntimeStateReady()
        {
            if (_inventory.Inventory != null &&
                _gameClock.Clock != null &&
                _needs.Needs != null &&
                _wallet.Wallet != null &&
                _rent.TryGetSnapshot(out _))
            {
                return true;
            }

            Debug.LogError($"{nameof(GameSaveController)} cannot save or load because game state is not initialized.", this);
            return false;
        }

        private bool ValidateConfiguration()
        {
            if (_player != null &&
                _carry != null &&
                _inventory != null &&
                _gameClock != null &&
                _needs != null &&
                _wallet != null &&
                _rent != null &&
                _sleep != null)
            {
                return true;
            }

            Debug.LogError($"{nameof(GameSaveController)} on {name} has incomplete configuration.", this);
            return false;
        }

        private static bool IsFiniteInRange(float value, float minimum, float maximum)
        {
            return !float.IsNaN(value) &&
                   !float.IsInfinity(value) &&
                   value >= minimum &&
                   value <= maximum;
        }

        private static void WriteAtomic(string path, string content)
        {
            string directory = Path.GetDirectoryName(path);

            if (string.IsNullOrWhiteSpace(directory))
                throw new DirectoryNotFoundException("Save directory could not be resolved.");

            Directory.CreateDirectory(directory);

            string temporaryPath = path + ".tmp";
            string backupPath = path + ".bak";

            File.WriteAllText(temporaryPath, content, new UTF8Encoding(false));

            if (!File.Exists(path))
            {
                File.Move(temporaryPath, path);
                return;
            }

            if (File.Exists(backupPath))
                File.Delete(backupPath);

            File.Replace(temporaryPath, path, backupPath);

            if (File.Exists(backupPath))
                File.Delete(backupPath);
        }
    }
}