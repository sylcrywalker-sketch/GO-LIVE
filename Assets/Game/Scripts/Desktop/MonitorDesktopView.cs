using GoLive.Localization;
using TMPro;
using UnityEngine;

namespace GoLive.Desktop
{
    // A legible physical-screen projection of the same window/account/storage state as the full desktop.
    public sealed class MonitorDesktopView : MonoBehaviour
    {
        [SerializeField] private DesktopRuntimeBehaviour runtime;
        [SerializeField] private LocalizationContext localization;
        [SerializeField] private CanvasGroup[] icons;
        [SerializeField] private GameObject window;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text content;
        private bool _bound;
        private void OnEnable() { runtime.Ready += Bind; if(runtime.IsReady) Bind(); }
        private void Bind()
        {
            if(_bound) return;
            _bound=true;
            runtime.State.Windows.Changed+=Refresh;runtime.State.Storage.Changed+=Refresh;
            runtime.State.Outline.Changed+=Refresh;runtime.State.Trich.Changed+=Refresh;
            runtime.State.Stream.Changed+=Refresh;runtime.State.Donation.Changed+=Refresh;
            localization.LanguageChanged+=LanguageChanged;Refresh();
        }
        private void OnDisable()
        {
            runtime.Ready-=Bind;if(!_bound)return;
            runtime.State.Windows.Changed-=Refresh;runtime.State.Storage.Changed-=Refresh;
            runtime.State.Outline.Changed-=Refresh;runtime.State.Trich.Changed-=Refresh;
            runtime.State.Stream.Changed-=Refresh;runtime.State.Donation.Changed-=Refresh;
            localization.LanguageChanged-=LanguageChanged;_bound=false;
        }
        private void LanguageChanged(GameLanguage _) => Refresh();
        private void Refresh()
        {
            DesktopState state=runtime.State;
            for(int i=0;i<icons.Length;i++) icons[i].alpha=state.Storage.IsInstalled(runtime.Catalog.Apps[i].Id)?1:.4f;
            DesktopAppId? active=state.Windows.ActiveApp;
            window.SetActive(active.HasValue);if(!active.HasValue)return;
            foreach(var app in runtime.Catalog.Apps) if(app.Id==active.Value) {title.text=localization.Text(app.NameKey);content.text=localization.Text(app.DescriptionKey);break;}
            switch(active.Value)
            {
                case DesktopAppId.Outline: content.text=state.Outline.IsCreated?state.Outline.Address+"\n\n"+localization.Text("desktop.outline.inbox")+" · "+state.Outline.Messages.Count:localization.Text("desktop.outline.create_title");break;
                case DesktopAppId.Trich: content.text=state.Trich.IsRegistered?state.Trich.Name+"\n\n"+state.Trich.Description:localization.Text("desktop.trich.join");break;
                case DesktopAppId.Streamly: content.text=state.Trich.Name+"\n\n"+localization.Text("desktop.stream.state."+state.Stream.State.ToString().ToLowerInvariant());break;
                case DesktopAppId.Donation: content.text=localization.Format("desktop.donation.total",state.Donation.TotalCents/100d);break;
                case DesktopAppId.MyComputer:
                    content.text=state.Storage.Drives.Count==0?localization.Text("desktop.disk.empty"):"";
                    foreach(var drive in state.Storage.Drives) content.text+=localization.Format("desktop.disk.name",drive.Letter)+"\n"+localization.Format("desktop.disk.capacity",state.Storage.GetFreeMiB(drive.DriveId)/1024f,drive.CapacityMiB/1024f)+"\n\n";
                    break;
            }
        }
    }
}
