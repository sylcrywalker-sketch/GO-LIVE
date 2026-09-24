using GoLive.GameTime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GoLive.Tests
{
    public sealed class PracticalLightTests
    {
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private GameObject _root;
        private Light _light;
        private MeshRenderer _glass;
        private PracticalLight _lamp;

        [SetUp]
        public void CreateLamp()
        {
            _root = new GameObject("Test lamp");
            _light = new GameObject("Light").AddComponent<Light>();
            _light.transform.SetParent(_root.transform);
            _light.intensity = 2f;
            _glass = GameObject.CreatePrimitive(PrimitiveType.Quad).GetComponent<MeshRenderer>();
            _glass.transform.SetParent(_root.transform);
            _lamp = _root.AddComponent<PracticalLight>();

            SerializedObject lamp = new(_lamp);
            SerializedProperty lights = lamp.FindProperty("lights");
            lights.arraySize = 1;
            lights.GetArrayElementAtIndex(0).objectReferenceValue = _light;
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
        public void ALevelScalesTheAuthoredLightAndTheGlowTogether()
        {
            _lamp.SetLevel(0.5f);

            Assert.That(_light.intensity, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(_light.enabled, Is.True);
            AssertEmission(new Color(2f, 1.5f, 1f));
            Assert.That(_lamp.Level, Is.EqualTo(0.5f));
        }

        [Test]
        public void OffSwitchesTheLightOffAndKeepsTheAuthoredIntensityForLater()
        {
            _lamp.SetLevel(0f);

            Assert.That(_light.enabled, Is.False);
            AssertEmission(Color.black);

            _lamp.SetLevel(1f);

            Assert.That(_light.enabled, Is.True);
            Assert.That(_light.intensity, Is.EqualTo(2f).Within(1e-5f), "level 1 is the authored lamp, whatever came before");
            AssertEmission(new Color(4f, 3f, 2f));
        }

        [Test]
        public void LevelsOutsideZeroToOneAreClamped()
        {
            _lamp.SetLevel(3f);
            Assert.That(_light.intensity, Is.EqualTo(2f).Within(1e-5f));

            _lamp.SetLevel(-1f);
            Assert.That(_light.enabled, Is.False);
            Assert.That(_lamp.Level, Is.EqualTo(0f));
        }

        [Test]
        public void AMissingLightOrGlowingPartIsReported()
        {
            Assert.That(_lamp.IsConfigured(), Is.True);

            SerializedObject lamp = new(_lamp);
            lamp.FindProperty("glowingParts").GetArrayElementAtIndex(0).FindPropertyRelative("renderer").objectReferenceValue = null;
            lamp.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(_lamp.IsConfigured(), Is.False);
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
