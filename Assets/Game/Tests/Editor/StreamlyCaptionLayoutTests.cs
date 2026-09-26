using GoLive.Desktop;
using GoLive.Localization;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace GoLive.Tests
{
    // The Streamly preview caption row (source, voice status, voice language) fits every text it can show in
    // Russian and English at its authored size, and its three rects stay side by side inside the preview width.
    public sealed class StreamlyCaptionLayoutTests
    {
        private const string CatalogPath = "Assets/Game/Scripts/Localization/Catalog/GameLocalizationCatalog.asset";
        private static readonly string[] PreviewKeys = { "desktop.stream.preview_screen", "desktop.stream.preview_unavailable" };
        private static readonly string[] StatusKeys =
        {
            "desktop.stream.voice.listening", "desktop.stream.voice.ready", "desktop.stream.voice.loading",
            "desktop.stream.voice.disabled", "desktop.stream.voice.microphone_unavailable",
            "desktop.stream.voice.model_missing", "desktop.stream.voice.unavailable"
        };
        private static readonly string[] LanguageKeys =
        {
            "desktop.stream.voice.language.auto", "desktop.stream.voice.language.russian", "desktop.stream.voice.language.english"
        };
        private SceneSetup[] _previousScenes;

        [OneTimeSetUp]
        public void OpenScene()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty && SceneManager.GetSceneAt(i).rootCount > 0)
                    Assert.Ignore("Save the open scene before running the Streamly caption layout fixture.");
            _previousScenes = EditorSceneManager.GetSceneManagerSetup();
            EditorSceneManager.OpenScene("Assets/Game/Scenes/GL.unity", OpenSceneMode.Single);
        }

        [OneTimeTearDown]
        public void RestoreScene() => SaveTestWorld.RestoreScene(_previousScenes);

        [Test]
        public void CaptionRowFitsEveryVoiceStateInBothLanguagesWithoutOverlap()
        {
            StreamlyView[] views = Object.FindObjectsByType<StreamlyView>(FindObjectsInactive.Include);
            Assert.That(views.Length, Is.EqualTo(1));
            var serialized = new SerializedObject(views[0]);
            TMP_Text preview = (TMP_Text)serialized.FindProperty("preview").objectReferenceValue;
            TMP_Text status = (TMP_Text)serialized.FindProperty("voiceStatus").objectReferenceValue;
            TMP_Text language = (TMP_Text)serialized.FindProperty("voiceLanguage").objectReferenceValue;
            var catalog = AssetDatabase.LoadAssetAtPath<LocalizationCatalog>(CatalogPath);

            AssertFits(preview, catalog, PreviewKeys);
            AssertFits(status, catalog, StatusKeys);
            AssertFits(language, catalog, LanguageKeys);

            // Side by side on one row (the voice chips are buttons, so their hit areas must not overlap).
            Rect source = preview.rectTransform.rect, voice = status.rectTransform.rect, chip = language.rectTransform.rect;
            float sourceRight = preview.rectTransform.anchoredPosition.x + source.width;
            float voiceLeft = status.rectTransform.anchoredPosition.x, voiceRight = voiceLeft + voice.width;
            float chipLeft = language.rectTransform.anchoredPosition.x, chipRight = chipLeft + chip.width;
            Assert.That(sourceRight, Is.LessThanOrEqualTo(voiceLeft), "preview source / voice status");
            Assert.That(voiceRight, Is.LessThanOrEqualTo(chipLeft), "voice status / voice language");
            Assert.That(chipRight, Is.EqualTo(preview.rectTransform.anchoredPosition.x + 708f), "the row ends at the preview's right edge");
        }

        private static void AssertFits(TMP_Text text, LocalizationCatalog catalog, string[] keys)
        {
            string original = text.text;
            try
            {
                foreach (string key in keys)
                foreach (GameLanguage gameLanguage in new[] { GameLanguage.Russian, GameLanguage.English })
                {
                    Assert.That(catalog.TryGetText(key, gameLanguage, out string value), Is.True, key);
                    text.text = value;
                    text.ForceMeshUpdate(true, true);
                    Assert.That(text.isTextTruncated, Is.False,
                        $"{text.name} ({gameLanguage}) \"{value}\" needs {text.GetPreferredValues(value).x:0.0} of {text.rectTransform.rect.width}");
                }
            }
            finally
            {
                text.text = original;
            }
        }
    }
}
