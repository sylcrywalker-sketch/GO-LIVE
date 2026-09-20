using TMPro;
using UnityEngine;

namespace GoLive.Localization
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class LocalizedTextView : MonoBehaviour
    {
        [SerializeField] private LocalizationContext _localization;
        [SerializeField] private string _key;

        private TMP_Text _text;

        private void Awake()
        {
            _text = GetComponent<TMP_Text>();

            if (_localization != null && !string.IsNullOrWhiteSpace(_key))
                return;

            Debug.LogError($"{nameof(LocalizedTextView)} on {name} has incomplete configuration.", this);
            enabled = false;
        }

        private void OnEnable()
        {
            if (_localization == null)
                return;

            _localization.LanguageChanged += HandleLanguageChanged;
            Refresh();
        }

        private void OnDisable()
        {
            if (_localization != null)
                _localization.LanguageChanged -= HandleLanguageChanged;
        }

        private void HandleLanguageChanged(GameLanguage language)
        {
            Refresh();
        }

        private void Refresh()
        {
            _text.text = _localization.Text(_key);
        }
    }
}