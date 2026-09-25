using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GoLive.Delivery;
using GoLive.Food;
using GoLive.Inventory;
using GoLive.Items;
using GoLive.Phone;
using GoLive.Player;
using GoLive.Shop;
using GoLive.Sleep;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GoLive.Tests
{
    // The real Player prefab (controller, interactor, carry) driven by a virtual keyboard through the project's
    // Input Actions: crouch, world/carried package opening with E/F/G, and the phone key.
    public sealed class PlayerControlsPlayModeTests
    {
        private const string PlayerPrefab = "Assets/Game/Prefab/Player/Player.prefab";
        private const string InputAsset = "Assets/Game/_Project/GO_LIVE_Input.inputactions";
        private const float FrameTime = 1f / 60f;
        private const float CrouchHeight = 1.3f;
        private const float SpawnHeight = 0.15f;

        private readonly List<GameObject> _created = new();

        private SceneSetup[] _previousScenes;
        private SaveTestWorld _world;
        private VirtualInput _input;
        private float? _previousCaptureDeltaTime;

        private PlayerController _player;
        private CharacterController _body;
        private Transform _viewPivot;
        private Camera _camera;

        [OneTimeSetUp]
        public void IsolateTestScene()
        {
            _previousScenes = SaveTestWorld.IsolateScene();
        }

        [OneTimeTearDown]
        public void RestoreEditorSceneSetup()
        {
            SaveTestWorld.RestoreScene(_previousScenes);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // What outlives Play Mode first: the input devices and settings and the frame time this test changed.
            _input?.Dispose();
            _input = null;

            if (_previousCaptureDeltaTime.HasValue)
                Time.captureDeltaTime = _previousCaptureDeltaTime.Value;

            _previousCaptureDeltaTime = null;

            for (int i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                    Object.DestroyImmediate(_created[i]);
            }

            _created.Clear();
            _world?.Dispose();
            _world = null;

            if (Application.isPlaying)
                yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator HoldingLeftCtrlCrouchesAndReleasingItStands()
        {
            yield return new EnterPlayMode(false);
            yield return StartRig(Vector3.zero);

            float standingHeight = _body.height;
            Vector3 standingCenter = _body.center;
            float standingEye = _viewPivot.localPosition.y;
            float feet = CapsuleBottom();
            float cameraHeight = _camera.transform.position.y;

            yield return Hold(Key.LeftCtrl);
            yield return PlayModeWait.Frames(20);

            Assert.That(_player.IsCrouching, Is.True);
            Assert.That(_body.height, Is.EqualTo(CrouchHeight).Within(1e-5f));
            Assert.That(CapsuleBottom(), Is.EqualTo(feet).Within(0.01f), "the feet stay on the floor");
            Assert.That(_viewPivot.localPosition.y, Is.EqualTo(standingEye - (standingHeight - CrouchHeight)).Within(1e-4f));
            Assert.That(_camera.transform.position.y, Is.EqualTo(cameraHeight - (standingHeight - CrouchHeight)).Within(0.02f), "the view lowers with the capsule");

            yield return Release();
            yield return PlayModeWait.Frames(20);

            Assert.That(_player.IsCrouching, Is.False);
            Assert.That(_body.height, Is.EqualTo(standingHeight));
            Assert.That(_body.center, Is.EqualTo(standingCenter));
            Assert.That(_viewPivot.localPosition.y, Is.EqualTo(standingEye).Within(1e-5f));
            Assert.That(CapsuleBottom(), Is.EqualTo(feet).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator CrouchedWalkingIsSlowerThanWalking()
        {
            yield return new EnterPlayMode(false);
            yield return StartRig(Vector3.zero);

            float walking = 0f;
            yield return MeasureSpeed(false, speed => walking = speed);

            _player.RestorePose(new PlayerPoseSnapshot(Vector3.zero, Quaternion.identity, 0f));
            yield return PlayModeWait.Frames(5);

            float crouched = 0f;
            yield return MeasureSpeed(true, speed => crouched = speed);

            Assert.That(walking, Is.EqualTo(4.5f).Within(0.15f));
            Assert.That(crouched, Is.EqualTo(2.4f).Within(0.15f));
            Assert.That(_player.IsCrouching, Is.True);
        }

        [UnityTest]
        public IEnumerator LowCeilingKeepsThePlayerCrouchedUntilThereIsRoom()
        {
            yield return new EnterPlayMode(false);
            yield return StartRig(Vector3.zero);

            float standingHeight = _body.height;
            float standingEye = _viewPivot.localPosition.y;

            yield return Hold(Key.LeftCtrl);
            yield return PlayModeWait.Frames(15);
            Assert.That(_player.IsCrouching, Is.True);

            GameObject ceiling = Box("Low ceiling", new Vector3(0f, CapsuleBottom() + 1.5f + 0.1f, 0f), new Vector3(3f, 0.2f, 3f));
            Physics.SyncTransforms();

            yield return Release();
            yield return PlayModeWait.Frames(20);

            Assert.That(_player.IsCrouching, Is.True, "no standing up through the ceiling");
            Assert.That(_body.height, Is.EqualTo(CrouchHeight).Within(1e-5f));
            Assert.That(_camera.transform.position.y, Is.LessThan(ceiling.transform.position.y - 0.1f));

            // Crouch-walking out from under it works; standing needs the whole capsule clear.
            yield return Hold(Key.W);
            yield return PlayModeWait.Frames(3);
            yield return Release();
            Assert.That(_player.IsCrouching, Is.True);

            Object.DestroyImmediate(ceiling);
            Physics.SyncTransforms();
            yield return PlayModeWait.Frames(20);

            Assert.That(_player.IsCrouching, Is.False, "stands once there is room and Ctrl is no longer held");
            Assert.That(_body.height, Is.EqualTo(standingHeight));
            Assert.That(_viewPivot.localPosition.y, Is.EqualTo(standingEye).Within(1e-5f));
        }

        [UnityTest]
        public IEnumerator RepeatedCrouchingNeverDriftsThePlayerHeight()
        {
            yield return new EnterPlayMode(false);
            yield return StartRig(Vector3.zero);

            float standingHeight = _body.height;
            Vector3 standingCenter = _body.center;
            float standingEye = _viewPivot.localPosition.y;
            float feet = CapsuleBottom();

            for (int i = 0; i < 6; i++)
            {
                yield return Hold(Key.LeftCtrl);
                yield return PlayModeWait.Frames(i % 2 == 0 ? 4 : 15);
                Assert.That(_player.IsCrouching, Is.True, $"cycle {i}");

                yield return Release();
                yield return PlayModeWait.Frames(15);

                Assert.That(_body.height, Is.EqualTo(standingHeight), $"cycle {i}");
                Assert.That(_body.center, Is.EqualTo(standingCenter), $"cycle {i}");
                Assert.That(_viewPivot.localPosition.y, Is.EqualTo(standingEye).Within(1e-5f), $"cycle {i}");
                Assert.That(CapsuleBottom(), Is.EqualTo(feet).Within(0.01f), $"cycle {i}");
            }
        }

        [UnityTest]
        public IEnumerator BlockedControlsFreezeTheStance(
            [Values(PlayerControlMask.All, PlayerControlMask.Movement, PlayerControlMask.Crouch)] PlayerControlMask blocked)
        {
            yield return new EnterPlayMode(false);
            yield return StartRig(Vector3.zero);

            IDisposable block = _player.Controls.Block(blocked);
            yield return Hold(Key.LeftCtrl);
            yield return PlayModeWait.Frames(10);
            Assert.That(_player.IsCrouching, Is.False, $"{blocked} blocked: Ctrl does not crouch");

            block.Dispose();
            yield return PlayModeWait.Frames(3);
            Assert.That(_player.IsCrouching, Is.True, "the held key applies once the block is released");

            block = _player.Controls.Block(blocked);
            yield return Release();
            yield return PlayModeWait.Frames(10);
            Assert.That(_player.IsCrouching, Is.True, $"{blocked} blocked: releasing Ctrl does not stand up");

            block.Dispose();
            yield return PlayModeWait.Frames(3);
            Assert.That(_player.IsCrouching, Is.False);
        }

        [UnityTest]
        public IEnumerator ReenablingTheControllerKeepsOneWorkingCrouchAction()
        {
            yield return new EnterPlayMode(false);
            yield return StartRig(Vector3.zero);

            InputAction crouch = Action("Player/Crouch");

            for (int i = 0; i < 3; i++)
            {
                _player.enabled = false;
                Assert.That(crouch.enabled, Is.False);
                _player.enabled = true;
                Assert.That(crouch.enabled, Is.True);
            }

            yield return Hold(Key.LeftCtrl);
            yield return PlayModeWait.Frames(3);
            Assert.That(_player.IsCrouching, Is.True);

            yield return Release();
            yield return PlayModeWait.Frames(3);
            Assert.That(_player.IsCrouching, Is.False);
        }

        [UnityTest]
        public IEnumerator RestoringAPoseUnderALowCeilingArrivesCrouched()
        {
            yield return new EnterPlayMode(false);
            yield return StartRig(Vector3.zero);

            float feet = CapsuleBottom();
            Box("Low shelf", new Vector3(6f, feet + 1.5f + 0.1f, 0f), new Vector3(2f, 0.2f, 2f));
            Physics.SyncTransforms();

            _player.RestorePose(new PlayerPoseSnapshot(new Vector3(6f, 0f, 0f), Quaternion.identity, 0f));
            Assert.That(_player.IsCrouching, Is.True, "loaded under the shelf: crouched, not inside it");
            Assert.That(_body.height, Is.EqualTo(CrouchHeight).Within(1e-5f));

            yield return PlayModeWait.Frames(10);
            Assert.That(_player.IsCrouching, Is.True);

            _player.RestorePose(new PlayerPoseSnapshot(Vector3.zero, Quaternion.identity, 0f));
            Assert.That(_player.IsCrouching, Is.False, "loaded in the open: standing");
        }

        [UnityTest]
        public IEnumerator UnopenedPackageInTheWorldOpensWhereItLiesWithF()
        {
            yield return new EnterPlayMode(false);
            yield return StartDeliveryRig();

            DeliveryPackageBehaviour package = DeliverSnack();
            yield return PlayModeWait.Frames(30);
            PlayerInteractor interactor = _player.GetComponent<PlayerInteractor>();
            PlayerCarry carry = _player.GetComponent<PlayerCarry>();

            yield return AimAt(package.GetComponent<Collider>());
            Assert.That(interactor.Prompts.TakeKey, Is.EqualTo("interaction.take"), "E takes the unopened package");
            Assert.That(interactor.Prompts.WorldUseKey, Is.EqualTo("interaction.open"), "F opens it where it lies");

            yield return Press(Key.E, "Player/Take");
            Assert.That(carry.CarriedItem, Is.SameAs(package.Item));
            Assert.That(interactor.Prompts.HeldUseKey, Is.EqualTo("interaction.open"));
            Assert.That(interactor.Prompts.DropKey, Is.EqualTo("interaction.drop"));

            yield return Press(Key.G, "Player/Drop");
            Assert.That(carry.HasItem, Is.False, "G drops the package");
            Assert.That(package.Item.Instance.Location, Is.EqualTo(ItemLocation.World));
            yield return PlayModeWait.Frames(60);

            yield return AimAt(package.GetComponent<Collider>());
            Vector3 position = package.transform.position;
            Quaternion rotation = package.transform.rotation;
            long balance = _world.Wallet.Wallet.BalanceCents;

            yield return Press(Key.F, "Player/Use");

            DeliveryRecord record = _world.Delivery.State.Records.Single();
            Assert.That(record.Stage, Is.EqualTo(DeliveryStage.Opened));
            Assert.That(carry.HasItem, Is.False);
            Assert.That(package.Item.Instance.Location, Is.EqualTo(ItemLocation.World));
            Assert.That(Vector3.Distance(package.transform.position, position), Is.LessThan(0.01f), "opened exactly where it lies");
            Assert.That(Quaternion.Angle(package.transform.rotation, rotation), Is.LessThan(1f));

            WorldItem banana = RuntimeItems("banana").Single();
            Assert.That(record.FulfillmentInstanceId, Is.EqualTo(banana.Instance.InstanceId));
            Assert.That(Vector3.Distance(banana.transform.position, package.ContentsAnchor.position), Is.LessThan(0.25f), "the item appears at the package");

            yield return AimAt(package.GetComponent<Collider>(), avoid: banana.GetComponentInChildren<Collider>());
            Assert.That(interactor.Prompts.WorldUseKey, Is.Null, "no Open prompt once opened");
            Assert.That(interactor.Prompts.TakeKey, Is.EqualTo("interaction.take"));

            yield return Press(Key.F, "Player/Use");
            yield return Press(Key.F, "Player/Use");

            Assert.That(RuntimeItems("banana"), Has.Count.EqualTo(1), "repeated F creates nothing");
            Assert.That(RuntimeItems("delivery-package"), Has.Count.EqualTo(1));
            Assert.That(_world.Delivery.State.Records.Single().FulfillmentInstanceId, Is.EqualTo(banana.Instance.InstanceId));
            Assert.That(_world.Wallet.Wallet.BalanceCents, Is.EqualTo(balance));
            Assert.That(_world.Shop.Orders.Orders, Has.Count.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator CarriedPackageStillOpensWithF()
        {
            yield return new EnterPlayMode(false);
            yield return StartDeliveryRig();

            DeliveryPackageBehaviour package = DeliverSnack();
            yield return PlayModeWait.Frames(30);
            PlayerCarry carry = _player.GetComponent<PlayerCarry>();

            yield return AimAt(package.GetComponent<Collider>());
            yield return Press(Key.E, "Player/Take");
            Assert.That(carry.CarriedItem, Is.SameAs(package.Item));

            yield return Press(Key.F, "Player/Use");

            Assert.That(carry.HasItem, Is.False, "the package is set down to open");
            Assert.That(package.Item.Instance.Location, Is.EqualTo(ItemLocation.World));
            Assert.That(package.transform.up.y, Is.GreaterThan(0.99f), "placed upright and left alone by physics");

            BoxCollider shape = package.GetComponent<BoxCollider>();
            Vector3 halfExtents = Vector3.Scale(shape.size, shape.transform.lossyScale) * 0.475f;
            Collider[] touching = Physics.OverlapBox(shape.transform.TransformPoint(shape.center), halfExtents, package.transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            Assert.That(touching.Any(collider => collider.transform.IsChildOf(_player.transform)), Is.False, "set down clear of the player's body, even while looking down");
            Assert.That(_world.Delivery.State.Records.Single().Stage, Is.EqualTo(DeliveryStage.Opened));
            Assert.That(RuntimeItems("banana"), Has.Count.EqualTo(1));

            WorldItem banana = RuntimeItems("banana").Single();
            yield return PlayModeWait.Frames(30);
            yield return AimAt(banana.GetComponentInChildren<Collider>());
            yield return Press(Key.E, "Player/Take");
            Assert.That(carry.CarriedItem, Is.SameAs(banana), "the delivered item can be picked up");
        }

        [UnityTest]
        public IEnumerator PhoneActionAnswersQAndIgnoresP()
        {
            yield return new EnterPlayMode(false);
            yield return StartInput();

            InputAction phone = Action("Player/Phone");
            bool wasEnabled = phone.enabled;
            phone.Enable();

            int performed = 0;
            void Count(InputAction.CallbackContext context) => performed++;
            phone.performed += Count;

            try
            {
                yield return Tap(Key.P);
                Assert.That(performed, Is.Zero, "P no longer draws the phone");

                yield return Tap(Key.Q);
                Assert.That(performed, Is.EqualTo(1), "Q draws the phone");

                yield return Tap(Key.Q);
                Assert.That(performed, Is.EqualTo(2), "Q again stows it");
            }
            finally
            {
                phone.performed -= Count;

                if (!wasEnabled)
                    phone.Disable();
            }
        }

        private IEnumerator StartInput()
        {
            _input = new VirtualInput(withMouse: false);
            _previousCaptureDeltaTime = Time.captureDeltaTime;
            Time.captureDeltaTime = FrameTime;

            yield return PlayModeWait.Frames(1);
        }

        private IEnumerator StartRig(Vector3 position)
        {
            yield return StartInput();

            Box("Test floor", new Vector3(0f, -0.5f, 0f), new Vector3(40f, 1f, 40f));
            CreatePlayer(position + Vector3.up * SpawnHeight, Quaternion.identity);

            // Let the CharacterController land and rest at its skin width before anything is measured.
            yield return PlayModeWait.Frames(30);
        }

        private IEnumerator StartDeliveryRig()
        {
            yield return StartInput();

            _world = SaveTestWorld.Create(2500);
            _world.StartPlayModeRuntime();
            CreatePlayer(new Vector3(0f, SpawnHeight, 0.6f), Quaternion.identity);

            yield return PlayModeWait.Frames(30);
        }

        // The real prefab without the systems these tests do not compose (phone, inventory, food, sleep).
        private void CreatePlayer(Vector3 position, Quaternion rotation)
        {
            GameObject holder = new("Inactive player holder");
            holder.SetActive(false);
            _created.Add(holder);

            GameObject player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab), holder.transform);
            Object.DestroyImmediate(player.GetComponent<PlayerFoodConsumption>());
            Object.DestroyImmediate(player.GetComponent<PlayerInventory>());
            Object.DestroyImmediate(player.GetComponent<PlayerSleepController>());
            Object.DestroyImmediate(player.GetComponentInChildren<PhoneBehaviour>(true).gameObject);

            player.transform.SetParent(null, false);
            player.transform.SetPositionAndRotation(position, rotation);
            _created.Add(player);

            _player = player.GetComponent<PlayerController>();
            _body = player.GetComponent<CharacterController>();
            _viewPivot = player.transform.Find("ViewPivot");
            _camera = player.GetComponentInChildren<Camera>();

            Assert.That(_player.isActiveAndEnabled, Is.True, "the Player prefab controller wires up (including its Crouch action)");
        }

        private DeliveryPackageBehaviour DeliverSnack()
        {
            ShopTestData.BuyOne(_world.Shop, SaveTestWorld.SnackId);
            _world.Clock.Clock.AdvanceMinutes(90);
            return Object.FindObjectsByType<DeliveryPackageBehaviour>(FindObjectsInactive.Include).Single();
        }

        private IEnumerator MeasureSpeed(bool crouched, Action<float> result)
        {
            if (crouched)
                yield return Hold(Key.LeftCtrl);

            yield return PlayModeWait.Frames(15);
            yield return Hold(crouched ? new[] { Key.LeftCtrl, Key.W } : new[] { Key.W });
            yield return PlayModeWait.Frames(5);

            Vector3 start = _player.transform.position;
            float startTime = Time.time;
            yield return PlayModeWait.Frames(30);

            Vector3 travelled = _player.transform.position - start;
            travelled.y = 0f;
            result(travelled.magnitude / (Time.time - startTime));

            if (!crouched)
                yield return Release();
        }

        // Steps the real player next to the collider and turns it so the camera ray hits that collider (and not the
        // one to avoid); the interactor then resolves prompts and F/E exactly as in the game.
        private IEnumerator AimAt(Collider target, Collider avoid = null)
        {
            Vector3 point = target.bounds.center;
            Vector3 away = _player.transform.position - point;
            away.y = 0f;

            if (away.sqrMagnitude < 0.01f)
                away = Vector3.back;

            Vector3 standAt = point + away.normalized * 1.3f;
            standAt.y = _player.transform.position.y;

            foreach (float height in new[] { 0f, -0.3f, 0.3f })
            {
                Vector3 aim = point + Vector3.up * (target.bounds.extents.y * height);

                for (int pass = 0; pass < 2; pass++)
                {
                    Vector3 toAim = aim - _camera.transform.position;
                    Vector3 flat = new(toAim.x, 0f, toAim.z);
                    float pitch = Mathf.Atan2(-toAim.y, flat.magnitude) * Mathf.Rad2Deg;

                    _player.RestorePose(new PlayerPoseSnapshot(standAt, Quaternion.LookRotation(flat.normalized, Vector3.up), pitch));
                    Physics.SyncTransforms();
                    yield return PlayModeWait.Frames(2);
                }

                Ray ray = new(_camera.transform.position, _camera.transform.forward);

                if (Physics.Raycast(ray, out RaycastHit hit, 2.5f, ~0, QueryTriggerInteraction.Ignore) && hit.collider == target && hit.collider != avoid)
                    yield break;
            }

            Assert.Fail($"could not aim at {target.name}");
        }

        private IEnumerator Press(Key key, string actionPath)
        {
            InputAction action = Action(actionPath);
            int performed = 0;
            void Count(InputAction.CallbackContext context) => performed++;
            action.performed += Count;

            yield return Tap(key);

            action.performed -= Count;
            Assert.That(performed, Is.EqualTo(1), $"{key} reached {actionPath} exactly once");
            yield return PlayModeWait.Frames(2);
        }

        private IEnumerator Tap(Key key)
        {
            InputSystem.QueueStateEvent(_input.Keyboard, new KeyboardState(key));
            yield return PlayModeWait.Frames(2);
            InputSystem.QueueStateEvent(_input.Keyboard, new KeyboardState());
            yield return PlayModeWait.Frames(2);
        }

        private IEnumerator Hold(params Key[] keys)
        {
            InputSystem.QueueStateEvent(_input.Keyboard, new KeyboardState(keys));
            yield return PlayModeWait.Frames(2);
        }

        private IEnumerator Release()
        {
            InputSystem.QueueStateEvent(_input.Keyboard, new KeyboardState());
            yield return PlayModeWait.Frames(2);
        }

        private float CapsuleBottom()
        {
            return _player.transform.position.y + _body.center.y - _body.height * 0.5f;
        }

        private GameObject Box(string name, Vector3 position, Vector3 size)
        {
            GameObject box = new(name);
            box.transform.position = position;
            box.AddComponent<BoxCollider>().size = size;
            _created.Add(box);
            return box;
        }

        private static InputAction Action(string path)
        {
            return AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputAsset).FindAction(path, true);
        }

        private static List<WorldItem> RuntimeItems(string definitionId)
        {
            return Object.FindObjectsByType<WorldItem>(FindObjectsInactive.Include)
                .Where(item => item.IsRuntime && item.Instance != null && item.Instance.DefinitionId == definitionId)
                .ToList();
        }

    }
}
