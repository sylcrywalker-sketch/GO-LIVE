using GoLive.Localization;
using TMPro;
using UnityEngine;

namespace GoLive.Desktop
{
    // Shared view lifecycle only: subscriptions belong to visible windows and never accumulate.
    public abstract class DesktopAppView : MonoBehaviour
    {
        [SerializeField] protected DesktopRuntimeBehaviour runtime;
        [SerializeField] protected LocalizationContext localization;
        [SerializeField] protected TMP_Text feedback;
        protected DesktopState State => runtime.State;
        private bool _bound;
        private string _feedbackKey;
        private int _restoreGeneration = -1;
        protected virtual void OnEnable()
        {
            runtime.Ready += Bind;
            if (runtime.IsReady) Bind();
        }
        protected virtual void OnDisable()
        {
            runtime.Ready -= Bind;
            if (!_bound) return;
            State.Storage.Changed -= Refresh;
            State.Outline.Changed -= Refresh;
            State.Trich.Changed -= Refresh;
            State.Donation.Changed -= Refresh;
            State.Stream.Changed -= Refresh;
            localization.LanguageChanged -= LanguageChanged;
            _bound = false;
        }
        private void Bind()
        {
            if (_bound) return;
            _bound = true;
            State.Storage.Changed += Refresh;
            State.Outline.Changed += Refresh;
            State.Trich.Changed += Refresh;
            State.Donation.Changed += Refresh;
            State.Stream.Changed += Refresh;
            localization.LanguageChanged += LanguageChanged;
            if (_restoreGeneration != State.RestoreGeneration)
            {
                _feedbackKey = null;
                _restoreGeneration = State.RestoreGeneration;
            }
            RefreshFeedback();
            Refresh();
        }
        private void LanguageChanged(GameLanguage _) { RefreshFeedback(); Refresh(); }
        protected string T(string key) => localization.Text(key);
        protected string F(string key, params object[] arguments) => localization.Format(key, arguments);
        protected void Result(string error, string successKey = "desktop.saved")
        {
            _feedbackKey = error ?? successKey;
            if (feedback == null) return;
            RefreshFeedback();
            feedback.color = error == null ? new Color(.20f,.39f,.33f) : new Color(.52f,.23f,.15f);
        }
        private void RefreshFeedback()
        {
            if (feedback != null) feedback.text = _feedbackKey == null ? "" : T(_feedbackKey);
        }
        protected abstract void Refresh();
    }
}
