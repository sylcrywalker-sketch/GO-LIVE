using System.Collections;
using System.Linq;
using GoLive.GameTime;
using GoLive.Items;
using GoLive.PcBuilding;
using GoLive.Player;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GoLive.Tests
{
    // The apartment's light across the day in the real GL scene: which lamps are on when, the night look, the sunlight
    // bouncing off the floor, the monitor that follows the PC's hardware and the Workbench light. Nothing here saves.
    public sealed class ApartmentLightingPlayModeTests
    {
        private const string GameScene = "Assets/Game/Scenes/GL.unity";
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private SceneSetup[] _previousScenes;
        private GameClockBehaviour _clock;
        private PracticalLight _deskLamp;
        private PracticalLight _roomLight;
        private PracticalLight _kitchenLight;
        private PracticalLight _hallwayLight;
        private Volume _nightGrade;
        private WindowSunBounce _sunBounce;
        private PcScreenView _screen;

        [OneTimeSetUp]
        public void OpenGameScene()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);

                if (scene.isDirty && scene.rootCount > 0)
                    Assert.Ignore("Save your open scene before running the GL lighting fixture; unsaved scene work will not be closed.");
            }

            _previousScenes = EditorSceneManager.GetSceneManagerSetup();
            EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Single);
        }

        [OneTimeTearDown]
        public void RestoreEditorSceneSetup()
        {
            SaveTestWorld.RestoreScene(_previousScenes);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Application.isPlaying)
                yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator TheNewGameMorningIsSunlitWithEveryLampOff()
        {
            yield return new EnterPlayMode(false);
            yield return Boot();

            Assert.That(_clock.Clock.Current.Hour, Is.EqualTo(7), "a new game starts in the morning");
            AssertLevels(desk: 0f, room: 0f, hallway: 0f, "morning");
            Assert.That(_nightGrade.weight, Is.EqualTo(0f), "no night grade by day");

            Assert.That(_sunBounce.SunlitShare, Is.GreaterThan(0.5f), "the morning sun comes in through the window");
            Light bounce = _sunBounce.GetComponentInChildren<Light>(true);
            Assert.That(bounce.enabled && bounce.intensity > 0f, Is.True, "the sun patch lights the room again");
            Assert.That(bounce.transform.position.x, Is.InRange(7.7f, 12.55f - 0.6f), "the bounce hangs inside the room, off the window wall");
            Assert.That(bounce.transform.position.z, Is.InRange(-21f, -12.8f));

            Assert.That(_screen.IsOn, Is.True, "the starter PC reaches a desktop, so its monitor is on");
        }

        [UnityTest]
        public IEnumerator LampsStayOffThroughTheAfternoonAndComeInAtDusk()
        {
            yield return new EnterPlayMode(false);
            yield return Boot();

            yield return AdvanceTo(12, 0);
            AssertLevels(desk: 0f, room: 0f, hallway: 0f, "at noon");
            Assert.That(_nightGrade.weight, Is.EqualTo(0f), "no night grade at noon");

            yield return AdvanceTo(15, 0);
            AssertLevels(desk: 0f, room: 0f, hallway: 0f, "at 15:00");
            Assert.That(_sunBounce.SunlitShare, Is.EqualTo(0f), "the courtyard wall hides the afternoon sun");
            Assert.That(_sunBounce.GetComponentInChildren<Light>(true).enabled, Is.False, "no sun patch, no bounce");

            yield return AdvanceTo(17, 0);
            Assert.That(_deskLamp.Level, Is.InRange(0.05f, 0.95f), "the lamps come in over the late afternoon");

            yield return AdvanceTo(18, 0);
            AssertLevels(desk: 1f, room: 1f, hallway: 1f, "at 18:00");
        }

        [UnityTest]
        public IEnumerator TheNightIsLitByTheDeskLampAndTheScreenAndHoldsUntilDawn()
        {
            yield return new EnterPlayMode(false);
            yield return Boot();

            yield return AdvanceTo(23, 0);
            AssertLevels(desk: 1f, room: 0f, hallway: 0.5f, "at 23:00");
            Assert.That(_nightGrade.weight, Is.EqualTo(1f), "the night grade is in");
            Assert.That(_deskLamp.GetComponentsInChildren<Light>(true).All(light => light.enabled), Is.True, "the desk lamp's lights are on");
            Assert.That(_roomLight.GetComponentsInChildren<Light>(true).Any(light => light.enabled), Is.False, "the ceiling light is off");
            Assert.That(_screen.IsOn, Is.True);

            yield return AdvanceTo(2, 30);
            AssertLevels(desk: 1f, room: 0f, hallway: 0.5f, "at 02:30");
            Assert.That(_nightGrade.weight, Is.EqualTo(1f), "the dark hours keep the full night look");

            yield return AdvanceTo(5, 0);
            Assert.That(_nightGrade.weight, Is.InRange(0.05f, 0.95f), "dawn comes in over the end of the night");
        }

        // Each lamp at full level is the intensity it was authored with, per instance: the three ceiling globes share a
        // prefab but not a brightness.
        [UnityTest]
        public IEnumerator EveryLampShinesWithItsOwnAuthoredIntensity()
        {
            yield return new EnterPlayMode(false);
            yield return Boot();

            yield return AdvanceTo(18, 0);
            AssertLevels(desk: 1f, room: 1f, hallway: 1f, "at 18:00");
            AssertIntensities(_kitchenLight, 1.8f);
            AssertIntensities(_roomLight, 2.4f);
            AssertIntensities(_hallwayLight, 1.6f);
            AssertIntensities(_deskLamp, 2.4f, 0.5f, 0.2f);

            yield return AdvanceTo(23, 0);
            AssertIntensities(_hallwayLight, 0.8f);
            AssertIntensities(_kitchenLight, 0.9f);
        }

        [UnityTest]
        public IEnumerator TheMonitorGoesDarkWhenThePcCanNotReachADesktop()
        {
            yield return new EnterPlayMode(false);
            yield return Boot();

            PcAssemblyBehaviour pc = Object.FindAnyObjectByType<PcAssemblyBehaviour>();
            PlayerCarry carry = Object.FindAnyObjectByType<PlayerCarry>();
            Renderer monitor = Field<Renderer>(_screen, "screen");
            Light glow = Field<Light[]>(_screen, "glow").Single();
            PcComponentSlot memory = pc.Slots.First(slot => pc.TryGetInstalledItem(slot, out WorldItem item) && item.Definition.PcComponent.ComponentType == PcComponentType.Ram);

            AssertScreen(monitor, glow, on: true, "with the starter parts");

            Assert.That(pc.TryRemoveToCarry(memory, carry), Is.True);
            Assert.That(pc.Capabilities.CanUseDesktop, Is.False);
            AssertScreen(monitor, glow, on: false, "without memory");

            Assert.That(pc.TryInstallCarried(memory, carry), Is.True);
            AssertScreen(monitor, glow, on: true, "with the memory back");
            yield return PlayModeWait.Frames(1);
        }

        [UnityTest]
        public IEnumerator TheWorkbenchLightFollowsTheBuildView()
        {
            yield return new EnterPlayMode(false);
            yield return Boot();

            PcWorkbenchBehaviour workbench = Object.FindAnyObjectByType<PcWorkbenchBehaviour>();
            Light workLight = Field<Light>(workbench, "workLight");
            Assert.That(workLight, Is.Not.Null, "GL gives the Workbench its light");
            Assert.That(workLight.enabled, Is.False, "off in normal play");

            Assert.That(workbench.Open(), Is.True);
            yield return PlayModeWait.Until(() => workbench.Phase == PcBuildModePhase.Open, "the build view to open");
            yield return PlayModeWait.Frames(1);
            Assert.That(workLight.enabled && workLight.intensity > 0f, Is.True, "the open case is lit");

            workbench.Close();
            yield return PlayModeWait.Until(() => workbench.Phase == PcBuildModePhase.Closed, "the build view to close");
            Assert.That(workLight.enabled, Is.False, "off again once the player is back");
        }

        private IEnumerator Boot()
        {
            yield return PlayModeWait.Frames(10);

            _clock = Object.FindAnyObjectByType<GameClockBehaviour>();
            DayLightingView view = Object.FindAnyObjectByType<DayLightingView>();
            Assert.That(view != null && view.isActiveAndEnabled, Is.True, "DayLightingView is wired and running");

            _deskLamp = Field<PracticalLight[]>(view, "deskLamps").Single();
            _roomLight = Field<PracticalLight[]>(view, "roomLights").Single();
            PracticalLight[] lamps = Field<PracticalLight[]>(view, "lamps");
            _kitchenLight = lamps.Single(lamp => lamp.name.StartsWith("Kitchen"));
            _hallwayLight = lamps.Single(lamp => lamp.name.StartsWith("Hallway"));
            _nightGrade = Field<Volume>(view, "nightGrade");
            _sunBounce = Object.FindAnyObjectByType<WindowSunBounce>();
            _screen = Object.FindAnyObjectByType<PcScreenView>();
            Assert.That(_sunBounce != null && _sunBounce.isActiveAndEnabled && _screen != null && _screen.isActiveAndEnabled, Is.True);
        }

        private IEnumerator AdvanceTo(int hour, int minute)
        {
            long minutes = (hour * 60 + minute - _clock.Clock.Current.MinuteOfDay + 24 * 60) % (24 * 60);
            _clock.AdvanceMinutes(minutes);
            yield return PlayModeWait.Frames(2);
        }

        private void AssertLevels(float desk, float room, float hallway, string when)
        {
            Assert.That(_deskLamp.Level, Is.EqualTo(desk).Within(0.01f), $"desk lamp {when}");
            Assert.That(_roomLight.Level, Is.EqualTo(room).Within(0.01f), $"room ceiling light {when}");
            Assert.That(_hallwayLight.Level, Is.EqualTo(hallway).Within(0.01f), $"hallway light {when}");
            Assert.That(_kitchenLight.Level, Is.EqualTo(hallway).Within(0.01f), $"kitchen light {when}");
        }

        private static void AssertIntensities(PracticalLight lamp, params float[] expected)
        {
            UnityEditor.SerializedProperty lights = new UnityEditor.SerializedObject(lamp).FindProperty("lights");
            Assert.That(lights.arraySize, Is.EqualTo(expected.Length), lamp.name);

            for (int i = 0; i < expected.Length; i++)
            {
                Light light = (Light)lights.GetArrayElementAtIndex(i).FindPropertyRelative("light").objectReferenceValue;
                Assert.That(light.intensity, Is.EqualTo(expected[i]).Within(1e-4f), $"{lamp.name} light {i}");
                Assert.That(light.enabled, Is.True, $"{lamp.name} light {i} is on");
            }
        }

        private void AssertScreen(Renderer monitor, Light glow, bool on, string when)
        {
            MaterialPropertyBlock block = new();
            monitor.GetPropertyBlock(block);
            Color emission = block.GetColor(EmissionColorId);

            Assert.That(_screen.IsOn, Is.EqualTo(on), $"screen state {when}");
            Assert.That(glow.enabled, Is.EqualTo(on), $"screen glow {when}");
            Assert.That(emission.maxColorComponent > 0f, Is.EqualTo(on), $"screen emission {when}");
        }

        private static T Field<T>(object owner, string name)
        {
            return (T)owner.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(owner);
        }
    }
}
