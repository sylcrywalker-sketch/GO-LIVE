using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GoLive.Desktop
{
    public sealed class OutlineView : DesktopAppView
    {
        [Serializable] private sealed class MessageRow
        {
            public Button button;
            public TMP_Text subject;
            public TMP_Text sender;
            public TMP_Text excerpt;
            public Image background;
            public Image senderIcon;
            public GameObject unreadMarker;
        }
        [SerializeField] private GameObject createPanel;
        [SerializeField] private GameObject inboxPanel;
        [SerializeField] private TMP_InputField username;
        [SerializeField] private Button create;
        [SerializeField] private TMP_Text address;
        [SerializeField] private TMP_Text message;
        [SerializeField] private MessageRow[] rows;
        [SerializeField] private Button previous;
        [SerializeField] private Button next;
        [SerializeField] private TMP_Text pageLabel;
        [SerializeField] private TMP_Text unreadCount;
        [SerializeField] private TMP_Text messageSubject;
        [SerializeField] private TMP_Text messageSender;
        [SerializeField] private Image messageSenderIcon;
        [SerializeField] private GameObject messageHeading;
        [SerializeField] private ScrollRect messageScroll;
        [SerializeField] private Sprite trichIcon;
        [SerializeField] private Sprite outlineIcon;
        private int _page;
        private string _selectedId;
        private int _restoreGeneration = -1;
        private void Awake()
        {
            create.onClick.AddListener(() => Result(State.Outline.CreateAddress(username.text)));
            for (int i = 0; i < rows.Length; i++) { int index = i; rows[i].button.onClick.AddListener(() => Read(index)); }
            previous.onClick.AddListener(() => { _page--; Refresh(); });
            next.onClick.AddListener(() => { _page++; Refresh(); });
        }
        private void Read(int row)
        {
            int index = State.Outline.Messages.Count - 1 - (_page * rows.Length + row);
            if (index < 0 || index >= State.Outline.Messages.Count) return;
            _selectedId = State.Outline.Messages[index].Id;
            State.Outline.MarkRead(_selectedId);
            Refresh();
            if (messageScroll != null) messageScroll.verticalNormalizedPosition = 1f;
        }
        protected override void Refresh()
        {
            bool created = State.Outline.IsCreated;
            createPanel.SetActive(!created);
            inboxPanel.SetActive(created);
            if (!created)
            {
                _page = 0;
                _selectedId = null;
                return;
            }
            if (_restoreGeneration != State.RestoreGeneration)
            {
                _selectedId = null;
                _page = 0;
                _restoreGeneration = State.RestoreGeneration;
                if (messageScroll != null) messageScroll.verticalNormalizedPosition = 1f;
            }
            address.text = State.Outline.Address;
            int pages = Mathf.Max(1, (State.Outline.Messages.Count + rows.Length - 1) / rows.Length);
            _page = Mathf.Clamp(_page, 0, pages - 1);
            pageLabel.text = $"{_page + 1} / {pages}";
            previous.interactable = _page > 0;
            next.interactable = _page < pages - 1;
            int unread = 0;
            foreach (OutlineMessage mail in State.Outline.Messages)
                if (!mail.IsRead) unread++;
            if (unreadCount != null)
            {
                unreadCount.text = unread.ToString();
                unreadCount.gameObject.SetActive(unread > 0);
            }
            for (int i = 0; i < rows.Length; i++)
            {
                int index = State.Outline.Messages.Count - 1 - (_page * rows.Length + i);
                rows[i].button.gameObject.SetActive(index >= 0);
                if (index < 0) continue;
                OutlineMessage mail = State.Outline.Messages[index];
                MessageRow row = rows[i];
                row.subject.text = (row.unreadMarker == null && !mail.IsRead ? "• " : "") + T(mail.SubjectKey);
                row.subject.fontStyle = mail.IsRead ? FontStyles.Normal : FontStyles.Bold;
                if (row.sender != null) row.sender.text = Sender(mail);
                if (row.excerpt != null) row.excerpt.text = Excerpt(mail);
                if (row.senderIcon != null) row.senderIcon.sprite = SenderIcon(mail);
                if (row.unreadMarker != null) row.unreadMarker.SetActive(!mail.IsRead);
                if (row.background != null) row.background.color = mail.Id == _selectedId
                    ? new Color(.83f, .90f, .98f)
                    : mail.IsRead ? new Color(.97f, .98f, .99f) : Color.white;
            }
            message.text = T(State.Outline.Messages.Count == 0 ? "desktop.outline.empty" : "desktop.outline.select");
            if (messageSubject != null) messageSubject.text = "";
            if (messageSender != null) messageSender.text = "";
            if (messageHeading != null) messageHeading.SetActive(false);
            foreach (OutlineMessage mail in State.Outline.Messages)
                if (mail.Id == _selectedId)
                {
                    message.text = (messageSubject == null ? T(mail.SubjectKey) + "\n\n" : "") + Body(mail);
                    if (messageSubject != null) messageSubject.text = T(mail.SubjectKey);
                    if (messageSender != null) messageSender.text = Sender(mail);
                    if (messageSenderIcon != null) messageSenderIcon.sprite = SenderIcon(mail);
                    if (messageHeading != null) messageHeading.SetActive(true);
                    break;
                }
        }
        private string Body(OutlineMessage mail)
        {
            var args = new object[mail.BodyArguments.Count];
            for (int i = 0; i < args.Length; i++) args[i] = mail.BodyArguments[i];
            return F(mail.BodyKey, args);
        }
        private string Excerpt(OutlineMessage mail)
        {
            string body = Body(mail);
            int end = body.IndexOf('\n');
            int carriageReturn = body.IndexOf('\r');
            if (carriageReturn >= 0 && (end < 0 || carriageReturn < end)) end = carriageReturn;
            return end < 0 ? body : body.Substring(0, end);
        }
        // Existing stream summaries are sent and signed by Trich. No sender/date is invented in saved mail.
        private static bool IsTrich(OutlineMessage mail) => mail.SubjectKey.StartsWith("desktop.mail.stream.", StringComparison.Ordinal)
            || mail.SubjectKey.StartsWith("desktop.mail.trich.", StringComparison.Ordinal);
        private string Sender(OutlineMessage mail) => IsTrich(mail) ? "Trich" : T("desktop.outline.system_sender");
        private Sprite SenderIcon(OutlineMessage mail) => IsTrich(mail) ? trichIcon : outlineIcon;
    }
}
