using System.IO;
using System.Linq;
using GoLive.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GoLive.Tests
{
    // The locked Skeleton control scheme lives in the project's Input Actions asset; gameplay reads it only through
    // InputActionReferences, so these bindings are the single source of the keys the player uses.
    public sealed class InputBindingsTests
    {
        private const string InputAssetPath = "Assets/Game/_Project/GO_LIVE_Input.inputactions";
        private const string PlayerPrefab = "Assets/Game/Prefab/Player/Player.prefab";
        private const string GameScene = "Assets/Game/Scenes/GL.unity";

        private static InputActionAsset Asset => AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputAssetPath);

        [TestCase("Take", "<Keyboard>/e")]
        [TestCase("Use", "<Keyboard>/f")]
        [TestCase("Drop", "<Keyboard>/g")]
        [TestCase("Special", "<Keyboard>/b")]
        [TestCase("Phone", "<Keyboard>/q")]
        [TestCase("Crouch", "<Keyboard>/leftCtrl")]
        [TestCase("Inventory", "<Keyboard>/tab")]
        [TestCase("Pause", "<Keyboard>/escape")]
        public void CoreControlUsesItsSkeletonKey(string action, string path)
        {
            Assert.That(Asset.FindAction($"Player/{action}", true).bindings.Select(binding => binding.path), Is.EqualTo(new[] { path }));
        }

        [Test]
        public void NothingListensToTheOldPhoneKey()
        {
            Assert.That(Asset.actionMaps.SelectMany(map => map.bindings).Any(binding => binding.path == "<Keyboard>/p"), Is.False);
        }

        [Test]
        public void NoKeyTriggersTwoPlayerActions()
        {
            var shared = Asset.FindActionMap("Player", true).bindings
                .Where(binding => !binding.isComposite && binding.path.StartsWith("<Keyboard>/"))
                .GroupBy(binding => binding.path)
                .Where(group => group.Select(binding => binding.action).Distinct().Count() > 1)
                .Select(group => $"{group.Key}: {string.Join(", ", group.Select(binding => binding.action))}")
                .ToArray();

            Assert.That(shared, Is.Empty);
        }

        [Test]
        public void CrouchIsAHeldButtonThatSeesAKeyAlreadyDown()
        {
            InputAction crouch = Asset.FindAction("Player/Crouch", true);

            Assert.That(crouch.type, Is.EqualTo(InputActionType.Button));
            Assert.That(crouch.wantsInitialStateCheck, Is.True);
            Assert.That(crouch.interactions, Is.Empty, "hold to crouch: the controller reads the pressed state");
        }

        [Test]
        public void PlayerPrefabAndGameSceneWireTheCrouchAction()
        {
            PlayerController controller = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab).GetComponent<PlayerController>();
            Object reference = new SerializedObject(controller).FindProperty("crouchAction").objectReferenceValue;

            Assert.That(reference, Is.InstanceOf<InputActionReference>());
            Assert.That(((InputActionReference)reference).action, Is.SameAs(Asset.FindAction("Player/Crouch", true)));

            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(reference, out string guid, out long fileId), Is.True);
            string scene = File.ReadAllText(GameScene);
            Assert.That(scene.Split('\n').Count(line => line.Trim() == $"crouchAction: {{fileID: {fileId}, guid: {guid}, type: 3}}"), Is.EqualTo(1),
                "the GL player controller references the same Crouch action");
        }
    }
}
