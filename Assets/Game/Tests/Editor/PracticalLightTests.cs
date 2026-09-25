using System.Reflection;
using GoLive.GameTime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace GoLive.Tests
{
    // A lamp is authored at full level (each light's full intensity, each glowing part's emission) and a level only
    // scales those values: nothing is read back from the lights, so no runtime copy can disagree with the lamp's lights.
    public sealed class PracticalLightTests
    {
        private const string DeskLampPrefab = "Assets/Game/Prefab/Lighting/DeskLamp.prefab";
        private const string CeilingLightPrefab = "Assets/Game/Prefab/Lighting/CeilingLight_Globe.prefab";

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private GameObject _root;
        private Light _main;
        private Light _fill;
        private MeshRenderer _glass;
        private PracticalLight _lamp;

        [SetUp]
        public void CreateLamp()
        {
            _root = new GameObject("Test lamp");
            _main = AddLight("Main", 7f);
            _fill = AddLight("Fill", 7f);
            _glass = GameObject.CreatePrimitive(PrimitiveType.Quad).GetComponent<MeshRenderer>();
            _glass.transform.SetParent(_root.transform);
            _lamp = _root.AddComponent<PracticalLight>();

            SerializedObject lamp = new(_lamp);
            SetLights(lamp, (_main, 2f), (_fill, 0.5f));
            SerializedProperty parts = lamp.FindProperty("glowingParts");
            parts.arraySize = 1;
            parts.GetArrayElementAtIndex(0).FindPropertyRelative("renderer").objectReferenceValue = _glass;
            parts.GetArrayElementAtIndex(0).FindPropertyRelative("fullEmission").colorValue = new Color(4f, 3f, 2f, 1f);
            lamp.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void DestroyLamp()
        {
            Object.DestroyImmediate(_root);
        }

        [Test]
        public void ALevelScalesEveryLightsFullIntensityAndTheGlowTogether()
        {
            _lamp.SetLevel(0.5f);

            Assert.That(_main.intensity, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(_fill.intensity, Is.EqualTo(0.25f).Within(1e-5f));
            Assert.That(_main.enabled && _fill.enabled, Is.True);
            AssertEmission(new Color(2f, 1.5f, 1f));
            Assert.That(_lamp.Level, Is.EqualTo(0.5f));
        }

        [Test]
        public void ZeroSwitchesEveryLightOff()
        {
            _lamp.SetLevel(0f);

            Assert.That(_main.enabled, Is.False);
            Assert.That(_fill.enabled, Is.False);
            AssertEmission(Color.black);
        }

        [Test]
        public void OneIsTheAuthoredLampWhateverTheLightsSaidBefore()
        {
            _lamp.SetLevel(0.3f);
            _main.intensity = 42f;
            _fill.intensity = 0f;

            _lamp.SetLevel(1f);

            Assert.That(_main.intensity, Is.EqualTo(2f).Within(1e-5f));
            Assert.That(_fill.intensity, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(_main.enabled && _fill.enabled, Is.True);
            AssertEmission(new Color(4f, 3f, 2f));
        }

        [Test]
        public void LevelsOutsideZeroToOneAreClamped()
        {
            _lamp.SetLevel(3f);
            Assert.That(_main.intensity, Is.EqualTo(2f).Within(1e-5f));

            _lamp.SetLevel(-1f);
            Assert.That(_main.enabled, Is.False);
            Assert.That(_lamp.Level, Is.EqualTo(0f));
        }

        // The regression: the lamp's light list changing after a level was applied (edited during play, or configured
        // after the component woke up) used to index a stale copy of the intensities.
        [Test]
        public void ChangingTheLightsAfterALevelNeverMismatches()
        {
            _lamp.SetLevel(0.5f);

            Light extra = AddLight("Extra", 3f);
            SerializedObject lamp = new(_lamp);
            SetLights(lamp, (_main, 2f), (_fill, 0.5f), (extra, 4f));
            lamp.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(() => _lamp.SetLevel(0.25f), Throws.Nothing);
            Assert.That(extra.intensity, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(_main.intensity, Is.EqualTo(0.5f).Within(1e-5f));

            SetLights(lamp, (extra, 4f));
            lamp.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(() => _lamp.SetLevel(1f), Throws.Nothing);
            Assert.That(extra.intensity, Is.EqualTo(4f).Within(1e-5f));
        }

        [Test]
        public void ALampConfiguredAfterItsFirstLevelStillWorks()
        {
            GameObject bare = new("Bare lamp");

            try
            {
                PracticalLight late = bare.AddComponent<PracticalLight>();

                LogAssert.Expect(LogType.Error, $"PracticalLight on {bare.name} has a missing light or glowing part, or an invalid full intensity: its level is not applied.");
                Assert.That(() => late.SetLevel(1f), Throws.Nothing);
                Assert.That(late.IsConfigured(), Is.False, "a lamp with nothing to light is not configured");

                Light light = AddLight("Late", 9f);
                light.transform.SetParent(bare.transform);
                SerializedObject lamp = new(late);
                SetLights(lamp, (light, 3f));
                lamp.ApplyModifiedPropertiesWithoutUndo();

                late.SetLevel(0.5f);

                Assert.That(light.intensity, Is.EqualTo(1.5f).Within(1e-5f));
                Assert.That(late.Level, Is.EqualTo(0.5f));
            }
            finally
            {
                Object.DestroyImmediate(bare);
            }
        }

        [Test]
        public void DisablingAndEnablingKeepsApplyingTheAuthoredValues()
        {
            for (int i = 0; i < 3; i++)
            {
                _root.SetActive(false);
                _root.SetActive(true);
                _lamp.SetLevel(i % 2 == 0 ? 0.5f : 1f);
            }

            Assert.That(_main.intensity, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(_fill.intensity, Is.EqualTo(0.25f).Within(1e-5f));
        }

        [Test]
        public void AnEditedLampAppliesItsNewValuesAtTheSameLevel()
        {
            _lamp.SetLevel(0.5f);

            SerializedObject lamp = new(_lamp);
            SetLights(lamp, (_main, 6f), (_fill, 0.5f));
            lamp.ApplyModifiedPropertiesWithoutUndo();
            typeof(PracticalLight).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_lamp, null);

            _lamp.SetLevel(0.5f);

            Assert.That(_main.intensity, Is.EqualTo(3f).Within(1e-5f));
        }

        [TestCase("missing light")]
        [TestCase("negative intensity")]
        [TestCase("NaN intensity")]
        [TestCase("infinite intensity")]
        [TestCase("missing glowing part")]
        public void MalformedBindingIsRejectedWithoutTouchingAnyLight(string problem)
        {
            SerializedObject lamp = new(_lamp);
            SerializedProperty main = lamp.FindProperty("lights").GetArrayElementAtIndex(0);

            switch (problem)
            {
                case "missing light": main.FindPropertyRelative("light").objectReferenceValue = null; break;
                case "negative intensity": main.FindPropertyRelative("fullIntensity").floatValue = -1f; break;
                case "NaN intensity": main.FindPropertyRelative("fullIntensity").floatValue = float.NaN; break;
                case "infinite intensity": main.FindPropertyRelative("fullIntensity").floatValue = float.PositiveInfinity; break;
                default: lamp.FindProperty("glowingParts").GetArrayElementAtIndex(0).FindPropertyRelative("renderer").objectReferenceValue = null; break;
            }

            lamp.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(_lamp.IsConfigured(), Is.False);

            LogAssert.Expect(LogType.Error, $"PracticalLight on {_root.name} has a missing light or glowing part, or an invalid full intensity: its level is not applied.");
            _lamp.SetLevel(0.5f);
            _lamp.SetLevel(0.75f);

            Assert.That(_fill.intensity, Is.EqualTo(7f), "nothing is applied, and the problem is reported once");
            Assert.That(_lamp.Level, Is.EqualTo(0f));
        }

        [TestCase(DeskLampPrefab, new[] { 2.4f, 0.5f, 0.2f })]
        [TestCase(CeilingLightPrefab, new[] { 2.2f })]
        public void LampPrefabsKeepTheirAuthoredIntensities(string prefabPath, float[] expected)
        {
            PracticalLight lamp = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath).GetComponentInChildren<PracticalLight>(true);
            SerializedProperty lights = new SerializedObject(lamp).FindProperty("lights");

            Assert.That(lamp.IsConfigured(), Is.True);
            Assert.That(lights.arraySize, Is.EqualTo(expected.Length));

            for (int i = 0; i < expected.Length; i++)
            {
                SerializedProperty binding = lights.GetArrayElementAtIndex(i);
                Light light = (Light)binding.FindPropertyRelative("light").objectReferenceValue;

                Assert.That(binding.FindPropertyRelative("fullIntensity").floatValue, Is.EqualTo(expected[i]).Within(1e-5f), $"{prefabPath} light {i}");
                Assert.That(light.intensity, Is.EqualTo(expected[i]).Within(1e-5f), "the migration took the intensity the light was authored with");
            }
        }

        private Light AddLight(string name, float intensity)
        {
            Light light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(_root != null ? _root.transform : null);
            light.intensity = intensity;
            return light;
        }

        private static void SetLights(SerializedObject lamp, params (Light Light, float FullIntensity)[] bindings)
        {
            SerializedProperty lights = lamp.FindProperty("lights");
            lights.arraySize = bindings.Length;

            for (int i = 0; i < bindings.Length; i++)
            {
                SerializedProperty binding = lights.GetArrayElementAtIndex(i);
                binding.FindPropertyRelative("light").objectReferenceValue = bindings[i].Light;
                binding.FindPropertyRelative("fullIntensity").floatValue = bindings[i].FullIntensity;
            }
        }

        private void AssertEmission(Color expected)
        {
            MaterialPropertyBlock block = new();
            _glass.GetPropertyBlock(block);
            Color emission = block.GetColor(EmissionColorId);

            Assert.That(emission.r, Is.EqualTo(expected.r).Within(1e-4f));
            Assert.That(emission.g, Is.EqualTo(expected.g).Within(1e-4f));
            Assert.That(emission.b, Is.EqualTo(expected.b).Within(1e-4f));
        }
    }
}
