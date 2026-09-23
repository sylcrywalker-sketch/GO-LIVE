using System;
using System.Collections.Generic;
using GoLive.GameTime;
using GoLive.Items;
using GoLive.Player;
using GoLive.Shop;
using UnityEngine;

namespace GoLive.Delivery
{
    [DisallowMultipleComponent]
    public sealed class DeliveryBehaviour : MonoBehaviour
    {
        private const int SlotsPerRow = 3;
        private const int SlotRows = 2;
        private const float PlacementRayLength = 3f;
        private const float PlacementStep = 0.1f;
        private const int PlacementAttempts = 7;
        private const float PlacementSkin = 0.01f;

        private static readonly Collider[] PlacementHits = new Collider[1];

        [SerializeField] private ShopBehaviour shop;
        [SerializeField] private GameClockBehaviour gameClock;
        [SerializeField] private ItemDefinition packageItem;
        [SerializeField] private Transform dropPoint;
        [SerializeField, Min(0.1f)] private float slotSpacing = 0.45f;

        public DeliveryState State { get; } = new();

        private readonly HashSet<string> _failedOrders = new(StringComparer.Ordinal);

        private GameClock _clock;
        private bool _started;
        private bool _bound;

        private void Awake()
        {
            if (!ValidateConfiguration())
                enabled = false;
        }

        private void Start()
        {
            if (!isActiveAndEnabled)
                return;

            _clock = gameClock.Clock;

            if (_clock == null)
            {
                Debug.LogError($"{nameof(DeliveryBehaviour)} could not access an initialized Game Clock.", this);
                enabled = false;
                return;
            }

            _started = true;

            Bind();
            DeliverDueOrders(_clock.Current);
        }

        private void OnEnable()
        {
            if (!_started)
                return;

            Bind();
            DeliverDueOrders(_clock.Current);
        }

        private void OnDisable()
        {
            Unbind();
        }

        public DeliverySnapshot CaptureSnapshot()
        {
            return State.CaptureSnapshot();
        }

        public bool TryResolveRuntimeItems(
            DeliverySnapshot snapshot,
            ShopOrdersSnapshot orders,
            out Dictionary<string, ItemDefinition> items)
        {
            items = null;

            DeliveryState deliveries = new();
            ShopOrderBook savedOrders = new();

            try
            {
                deliveries.Restore(snapshot);
                savedOrders.Restore(orders);
            }
            catch (ArgumentException)
            {
                return false;
            }

            if (shop == null || !IsValidPackageItem(packageItem) || !deliveries.MatchesOrders(savedOrders))
                return false;

            Dictionary<string, ItemDefinition> resolved = new(StringComparer.Ordinal);

            for (int i = 0; i < deliveries.Records.Count; i++)
            {
                DeliveryRecord record = deliveries.Records[i];

                if (!savedOrders.TryGetOrder(record.OrderId, out ShopOrder order) ||
                    !shop.TryGetProduct(order.ProductId, out ShopProductDefinition product))
                {
                    return false;
                }

                resolved.Add(record.PackageInstanceId, packageItem);

                if (!record.IsOpened)
                    continue;

                if (!IsDeliverable(product))
                    return false;

                resolved.Add(record.FulfillmentInstanceId, product.FulfillmentItem);
            }

            items = resolved;
            return true;
        }

