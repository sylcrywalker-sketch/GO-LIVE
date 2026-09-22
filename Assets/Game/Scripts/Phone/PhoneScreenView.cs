using System.Globalization;
using GoLive.GameTime;
using GoLive.Localization;
using GoLive.Shop;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GoLive.Phone
{
    [DisallowMultipleComponent]
    public sealed class PhoneScreenView : MonoBehaviour
    {
        private const string LandlordContactId = "landlord";
        private const string BudgetGpuProductId = "budget-gpu";
        private const string BasicKeyboardProductId = "basic-keyboard";

        [Header("Core")]
        [SerializeField] private PhoneBehaviour _phone;
        [SerializeField] private LocalizationContext _localization;
        [SerializeField] private GameClockBehaviour _clock;
        [SerializeField] private PhoneMessagesBehaviour _messages;
        [SerializeField] private ShopBehaviour _shop;

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

        [Header("Shop Storefront")]
        [SerializeField] private GameObject _storefront;

        [SerializeField] private Button _budgetGpuCard;
        [SerializeField] private Image _budgetGpuImage;
        [SerializeField] private TMP_Text _budgetGpuName;
        [SerializeField] private TMP_Text _budgetGpuPrice;
        [SerializeField] private TMP_Text _budgetGpuActionLabel;

        [SerializeField] private Button _keyboardCard;
        [SerializeField] private Image _keyboardImage;
        [SerializeField] private TMP_Text _keyboardName;
        [SerializeField] private TMP_Text _keyboardAvailability;

        [Header("Shop Product Details")]
        [SerializeField] private GameObject _productDetails;
        [SerializeField] private Image _detailsImage;
        [SerializeField] private TMP_Text _detailsCategory;
        [SerializeField] private TMP_Text _detailsName;
        [SerializeField] private TMP_Text _detailsPrice;
        [SerializeField] private TMP_Text _detailsDescription;
        [SerializeField] private Button _detailsBuy;
        [SerializeField] private TMP_Text _detailsBuyLabel;

        public bool IsConversationVisible => _conversationVisible;
        public bool IsProductDetailsVisible => _productDetailsVisible;

        private bool _conversationVisible;
        private bool _productDetailsVisible;
        private bool _started;
        private bool _bound;

        private GameClock _subscribedClock;
        private GameTimeSnapshot _time;

        private void Awake()
        {
            if (!ValidateConfiguration())
            {
                enabled = false;
                return;
            }

            SetShopMode(false);
        }

        private void Start()
        {
            if (!isActiveAndEnabled)
                return;

            if (!_shop.TryGetProduct(BudgetGpuProductId, out _) ||
                !_shop.TryGetProduct(BasicKeyboardProductId, out _))
            {
                Debug.LogError(
                    $"{nameof(PhoneScreenView)} requires '{BudgetGpuProductId}' and '{BasicKeyboardProductId}' in the Shop Catalog.",
                    this);

                enabled = false;
                return;
            }

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
            _productDetailsVisible = false;

            SetShopMode(false);
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
            _shop.Changed += HandleShopChanged;

            _contact.onClick.AddListener(OpenConversation);
            _budgetGpuCard.onClick.AddListener(OpenBudgetGpuDetails);
            _detailsBuy.onClick.AddListener(BuyBudgetGpu);

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
            _shop.Changed -= HandleShopChanged;

            _contact.onClick.RemoveListener(OpenConversation);
            _budgetGpuCard.onClick.RemoveListener(OpenBudgetGpuDetails);
            _detailsBuy.onClick.RemoveListener(BuyBudgetGpu);

            if (_subscribedClock != null)
                _subscribedClock.MinuteChanged -= HandleMinuteChanged;

            _subscribedClock = null;
            _bound = false;
        }

        private void HandleScreenChanged()
        {
            _conversationVisible = false;
            _productDetailsVisible = false;

            SetShopMode(false);
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

        private void HandleShopChanged()
        {
            RefreshShop();
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

        private void OpenBudgetGpuDetails()
        {
            if (!_phone.IsInteractive ||
                _phone.CurrentScreen != PhoneScreenId.Shop)
            {
                return;
            }

            _productDetailsVisible = true;

            SetShopMode(true);
            RefreshShop();
            RefreshClock();
        }

        private void BuyBudgetGpu()
        {
            if (!_phone.IsInteractive ||
                _phone.CurrentScreen != PhoneScreenId.Shop ||
                !_productDetailsVisible)
            {
                return;
            }

            _shop.TryPurchase(BudgetGpuProductId);

            RefreshShop();
        }

        private bool TryBack()
        {
            if (_phone.CurrentScreen == PhoneScreenId.Shop &&
                _productDetailsVisible)
            {
                _productDetailsVisible = false;

                SetShopMode(false);
                RefreshShop();
                RefreshClock();

                return true;
            }

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
            RefreshShop();
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

        private void RefreshShop()
        {
            if (!_shop.TryGetProduct(
                    BudgetGpuProductId,
                    out ShopProductDefinition gpu))
            {
                return;
            }

            if (!_shop.TryGetProduct(
                    BasicKeyboardProductId,
                    out ShopProductDefinition keyboard))
            {
                return;
            }

            _budgetGpuImage.sprite = gpu.Image;
            _budgetGpuImage.enabled = gpu.Image != null;

            _budgetGpuName.text =
                _localization.Text(gpu.NameLocalizationKey);

            _budgetGpuPrice.text =
                FormatMoney(gpu.PriceCents);

            _budgetGpuActionLabel.text =
                _localization.Text("phone.details");

            _budgetGpuCard.interactable = true;

            _keyboardImage.sprite = keyboard.Image;
            _keyboardImage.enabled = keyboard.Image != null;

            _keyboardName.text =
                _localization.Text(keyboard.NameLocalizationKey);

            _keyboardAvailability.text =
                keyboard.IsAvailable
                    ? FormatMoney(keyboard.PriceCents)
                    : _localization.Text("phone.unavailable");

            _keyboardCard.interactable = false;

            _detailsImage.sprite = gpu.Image;
            _detailsImage.enabled = gpu.Image != null;

            _detailsCategory.text =
                _localization.Text(gpu.CategoryLocalizationKey);

            _detailsName.text =
                _localization.Text(gpu.NameLocalizationKey);

            _detailsPrice.text =
                FormatMoney(gpu.PriceCents);

            _detailsDescription.text =
                _localization.Text(gpu.DescriptionLocalizationKey);

            ShopPurchaseResultCode purchaseState =
                _shop.EvaluatePurchase(BudgetGpuProductId);

            _detailsBuyLabel.text =
                _localization.Text(
                    GetPurchaseButtonKey(purchaseState));

            _detailsBuy.interactable =
                purchaseState == ShopPurchaseResultCode.Success;

            SetShopMode(_productDetailsVisible);
        }

        private void SetShopMode(bool showDetails)
        {
            if (_storefront != null)
                _storefront.SetActive(!showDetails);

            if (_productDetails != null)
                _productDetails.SetActive(showDetails);
        }

        private string GetPurchaseButtonKey(ShopPurchaseResultCode state)
        {
            return state switch
            {
                ShopPurchaseResultCode.Success =>
                    "phone.buy",

                ShopPurchaseResultCode.PurchaseLimitReached =>
                    "phone.ordered",

                ShopPurchaseResultCode.InsufficientFunds =>
                    "phone.not_enough_money",

                ShopPurchaseResultCode.Unavailable =>
                    "phone.unavailable",

                ShopPurchaseResultCode.ProductNotFound =>
                    "phone.unavailable",

                ShopPurchaseResultCode.NotReady =>
                    "phone.unavailable",

                ShopPurchaseResultCode.Busy =>
                    "phone.buy",

                _ =>
                    "phone.unavailable"
            };
        }

        private void RefreshClock()
        {
            string title =
                _phone.CurrentScreen switch
                {
                    PhoneScreenId.Messages =>
                        _localization.Text("phone.messages"),

                    PhoneScreenId.Shop =>
                        _localization.Text(
                            _productDetailsVisible
                                ? "phone.product_details"
                                : "phone.shop"),

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

        private static string FormatMoney(long cents)
        {
            decimal dollars = cents / 100m;

            return
                $"${dollars.ToString("0.00", CultureInfo.InvariantCulture)}";
        }

        private bool ValidateConfiguration()
        {
            if (_phone == null ||
                _localization == null ||
                _clock == null ||
                _messages == null ||
                _shop == null ||
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
                _bubbles == null ||
                _storefront == null ||
                _budgetGpuCard == null ||
                _budgetGpuImage == null ||
                _budgetGpuName == null ||
                _budgetGpuPrice == null ||
                _budgetGpuActionLabel == null ||
                _keyboardCard == null ||
                _keyboardImage == null ||
                _keyboardName == null ||
                _keyboardAvailability == null ||
                _productDetails == null ||
                _detailsImage == null ||
                _detailsCategory == null ||
                _detailsName == null ||
                _detailsPrice == null ||
                _detailsDescription == null ||
                _detailsBuy == null ||
                _detailsBuyLabel == null)
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