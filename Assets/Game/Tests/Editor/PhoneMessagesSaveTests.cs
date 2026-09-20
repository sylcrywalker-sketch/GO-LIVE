using System;
using System.IO;
using System.Linq;
using System.Reflection;
using GoLive.Economy;
using GoLive.GameTime;
using GoLive.Inventory;
using GoLive.Needs;
using GoLive.Persistence;
using GoLive.Phone;
using GoLive.Player;
using GoLive.Sleep;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GoLive.Tests
{
    public sealed class PhoneMessagesSaveTests
    {
        private GameObject _root;
        private GameSaveController _save;
        private PhoneMessagesBehaviour _owner;
        private GameClockBehaviour _clock;
        private WalletBehaviour _wallet;
        private string _directory;
        private string _path;
        private SceneSetup[] _previousScenes;

        // Explicit EditMode composition, not a substitute for Awake/Start or Play Mode verification.
        [OneTimeSetUp]
        public void IsolateTestScene()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isDirty && scene.rootCount > 0)
                    Assert.Ignore("Save your open scene before running the isolated Save/Load fixture; unsaved scene work will not be closed.");
            }
            _previousScenes = EditorSceneManager.GetSceneManagerSetup();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [OneTimeTearDown]
        public void RestoreEditorSceneSetup()
        {
            if (_previousScenes != null && _previousScenes.Length > 0)
                EditorSceneManager.RestoreSceneManagerSetup(_previousScenes);
        }

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "GoLiveMessagesTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            _path = Path.Combine(_directory, "save.json");
            _root = new GameObject("Messages save test");
            _root.SetActive(false);
            var pivot = new GameObject("Test look pivot").transform;
            pivot.SetParent(_root.transform, false);
            var player = _root.AddComponent<PlayerController>();
            SetField(player, "lookPivot", pivot);
            SetField(player, "_characterController", _root.GetComponent<CharacterController>());
            SetField(player, "_lookPivotBaseRotation", Quaternion.identity);
            var carry = _root.AddComponent<PlayerCarry>();
            var inventory = _root.AddComponent<PlayerInventory>();
            SetField(carry, "carryAnchor", pivot);
            SetField(inventory, "storedItemsRoot", pivot);
            SetProperty(inventory, "Inventory", new GoLive.Inventory.Inventory(12));
            _clock = _root.AddComponent<GameClockBehaviour>();
            SetProperty(_clock, "Clock", new GameClock(1, 7, 12));
            var needs = _root.AddComponent<PlayerNeedsBehaviour>();
            SetProperty(needs, "Needs", new PlayerNeeds(new PlayerNeedsRules(100, 20, 4, 100, 100)));
            _wallet = _root.AddComponent<WalletBehaviour>();
            SetProperty(_wallet, "Wallet", new Wallet(4000));
            var rent = _root.AddComponent<RentBehaviour>();
            SetField(rent, "_rent", new RentAccount(new RentRules(5000, 5000, 2000, 3, 6, 6), _clock.Clock.Current));
            var sleep = _root.AddComponent<PlayerSleepController>();
            _owner = _root.AddComponent<PhoneMessagesBehaviour>();
            _save = _root.AddComponent<GameSaveController>();
            SetField(_save, "_player", player);
            SetField(_save, "_carry", carry);
            SetField(_save, "_inventory", inventory);
            SetField(_save, "_gameClock", _clock);
            SetField(_save, "_needs", needs);
            SetField(_save, "_wallet", _wallet);
            SetField(_save, "_rent", rent);
            SetField(_save, "_sleep", sleep);
            SetField(_save, "_phoneMessages", _owner);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                Object.DestroyImmediate(_root);
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        [Test]
        public void ControllerCaptureWritesMessagesIntoTheActualSaveFile()
        {
            AddIncoming("first");
            Assert.That(Save(), Is.True);
            var data = ReadSave();
            Assert.That(data.Messages, Is.Not.Null);
            Assert.That(data.Messages.Conversations, Has.Length.EqualTo(1));
            Assert.That(data.Messages.Conversations[0].ContactId, Is.EqualTo("landlord"));
            Assert.That(data.Messages.Conversations[0].Messages[0].MessageId, Is.EqualTo("first"));
            Assert.That(data.Messages.Conversations[0].Messages[0].IsRead, Is.False);
            Assert.That(data.Version, Is.EqualTo(2));
        }

        [Test]
        public void SaveThenLoadPreservesHistoryReadStateAndUnread()
        {
            AddIncoming("read");
            _owner.Messages.MarkConversationRead("landlord");
            AddIncoming("unread");
            string expected = SnapshotJson();
            Assert.That(Save(), Is.True);
            _owner.Messages.Restore(new PhoneMessagesSnapshot());
            Assert.That(Load(), Is.True);
            Assert.That(SnapshotJson(), Is.EqualTo(expected));
            Assert.That(_owner.Messages.TotalUnreadCount, Is.EqualTo(1));
            Assert.That(_owner.Messages.Conversations[0].Messages.Select(message => message.IsRead), Is.EqualTo(new[] { true, false }));
        }

        [Test]
        public void OutgoingDirectionTextAndGameTimestampSurviveLoad()
        {
            _clock.Clock.AdvanceMinutes(2);
            _owner.Messages.TryAddOutgoing("reply", "landlord", MessageContent.FromText("Завтра заплачу."), _clock.Clock.Current);
            long sentAt = _clock.Clock.Current.TotalSeconds;
            Assert.That(Save(), Is.True);
            _owner.Messages.Restore(new PhoneMessagesSnapshot());
            _clock.Clock.AdvanceMinutes(60);
            Assert.That(Load(), Is.True);
            var message = _owner.Messages.Conversations[0].Messages[0];
            Assert.That(message.Direction, Is.EqualTo(MessageDirection.Outgoing));
            Assert.That(message.Content.Value, Is.EqualTo("Завтра заплачу."));
            Assert.That(message.Timestamp.TotalSeconds, Is.EqualTo(sentAt));
            Assert.That(message.IsRead, Is.True);
            Assert.That(_owner.Messages.TotalUnreadCount, Is.Zero);
        }

        [Test]
        public void MultipleConversationsAndAnEmptyContactSurviveLoad()
        {
            AddIncoming("one", "landlord");
            AddIncoming("two", "friend");
            _owner.Messages.GetOrCreateConversation("empty");
            Assert.That(Save(), Is.True);
            _owner.Messages.Restore(new PhoneMessagesSnapshot());
            Assert.That(Load(), Is.True);
            Assert.That(_owner.Messages.Conversations.Select(conversation => conversation.ContactId), Is.EqualTo(new[] { "landlord", "friend", "empty" }));
            Assert.That(_owner.Messages.TotalUnreadCount, Is.EqualTo(2));
        }

        [Test]
        public void LoadedIdsRemainProtectedAcrossContactsAndDirections()
        {
            AddIncoming("identity");
            Assert.That(Save(), Is.True);
            _owner.Messages.Restore(new PhoneMessagesSnapshot());
            Assert.That(Load(), Is.True);
            Assert.That(_owner.Messages.TryAddOutgoing("identity", "other", MessageContent.FromText("duplicate"), _clock.Clock.Current), Is.False);
            Assert.That(_owner.Messages.TryAddIncoming("identity", "landlord", MessageContent.FromText("duplicate"), _clock.Clock.Current), Is.False);
            Assert.That(_owner.Messages.Conversations.Count, Is.EqualTo(1));
            Assert.That(_owner.Messages.Conversations[0].Messages.Count, Is.EqualTo(1));
        }

        [Test]
        public void RepeatedLoadKeepsTheOwnerInstanceAndDoesNotAppendOrSeed()
        {
            AddIncoming("identity");
            var instance = _owner.Messages;
            Assert.That(Save(), Is.True);
            int changes = 0;
            instance.Changed += () => changes++;
            Assert.That(Load(), Is.True);
            Assert.That(Load(), Is.True);
            Assert.That(_owner.Messages, Is.SameAs(instance));
            Assert.That(instance.Conversations[0].Messages.Count, Is.EqualTo(1));
            Assert.That(changes, Is.EqualTo(2));
        }

        [Test]
        public void EmptyMessagesSaveLoadsWithoutStartingAConversation()
        {
            Assert.That(_owner.Messages.Conversations, Is.Empty);
            Assert.That(Save(), Is.True);
            AddIncoming("discard-on-load");
            Assert.That(Load(), Is.True);
            Assert.That(_owner.Messages.Conversations, Is.Empty);
        }

        [TestCase("duplicate")]
        [TestCase("negative time")]
        [TestCase("version")]
        [TestCase("missing section")]
        [TestCase("null section")]
        [TestCase("empty section")]
        [TestCase("missing conversations")]
        public void InvalidMessagesAreRejectedBeforeAnyStateIsApplied(string corruption)
        {
            AddIncoming("saved-first");
            AddIncoming("saved-second", "friend");
            Assert.That(Save(), Is.True);
            var data = ReadSave();
            switch (corruption)
            {
                case "duplicate": data.Messages.Conversations[1].Messages[0].MessageId = "saved-first"; break;
                case "negative time": data.Messages.Conversations[1].Messages[0].GameTimeSeconds = -1; break;
                case "version": data.Messages.Version = 999; break;
            }
            string json = JsonUtility.ToJson(data);
            string messagesField = "\"Messages\":" + JsonUtility.ToJson(data.Messages);
            // JsonUtility.ToJson expands inline null classes into default objects; corrupt the JSON itself.
            if (corruption == "missing section")
                json = json.Replace(messagesField + ",", string.Empty);
            if (corruption == "null section")
                json = json.Replace(messagesField, "\"Messages\":null");
            if (corruption == "empty section")
                json = json.Replace(messagesField, "\"Messages\":{}");
            if (corruption == "missing conversations")
                json = json.Replace(messagesField, "\"Messages\":{\"Version\":1}");
            File.WriteAllText(_path, json);
            string fileBefore = File.ReadAllText(_path);
            _wallet.Wallet.Restore(12345);
            _clock.Clock.AdvanceMinutes(4);
            _root.transform.position = new Vector3(2, 3, 4);
            AddIncoming("live");
            string before = SnapshotJson();
            long clockBefore = _clock.Clock.Current.TotalSeconds;
            int changes = 0;
            _owner.Messages.Changed += () => changes++;
            LogAssert.Expect(LogType.Error, $"Save validation failed: {_path}");
            Assert.That(Load(), Is.False);
            Assert.That(SnapshotJson(), Is.EqualTo(before));
            Assert.That(changes, Is.Zero);
            Assert.That(_wallet.Wallet.BalanceCents, Is.EqualTo(12345));
            Assert.That(_clock.Clock.Current.TotalSeconds, Is.EqualTo(clockBefore));
            Assert.That(_root.transform.position, Is.EqualTo(new Vector3(2, 3, 4)));
            Assert.That(File.ReadAllText(_path), Is.EqualTo(fileBefore));
        }

        [Test]
        public void DirectSnapshotWithNullConversationsFailsPreflightWithoutChangingMessages()
        {
            AddIncoming("live");
            Assert.That(Save(), Is.True);
            var data = ReadSave();
            data.Messages.Conversations = null;
            string before = SnapshotJson();
            object[] arguments = { data, null };
            bool valid = (bool)typeof(GameSaveController).GetMethod("ValidateSaveData", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_save, arguments);
            Assert.That(valid, Is.False);
            Assert.That(SnapshotJson(), Is.EqualTo(before));
        }

        [Test]
        public void UnityJsonNullArrayIsNormalizedToAnEmptyArrayBeforeDomainValidation()
        {
            // JsonUtility defines null arrays as empty arrays. Do not mistake this for a missing Messages section.
            var data = new GameSaveData { Messages = new PhoneMessagesSnapshot { Version = 0, Conversations = null } };
            JsonUtility.FromJsonOverwrite("{\"Messages\":{\"Version\":1,\"Conversations\":null}}", data);
            Assert.That(data.Messages.Conversations, Is.Not.Null.And.Empty);
            Assert.That(data.Messages.Version, Is.EqualTo(1));
        }

        [Test]
        public void OldSkeletonSaveVersionIsRejectedWithoutMigration()
        {
            AddIncoming("current");
            Assert.That(Save(), Is.True);
            var data = ReadSave();
            data.Version = 1;
            data.Messages = null;
            File.WriteAllText(_path, JsonUtility.ToJson(data));
            string before = SnapshotJson();
            LogAssert.Expect(LogType.Error, $"Save validation failed: {_path}");
            Assert.That(Load(), Is.False);
            Assert.That(SnapshotJson(), Is.EqualTo(before));
        }

        [Test]
        public void MissingOwnerRefusesSaveAndLoadWithoutOverwritingFile()
        {
            Assert.That(Save(), Is.True);
            string before = File.ReadAllText(_path);
            SetField(_save, "_phoneMessages", null);
            LogAssert.Expect(LogType.Error, $"GameSaveController on {_root.name} has incomplete configuration.");
            Assert.That(Save(), Is.False);
            LogAssert.Expect(LogType.Error, $"GameSaveController on {_root.name} has incomplete configuration.");
            Assert.That(Load(), Is.False);
            Assert.That(File.ReadAllText(_path), Is.EqualTo(before));
        }

        [Test]
        public void OwnerKeepsOneEmptyInitiallyCreatedInstanceAcrossEnableChanges()
        {
            var instance = _owner.Messages;
            Assert.That(instance, Is.Not.Null);
            Assert.That(instance.Conversations, Is.Empty);
            AddIncoming("keep");
            _owner.enabled = false;
            _owner.enabled = true;
            Assert.That(_owner.Messages, Is.SameAs(instance));
            Assert.That(instance.TotalUnreadCount, Is.EqualTo(1));
        }

        [Test]
        public void ExistingStateStillRestoresAlongsideMessages()
        {
            AddIncoming("saved");
            Assert.That(Save(), Is.True);
            long savedTime = _clock.Clock.Current.TotalSeconds;
            _wallet.Wallet.Restore(222);
            _clock.Clock.AdvanceMinutes(8);
            _root.transform.position = new Vector3(10, 0, 3);
            _owner.Messages.MarkConversationRead("landlord");
            Assert.That(Load(), Is.True);
            Assert.That(_wallet.Wallet.BalanceCents, Is.EqualTo(4000));
            Assert.That(_clock.Clock.Current.TotalSeconds, Is.EqualTo(savedTime));
            Assert.That(_root.transform.position, Is.EqualTo(Vector3.zero));
            Assert.That(_owner.Messages.TotalUnreadCount, Is.EqualTo(1));
        }

        private void AddIncoming(string id, string contact = "landlord")
            => _owner.Messages.TryAddIncoming(id, contact, MessageContent.FromText("Тестовое сообщение"), _clock.Clock.Current);

        private bool Save() => InvokeSaveMethod("TrySave");
        private bool Load() => InvokeSaveMethod("TryLoad");
        private string SnapshotJson() => JsonUtility.ToJson(_owner.Messages.CaptureSnapshot());
        private GameSaveData ReadSave() => JsonUtility.FromJson<GameSaveData>(File.ReadAllText(_path));

        private bool InvokeSaveMethod(string method)
            => (bool)typeof(GameSaveController).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_save, new object[] { _path });

        private static void SetField(object target, string name, object value)
            => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private static void SetProperty(object target, string name, object value)
            => target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public).SetValue(target, value);
    }
}
