using System;
using System.Globalization;

namespace GoLive.Localization
{
    public sealed class LocalizationRuntime
    {
        public GameLanguage CurrentLanguage { get; private set; }

        public event Action<GameLanguage> LanguageChanged;

        private readonly LocalizationCatalog _catalog;

        public LocalizationRuntime(LocalizationCatalog catalog, GameLanguage startingLanguage)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));

            if (!Enum.IsDefined(typeof(GameLanguage), startingLanguage))
                throw new ArgumentOutOfRangeException(nameof(startingLanguage));

            CurrentLanguage = startingLanguage;
        }

        public string Text(string key)
        {
            return _catalog.TryGetText(key, CurrentLanguage, out string text)
                ? text
                : $"[{key}]";
        }

        public string Format(string key, params object[] arguments)
        {
            return string.Format(CultureInfo.InvariantCulture, Text(key), arguments);
        }

        public void SetLanguage(GameLanguage language)
        {
            if (!Enum.IsDefined(typeof(GameLanguage), language))
                throw new ArgumentOutOfRangeException(nameof(language));

            if (CurrentLanguage == language)
                return;

            CurrentLanguage = language;
            LanguageChanged?.Invoke(language);
        }
    }
}