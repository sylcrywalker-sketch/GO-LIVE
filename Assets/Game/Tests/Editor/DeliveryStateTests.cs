using System;
using GoLive.Delivery;
using GoLive.Economy;
using GoLive.GameTime;
using GoLive.Shop;
using NUnit.Framework;
using UnityEngine;

namespace GoLive.Tests
{
    public sealed class DeliveryStateTests
    {
        private static readonly ShopPurchaseOffer BudgetGpu = new("budget-gpu", 1500, 1, 150, true);
        private static readonly ShopPurchaseOffer Banana = new("banana", 60, 0, 90, true);

        private GameClock _clock;
        private ShopOrderBook _orders;
        private ShopPurchase _purchase;
        private DeliveryState _state;

        [SetUp]
        public void SetUp()
        {
            _clock = new GameClock(1, 7, 3);
            _orders = new ShopOrderBook();
            _purchase = new ShopPurchase(new Wallet(2500), _orders, _clock);
            _state = new DeliveryState();
        }

        [Test]
        public void OrderThatIsNotDueNeedsNoArrivalAndCannotBeRecorded()
        {
            ShopOrder order = _purchase.TryPurchase(BudgetGpu).Order;
            _clock.AdvanceMinutes(149);

            Assert.That(_state.NeedsArrival(order, _clock.Current), Is.False);
            Assert.That(_state.TryRecordArrival(_orders, order.OrderId, "package-1", _clock.Current), Is.False);
            Assert.That(_state.Records, Is.Empty);
            Assert.That(order.Status, Is.EqualTo(ShopOrderStatus.Placed));
        }

        [Test]
        public void DueOrderRecordsExactlyOnePackageAndBecomesDelivered()
        {
            ShopOrder order = _purchase.TryPurchase(BudgetGpu).Order;
            _clock.AdvanceMinutes(150);

            Assert.That(_state.NeedsArrival(order, _clock.Current), Is.True);
            Assert.That(_state.TryRecordArrival(_orders, order.OrderId, "package-1", _clock.Current), Is.True);

            Assert.That(_state.Records, Has.Count.EqualTo(1));
            DeliveryRecord record = _state.Records[0];
            Assert.That(record.OrderId, Is.EqualTo(order.OrderId));
            Assert.That(record.PackageInstanceId, Is.EqualTo("package-1"));
            Assert.That(record.Stage, Is.EqualTo(DeliveryStage.PackageAvailable));
            Assert.That(record.FulfillmentInstanceId, Is.Null);
            Assert.That(order.Status, Is.EqualTo(ShopOrderStatus.Delivered));
            Assert.That(_orders.CountActive(), Is.Zero);
            Assert.That(_orders.Orders, Has.Count.EqualTo(1));
        }

        [Test]
        public void RepeatedEvaluationNeverRecordsASecondPackage()
        {
            ShopOrder order = _purchase.TryPurchase(BudgetGpu).Order;
            _clock.AdvanceMinutes(151);
            _state.TryRecordArrival(_orders, order.OrderId, "package-1", _clock.Current);

            for (int i = 0; i < 5; i++)
            {
                _clock.AdvanceMinutes(1);
                Assert.That(_state.NeedsArrival(order, _clock.Current), Is.False);
                Assert.That(_state.TryRecordArrival(_orders, order.OrderId, $"package-extra-{i}", _clock.Current), Is.False);
            }

            Assert.That(_state.Records, Has.Count.EqualTo(1));
            Assert.That(_state.Records[0].PackageInstanceId, Is.EqualTo("package-1"));
        }

        [Test]
        public void EightHourTimeJumpAcrossTheDueTimeDeliversOnce()
        {
            ShopOrder order = _purchase.TryPurchase(BudgetGpu).Order;
            int advances = 0;
            _clock.Advanced += _ => advances++;

            _clock.AdvanceMinutes(8 * 60);

            Assert.That(advances, Is.EqualTo(1));
            Assert.That(_clock.Current.TotalSeconds, Is.GreaterThan(order.DeliveryDueAt.TotalSeconds));
            Assert.That(_state.NeedsArrival(order, _clock.Current), Is.True);
            Assert.That(_state.TryRecordArrival(_orders, order.OrderId, "package-1", _clock.Current), Is.True);
            Assert.That(_state.NeedsArrival(order, _clock.Current), Is.False);
            Assert.That(_state.Records, Has.Count.EqualTo(1));
        }

