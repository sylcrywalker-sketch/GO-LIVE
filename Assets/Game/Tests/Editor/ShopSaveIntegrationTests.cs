using System.IO;
using GoLive.Delivery;
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
    public sealed class ShopSaveIntegrationTests
    {
        private const string GpuId = SaveTestWorld.BudgetGpuId;

        private SaveTestWorld _world;
        private SceneSetup[] _previousScenes;

        private long Balance => _world.Wallet.Wallet.BalanceCents;

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
            _world = SaveTestWorld.Create(2500);
        }

        [TearDown]
        public void TearDown()
        {
            _world?.Dispose();
        }

        [Test]
        public void SaveFileIsSchemaV5WithOrdersDeliveryAndPcSections()
        {
            ShopOrder placed = _world.Shop.TryPurchase(GpuId).Order;

            Assert.That(_world.TrySave(), Is.True);

            GameSaveData data = _world.ReadSave();
            Assert.That(data.Version, Is.EqualTo(5));
            Assert.That(data.PcAssembly, Is.Not.Null);
            Assert.That(data.PcAssembly.Version, Is.EqualTo(PcAssembly.SnapshotVersion));
            Assert.That(data.PcAssembly.InstalledSlots, Is.Empty);
            Assert.That(data.Delivery, Is.Not.Null);
            Assert.That(data.Delivery.Version, Is.EqualTo(DeliverySnapshot.CurrentVersion));
            Assert.That(data.Delivery.Deliveries, Is.Empty);
            Assert.That(data.BalanceCents, Is.EqualTo(1000));
            Assert.That(data.Orders, Is.Not.Null);
            Assert.That(data.Orders.Version, Is.EqualTo(ShopOrdersSnapshot.CurrentVersion));
            Assert.That(data.Orders.Orders, Has.Length.EqualTo(1));

            ShopOrderSnapshot saved = data.Orders.Orders[0];
            Assert.That(saved.OrderId, Is.EqualTo(placed.OrderId));
            Assert.That(saved.ProductId, Is.EqualTo(GpuId));
            Assert.That(saved.PaidPriceCents, Is.EqualTo(1500));
            Assert.That(saved.PlacedAtGameTimeSeconds, Is.EqualTo(placed.PlacedAt.TotalSeconds));
            Assert.That(saved.DeliveryDueGameTimeSeconds, Is.EqualTo(placed.DeliveryDueAt.TotalSeconds));
            Assert.That(saved.Status, Is.EqualTo(ShopOrderStatus.Placed));
        }

        [Test]
        public void BudgetGpuPurchaseSurvivesSaveAndLoad()
        {
            ShopPurchaseResult purchase = _world.Shop.TryPurchase(GpuId);

            Assert.That(purchase.Succeeded, Is.True);
            Assert.That(Balance, Is.EqualTo(1000));
            Assert.That(_world.Shop.Orders.Orders, Has.Count.EqualTo(1));
            Assert.That(_world.TrySave(), Is.True);

            _world.Wallet.Wallet.Restore(2222);
            _world.Shop.RestoreOrders(new ShopOrdersSnapshot());
            _world.Clock.Clock.AdvanceMinutes(30);

            Assert.That(_world.TryLoad(), Is.True);
            Assert.That(Balance, Is.EqualTo(1000));
            Assert.That(_world.Shop.Orders.Orders, Has.Count.EqualTo(1));

            ShopOrder loaded = _world.Shop.Orders.Orders[0];
            Assert.That(loaded.OrderId, Is.EqualTo(purchase.Order.OrderId));
            Assert.That(loaded.ProductId, Is.EqualTo(GpuId));
            Assert.That(loaded.PaidPriceCents, Is.EqualTo(1500));
            Assert.That(loaded.PlacedAt.TotalSeconds, Is.EqualTo(purchase.Order.PlacedAt.TotalSeconds));
            Assert.That(loaded.DeliveryDueAt.TotalSeconds, Is.EqualTo(purchase.Order.DeliveryDueAt.TotalSeconds));
            Assert.That(loaded.Status, Is.EqualTo(ShopOrderStatus.Placed));
        }

        [Test]
        public void LoadedOrderKeepsTheBudgetGpuPurchaseLimit()
        {
            _world.Shop.TryPurchase(GpuId);
            Assert.That(_world.TrySave(), Is.True);

            _world.Wallet.Wallet.Restore(2500);
            _world.Shop.RestoreOrders(new ShopOrdersSnapshot());
            Assert.That(_world.TryLoad(), Is.True);

            Assert.That(_world.Shop.EvaluatePurchase(GpuId), Is.EqualTo(ShopPurchaseResultCode.PurchaseLimitReached));
            Assert.That(_world.Shop.TryPurchase(GpuId).Code, Is.EqualTo(ShopPurchaseResultCode.PurchaseLimitReached));
            Assert.That(Balance, Is.EqualTo(1000));
            Assert.That(_world.Shop.Orders.Orders, Has.Count.EqualTo(1));
        }

        [Test]
        public void RepeatedLoadOfTheSameSaveNeverDuplicatesOrders()
        {
            _world.Shop.TryPurchase(GpuId);
            Assert.That(_world.TrySave(), Is.True);

            ShopOrderBook book = _world.Shop.Orders;

            Assert.That(_world.TryLoad(), Is.True);
            Assert.That(_world.TryLoad(), Is.True);
            Assert.That(_world.TryLoad(), Is.True);

            Assert.That(_world.Shop.Orders, Is.SameAs(book));
            Assert.That(book.Orders, Has.Count.EqualTo(1));
            Assert.That(Balance, Is.EqualTo(1000));
        }

        [Test]
        public void EmptyOrdersSnapshotSavesAndLoads()
        {
            Assert.That(_world.TrySave(), Is.True);

            GameSaveData data = _world.ReadSave();
            Assert.That(data.Orders, Is.Not.Null);
            Assert.That(data.Orders.Version, Is.EqualTo(ShopOrdersSnapshot.CurrentVersion));
            Assert.That(data.Orders.Orders, Is.Empty);

            _world.Shop.TryPurchase(GpuId);

            Assert.That(_world.TryLoad(), Is.True);
            Assert.That(_world.Shop.Orders.Orders, Is.Empty);
            Assert.That(Balance, Is.EqualTo(2500));
            Assert.That(_world.Shop.EvaluatePurchase(GpuId), Is.EqualTo(ShopPurchaseResultCode.Success));
        }

        [TestCase("duplicate order id")]
        [TestCase("unknown product")]
        [TestCase("malformed product id")]
        [TestCase("placed after save time")]
        [TestCase("delivery before placement")]
        [TestCase("delivered before due time")]
        [TestCase("negative time")]
        [TestCase("non-positive price")]
        [TestCase("unknown status")]
        [TestCase("purchase limit exceeded")]
        [TestCase("unsupported version")]
        public void InvalidOrdersRejectTheWholeSaveBeforeAnyStateChanges(string corruption)
        {
            _world.Shop.TryPurchase(GpuId);
            AddIncoming("saved-message");
            Assert.That(_world.TrySave(), Is.True);

            GameSaveData data = _world.ReadSave();
            ShopOrderSnapshot order = data.Orders.Orders[0];

            switch (corruption)
            {
                case "duplicate order id":
                    data.Orders.Orders = new[] { order, CopyOf(order, order.OrderId, SaveTestWorld.SnackId) };
                    break;
                case "unknown product":
                    order.ProductId = "retired-product";
                    break;
                case "malformed product id":
                    order.ProductId = "Budget GPU";
                    break;
                case "placed after save time":
                    order.PlacedAtGameTimeSeconds = data.GameTimeSeconds + 60;
                    order.DeliveryDueGameTimeSeconds = data.GameTimeSeconds + 9060;
                    break;
                case "delivery before placement":
                    order.DeliveryDueGameTimeSeconds = order.PlacedAtGameTimeSeconds - 60;
                    break;
                case "delivered before due time":
                    order.Status = ShopOrderStatus.Delivered;
                    break;
                case "negative time":
                    order.PlacedAtGameTimeSeconds = -1;
                    break;
                case "non-positive price":
                    order.PaidPriceCents = 0;
                    break;
                case "unknown status":
                    order.Status = (ShopOrderStatus)7;
                    break;
                case "purchase limit exceeded":
                    data.Orders.Orders = new[] { order, CopyOf(order, "second-gpu-order", GpuId) };
                    break;
                case "unsupported version":
                    data.Orders.Version = 99;
                    break;
            }

            File.WriteAllText(_world.SavePath, JsonUtility.ToJson(data));
            AssertLoadRejectedWithoutTouchingLiveState();
        }

        [TestCase("missing section")]
        [TestCase("null section")]
        [TestCase("empty section")]
        [TestCase("missing orders array")]
        public void CurrentSaveWithoutAnOrdersSectionIsRejected(string corruption)
        {
            _world.Shop.TryPurchase(GpuId);
            Assert.That(_world.TrySave(), Is.True);

            GameSaveData data = _world.ReadSave();
            string json = JsonUtility.ToJson(data);
            string ordersField = "\"Orders\":" + JsonUtility.ToJson(data.Orders);
            Assert.That(json, Does.Contain(ordersField + ","));

            // JsonUtility.ToJson expands inline null classes into default objects; corrupt the JSON itself.
            json = corruption switch
            {
                "missing section" => json.Replace(ordersField + ",", string.Empty),
                "null section" => json.Replace(ordersField, "\"Orders\":null"),
                "empty section" => json.Replace(ordersField, "\"Orders\":{}"),
                _ => json.Replace(ordersField, "\"Orders\":{\"Version\":1}")
            };

            File.WriteAllText(_world.SavePath, json);
            AssertLoadRejectedWithoutTouchingLiveState();
        }

        [TestCase("missing section")]
        [TestCase("null section")]
        [TestCase("empty section")]
        [TestCase("missing deliveries array")]
        public void CurrentSaveWithoutADeliverySectionIsRejected(string corruption)
        {
            _world.Shop.TryPurchase(GpuId);
            Assert.That(_world.TrySave(), Is.True);

            GameSaveData data = _world.ReadSave();
            string json = JsonUtility.ToJson(data);
            string deliveryField = "\"Delivery\":" + JsonUtility.ToJson(data.Delivery);
            Assert.That(json, Does.Contain(deliveryField + ","));

            json = corruption switch
            {
                "missing section" => json.Replace(deliveryField + ",", string.Empty),
                "null section" => json.Replace(deliveryField, "\"Delivery\":null"),
                "empty section" => json.Replace(deliveryField, "\"Delivery\":{}"),
                _ => json.Replace(deliveryField, "\"Delivery\":{\"Version\":1}")
            };

            File.WriteAllText(_world.SavePath, json);
            AssertLoadRejectedWithoutTouchingLiveState();
        }

        [Test]
        public void MissingDeliveryOwnerRefusesSaveAndLoadWithoutOverwritingFile()
        {
            Assert.That(_world.TrySave(), Is.True);
            string before = File.ReadAllText(_world.SavePath);

            SaveTestWorld.SetField(_world.Save, "_delivery", null);

            LogAssert.Expect(LogType.Error, $"GameSaveController on {_world.Root.name} has incomplete configuration.");
            Assert.That(_world.TrySave(), Is.False);
            LogAssert.Expect(LogType.Error, $"GameSaveController on {_world.Root.name} has incomplete configuration.");
            Assert.That(_world.TryLoad(), Is.False);
            Assert.That(File.ReadAllText(_world.SavePath), Is.EqualTo(before));
        }

        [Test]
        public void MessagesWalletAndOrdersRestoreTogether()
        {
            AddIncoming("rent-reminder");
            _world.Messages.Messages.MarkConversationRead("landlord");
            AddIncoming("rent-final");
            _world.Shop.TryPurchase(GpuId);
            _world.Shop.TryPurchase(SaveTestWorld.SnackId);
            string messages = MessagesJson();
            string orders = OrdersJson();

            Assert.That(_world.TrySave(), Is.True);

            _world.Messages.Messages.Restore(new PhoneMessagesSnapshot());
            _world.Wallet.Wallet.Restore(2500);
            _world.Shop.RestoreOrders(new ShopOrdersSnapshot());

            Assert.That(_world.TryLoad(), Is.True);
            Assert.That(MessagesJson(), Is.EqualTo(messages));
            Assert.That(_world.Messages.Messages.TotalUnreadCount, Is.EqualTo(1));
            Assert.That(Balance, Is.EqualTo(800));
            Assert.That(OrdersJson(), Is.EqualTo(orders));
            Assert.That(_world.Shop.Orders.CountActive(), Is.EqualTo(2));
        }

        [Test]
        public void MissingShopRefusesSaveAndLoadWithoutOverwritingFile()
        {
            Assert.That(_world.TrySave(), Is.True);
            string before = File.ReadAllText(_world.SavePath);

            SaveTestWorld.SetField(_world.Save, "_shop", null);

            LogAssert.Expect(LogType.Error, $"GameSaveController on {_world.Root.name} has incomplete configuration.");
            Assert.That(_world.TrySave(), Is.False);
            LogAssert.Expect(LogType.Error, $"GameSaveController on {_world.Root.name} has incomplete configuration.");
            Assert.That(_world.TryLoad(), Is.False);
            Assert.That(File.ReadAllText(_world.SavePath), Is.EqualTo(before));
        }

        private void AssertLoadRejectedWithoutTouchingLiveState()
        {
            string fileBefore = File.ReadAllText(_world.SavePath);

            _world.Wallet.Wallet.Restore(777);
            _world.Shop.RestoreOrders(new ShopOrdersSnapshot());
            _world.Clock.Clock.AdvanceMinutes(5);
            _world.Root.transform.position = new Vector3(1, 2, 3);
            AddIncoming("live-message");

            string messagesBefore = MessagesJson();
            string deliveryBefore = JsonUtility.ToJson(_world.Delivery.CaptureSnapshot());
            long clockBefore = _world.Clock.Clock.Current.TotalSeconds;
            ShopOrderBook book = _world.Shop.Orders;
            int orderChanges = 0;
            book.Changed += () => orderChanges++;

            LogAssert.Expect(LogType.Error, $"Save validation failed: {_world.SavePath}");
            Assert.That(_world.TryLoad(), Is.False);

            Assert.That(JsonUtility.ToJson(_world.Delivery.CaptureSnapshot()), Is.EqualTo(deliveryBefore));
            Assert.That(Balance, Is.EqualTo(777));
            Assert.That(_world.Shop.Orders, Is.SameAs(book));
            Assert.That(book.Orders, Is.Empty);
            Assert.That(orderChanges, Is.Zero);
            Assert.That(_world.Clock.Clock.Current.TotalSeconds, Is.EqualTo(clockBefore));
            Assert.That(_world.Root.transform.position, Is.EqualTo(new Vector3(1, 2, 3)));
            Assert.That(MessagesJson(), Is.EqualTo(messagesBefore));
            Assert.That(File.ReadAllText(_world.SavePath), Is.EqualTo(fileBefore));
        }

        private void AddIncoming(string id)
        {
            _world.Messages.Messages.TryAddIncoming(id, "landlord", MessageContent.FromText("Тестовое сообщение"), _world.Clock.Clock.Current);
        }

        private string MessagesJson()
        {
            return JsonUtility.ToJson(_world.Messages.Messages.CaptureSnapshot());
        }

        private string OrdersJson()
        {
            return JsonUtility.ToJson(_world.Shop.CaptureOrders());
        }

        private static ShopOrderSnapshot CopyOf(ShopOrderSnapshot order, string orderId, string productId)
        {
            return new ShopOrderSnapshot
            {
                OrderId = orderId,
                ProductId = productId,
                PaidPriceCents = order.PaidPriceCents,
                PlacedAtGameTimeSeconds = order.PlacedAtGameTimeSeconds,
                DeliveryDueGameTimeSeconds = order.DeliveryDueGameTimeSeconds,
                Status = order.Status
            };
        }
    }
}
