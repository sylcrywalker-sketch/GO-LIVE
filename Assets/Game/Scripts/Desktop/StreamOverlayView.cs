using System;
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
        [SerializeField] private TMP_Text subscribers;
        [SerializeField] private TMP_Text duration;
        [SerializeField] private TMP_Text chatViewers;
        [SerializeField] private TMP_Text chat;
        [SerializeField] private TMP_Text donation;
        [SerializeField] private GameObject donationPanel;
        private bool _bound;
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
            if (_bound)
            {
                runtime.State.Stream.Changed -= Refresh;
                runtime.State.Stream.ChatAdded -= ChatAdded;
                runtime.State.Donation.Changed -= DonationChanged;
                localization.LanguageChanged -= LanguageChanged;
                _bound = false;
            }
            panel.SetActive(false);
            ClearAlert();
        }

        private void LanguageChanged(GameLanguage _) { Refresh(); RefreshChat(); ShowAlert(); }
        private void ChatAdded(StreamChatMessage _) => RefreshChat();

        // Only the six-second visual alert needs a frame timer; counters refresh from Stream.Changed.
        private void Update()
        {
            if (!_bound || _alert == null) return;
            _alertRemaining -= Time.unscaledDeltaTime;
            if (_alertRemaining <= 0) ClearAlert();
        }

        private void Refresh()
        {
            bool live = runtime.State.Stream.State == StreamState.Live;
            panel.SetActive(live);
            if (!live)
            {
                chat.text = "";
                ClearAlert();
                return;
            }
            RefreshStatistics();
        }

        private void RefreshStatistics()
        {
            StreamSession stream = runtime.State.Stream;
            AudienceSimulation audience = stream.Audience;
            long previous = runtime.State.Trich.TotalFollowers;
            long followers = previous > long.MaxValue - audience.Follows ? long.MaxValue : previous + audience.Follows;
            statistics.text = localization.Format("desktop.overlay.count", audience.CurrentViewers);
            subscribers.text = localization.Format("desktop.overlay.count", followers);
            duration.text = localization.Format("desktop.overlay.duration", Math.Floor(stream.DurationSeconds / 3600d),
                (int)(stream.DurationSeconds / 60d % 60), (int)(stream.DurationSeconds % 60d));
            chatViewers.text = statistics.text;
        }

        private void RefreshChat()
        {
            var messages = runtime.State.Stream.Chat;
            var builder = new StringBuilder();
            for (int i = Mathf.Max(0, messages.Count - 8); i < messages.Count; i++)
            {
                string name = messages[i].SenderName.Replace("<", "").Replace(">", "");
                builder.Append("<color=#").Append(NameColor(name)).Append("><b>")
                    .Append(name).Append("</b></color>  ")
                    .Append(localization.Text(messages[i].BodyKey)).Append('\n');
            }
            chat.text = builder.ToString();
        }

        private static string NameColor(string name)
        {
            uint hash = 0;
            for (int i = 0; i < name.Length; i++) hash = unchecked(hash * 31 + name[i]);
            return (hash % 4) switch
            {
                0 => "A995DC", 1 => "71C9CF", 2 => "E2B869", _ => "CE8FBB"
            };
        }

        private void DonationChanged()
        {
            DonationAccount account = runtime.State.Donation;
            DonationReceipt latest = account.History.Count == 0 ? null : account.History[account.History.Count - 1];
            if (latest != null && latest.Id != _lastReceiptId)
            {
                _lastReceiptId = latest.Id;
                if (runtime.State.Stream.State == StreamState.Live && account.AlertsEnabled)
                {
                    _alert = latest;
                    _alertRemaining = 6;
                }
            }
            ShowAlert();
        }

        private void ClearAlert()
        {
            _alert = null;
            _alertRemaining = 0;
            donation.text = "";
            donationPanel.SetActive(false);
        }

        private void ShowAlert()
        {
            bool show = _alert != null && runtime.State.Donation.AlertsEnabled
                && runtime.State.Stream.State == StreamState.Live;
            donationPanel.SetActive(show);
            donation.text = show
                ? localization.Format("desktop.donation.receipt", _alert.SenderName, _alert.AmountCents / 100d)
                : "";
        }
    }
}
