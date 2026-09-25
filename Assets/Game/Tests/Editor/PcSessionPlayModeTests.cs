using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GoLive.Desktop;
using GoLive.Interaction;
using GoLive.Inventory;
using GoLive.Localization;
using GoLive.PcBuilding;
using GoLive.Phone;
using GoLive.Player;
using GoLive.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GoLive.Tests
{
    public sealed class PcSessionPlayModeTests
    {
        private SceneSetup[] _previousScenes;
        private VirtualInput _input;
        private PcSessionBehaviour _session;
        private PlayerController _player;
        private Camera _camera;

        [OneTimeSetUp]
        public void OpenScene()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty && SceneManager.GetSceneAt(i).rootCount > 0)
                    Assert.Ignore("Save the open scene before running the PC session fixture.");
            _previousScenes = EditorSceneManager.GetSceneManagerSetup();
            EditorSceneManager.OpenScene("Assets/Game/Scenes/GL.unity", OpenSceneMode.Single);
        }

        [OneTimeTearDown]
        public void RestoreScene() => SaveTestWorld.RestoreScene(_previousScenes);

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _input?.Dispose();
            _input = null;
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RepeatedSeatFocusAndBackRestoreCameraWorldPoseAndCursor()
        {
            yield return new EnterPlayMode(false);
            yield return Boot();
            Vector3 cameraLocal = _camera.transform.localPosition;
            Quaternion cameraRotation = _camera.transform.localRotation;
            float fov = _camera.fieldOfView;
            PlayerPoseSnapshot world = _player.CapturePose();
            CursorLockMode cursor = Cursor.lockState;
            bool visible = Cursor.visible;
            Assert.That(_session.TryTogglePower(), Is.True);
            _session.Session.Tick(1.5f);
            _session.ToggleMonitor();

            for (int i = 0; i < 3; i++)
            {
                Assert.That(_session.TrySit(), Is.True);
                Assert.That(_player.Controls.IsAllowed(PlayerControlMask.All), Is.False);
                yield return PlayModeWait.Frames(3);
                Assert.That(_session.TryFocus(), Is.True);
                Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
                Assert.That(Cursor.visible, Is.True);
                Assert.That(_session.CaptureWorldPose().Position, Is.EqualTo(world.Position));
                Assert.That(_session.HandleBack(), Is.True);
                Assert.That(_session.Session.Usage, Is.EqualTo(PcUsageState.Seated));
                Assert.That(_session.HandleBack(), Is.True);
                AssertRestored(cameraLocal, cameraRotation, fov, world, cursor, visible);
            }
        }

        [UnityTest]
        public IEnumerator DisableResetsStateAndRestoresOnlyItsControlOwnership()
        {
            yield return new EnterPlayMode(false);
            yield return Boot();
            Vector3 local = _camera.transform.localPosition;
            Quaternion rotation = _camera.transform.localRotation;
            float fov = _camera.fieldOfView;
            PlayerPoseSnapshot pose = _player.CapturePose();
            CursorLockMode cursor = Cursor.lockState;
            bool visible = Cursor.visible;
            Assert.That(_session.TrySit(), Is.True);
            using (IDisposable anotherOwner = _player.Controls.Block(PlayerControlMask.Jump))
            {
                _session.enabled = false;
                Assert.That(_session.Session.Usage, Is.EqualTo(PcUsageState.Standing));
                Assert.That(_session.Session.Power, Is.EqualTo(PcPowerState.Off));
                Assert.That(_session.Session.MonitorOn, Is.False);
                Assert.That(_player.Controls.IsAllowed(PlayerControlMask.Jump), Is.False, "another owner keeps its block");
                Assert.That(_player.Controls.IsAllowed(PlayerControlMask.Movement), Is.True);
            }
            AssertRestored(local, rotation, fov, pose, cursor, visible);
            _session.enabled = true;
            Assert.That(_session.TrySit(), Is.True, "re-enable binds exactly one session lifecycle");
            _session.Reset();
            AssertRestored(local, rotation, fov, pose, cursor, visible);
        }

        [UnityTest]
        public IEnumerator EscapeLeavesDesktopThenSeatWithoutOpeningPauseAndBlocksPhoneInventory()
        {
            yield return new EnterPlayMode(false);
            yield return Boot();
            _session.TryTogglePower();
            _session.Session.Tick(1.5f);
            _session.ToggleMonitor();
            _session.TrySit();
            _session.TryFocus();
            yield return Press(Key.F);
            Assert.That(_session.Session.MonitorOn, Is.True, "F belongs to desktop text input when focused");
            yield return Press(Key.Q);
            yield return Press(Key.Tab);
            Assert.That(Object.FindAnyObjectByType<PhoneBehaviour>(FindObjectsInactive.Include).IsOpen, Is.False);
            Assert.That(Object.FindAnyObjectByType<InventoryUiController>(FindObjectsInactive.Include).IsOpen, Is.False);
            yield return Press(Key.Escape);
            Assert.That(_session.Session.Usage, Is.EqualTo(PcUsageState.Seated));
            Assert.That(Object.FindAnyObjectByType<GamePauseController>(FindObjectsInactive.Include).IsPaused, Is.False);
            yield return Press(Key.F);
            Assert.That(_session.Session.MonitorOn, Is.False, "F toggles the monitor while seated");
            yield return Press(Key.Escape);
            Assert.That(_session.Session.Usage, Is.EqualTo(PcUsageState.Standing));
            Assert.That(Object.FindAnyObjectByType<GamePauseController>(FindObjectsInactive.Include).IsPaused, Is.False);
        }

        [UnityTest]
        public IEnumerator MonitorCanBeFocusedByMouseAfterClosingThePhone()
        {
            yield return new EnterPlayMode(false);
            yield return Boot();
            PhoneBehaviour phone = Object.FindAnyObjectByType<PhoneBehaviour>(FindObjectsInactive.Include);
            yield return Press(Key.Q);
            yield return PlayModeWait.Until(() => phone.PresentationState == PhonePresentationState.Held, "the phone to be held and interactive");
            Assert.That(phone.IsInteractive, Is.True);
            yield return Press(Key.Q);
            yield return PlayModeWait.Until(() => phone.PresentationState == PhonePresentationState.Hidden, "the phone to finish closing");
            Assert.That(phone.IsOpen, Is.False);

            Assert.That(_session.TryTogglePower(), Is.True);
            _session.Session.Tick(1.5f);
            _session.ToggleMonitor();
            Assert.That(_session.TrySit(), Is.True);
            Transform anchor = Field<Transform>(_session, "seatViewAnchor");
            yield return PlayModeWait.Until(() => Vector3.Distance(_camera.transform.position, anchor.position) < 0.001f &&
                Quaternion.Angle(_camera.transform.rotation, anchor.rotation) < 0.1f, "the seated camera to face the monitor");
            Ray center = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
            Assert.That(Physics.Raycast(center, out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Ignore), Is.True);
            Assert.That(hit.collider, Is.EqualTo(Field<Collider>(_session, "monitorCollider")), "the player is aiming at the authored monitor");

            Vector2 point = new(Screen.width / 2f, Screen.height / 2f);
            InputSystem.QueueStateEvent(_input.Mouse, new MouseState { position = point, buttons = 1 });
            yield return PlayModeWait.Frames(2);
            InputSystem.QueueStateEvent(_input.Mouse, new MouseState { position = point });
            yield return PlayModeWait.Frames(2);
            Assert.That(_session.Session.Usage, Is.EqualTo(PcUsageState.Focused), "closing the phone must not disable monitor mouse input");
        }

        [UnityTest]
        public IEnumerator HardwareAndMonitorPowerAreDistinctAndBuildStopsRunningPc()
        {
            yield return new EnterPlayMode(false);
            yield return Boot();
            PcScreenView screen = Object.FindAnyObjectByType<PcScreenView>();
            Assert.That(screen.IsOn, Is.False, "new game starts with both switches off");
            _session.ToggleMonitor();
            Assert.That(screen.IsOn, Is.False);
            _session.TryTogglePower();
            _session.Session.Tick(1.5f);
            Assert.That(screen.IsOn, Is.True);
            _session.TrySit();
            _session.TryFocus();
            _session.TryTogglePower();
            Assert.That(screen.IsOn, Is.False);
            Assert.That(_session.Session.Usage, Is.EqualTo(PcUsageState.Seated));
            Assert.That(_session.Session.MonitorOn, Is.True);
            _session.HandleBack();
            _session.TryTogglePower();
            _session.Session.Tick(1.5f);
            Assert.That(Object.FindAnyObjectByType<PcWorkbenchBehaviour>().Open(), Is.True);
            Assert.That(_session.Session.Power, Is.EqualTo(PcPowerState.Off));
            Assert.That(_session.Session.Usage, Is.EqualTo(PcUsageState.Standing));
        }

        [UnityTest]
        public IEnumerator LosingApplicationFocusLeavesDesktopButKeepsSeatAndPower()
        {
            yield return new EnterPlayMode(false);
            yield return Boot();
            _session.TryTogglePower();
            _session.Session.Tick(1.5f);
            _session.ToggleMonitor();
            _session.TrySit();
            _session.TryFocus();
            _session.SendMessage("OnApplicationFocus", false, SendMessageOptions.RequireReceiver);
            Assert.That(_session.Session.Usage, Is.EqualTo(PcUsageState.Seated));
            Assert.That(_session.Session.Power, Is.EqualTo(PcPowerState.Running));
            Assert.That(_session.HandleBack(), Is.True);
            Assert.That(_player.Controls.IsAllowed(PlayerControlMask.All), Is.True);
        }

        private IEnumerator Boot()
        {
            _input = new VirtualInput(withMouse: true);
            yield return PlayModeWait.Frames(10);
            _session = Object.FindAnyObjectByType<PcSessionBehaviour>();
            Assert.That(_session, Is.Not.Null, "GL has an explicitly authored PC session");
            Assert.That(_session.isActiveAndEnabled, Is.True);
            _player = Field<PlayerController>(_session, "playerController");
            _camera = Field<Camera>(_session, "playerCamera");
        }

        [UnityTest]
        public IEnumerator PowerRejectionShowsBothLocalizedBlockersAndEmitsOnce()
        {
            yield return new EnterPlayMode(false);
            yield return Boot();
            PcAssemblyBehaviour pc = Field<PcAssemblyBehaviour>(_session, "pc");
            Assert.That(pc.Assembly.TryRecordRemoval("cpu-0", out _), Is.True);
            Assert.That(pc.Assembly.TryRecordRemoval("ram-0", out _), Is.True);
            PcSessionFeedbackView feedback = Object.FindAnyObjectByType<PcSessionFeedbackView>(FindObjectsInactive.Include);
            Assert.That(feedback, Is.Not.Null, "GL has a feedback presenter wired to the session");
            LocalizationContext localization = Field<LocalizationContext>(feedback, "localization");
            TMP_Text message = Field<TMP_Text>(feedback, "message");
            GameObject panel = Field<GameObject>(feedback, "panel");
            IReadOnlyList<PcDiagnostic> diagnostics = null;
            int fullFeedback = 0;
            void Capture(IReadOnlyList<PcDiagnostic> value) { diagnostics = value; fullFeedback++; }
            _session.PowerOnRejected += Capture;
            try
            {
                foreach (GameLanguage language in new[] { GameLanguage.Russian, GameLanguage.English })
                {
                    localization.SetLanguage(language);
                    fullFeedback = 0;
                    Assert.That(_session.TryTogglePower(), Is.False);
                    Assert.That(fullFeedback, Is.EqualTo(1));
                    Assert.That(diagnostics.Count(value => value.Severity == PcDiagnosticSeverity.Blocker), Is.EqualTo(2));
                    Assert.That(panel.activeInHierarchy, Is.True, "the rejected power request is visible to the player");
                    foreach (PcDiagnosticCode code in new[] { PcDiagnosticCode.MissingCpu, PcDiagnosticCode.MissingMemory })
                    {
                        PcDiagnostic diagnostic = PcDiagnostic.For(code);
                        Assert.That(message.text, Does.Contain(localization.Text(diagnostic.TitleKey)), $"{language}: the missing part is named");
                        Assert.That(message.text, Does.Contain(localization.Text(diagnostic.DetailKey)), $"{language}: its consequence is explained");
                    }
                    Assert.That(_session.Session.Power, Is.EqualTo(PcPowerState.Off));
                }
            }
            finally
            {
                _session.PowerOnRejected -= Capture;
            }
        }

        private IEnumerator Press(Key key)
        {
            InputSystem.QueueStateEvent(_input.Keyboard, new KeyboardState(key));
            yield return PlayModeWait.Frames(2);
            InputSystem.QueueStateEvent(_input.Keyboard, new KeyboardState());
            yield return PlayModeWait.Frames(2);
        }

        private void AssertRestored(Vector3 local, Quaternion rotation, float fov, PlayerPoseSnapshot world, CursorLockMode cursor, bool visible)
        {
            Assert.That(Vector3.Distance(_camera.transform.localPosition, local), Is.LessThan(0.00001f));
            Assert.That(Quaternion.Angle(_camera.transform.localRotation, rotation), Is.LessThan(0.001f));
            Assert.That(_camera.fieldOfView, Is.EqualTo(fov));
            Assert.That(Vector3.Distance(_player.CapturePose().Position, world.Position), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(_player.CapturePose().Rotation, world.Rotation), Is.LessThan(0.001f));
            Assert.That(_player.Controls.IsAllowed(PlayerControlMask.All), Is.True);
            Assert.That(Cursor.lockState, Is.EqualTo(cursor));
            Assert.That(Cursor.visible, Is.EqualTo(visible));
        }

        private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    }
}
