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

        // Counted text lives under "<key>.one", "<key>.few" and "<key>.many"; {0} is the count.
        public string FormatCount(string key, long count)
        {
            return Format(PluralKey(key, CurrentLanguage, count), count);
        }

        public static string PluralKey(string key, GameLanguage language, long count)
        {
            return $"{key}.{PluralForm(language, count)}";
        }

        // Russian: 1, 21, 31 -> one; 2-4, 22-24 -> few; 0, 5-20, 25-30 -> many. English: 1 -> one, else many.
        public static string PluralForm(GameLanguage language, long count)
        {
            long n = Math.Abs(count);

            switch (language)
            {
                case GameLanguage.Russian:
                    long last = n % 10;
                    long lastTwo = n % 100;

                    if (last == 1 && lastTwo != 11)
                        return "one";

                    return (last is >= 2 and <= 4) && (lastTwo is < 12 or > 14) ? "few" : "many";

                case GameLanguage.English:
                    return n == 1 ? "one" : "many";

                default:
                    throw new ArgumentOutOfRangeException(nameof(language));
            }
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