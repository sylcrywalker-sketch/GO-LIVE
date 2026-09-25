using System;
using GoLive.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GoLive.Desktop
{
    public sealed class DesktopShellView : MonoBehaviour
    {
        [Serializable] private sealed class AppPresentation
        {
            public DesktopAppId appId;
            public Button shortcut;
            public Button startShortcut;
            public CanvasGroup shortcutAppearance;
            public GameObject window;
            public Button minimize;
            public Button close;
            public Button task;
            public Image titlebar;
            public Color activeTitleColor;
        }
        [SerializeField] private DesktopRuntimeBehaviour runtime;
        [SerializeField] private LocalizationContext localization;
        [SerializeField] private GameObject desktopRoot;
        [SerializeField] private GameObject startMenu;
        [SerializeField] private Button startButton;
        [SerializeField] private Button leave;
        [SerializeField] private Button shutdown;
        [SerializeField] private AppPresentation[] apps;
        [SerializeField] private TMP_Text toast;
        private bool _bound;
        private float _toastRemaining;

        private void Awake()
        {
            desktopRoot.SetActive(false);
            startMenu.SetActive(false);
            startButton.onClick.AddListener(() =>
            {
                startMenu.SetActive(!startMenu.activeSelf);
                if (startMenu.activeSelf) startMenu.transform.SetAsLastSibling();
            });
            leave.onClick.AddListener(() => runtime.Session.Back());
            shutdown.onClick.AddListener(() => { runtime.State.Windows.CloseAll(); runtime.Session.PowerOff(); });
            foreach (AppPresentation app in apps)
            {
                AppPresentation captured = app;
                app.shortcut.onClick.AddListener(() => Open(captured.appId));
                app.startShortcut.onClick.AddListener(() => Open(captured.appId));
                app.close.onClick.AddListener(() => runtime.State.Windows.Close(captured.appId));
                app.minimize.onClick.AddListener(() => runtime.State.Windows.Minimize(captured.appId));
                app.task.onClick.AddListener(() => runtime.State.Windows.Activate(captured.appId));
                app.window.SetActive(false);
            }
        }
        private void OnEnable()
        {
            runtime.Ready += Bind;
            if (runtime.IsReady) Bind();
        }
        private void Bind()
        {
            if (_bound) return;
            _bound = true;
            runtime.Session.Changed += Refresh;
            runtime.State.Windows.Changed += Refresh;
            runtime.State.Storage.Changed += Refresh;
            runtime.Feedback += ShowFeedback;
            Refresh();
        }
        private void OnDisable()
        {
            runtime.Ready -= Bind;
            if (!_bound) return;
            runtime.Session.Changed -= Refresh;
            runtime.State.Windows.Changed -= Refresh;
            runtime.State.Storage.Changed -= Refresh;
            runtime.Feedback -= ShowFeedback;
            desktopRoot.SetActive(false);
            _bound = false;
        }
        private void Open(DesktopAppId id)
        {
            startMenu.SetActive(false);
            if (!runtime.State.Storage.IsInstalled(id)) id = DesktopAppId.Hub;
            runtime.Open(id);
        }
        private void Refresh()
        {
            bool focused = runtime.Session.Usage == PcUsageState.Focused;
            desktopRoot.SetActive(focused);
            if (!focused) startMenu.SetActive(false);
            foreach (AppPresentation app in apps)
            {
                app.shortcutAppearance.alpha = runtime.State.Storage.IsInstalled(app.appId) ? 1f : .55f;
                app.startShortcut.gameObject.SetActive(runtime.State.Storage.IsInstalled(app.appId));
                app.window.SetActive(runtime.State.Windows.IsVisible(app.appId));
                app.task.gameObject.SetActive(runtime.State.Windows.IsOpen(app.appId));
                bool active=runtime.State.Windows.ActiveApp==app.appId;
                app.titlebar.color=active?app.activeTitleColor:Color.Lerp(app.activeTitleColor,new Color(.28f,.31f,.36f),.55f);
                app.task.targetGraphic.color=active?new Color(.12f,.37f,.62f):new Color(.07f,.22f,.38f);
            }
            foreach (DesktopAppId id in runtime.State.Windows.ActivationOrder)
                foreach (AppPresentation app in apps)
                    if (app.appId == id) { app.window.transform.SetAsLastSibling(); break; }
            foreach (DesktopWindow open in runtime.State.Windows.Windows)
                foreach (AppPresentation app in apps)
                    if (app.appId == open.AppId) { app.task.transform.SetAsLastSibling(); break; }
        }
        private void ShowFeedback(string key)
        {
            toast.text = localization.Text(key);
            _toastRemaining = 5;
            toast.gameObject.SetActive(true);
        }
        private void Update()
        {
            if (_toastRemaining <= 0) return;
            _toastRemaining -= Time.unscaledDeltaTime;
            if (_toastRemaining <= 0) toast.gameObject.SetActive(false);
        }
    }
}
