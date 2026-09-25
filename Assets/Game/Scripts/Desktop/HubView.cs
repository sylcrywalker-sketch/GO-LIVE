using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GoLive.Desktop
{
    public sealed class HubView : DesktopAppView
    {
        [Serializable] private sealed class AppCard
        {
            public DesktopAppId appId;
            public Button install;
            public TMP_Text state;
            public GameObject root;
        }
        [SerializeField] private AppCard[] cards;
        [SerializeField] private TMP_InputField search;
        [SerializeField] private GameObject noResults;
        private void Awake()
        {
            search.onValueChanged.AddListener(_ => Refresh());
            foreach (AppCard card in cards)
            {
                AppCard captured = card;
                captured.install.onClick.AddListener(() => Install(captured.appId));
            }
        }
        private void Install(DesktopAppId id)
        {
            if (State.Storage.IsInstalled(id)) runtime.Open(id);
            else Result(State.Storage.TryInstall(id), "desktop.installed");
        }
        protected override void Refresh()
        {
            int visible = 0;
            foreach (AppCard card in cards)
            {
                card.state.text = T(State.Storage.IsInstalled(card.appId) ? "desktop.open" : "desktop.install");
                string name = "";
                foreach (var app in runtime.Catalog.Apps) if (app.Id == card.appId) { name = T(app.NameKey); break; }
                bool matches = name.IndexOf(search.text.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
                card.root.SetActive(matches);
                if (matches) visible++;
            }
            noResults.SetActive(visible == 0);
        }
    }
}