        public void Restore(DeliverySnapshot snapshot, IReadOnlyDictionary<string, WorldItem> items)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));

            State.Restore(snapshot);
            _failedOrders.Clear();

            for (int i = 0; i < State.Records.Count; i++)
            {
                DeliveryRecord record = State.Records[i];

                if (!items.TryGetValue(record.PackageInstanceId, out WorldItem item) ||
                    !item.TryGetComponent(out DeliveryPackageBehaviour package))
                {
                    throw new InvalidOperationException(
                        $"Delivery package {record.PackageInstanceId} for order {record.OrderId} is missing.");
                }

                package.Bind(this, record.IsOpened);
            }
        }

        // An unopened package opens while it lies in the world or while this carrier holds it.
        internal bool CanOpen(WorldItem package, PlayerCarry carry)
        {
            return isActiveAndEnabled &&
                   carry != null &&
                   package != null &&
                   package.Instance != null &&
                   (package.Instance.Location == ItemLocation.World || carry.CarriedItem == package) &&
                   State.TryGetRecordForPackage(package.Instance.InstanceId, out DeliveryRecord record) &&
                   !record.IsOpened;
        }

        internal bool TryOpen(DeliveryPackageBehaviour package, PlayerCarry carry)
        {
            WorldItem box = package != null ? package.Item : null;

            if (!CanOpen(box, carry))
                return false;

            State.TryGetRecordForPackage(box.Instance.InstanceId, out DeliveryRecord record);

            if (!shop.TryGetOrder(record.OrderId, out ShopOrder order) ||
                !shop.TryGetProduct(order.ProductId, out ShopProductDefinition product) ||
                !IsDeliverable(product))
            {
                Debug.LogError(
                    $"Delivery package for order {record.OrderId} cannot be opened: its product has no deliverable fulfillment item.",
                    this);

                return false;
            }

            // A carried package is set down upright first; a package in the world opens exactly where it lies.
            if (carry.CarriedItem == box)
            {
                Quaternion upright = Quaternion.Euler(0f, box.transform.eulerAngles.y, 0f);

                if (!carry.TryPlace(FindPlacement(box, carry, upright), upright))
                    return false;
            }

            ItemDefinition contents = product.FulfillmentItem;
            Transform anchor = package.ContentsAnchor;

            WorldItem item = WorldItem.SpawnRuntime(
                contents,
                ItemInstance.CreateNew(contents.ItemId),
                anchor.position,
                anchor.rotation);

            if (item == null || !State.TryRecordOpened(record.OrderId, item.Instance.InstanceId))
            {
                if (item != null)
                    item.DestroyRuntime();

                Debug.LogError(
                    $"Delivery package for order {record.OrderId} could not produce its {contents.ItemId}.",
                    this);

                return false;
            }

            package.SetOpened(true);
            return true;
        }

        private void Bind()
        {
            if (_bound)
                return;

            _clock.Advanced += HandleTimeAdvanced;
            _bound = true;
        }

        private void Unbind()
        {
            if (!_bound)
                return;

            _clock.Advanced -= HandleTimeAdvanced;
            _bound = false;
        }

        private void HandleTimeAdvanced(GameTimeAdvance advance)
        {
            DeliverDueOrders(advance.Current);
        }

        private void DeliverDueOrders(GameTimeSnapshot now)
        {
            IReadOnlyList<ShopOrder> orders = shop.Orders.Orders;

            for (int i = 0; i < orders.Count; i++)
            {
                ShopOrder order = orders[i];

                if (State.NeedsArrival(order, now) && !_failedOrders.Contains(order.OrderId))
                    Deliver(order, now);
            }
        }

        private void Deliver(ShopOrder order, GameTimeSnapshot now)
        {
            if (!shop.TryGetProduct(order.ProductId, out ShopProductDefinition product) || !IsDeliverable(product))
            {
                ReportFailure(order, "its product has no deliverable fulfillment item");
                return;
            }

            WorldItem package = WorldItem.SpawnRuntime(
                packageItem,
                ItemInstance.CreateNew(packageItem.ItemId),
                GetSlotPosition(State.Records.Count),
                dropPoint.rotation);

            if (package == null)
            {
                ReportFailure(order, "the delivery package could not be spawned");
                return;
            }

            if (!State.TryRecordArrival(shop.Orders, order.OrderId, package.Instance.InstanceId, now))
            {
                package.DestroyRuntime();
                ReportFailure(order, "the arrival could not be recorded");
                return;
            }

            package.GetComponent<DeliveryPackageBehaviour>().Bind(this, false);
        }

        private void ReportFailure(ShopOrder order, string reason)
        {
            _failedOrders.Add(order.OrderId);

            Debug.LogError(
                $"Order {order.OrderId} ({order.ProductId}) was not delivered: {reason}. It stays Placed and is retried after the next load.",
                this);
        }

        private Vector3 GetSlotPosition(int deliveryIndex)
        {
            int slot = deliveryIndex % (SlotsPerRow * SlotRows);

            float x = (slot % SlotsPerRow) switch
            {
                1 => slotSpacing,
                2 => -slotSpacing,
                _ => 0f
            };

            float z = slot / SlotsPerRow * slotSpacing;

            return dropPoint.position + dropPoint.rotation * new Vector3(x, 0f, z);
        }

        // On the floor under the carried package; when that spot overlaps the carrier's body (the box swings close
        // while looking down) or anything else, the next clear spot a little further in front of the carrier.
        private static Vector3 FindPlacement(WorldItem box, PlayerCarry carry, Quaternion upright)
        {
            Vector3 under = GetFloorPosition(box.transform.position);

            if (!box.TryGetComponent(out BoxCollider shape))
                return under;

            Vector3 forward = Vector3.ProjectOnPlane(carry.transform.forward, Vector3.up).normalized;

            for (int i = 0; i < PlacementAttempts; i++)
            {
                Vector3 candidate = i == 0 ? under : GetFloorPosition(box.transform.position + forward * (PlacementStep * i));

                if (IsClear(shape, candidate, upright))
                    return candidate;
            }

            return under;
        }

        // The carried package's own colliders are disabled, so any overlap is something else; the test box is
        // lifted off and shrunk from the floor it will stand on.
        private static bool IsClear(BoxCollider shape, Vector3 position, Quaternion rotation)
        {
            Vector3 scale = shape.transform.lossyScale;
            Vector3 halfExtents = Vector3.Scale(shape.size, scale) * 0.5f - Vector3.one * PlacementSkin;
            Vector3 center = position + rotation * Vector3.Scale(shape.center, scale) + Vector3.up * (2f * PlacementSkin);

            return Physics.OverlapBoxNonAlloc(
                center,
                halfExtents,
                PlacementHits,
                rotation,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore) == 0;
        }

        private static Vector3 GetFloorPosition(Vector3 from)
        {
            return Physics.Raycast(
                from,
                Vector3.down,
                out RaycastHit hit,
                PlacementRayLength,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore)
                ? hit.point
                : from;
        }

        private static bool IsDeliverable(ShopProductDefinition product)
        {
            return product.FulfillmentItem != null && product.FulfillmentItem.TryGetRuntimePrefab(out _);
        }

        private static bool IsValidPackageItem(ItemDefinition item)
        {
            return item != null &&
                   !item.CanStoreInInventory &&
                   item.TryGetRuntimePrefab(out WorldItem prefab) &&
                   prefab.TryGetComponent<DeliveryPackageBehaviour>(out _);
        }

        private bool ValidateConfiguration()
        {
            if (shop == null || gameClock == null || dropPoint == null)
            {
                Debug.LogError(
                    $"{nameof(DeliveryBehaviour)} on {name} requires a Shop, a Game Clock and a Drop Point.",
                    this);

                return false;
            }

            if (!IsValidPackageItem(packageItem))
            {
                Debug.LogError(
                    $"{nameof(DeliveryBehaviour)} on {name} requires a Package Item that cannot be stored in the Inventory and whose World Prefab has a WorldItem, a Collider and a {nameof(DeliveryPackageBehaviour)}.",
                    this);

                return false;
            }

            return true;
        }
    }
}
