using TMPro;
using System.Globalization;
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
        [SerializeField] private GameObject overviewPanel;
        [SerializeField] private GameObject editPanel;
        [SerializeField] private Button editProfile;
        [SerializeField] private Button backToProfile;
        [SerializeField] private TMP_Text profileName;
        [SerializeField] private TMP_Text profileDescription;
        [SerializeField] private ChannelAvatarGraphic profileAvatar;
        [SerializeField] private TMP_Text subscribers;
        [SerializeField] private TMP_Text peakViewers;
        [SerializeField] private TMP_Text hoursStreamed;
        private int _avatar;
        private bool _editing;
        private bool _profileLoaded;
        private string _loadedCode;
        private int _restoreGeneration = -1;
        private void Awake()
        {
            register.onClick.AddListener(() => ShowResult(State.Trich.Register(State.Outline, email.text)));
            openOutline.onClick.AddListener(() => runtime.Open(DesktopAppId.Outline));
            save.onClick.AddListener(SaveProfile);
            copy.onClick.AddListener(() => { GUIUtility.systemCopyBuffer = State.Trich.ChannelCode; ShowResult(null,"desktop.copied"); });
            if (editProfile != null) editProfile.onClick.AddListener(() => SetEditing(true));
            if (backToProfile != null) backToProfile.onClick.AddListener(() => SetEditing(false));
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
                _editing = false;
                if (!email.isFocused && string.IsNullOrEmpty(email.text)) email.SetTextWithoutNotify(State.Outline.Address);
                return;
            }
            if (!_profileLoaded || _loadedCode != State.Trich.ChannelCode || _restoreGeneration != State.RestoreGeneration)
            {
                _editing = false;
                channelName.SetTextWithoutNotify(State.Trich.Name);
                description.SetTextWithoutNotify(State.Trich.Description);
                _avatar = State.Trich.AvatarId;
                _profileLoaded = true;
                _loadedCode = State.Trich.ChannelCode;
                _restoreGeneration = State.RestoreGeneration;
            }
            code.text = State.Trich.ChannelCode;
            if (statistics != null)
                statistics.text = F("desktop.trich.statistics", State.Trich.CompletedStreams, State.Trich.TotalFollowers,
                    State.Trich.PeakViewers, State.Trich.TotalDurationSeconds / 60d, State.Trich.TotalDonationCents / 100d);
            if (profileName != null) profileName.text = State.Trich.Name;
            if (profileDescription != null) profileDescription.text = string.IsNullOrWhiteSpace(State.Trich.Description)
                ? T("desktop.trich.description_empty") : State.Trich.Description;
            if (profileAvatar != null) profileAvatar.SetPortrait(State.Trich.AvatarId);
            if (subscribers != null) subscribers.text = State.Trich.TotalFollowers.ToString(CultureInfo.InvariantCulture);
            if (peakViewers != null) peakViewers.text = State.Trich.PeakViewers.ToString(CultureInfo.InvariantCulture);
            if (hoursStreamed != null) hoursStreamed.text = F("desktop.trich.hours_value", State.Trich.TotalDurationSeconds / 3600d);
            ShowPanels();
            ShowSelection();
        }
        private void SaveProfile()
        {
            string error = State.Trich.EditProfile(channelName.text, description.text, _avatar);
            ShowResult(error);
            if (error == null) SetEditing(false);
        }
        private void SetEditing(bool editing)
        {
            _editing = editing;
            ShowPanels();
        }
        private void ShowPanels()
        {
            if (overviewPanel != null) overviewPanel.SetActive(!_editing);
            if (editPanel != null) editPanel.SetActive(_editing);
        }
        private void ShowResult(string error, string successKey = "desktop.saved")
        {
            Result(error, successKey);
            if (feedback != null) feedback.color = error == null
                ? new Color(.60f, .82f, .70f) : new Color(.96f, .58f, .47f);
        }
        private void ShowSelection()
        {
            for (int i = 0; i < avatarFrames.Length; i++) avatarFrames[i].color = i == _avatar
                ? new Color(.44f, .31f, .65f) : new Color(.16f, .18f, .23f);
        }
    }
}