        [Test]
        public void MultipleDueOrdersGetOnePackageEach()
        {
            ShopOrder gpu = _purchase.TryPurchase(BudgetGpu).Order;
            ShopOrder firstBanana = _purchase.TryPurchase(Banana).Order;
            ShopOrder secondBanana = _purchase.TryPurchase(Banana).Order;
            _clock.AdvanceMinutes(600);

            Assert.That(_state.TryRecordArrival(_orders, gpu.OrderId, "package-gpu", _clock.Current), Is.True);
            Assert.That(_state.TryRecordArrival(_orders, firstBanana.OrderId, "package-banana-1", _clock.Current), Is.True);
            Assert.That(_state.TryRecordArrival(_orders, secondBanana.OrderId, "package-banana-2", _clock.Current), Is.True);

            Assert.That(_state.Records, Has.Count.EqualTo(3));
            Assert.That(_orders.CountActive(), Is.Zero);
        }

        [Test]
        public void OnePackageInstanceCannotServeTwoOrders()
        {
            ShopOrder first = _purchase.TryPurchase(Banana).Order;
            ShopOrder second = _purchase.TryPurchase(Banana).Order;
            _clock.AdvanceMinutes(90);

            Assert.That(_state.TryRecordArrival(_orders, first.OrderId, "package-1", _clock.Current), Is.True);
            Assert.That(_state.TryRecordArrival(_orders, second.OrderId, "package-1", _clock.Current), Is.False);
            Assert.That(second.Status, Is.EqualTo(ShopOrderStatus.Placed));
            Assert.That(_state.Records, Has.Count.EqualTo(1));
        }

        [Test]
        public void UnknownOrderIsNeverRecorded()
        {
            _clock.AdvanceMinutes(600);

            Assert.That(_state.TryRecordArrival(_orders, "missing-order", "package-1", _clock.Current), Is.False);
            Assert.That(_state.Records, Is.Empty);
        }

        [Test]
        public void OpeningRecordsExactlyOneFulfillmentItemAndRepeatedOpenIsRejected()
        {
            ShopOrder order = _purchase.TryPurchase(BudgetGpu).Order;
            _clock.AdvanceMinutes(150);
            _state.TryRecordArrival(_orders, order.OrderId, "package-1", _clock.Current);

            Assert.That(_state.TryRecordOpened(order.OrderId, "gpu-1"), Is.True);
            Assert.That(_state.TryRecordOpened(order.OrderId, "gpu-2"), Is.False);

            DeliveryRecord record = _state.Records[0];
            Assert.That(record.Stage, Is.EqualTo(DeliveryStage.Opened));
            Assert.That(record.FulfillmentInstanceId, Is.EqualTo("gpu-1"));
            Assert.That(order.Status, Is.EqualTo(ShopOrderStatus.Delivered));
        }

        [Test]
        public void FulfillmentItemMustBeANewInstance()
        {
            ShopOrder order = _purchase.TryPurchase(BudgetGpu).Order;
            _clock.AdvanceMinutes(150);
            _state.TryRecordArrival(_orders, order.OrderId, "package-1", _clock.Current);

            Assert.That(_state.TryRecordOpened(order.OrderId, "package-1"), Is.False);
            Assert.That(_state.TryRecordOpened(order.OrderId, " "), Is.False);
            Assert.That(_state.TryRecordOpened("missing-order", "gpu-1"), Is.False);
            Assert.That(_state.Records[0].IsOpened, Is.False);
        }

        [Test]
        public void SnapshotRoundTripKeepsEveryRecord()
        {
            ShopOrder gpu = _purchase.TryPurchase(BudgetGpu).Order;
            ShopOrder banana = _purchase.TryPurchase(Banana).Order;
            _clock.AdvanceMinutes(150);
            _state.TryRecordArrival(_orders, gpu.OrderId, "package-gpu", _clock.Current);
            _state.TryRecordArrival(_orders, banana.OrderId, "package-banana", _clock.Current);
            _state.TryRecordOpened(gpu.OrderId, "gpu-1");

            DeliverySnapshot snapshot = JsonUtility.FromJson<DeliverySnapshot>(JsonUtility.ToJson(_state.CaptureSnapshot()));
            DeliveryState restored = new();
            restored.Restore(snapshot);

            Assert.That(snapshot.Version, Is.EqualTo(DeliverySnapshot.CurrentVersion));
            Assert.That(restored.Records, Has.Count.EqualTo(2));
            Assert.That(restored.TryGetRecord(gpu.OrderId, out DeliveryRecord opened), Is.True);
            Assert.That(opened.Stage, Is.EqualTo(DeliveryStage.Opened));
            Assert.That(opened.FulfillmentInstanceId, Is.EqualTo("gpu-1"));
            Assert.That(restored.TryGetRecordForPackage("package-banana", out DeliveryRecord waiting), Is.True);
            Assert.That(waiting.Stage, Is.EqualTo(DeliveryStage.PackageAvailable));
            Assert.That(waiting.FulfillmentInstanceId, Is.Null);
            Assert.That(restored.MatchesOrders(_orders), Is.True);
            Assert.That(restored.TryRecordOpened(gpu.OrderId, "gpu-2"), Is.False);
        }

