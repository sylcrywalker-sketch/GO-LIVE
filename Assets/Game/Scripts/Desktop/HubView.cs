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
        }
        [SerializeField] private AppCard[] cards;
        private void Awake()
        {
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
            foreach (AppCard card in cards)
                card.state.text = T(State.Storage.IsInstalled(card.appId) ? "desktop.open" : "desktop.install");
        }
    }
}
