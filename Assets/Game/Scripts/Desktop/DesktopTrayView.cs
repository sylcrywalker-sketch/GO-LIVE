using GoLive.GameTime;
using GoLive.Localization;
using TMPro;
using UnityEngine;

namespace GoLive.Desktop
{
    public sealed class DesktopTrayView : MonoBehaviour
    {
        [SerializeField] private GameClockBehaviour clock;
        [SerializeField] private LocalizationContext localization;
        [SerializeField] private TMP_Text time;
        [SerializeField] private TMP_Text language;
        private bool _bound;
        private void OnEnable() { if(clock.Clock!=null) Bind(); }
        private void Start() => Bind();
        private void Bind()
        {
            if(_bound||!isActiveAndEnabled) return;
            _bound=true;clock.Clock.MinuteChanged+=Render;localization.LanguageChanged+=Language;
            Render(clock.Clock.Current);Language(localization.CurrentLanguage);
        }
        private void OnDisable()
        {
            if(!_bound)return;clock.Clock.MinuteChanged-=Render;localization.LanguageChanged-=Language;_bound=false;
        }
        private void Render(GameTimeSnapshot value) => time.text=$"{value.Hour:00}:{value.Minute:00}\n"+localization.Format("desktop.tray.day",value.Day);
        private void Language(GameLanguage value) { language.text=value==GameLanguage.Russian?"RU":"EN"; Render(clock.Clock.Current); }
    }
}
