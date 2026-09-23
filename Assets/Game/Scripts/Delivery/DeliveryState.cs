using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using GoLive.GameTime;
using GoLive.Shop;

namespace GoLive.Delivery
{
    public enum DeliveryStage
    {
        PackageAvailable = 0,
        Opened = 1
    }

    public sealed class DeliveryRecord
    {
        public string OrderId { get; }
        public string PackageInstanceId { get; }
        public string FulfillmentInstanceId { get; private set; }
        public DeliveryStage Stage { get; private set; }
        public bool IsOpened => Stage == DeliveryStage.Opened;

        internal DeliveryRecord(
            string orderId,
            string packageInstanceId,
            DeliveryStage stage,
            string fulfillmentInstanceId)
        {
            OrderId = orderId;
            PackageInstanceId = packageInstanceId;
            Stage = stage;
            FulfillmentInstanceId = fulfillmentInstanceId;
        }

        internal void MarkOpened(string fulfillmentInstanceId)
        {
            Stage = DeliveryStage.Opened;
            FulfillmentInstanceId = fulfillmentInstanceId;
        }
    }

    public sealed class DeliveryState
    {
        public IReadOnlyList<DeliveryRecord> Records { get; private set; }

        private List<DeliveryRecord> _records = new();
        private Dictionary<string, DeliveryRecord> _byOrder = new(StringComparer.Ordinal);
        private Dictionary<string, DeliveryRecord> _byPackage = new(StringComparer.Ordinal);
        private HashSet<string> _itemIds = new(StringComparer.Ordinal);

        public DeliveryState()
        {
            Records = new ReadOnlyCollection<DeliveryRecord>(_records);
        }

        public bool TryGetRecord(string orderId, out DeliveryRecord record)
        {
            record = null;
            return orderId != null && _byOrder.TryGetValue(orderId, out record);
        }

        public bool TryGetRecordForPackage(string packageInstanceId, out DeliveryRecord record)
        {
            record = null;
            return packageInstanceId != null && _byPackage.TryGetValue(packageInstanceId, out record);
        }

        public bool NeedsArrival(ShopOrder order, GameTimeSnapshot now)
        {
            return order != null &&
                   order.IsDeliveryDue(now) &&
                   !_byOrder.ContainsKey(order.OrderId);
        }

        public bool TryRecordArrival(
            ShopOrderBook orders,
            string orderId,
            string packageInstanceId,
            GameTimeSnapshot now)
        {
            if (orders == null ||
                !orders.TryGetOrder(orderId, out ShopOrder order) ||
                !NeedsArrival(order, now) ||
                !IsUnusedItemId(packageInstanceId))
            {
                return false;
            }

            DeliveryRecord record = new(orderId, packageInstanceId, DeliveryStage.PackageAvailable, null);
            Add(record);

            if (orders.TryMarkDelivered(orderId, now))
                return true;

            Remove(record);
            return false;
        }

        public bool TryRecordOpened(string orderId, string fulfillmentInstanceId)
        {
            if (!TryGetRecord(orderId, out DeliveryRecord record) ||
                record.IsOpened ||
                !IsUnusedItemId(fulfillmentInstanceId))
            {
                return false;
            }

            record.MarkOpened(fulfillmentInstanceId);
            _itemIds.Add(fulfillmentInstanceId);
            return true;
        }

        public bool MatchesOrders(ShopOrderBook orders)
        {
            if (orders == null)
                return false;

            IReadOnlyList<ShopOrder> list = orders.Orders;
            int deliveredOrders = 0;

            for (int i = 0; i < list.Count; i++)
            {
                bool delivered = list[i].Status == ShopOrderStatus.Delivered;

                if (delivered != _byOrder.ContainsKey(list[i].OrderId))
                    return false;

                if (delivered)
                    deliveredOrders++;
            }

            return deliveredOrders == _records.Count;
        }

        public DeliverySnapshot CaptureSnapshot()
        {
            DeliveryRecordSnapshot[] deliveries = new DeliveryRecordSnapshot[_records.Count];

            for (int i = 0; i < _records.Count; i++)
            {
                DeliveryRecord record = _records[i];

                deliveries[i] = new DeliveryRecordSnapshot
                {
                    OrderId = record.OrderId,
                    PackageInstanceId = record.PackageInstanceId,
                    FulfillmentInstanceId = record.FulfillmentInstanceId ?? string.Empty,
                    Stage = record.Stage
                };
            }

            return new DeliverySnapshot
            {
                Deliveries = deliveries
            };
        }

