using GoLive.Localization;
using TMPro;
using UnityEngine;

namespace GoLive.Desktop
{
    public sealed class MonitorSurfaceView : MonoBehaviour
    {
        [SerializeField] private PcSessionBehaviour session;
        [SerializeField] private LocalizationContext localization;
        [SerializeField] private GameObject splash;
        [SerializeField] private GameObject desktop;
        [SerializeField] private TMP_Text seatedHint;
        private void OnEnable()
        {
            session.Session.Changed += Refresh;
            localization.LanguageChanged += LanguageChanged;
            Refresh();
        }
        private void OnDisable()
        {
            session.Session.Changed -= Refresh;
            localization.LanguageChanged -= LanguageChanged;
        }
        private void LanguageChanged(GameLanguage _) => Refresh();
        private void Refresh()
        {
            PcSession state = session.Session;
            splash.SetActive(state.MonitorOn && state.Power == PcPowerState.Booting);
            desktop.SetActive(state.ScreenActive);
            seatedHint.gameObject.SetActive(state.Usage == PcUsageState.Seated);
            seatedHint.text = localization.Text(state.ScreenActive ? "pc.session.focus_hint" : "pc.session.off_hint");
        }
    }
}
