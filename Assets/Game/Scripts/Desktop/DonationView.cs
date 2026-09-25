using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GoLive.Desktop
{
    public sealed class DonationView : DesktopAppView
    {
        [SerializeField] private TMP_InputField accountName;
        [SerializeField] private Toggle alerts;
        [SerializeField] private Button save;
        [SerializeField] private TMP_Text total;
        [SerializeField] private TMP_Text history;
        private bool _loaded;
        private int _restoreGeneration = -1;
        private void Awake() => save.onClick.AddListener(() => Result(State.Donation.Configure(accountName.text, alerts.isOn)));
        protected override void Refresh()
        {
            if (!_loaded || _restoreGeneration != State.RestoreGeneration)
            {
                accountName.SetTextWithoutNotify(State.Donation.Name);
                alerts.SetIsOnWithoutNotify(State.Donation.AlertsEnabled);
                _loaded = true;
                _restoreGeneration = State.RestoreGeneration;
            }
            total.text = F("desktop.donation.total", State.Donation.TotalCents / 100d);
            var builder = new StringBuilder();
            var receipts = State.Donation.History;
            for (int i = receipts.Count - 1; i >= Mathf.Max(0, receipts.Count - 7); i--)
                builder.AppendLine(F("desktop.donation.receipt", receipts[i].SenderName, receipts[i].AmountCents / 100d));
            history.text = receipts.Count == 0 ? T("desktop.donation.empty") : builder.ToString();
        }
    }
}
