using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GoLive.Desktop
{
    public sealed class OutlineView : DesktopAppView
    {
        [Serializable] private sealed class MessageRow { public Button button; public TMP_Text subject; }
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
        private int _page;
        private string _selectedId;
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
        }
        protected override void Refresh()
        {
            bool created = State.Outline.IsCreated;
            createPanel.SetActive(!created);
            inboxPanel.SetActive(created);
            if (!created) return;
            address.text = State.Outline.Address;
            int pages = Mathf.Max(1, (State.Outline.Messages.Count + rows.Length - 1) / rows.Length);
            _page = Mathf.Clamp(_page, 0, pages - 1);
            pageLabel.text = $"{_page + 1} / {pages}";
            previous.interactable = _page > 0;
            next.interactable = _page < pages - 1;
            for (int i = 0; i < rows.Length; i++)
            {
                int index = State.Outline.Messages.Count - 1 - (_page * rows.Length + i);
                rows[i].button.gameObject.SetActive(index >= 0);
                if (index < 0) continue;
                OutlineMessage mail = State.Outline.Messages[index];
                rows[i].subject.text = (mail.IsRead ? "" : "• ") + T(mail.SubjectKey);
            }
            message.text = T(State.Outline.Messages.Count == 0 ? "desktop.outline.empty" : "desktop.outline.select");
            foreach (OutlineMessage mail in State.Outline.Messages)
                if (mail.Id == _selectedId)
                {
                    var args = new object[mail.BodyArguments.Count];
                    for (int i = 0; i < args.Length; i++) args[i] = mail.BodyArguments[i];
                    message.text = T(mail.SubjectKey) + "\n\n" + F(mail.BodyKey, args);
                    break;
                }
        }
    }
}
