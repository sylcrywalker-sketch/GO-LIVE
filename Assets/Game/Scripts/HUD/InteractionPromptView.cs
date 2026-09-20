using System.Text;
using GoLive.Localization;
using GoLive.Player;
using TMPro;
using UnityEngine;

namespace GoLive.HUD
{
    [DisallowMultipleComponent]
    public sealed class InteractionPromptView : MonoBehaviour
    {
        [SerializeField] private PlayerInteractor playerInteractor;
        [SerializeField] private LocalizationContext localization;
        [SerializeField] private TMP_Text interactionText;
        [SerializeField] private TMP_Text carryText;
        [SerializeField] private TMP_Text heldUseText;

        private readonly StringBuilder _builder = new(96);

        private void Awake()
        {
            if (playerInteractor != null && localization != null && interactionText != null && carryText != null && heldUseText != null)
                return;

            Debug.LogError($"{nameof(InteractionPromptView)} on {name} has incomplete configuration.", this);
            enabled = false;
        }

        private void OnEnable()
        {
            if (!enabled)
                return;

            playerInteractor.PromptsChanged += HandlePromptsChanged;
            localization.LanguageChanged += HandleLanguageChanged;

            Refresh(playerInteractor.Prompts);
        }

        private void OnDisable()
        {
            if (playerInteractor != null)
                playerInteractor.PromptsChanged -= HandlePromptsChanged;

            if (localization != null)
                localization.LanguageChanged -= HandleLanguageChanged;
        }

        private void HandlePromptsChanged(InteractionPromptState prompts)
        {
            Refresh(prompts);
        }

        private void HandleLanguageChanged(GameLanguage language)
        {
            Refresh(playerInteractor.Prompts);
        }

        private void Refresh(InteractionPromptState prompts)
        {
            RenderWorldPrompts(prompts);
            SetPrompt(carryText, playerInteractor.GetDropBinding(), prompts.DropKey);
            SetPrompt(heldUseText, playerInteractor.GetUseBinding(), prompts.HeldUseKey);
        }

        private void RenderWorldPrompts(InteractionPromptState prompts)
        {
            _builder.Clear();

            AppendPrompt(playerInteractor.GetTakeBinding(), prompts.TakeKey);
            AppendPrompt(playerInteractor.GetUseBinding(), prompts.WorldUseKey);
            AppendPrompt(playerInteractor.GetSpecialBinding(), prompts.SpecialKey);

            string value = _builder.ToString();

            interactionText.text = value;
            interactionText.gameObject.SetActive(value.Length > 0);
        }

        private void AppendPrompt(string binding, string localizationKey)
        {
            if (string.IsNullOrWhiteSpace(binding) || string.IsNullOrWhiteSpace(localizationKey))
                return;

            if (_builder.Length > 0)
                _builder.AppendLine();

            _builder.Append('[');
            _builder.Append(binding);
            _builder.Append("] ");
            _builder.Append(localization.Text(localizationKey));
        }

        private void SetPrompt(TMP_Text target, string binding, string localizationKey)
        {
            bool visible = !string.IsNullOrWhiteSpace(binding) && !string.IsNullOrWhiteSpace(localizationKey);

            target.gameObject.SetActive(visible);

            if (!visible)
            {
                target.text = string.Empty;
                return;
            }

            target.text = $"[{binding}] {localization.Text(localizationKey)}";
        }
    }
}