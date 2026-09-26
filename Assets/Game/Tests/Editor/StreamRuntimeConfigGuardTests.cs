using System.Collections.Generic;
using System.Reflection;
using GoLive.Desktop;
using GoLive.Voice;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace GoLive.Tests
{
    // The runtime bridges accept the authored production configs silently and report a missing or invalid config
    // with its own error (the errors are never suppressed; a valid config is never reported as missing).
    public sealed class StreamRuntimeConfigGuardTests
    {
        private SceneSetup[] _previousScenes;
        private readonly List<Object> _owned = new();

        [OneTimeSetUp] public void Isolate() => _previousScenes = SaveTestWorld.IsolateScene();
        [OneTimeTearDown] public void RestoreScene() => SaveTestWorld.RestoreScene(_previousScenes);

        [TearDown]
        public void TearDown()
        {
            foreach (Object owned in _owned)
                if (owned != null) Object.DestroyImmediate(owned);
            _owned.Clear();
        }

        [Test]
        public void DesktopRuntimeAcceptsTheProductionTuningAndRejectsMissingOrInvalidTuning()
        {
            using SaveTestWorld world = SaveTestWorld.Create(0);
            Assert.That(world.Desktop.enabled, Is.True);
            Assert.That(world.Desktop.State, Is.Not.Null, "the production tuning passes the Awake guard");

            DesktopRuntimeBehaviour missing = Desktop(world, null);
            LogAssert.Expect(LogType.Error, "Audience tuning is missing.");
            Awake(missing);
            Assert.That(missing.enabled, Is.False);
            Assert.That(missing.State, Is.Null);

            var broken = Owned(ScriptableObject.CreateInstance<AudienceTuningConfig>());
            broken.Tuning.MeanWatchSeconds = 0;
            DesktopRuntimeBehaviour invalid = Desktop(world, broken);
            LogAssert.Expect(LogType.Error, broken.ValidationError);
            Awake(invalid);
            Assert.That(invalid.enabled, Is.False);
            Assert.That(invalid.State, Is.Null);
        }

        [Test]
        public void VoiceInputAcceptsTheProductionSettingsAndRejectsMissingOrInvalidSettings()
        {
            VoiceInputBehaviour valid = Voice(AssetDatabase.LoadAssetAtPath<VoiceActivityConfig>(StreamRuntimeConfigTests.VoiceActivityPath));
            Awake(valid);
            try
            {
                Assert.That(valid.enabled, Is.True);
                Assert.That(valid.Recognition, Is.Not.Null, "the production settings pass the Awake guard");
            }
            finally
            {
                valid.Recognition?.Dispose();
            }

            VoiceInputBehaviour missing = Voice(null);
            LogAssert.Expect(LogType.Error, "Voice activity settings are missing.");
            Awake(missing);
            Assert.That(missing.enabled, Is.False);
            Assert.That(missing.Recognition, Is.Null);

            var broken = Owned(ScriptableObject.CreateInstance<VoiceActivityConfig>());
            broken.Settings.MaximumPhraseMilliseconds = 500;
            VoiceInputBehaviour invalid = Voice(broken);
            LogAssert.Expect(LogType.Error, broken.ValidationError);
            Awake(invalid);
            Assert.That(invalid.enabled, Is.False);
            Assert.That(invalid.Recognition, Is.Null);
        }

        // The world's own authored dependencies with the given tuning (or none).
        private DesktopRuntimeBehaviour Desktop(SaveTestWorld world, AudienceTuningConfig tuning)
        {
            var desktop = Owned(new GameObject("Desktop runtime config guard")).AddComponent<DesktopRuntimeBehaviour>();
            foreach (string field in new[] { "pc", "session", "peripherals", "catalog", "clock", "wallet" })
                SaveTestWorld.SetField(desktop, field, Field(world.Desktop, field));
            SaveTestWorld.SetField(desktop, "audienceTuning", tuning);
            return desktop;
        }

        private VoiceInputBehaviour Voice(VoiceActivityConfig activity)
        {
            var voice = Owned(new GameObject("Voice input config guard")).AddComponent<VoiceInputBehaviour>();
            SaveTestWorld.SetField(voice, "activity", activity);
            return voice;
        }

        private T Owned<T>(T owned) where T : Object
        {
            _owned.Add(owned);
            return owned;
        }

        private static void Awake(MonoBehaviour target) =>
            target.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

        private static object Field(object owner, string name) =>
            owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    }
}
