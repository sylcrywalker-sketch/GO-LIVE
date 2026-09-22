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
        private const string LandlordContactId = "landlord";

        [Header("Core")]
        [SerializeField] private PhoneBehaviour _phone;
        [SerializeField] private LocalizationContext _localization;
        [SerializeField] private GameClockBehaviour _clock;
        [SerializeField] private PhoneMessagesBehaviour _messages;
        [SerializeField] private PhoneShopView _shopView;

        [Header("Shared and Home")]
        [SerializeField] private TMP_Text _header;
        [SerializeField] private TMP_Text _homeClock;
        [SerializeField] private TMP_Text _shopLabel;
        [SerializeField] private TMP_Text _messagesLabel;
        [SerializeField] private TMP_Text _unreadBadge;
        [SerializeField] private Button _back;
        [SerializeField] private TMP_Text _backLabel;

        [Header("Messages")]
        [SerializeField] private Button _contact;
        [SerializeField] private Image _avatar;
        [SerializeField] private Sprite _landlordAvatar;
        [SerializeField] private TMP_Text _contactText;
        [SerializeField] private ScrollRect _conversation;
        [SerializeField] private TMP_Text[] _bubbles;

        public bool IsConversationVisible => _conversationVisible;

        private bool _conversationVisible;
        private bool _started;
        private bool _bound;

        private GameClock _subscribedClock;
        private GameTimeSnapshot _time;

        private void Awake()
        {
            if (!ValidateConfiguration())
                enabled = false;
        }

        private void Start()
        {
            if (!isActiveAndEnabled)
                return;

            _started = true;
            Bind();
        }

        private void OnEnable()
        {
            if (_started)
                Bind();
        }

        private void OnDisable()
        {
            Unbind();

            _conversationVisible = false;
        }

        private void Bind()
        {
            if (_bound)
                return;

            _subscribedClock = _clock.Clock;

            if (_subscribedClock == null)
            {
                Debug.LogError(
                    $"{nameof(PhoneScreenView)} could not access initialized Game Clock.",
                    this);

                enabled = false;
                return;
            }

            _phone.ScreenChanged += HandleScreenChanged;
            _phone.ScreenBackRequested += TryBack;
            _localization.LanguageChanged += HandleLanguageChanged;
            _messages.Messages.Changed += HandleMessagesChanged;
            _shopView.PageChanged += RefreshClock;

            _contact.onClick.AddListener(OpenConversation);

            _subscribedClock.MinuteChanged += HandleMinuteChanged;

            _time = _subscribedClock.Current;
            _bound = true;

            HandleScreenChanged();
        }

        private void Unbind()
        {
            if (!_bound)
                return;

            _phone.ScreenChanged -= HandleScreenChanged;
            _phone.ScreenBackRequested -= TryBack;
            _localization.LanguageChanged -= HandleLanguageChanged;
            _messages.Messages.Changed -= HandleMessagesChanged;
            _shopView.PageChanged -= RefreshClock;

            _contact.onClick.RemoveListener(OpenConversation);

            if (_subscribedClock != null)
                _subscribedClock.MinuteChanged -= HandleMinuteChanged;

            _subscribedClock = null;
            _bound = false;
        }

        private void HandleScreenChanged()
        {
            _conversationVisible = false;
            Refresh();
        }

        private void HandleLanguageChanged(GameLanguage language)
        {
            Refresh();
        }

        private void HandleMessagesChanged()
        {
            RefreshMessages();
        }

        private void HandleMinuteChanged(GameTimeSnapshot time)
        {
            _time = time;
            RefreshClock();
        }

        private void OpenConversation()
        {
            if (!_phone.IsInteractive ||
                _phone.CurrentScreen != PhoneScreenId.Messages ||
                !_messages.Messages.TryGetConversation(
                    LandlordContactId,
                    out PhoneConversation conversation) ||
                conversation.Messages.Count == 0)
            {
                return;
            }

            _conversationVisible = true;

            if (!_messages.Messages.MarkConversationRead(LandlordContactId))
                RefreshMessages();

            Canvas.ForceUpdateCanvases();

            _conversation.StopMovement();
            _conversation.verticalNormalizedPosition = 1f;

            RefreshMessages();
        }

        private bool TryBack()
        {
            if (_phone.CurrentScreen != PhoneScreenId.Messages ||
                !_conversationVisible)
            {
                return false;
            }

            _conversationVisible = false;
            RefreshMessages();

            return true;
        }

        private void Refresh()
        {
            _shopLabel.text = _localization.Text("phone.shop");
            _messagesLabel.text = _localization.Text("phone.messages");
            _backLabel.text = _localization.Text("phone.back");

            _back.gameObject.SetActive(
                _phone.CurrentScreen != PhoneScreenId.Home);

            RefreshMessages();
            RefreshClock();
        }

        private void RefreshMessages()
        {
            int totalUnread = _messages.Messages.TotalUnreadCount;

            _unreadBadge.transform.parent.gameObject.SetActive(totalUnread > 0);
            _unreadBadge.text =
                totalUnread > 99 ? "99+" : totalUnread.ToString();

            _avatar.sprite = _landlordAvatar;

            if (!_messages.Messages.TryGetConversation(
                    LandlordContactId,
                    out PhoneConversation conversation) ||
                conversation.Messages.Count == 0)
            {
                _conversationVisible = false;

                _contact.gameObject.SetActive(false);
                _conversation.gameObject.SetActive(false);

                ClearBubbles();
                return;
            }

            _contact.gameObject.SetActive(true);

            PhoneMessage latestMessage =
                conversation.Messages[conversation.Messages.Count - 1];

            string contactName =
                _localization.Text("phone.landlord");

            if (_conversationVisible)
            {
                _contactText.text =
                    $"<b>{contactName}</b>\n" +
                    $"<size=14><color=#A4B3BE>{_localization.Text("phone.conversation")}</color></size>";
            }
            else
            {
                string preview =
                    ResolveContent(latestMessage.Content);

                string time =
                    FormatTime(latestMessage.Timestamp);

                _contactText.text =
                    $"<b>{contactName}</b>\n" +
                    $"<size=16><color=#BAC5CD>{preview}</color></size>\n" +
                    $"<size=13><color=#96A9B8>{time}{UnreadCaption(conversation.UnreadCount)}</color></size>";
            }

            _contact.interactable = !_conversationVisible;
            _conversation.gameObject.SetActive(_conversationVisible);

            RefreshBubbles(conversation);
        }

        private void RefreshBubbles(PhoneConversation conversation)
        {
            ClearBubbles();

            if (!_conversationVisible)
                return;

            int visibleCount =
                Mathf.Min(
                    _bubbles.Length,
                    conversation.Messages.Count);

            int firstMessageIndex =
                conversation.Messages.Count - visibleCount;

            for (int i = 0; i < visibleCount; i++)
            {
                TMP_Text bubble = _bubbles[i];

                PhoneMessage message =
                    conversation.Messages[firstMessageIndex + i];

                bubble.gameObject.SetActive(true);
                bubble.text = ResolveContent(message.Content);

                bubble.alignment =
                    message.Direction == MessageDirection.Outgoing
                        ? TextAlignmentOptions.MidlineRight
                        : TextAlignmentOptions.MidlineLeft;
            }
        }

        private void ClearBubbles()
        {
            for (int i = 0; i < _bubbles.Length; i++)
            {
                _bubbles[i].text = string.Empty;
                _bubbles[i].gameObject.SetActive(false);
            }
        }

        private string ResolveContent(MessageContent content)
        {
            return content.Kind == MessageContentKind.LocalizationKey
                ? _localization.Text(content.Value)
                : content.Value;
        }

        private void RefreshClock()
        {
            string title =
                _phone.CurrentScreen switch
                {
                    PhoneScreenId.Messages =>
                        _localization.Text("phone.messages"),

                    PhoneScreenId.Shop =>
                        _localization.Text(_shopView.TitleKey),

                    _ =>
                        _localization.Text("phone.brand")
                };

            _header.text =
                $"<size=14>{_time.Hour:00}:{_time.Minute:00}<pos=295>LTE</size>\n" +
                $"<line-height=170%><size=29><b>{title}</b></size>";

            _homeClock.text =
                $"<size=72>{_time.Hour:00}:{_time.Minute:00}</size>\n" +
                $"<size=18><color=#CDD5DB>{_localization.Format("hud.day", _time.Day)}</color></size>";
        }

        private string UnreadCaption(int count)
        {
            return count > 0
                ? "  ·  " +
                  _localization.Format("phone.unread", count)
                : string.Empty;
        }

        private static string FormatTime(GameTimeSnapshot time)
        {
            return $"{time.Hour:00}:{time.Minute:00}";
        }

        private bool ValidateConfiguration()
        {
            if (_phone == null ||
                _localization == null ||
                _clock == null ||
                _messages == null ||
                _shopView == null ||
                _header == null ||
                _homeClock == null ||
                _shopLabel == null ||
                _messagesLabel == null ||
                _unreadBadge == null ||
                _back == null ||
                _backLabel == null ||
                _contact == null ||
                _avatar == null ||
                _contactText == null ||
                _conversation == null ||
                _bubbles == null)
            {
                Debug.LogError(
                    $"{nameof(PhoneScreenView)} on {name} has incomplete configuration.",
                    this);

                return false;
            }

            for (int i = 0; i < _bubbles.Length; i++)
            {
                if (_bubbles[i] != null)
                    continue;

                Debug.LogError(
                    $"{nameof(PhoneScreenView)} on {name} contains an empty message bubble reference.",
                    this);

                return false;
            }

            return true;
        }
    }
}