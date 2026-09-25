using System.Collections.Generic;
using System.Text;
using GoLive.Localization;
using GoLive.PcBuilding;
using TMPro;
using UnityEngine;

namespace GoLive.Desktop
{
    public sealed class PcSessionFeedbackView : MonoBehaviour
    {
        [SerializeField] private PcSessionBehaviour session;
        [SerializeField] private LocalizationContext localization;
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text message;
        private float _remaining;
        private void OnEnable()
        {
            session.PowerOnRejected += Rejected;
            panel.SetActive(false);
        }
        private void OnDisable()
        {
            session.PowerOnRejected -= Rejected;
            panel.SetActive(false);
        }
        private void Rejected(IReadOnlyList<PcDiagnostic> diagnostics)
        {
            var text = new StringBuilder(localization.Text("pc.status.wont_boot"));
            foreach (PcDiagnostic diagnostic in diagnostics)
                if (diagnostic.Severity == PcDiagnosticSeverity.Blocker)
                    text.Append('\n').Append(localization.Text(diagnostic.TitleKey)).Append(": ")
                        .Append(localization.Format(diagnostic.DetailKey, diagnostic.RequiredWatts, diagnostic.AvailableWatts));
            Show(text.ToString());
        }
        private void Show(string value)
        {
            message.text = value;
            float height = message.GetPreferredValues(value, message.rectTransform.rect.width, float.PositiveInfinity).y;
            message.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            ((RectTransform)panel.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height + 40);
            panel.SetActive(true);
            _remaining = 5;
        }
        private void Update()
        {
            if (_remaining <= 0) return;
            _remaining -= Time.unscaledDeltaTime;
            if (_remaining <= 0) panel.SetActive(false);
        }
    }
}
