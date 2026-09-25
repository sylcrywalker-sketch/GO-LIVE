using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GoLive.Desktop
{
    public sealed class DonationView : DesktopAppView
    {
        [Serializable]
        private sealed class ReceiptRow
        {
            public GameObject root;
            public TMP_Text username;
            public TMP_Text amount;
        }

        [SerializeField] private TMP_InputField accountName;
        [SerializeField] private Toggle alerts;
        [SerializeField] private Button save;
        [SerializeField] private TMP_Text total;
        [SerializeField] private TMP_Text history;
        [SerializeField] private TMP_Text serviceStatus;
        [SerializeField] private TMP_Text alertStatus;
        [SerializeField] private ReceiptRow[] receipts;
        private bool _loaded;
        private int _restoreGeneration = -1;

        private void Awake() => save.onClick.AddListener(() =>
        {
            string error = State.Donation.Configure(accountName.text, alerts.isOn);
            Result(error);
            feedback.color = error == null ? new Color(.50f, .79f, .62f) : new Color(.94f, .72f, .42f);
        });

        protected override void Refresh()
        {
            DonationAccount account = State.Donation;
            if (!_loaded || _restoreGeneration != State.RestoreGeneration)
            {
                accountName.SetTextWithoutNotify(account.Name);
                alerts.SetIsOnWithoutNotify(account.AlertsEnabled);
                _loaded = true;
                _restoreGeneration = State.RestoreGeneration;
            }
            serviceStatus.text = T(account.Name.Length == 0 ? "desktop.donation.status_setup" : "desktop.donation.status_ready");
            alertStatus.text = T(account.AlertsEnabled ? "desktop.donation.alerts_on" : "desktop.donation.alerts_off");
            total.text = F("desktop.donation.amount", account.TotalCents / 100d);
            history.text = account.History.Count == 0 ? T("desktop.donation.empty") : "";
            for (int i = 0; i < receipts.Length; i++)
            {
                int index = account.History.Count - 1 - i;
                receipts[i].root.SetActive(index >= 0);
                if (index < 0) continue;
                DonationReceipt receipt = account.History[index];
                receipts[i].username.text = receipt.SenderName;
                receipts[i].amount.text = F("desktop.donation.amount", receipt.AmountCents / 100d);
            }
        }
    }
}
