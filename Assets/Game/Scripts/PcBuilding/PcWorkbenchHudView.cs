using System;
using TMPro;
using UnityEngine;

namespace GoLive.PcBuilding
{
    // The PC Build Mode screen: the normal gameplay HUD fades out and the build UI (PC status, parts panel, slot card)
    // fades in, following the build mode's timeline. Presentation only; the Workbench decides every text.
    [DisallowMultipleComponent]
    public sealed class PcWorkbenchHudView : MonoBehaviour
    {
        private const float GameplayHudFadeEnd = 0.3f;
        private const float BuildUiFadeStart = 0.7f;

        public enum Tone
        {
            Hint,
            Action,
            Rejected
        }

        [SerializeField] private CanvasGroup buildUi;
        [Tooltip("Gameplay HUD blocks that give way to the build view (day, needs, balance, TAB hint).")]
        [SerializeField] private CanvasGroup[] gameplayHud = Array.Empty<CanvasGroup>();

        [Header("PC status")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text statusText;

        [Header("Slot card")]
        [SerializeField] private TMP_Text cardTitleText;
        [SerializeField] private TMP_Text cardDetailText;
        [SerializeField] private TMP_Text actionText;
        [SerializeField] private TMP_Text controlsText;
        [SerializeField, Min(0.5f)] private float feedbackSeconds = 2.5f;
        [SerializeField] private Color actionColor = new(0.72f, 0.95f, 0.8f, 1f);
        [SerializeField] private Color rejectedColor = new(1f, 0.62f, 0.56f, 1f);
        [SerializeField] private Color hintColor = new(0.64f, 0.68f, 0.73f, 1f);

        public bool IsVisible => buildUi != null && buildUi.alpha > 0f;
        public string Status => statusText.text;
        public string Title => cardTitleText.text;
        public string Detail => cardDetailText.text;
        public string Action => actionText.text;

        private string _action;
        private Tone _actionTone;
        private float _feedbackUntil;

        private void Awake()
        {
            if (buildUi != null && titleText != null && statusText != null && cardTitleText != null && cardDetailText != null && actionText != null && controlsText != null &&
                Array.IndexOf(gameplayHud, null) < 0)
            {
                SetPresence(0f, false);
                return;
            }

            Debug.LogError($"{nameof(PcWorkbenchHudView)} on {name} has incomplete configuration.", this);
            enabled = false;
        }

        private void Update()
        {
            if (_feedbackUntil > 0f && Time.unscaledTime >= _feedbackUntil)
            {
                _feedbackUntil = 0f;
                ApplyAction();
            }
        }

        // 0 = normal gameplay HUD, 1 = build UI. The gameplay HUD is gone while the PC is still on its way and the
        // build UI comes in only as the side panel leaves, so the two never overlap and nothing covers the approach.
        // Only the fully opened build UI takes clicks.
        public void SetPresence(float amount, bool interactive)
        {
            buildUi.alpha = Mathf.Clamp01((amount - BuildUiFadeStart) / (1f - BuildUiFadeStart));
            buildUi.interactable = interactive;
            buildUi.blocksRaycasts = interactive;

            for (int i = 0; i < gameplayHud.Length; i++)
                gameplayHud[i].alpha = Mathf.Clamp01(1f - amount / GameplayHudFadeEnd);

            if (amount <= 0f)
                _feedbackUntil = 0f;
        }

        public void RenderStatus(string title, string status)
        {
            titleText.text = title;
            statusText.text = status;
        }

        public void RenderCard(string title, string detail, string action, Tone tone, string controls)
        {
            SetLine(cardTitleText, title);
            SetLine(cardDetailText, detail);
            SetLine(controlsText, controls);

            // A new situation replaces any explanation of the previous one.
            _action = action;
            _actionTone = tone;
            _feedbackUntil = 0f;
            ApplyAction();
        }

        // A short-lived explanation of why the last click or key did nothing, until it expires or the situation changes.
        public void ShowFeedback(string text)
        {
            _feedbackUntil = Time.unscaledTime + feedbackSeconds;
            SetLine(actionText, text);
            actionText.color = rejectedColor;
        }

        private void ApplyAction()
        {
            SetLine(actionText, _action);
            actionText.color = _actionTone switch
            {
                Tone.Action => actionColor,
                Tone.Rejected => rejectedColor,
                _ => hintColor
            };
        }

        private static void SetLine(TMP_Text text, string value)
        {
            bool visible = !string.IsNullOrEmpty(value);

            text.text = visible ? value : string.Empty;
            text.gameObject.SetActive(visible);
        }
    }
}
