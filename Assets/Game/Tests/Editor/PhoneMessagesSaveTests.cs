using System.IO;
using System.Linq;
using System.Reflection;
using GoLive.Economy;
using GoLive.GameTime;
using GoLive.PcBuilding;
using GoLive.Persistence;
using GoLive.Phone;
using GoLive.Shop;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace GoLive.Tests
{
    public sealed class PhoneMessagesSaveTests
    {
        private SaveTestWorld _world;
        private GameObject _root;
        private GameSaveController _save;
        private PhoneMessagesBehaviour _owner;
        private GameClockBehaviour _clock;
        private WalletBehaviour _wallet;
        private ShopBehaviour _shop;
        private string _path;
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

        [SetUp]
        public void SetUp()
        {
            _world = SaveTestWorld.Create(4000);
            _root = _world.Root;
            _save = _world.Save;
            _owner = _world.Messages;
            _clock = _world.Clock;
            _wallet = _world.Wallet;
            _shop = _world.Shop;
            _path = _world.SavePath;
        }

        [TearDown]
        public void TearDown()
        {
            _world?.Dispose();
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
            Assert.That(data.Version, Is.EqualTo(7));
            Assert.That(data.Orders, Is.Not.Null);
            Assert.That(data.Orders.Version, Is.EqualTo(ShopOrdersSnapshot.CurrentVersion));
            Assert.That(data.Orders.Orders, Is.Empty);
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
            object[] arguments = { data, null, null };
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

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void OldSkeletonSaveVersionIsRejectedWithoutMigration(int version)
        {
            AddIncoming("current");
            ShopTestData.BuyOne(_shop, SaveTestWorld.BudgetGpuId);
            Assert.That(Save(), Is.True);
            var data = ReadSave();
            data.Version = version;
            // Every older save predates the Student PC's starter hardware: none of its parts among the items, none in the
            // PC record. Versions 1-4 also predate the PC assembly section, 1-3 Delivery, 2 and 1 Orders, 1 Messages.
            data.Items = data.Items.Where(item => !_world.StarterItemIds.Contains(item.InstanceId)).ToArray();
            data.PcAssembly.InstalledSlots = new PcInstalledSlotSnapshot[0];
            string json = JsonUtility.ToJson(data);
            if (version < 5)
                json = json.Replace("\"PcAssembly\":" + JsonUtility.ToJson(data.PcAssembly) + ",", string.Empty);
            if (version < 4)
                json = json.Replace("\"Delivery\":" + JsonUtility.ToJson(data.Delivery) + ",", string.Empty);
            if (version < 3)
                json = json.Replace("\"Orders\":" + JsonUtility.ToJson(data.Orders) + ",", string.Empty);
            if (version == 1)
                json = json.Replace("\"Messages\":" + JsonUtility.ToJson(data.Messages) + ",", string.Empty);
            Assert.That(json.Contains("\"PcAssembly\":"), Is.EqualTo(version == 5));
            File.WriteAllText(_path, json);
            string pcBefore = JsonUtility.ToJson(_world.Pc.CaptureSnapshot());
            _wallet.Wallet.Restore(777);
            _shop.RestoreOrders(new ShopOrdersSnapshot());
            _clock.Clock.AdvanceMinutes(3);
            _root.transform.position = new Vector3(5, 0, 1);
            string before = SnapshotJson();
            long clockBefore = _clock.Clock.Current.TotalSeconds;
            LogAssert.Expect(LogType.Error, $"Save validation failed: {_path}");
            Assert.That(Load(), Is.False);
            Assert.That(SnapshotJson(), Is.EqualTo(before));
            Assert.That(_wallet.Wallet.BalanceCents, Is.EqualTo(777));
            Assert.That(_shop.Orders.Orders, Is.Empty);
            Assert.That(_clock.Clock.Current.TotalSeconds, Is.EqualTo(clockBefore));
            Assert.That(_root.transform.position, Is.EqualTo(new Vector3(5, 0, 1)));
            Assert.That(JsonUtility.ToJson(_world.Pc.CaptureSnapshot()), Is.EqualTo(pcBefore), "the starter hardware stays installed");
            Assert.That(_world.Pc.Capabilities.CanPowerOn, Is.True);
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
            => SaveTestWorld.SetField(target, name, value);
    }
}
