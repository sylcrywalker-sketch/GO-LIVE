using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using GoLive.Delivery;
using GoLive.Desktop;
using GoLive.GameTime;
using GoLive.Inventory;
using GoLive.Items;
using GoLive.Localization;
using GoLive.PcBuilding;
using GoLive.Persistence;
using GoLive.Player;
using GoLive.Shop;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace GoLive.Tests
{
    // One sequential real-scene journey owns every capture. Apps are operated through the authored controls;
    // domain Tick only accelerates deterministic live time. Save round-trip uses the actual save validator/apply.
    [Category("DesktopVisualFlow")]
    public sealed partial class DesktopFlowPlayModeTests
    {
        private SceneSetup[] _previousScenes;
        private VirtualInput _input;
        private DesktopVisualCapture _capture;
        private DesktopRuntimeBehaviour _runtime;
        private PcSessionBehaviour _session;
        private DesktopShellView _shell;
        private StreamOverlayView _overlay;
        private LocalizationContext _localization;
        private PlayerController _player;
        private PlayerInteractor _interactor;
        private PcAssemblyBehaviour _pc;
        private Camera _camera;
        private Array _presentations;
        private string _clipboard;

        [OneTimeSetUp]
        public void OpenRealScene()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty && SceneManager.GetSceneAt(i).rootCount > 0)
                    Assert.Ignore("Save the open scene before the GL desktop journey; existing scene edits are preserved.");
            _previousScenes = EditorSceneManager.GetSceneManagerSetup();
            EditorSceneManager.OpenScene("Assets/Game/Scenes/GL.unity", OpenSceneMode.Single);
        }

        [OneTimeTearDown]
        public void RestoreScenes() => SaveTestWorld.RestoreScene(_previousScenes);

        [UnityTearDown]
        public IEnumerator RestoreOwnedEditorAndInputState()
        {
            _capture?.Dispose();
            _capture = null;
            _input?.Dispose();
            _input = null;
            if (_clipboard != null) GUIUtility.systemCopyBuffer = _clipboard;
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [UnityTest, Timeout(360000)]
        public IEnumerator RealSceneFirstBroadcastSevenAppsSaveLoadAndBilingualScreenshots()
        {
            // Existing GL shelf geometry warning; every other warning/error remains unexpected.
            LogAssert.Expect(LogType.Warning, new Regex("^" + Regex.Escape(
                "BoxCollider does not support negative scale or size.\n" +
                "The effective box size has been forced positive and is likely to give unexpected collision geometry.\n" +
                "If you absolutely need to use negative scaling you can use the convex MeshCollider. Scene hierarchy path \"Props/SM_Shelf_001\"") + "$"));
            yield return new EnterPlayMode(false);
            yield return Boot();
            _localization.SetLanguage(GameLanguage.Russian);
            yield return FaceCase();
            yield return _capture.Capture("ru-01-case-power-prompt");
            Assert.That(_interactor.Prompts.ObjectNameKey, Is.EqualTo("pc.object.case"));
            Assert.That(_interactor.Prompts.WorldUseKey, Is.EqualTo("pc.power.on"));
            Assert.That(_interactor.Prompts.SpecialKey, Is.EqualTo("pc.workbench.enter"));
            yield return Press(Key.F);
            yield return PlayModeWait.Until(() => _session.Session.Power == PcPowerState.Running, "case power to boot the desktop");
            Assert.That(_session.Session.MonitorOn, Is.False);
            yield return FaceMonitor();
            yield return _capture.Capture("ru-02-monitor-off-prompt");
            // Screenshot encoding advances the scene; reacquire the monitor after capsule settling.
            yield return FaceMonitor();
            Assert.That(_interactor.Prompts.ObjectNameKey, Is.EqualTo("pc.object.monitor"));
            Assert.That(_interactor.Prompts.PrimaryKey, Is.EqualTo("pc.session.sit"));
            Assert.That(_interactor.Prompts.WorldUseKey, Is.EqualTo("pc.monitor.on"));
            yield return Press(Key.F);
            Assert.That(_session.Session.ScreenActive, Is.True);
            yield return _capture.Capture("ru-03-monitor-desktop");
            yield return SitAndFocus();
            AssertDesktopShortcutLabels();
            yield return _capture.Capture("ru-04-desktop");
            yield return CaptureStartMenu("ru-04b-start-menu-initial", 3, DesktopAppId.MyComputer);
            yield return CloseApp(DesktopAppId.MyComputer);

            yield return OpenApp(DesktopAppId.Hub);
            long usedBefore = UsedStorage();
            var hub = One<HubView>();
            var cards = Field<Array>(hub, "cards").Cast<object>().ToArray();
            Assert.That(cards.Select(card => Field<DesktopAppId>(card, "appId")), Is.EquivalentTo(new[]
            {
                DesktopAppId.Streamly, DesktopAppId.Trich, DesktopAppId.Outline, DesktopAppId.Donation, DesktopAppId.Web
            }), "Hub lists the five supported apps, including the installed browser");
            TMP_InputField appSearch = Field<TMP_InputField>(hub, "search");
            Type(appSearch, "  tRiCh  ");
            Assert.That(cards.Where(card => Field<GameObject>(card, "root").activeInHierarchy)
                .Select(card => Field<DesktopAppId>(card, "appId")), Is.EqualTo(new[] { DesktopAppId.Trich }),
                "search matches an application name regardless of casing and outer spaces");
            Assert.That(Field<GameObject>(hub, "noResults").activeInHierarchy, Is.False);
            Type(appSearch, "unavailable.application");
            Assert.That(cards.Any(card => Field<GameObject>(card, "root").activeInHierarchy), Is.False);
            Assert.That(Field<GameObject>(hub, "noResults").activeInHierarchy, Is.True);
            Assert.That(UsedStorage(), Is.EqualTo(usedBefore), "filtering the catalog does not install anything");
            Type(appSearch, "");
            Assert.That(cards.All(card => Field<GameObject>(card, "root").activeInHierarchy), Is.True);
            var optional = new[] { DesktopAppId.Outline, DesktopAppId.Trich, DesktopAppId.Streamly, DesktopAppId.Donation };
            int expectedInstallSize = optional.Sum(id => _runtime.Catalog.Apps.Single(app => app.Id == id).SizeMiB);
            foreach (DesktopAppId id in optional)
            {
                Assert.That(_runtime.State.Storage.IsInstalled(id), Is.False, id.ToString());
                object card = cards.Single(value => Field<DesktopAppId>(value, "appId") == id);
                Click(Field<Button>(card, "install"));
                yield return PlayModeWait.Frames(2);
                Assert.That(_runtime.State.Storage.IsInstalled(id), Is.True, id.ToString());
            }
            Assert.That(UsedStorage() - usedBefore, Is.EqualTo(expectedInstallSize));
            yield return CaptureApp("ru-05-hub", DesktopAppId.Hub);
            yield return CaptureStartMenu("ru-05b-start-menu-installed", 7, DesktopAppId.Hub);
            yield return OpenApp(DesktopAppId.MyComputer);
            Assert.That(_runtime.State.Storage.Drives[0].Letter, Is.EqualTo("C"));
            yield return CaptureApp("ru-06-my-computer", DesktopAppId.MyComputer);

            yield return OpenApp(DesktopAppId.Trich);
            var trich = One<TrichView>();
            Click(Field<Button>(trich, "register"));
            Assert.That(_runtime.State.Trich.IsRegistered, Is.False, "Outline is a real prerequisite");
            Assert.That(Field<TMP_Text>(trich, "feedback").text, Is.EqualTo(_localization.Text("desktop.trich.outline_required")));
            Click(Field<Button>(trich, "openOutline"));
            yield return PlayModeWait.Frames(2);
            var outline = One<OutlineView>();
            Type(Field<TMP_InputField>(outline, "username"), "player.test");
            Click(Field<Button>(outline, "create"));
            Assert.That(_runtime.State.Outline.Address, Is.EqualTo("player.test@outline.local"));
            yield return CaptureApp("ru-07-outline-created", DesktopAppId.Outline);
            yield return OpenApp(DesktopAppId.Trich);
            Type(Field<TMP_InputField>(trich, "email"), _runtime.State.Outline.Address);
            Click(Field<Button>(trich, "register"));
            Assert.That(_runtime.State.Trich.IsRegistered, Is.True);
            yield return PlayModeWait.Frames(2);
            Assert.That(Field<GameObject>(trich, "overviewPanel").activeInHierarchy, Is.True);
            Assert.That(Field<GameObject>(trich, "editPanel").activeInHierarchy, Is.False);
            Assert.That(_runtime.State.Trich.AvatarId, Is.Zero);
            Assert.That(Field<int>(Field<ChannelAvatarGraphic>(trich, "profileAvatar"), "portrait"), Is.Zero);
            Assert.That(Field<TMP_Text>(trich, "profileName").text, Is.EqualTo(_runtime.State.Trich.Name));
            Assert.That(Field<TMP_Text>(trich, "profileDescription").text,
                Is.EqualTo(_localization.Text("desktop.trich.description_empty")));
            Assert.That(Field<TMP_Text>(trich, "code").text, Is.EqualTo(_runtime.State.Trich.ChannelCode));
            yield return CaptureApp("ru-08a-trich-default", DesktopAppId.Trich);
            _localization.SetLanguage(GameLanguage.English);
            Assert.That(Field<TMP_Text>(trich, "profileDescription").text,
                Is.EqualTo(_localization.Text("desktop.trich.description_empty")));
            yield return CaptureApp("en-trich-default", DesktopAppId.Trich);
            _localization.SetLanguage(GameLanguage.Russian);
            Click(Field<Button>(trich, "editProfile"));
            yield return PlayModeWait.Frames(2);
            Assert.That(Field<GameObject>(trich, "editPanel").activeInHierarchy, Is.True);
            Type(Field<TMP_InputField>(trich, "channelName"), new string('W',32));
            Type(Field<TMP_InputField>(trich, "description"), new string('W',240));
            Click(Field<Button>(trich, "save"));
            yield return PlayModeWait.Frames(3);
            TMP_Text longName=Field<TMP_Text>(trich,"profileName");
            longName.ForceMeshUpdate();
            Assert.That(longName.preferredHeight,Is.LessThanOrEqualTo(longName.rectTransform.rect.height+.5f),"a maximum-length valid channel name stays inside its layout");
            var descriptionScroll=Field<TMP_Text>(trich,"profileDescription").GetComponentInParent<ScrollRect>();
            Assert.That(descriptionScroll.content.rect.height,Is.GreaterThan(descriptionScroll.viewport.rect.height),"the full long description remains scrollable at body size");
            Assert.That(descriptionScroll.verticalScrollbar.gameObject.activeInHierarchy,Is.True);
            yield return CaptureApp("ru-trich-long-profile",DesktopAppId.Trich);
            Click(Field<Button>(trich,"editProfile"));
            yield return PlayModeWait.Frames(2);
            Type(Field<TMP_InputField>(trich, "channelName"), "GO LIVE Player");
            Type(Field<TMP_InputField>(trich, "description"), "Games and good company");
            Click(Field<Button[]>(trich, "avatars")[2]);
            Click(Field<Button>(trich, "save"));
            yield return PlayModeWait.Frames(2);
            Assert.That(Field<GameObject>(trich, "overviewPanel").activeInHierarchy, Is.True);
            Assert.That(Field<GameObject>(trich, "editPanel").activeInHierarchy, Is.False);
            Assert.That(Field<TMP_Text>(trich, "profileName").text, Is.EqualTo("GO LIVE Player"));
            Assert.That(Field<TMP_Text>(trich, "profileDescription").text, Is.EqualTo("Games and good company"));
            Assert.That(Field<int>(Field<ChannelAvatarGraphic>(trich, "profileAvatar"), "portrait"), Is.EqualTo(2));
            Click(Field<Button>(trich, "copy"));
            string stableCode = _runtime.State.Trich.ChannelCode;
            Assert.That(GUIUtility.systemCopyBuffer, Is.EqualTo(stableCode));
            Assert.That(_runtime.State.Trich.AvatarId, Is.EqualTo(2));
            yield return CaptureApp("ru-08-trich-profile", DesktopAppId.Trich);
            yield return CloseApp(DesktopAppId.Trich);
            yield return OpenApp(DesktopAppId.Trich);
            Assert.That(_runtime.State.Trich.ChannelCode, Is.EqualTo(stableCode));

            yield return OpenApp(DesktopAppId.Donation);
            var donation = One<DonationView>();
            Type(Field<TMP_InputField>(donation, "accountName"), "GO LIVE Player");
            Click(Field<Toggle>(donation, "alerts"));
            Click(Field<Button>(donation, "save"));
            Assert.That(_runtime.State.Donation.AlertsEnabled, Is.False);
            Click(Field<Toggle>(donation, "alerts"));
            Click(Field<Button>(donation, "save"));
            Assert.That(_runtime.State.Donation.AlertsEnabled, Is.True);
            yield return CaptureApp("ru-09-donation-settings", DesktopAppId.Donation);
            yield return OpenApp(DesktopAppId.Web);
            var web = One<WebView>();
            Type(Field<TMP_InputField>(web, "address"), "unknown.example");
            Click(Field<Button>(web, "go"));
            Assert.That(Field<TMP_Text>(web, "page").text, Is.EqualTo(_localization.Text("desktop.web.unavailable")));
            yield return CaptureApp("ru-10-web-local-result", DesktopAppId.Web);
            Assert.That(Field<Button>(web, "back").interactable, Is.True);
            Assert.That(Field<Button>(web, "forward").interactable, Is.False);
            Click(Field<Button>(web, "back"));
            Assert.That(Field<TMP_InputField>(web, "address").text, Is.EqualTo("home.go"));
            Assert.That(Field<TMP_Text>(web, "page").text, Is.EqualTo(_localization.Text("desktop.web.home_body")));
            Assert.That(Field<Button>(web, "back").interactable, Is.False);
            Assert.That(Field<Button>(web, "forward").interactable, Is.True);
            Click(Field<Button>(web, "forward"));
            Assert.That(Field<TMP_InputField>(web, "address").text, Is.EqualTo("unknown.example"));
            Assert.That(Field<TMP_Text>(web, "page").text, Is.EqualTo(_localization.Text("desktop.web.unavailable")));
            Assert.That(Field<Button>(web, "forward").interactable, Is.False);
            Click(Field<Button>(web, "home"));
            Assert.That(Field<TMP_InputField>(web, "address").text, Is.EqualTo("home.go"));
            Assert.That(Field<TMP_Text>(web, "page").text, Is.EqualTo(_localization.Text("desktop.web.home_body")));
            yield return CloseAllApps();

            yield return OpenApp(DesktopAppId.Streamly);
            var streamly = One<StreamlyView>();
            Click(Field<Button>(streamly, "paste"));
            Assert.That(Field<TMP_InputField>(streamly, "channelCode").text, Is.EqualTo(stableCode));
            Click(Field<Button>(streamly, "connect"));
            Assert.That(_runtime.State.Stream.IsConnected, Is.True);
            Click(Field<Button[]>(streamly, "quality")[2]);
            Assert.That(_runtime.State.Stream.CheckStart(_runtime.Capabilities, true, _runtime.UploadMbps), Is.Not.Null, "starter hardware/upload cannot provide High");
            yield return CaptureApp("ru-11-streamly-quality-limit", DesktopAppId.Streamly);
            Click(Field<Button[]>(streamly, "quality")[1]);
            Assert.That(_runtime.State.Stream.Quality, Is.EqualTo(StreamQuality.Medium));
            yield return CaptureApp("ru-11b-streamly-idle", DesktopAppId.Streamly);
            yield return StartBroadcast(streamly);
            yield return CaptureApp("ru-12-streamly-live", DesktopAppId.Streamly);
            Click(Field<Button>(Presentation(DesktopAppId.Streamly), "minimize"));
            Assert.That(_runtime.State.Windows.IsVisible(DesktopAppId.Streamly), Is.False);
            Assert.That(Field<GameObject>(_overlay, "panel").activeInHierarchy, Is.True);
            Click(Field<Button>(Presentation(DesktopAppId.Streamly), "task"));
            Assert.That(_runtime.State.Windows.IsVisible(DesktopAppId.Streamly), Is.True);
            yield return CloseApp(DesktopAppId.Streamly);
            Assert.That(_runtime.State.Stream.State, Is.EqualTo(StreamState.Live), "closing Streamly only closes its window");
            yield return Press(Key.Escape);
            Assert.That(_session.Session.Usage, Is.EqualTo(PcUsageState.Seated));
            yield return _capture.Capture("ru-13-live-overlay-seated");
            yield return Press(Key.Escape);
            Assert.That(_session.Session.Usage, Is.EqualTo(PcUsageState.Standing));
            yield return _capture.Capture("ru-14-live-overlay-world");
            yield return SitAndFocus();
            yield return OpenApp(DesktopAppId.Streamly);
            yield return StopBroadcast(streamly);
            Assert.That(_runtime.State.Trich.CompletedStreams, Is.EqualTo(1));
            Assert.That(_runtime.State.Outline.Messages.Count, Is.EqualTo(1));
            Assert.That(_runtime.State.Donation.History.Count, Is.GreaterThan(0));
            yield return OpenApp(DesktopAppId.Outline);
            object firstMessage = Field<Array>(outline, "rows").GetValue(0);
            yield return OpenApp(DesktopAppId.Hub);
            Assert.That(_runtime.State.Windows.ActiveApp, Is.EqualTo(DesktopAppId.Hub));
            Assert.That(_runtime.State.Outline.Messages[0].IsRead, Is.False);
            // The authored Outline titlebar is above Hub's top edge. Focus that visible chrome
            // through real mouse input before reading the mail row, which starts after the sidebar.
            RectTransform outlineTitlebar = Field<Image>(Presentation(DesktopAppId.Outline), "titlebar").rectTransform;
            Canvas.ForceUpdateCanvases();
            Canvas outlineCanvas = outlineTitlebar.GetComponentInParent<Canvas>();
            Camera outlineCamera = outlineCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : outlineCanvas.worldCamera;
            Vector2 titlebarLocal = outlineTitlebar.rect.min + Vector2.Scale(outlineTitlebar.rect.size, new Vector2(.25f, .75f));
            Vector2 titlebarPoint = RectTransformUtility.WorldToScreenPoint(outlineCamera, outlineTitlebar.TransformPoint(titlebarLocal));
            var titlebarPointer = new PointerEventData(EventSystem.current) { position = titlebarPoint };
            var titlebarHits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(titlebarPointer, titlebarHits);
            Assert.That(titlebarHits.Count, Is.GreaterThan(0));
            Assert.That(titlebarHits[0].gameObject.transform.IsChildOf(outlineTitlebar), Is.True,
                "Outline chrome must be visibly exposed before it can receive input");
            yield return ClickPointer(titlebarPoint);
            Assert.That(_runtime.State.Windows.ActiveApp, Is.EqualTo(DesktopAppId.Outline), "clicking exposed chrome focuses the background window");
            Assert.That(_runtime.State.Outline.Messages[0].IsRead, Is.False, "focusing the mail window does not read a message");
            Click(Field<Button>(firstMessage, "button"));
            Assert.That(_runtime.State.Outline.Messages[0].IsRead, Is.True);
            yield return CaptureApp("ru-15-outline-stream-summary", DesktopAppId.Outline);

            _localization.SetLanguage(GameLanguage.English);
            yield return CloseAllApps();
            AssertDesktopShortcutLabels();
            yield return _capture.Capture("en-01-desktop");
            yield return CaptureStartMenu("en-01b-start-menu-installed", 7, DesktopAppId.Hub);
            yield return CloseApp(DesktopAppId.Hub);
            foreach (DesktopAppId id in Enum.GetValues(typeof(DesktopAppId)))
            {
                yield return OpenApp(id);
                if (id == DesktopAppId.Trich)
                    Assert.That(Field<TMP_Text>(trich, "feedback").text, Is.EqualTo(_localization.Text("desktop.copied")), "closed-window feedback follows the new language");
                if (id == DesktopAppId.Donation)
                    Assert.That(Field<TMP_Text>(donation, "feedback").text, Is.EqualTo(_localization.Text("desktop.saved")));
                yield return CaptureApp("en-app-" + id.ToString().ToLowerInvariant(), id);
                yield return CloseApp(id);
            }
            yield return OpenApp(DesktopAppId.Streamly);
            yield return StartBroadcast(streamly);
            _runtime.State.Stream.Tick(16);
            yield return PlayModeWait.Frames(3);
            TMP_Text visibleChat = Field<TMP_Text>(_overlay, "chat");
            visibleChat.ForceMeshUpdate();
            Assert.That(_runtime.State.Stream.DurationSeconds, Is.GreaterThan(45));
            Assert.That(visibleChat.isTextOverflowing, Is.False, "the latest chat lines fit after the buffer exceeds the visible history");
            Assert.That(visibleChat.text, Does.Contain(_localization.Text(_runtime.State.Stream.Chat.Last().BodyKey)));
            yield return CaptureApp("en-02-streamly-live", DesktopAppId.Streamly);
            yield return Press(Key.Escape);
            yield return Press(Key.Escape);
            yield return _capture.Capture("en-03-live-overlay-world");
            yield return SitAndFocus();
            yield return OpenApp(DesktopAppId.Streamly);
            yield return StopBroadcast(streamly);
            Assert.That(_runtime.State.Trich.CompletedStreams, Is.EqualTo(2));

            var save = One<GameSaveController>();
            PlayerPoseSnapshot worldPose = _session.CaptureWorldPose();
            var data = (GameSaveData)Method(save, "Capture").Invoke(save, null);
            Assert.That(data.Player.Position, Is.EqualTo(worldPose.Position));
            var loaded = JsonUtility.FromJson<GameSaveData>(JsonUtility.ToJson(data));
            object[] validation = { loaded, null, null };
            Assert.That((bool)Method(save, "ValidateSaveData").Invoke(save, validation), Is.True);
            string savedChannelName = _runtime.State.Trich.Name;
            string savedDescription = _runtime.State.Trich.Description;
            string savedDonationName = _runtime.State.Donation.Name;
            bool savedAlerts = _runtime.State.Donation.AlertsEnabled;
            yield return OpenApp(DesktopAppId.Trich);
            Click(Field<Button>(trich, "editProfile"));
            yield return PlayModeWait.Frames(2);
            Type(Field<TMP_InputField>(trich, "channelName"), "Unsaved channel draft");
            Type(Field<TMP_InputField>(trich, "description"), "Unsaved description draft");
            Click(Field<Button[]>(trich, "avatars")[0]);
            yield return OpenApp(DesktopAppId.Donation);
            Type(Field<TMP_InputField>(donation, "accountName"), "Unsaved donation draft");
            Click(Field<Toggle>(donation, "alerts"));
            Assert.That(_runtime.State.Trich.Name, Is.EqualTo(savedChannelName), "unsaved form edits do not change durable accounts");
            Assert.That(_runtime.State.Donation.Name, Is.EqualTo(savedDonationName));
            Method(save, "Apply").Invoke(save, new[] { loaded, validation[1], validation[2] });
            yield return PlayModeWait.Frames(4);
            Assert.That(_session.Session.Usage, Is.EqualTo(PcUsageState.Standing));
            Assert.That(_session.Session.Power, Is.EqualTo(PcPowerState.Off));
            Assert.That(_session.Session.MonitorOn, Is.False);
            Assert.That(_runtime.State.Stream.IsConnected, Is.False);
            Assert.That(_runtime.State.Windows.Windows, Is.Empty);
            Assert.That(_runtime.State.Trich.ChannelCode, Is.EqualTo(stableCode));
            Assert.That(_runtime.State.Trich.CompletedStreams, Is.EqualTo(2));
            Assert.That(_runtime.State.Outline.Messages.Count, Is.EqualTo(2));
            Assert.That(_runtime.State.Storage.InstalledContent.Select(content => content.AppId).Distinct().Count(), Is.EqualTo(7));
            AssertUniqueOwnersAndWindows();
            yield return FaceCase();
            yield return _capture.Capture("en-04-case-power-prompt");
            yield return FaceMonitor();
            yield return _capture.Capture("en-05-monitor-off-prompt");
            yield return Press(Key.E);
            Assert.That(_session.Session.Usage, Is.EqualTo(PcUsageState.Seated));
            Transform seat = Field<Transform>(_session, "seatViewAnchor");
            yield return PlayModeWait.Until(() => Vector3.Distance(_camera.transform.position, seat.position) < .001f, "the powered-off seated view");
            yield return _capture.Capture("en-06-seated-pc-off");
            _localization.SetLanguage(GameLanguage.Russian);
            yield return _capture.Capture("ru-16-seated-pc-off");
            yield return Press(Key.Escape);
            // The actual input path has already been exercised above. Pause simulation only for observing
            // the real authored boot splash; the session receives no artificial transition or replacement UI.
            float previousTimeScale = Time.timeScale;
            try
            {
                Time.timeScale = 0;
                _session.ToggleMonitor();
                Assert.That(_session.TryTogglePower(), Is.True);
                Assert.That(_session.Session.Power, Is.EqualTo(PcPowerState.Booting));
                yield return _capture.Capture("ru-17-monitor-boot-splash");
                _localization.SetLanguage(GameLanguage.English);
                yield return _capture.Capture("en-07-monitor-boot-splash");
            }
            finally { Time.timeScale = previousTimeScale; }
            yield return PlayModeWait.Until(() => _session.Session.Power == PcPowerState.Running, "the restored PC to finish its real boot");
            yield return SitAndFocus();
            yield return OpenApp(DesktopAppId.Trich);
            Assert.That(One<TrichView>(), Is.SameAs(trich), "load refreshes the existing authored view");
            Assert.That(Field<TMP_InputField>(trich, "channelName").text, Is.EqualTo(savedChannelName));
            Assert.That(Field<TMP_InputField>(trich, "description").text, Is.EqualTo(savedDescription));
            Assert.That(Field<int>(trich, "_avatar"), Is.EqualTo(2));
            Assert.That(Field<GameObject>(trich, "overviewPanel").activeInHierarchy, Is.True,
                "restoring reopens the saved profile overview rather than a stale draft");
            Assert.That(Field<TMP_Text>(trich, "profileName").text, Is.EqualTo(savedChannelName));
            Assert.That(Field<TMP_Text>(trich, "feedback").text, Is.Empty, "restoring clears feedback from the previous interaction");
            yield return OpenApp(DesktopAppId.Donation);
            Assert.That(One<DonationView>(), Is.SameAs(donation));
            Assert.That(Field<TMP_InputField>(donation, "accountName").text, Is.EqualTo(savedDonationName));
            Assert.That(Field<Toggle>(donation, "alerts").isOn, Is.EqualTo(savedAlerts));
            Assert.That(Field<TMP_Text>(donation, "feedback").text, Is.Empty);
            yield return CloseAllApps();
            yield return Press(Key.Escape);
            yield return Press(Key.Escape);
            var delivered = new List<WorldItem>();
            yield return DeliverGpuToInventory(delivered);
            WorldItem gpu = delivered.Single();
            yield return FaceCase();
            yield return Press(Key.B);
            var workbench = One<PcWorkbenchBehaviour>();
            yield return PlayModeWait.Until(() => workbench.IsInteractive, "the real PC Build presentation to open");
            if (_pc.TryGetSlot("gpu-0", out PcComponentSlot gpuSlot))
            {
                Vector3 pointer = _camera.WorldToScreenPoint(gpuSlot.transform.TransformPoint(gpuSlot.TargetBounds.center));
                InputSystem.QueueStateEvent(_input.Mouse, new MouseState { position = pointer });
                yield return PlayModeWait.Frames(5);
            }
            AssertHardwareMarks(false);
            yield return _capture.Capture("en-08-pc-build");
            _localization.SetLanguage(GameLanguage.Russian);
            yield return PlayModeWait.Frames(3);
            yield return _capture.Capture("ru-18-pc-build");
            PcBuildInventoryView parts = One<PcBuildInventoryView>();
            InventoryItemView row = Field<InventoryListView>(parts, "list").GetComponentsInChildren<InventoryItemView>(true)
                .Single(candidate => candidate.InstanceId == gpu.Instance.InstanceId);
            Canvas.ForceUpdateCanvases();
            var rowRect = (RectTransform)row.transform;
            yield return ClickPointer(RectTransformUtility.WorldToScreenPoint(null, rowRect.TransformPoint(rowRect.rect.center)));
            Assert.That(_player.GetComponent<PlayerCarry>().CarriedItem, Is.SameAs(gpu));
            Assert.That(gpu.Instance.Location, Is.EqualTo(ItemLocation.Carried));
            Assert.That(_pc.TryGetSlot("gpu-0", out gpuSlot), Is.True);
            Vector3 slotPoint = _camera.WorldToScreenPoint(gpuSlot.transform.TransformPoint(gpuSlot.TargetBounds.center));
            InputSystem.QueueStateEvent(_input.Mouse, new MouseState { position = slotPoint });
            yield return PlayModeWait.Frames(5);
            Assert.That(workbench.TargetSlot, Is.SameAs(gpuSlot));
            Assert.That(workbench.IsGhostVisible, Is.True, "the held GPU has the real installation preview");
            yield return _capture.Capture("ru-19-pc-build-held-gpu-ghost");
            _localization.SetLanguage(GameLanguage.English);
            yield return _capture.Capture("en-09-pc-build-held-gpu-ghost");
            yield return ClickPointer(slotPoint);
            Assert.That(gpu.Instance.Location, Is.EqualTo(ItemLocation.Installed));
            Assert.That(gpu.transform.parent, Is.SameAs(gpuSlot.InstallAnchor));
            Assert.That(workbench.IsGhostVisible, Is.False);
            AssertHardwareMarks(true);
            yield return _capture.Capture("en-10-pc-build-gpu-installed");
            yield return Press(Key.Escape);
            yield return PlayModeWait.Until(() => !workbench.IsOpen, "PC Build to restore the world view");
            LogAssert.NoUnexpectedReceived();
        }

        private void AssertHardwareMarks(bool gpuInstalled)
        {
            var status=One<PcHardwareStatusView>();
            PcHardwareGlyph[] glyphs=status.GetComponentsInChildren<PcHardwareGlyph>(false);
            Assert.That(glyphs.Length,Is.EqualTo(12));
            foreach(PcHardwareGlyph glyph in glyphs)
            {
                Assert.That(glyph.GetComponent<CanvasRenderer>(),Is.Not.Null,glyph.name+" must render into the real Canvas");
                Assert.That(glyph.rectTransform.rect.width,Is.GreaterThanOrEqualTo(24));
                Assert.That(glyph.rectTransform.rect.height,Is.GreaterThanOrEqualTo(24));
            }
            foreach(object row in Field<Array>(status,"rows"))
            {
                bool missingGpu=Field<PcComponentType>(row,"component")==PcComponentType.Gpu&&!gpuInstalled;
                Assert.That(Field<PcHardwareGlyph.Shape>(Field<PcHardwareGlyph>(row,"marker"),"shape"),
                    Is.EqualTo(missingGpu?PcHardwareGlyph.Shape.Warning:PcHardwareGlyph.Shape.Check));
            }
        }

        private IEnumerator Boot()
        {
            _input = new VirtualInput(true);
            _clipboard = GUIUtility.systemCopyBuffer;
            _capture = new DesktopVisualCapture();
            yield return _capture.WaitForResolution();
            yield return PlayModeWait.Frames(10);
            _runtime = One<DesktopRuntimeBehaviour>();
            _session = One<PcSessionBehaviour>();
            _shell = One<DesktopShellView>();
            _overlay = One<StreamOverlayView>();
            _localization = One<LocalizationContext>();
            _pc = One<PcAssemblyBehaviour>();
            _player = Field<PlayerController>(_session, "playerController");
            _interactor = _player.GetComponent<PlayerInteractor>();
            _camera = Field<Camera>(_session, "playerCamera");
            _presentations = Field<Array>(_shell, "apps");
            Assert.That(_runtime.IsReady && _pc.IsReady && _session.isActiveAndEnabled, Is.True, "GL authored references must be ready");
            Assert.That(_runtime.Session, Is.SameAs(_session.Session));
            Assert.That(_runtime.State.Outline.IsCreated || _runtime.State.Trich.IsRegistered, Is.False, "the journey starts from the authored new game");
            Assert.That(Field<GameObject>(_overlay, "panel").activeInHierarchy, Is.False);
            Assert.That(Field<GameObject>(_overlay, "panel").GetComponentsInParent<GraphicRaycaster>(true), Is.Empty, "overlay has no raycaster to intercept desktop controls");
            AssertUniqueOwnersAndWindows();
        }

        private IEnumerator DeliverGpuToInventory(List<WorldItem> delivered)
        {
            // Arrange a real purchasable component through the existing shop/delivery systems, then
            // unpack it with player input. The visible parts panel and installation remain real input.
            ShopTestData.BuyOne(One<ShopBehaviour>(), "budget-gpu");
            One<GameClockBehaviour>().Clock.AdvanceMinutes(151);
            yield return PlayModeWait.Frames(30);
            DeliveryPackageBehaviour package = Object.FindObjectsByType<DeliveryPackageBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Single(candidate => candidate.Item != null && candidate.Item.Instance != null);
            PlayerCarry carry = _player.GetComponent<PlayerCarry>();
            Assert.That(carry.TryCarry(package.Item), Is.True);
            yield return Press(Key.F);
            yield return PlayModeWait.Frames(3);
            WorldItem gpu = Object.FindObjectsByType<WorldItem>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(item => item.Instance != null && item.Instance.DefinitionId == "budget-gpu" && item.Instance.Location == ItemLocation.World);
            Assert.That(carry.TryCarry(gpu), Is.True);
            Assert.That(_player.GetComponent<PlayerInventory>().TryStoreCarriedItem(), Is.True);
            delivered.Add(gpu);
        }

        private IEnumerator FaceCase()
        {
            Vector3 target = _pc.GetComponent<Collider>().bounds.center;
            yield return FaceTarget(new Vector3(target.x - 1.2f, 0.274f, target.z - 0.3f), target);
        }

        private IEnumerator FaceMonitor()
        {
            Transform anchor = Field<Transform>(_session, "seatViewAnchor");
            Vector3 position = anchor.position;
            position.y = _player.CapturePose().Position.y;
            Vector3 target = anchor.position + anchor.forward * 0.86f;
            yield return FaceTarget(position, target);
            Physics.Raycast(_camera.ViewportPointToRay(new Vector3(.5f, .5f)), out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Ignore);
            TestContext.WriteLine($"MONITOR_AIM body={_player.CapturePose().Position} camera={_camera.transform.position} forward={_camera.transform.forward} target={target} hit={(hit.collider != null ? hit.collider.name : "none")}");
        }

        private IEnumerator FaceTarget(Vector3 position, Vector3 target)
        {
            Vector3 flat = target - position;
            flat.y = 0;
            _player.RestorePose(new PlayerPoseSnapshot(position, Quaternion.LookRotation(flat), 0));
            yield return PlayModeWait.Frames(6);
            float settleTime = Time.time + .4f;
            yield return PlayModeWait.Until(() => Time.time >= settleTime, "the standing capsule and eye-height transition to settle");
            // The character capsule may settle away from a chair/desk. Aim from the real resulting eye,
            // and retain that physically valid body position instead of repeatedly teleporting into it.
            for (int i = 0; i < 3; i++)
            {
                PlayerPoseSnapshot settled = _player.CapturePose();
                Vector3 toTarget = target - _camera.transform.position;
                Vector3 horizontal = new(toTarget.x, 0, toTarget.z);
                float pitch = -Mathf.Atan2(toTarget.y, horizontal.magnitude) * Mathf.Rad2Deg;
                _player.RestorePose(new PlayerPoseSnapshot(settled.Position, Quaternion.LookRotation(horizontal), pitch));
                yield return PlayModeWait.Frames(4);
            }
        }

        private IEnumerator SitAndFocus()
        {
            if (_session.Session.Usage == PcUsageState.Standing)
            {
                // Reacquire the physical monitor after screenshot I/O; the standing capsule may
                // finish settling against the chair while the previous capture is written.
                yield return FaceMonitor();
                Assert.That(_interactor.Prompts.PrimaryKey, Is.EqualTo("pc.session.sit"));
                yield return Press(Key.E);
                Assert.That(_session.Session.Usage, Is.EqualTo(PcUsageState.Seated));
            }
            Transform anchor = Field<Transform>(_session, "seatViewAnchor");
            yield return PlayModeWait.Until(() => Vector3.Distance(_camera.transform.position, anchor.position) < 0.001f, "the seated camera transition");
            Vector2 point = new(Screen.width / 2f, Screen.height / 2f);
            InputSystem.QueueStateEvent(_input.Mouse, new MouseState { position = point, buttons = 1 });
            yield return PlayModeWait.Frames(2);
            InputSystem.QueueStateEvent(_input.Mouse, new MouseState { position = point });
            yield return PlayModeWait.Frames(2);
            Assert.That(_session.Session.Usage, Is.EqualTo(PcUsageState.Focused));
            Assert.That(Field<GameObject>(_shell, "desktopRoot").activeInHierarchy, Is.True);
        }

        private IEnumerator StartBroadcast(StreamlyView view)
        {
            Click(Field<Button>(view, "startStop"));
            yield return PlayModeWait.Until(() => _runtime.State.Stream.State == StreamState.Live, "the actual Start button to complete startup");
            _runtime.State.Stream.Tick(31);
            yield return PlayModeWait.Frames(3);
            Assert.That(_runtime.State.Stream.Viewers, Is.GreaterThan(0));
            Assert.That(_runtime.State.Stream.Chat.Count, Is.GreaterThan(0));
            Assert.That(_runtime.State.Stream.DonationCents, Is.GreaterThan(0));
            Assert.That(Field<GameObject>(_overlay, "panel").activeInHierarchy, Is.True);
        }

        private IEnumerator StopBroadcast(StreamlyView view)
        {
            Click(Field<Button>(view, "startStop"));
            Assert.That(_runtime.State.Stream.State, Is.EqualTo(StreamState.Stopping));
            Assert.That(Field<GameObject>(_overlay, "panel").activeInHierarchy, Is.False, "overlay is visible only during Live");
            yield return PlayModeWait.Until(() => _runtime.State.Stream.State == StreamState.Offline, "the Stop button to complete shutdown");
        }

        private IEnumerator OpenApp(DesktopAppId id)
        {
            Click(Field<Button>(Presentation(id), "shortcut"));
            yield return PlayModeWait.Frames(2);
            Assert.That(_runtime.State.Windows.ActiveApp, Is.EqualTo(id));
            Assert.That(_runtime.State.Windows.Windows.Count(window => window.AppId == id), Is.EqualTo(1));
            Assert.That(Field<GameObject>(Presentation(id), "window").activeInHierarchy, Is.True);
        }

        private IEnumerator CloseApp(DesktopAppId id)
        {
            Click(Field<Button>(Presentation(id), "task"));
            yield return PlayModeWait.Frames(2);
            Assert.That(_runtime.State.Windows.ActiveApp, Is.EqualTo(id));
            Click(Field<Button>(Presentation(id), "close"));
            yield return PlayModeWait.Frames(1);
            Assert.That(_runtime.State.Windows.IsOpen(id), Is.False);
            Assert.That(Field<Button>(Presentation(id), "task").gameObject.activeSelf, Is.False);
        }

        private IEnumerator CloseAllApps()
        {
            foreach (DesktopAppId id in _runtime.State.Windows.Windows.Select(window => window.AppId).ToArray())
                yield return CloseApp(id);
        }

        private IEnumerator CaptureApp(string name, DesktopAppId id)
        {
            foreach (DesktopWindow other in _runtime.State.Windows.Windows.ToArray())
            {
                if(other.AppId==id||!_runtime.State.Windows.IsVisible(other.AppId))continue;
                Click(Field<Button>(Presentation(other.AppId), "task"));
                yield return PlayModeWait.Frames(2);
                Click(Field<Button>(Presentation(other.AppId), "minimize"));
                yield return PlayModeWait.Frames(1);
            }
            Click(Field<Button>(Presentation(id), "task"));
            yield return PlayModeWait.Frames(2);
            GameObject window = Field<GameObject>(Presentation(id), "window");
            Canvas.ForceUpdateCanvases();
            yield return _capture.Capture(name);
            foreach (TMP_Text text in window.GetComponentsInChildren<TMP_Text>(false))
            {
                if (text.GetComponentInParent<TMP_InputField>() != null || string.IsNullOrWhiteSpace(text.text)) continue;
                text.ForceMeshUpdate();
                Assert.That(text.isTextTruncated, Is.False, name + ": " + text.name + " rect=" + text.rectTransform.rect.size + " font=" + text.fontSize + " text=" + text.text);
                Assert.That(text.text.Contains("[desktop.") || text.text.Contains("[pc."), Is.False, name + ": untranslated " + text.text);
            }
        }

        private IEnumerator CaptureStartMenu(string name, int expectedEntries, DesktopAppId open)
        {
            Click(Field<Button>(_shell, "startButton"));
            yield return PlayModeWait.Frames(2);
            Assert.That(Field<GameObject>(_shell, "startMenu").activeInHierarchy, Is.True);
            object[] presentations = _presentations.Cast<object>().ToArray();
            Assert.That(presentations.Count(app => Field<Button>(app, "startShortcut").gameObject.activeInHierarchy), Is.EqualTo(expectedEntries));
            foreach (object app in presentations)
            {
                DesktopAppId id = Field<DesktopAppId>(app, "appId");
                Assert.That(Field<Button>(app, "startShortcut").gameObject.activeInHierarchy,
                    Is.EqualTo(_runtime.State.Storage.IsInstalled(id)), "Start menu installation availability: " + id);
            }
            yield return _capture.Capture(name);
            Click(Field<Button>(Presentation(open), "startShortcut"));
            yield return PlayModeWait.Frames(2);
            Assert.That(Field<GameObject>(_shell, "startMenu").activeInHierarchy, Is.False);
            Assert.That(_runtime.State.Windows.ActiveApp, Is.EqualTo(open));
            Assert.That(_runtime.State.Windows.Windows.Count(window => window.AppId == open), Is.EqualTo(1), "menu reuses the single owned window");
        }

        private void AssertDesktopShortcutLabels()
        {
            Canvas.ForceUpdateCanvases();
            foreach (object app in _presentations)
                foreach (TMP_Text label in Field<Button>(app, "shortcut").GetComponentsInChildren<TMP_Text>(false))
                {
                    label.ForceMeshUpdate();
                    Assert.That(label.isTextTruncated, Is.False, _localization.CurrentLanguage + " desktop shortcut " + label.text);
                    Assert.That(label.text.Contains("[desktop."), Is.False, "shortcut name is localized");
                }
        }

        private void AssertUniqueOwnersAndWindows()
        {
            Assert.That(Object.FindObjectsByType<DesktopRuntimeBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<PcSessionBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(_presentations.Length, Is.EqualTo(7));
            Assert.That(_presentations.Cast<object>().Select(app => Field<DesktopAppId>(app, "appId")).Distinct().Count(), Is.EqualTo(7));
            Assert.That(_presentations.Cast<object>().Select(app => Field<GameObject>(app, "window")).Distinct().Count(), Is.EqualTo(7));
            Assert.That(_presentations.Cast<object>().Select(app => Field<Button>(app, "task")).Distinct().Count(), Is.EqualTo(7));
            Assert.That(Object.FindObjectsByType<DesktopAppView>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.EqualTo(7));
        }

        private long UsedStorage() => _runtime.State.Storage.Drives.Sum(drive => (long)_runtime.State.Storage.GetUsedMiB(drive.DriveId));
        private object Presentation(DesktopAppId id) => _presentations.Cast<object>().Single(app => Field<DesktopAppId>(app, "appId") == id);

        private IEnumerator Press(Key key)
        {
            InputSystem.QueueStateEvent(_input.Keyboard, new KeyboardState(key));
            yield return PlayModeWait.Frames(2);
            InputSystem.QueueStateEvent(_input.Keyboard, new KeyboardState());
            yield return PlayModeWait.Frames(2);
        }

        private IEnumerator ClickPointer(Vector2 position)
        {
            InputSystem.QueueStateEvent(_input.Mouse, new MouseState { position = position });
            yield return PlayModeWait.Frames(2);
            InputSystem.QueueStateEvent(_input.Mouse, new MouseState { position = position, buttons = 1 });
            yield return PlayModeWait.Frames(2);
            InputSystem.QueueStateEvent(_input.Mouse, new MouseState { position = position });
            yield return PlayModeWait.Frames(3);
        }

        private static void Type(TMP_InputField input, string text)
        {
            Assert.That(input.gameObject.activeInHierarchy && input.interactable, Is.True, input.name);
            input.text = text;
            input.onEndEdit.Invoke(text);
        }

        private static void Click(Selectable control, Vector2? normalizedPoint = null)
        {
            Assert.That(control != null && control.gameObject.activeInHierarchy && control.IsInteractable(), Is.True, control != null ? control.name : "unwired control");
            Canvas.ForceUpdateCanvases();
            Canvas canvas = control.GetComponentInParent<Canvas>();
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var rect = (RectTransform)control.transform;
            Vector2 fraction = normalizedPoint ?? new Vector2(.5f, .5f);
            Vector2 local = rect.rect.min + Vector2.Scale(rect.rect.size, fraction);
            Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(local));
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, position = point };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits.Count, Is.GreaterThan(0), control.name + " must be reachable by the real UI raycaster");
            Assert.That(hits[0].gameObject.transform.IsChildOf(control.transform), Is.True, control.name + " is obscured by " + hits[0].gameObject.name);
            pointer.pointerPress = control.gameObject;
            pointer.rawPointerPress = hits[0].gameObject;
            pointer.pointerPressRaycast = pointer.pointerCurrentRaycast = hits[0];
            pointer.pressPosition = point;
            pointer.eligibleForClick = true;
            ExecuteEvents.Execute(control.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(control.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(control.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        private static T One<T>() where T : Object
        {
            T[] objects = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.That(objects.Length, Is.EqualTo(1), "the authored scene has exactly one " + typeof(T).Name);
            return objects[0];
        }

        private static T Field<T>(object owner, string name)
        {
            for (Type type = owner.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null) return (T)field.GetValue(owner);
            }
            Assert.Fail(owner.GetType().Name + " has no authored field " + name);
            return default;
        }

        private static MethodInfo Method(object owner, string name)
        {
            MethodInfo method = owner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, name);
            return method;
        }
    }
}
