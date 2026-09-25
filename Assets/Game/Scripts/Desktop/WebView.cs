using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GoLive.Desktop
{
    public sealed class WebView : DesktopAppView
    {
        [SerializeField] private TMP_InputField address;
        [SerializeField] private Button go;
        [SerializeField] private Button home;
        [SerializeField] private Button hub;
        [SerializeField] private Button trich;
        [SerializeField] private TMP_Text page;
        private string _pageKey = "desktop.web.home_body";
        private void Awake()
        {
            go.onClick.AddListener(Navigate);
            address.onSubmit.AddListener(_ => Navigate());
            home.onClick.AddListener(() => { address.SetTextWithoutNotify("home.go"); _pageKey = "desktop.web.home_body"; Refresh(); });
            hub.onClick.AddListener(() => runtime.Open(DesktopAppId.Hub));
            trich.onClick.AddListener(() => runtime.Open(State.Storage.IsInstalled(DesktopAppId.Trich) ? DesktopAppId.Trich : DesktopAppId.Hub));
        }
        private void Navigate()
        {
            string value = address.text.Trim().ToLowerInvariant();
            if (value == "hub.go") runtime.Open(DesktopAppId.Hub);
            else if (value == "trich.go") runtime.Open(State.Storage.IsInstalled(DesktopAppId.Trich) ? DesktopAppId.Trich : DesktopAppId.Hub);
            _pageKey = value == "home.go" || value == "hub.go" || value == "trich.go" ? "desktop.web.home_body" : "desktop.web.unavailable";
            Refresh();
        }
        protected override void Refresh() => page.text = T(_pageKey);
    }
}