        public void Restore(DeliverySnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            if (snapshot.Version != DeliverySnapshot.CurrentVersion ||
                snapshot.Deliveries == null)
            {
                throw new ArgumentException(
                    "Unsupported or incomplete Delivery snapshot.",
                    nameof(snapshot));
            }

            int count = snapshot.Deliveries.Length;
            List<DeliveryRecord> records = new(count);
            Dictionary<string, DeliveryRecord> byOrder = new(count, StringComparer.Ordinal);
            Dictionary<string, DeliveryRecord> byPackage = new(count, StringComparer.Ordinal);
            HashSet<string> itemIds = new(StringComparer.Ordinal);

            for (int i = 0; i < count; i++)
            {
                DeliveryRecord record = RestoreRecord(snapshot.Deliveries[i]);

                if (!byOrder.TryAdd(record.OrderId, record))
                {
                    throw new ArgumentException(
                        $"Duplicate delivery for Order ID '{record.OrderId}'.",
                        nameof(snapshot));
                }

                if (!itemIds.Add(record.PackageInstanceId) ||
                    (record.IsOpened && !itemIds.Add(record.FulfillmentInstanceId)))
                {
                    throw new ArgumentException(
                        $"Delivery for Order ID '{record.OrderId}' reuses an item instance ID.",
                        nameof(snapshot));
                }

                byPackage.Add(record.PackageInstanceId, record);
                records.Add(record);
            }

            _records = records;
            _byOrder = byOrder;
            _byPackage = byPackage;
            _itemIds = itemIds;
            Records = new ReadOnlyCollection<DeliveryRecord>(_records);
        }

        private bool IsUnusedItemId(string instanceId)
        {
            return !string.IsNullOrWhiteSpace(instanceId) && !_itemIds.Contains(instanceId);
        }

        private void Add(DeliveryRecord record)
        {
            _records.Add(record);
            _byOrder.Add(record.OrderId, record);
            _byPackage.Add(record.PackageInstanceId, record);
            _itemIds.Add(record.PackageInstanceId);
        }

        private void Remove(DeliveryRecord record)
        {
            _records.Remove(record);
            _byOrder.Remove(record.OrderId);
            _byPackage.Remove(record.PackageInstanceId);
            _itemIds.Remove(record.PackageInstanceId);
        }

        private static DeliveryRecord RestoreRecord(DeliveryRecordSnapshot saved)
        {
            if (saved == null)
                throw new ArgumentException("A saved delivery is missing.", nameof(saved));

            if (!ShopId.IsValid(saved.OrderId))
                throw new ArgumentException("Saved delivery has an invalid Order ID.", nameof(saved));

            if (string.IsNullOrWhiteSpace(saved.PackageInstanceId))
                throw new ArgumentException("Saved delivery has no package instance ID.", nameof(saved));

            switch (saved.Stage)
            {
                case DeliveryStage.PackageAvailable when string.IsNullOrEmpty(saved.FulfillmentInstanceId):
                    return new DeliveryRecord(saved.OrderId, saved.PackageInstanceId, saved.Stage, null);

                case DeliveryStage.Opened when !string.IsNullOrWhiteSpace(saved.FulfillmentInstanceId):
                    return new DeliveryRecord(saved.OrderId, saved.PackageInstanceId, saved.Stage, saved.FulfillmentInstanceId);

                default:
                    throw new ArgumentException(
                        $"Saved delivery for Order ID '{saved.OrderId}' has an unknown stage or a fulfillment item that does not match it.",
                        nameof(saved));
            }
        }
    }

    [Serializable]
    public sealed class DeliverySnapshot
    {
        public const int CurrentVersion = 1;

        public int Version = CurrentVersion;
        public DeliveryRecordSnapshot[] Deliveries = Array.Empty<DeliveryRecordSnapshot>();
    }

    [Serializable]
    public sealed class DeliveryRecordSnapshot
    {
        public string OrderId;
        public string PackageInstanceId;
        public string FulfillmentInstanceId;
        public DeliveryStage Stage;
    }
}
