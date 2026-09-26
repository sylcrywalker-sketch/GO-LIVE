using System.Reflection;
using GoLive.Localization;
using GoLive.Voice;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GoLive.Tests
{
    public sealed class VoiceInputConfigurationTests
    {
        private GameObject _holder;
        private VoiceInputBehaviour _voice;
        private LocalizationContext _localization;
        private bool _hadLanguage;
        private int _savedLanguage;

        [SetUp]
        public void SetUp()
        {
            _hadLanguage = PlayerPrefs.HasKey(VoiceInputBehaviour.LanguageKey);
            _savedLanguage = PlayerPrefs.GetInt(VoiceInputBehaviour.LanguageKey);
            _holder = new GameObject("Voice configuration test");
            _holder.SetActive(false);
            _localization = _holder.AddComponent<LocalizationContext>();
            SaveTestWorld.SetField(_localization, "_catalog", AssetDatabase.LoadAssetAtPath<LocalizationCatalog>(
                "Assets/Game/Scripts/Localization/Catalog/GameLocalizationCatalog.asset"));
            _voice = _holder.AddComponent<VoiceInputBehaviour>();
            SaveTestWorld.SetField(_voice, "localization", _localization);
            SaveTestWorld.SetField(_voice, "activity", AssetDatabase.LoadAssetAtPath<VoiceActivityConfig>(
                "Assets/Game/Config/Voice/VoiceActivity.asset"));
        }

        [TearDown]
        public void TearDown()
        {
            _voice?.Recognition?.Dispose();
            Object.DestroyImmediate(_holder);
            if (_hadLanguage) PlayerPrefs.SetInt(VoiceInputBehaviour.LanguageKey, _savedLanguage);
            else PlayerPrefs.DeleteKey(VoiceInputBehaviour.LanguageKey);
        }

        [TestCase(GameLanguage.Russian, -1, SpeechLanguage.Russian)]
        [TestCase(GameLanguage.English, -1, SpeechLanguage.English)]
        [TestCase(GameLanguage.Russian, 99, SpeechLanguage.Russian)]
        [TestCase(GameLanguage.English, 99, SpeechLanguage.English)]
        [TestCase(GameLanguage.Russian, 0, SpeechLanguage.Auto)]
        [TestCase(GameLanguage.English, 0, SpeechLanguage.Auto)]
        [TestCase(GameLanguage.Russian, 2, SpeechLanguage.English)]
        [TestCase(GameLanguage.English, 1, SpeechLanguage.Russian)]
        public void StartupForcesUiLanguageOnlyWithoutAValidPlayerChoice(GameLanguage uiLanguage, int saved, SpeechLanguage expected)
        {
            _localization.SetLanguage(uiLanguage);
            if (saved == -1) PlayerPrefs.DeleteKey(VoiceInputBehaviour.LanguageKey);
            else PlayerPrefs.SetInt(VoiceInputBehaviour.LanguageKey, saved);
            typeof(VoiceInputBehaviour).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(_voice, null);
            Assert.That(_voice.Recognition.Language, Is.EqualTo(expected));
            Assert.That(_voice.IsCapturing, Is.False, "checking voice settings never opens the microphone");
            Assert.That(PlayerPrefs.HasKey(VoiceInputBehaviour.LanguageKey), Is.EqualTo(saved != -1), "default selection does not invent a saved player choice");
            if (saved != -1) Assert.That(PlayerPrefs.GetInt(VoiceInputBehaviour.LanguageKey), Is.EqualTo(saved));
        }

        [Test]
        public void NewVoiceBridgeUsesEvaluatedShippingModelAndGpuBeam()
        {
            var serialized = new SerializedObject(_voice);
            SerializedProperty models = serialized.FindProperty("modelFiles");
            Assert.That(models.arraySize, Is.EqualTo(1));
            Assert.That(models.GetArrayElementAtIndex(0).stringValue, Is.EqualTo("Whisper/ggml-large-v3-turbo-q5_0.bin"));
            Assert.That(serialized.FindProperty("useGpu").boolValue, Is.True);
            SerializedProperty decoding = serialized.FindProperty("decoding");
            Assert.That(decoding, Is.Not.Null, "the authored decoding settings must reach the production factory");
            Assert.That(decoding.FindPropertyRelative("BeamSearch").boolValue, Is.True);
            typeof(VoiceInputBehaviour).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(_voice, null);
            var factory = (System.Func<ISpeechRecognizer>)typeof(VoiceRecognition)
                .GetField("_createRecognizer", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_voice.Recognition);
            using ISpeechRecognizer recognizer = factory();
            Assert.That(recognizer, Is.TypeOf<WhisperSpeechRecognizer>());
            var actualDecoding = (WhisperDecoding)typeof(WhisperSpeechRecognizer)
                .GetField("_decoding", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(recognizer);
            Assert.That(actualDecoding.BeamSearch, Is.True, "the worker factory must use the bridge's decoding settings");
        }
    }
}
