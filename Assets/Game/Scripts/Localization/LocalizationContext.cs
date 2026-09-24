using System;
using UnityEngine;

namespace GoLive.Localization
{
    [DisallowMultipleComponent]
    public sealed class LocalizationContext : MonoBehaviour
    {
        [SerializeField] private LocalizationCatalog _catalog;
        [SerializeField] private GameLanguage _startingLanguage = GameLanguage.Russian;

        public GameLanguage CurrentLanguage
        {
            get
            {
                EnsureInitialized();
                return _runtime?.CurrentLanguage ?? _startingLanguage;
            }
        }

        public event Action<GameLanguage> LanguageChanged;

        private LocalizationRuntime _runtime;
        private bool _initializationFailed;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnDestroy()
        {
            if (_runtime != null)
                _runtime.LanguageChanged -= HandleLanguageChanged;
        }

        public string Text(string key)
        {
            return EnsureInitialized() ? _runtime.Text(key) : $"[{key}]";
        }

        public string Format(string key, params object[] arguments)
        {
            if (!EnsureInitialized())
                return $"[{key}]";

            try
            {
                return _runtime.Format(key, arguments);
            }
            catch (FormatException exception)
            {
                Debug.LogError($"Invalid localization format for key '{key}': {exception.Message}", _catalog);
                return _runtime.Text(key);
            }
        }

        public string FormatCount(string key, long count)
        {
            if (!EnsureInitialized())
                return $"[{key}]";

            try
            {
                return _runtime.FormatCount(key, count);
            }
            catch (FormatException exception)
            {
                Debug.LogError($"Invalid localization format for counted key '{key}': {exception.Message}", _catalog);
                return _runtime.Text(LocalizationRuntime.PluralKey(key, _runtime.CurrentLanguage, count));
            }
        }

        public void SetLanguage(GameLanguage language)
        {
            if (EnsureInitialized())
                _runtime.SetLanguage(language);
        }

        [ContextMenu("Use Russian")]
        private void UseRussian()
        {
            SetLanguage(GameLanguage.Russian);
        }

        [ContextMenu("Use English")]
        private void UseEnglish()
        {
            SetLanguage(GameLanguage.English);
        }

        private bool EnsureInitialized()
        {
            if (_runtime != null)
                return true;

            if (_initializationFailed)
                return false;

            if (_catalog == null)
            {
                Debug.LogError($"{nameof(LocalizationContext)} on {name} requires a Localization Catalog.", this);
                _initializationFailed = true;
                return false;
            }

            if (!_catalog.Validate(out string error))
            {
                Debug.LogError($"Localization Catalog is invalid: {error}", _catalog);
                _initializationFailed = true;
                return false;
            }

            _runtime = new LocalizationRuntime(_catalog, _startingLanguage);
            _runtime.LanguageChanged += HandleLanguageChanged;

            return true;
        }

        private void HandleLanguageChanged(GameLanguage language)
        {
            LanguageChanged?.Invoke(language);
        }
    }
}