using System;
using System.Collections.Generic;

namespace GoLive.Desktop
{
    public sealed class DonationReceipt
    {
        public string Id { get; }
        public string SenderName { get; }
        public long AmountCents { get; }
        internal DonationReceipt(string id, string senderName, long amountCents) { Id = id; SenderName = senderName; AmountCents = amountCents; }
    }

    public sealed class DonationAccount
    {
        public const int MaximumHistory = 100;
        public const int MaximumReceivedIds = 4096;
        public string Name { get; private set; } = "";
        public bool AlertsEnabled { get; private set; } = true;
        public long TotalCents { get; private set; }
        public IReadOnlyList<DonationReceipt> History { get; private set; }
        public int RemainingReceiptCapacity => MaximumReceivedIds - _receivedIds.Count;
        public event Action Changed;
        // Raised exactly once per newly accepted receipt (never for a repeated id or restored history).
        public event Action<DonationReceipt> Received;
        private List<DonationReceipt> _history = new();
        private List<string> _receivedIds = new();
        private HashSet<string> _received = new(StringComparer.Ordinal);

        public DonationAccount() => History = _history.AsReadOnly();

        public string Configure(string name, bool alertsEnabled)
        {
            string normalized = name?.Trim();
            if (!DesktopAccountValidation.Text(normalized, 1, 32)) return "desktop.donation.invalid_name";
            if (Name == normalized && AlertsEnabled == alertsEnabled) return null;
            Name = normalized;
            AlertsEnabled = alertsEnabled;
            Changed?.Invoke();
            return null;
        }

        public string Receive(string id, string senderName, long amountCents)
        {
            if (!DesktopAccountValidation.Id(id)) return "desktop.donation.invalid_receipt";
            if (_received.Contains(id)) return null;
            if (!DesktopAccountValidation.Text(senderName, 1, 32) || amountCents <= 0) return "desktop.donation.invalid_receipt";
            if (TotalCents > long.MaxValue - amountCents) return "desktop.donation.total_limit";
            if (_receivedIds.Count == MaximumReceivedIds) return "desktop.donation.history_full";
            _received.Add(id);
            _receivedIds.Add(id);
            var receipt = new DonationReceipt(id, senderName, amountCents);
            if (_history.Count == MaximumHistory) _history.RemoveAt(0);
            _history.Add(receipt);
            TotalCents += amountCents;
            Received?.Invoke(receipt);
            Changed?.Invoke();
            return null;
        }

        public DonationSnapshot Capture()
        {
            var snapshot = new DonationSnapshot
            {
                Name = Name, AlertsEnabled = AlertsEnabled, TotalCents = TotalCents,
                ReceivedIds = _receivedIds.ToArray(), History = new DonationReceiptSnapshot[_history.Count]
            };
            for (int i = 0; i < _history.Count; i++)
            {
                DonationReceipt receipt = _history[i];
                snapshot.History[i] = new DonationReceiptSnapshot { Id = receipt.Id, SenderName = receipt.SenderName, AmountCents = receipt.AmountCents };
            }
            return snapshot;
        }

        public static bool Validate(DonationSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Version != 1 || !DesktopAccountValidation.Text(snapshot.Name, 0, 32) || snapshot.TotalCents < 0 ||
                snapshot.History == null || !DesktopAccountValidation.Ledger(snapshot.ReceivedIds, MaximumReceivedIds) ||
                snapshot.History.Length != Math.Min(snapshot.ReceivedIds.Length, MaximumHistory)) return false;
            int offset = snapshot.ReceivedIds.Length - snapshot.History.Length;
            long retainedTotal = 0;
            for (int i = 0; i < snapshot.History.Length; i++)
            {
                DonationReceiptSnapshot receipt = snapshot.History[i];
                if (receipt == null || receipt.Id != snapshot.ReceivedIds[offset + i] || !DesktopAccountValidation.Text(receipt.SenderName, 1, 32) ||
                    receipt.AmountCents <= 0 || retainedTotal > long.MaxValue - receipt.AmountCents) return false;
                retainedTotal += receipt.AmountCents;
            }
            return snapshot.TotalCents >= retainedTotal && (offset > 0 || snapshot.TotalCents == retainedTotal);
        }

        public void Restore(DonationSnapshot snapshot)
        {
            if (!Validate(snapshot)) throw new ArgumentException("Invalid Donation snapshot.", nameof(snapshot));
            var history = new List<DonationReceipt>(snapshot.History.Length);
            foreach (DonationReceiptSnapshot receipt in snapshot.History)
                history.Add(new DonationReceipt(receipt.Id, receipt.SenderName, receipt.AmountCents));
            var ids = new List<string>(snapshot.ReceivedIds);
            var received = new HashSet<string>(ids, StringComparer.Ordinal);
            Name = snapshot.Name;
            AlertsEnabled = snapshot.AlertsEnabled;
            TotalCents = snapshot.TotalCents;
            _history = history;
            History = _history.AsReadOnly();
            _receivedIds = ids;
            _received = received;
            Changed?.Invoke();
        }
    }
}
