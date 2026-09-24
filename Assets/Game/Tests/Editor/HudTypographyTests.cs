using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using GoLive.Localization;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace GoLive.Tests
{
    // The gameplay HUD and the TAB inventory share one typography: Manrope, from static atlases that already hold every
    // character their Russian and English strings use (a missing glyph would silently fall back to another font).
    public sealed class HudTypographyTests
    {
        private const string HudPrefab = "Assets/Game/Prefab/HUD/[HUD].prefab";
        private const string CatalogPath = "Assets/Game/Scripts/Localization/Catalog/GameLocalizationCatalog.asset";
        private const string FontFolder = "Assets/Game/UI/Fonts/Manrope";
        private static readonly string[] Weights = { "Manrope-Medium", "Manrope-SemiBold", "Manrope-Bold" };
        private static readonly string[] HudKeyPrefixes = { "hud.", "rent.", "inventory.", "item.", "interaction." };

        [Test]
        public void GameplayHudAndInventoryTextUseManrope()
        {
            Transform hud = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefab).transform;
            List<TMP_Text> texts = new[] { "Status", "Balance", "InventoryUI" }
                .SelectMany(block => hud.Find(block).GetComponentsInChildren<TMP_Text>(true))
                .ToList();

            Assert.That(texts.Count, Is.GreaterThan(20), "status, balance and inventory texts found");

            foreach (TMP_Text text in texts)
            {
                Assert.That(text.font, Is.Not.Null, text.name);
                Assert.That(text.font.name, Does.StartWith("Manrope"), $"{text.transform.parent.name}/{text.name}");
            }
        }

        [Test]
        public void ManropeAtlasesAreStaticAndHoldEveryHudAndInventoryString()
        {
            LocalizationCatalog catalog = AssetDatabase.LoadAssetAtPath<LocalizationCatalog>(CatalogPath);
            LocalizationEntry[] entries = (LocalizationEntry[])typeof(LocalizationCatalog)
                .GetField("_entries", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(catalog);
            List<LocalizationEntry> hudEntries = entries.Where(entry => HudKeyPrefixes.Any(prefix => entry.Key.StartsWith(prefix))).ToList();
            Assert.That(hudEntries.Count, Is.GreaterThan(30), "HUD, rent, inventory, item and prompt strings found");

            foreach (string weight in Weights)
            {
                TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{FontFolder}/{weight} SDF.asset");
                Assert.That(font, Is.Not.Null, weight);
                Assert.That(font.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Static), $"{weight}: nothing is added to the atlas at runtime");

                foreach (LocalizationEntry entry in hudEntries)
                {
                    foreach (GameLanguage language in new[] { GameLanguage.Russian, GameLanguage.English })
                    {
                        string text = Regex.Replace(entry.GetText(language) ?? string.Empty, "<[^>]+>", string.Empty).Replace("\n", string.Empty).Replace("\r", string.Empty);
                        if (text.Length == 0)
                            continue;

                        font.HasCharacters(text, out List<char> missing);
                        Assert.That(missing, Is.Empty, $"{weight} lacks '{new string(missing?.ToArray() ?? new char[0])}' for {entry.Key} ({language})");
                    }
                }
            }
        }
    }
}