        [TestCase("unsupported version")]
        [TestCase("missing array")]
        [TestCase("null record")]
        [TestCase("invalid order id")]
        [TestCase("blank package id")]
        [TestCase("duplicate order id")]
        [TestCase("duplicate package id")]
        [TestCase("opened without fulfillment item")]
        [TestCase("unopened with fulfillment item")]
        [TestCase("fulfillment item reused as package")]
        [TestCase("duplicate fulfillment item")]
        [TestCase("unknown stage")]
        public void InvalidSnapshotIsRejectedWithoutChangingState(string corruption)
        {
            ShopOrder order = _purchase.TryPurchase(BudgetGpu).Order;
            _clock.AdvanceMinutes(150);
            _state.TryRecordArrival(_orders, order.OrderId, "live-package", _clock.Current);
            string before = JsonUtility.ToJson(_state.CaptureSnapshot());

            DeliverySnapshot snapshot = new()
            {
                Deliveries = new[]
                {
                    Record("order-a", "package-a", DeliveryStage.Opened, "item-a"),
                    Record("order-b", "package-b", DeliveryStage.PackageAvailable, "")
                }
            };

            switch (corruption)
            {
                case "unsupported version": snapshot.Version = 2; break;
                case "missing array": snapshot.Deliveries = null; break;
                case "null record": snapshot.Deliveries[1] = null; break;
                case "invalid order id": snapshot.Deliveries[1].OrderId = "Order B"; break;
                case "blank package id": snapshot.Deliveries[1].PackageInstanceId = " "; break;
                case "duplicate order id": snapshot.Deliveries[1].OrderId = "order-a"; break;
                case "duplicate package id": snapshot.Deliveries[1].PackageInstanceId = "package-a"; break;
                case "opened without fulfillment item": snapshot.Deliveries[0].FulfillmentInstanceId = ""; break;
                case "unopened with fulfillment item": snapshot.Deliveries[1].FulfillmentInstanceId = "item-b"; break;
                case "fulfillment item reused as package": snapshot.Deliveries[1].PackageInstanceId = "item-a"; break;
                case "duplicate fulfillment item":
                    snapshot.Deliveries[1].Stage = DeliveryStage.Opened;
                    snapshot.Deliveries[1].FulfillmentInstanceId = "item-a";
                    break;
                case "unknown stage": snapshot.Deliveries[1].Stage = (DeliveryStage)7; break;
            }

            Assert.That(() => _state.Restore(snapshot), Throws.InstanceOf<ArgumentException>());
            Assert.That(JsonUtility.ToJson(_state.CaptureSnapshot()), Is.EqualTo(before));
        }

        [Test]
        public void DeliveryRecordsMustMatchDeliveredOrders()
        {
            ShopOrder gpu = _purchase.TryPurchase(BudgetGpu).Order;
            ShopOrder banana = _purchase.TryPurchase(Banana).Order;
            _clock.AdvanceMinutes(150);

            Assert.That(_state.MatchesOrders(_orders), Is.True, "no packages yet, both orders Placed");

            _state.TryRecordArrival(_orders, gpu.OrderId, "package-gpu", _clock.Current);
            Assert.That(_state.MatchesOrders(_orders), Is.True, "package exists, order Delivered");

            DeliveryState packageForPlacedOrder = new();
            packageForPlacedOrder.Restore(new DeliverySnapshot
            {
                Deliveries = new[]
                {
                    Record(gpu.OrderId, "package-gpu", DeliveryStage.PackageAvailable, ""),
                    Record(banana.OrderId, "package-banana", DeliveryStage.PackageAvailable, "")
                }
            });
            Assert.That(packageForPlacedOrder.MatchesOrders(_orders), Is.False, "package for a Placed order");

            DeliveryState deliveredWithoutPackage = new();
            Assert.That(deliveredWithoutPackage.MatchesOrders(_orders), Is.False, "Delivered order without a package");

            DeliveryState packageForUnknownOrder = new();
            packageForUnknownOrder.Restore(new DeliverySnapshot
            {
                Deliveries = new[]
                {
                    Record(gpu.OrderId, "package-gpu", DeliveryStage.PackageAvailable, ""),
                    Record("retired-order", "package-old", DeliveryStage.Opened, "item-old")
                }
            });
            Assert.That(packageForUnknownOrder.MatchesOrders(_orders), Is.False, "package for an unknown order");
        }

        private static DeliveryRecordSnapshot Record(string orderId, string packageId, DeliveryStage stage, string fulfillmentId)
        {
            return new DeliveryRecordSnapshot
            {
                OrderId = orderId,
                PackageInstanceId = packageId,
                Stage = stage,
                FulfillmentInstanceId = fulfillmentId
            };
        }
    }
}
