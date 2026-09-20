using GoLive.GameTime;
using GoLive.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GoLive.Phone
{
    [DisallowMultipleComponent]
    public sealed class PhoneScreenView : MonoBehaviour
    {
        [Header("Existing engine and scene bindings")]
        [SerializeField] private PhoneBehaviour _phone;
        [SerializeField] private LocalizationContext _localization;
        [SerializeField] private GameClockBehaviour _clock;

        [Header("Shared and Home")]
        [SerializeField] private TMP_Text _header;
        [SerializeField] private TMP_Text _homeClock;
        [SerializeField] private TMP_Text _shopLabel;
        [SerializeField] private TMP_Text _messagesLabel;
        [SerializeField] private TMP_Text _unreadBadge;
        [SerializeField] private Button _back;
        [SerializeField] private TMP_Text _backLabel;

        [Header("Messages visual sample")]
        [SerializeField] private Button _contact;
        [SerializeField] private Image _avatar;
        [SerializeField] private Sprite _landlordAvatar;
        [SerializeField, Min(0)] private int _previewUnreadCount = 1;
        [SerializeField] private TMP_Text _contactText;
        [SerializeField] private ScrollRect _conversation;
        [SerializeField] private TMP_Text[] _bubbles;

        [Header("Shop visual sample")]
        [SerializeField] private TMP_Text _productText;
        [SerializeField] private Button _buy;
        [SerializeField] private TMP_Text _buyLabel;
        [SerializeField] private bool _previewProductAvailable = true;

        public bool IsConversationVisible => _conversationVisible;
        private bool _conversationVisible;
        private bool _previewPressed;
        private bool _bound;
        private GameClock _subscribedClock;
        private GameTimeSnapshot _time;

        private void OnEnable()
        {
            if (_phone == null || _localization == null || _clock == null)
            {
                Debug.LogError($"{nameof(PhoneScreenView)} requires its Phone, Localization and GameClock references.", this);
                return;
            }

            _phone.ScreenChanged += HandleScreenChanged;
            _phone.ScreenBackRequested += TryBack;
            _localization.LanguageChanged += HandleLanguageChanged;
            _contact.onClick.AddListener(OpenConversation);
            _buy.onClick.AddListener(PreviewBuy);
            _subscribedClock = _clock.Clock;
            if (_subscribedClock != null)
            {
                _time = _subscribedClock.Current;
                _subscribedClock.MinuteChanged += HandleMinuteChanged;
            }
            _bound = true;
            HandleScreenChanged();
        }

        private void OnDisable()
        {
            if (!_bound)
                return;

            _phone.ScreenChanged -= HandleScreenChanged;
            _phone.ScreenBackRequested -= TryBack;
            _localization.LanguageChanged -= HandleLanguageChanged;
            _contact.onClick.RemoveListener(OpenConversation);
            _buy.onClick.RemoveListener(PreviewBuy);
            if (_subscribedClock != null)
                _subscribedClock.MinuteChanged -= HandleMinuteChanged;
            _subscribedClock = null;
            _bound = false;
            _conversationVisible = false;
            _previewPressed = false;
        }

        public void SetUnreadPreview(int count)
        {
            _previewUnreadCount = Mathf.Max(0, count);
            if (_bound)
                Refresh();
        }

        public void SetProductAvailablePreview(bool available)
        {
            _previewProductAvailable = available;
            _previewPressed = false;
            if (_bound)
                Refresh();
        }

        private void HandleScreenChanged()
        {
            _conversationVisible = false;
            _previewPressed = false;
            Refresh();
        }

        private void HandleLanguageChanged(GameLanguage language) => Refresh();

        private void HandleMinuteChanged(GameTimeSnapshot time)
        {
            _time = time;
            RefreshClock();
        }

        private void OpenConversation()
        {
            if (!_phone.IsInteractive || _phone.CurrentScreen != PhoneScreenId.Messages)
                return;

            _conversationVisible = true;
            Refresh();
            Canvas.ForceUpdateCanvases();
            _conversation.StopMovement();
            _conversation.verticalNormalizedPosition = 1f;
        }

        private bool TryBack()
        {
            if (_phone.CurrentScreen != PhoneScreenId.Messages || !_conversationVisible)
                return false;

            _conversationVisible = false;
            Refresh();
            return true;
        }

        private void PreviewBuy()
        {
            if (!_phone.IsInteractive || _phone.CurrentScreen != PhoneScreenId.Shop || !_previewProductAvailable)
                return;

            _previewPressed = true;
            Refresh();
        }

        private void Refresh()
        {
            _shopLabel.text = _localization.Text("phone.shop");
            _messagesLabel.text = _localization.Text("phone.messages");
            _backLabel.text = _localization.Text("phone.back");
            _back.gameObject.SetActive(_phone.CurrentScreen != PhoneScreenId.Home);
            _avatar.sprite = _landlordAvatar;
            _unreadBadge.transform.parent.gameObject.SetActive(_previewUnreadCount > 0);
            _unreadBadge.text = _previewUnreadCount > 99 ? "99+" : _previewUnreadCount.ToString();
            string name = _localization.Text("phone.landlord");
            _contactText.text = _conversationVisible
                ? $"<b>{name}</b>\n<size=14><color=#A4B3BE>{_localization.Text("phone.conversation")}</color></size>"
                : $"<b>{name}</b>\n<size=16><color=#BAC5CD>{_localization.Text("phone.message_preview")}</color></size>\n<size=13><color=#96A9B8>07:12{UnreadCaption()}</color></size>";
            _contact.interactable = !_conversationVisible;
            _conversation.gameObject.SetActive(_conversationVisible);
            for (int i = 0; i < _bubbles.Length; i++)
                _bubbles[i].text = _localization.Text($"phone.sample_message_{i + 1}");
            _buy.interactable = _previewProductAvailable && !_previewPressed;
            _buyLabel.text = _localization.Text(_previewPressed ? "phone.preview_only" : _previewProductAvailable ? "phone.buy" : "phone.unavailable");
            _productText.text = $"<size=13><color=#A3B8CA>{_localization.Text("phone.product_category")}</color></size>\n<size=30><b>{_localization.Text("phone.product_name")}</b></size>\n<line-height=145%><size=32>$15.00</size>\n<size=17><color=#BAC5CD>{_localization.Text(_previewPressed ? "phone.preview_feedback" : "phone.product_description")}</color></size>";
            RefreshClock();
        }

        private string UnreadCaption() => _previewUnreadCount > 0 ? "  ·  " + _localization.Format("phone.unread", _previewUnreadCount) : string.Empty;

        private void RefreshClock()
        {
            string title = _phone.CurrentScreen switch
            {
                PhoneScreenId.Messages => _localization.Text("phone.messages"),
                PhoneScreenId.Shop => _localization.Text("phone.shop"),
                _ => _localization.Text("phone.brand")
            };
            _header.text = $"<size=14>{_time.Hour:00}:{_time.Minute:00}<pos=295>LTE</size>\n<line-height=170%><size=29><b>{title}</b></size>";
            _homeClock.text = $"<size=72>{_time.Hour:00}:{_time.Minute:00}</size>\n<size=18><color=#CDD5DB>{_localization.Format("hud.day", _time.Day)}</color></size>";
        }
    }
}
