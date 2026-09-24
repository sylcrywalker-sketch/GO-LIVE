using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GoLive.Delivery;
using GoLive.GameTime;
using GoLive.Inventory;
using GoLive.Items;
using GoLive.Localization;
using GoLive.PcBuilding;
using GoLive.Persistence;
using GoLive.Phone;
using GoLive.Player;
using GoLive.Shop;
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
    // PC Build Mode in the real GL scene: the real player, HUD, parts panel, phone and input router, driven by a
    // virtual keyboard and mouse through the project's Input Actions. Nothing here saves to disk.
    public sealed class PcWorkbenchPlayModeTests
    {
        private const string GameScene = "Assets/Game/Scenes/GL.unity";
        private const string GpuDefinition = "Item_BudgetGPU";

        private SceneSetup[] _previousScenes;
        private InputSettings.EditorInputBehaviorInPlayMode _editorInput;
        private InputSettings.BackgroundBehavior _backgroundBehavior;
        private Keyboard _keyboard;
        private Mouse _mouse;
        private Vector2 _pointer;

        private PcWorkbenchBehaviour _workbench;
        private PcAssemblyBehaviour _pc;
        private PcBuildPresentation _presentation;
        private PcComponentSlot _slot;
        private PlayerController _player;
        private PlayerCarry _carry;
        private PlayerInventory _inventory;
        private PlayerInteractor _interactor;
        private InventoryUiController _tab;
        private GameUiInputRouter _router;
        private PhoneBehaviour _phone;
        private PcWorkbenchHudView _hud;
        private PcBuildInventoryView _parts;
        private LocalizationContext _localization;
        private Camera _camera;

        [OneTimeSetUp]
        public void OpenGameScene()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);

                if (scene.isDirty && scene.rootCount > 0)
                    Assert.Ignore("Save your open scene before running the GL build mode fixture; unsaved scene work will not be closed.");
            }

            _previousScenes = EditorSceneManager.GetSceneManagerSetup();
            EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Single);
        }

        [OneTimeTearDown]
        public void RestoreEditorSceneSetup()
        {
            SaveTestWorld.RestoreScene(_previousScenes);
        }

        [SetUp]
        public void RememberInputSettings()
        {
            _editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            _backgroundBehavior = InputSystem.settings.backgroundBehavior;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_keyboard != null)
                InputSystem.RemoveDevice(_keyboard);

            if (_mouse != null)
                InputSystem.RemoveDevice(_mouse);

            _keyboard = null;
            _mouse = null;
            InputSystem.settings.editorInputBehaviorInPlayMode = _editorInput;
            InputSystem.settings.backgroundBehavior = _backgroundBehavior;

            if (Application.isPlaying)
                yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator BBringsThePcToThePlayerAndEscOrBTakesItBackRestoringEverything()
        {
            yield return new EnterPlayMode(false);
            yield return Boot();
            yield return FaceThePc();

            Assert.That(_interactor.Prompts.SpecialKey, Is.EqualTo("pc.workbench.enter"), "looking at the PC offers [B]");
            PlayerPoseSnapshot pose = _player.CapturePose();
            Vector3 cameraLocal = _camera.transform.localPosition;
            Quaternion cameraRotation = _camera.transform.localRotation;
            Vector3 eye = _camera.transform.position;
            float fieldOfView = _camera.fieldOfView;
            CursorLockMode lockBefore = Cursor.lockState;
            bool cursorBefore = Cursor.visible;
            Transform panel = _presentation.SidePanel;
            Vector3 panelClosed = panel.localPosition;
            Transform root = _presentation.PresentationRoot;
            Vector3 restPosition = root.localPosition;
            Quaternion restRotation = root.localRotation;
            GameplayRoot gameplayRoot = new(_workbench.transform);

            yield return Press(Key.B, Action(_interactor, "specialAction"));

            Assert.That(_workbench.Phase, Is.EqualTo(PcBuildModePhase.Opening), "the mode starts with its presentation");
            Assert.That(_player.Controls.IsAllowed(PlayerControlMask.Movement | PlayerControlMask.Look | PlayerControlMask.Crouch | PlayerControlMask.Interaction), Is.False);

            float travelled = 0f;

            while (_workbench.Phase == PcBuildModePhase.Opening)
            {
                gameplayRoot.AssertUnchanged("while the PC comes over");
                Assert.That(Vector3.Distance(_camera.transform.position, eye), Is.LessThan(1e-4f), "the camera stays in the player's head");
                travelled = Mathf.Max(travelled, Vector3.Distance(root.localPosition, restPosition) * _workbench.transform.lossyScale.x);
                yield return null;
            }

            yield return null;
            gameplayRoot.AssertUnchanged("in the build view");
            Assert.That(_workbench.Phase, Is.EqualTo(PcBuildModePhase.Open));
            Assert.That(travelled, Is.GreaterThan(0.3f), "the PC itself came over to the player");
            Assert.That(Vector3.Distance(root.localPosition, _presentation.BuildLocalPosition), Is.LessThan(1e-6f), "the PC settled in its build pose");
            Assert.That(Quaternion.Angle(root.localRotation, _presentation.BuildLocalRotation), Is.LessThan(0.01f));
            Assert.That(root.localScale, Is.EqualTo(Vector3.one), "moved and turned, never stretched");
            Assert.That(Vector3.Distance(_presentation.ViewAnchor.position, _camera.transform.position), Is.LessThan(0.001f), "the eye sees the PC from its build view anchor");
            Assert.That(Quaternion.Angle(_presentation.ViewAnchor.rotation, _camera.transform.rotation), Is.LessThan(0.1f));
            Assert.That(Vector3.Distance(_camera.transform.position, eye), Is.LessThan(1e-4f), "the camera did not travel");
            Assert.That(Quaternion.Angle(_camera.transform.rotation, Quaternion.LookRotation(Flat(_camera.transform.forward))), Is.InRange(10f, 30f), "only a slight downward look");
            Assert.That(_camera.fieldOfView, Is.EqualTo(fieldOfView), "the field of view is the player's own");
            Assert.That(panel.gameObject.activeSelf, Is.False, "the side panel was taken away");
            Assert.That(_hud.IsVisible, Is.True);
            Assert.That(GameplayHudAlpha(), Is.LessThan(0.001f), "the gameplay HUD gave way");
            Assert.That(Vector3.Distance(_player.transform.position, pose.Position), Is.LessThan(0.02f), "the player is not teleported");
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
            Assert.That(Cursor.visible, Is.True);
            Assert.That(_tab.IsOpen, Is.False, "build mode has its own parts panel, not the TAB overlay");

            Vector3 standing = _player.transform.position;
            yield return Hold(Key.W, Key.LeftCtrl);
            yield return Frames(12);
            yield return Release();
            Assert.That(Vector3.Distance(Flat(_player.transform.position), Flat(standing)), Is.LessThan(0.005f), "W does not walk");
            Assert.That(_player.IsCrouching, Is.False, "Ctrl does not crouch");

            yield return Press(Key.Q, Action(_router, "phoneAction"));
            yield return Press(Key.Tab, Action(_router, "inventoryAction"));
            Assert.That(_phone.IsOpen, Is.False, "the phone does not steal the build mode's input");
            Assert.That(_tab.IsOpen, Is.False, "TAB does nothing in build mode");
            Assert.That(_workbench.Phase, Is.EqualTo(PcBuildModePhase.Open));

            yield return Press(Key.Escape, Action(_router, "backAction"));
            Assert.That(_workbench.Phase, Is.EqualTo(PcBuildModePhase.Closing), "Esc plays the presentation backwards");
            Assert.That(_player.Controls.IsAllowed(PlayerControlMask.Movement), Is.False, "controls come back only at the end");

            while (_workbench.Phase == PcBuildModePhase.Closing)
            {
                gameplayRoot.AssertUnchanged("while the PC goes back");
                yield return null;
            }

            yield return null;
            gameplayRoot.AssertUnchanged("after the build view");
            Assert.That(_workbench.Phase, Is.EqualTo(PcBuildModePhase.Closed));
            Assert.That(root.localPosition.Equals(restPosition) && root.localRotation.Equals(restRotation), Is.True, "the PC is exactly back in its place");
            Assert.That(_camera.transform.localPosition, Is.EqualTo(cameraLocal), "back exactly in the player's head");
            Assert.That(_camera.transform.localRotation, Is.EqualTo(cameraRotation));
            Assert.That(_camera.fieldOfView, Is.EqualTo(fieldOfView));
            Assert.That(panel.gameObject.activeSelf, Is.True);
            Assert.That(panel.localPosition.Equals(panelClosed), Is.True, "the side panel is back on the case");
            Assert.That(_hud.IsVisible, Is.False);
            Assert.That(GameplayHudAlpha(), Is.GreaterThan(0.999f));
            Assert.That(_player.Controls.IsAllowed(PlayerControlMask.All), Is.True);
            Assert.That(Cursor.lockState, Is.EqualTo(lockBefore));
            Assert.That(Cursor.visible, Is.EqualTo(cursorBefore));
            Assert.That(Field<GamePauseController>(_router, "pause").IsPaused, Is.False, "Esc went to the build mode, not the pause menu");
            AssertPose(pose);

            yield return Frames(3);
            yield return Press(Key.B, Action(_interactor, "specialAction"));
            yield return WaitForPhase(PcBuildModePhase.Open);
            yield return Press(Key.B, Action(_interactor, "specialAction"));
            Assert.That(_workbench.Phase, Is.EqualTo(PcBuildModePhase.Closing), "B leaves too");
            yield return WaitForPhase(PcBuildModePhase.Closed);
            AssertPose(pose);
            Assert.That(_camera.transform.localPosition, Is.EqualTo(cameraLocal));
            Assert.That(root.localPosition.Equals(restPosition) && root.localRotation.Equals(restRotation), Is.True);
            gameplayRoot.AssertUnchanged("after the second visit");
        }

        // Esc half-way in: the PC turns round from where it is, goes back, and can be brought over again.
        [UnityTest]
        public IEnumerator EscHalfWayInTurnsRoundWithoutAJumpAndOpensAgainCleanly()
        {
            yield return new EnterPlayMode(false);
            yield return Boot();
            yield return FaceThePc();

            Vector3 cameraLocal = _camera.transform.localPosition;
            Quaternion cameraRotation = _camera.transform.localRotation;
            Transform root = _presentation.PresentationRoot;
            Transform panel = _presentation.SidePanel;
            Vector3 restPosition = root.localPosition;
            Quaternion restRotation = root.localRotation;
            Vector3 panelClosed = panel.localPosition;
            GameplayRoot gameplayRoot = new(_workbench.transform);
            yield return Press(Key.B, Action(_interactor, "specialAction"));

            float deadline = Time.realtimeSinceStartup + 3f;

            while (Amount("ApproachAmount") < 0.5f && Time.realtimeSinceStartup < deadline)
                yield return null;

            yield return null;
            Vector3 before = root.position;
            Quaternion turnedBefore = root.rotation;
            Quaternion aimBefore = _camera.transform.rotation;
            Assert.That(Vector3.Distance(before, root.parent.TransformPoint(restPosition)), Is.GreaterThan(0.2f), "half-way: a jump back would be obvious");
            _workbench.Close();
            yield return null;

            // One frame moves the PC at most a few centimetres (its step is clamped to 1/30 s); a jump would be tenths
            // of a metre. Batch frame times vary, so the bound is physical rather than the last step seen.
            Assert.That(_workbench.Phase, Is.EqualTo(PcBuildModePhase.Closing));
            Assert.That(Vector3.Distance(root.position, before), Is.LessThan(0.05f), "turning round continues from where the PC is");
            Assert.That(Quaternion.Angle(root.rotation, turnedBefore), Is.LessThan(3f));
            Assert.That(Quaternion.Angle(_camera.transform.rotation, aimBefore), Is.LessThan(1f), "no camera snap");
            yield return WaitForPhase(PcBuildModePhase.Closed);

            Assert.That(root.localPosition.Equals(restPosition) && root.localRotation.Equals(restRotation), Is.True, "exactly back in its place");
            Assert.That(panel.gameObject.activeSelf && panel.localPosition.Equals(panelClosed), Is.True, "the panel never came off");
            Assert.That(_camera.transform.localPosition, Is.EqualTo(cameraLocal));
            Assert.That(_camera.transform.localRotation, Is.EqualTo(cameraRotation));
            Assert.That(_hud.IsVisible, Is.False, "no UI left half-visible");
            Assert.That(GameplayHudAlpha(), Is.GreaterThan(0.999f));
            Assert.That(_player.Controls.IsAllowed(PlayerControlMask.All), Is.True);

            yield return Press(Key.B, Action(_interactor, "specialAction"));
            yield return WaitForPhase(PcBuildModePhase.Open);
            Assert.That(Vector3.Distance(_presentation.ViewAnchor.position, _camera.transform.position), Is.LessThan(0.001f), "the second visit arrives exactly as the first would have");
            _workbench.Close();
            yield return WaitForPhase(PcBuildModePhase.Closed);

            Assert.That(root.localPosition.Equals(restPosition) && root.localRotation.Equals(restRotation), Is.True);
            Assert.That(panel.localPosition.Equals(panelClosed), Is.True);
            Assert.That(_camera.transform.localRotation, Is.EqualTo(cameraRotation));
            gameplayRoot.AssertUnchanged("after turning round");
        }

        [UnityTest]
        public IEnumerator RepeatedEnterAndExitNeverDuplicatesSubscriptionsOrDrifts()
        {
            yield return new EnterPlayMode(false);
            yield return Boot();
            yield return FaceThePc();

            int assembly = Listeners(_pc.Assembly, "Changed");
            int carry = Listeners(_carry, "CarriedItemChanged");
            int language = Listeners(_localization, "LanguageChanged");
            int inventory = Listeners(_inventory.Inventory, "Changed");
            Transform root = _presentation.PresentationRoot;
            Transform panel = _presentation.SidePanel;
            Vector3 restPosition = root.localPosition;
            Quaternion restRotation = root.localRotation;
            Vector3 restScale = root.localScale;
            Vector3 panelPosition = panel.localPosition;
            Quaternion panelRotation = panel.localRotation;
            Quaternion cameraRotation = _camera.transform.localRotation;
            GameplayRoot gameplayRoot = new(_workbench.transform);

            for (int i = 0; i < 5; i++)
            {
                Assert.That(_workbench.Open(), Is.True);
                Assert.That(_workbench.Open(), Is.False, "already open");
                Assert.That(Listeners(_pc.Assembly, "Changed"), Is.EqualTo(assembly + 1));
                Assert.That(Listeners(_carry, "CarriedItemChanged"), Is.EqualTo(carry + 2), "the Workbench and its parts panel");
                Assert.That(Listeners(_localization, "LanguageChanged"), Is.EqualTo(language + 1));
                Assert.That(Listeners(_inventory.Inventory, "Changed"), Is.EqualTo(inventory + 2), "the parts list and its capacity line");
                yield return WaitForPhase(PcBuildModePhase.Open);
                Assert.That(Vector3.Distance(_presentation.ViewAnchor.position, _camera.transform.position), Is.LessThan(0.001f), $"visit {i + 1} arrives in the build pose");

                _workbench.Close();
                _workbench.Close();
                yield return WaitForPhase(PcBuildModePhase.Closed);
                gameplayRoot.AssertUnchanged($"after visit {i + 1}");
            }

            Assert.That(Listeners(_pc.Assembly, "Changed"), Is.EqualTo(assembly));
            Assert.That(Listeners(_carry, "CarriedItemChanged"), Is.EqualTo(carry));
            Assert.That(Listeners(_localization, "LanguageChanged"), Is.EqualTo(language));
            Assert.That(Listeners(_inventory.Inventory, "Changed"), Is.EqualTo(inventory));
            Assert.That(_player.Controls.IsAllowed(PlayerControlMask.All), Is.True, "no control block leaks");

            // No drift after five full visits: every transient pose is back bit for bit.
            Assert.That(root.localPosition.Equals(restPosition), Is.True, $"{root.localPosition:F7} != {restPosition:F7}");
            Assert.That(root.localRotation.Equals(restRotation), Is.True);
            Assert.That(root.localScale.Equals(restScale), Is.True);
            Assert.That(panel.localPosition.Equals(panelPosition) && panel.localRotation.Equals(panelRotation), Is.True, "the side panel is back bit for bit");
            Assert.That(panel.gameObject.activeSelf, Is.True);
            Assert.That(_camera.transform.localRotation, Is.EqualTo(cameraRotation));
        }

        [UnityTest]
        public IEnumerator PartsPanelTakesPcPartsRefusesEverythingElseAndClickInstalls()
        {
            yield return new EnterPlayMode(false);
            yield return Boot();

            WorldItem gpu = Store(Spawn(GpuDefinition));
            WorldItem banana = Store(Spawn("BananaDefinition"));
            WorldItem motherboard = Store(Spawn("Item_UsedMotherboard"));
            ItemInstance instance = gpu.Instance;
            Renderer highlight = Field<Renderer>(_slot, "highlight");

            Assert.That(_workbench.Open(), Is.True);
            yield return WaitForPhase(PcBuildModePhase.Open);

            string gpuName = _localization.Text("pc.component.gpu");
            Assert.That(Note(Row(gpu)), Is.EqualTo(_localization.Format("pc.part.installable", gpuName)));
            Assert.That(RowAlpha(Row(gpu)), Is.EqualTo(1f));
            Assert.That(Note(Row(motherboard)), Is.EqualTo(_localization.Format("pc.part.slot_taken", _localization.Text("pc.component.motherboard"))), "its place is taken by the starter board");
            Assert.That(Note(Row(banana)), Is.EqualTo(_localization.Text("pc.part.not_part")));
            Assert.That(RowAlpha(Row(banana)), Is.LessThan(1f), "non-parts are faded");
            Assert.That(highlight.enabled, Is.True, "the empty slot shows where a part can go");
            Assert.That(_hud.Status, Does.Contain(gpuName).And.Contain(_localization.Text("pc.workbench.missing")), "the status says what is missing");

            yield return Click(Centre(Row(banana).transform));
            Assert.That(_carry.HasItem, Is.False, "a banana stays in the Inventory");
            Assert.That(_hud.Action, Is.EqualTo(_localization.Format("pc.feedback.not_part", _localization.Text(banana.Definition.NameLocalizationKey))));

            yield return Click(Centre(Row(gpu).transform));
            Assert.That(_carry.CarriedItem, Is.SameAs(gpu));
            Assert.That(instance.Location, Is.EqualTo(ItemLocation.Carried));
            Assert.That(gpu.GetComponentsInChildren<Renderer>().All(renderer => !renderer.enabled), Is.True, "the held part is not drawn in the build view");
            Assert.That(highlight.enabled, Is.True);

            int worldItems = WorldItems().Length;
            yield return PointAt(_slot);

            Assert.That(_workbench.TargetSlot, Is.SameAs(_slot));
            Assert.That(_workbench.IsGhostVisible, Is.True);
            GameObject ghost = GhostRoot();
            Assert.That(ghost.transform.parent, Is.SameAs(_slot.InstallAnchor));
            Assert.That(ghost.GetComponentsInChildren<WorldItem>(true), Is.Empty);
            Assert.That(ghost.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(ghost.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(WorldItems(), Has.Length.EqualTo(worldItems), "the ghost is not an item");
            Assert.That(CaptureSave().Items, Has.Length.EqualTo(worldItems), "the ghost is not saved");
            Assert.That(_hud.Action, Does.Contain(_localization.Text("pc.install.gpu")), "the stale refusal gave way to the slot's action");
            Bounds ghostBounds = BoundsOf(ghost);

            yield return Click(_pointer);

            Assert.That(gpu.Instance, Is.SameAs(instance));
            Assert.That(instance.Location, Is.EqualTo(ItemLocation.Installed));
            Assert.That(gpu.transform.parent, Is.SameAs(_slot.InstallAnchor));
            Assert.That(_carry.HasItem, Is.False);
            Assert.That(gpu.GetComponentsInChildren<Renderer>().All(renderer => renderer.enabled), Is.True, "installed parts are drawn again");
            Vector3 scale = gpu.transform.lossyScale;
            Assert.That(scale.x, Is.EqualTo(scale.y).Within(1e-4f), "not stretched");
            Assert.That(scale.y, Is.EqualTo(scale.z).Within(1e-4f));
            Assert.That(Vector3.Distance(BoundsOf(gpu.gameObject).center, ghostBounds.center), Is.LessThan(0.001f), "ghost pose == installed pose");
            Assert.That(Vector3.Distance(BoundsOf(gpu.gameObject).size, ghostBounds.size), Is.LessThan(0.001f));
            Assert.That(_workbench.IsGhostVisible, Is.False);
            Assert.That(highlight.enabled, Is.False, "nothing over an installed part");
            Assert.That(Note(Row(motherboard)), Is.Not.Empty);
            Assert.That(_hud.Status, Does.Contain(_localization.Text(gpu.Definition.NameLocalizationKey)));
            Assert.That(_hud.Action, Does.StartWith("[F]").And.Contain(_localization.Text("pc.remove.gpu")));

            yield return Press(Key.F, Action(_interactor, "useAction"));
            Assert.That(_carry.CarriedItem, Is.SameAs(gpu));
            Assert.That(instance.Location, Is.EqualTo(ItemLocation.Carried));
            Assert.That(_pc.Assembly.IsSlotOccupied("gpu-0"), Is.False);

            yield return Click(Centre(Field<InventoryItemView>(_parts, "heldItem").transform));
            Assert.That(_carry.HasItem, Is.False, "a click on the hands card puts the part back");
            Assert.That(_inventory.Inventory.Contains(instance.InstanceId), Is.True);

            _workbench.Close();
            yield return WaitForPhase(PcBuildModePhase.Closed);
        }

        [UnityTest]
        public IEnumerator WhatTheHandsHoldIsHiddenOnlyWhileBuilding()
        {
            yield return new EnterPlayMode(false);
            yield return Boot();

            WorldItem gpu = Spawn(GpuDefinition);
            Assert.That(_carry.TryCarry(gpu), Is.True);
            Renderer[] renderers = gpu.GetComponentsInChildren<Renderer>();
            Assert.That(renderers.All(renderer => renderer.enabled), Is.True);

            Assert.That(_workbench.Open(), Is.True);
            Assert.That(renderers.All(renderer => !renderer.enabled), Is.True, "hidden from the first frame");
            yield return WaitForPhase(PcBuildModePhase.Open);

            _workbench.Close();
            yield return WaitForPhase(PcBuildModePhase.Closed);
            Assert.That(renderers.All(renderer => renderer.enabled), Is.True, "drawn again when the mode ends");
            Assert.That(_carry.CarriedItem, Is.SameAs(gpu), "still in the hands");
        }

        // The final close-up in a 16:9 view: the case fills about two thirds of the height, left of centre; the graphics
        // card slot is on screen with nothing in the way and answers the pointer; the build UI keeps clear of both.
        [UnityTest]
        public IEnumerator CloseUpFillsTwoThirdsOfA16By9ViewLeftOfThePartsPanel()
        {
            yield return new EnterPlayMode(false);
            yield return Boot();
            yield return FaceThePc();

            Assert.That(_workbench.Open(), Is.True);
            yield return WaitForPhase(PcBuildModePhase.Open);

            Camera probe = Probe16By9();
            Rect shell = Project(probe, ChassisCorners());
            Rect slot = Project(probe, SlotCorners(_slot));
            TestContext.WriteLine($"case in the 16:9 close-up: x {shell.xMin:F3}..{shell.xMax:F3} (width {shell.width:F3}), y {shell.yMin:F3}..{shell.yMax:F3} (height {shell.height:F3}); graphics card slot x {slot.xMin:F3}..{slot.xMax:F3}, y {slot.yMin:F3}..{slot.yMax:F3}");

            Assert.That(shell.height, Is.InRange(0.58f, 0.67f), "the PC fills about two thirds of the view height");
            Assert.That(shell.xMin > 0.02f && shell.xMax < 0.98f && shell.yMin > 0.02f && shell.yMax < 0.98f, Is.True, "the whole case is readable");
            Assert.That(shell.center.x, Is.InRange(0.33f, 0.45f), "slightly left of centre");
            Assert.That(slot.xMin > 0f && slot.xMax < 1f && slot.yMin > 0f && slot.yMax < 1f, Is.True, "the graphics card slot is on screen");

            Vector3 eye = _camera.transform.position;
            List<string> blockers = new();

            foreach (Vector3 target in SlotCorners(_slot).Append(_slot.transform.TransformPoint(_slot.TargetBounds.center)))
            {
                blockers.AddRange(Physics.RaycastAll(eye, target - eye, Vector3.Distance(eye, target), ~0, QueryTriggerInteraction.Ignore)
                    .Where(hit => !hit.transform.IsChildOf(_workbench.transform))
                    .Select(hit => hit.collider.name));
            }

            Assert.That(blockers, Is.Empty, "nothing stands between the eye and the graphics card slot");

            // The build UI laid out at 1920x1080.
            Canvas canvas = _hud.GetComponentInParent<Canvas>().rootCanvas;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = probe;
            canvas.planeDistance = probe.nearClipPlane + 0.05f;
            yield return null;
            Canvas.ForceUpdateCanvases();

            foreach (string block in new[] { "StatusPanel", "Card", "PartsPanel" })
            {
                Rect ui = Project(probe, WorldCorners((RectTransform)_hud.transform.Find(block)));
                float covered = Overlap(ui, shell) / (shell.width * shell.height);
                TestContext.WriteLine($"{block}: x {ui.xMin:F3}..{ui.xMax:F3}, y {ui.yMin:F3}..{ui.yMax:F3}; covers {covered:P1} of the case");

                Assert.That(covered, Is.LessThan(0.05f), $"{block} does not cover the case");
                Assert.That(Overlap(ui, slot), Is.Zero, $"{block} keeps clear of the graphics card slot");

                if (block == "PartsPanel")
                    Assert.That(ui.xMin, Is.GreaterThan(shell.xMax), "the parts panel stays right of the case");
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
            Dispose(probe);

            yield return PointAt(_slot);
            Assert.That(_workbench.TargetSlot, Is.SameAs(_slot), "the pointer finds the slot in the close-up");

            _workbench.Close();
            yield return WaitForPhase(PcBuildModePhase.Closed);
        }

        // The side panel: on the case, then off it and out past the eye; hidden only once it is out of the view, and back
        // on the case exactly when the PC is put back.
        [UnityTest]
        public IEnumerator SidePanelLeavesPastTheEyeAndIsHiddenOnlyOutOfView()
        {
            yield return new EnterPlayMode(false);
            yield return Boot();
            yield return FaceThePc();

            Transform panel = _presentation.SidePanel;
            Vector3 closedPosition = panel.localPosition;
            Quaternion closedRotation = panel.localRotation;
            Camera probe = Probe16By9();
            float travelled = 0f;
            bool seenMoving = false;
            bool hiddenOutOfView = false;
            int hiddenFrames = 0;

            Assert.That(_workbench.Open(), Is.True);
            float deadline = Time.realtimeSinceStartup + 5f;

            while ((_workbench.Phase == PcBuildModePhase.Opening || hiddenFrames == 0) && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                probe.transform.SetPositionAndRotation(_camera.transform.position, _camera.transform.rotation);

                if (panel.gameObject.activeSelf)
                {
                    Assert.That(hiddenFrames, Is.Zero, "once gone, the panel stays gone");
                    float away = Vector3.Distance(panel.position, panel.parent.TransformPoint(closedPosition));
                    travelled = Mathf.Max(travelled, away);
                    seenMoving |= away > 0.05f && !IsOutOfView(probe, PanelCorners());
                    Assert.That(CutByNearPlane(probe, panel), Is.False, "the panel passes beside the eye, never through the near plane");
                }
                else if (hiddenFrames++ == 0)
                {
                    hiddenOutOfView = IsOutOfView(probe, PanelCorners());
                    TestContext.WriteLine($"panel hidden at cover amount {Amount("CoverAmount"):F3}, {travelled:F2} m from its place on the case, eye-space position {_camera.transform.InverseTransformPoint(panel.position):F2}");
                }

                Assert.That(_workbench.Phase, Is.Not.EqualTo(PcBuildModePhase.Closed));
            }

            Assert.That(travelled, Is.GreaterThan(0.5f), "the panel travelled well away from the case");
            Assert.That(seenMoving, Is.True, "the panel is seen coming off, it does not just vanish");
            Assert.That(hiddenOutOfView, Is.True, "the panel is hidden only once it is out of the view");

            yield return WaitForPhase(PcBuildModePhase.Open);
            Assert.That(panel.gameObject.activeSelf, Is.False);

            _workbench.Close();
            bool back = false;

            while (_workbench.Phase == PcBuildModePhase.Closing)
            {
                back |= panel.gameObject.activeSelf && Amount("CoverAmount") > 0.5f;
                yield return null;
            }

            yield return null;
            Dispose(probe);
            Assert.That(back, Is.True, "the panel comes back the same way");
            Assert.That(panel.gameObject.activeSelf, Is.True);
            Assert.That(panel.localPosition.Equals(closedPosition) && panel.localRotation.Equals(closedRotation), Is.True, "on the case exactly as authored");
        }

        // Inventory -> hands -> installed in the close-up -> saved while the PC is presented -> loaded twice -> removed in
        // the close-up -> hands -> Inventory: one GPU with one identity all the way, and no save carries a presentation
        // pose. The GPU is bought and delivered, so the saves pass the load preflight; they stay in memory (validated
        // and applied through the real save controller).
        [UnityTest]
        public IEnumerator GpuKeepsItsIdentityThroughTheCloseUpAndSavesCarryNoPresentationPose()
        {
            yield return new EnterPlayMode(false);
            yield return Boot();

            List<WorldItem> delivered = new();
            yield return DeliverGpu(delivered);
            WorldItem gpu = Store(delivered.Single());
            yield return FaceThePc();
            string id = gpu.Instance.InstanceId;
            Transform root = _presentation.PresentationRoot;
            Transform panel = _presentation.SidePanel;
            Vector3 restPosition = root.localPosition;
            Quaternion restRotation = root.localRotation;
            Vector3 panelClosed = panel.localPosition;

            Assert.That(_pc.Capabilities.GamingGraphicsAvailable, Is.False, "F: before the card");
            Assert.That(_pc.Capabilities.CanPlayCriticalStrike, Is.False);

            Assert.That(_workbench.Open(), Is.True);
            yield return WaitForPhase(PcBuildModePhase.Open);
            yield return Click(Centre(Row(gpu).transform));
            Assert.That(_carry.CarriedItem, Is.SameAs(gpu), "Inventory -> hands");
            yield return PointAt(_slot);
            yield return Click(_pointer);

            Assert.That(gpu.Instance.Location, Is.EqualTo(ItemLocation.Installed), "hands -> slot");
            Assert.That(_pc.Capabilities.GamingGraphicsAvailable, Is.True, "F: after the card");
            Assert.That(_pc.Capabilities.CanPlayCriticalStrike, Is.True, "with every starter part still installed");
            Assert.That(_hud.Status, Does.Not.Contain(_localization.Text("pc.diagnostic.no_gpu.title")), "the limitation is gone from the status");
            Assert.That(gpu.transform.parent, Is.SameAs(_slot.InstallAnchor));
            Assert.That(gpu.transform.IsChildOf(root), Is.True, "the real GPU travels with the presented PC");
            Assert.That(Gpus(), Has.Length.EqualTo(1));

            GameSaveData presented = CaptureSave();
            ItemSaveData savedGpu = presented.Items.Single(item => item.InstanceId == id);
            Assert.That(savedGpu.Position, Is.EqualTo(Vector3.zero), "installed parts carry no world pose");
            Assert.That(savedGpu.Rotation, Is.EqualTo(Quaternion.identity));

            WorldItem package = WorldItems().First(item => item.IsRuntime && item.Instance.Location == ItemLocation.World);
            ItemSaveData savedPackage = presented.Items.Single(item => item.InstanceId == package.Instance.InstanceId);
            Assert.That(savedPackage.Position, Is.EqualTo(package.transform.position), "items lying in the world keep their own pose");
            Assert.That(Quaternion.Angle(savedPackage.Rotation, package.transform.rotation), Is.LessThan(0.01f));

            _workbench.Close();
            yield return WaitForPhase(PcBuildModePhase.Closed);
            GameSaveData resting = CaptureSave();
            Assert.That(PersistentState(presented), Is.EqualTo(PersistentState(resting)), "a save made in the close-up is the save made with the PC in its place");

            for (int i = 0; i < 2; i++)
            {
                Load(presented);
                yield return Frames(3);

                WorldItem loaded = Gpus().Single();
                Assert.That(loaded.Instance.InstanceId, Is.EqualTo(id), $"load {i + 1}: the same GPU");
                Assert.That(loaded.Instance.Location, Is.EqualTo(ItemLocation.Installed));
                Assert.That(loaded.transform.parent, Is.SameAs(_slot.InstallAnchor));
                Assert.That(root.localPosition.Equals(restPosition) && root.localRotation.Equals(restRotation), Is.True, "after a load the PC starts in its place");
                Assert.That(panel.gameObject.activeSelf && panel.localPosition.Equals(panelClosed), Is.True, "with its side panel on");
                Assert.That(_pc.Capabilities.CanPlayCriticalStrike, Is.True, $"load {i + 1}: the loaded card counts");
                Assert.That(_pc.Assembly.InstalledComponents.Select(component => component.SlotId), Is.EqualTo(StarterSlots.Append("gpu-0")));
            }

            yield return FaceThePc();
            Assert.That(_workbench.Open(), Is.True);
            yield return WaitForPhase(PcBuildModePhase.Open);
            WorldItem installed = Gpus().Single();
            yield return PointAt(_slot);
            yield return Press(Key.F, Action(_interactor, "useAction"));
            Assert.That(_carry.CarriedItem, Is.SameAs(installed), "slot -> hands");
            Assert.That(installed.Instance.Location, Is.EqualTo(ItemLocation.Carried));

            yield return Click(Centre(Field<InventoryItemView>(_parts, "heldItem").transform));
            Assert.That(_inventory.Inventory.Contains(id), Is.True, "hands -> Inventory");
            Assert.That(_carry.HasItem, Is.False);

            _workbench.Close();
            yield return WaitForPhase(PcBuildModePhase.Closed);
            Assert.That(Gpus().Single().Instance.InstanceId, Is.EqualTo(id), "still one GPU with its identity");
            Assert.That(_pc.Assembly.IsSlotOccupied("gpu-0"), Is.False);
            Assert.That(_pc.Assembly.InstalledComponents.Select(component => component.SlotId), Is.EqualTo(StarterSlots), "the starter parts stay");
            Assert.That(_pc.Capabilities.GamingGraphicsAvailable, Is.False, "F: the card is out again");
            Assert.That(_pc.Capabilities.CanPlayCriticalStrike, Is.False);
            Assert.That(_pc.Capabilities.CanPowerOn, Is.True);
            Assert.That(root.localPosition.Equals(restPosition) && root.localRotation.Equals(restRotation), Is.True);
        }

        // Stage 2A, cases A and B in the real GL scene: the new game's PC is a real computer without a graphics card, and
        // the Workbench says so in words.
        [UnityTest]
        public IEnumerator NewGamePcHasItsStarterPartsAndTheWorkbenchNamesTheMissingGraphicsCard()
        {
            yield return new EnterPlayMode(false);
            yield return Boot();

            foreach (string slotId in StarterSlots)
            {
                Assert.That(_pc.TryGetSlot(slotId, out PcComponentSlot slot), Is.True, slotId);
                Assert.That(_pc.TryGetInstalledItem(slot, out WorldItem installed), Is.True, slotId);
                Assert.That(installed.IsRuntime, Is.False, $"{slotId}: a scene-authored item");
                Assert.That(installed.Instance.InstanceId, Is.EqualTo(installed.AuthoredInstanceId), $"{slotId}: its persistent scene identity");
                Assert.That(installed.Instance.Location, Is.EqualTo(ItemLocation.Installed));
                Assert.That(installed.transform.parent, Is.SameAs(slot.InstallAnchor));
            }

            Assert.That(WorldItems().Select(item => item.Instance.InstanceId), Is.Unique, "no duplicate identities in the scene");
            PcCapabilities pc = _pc.Capabilities;
            Assert.That(pc.CanPowerOn && pc.CanUseDesktop, Is.True);
            Assert.That(pc.GamingGraphicsAvailable || pc.CanPlayCriticalStrike, Is.False);
            Assert.That(_pc.TryPowerOn().Outcome, Is.EqualTo(PcPowerOnOutcome.Started));

            yield return FaceThePc();
            Assert.That(_workbench.Open(), Is.True);
            yield return WaitForPhase(PcBuildModePhase.Open);

            string status = _hud.Status;
            TestContext.WriteLine(status);

            foreach (string slotId in StarterSlots)
            {
                _pc.TryGetSlot(slotId, out PcComponentSlot slot);
                _pc.TryGetInstalledItem(slot, out WorldItem part);
                Assert.That(status, Does.Contain($"{_localization.Text(slot.ComponentType.NameKey())} — {_localization.Text(part.Definition.NameLocalizationKey)}"), slotId);
            }

            Assert.That(status, Does.Contain($"{_localization.Text("pc.component.gpu")} — {_localization.Text("pc.workbench.missing")}"));
            Assert.That(status, Does.Contain(_localization.Text("pc.diagnostic.no_gpu.title")).And.Contain(_localization.Text("pc.diagnostic.no_gpu.detail")));
            Assert.That(status, Does.Not.Contain("starter-").And.Not.Contain("-0").And.Not.Contain(nameof(PcDiagnosticCode.NoDedicatedGpu)), "no internal IDs or enum names");

            foreach (WorldItem part in WorldItems().Where(item => !item.IsRuntime))
                Assert.That(status, Does.Not.Contain(part.Instance.InstanceId));

            _workbench.Close();
            yield return WaitForPhase(PcBuildModePhase.Closed);
        }

        // Cases C and D: the real starter memory module out through the Workbench, into the Inventory, through a save
        // file and back into its slot, one identity all the way.
        [UnityTest]
        public IEnumerator StarterMemoryComesOutThroughTheWorkbenchSurvivesASaveAndGoesBackIn()
        {
            yield return new EnterPlayMode(false);
            yield return Boot();
            yield return FaceThePc();

            Assert.That(_pc.TryGetSlot("ram-0", out PcComponentSlot ramSlot), Is.True);
            Assert.That(_pc.TryGetInstalledItem(ramSlot, out WorldItem ram), Is.True);
            ItemInstance instance = ram.Instance;
            string id = instance.InstanceId;
            int items = WorldItems().Length;

            Assert.That(_workbench.Open(), Is.True);
            yield return WaitForPhase(PcBuildModePhase.Open);
            yield return PointAt(ramSlot);
            Assert.That(_workbench.TargetSlot, Is.SameAs(ramSlot), "the pointer finds the memory slot in the close-up");
            Assert.That(_hud.Action, Does.Contain(_localization.Text("pc.remove.ram")));
            yield return Press(Key.F, Action(_interactor, "useAction"));

            Assert.That(_carry.CarriedItem, Is.SameAs(ram), "slot -> hands, the same item");
            Assert.That(ram.Instance, Is.SameAs(instance));
            Assert.That(instance.Location, Is.EqualTo(ItemLocation.Carried));
            Assert.That(_pc.Capabilities.CanPowerOn, Is.False);
            Assert.That(_pc.Capabilities.Diagnostics.First().Code, Is.EqualTo(PcDiagnosticCode.MissingMemory));
            Assert.That(_hud.Status, Does.Contain(_localization.Text("pc.diagnostic.no_memory.title")), "the status says why the PC cannot start");

            yield return Click(Centre(Field<InventoryItemView>(_parts, "heldItem").transform));
            Assert.That(_inventory.Inventory.Contains(id), Is.True, "hands -> Inventory");
            _workbench.Close();
            yield return WaitForPhase(PcBuildModePhase.Closed);

            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "GoLiveSaveTests", System.Guid.NewGuid().ToString("N"), "save.json");

            try
            {
                Assert.That(SaveTo(path), Is.True);
                Assert.That(LoadFrom(path), Is.True);
                yield return Frames(3);

                Assert.That(_inventory.Inventory.Contains(id), Is.True, "loaded into the Inventory");
                Assert.That(WorldItems().Single(item => item.Instance.InstanceId == id), Is.SameAs(ram));
                Assert.That(_pc.Assembly.IsSlotOccupied("ram-0"), Is.False, "the memory slot stays empty");
                Assert.That(_pc.Capabilities.CanPowerOn, Is.False, "the PC still cannot start");
                Assert.That(WorldItems(), Has.Length.EqualTo(items), "no duplicate");

                yield return FaceThePc();
                Assert.That(_workbench.Open(), Is.True);
                yield return WaitForPhase(PcBuildModePhase.Open);
                yield return Click(Centre(Row(ram).transform));
                Assert.That(_carry.CarriedItem, Is.SameAs(ram), "Inventory -> hands");
                yield return PointAt(ramSlot);
                Assert.That(_workbench.IsGhostVisible, Is.True);
                yield return Click(_pointer);

                Assert.That(ram.Instance.InstanceId, Is.EqualTo(id));
                Assert.That(ram.Instance.Location, Is.EqualTo(ItemLocation.Installed), "hands -> slot");
                Assert.That(ram.transform.parent, Is.SameAs(ramSlot.InstallAnchor));
                Assert.That(_pc.Capabilities.CanPowerOn, Is.True, "it starts again");
                _workbench.Close();
                yield return WaitForPhase(PcBuildModePhase.Closed);

                Assert.That(SaveTo(path), Is.True);
                Assert.That(LoadFrom(path), Is.True);
                yield return Frames(3);

                Assert.That(_pc.TryGetInstalledItem(ramSlot, out WorldItem loaded) && loaded.Instance.InstanceId == id, Is.True, "the same module in the same slot");
                Assert.That(_pc.Capabilities.CanPowerOn, Is.True);
                Assert.That(_pc.Assembly.InstalledComponents.Select(component => component.SlotId), Is.EqualTo(StarterSlots));
                Assert.That(WorldItems(), Has.Length.EqualTo(items));
            }
            finally
            {
                if (System.IO.Directory.Exists(System.IO.Path.GetDirectoryName(path)))
                    System.IO.Directory.Delete(System.IO.Path.GetDirectoryName(path), true);
            }
        }

        // The motherboard is fixed for now: the Workbench explains instead of taking it out.
        [UnityTest]
        public IEnumerator TheWorkbenchExplainsThatTheMotherboardStaysIn()
        {
            yield return new EnterPlayMode(false);
            yield return Boot();
            yield return FaceThePc();

            Assert.That(_pc.TryGetSlot("motherboard-0", out PcComponentSlot board), Is.True);
            Assert.That(_workbench.Open(), Is.True);
            yield return WaitForPhase(PcBuildModePhase.Open);

            yield return PointAt(board);
            Assert.That(_workbench.TargetSlot, Is.SameAs(board), "the bare board answers the pointer");
            Assert.That(_hud.Action, Is.EqualTo(_localization.Text("pc.reject.fixed")));
            yield return Press(Key.F, Action(_interactor, "useAction"));

            Assert.That(_carry.HasItem, Is.False);
            Assert.That(_pc.Assembly.IsSlotOccupied("motherboard-0"), Is.True);
            Assert.That(_pc.Capabilities.HasMotherboard, Is.True);

            _workbench.Close();
            yield return WaitForPhase(PcBuildModePhase.Closed);
        }

        // ---------------------------------------------------------------- helpers

        private static readonly string[] StarterSlots = { "motherboard-0", "cpu-0", "ram-0", "psu-0", "storage-0" };

        // A real save file through the GL scene's save controller (validation, apply), in a test-owned folder.
        private static bool SaveTo(string path)
        {
            GameSaveController save = Object.FindAnyObjectByType<GameSaveController>();
            return (bool)typeof(GameSaveController).GetMethod("TrySave", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(save, new object[] { path });
        }

        private static bool LoadFrom(string path)
        {
            GameSaveController save = Object.FindAnyObjectByType<GameSaveController>();
            return (bool)typeof(GameSaveController).GetMethod("TryLoad", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(save, new object[] { path });
        }

        private IEnumerator Boot()
        {
            yield return Frames(10);

            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;

            foreach (InputDevice stale in InputSystem.devices.Where(device => device is Keyboard or Mouse).ToList())
                InputSystem.RemoveDevice(stale);

            _keyboard = InputSystem.AddDevice<Keyboard>();
            _mouse = InputSystem.AddDevice<Mouse>();
            yield return Frames(2);

            _workbench = Object.FindAnyObjectByType<PcWorkbenchBehaviour>();
            _pc = _workbench.GetComponent<PcAssemblyBehaviour>();
            _presentation = _workbench.GetComponent<PcBuildPresentation>();
            Assert.That(_pc.TryGetSlot("gpu-0", out _slot), Is.True);
            _player = Object.FindAnyObjectByType<PlayerController>();
            _carry = _player.GetComponent<PlayerCarry>();
            _inventory = _player.GetComponent<PlayerInventory>();
            _interactor = _player.GetComponent<PlayerInteractor>();
            _tab = Object.FindAnyObjectByType<InventoryUiController>(FindObjectsInactive.Include);
            _router = Object.FindAnyObjectByType<GameUiInputRouter>();
            _phone = Object.FindAnyObjectByType<PhoneBehaviour>(FindObjectsInactive.Include);
            _hud = Object.FindAnyObjectByType<PcWorkbenchHudView>(FindObjectsInactive.Include);
            _parts = Object.FindAnyObjectByType<PcBuildInventoryView>(FindObjectsInactive.Include);
            _localization = Object.FindAnyObjectByType<LocalizationContext>();
            _camera = Field<Camera>(_workbench, "playerCamera");

            Assert.That(_workbench.isActiveAndEnabled && _pc.isActiveAndEnabled && _presentation.isActiveAndEnabled && _hud.isActiveAndEnabled && _parts.isActiveAndEnabled, Is.True, "GL build mode is wired");
            Assert.That(_pc.IsReady, Is.True);
            Assert.That(_pc.Assembly.InstalledComponents.Select(component => component.SlotId), Is.EqualTo(StarterSlots), "a new game starts with the starter parts");
            Assert.That(_pc.Assembly.IsSlotOccupied("gpu-0"), Is.False, "and an empty graphics card slot");
        }

        // In front of the desk, a little toward the room end, looking at the case.
        private IEnumerator FaceThePc()
        {
            Vector3 target = _pc.GetComponent<Collider>().bounds.center;
            Vector3 position = new(target.x - 1.2f, 0.274f, target.z - 0.3f);
            Vector3 flat = Flat(target - position);

            _player.RestorePose(new PlayerPoseSnapshot(position, Quaternion.LookRotation(flat), 0f));
            yield return Frames(3);

            Vector3 toTarget = target - _camera.transform.position;
            float pitch = -Mathf.Atan2(toTarget.y, Flat(toTarget).magnitude) * Mathf.Rad2Deg;
            _player.RestorePose(new PlayerPoseSnapshot(position, Quaternion.LookRotation(flat), pitch));
            yield return Frames(6);
        }

        // Real-time wait (batch frames are sub-millisecond), plus the frame whose LateUpdate applies the final pose.
        private IEnumerator WaitForPhase(PcBuildModePhase phase)
        {
            float deadline = Time.realtimeSinceStartup + 5f;

            while (_workbench.Phase != phase && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(_workbench.Phase, Is.EqualTo(phase));
            yield return null;
        }

        private WorldItem Spawn(string definitionAsset)
        {
            ItemDefinition definition = ShopTestData.LoadItem(definitionAsset);
            WorldItem item = WorldItem.SpawnRuntime(definition, ItemInstance.CreateNew(definition.ItemId), _player.transform.position + Vector3.up * 0.3f, Quaternion.identity);
            Assert.That(item, Is.Not.Null, definitionAsset);
            return item;
        }

        // A Budget GPU the way the game gets one: bought in the Shop, delivered, and taken out of its package with F.
        private IEnumerator DeliverGpu(List<WorldItem> delivered)
        {
            Assert.That(Object.FindAnyObjectByType<ShopBehaviour>().TryPurchase("budget-gpu").Succeeded, Is.True, "the Budget GPU is bought");
            Object.FindAnyObjectByType<GameClockBehaviour>().Clock.AdvanceMinutes(151);
            yield return Frames(30);

            DeliveryPackageBehaviour package = Object.FindObjectsByType<DeliveryPackageBehaviour>(FindObjectsInactive.Exclude)
                .Single(candidate => candidate.Item != null && candidate.Item.Instance != null);
            Assert.That(_carry.TryCarry(package.Item), Is.True);
            yield return Press(Key.F, Action(_interactor, "useAction"));
            yield return Frames(3);

            delivered.Add(Gpus().Single());
            Assert.That(delivered[0].Instance.Location, Is.EqualTo(ItemLocation.World), "unpacked");
        }

        private WorldItem Store(WorldItem item)
        {
            Assert.That(_carry.TryCarry(item), Is.True);
            Assert.That(_inventory.TryStoreCarriedItem(), Is.True);
            return item;
        }

        private InventoryItemView Row(WorldItem item)
        {
            return Field<InventoryListView>(_parts, "list").GetComponentsInChildren<InventoryItemView>(true).Single(view => view.InstanceId == item.Instance.InstanceId);
        }

        private static string Note(InventoryItemView row)
        {
            return Field<TMP_Text>(row, "detail").text;
        }

        private static float RowAlpha(InventoryItemView row)
        {
            return Field<CanvasGroup>(row, "content").alpha;
        }

        private IEnumerator PointAt(PcComponentSlot slot)
        {
            Vector3 screen = _camera.WorldToScreenPoint(slot.transform.TransformPoint(slot.TargetBounds.center));
            Assert.That(screen.z, Is.GreaterThan(0f));
            _pointer = screen;
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = _pointer });
            yield return Frames(4);
        }

        private IEnumerator Click(Vector2 position)
        {
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = position });
            yield return Frames(2);
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = position, buttons = 1 });
            yield return Frames(2);
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = position });
            yield return Frames(3);
        }

        private IEnumerator Press(Key key, InputAction action)
        {
            int performed = 0;
            void OnPerformed(InputAction.CallbackContext context) => performed++;
            action.performed += OnPerformed;

            for (int attempt = 0; attempt < 4 && performed == 0; attempt++)
            {
                InputSystem.QueueStateEvent(_keyboard, new KeyboardState(key));
                yield return Frames(2);
                InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
                yield return Frames(2);
            }

            action.performed -= OnPerformed;
            Assert.That(performed, Is.EqualTo(1), $"{key} reached '{action.name}' exactly once");
            yield return Frames(1);
        }

        private IEnumerator Hold(params Key[] keys)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(keys));
            yield return Frames(2);
        }

        private IEnumerator Release()
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return Frames(2);
        }

        private void AssertPose(PlayerPoseSnapshot expected)
        {
            PlayerPoseSnapshot pose = _player.CapturePose();
            Assert.That(Vector3.Distance(Flat(pose.Position), Flat(expected.Position)), Is.LessThan(0.02f), "the player stays where they stood");
            Assert.That(Quaternion.Angle(pose.Rotation, expected.Rotation), Is.LessThan(0.1f));
            Assert.That(pose.Pitch, Is.EqualTo(expected.Pitch).Within(0.1f));
        }

        private float Amount(string property)
        {
            object sequence = typeof(PcWorkbenchBehaviour).GetField("_sequence", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_workbench);
            return (float)sequence.GetType().GetProperty(property).GetValue(sequence);
        }

        private float GameplayHudAlpha()
        {
            return Field<CanvasGroup[]>(_hud, "gameplayHud").Max(group => group.alpha);
        }

        private GameObject GhostRoot()
        {
            return (GameObject)typeof(PcWorkbenchBehaviour).GetProperty("GhostRoot", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_workbench);
        }

        private static GameSaveData CaptureSave()
        {
            GameSaveController save = Object.FindAnyObjectByType<GameSaveController>();
            return (GameSaveData)typeof(GameSaveController).GetMethod("Capture", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(save, null);
        }

        private static WorldItem[] WorldItems()
        {
            return Object.FindObjectsByType<WorldItem>(FindObjectsInactive.Include).Where(item => item.Instance != null).ToArray();
        }

        private static int Listeners(object owner, string eventName)
        {
            Delegate handler = (Delegate)owner.GetType().GetField(eventName, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
            return handler?.GetInvocationList().Length ?? 0;
        }

        private static InputAction Action(object owner, string field)
        {
            return Field<InputActionReference>(owner, field).action;
        }

        private static T Field<T>(object owner, string name)
        {
            return (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
        }

        private static Vector2 Centre(Transform transform)
        {
            Canvas.ForceUpdateCanvases();
            RectTransform rect = (RectTransform)transform;
            return RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        }

        private static IEnumerable<Vector3> Corners(Bounds b)
        {
            for (int i = 0; i < 8; i++)
                yield return new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
        }

        private static Bounds BoundsOf(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = renderers[0].bounds;

            foreach (Renderer renderer in renderers)
                bounds.Encapsulate(renderer.bounds);

            return bounds;
        }

        private static Vector3 Flat(Vector3 value)
        {
            return new Vector3(value.x, 0f, value.z);
        }

        // A copy of the player's camera rendering 1920x1080, for 16:9 framing and layout checks.
        private Camera Probe16By9()
        {
            Camera probe = new GameObject("16:9 probe").AddComponent<Camera>();
            probe.CopyFrom(_camera);
            probe.targetTexture = new RenderTexture(1920, 1080, 24);
            probe.aspect = 16f / 9f;
            return probe;
        }

        private static void Dispose(Camera probe)
        {
            RenderTexture texture = probe.targetTexture;
            probe.targetTexture = null;
            Object.Destroy(texture);
            Object.Destroy(probe.gameObject);
        }

        // The case itself (every Case part except the removable side panel), as oriented boxes.
        private Vector3[] ChassisCorners()
        {
            return _presentation.PresentationRoot.Find("Case").GetComponentsInChildren<MeshFilter>(true)
                .Where(filter => filter.transform != _presentation.SidePanel)
                .SelectMany(MeshCorners)
                .ToArray();
        }

        private Vector3[] PanelCorners()
        {
            return MeshCorners(_presentation.SidePanel.GetComponent<MeshFilter>()).ToArray();
        }

        private static Vector3[] SlotCorners(PcComponentSlot slot)
        {
            return Corners(slot.TargetBounds).Select(slot.transform.TransformPoint).ToArray();
        }

        private static IEnumerable<Vector3> MeshCorners(MeshFilter filter)
        {
            return Corners(filter.sharedMesh.bounds).Select(filter.transform.TransformPoint);
        }

        private static Vector3[] WorldCorners(RectTransform rect)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return corners;
        }

        private static Rect Project(Camera camera, IEnumerable<Vector3> points)
        {
            Vector3[] view = points.Select(camera.WorldToViewportPoint).ToArray();
            Assert.That(view.All(point => point.z > 0f), Is.True, "in front of the eye");
            return Rect.MinMaxRect(view.Min(point => point.x), view.Min(point => point.y), view.Max(point => point.x), view.Max(point => point.y));
        }

        private static float Overlap(Rect a, Rect b)
        {
            return Mathf.Max(0f, Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin)) * Mathf.Max(0f, Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin));
        }

        // True when one frustum plane has every corner on its outer side: nothing of the box can be in view.
        private static bool IsOutOfView(Camera camera, Vector3[] corners)
        {
            return GeometryUtility.CalculateFrustumPlanes(camera).Any(plane => corners.All(corner => !plane.GetSide(corner)));
        }

        // Samples the panel's volume: is any point inside the view cone but nearer than the near plane (visibly cut)?
        private static bool CutByNearPlane(Camera camera, Transform panel)
        {
            Bounds b = panel.GetComponent<MeshFilter>().sharedMesh.bounds;
            float tanV = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float tanH = tanV * camera.aspect;

            for (int i = 0; i <= 8; i++)
            {
                for (int j = 0; j <= 8; j++)
                {
                    for (int k = 0; k <= 8; k++)
                    {
                        Vector3 local = new(Mathf.Lerp(b.min.x, b.max.x, i / 8f), Mathf.Lerp(b.min.y, b.max.y, j / 8f), Mathf.Lerp(b.min.z, b.max.z, k / 8f));
                        Vector3 eye = camera.transform.InverseTransformPoint(panel.TransformPoint(local));

                        if (eye.z > 0f && eye.z < camera.nearClipPlane && Mathf.Abs(eye.x) < eye.z * tanH && Mathf.Abs(eye.y) < eye.z * tanV)
                            return true;
                    }
                }
            }

            return false;
        }

        private static WorldItem[] Gpus()
        {
            PropertyInfo discarded = typeof(WorldItem).GetProperty("IsDiscarded", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return WorldItems().Where(item => item.Instance.DefinitionId == "budget-gpu" && !(bool)discarded.GetValue(item)).ToArray();
        }

        // The saved state that could carry a presentation pose: the player, every item and the PC record.
        private static string PersistentState(GameSaveData data)
        {
            return JsonUtility.ToJson(data.Player) + string.Concat(data.Items.Select(item => JsonUtility.ToJson(item))) + JsonUtility.ToJson(data.PcAssembly);
        }

        // Through JSON, validation and apply, like a load from disk.
        private static void Load(GameSaveData saved)
        {
            GameSaveController save = Object.FindAnyObjectByType<GameSaveController>();
            GameSaveData data = new();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(saved), data);
            object[] validation = { data, null, null };

            Assert.That((bool)typeof(GameSaveController).GetMethod("ValidateSaveData", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(save, validation), Is.True, "the save validates");
            typeof(GameSaveController).GetMethod("Apply", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(save, new[] { data, validation[1], validation[2] });
        }

        // The PC's gameplay root: its world position, rotation and scale never change in PC Build Mode.
        private readonly struct GameplayRoot
        {
            private readonly Transform _transform;
            private readonly Vector3 _position;
            private readonly Quaternion _rotation;
            private readonly Vector3 _scale;

            public GameplayRoot(Transform transform)
            {
                _transform = transform;
                _position = transform.position;
                _rotation = transform.rotation;
                _scale = transform.localScale;
            }

            public void AssertUnchanged(string when)
            {
                Assert.That(_transform.position.Equals(_position) && _transform.rotation.Equals(_rotation) && _transform.localScale.Equals(_scale), Is.True, $"the PC's gameplay root moved {when}");
            }
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
                yield return null;
        }
    }
}
