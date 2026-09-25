using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using GoLive.Delivery;
using GoLive.Desktop;
using GoLive.GameTime;
using GoLive.Inventory;
using GoLive.Items;
using GoLive.Localization;
using GoLive.PcBuilding;
using GoLive.Player;
using GoLive.Shop;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace GoLive.Tests
{
    public sealed partial class DesktopFlowPlayModeTests
    {
        [UnityTest, Timeout(360000)]
        public IEnumerator PhysicalPeripheralsAndSevenBilingualReadinessStates()
        {
            LogAssert.Expect(LogType.Warning, new Regex("^" + Regex.Escape(
                "BoxCollider does not support negative scale or size.\n" +
                "The effective box size has been forced positive and is likely to give unexpected collision geometry.\n" +
                "If you absolutely need to use negative scaling you can use the convex MeshCollider. Scene hierarchy path \"Props/SM_Shelf_001\"") + "$"));
            yield return new EnterPlayMode(false);
            yield return Boot();
            var rig = _runtime.PeripheralRig;
            var carry = _player.GetComponent<PlayerCarry>();
            var inventory = _player.GetComponent<PlayerInventory>();
            var micSocket = Field<PcPeripheralSocket>(rig, "microphoneSocket");
            var camSocket = Field<PcPeripheralSocket>(rig, "webcamSocket");
            Assert.That(rig.State.HasMicrophone, Is.True, "new-game scene explicitly installs its actual desk mic");
            Assert.That(rig.State.HasWebcam, Is.False);
            Assert.That(rig.TryGetConnectedItem(PcPeripheralKind.Microphone, out WorldItem microphone), Is.True);
            string micId = microphone.Instance.InstanceId;

            // A purchased/delivered camera still isn't connected. Use the real shop and package item.
            ShopTestData.BuyOne(One<ShopBehaviour>(), "used-webcam");
            Assert.That(rig.State.HasWebcam, Is.False, "purchase history does not create a connection");
            One<GameClockBehaviour>().Clock.AdvanceMinutes(151);
            yield return PlayModeWait.Frames(30);
            var package = Object.FindObjectsByType<DeliveryPackageBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Single(item => item.Item != null && item.Item.Instance != null);
            Assert.That(carry.TryCarry(package.Item), Is.True);
            yield return Press(Key.F);
            yield return PlayModeWait.Frames(3);
            WorldItem webcam = Object.FindObjectsByType<WorldItem>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(item => item.Instance != null && item.Instance.DefinitionId == "used-webcam" && item.Instance.Location == ItemLocation.World);
            Assert.That(rig.State.HasWebcam, Is.False, "the delivered item remains a loose world item");
            Assert.That(carry.TryCarry(webcam), Is.True);
            Assert.That(rig.State.HasWebcam, Is.False, "carrying equipment is not connecting it");
            yield return UsePeripheralSocket(camSocket, "pc.peripheral.connect");
            Assert.That(webcam.IsInstalled && !carry.HasItem && rig.State.HasWebcam, Is.True);
            yield return _capture.Capture("peripheral-world-connected");

            yield return FaceCase();
            yield return Press(Key.F);
            yield return PlayModeWait.Until(() => _session.Session.Power == PcPowerState.Running, "PC boots without a GPU");
            yield return FaceMonitor();
            yield return Press(Key.F);
            yield return SitAndFocus();
            Assert.That(_runtime.State.Storage.TryInstall(DesktopAppId.Streamly), Is.Null);
            Assert.That(_runtime.State.Outline.CreateAddress("peripheral.test"), Is.Null);
            Assert.That(_runtime.State.Trich.Register(_runtime.State.Outline, _runtime.State.Outline.Address), Is.Null);
            yield return OpenApp(DesktopAppId.Streamly);
            var view = One<StreamlyView>();
            Type(Field<TMP_InputField>(view, "channelCode"), _runtime.State.Trich.ChannelCode);
            Click(Field<Button>(view, "connect"));
            Click(Field<Button[]>(view, "quality")[1]);
            yield return CaptureReadinessPair("01-everything-ready", view, true, true, true, true, true);

            // Remove the actual mic through the authored E target, store it and reopen the existing window.
            yield return StandFromDesktop();
            yield return UsePeripheralSocket(micSocket, "pc.peripheral.disconnect");
            Assert.That(carry.CarriedItem, Is.SameAs(microphone));
            Assert.That(inventory.TryStoreCarriedItem(), Is.True);
            yield return SitAndFocus();
            yield return CaptureReadinessPair("02-microphone-missing", view, true, true, false, true, true);
            Assert.That(Field<TMP_Text>(view, "requirements").text, Is.EqualTo(_localization.Text("desktop.stream.microphone_missing")));
            yield return StandFromDesktop();
            Assert.That(inventory.TryTakeToCarry(micId), Is.True);
            yield return UsePeripheralSocket(micSocket, "pc.peripheral.connect");
            Assert.That(rig.TryGetConnectedItem(PcPeripheralKind.Microphone, out WorldItem reconnected), Is.True);
            Assert.That(reconnected, Is.SameAs(microphone));
            yield return SitAndFocus();

            // While this exact Streamly window is open, the game-owned upload change refreshes it immediately.
            _runtime.SetUploadMbps(0);
            Assert.That(Field<Button>(view, "startStop").interactable, Is.False);
            yield return CaptureReadinessPair("04-internet-missing", view, true, false, true, true, true);
            _runtime.SetUploadMbps(8);
            Assert.That(Field<Button>(view, "startStop").interactable, Is.True);
            Click(Field<Button[]>(view, "quality")[2]);
            yield return CaptureReadinessPair("05-unsupported-quality", view, true, true, true, true, false);
            Click(Field<Button[]>(view, "quality")[1]);
            _runtime.SetUploadMbps(5);

            yield return StartBroadcast(view);
            yield return CaptureReadinessPair("06-live-with-webcam", view, true, true, true, true, true);
            yield return StandFromDesktop();
            yield return UsePeripheralSocket(camSocket, "pc.peripheral.disconnect");
            Assert.That(carry.CarriedItem, Is.SameAs(webcam));
            Assert.That(inventory.TryStoreCarriedItem(), Is.True);
            Assert.That(_runtime.State.Stream.State, Is.EqualTo(StreamState.Live), "optional camera loss keeps the broadcast running");
            yield return SitAndFocus();
            yield return CaptureReadinessPair("07-live-without-webcam", view, true, true, true, false, true);
            yield return StopBroadcast(view);
            yield return CaptureReadinessPair("03-webcam-missing", view, true, true, true, false, true);
            // Camera-free preflight also allows a fresh normal start, through the same real button.
            yield return StartBroadcast(view);
            yield return StandFromDesktop();
            yield return UsePeripheralSocket(micSocket, "pc.peripheral.disconnect");
            Assert.That(_runtime.State.Stream.State, Is.EqualTo(StreamState.Offline), "mandatory mic loss aborts an active stream");
            Assert.That(carry.CarriedItem, Is.SameAs(microphone));
            Assert.That(Object.FindObjectsByType<WorldItem>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Count(item => item.Instance != null && item.Instance.InstanceId == micId), Is.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
        }

        private IEnumerator CaptureReadinessPair(string state, StreamlyView view, params bool[] expected)
        {
            StreamReadiness readiness = _runtime.State.Stream.EvaluateReadiness(_runtime.Capabilities,
                _session.Session.Power == PcPowerState.Running, _runtime.UploadMbps);
            Assert.That(new[] { readiness.ChannelReady, readiness.InternetReady, readiness.MicrophoneReady,
                readiness.WebcamReady, readiness.QualitySupported }, Is.EqualTo(expected));
            Assert.That(Field<Button>(view, "startStop").interactable,
                Is.EqualTo(_runtime.State.Stream.State == StreamState.Live || readiness.CanStart));
            var texts = Field<TMP_Text[]>(view, "readinessTexts");
            var marks = Field<DesktopGlyphGraphic[]>(view, "readinessMarks");
            var warnings = Field<DesktopGlyphGraphic[]>(view, "readinessWarnings");
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(marks[i].enabled, Is.EqualTo(expected[i]));
                Assert.That(warnings[i].enabled, Is.EqualTo(!expected[i]));
                if (!expected[i]) Assert.That(texts[i].color.g, i == 3 ? Is.GreaterThan(.7f) : Is.LessThan(.5f));
            }
            _localization.SetLanguage(GameLanguage.Russian);
            yield return CaptureApp("peripheral-ru-" + state, DesktopAppId.Streamly);
            _localization.SetLanguage(GameLanguage.English);
            yield return CaptureApp("peripheral-en-" + state, DesktopAppId.Streamly);
        }

        private IEnumerator StandFromDesktop()
        {
            if (_session.Session.Usage == PcUsageState.Focused) yield return Press(Key.Escape);
            if (_session.Session.Usage == PcUsageState.Seated) yield return Press(Key.Escape);
            Assert.That(_session.Session.Usage, Is.EqualTo(PcUsageState.Standing));
        }

        private IEnumerator UsePeripheralSocket(PcPeripheralSocket socket, string prompt)
        {
            var collider = Field<Collider>(socket, "interactionCollider");
            Transform seat = Field<Transform>(_session, "seatViewAnchor");
            Vector3 position = seat.position;
            position.y = _player.CapturePose().Position.y;
            yield return FaceTarget(position, collider.bounds.center);
            Physics.Raycast(_camera.ViewportPointToRay(new Vector3(.5f, .5f)), out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Ignore);
            TestContext.WriteLine($"PERIPHERAL_AIM {socket.Kind} body={_player.CapturePose().Position} eye={_camera.transform.position} target={collider.bounds.center} hit={(hit.collider != null ? hit.collider.name : "none")} distance={hit.distance} prompt={_interactor.Prompts.PrimaryKey}");
            if (_interactor.Prompts.PrimaryKey != prompt) yield return _capture.Capture("peripheral-world-unreachable-" + socket.Kind.ToString().ToLowerInvariant());
            Assert.That(_interactor.Prompts.PrimaryKey, Is.EqualTo(prompt), socket.Kind + " is physically reachable by the player's ray");
            yield return Press(Key.E);
        }
    }
}
