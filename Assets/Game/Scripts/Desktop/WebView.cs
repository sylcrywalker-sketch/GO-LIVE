using TMPro;
using System.Collections.Generic;
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
        [SerializeField] private Button back;
        [SerializeField] private Button forward;
        private readonly List<string> _history = new() { "home.go" };
        private int _historyIndex;
        private string _pageKey = "desktop.web.home_body";
        private void Awake()
        {
            go.onClick.AddListener(Navigate);
            address.onSubmit.AddListener(_ => Navigate());
            home.onClick.AddListener(() => { address.SetTextWithoutNotify("home.go"); Navigate(); });
            back.onClick.AddListener(() => Travel(-1));
            forward.onClick.AddListener(() => Travel(1));
            hub.onClick.AddListener(() => runtime.Open(DesktopAppId.Hub));
            trich.onClick.AddListener(() => runtime.Open(State.Storage.IsInstalled(DesktopAppId.Trich) ? DesktopAppId.Trich : DesktopAppId.Hub));
        }
        private void Navigate()
        {
            string value = address.text.Trim().ToLowerInvariant();
            if (_history[_historyIndex] != value)
            {
                if (_historyIndex < _history.Count - 1) _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
                if (_history.Count == 32) _history.RemoveAt(0);
                _history.Add(value); _historyIndex = _history.Count - 1;
            }
            if (value == "hub.go") runtime.Open(DesktopAppId.Hub);
            else if (value == "trich.go") runtime.Open(State.Storage.IsInstalled(DesktopAppId.Trich) ? DesktopAppId.Trich : DesktopAppId.Hub);
            _pageKey = value == "home.go" || value == "hub.go" || value == "trich.go" ? "desktop.web.home_body" : "desktop.web.unavailable";
            Refresh();
        }
        private void Travel(int direction)
        {
            int next = _historyIndex + direction;
            if (next < 0 || next >= _history.Count) return;
            _historyIndex = next;
            string value = _history[next];
            address.SetTextWithoutNotify(value);
            _pageKey = value == "home.go" || value == "hub.go" || value == "trich.go" ? "desktop.web.home_body" : "desktop.web.unavailable";
            Refresh();
        }
        protected override void Refresh()
        {
            page.text = T(_pageKey);
            back.interactable = _historyIndex > 0;
            forward.interactable = _historyIndex < _history.Count - 1;
        }
    }
}
