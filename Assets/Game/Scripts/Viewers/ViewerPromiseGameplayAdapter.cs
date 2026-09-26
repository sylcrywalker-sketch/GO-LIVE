using System;
using System.Collections.Generic;
using GoLive.Desktop;
using GoLive.Items;
using GoLive.PcBuilding;
using GoLive.Shop;

namespace GoLive.Viewers
{
    // Explicitly owned scene adapter. Subscribes to paid orders, installed component/peripheral identities
    // and actual broadcast starts. Baseline construction and restore are silent; cart and prose never enter.
    public sealed class ViewerPromiseGameplayAdapter : IDisposable
    {
        private readonly ViewerCore _core;
        private readonly ShopOrderBook _orders;
        private readonly IReadOnlyList<ShopProductDefinition> _products;
        private readonly PcAssembly _assembly;
        private readonly PcPeripherals _peripherals;
        private readonly StreamSession _stream;
        private readonly Func<double> _minutes;
        private readonly HashSet<string> _knownOrders = new(StringComparer.Ordinal);
        private readonly HashSet<string> _installed = new(StringComparer.Ordinal);
        private string _lastBroadcast;
        private bool _restoring;

        public ViewerPromiseGameplayAdapter(ViewerCore core, ShopOrderBook orders, IReadOnlyList<ShopProductDefinition> products,
            PcAssembly assembly, PcPeripherals peripherals, StreamSession stream, Func<double> gameMinutes)
        {
            _core = core ?? throw new ArgumentNullException(nameof(core));
            _orders = orders ?? throw new ArgumentNullException(nameof(orders));
            _products = products ?? throw new ArgumentNullException(nameof(products));
            _assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
            _peripherals = peripherals ?? throw new ArgumentNullException(nameof(peripherals));
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _minutes = gameMinutes ?? throw new ArgumentNullException(nameof(gameMinutes));
            var supported = new HashSet<string>(StringComparer.Ordinal);
            foreach (var product in products) { var subject = Subject(product.FulfillmentItem); if (subject != null) supported.Add(subject); }
            _core.Community.SetPurchasablePromiseSubjects(supported);
            Rebaseline();
            _orders.Changed += OrdersChanged; _assembly.Changed += InstalledChanged;
            _peripherals.Changed += InstalledChanged; _stream.Changed += StreamChanged;
        }

        public void BeginRestore() => _restoring = true;
        public void EndRestore() { Rebaseline(); _restoring = false; }
        public void Dispose()
        { _orders.Changed -= OrdersChanged; _assembly.Changed -= InstalledChanged;
            _peripherals.Changed -= InstalledChanged; _stream.Changed -= StreamChanged; }

        public static string Subject(ItemDefinition item)
        {
            if (item == null) return null;
            if (item.PcComponent != null && item.PcComponent.ComponentType == PcComponentType.Gpu) return "gpu";
            return item.PeripheralKind == PcPeripheralKind.Microphone ? "microphone" : item.PeripheralKind == PcPeripheralKind.Webcam ? "webcam" : null;
        }

        private EventWitnesses Witnesses() => _stream.State == StreamState.Live ? EventWitnesses.Capture(_core.Roster) : EventWitnesses.Empty();
        private void OrdersChanged()
        {
            if (_restoring) return;
            foreach (var order in _orders.Orders)
            {
                if (!_knownOrders.Add(order.OrderId)) continue;
                foreach (var product in _products)
                {
                    if (product.ProductId != order.ProductId) continue;
                    string subject = Subject(product.FulfillmentItem);
                    if (subject != null) _core.ObserveGameplay(new PromiseGameplayFact(PromiseAction.Purchase, subject,
                        order.PlacedAt.TotalSeconds / 60d, order.OrderId, Witnesses()));
                    break;
                }
            }
        }
        private Dictionary<string, string> CurrentInstalled()
        {
            var current = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var component in _assembly.InstalledComponents)
                if (component.ComponentType == PcComponentType.Gpu) current[component.InstanceId] = "gpu";
            string mic = _peripherals.GetConnectedId(PcPeripheralKind.Microphone), cam = _peripherals.GetConnectedId(PcPeripheralKind.Webcam);
            if (mic.Length > 0) current[mic] = "microphone";
            if (cam.Length > 0) current[cam] = "webcam";
            return current;
        }
        private void InstalledChanged()
        {
            if (_restoring) return;
            var current = CurrentInstalled();
            foreach (var pair in current)
                if (!_installed.Contains(pair.Key)) _core.ObserveGameplay(new PromiseGameplayFact(PromiseAction.Install, pair.Value,
                    _minutes(), pair.Key, Witnesses()));
            _installed.Clear(); foreach (string id in current.Keys) _installed.Add(id);
        }
        private void StreamChanged()
        {
            if (_restoring || _stream.State != StreamState.Live || _lastBroadcast == _stream.BroadcastId) return;
            _lastBroadcast = _stream.BroadcastId;
            double now = _minutes();
            _core.ObserveGameplay(new PromiseGameplayFact(PromiseAction.StartBroadcast, "stream-start", now,
                _lastBroadcast, Witnesses(), now % 1440));
        }
        private void Rebaseline()
        {
            _knownOrders.Clear(); foreach (var order in _orders.Orders) _knownOrders.Add(order.OrderId);
            _installed.Clear(); foreach (string id in CurrentInstalled().Keys) _installed.Add(id);
            _lastBroadcast = _stream.State == StreamState.Live ? _stream.BroadcastId : null;
        }
    }
}
