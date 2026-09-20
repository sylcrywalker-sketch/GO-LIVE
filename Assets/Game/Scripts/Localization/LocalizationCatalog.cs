using System;
using System.Collections.Generic;
using UnityEngine;

namespace GoLive.Localization
{
    public enum GameLanguage
    {
        Russian,
        English
    }

    [Serializable]
    public sealed class LocalizationEntry
    {
        [SerializeField] private string _key;

        [TextArea]
        [SerializeField] private string _russian;

        [TextArea]
        [SerializeField] private string _english;

        public string Key => _key;

        public string GetText(GameLanguage language)
        {
            string primary = language switch
            {
                GameLanguage.Russian => _russian,
                GameLanguage.English => _english,
                _ => string.Empty
            };

            if (!string.IsNullOrWhiteSpace(primary))
                return primary;

            string fallback = language == GameLanguage.Russian ? _english : _russian;
            return fallback ?? string.Empty;
        }
    }

    [CreateAssetMenu(fileName = "LocalizationCatalog", menuName = "GO! LIVE/Localization/Catalog")]
    public sealed class LocalizationCatalog : ScriptableObject
    {
        [SerializeField] private LocalizationEntry[] _entries = Array.Empty<LocalizationEntry>();

        private Dictionary<string, LocalizationEntry> _lookup;

        public bool Validate(out string error)
        {
            HashSet<string> keys = new(StringComparer.Ordinal);

            for (int i = 0; i < _entries.Length; i++)
            {
                LocalizationEntry entry = _entries[i];

                if (entry == null)
                {
                    error = $"Localization entry at index {i} is null.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(entry.Key))
                {
                    error = $"Localization entry at index {i} has an empty key.";
                    return false;
                }

                if (!keys.Add(entry.Key))
                {
                    error = $"Duplicate localization key: {entry.Key}";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        public bool TryGetText(string key, GameLanguage language, out string text)
        {
            text = null;

            if (string.IsNullOrWhiteSpace(key))
                return false;

            EnsureLookup();

            if (!_lookup.TryGetValue(key, out LocalizationEntry entry))
                return false;

            text = entry.GetText(language);
            return !string.IsNullOrWhiteSpace(text);
        }

        private void EnsureLookup()
        {
            if (_lookup != null)
                return;

            _lookup = new Dictionary<string, LocalizationEntry>(_entries.Length, StringComparer.Ordinal);

            for (int i = 0; i < _entries.Length; i++)
            {
                LocalizationEntry entry = _entries[i];

                if (entry == null || string.IsNullOrWhiteSpace(entry.Key))
                    continue;

                _lookup.TryAdd(entry.Key, entry);
            }
        }

        private void OnEnable()
        {
            _lookup = null;
        }

        private void OnValidate()
        {
            _lookup = null;
        }
    }
}