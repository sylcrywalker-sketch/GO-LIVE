using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GoLive.Desktop
{
    public sealed class TrichView : DesktopAppView
    {
        [SerializeField] private GameObject registerPanel;
        [SerializeField] private GameObject profilePanel;
        [SerializeField] private TMP_InputField email;
        [SerializeField] private Button register;
        [SerializeField] private Button openOutline;
        [SerializeField] private TMP_InputField channelName;
        [SerializeField] private TMP_InputField description;
        [SerializeField] private Button save;
        [SerializeField] private Button copy;
        [SerializeField] private TMP_Text code;
        [SerializeField] private TMP_Text statistics;
        [SerializeField] private Button[] avatars;
        [SerializeField] private Image[] avatarFrames;
        private int _avatar;
        private bool _profileLoaded;
        private string _loadedCode;
        private int _restoreGeneration = -1;
        private void Awake()
        {
            register.onClick.AddListener(() => Result(State.Trich.Register(State.Outline, email.text)));
            openOutline.onClick.AddListener(() => runtime.Open(DesktopAppId.Outline));
            save.onClick.AddListener(() => Result(State.Trich.EditProfile(channelName.text, description.text, _avatar)));
            copy.onClick.AddListener(() => { GUIUtility.systemCopyBuffer = State.Trich.ChannelCode; Result(null,"desktop.copied"); });
            for (int i = 0; i < avatars.Length; i++) { int id = i; avatars[i].onClick.AddListener(() => { _avatar = id; ShowSelection(); }); }
        }
        protected override void Refresh()
        {
            bool registered = State.Trich.IsRegistered;
            registerPanel.SetActive(!registered);
            profilePanel.SetActive(registered);
            if (!registered)
            {
                _profileLoaded = false;
                if (!email.isFocused && string.IsNullOrEmpty(email.text)) email.SetTextWithoutNotify(State.Outline.Address);
                return;
            }
            if (!_profileLoaded || _loadedCode != State.Trich.ChannelCode || _restoreGeneration != State.RestoreGeneration)
            {
                channelName.SetTextWithoutNotify(State.Trich.Name);
                description.SetTextWithoutNotify(State.Trich.Description);
                _avatar = State.Trich.AvatarId;
                _profileLoaded = true;
                _loadedCode = State.Trich.ChannelCode;
                _restoreGeneration = State.RestoreGeneration;
            }
            code.text = State.Trich.ChannelCode;
            statistics.text = F("desktop.trich.statistics", State.Trich.CompletedStreams, State.Trich.TotalFollowers,
                State.Trich.PeakViewers, State.Trich.TotalDurationSeconds / 60d, State.Trich.TotalDonationCents / 100d);
            ShowSelection();
        }
        private void ShowSelection()
        {
            for (int i = 0; i < avatarFrames.Length; i++) avatarFrames[i].color = i == _avatar ? new Color(.38f, .27f, .60f) : new Color(.78f, .82f, .85f);
        }
    }
}
