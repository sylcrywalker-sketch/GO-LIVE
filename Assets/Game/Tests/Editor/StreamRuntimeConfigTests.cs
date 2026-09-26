using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using GoLive.Desktop;
using GoLive.Voice;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GoLive.Tests
{
    // The production GL composition carries its authored stream configs: one audience tuning asset on the one desktop
    // runtime and one voice activity asset on the one voice bridge, assigned in the scene (never created, searched for
    // or defaulted at runtime). Entering Play Mode must reach an initialized desktop, stream and voice runtime.
    public sealed class StreamRuntimeConfigTests
    {
        internal const string AudienceTuningPath = "Assets/Game/Config/Desktop/AudienceTuning.asset";
        internal const string VoiceActivityPath = "Assets/Game/Config/Voice/VoiceActivity.asset";
        internal const string ViewerCorePath = "Assets/Game/Config/Viewers/ViewerCore.asset";
        private SceneSetup[] _previousScenes;

        [OneTimeSetUp]
        public void OpenScene()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty && SceneManager.GetSceneAt(i).rootCount > 0)
                    Assert.Ignore("Save the open scene before running the GL stream config fixture.");
            _previousScenes = EditorSceneManager.GetSceneManagerSetup();
            EditorSceneManager.OpenScene("Assets/Game/Scenes/GL.unity", OpenSceneMode.Single);
        }

        [OneTimeTearDown]
        public void RestoreScene() => SaveTestWorld.RestoreScene(_previousScenes);

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [Test]
        public void ProductionConfigAssetsAreUniqueAndValid()
        {
            AudienceTuningConfig audience = OnlyAsset<AudienceTuningConfig>(AudienceTuningPath);
            Assert.That(audience.Tuning, Is.Not.Null);
            Assert.That(audience.ValidationError, Is.Null);

            Assert.That(OnlyAsset<GoLive.Viewers.ViewerCoreConfig>(ViewerCorePath).ValidationError, Is.Null);
            VoiceActivityConfig voice = OnlyAsset<VoiceActivityConfig>(VoiceActivityPath);
            Assert.That(voice.Settings, Is.Not.Null);
            Assert.That(voice.ValidationError, Is.Null);
            // Phrase and pre-roll buffers are sized from these; whisper decodes at most 30 s per phrase.
            Assert.That(voice.Settings.MaximumPhraseMilliseconds, Is.LessThanOrEqualTo(30000));
            Assert.That(voice.Settings.PreRollMilliseconds, Is.LessThanOrEqualTo(1000));
        }

        [Test]
        public void AudienceTuningKeepsTheRoadmapBaseline()
        {
            AudienceTuning tuning = Load<AudienceTuningConfig>(AudienceTuningPath).Tuning;
            Assert.That(tuning.FirstStreamInitialViewersMin, Is.GreaterThanOrEqualTo(1), "a first stream is discovered at once");
            Assert.That(tuning.FirstStreamInitialViewersMax, Is.LessThanOrEqualTo(3), "a first stream opens with about 1-3 viewers");

            // Samples at 00, 03, ..., 24 h: the peak lies inside 15:00-21:00, night is weaker but never empty.
            float[] curve = tuning.DailyCurve;
            float peak = curve.Max();
            Assert.That(Array.IndexOf(curve, peak) * 3, Is.InRange(15, 21));
            foreach (int night in new[] { 0, 1, 8 })
                Assert.That(curve[night], Is.GreaterThan(0f).And.LessThan(peak), $"{night * 3:00}:00");

            // Bounded: even the best-case brand-new channel stays in single digits (no absurd spikes).
            double bestNewChannel = tuning.DiscoveryViewers * peak * tuning.HighQualityAttraction * tuning.WebcamEngagement
                + tuning.FirstStreamBoostViewers;
            Assert.That(bestNewChannel, Is.LessThan(10));
        }

        [Test]
        public void GlAssignsTheProductionConfigsExplicitlyToTheSingleRuntimeOwners()
        {
            DesktopRuntimeBehaviour runtime = One<DesktopRuntimeBehaviour>();
            VoiceInputBehaviour voice = One<VoiceInputBehaviour>();

            Assert.That(Reference(runtime, "audienceTuning"), Is.SameAs(Load<AudienceTuningConfig>(AudienceTuningPath)));
            Assert.That(Reference(voice, "activity"), Is.SameAs(Load<VoiceActivityConfig>(VoiceActivityPath)));
            Assert.That(Reference(runtime, "viewerCore"), Is.SameAs(Load<GoLive.Viewers.ViewerCoreConfig>(ViewerCorePath)));
            Assert.That(Reference(runtime, "voice"), Is.SameAs(voice), "the desktop runtime drives the one voice bridge");
            Assert.That(Reference(One<StreamlyView>(), "voice"), Is.SameAs(voice));
            AssertEveryReferenceAssigned(runtime);
            AssertEveryReferenceAssigned(voice);
            Assert.That(runtime.enabled && runtime.gameObject.activeInHierarchy, Is.True);
            Assert.That(voice.enabled && voice.gameObject.activeInHierarchy, Is.True);
        }

        // Any error logged while entering Play Mode (such as "Audience tuning is missing.") fails this test.
        [UnityTest, Timeout(120000)]
        public IEnumerator EnteringGlInitializesTheStreamRuntimeWithTheAuthoredConfigs()
        {
            // Existing GL shelf geometry warning; every other warning/error remains unexpected.
            LogAssert.Expect(LogType.Warning, new Regex("^" + Regex.Escape(
                "BoxCollider does not support negative scale or size.\n" +
                "The effective box size has been forced positive and is likely to give unexpected collision geometry.\n" +
                "If you absolutely need to use negative scaling you can use the convex MeshCollider. Scene hierarchy path \"Props/SM_Shelf_001\"") + "$"));
            yield return new EnterPlayMode(false);
            yield return PlayModeWait.Frames(3);
            DesktopRuntimeBehaviour runtime = One<DesktopRuntimeBehaviour>();
            VoiceInputBehaviour voice = One<VoiceInputBehaviour>();

            Assert.That(runtime.enabled, Is.True);
            Assert.That(runtime.State, Is.Not.Null);
            Assert.That(voice.enabled, Is.True);
            Assert.That(voice.Recognition, Is.Not.Null);
            // The simulation and the VAD read the authored assets themselves, not a code default.
            Assert.That(Field<AudienceTuning>(runtime.State.Stream, "_tuning"), Is.SameAs(Load<AudienceTuningConfig>(AudienceTuningPath).Tuning));
            Assert.That(Field<VoiceActivitySettings>(voice.Recognition, "_vadSettings"), Is.SameAs(Load<VoiceActivityConfig>(VoiceActivityPath).Settings));
            yield return PlayModeWait.Until(() => runtime.IsReady, "the desktop runtime to bind");
            Assert.That(runtime.State.Stream.State, Is.EqualTo(StreamState.Offline));
            LogAssert.NoUnexpectedReceived();
        }

        private static T Load<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset, Is.Not.Null, path);
            return asset;
        }

        private static T OnlyAsset<T>(string path) where T : ScriptableObject
        {
            string[] paths = AssetDatabase.FindAssets("t:" + typeof(T).Name).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            Assert.That(paths, Is.EqualTo(new[] { path }), "one production " + typeof(T).Name);
            return Load<T>(path);
        }

        private static T One<T>() where T : Object
        {
            T[] objects = Object.FindObjectsByType<T>(FindObjectsInactive.Include);
            Assert.That(objects.Length, Is.EqualTo(1), "the authored scene has exactly one " + typeof(T).Name);
            return objects[0];
        }

        // The serialized scene reference itself, not a value recovered at runtime.
        private static Object Reference(Object owner, string field)
        {
            SerializedProperty property = new SerializedObject(owner).FindProperty(field);
            Assert.That(property, Is.Not.Null, owner.GetType().Name + "." + field);
            return property.objectReferenceValue;
        }

        private static void AssertEveryReferenceAssigned(Object owner)
        {
            SerializedProperty property = new SerializedObject(owner).GetIterator();
            while (property.NextVisible(true))
                if (property.propertyType == SerializedPropertyType.ObjectReference && property.name != "m_Script")
                    Assert.That(property.objectReferenceValue, Is.Not.Null, owner.GetType().Name + "." + property.propertyPath);
        }

        private static T Field<T>(object owner, string name) =>
            (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    }
}
