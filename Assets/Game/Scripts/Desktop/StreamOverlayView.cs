using System.Text;
using GoLive.Localization;
using TMPro;
using UnityEngine;

namespace GoLive.Desktop
{
    public sealed class StreamOverlayView : MonoBehaviour
    {
        [SerializeField] private DesktopRuntimeBehaviour runtime;
        [SerializeField] private LocalizationContext localization;
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text statistics;
        [SerializeField] private TMP_Text chat;
        [SerializeField] private TMP_Text donation;
        private bool _bound;
        private long _shownSecond = -1;
        private string _lastReceiptId;
        private DonationReceipt _alert;
        private float _alertRemaining;
        private void OnEnable()
        {
            runtime.Ready += Bind;
            panel.SetActive(false);
            if (runtime.IsReady) Bind();
        }
        private void Bind()
        {
            if (_bound) return;
            _bound = true;
            var receipts = runtime.State.Donation.History;
            _lastReceiptId = receipts.Count == 0 ? null : receipts[receipts.Count - 1].Id;
            runtime.State.Stream.Changed += Refresh;
            runtime.State.Stream.ChatAdded += ChatAdded;
            runtime.State.Donation.Changed += DonationChanged;
            localization.LanguageChanged += LanguageChanged;
            Refresh();
            RefreshChat();
        }
        private void OnDisable()
        {
            runtime.Ready -= Bind;
            if (!_bound) return;
            runtime.State.Stream.Changed -= Refresh;
            runtime.State.Stream.ChatAdded -= ChatAdded;
            runtime.State.Donation.Changed -= DonationChanged;
            localization.LanguageChanged -= LanguageChanged;
            _bound = false;
            panel.SetActive(false);
        }
        private void LanguageChanged(GameLanguage _) { Refresh(); RefreshChat(); ShowAlert(); }
        private void ChatAdded(StreamChatMessage _) => RefreshChat();
        private void Update()
        {
            if (!_bound || !panel.activeSelf) return;
            if (_alertRemaining > 0)
            {
                _alertRemaining -= Time.unscaledDeltaTime;
                if (_alertRemaining <= 0) { _alert = null; donation.text = ""; }
            }
            double seconds = runtime.State.Stream.DurationSeconds;
            long second = seconds >= long.MaxValue ? long.MaxValue : (long)seconds;
            if (second != _shownSecond) { _shownSecond = second; RefreshStatistics(); }
        }
        private void Refresh()
        {
            panel.SetActive(runtime.State.Stream.State == StreamState.Live);
            RefreshStatistics();
            if (!panel.activeSelf) { chat.text = ""; donation.text = ""; _shownSecond = -1; _alert = null; _alertRemaining = 0; }
        }
        private void RefreshStatistics()
        {
            StreamSession stream = runtime.State.Stream;
            double minutes = System.Math.Floor(stream.DurationSeconds / 60d);
            statistics.text = localization.Format("desktop.overlay.stats", stream.Viewers, minutes, (int)(stream.DurationSeconds % 60), stream.Followers, stream.DonationCents / 100d);
        }
        private void RefreshChat()
        {
            var messages = runtime.State.Stream.Chat;
            var builder = new StringBuilder();
            for (int i = Mathf.Max(0, messages.Count - 6); i < messages.Count; i++)
                builder.Append(messages[i].SenderName).Append("\n").Append(localization.Text(messages[i].BodyKey)).Append("\n\n");
            chat.text = builder.ToString();
        }
        private void DonationChanged()
        {
            var account = runtime.State.Donation;
            DonationReceipt latest = account.History.Count == 0 ? null : account.History[account.History.Count - 1];
            if (latest != null && latest.Id != _lastReceiptId)
            {
                _lastReceiptId = latest.Id;
                if (runtime.State.Stream.State == StreamState.Live && account.AlertsEnabled) { _alert = latest; _alertRemaining = 6; }
            }
            ShowAlert();
        }
        private void ShowAlert() => donation.text = _alert != null && runtime.State.Donation.AlertsEnabled
            ? localization.Format("desktop.donation.receipt", _alert.SenderName, _alert.AmountCents / 100d) : "";
    }
}
