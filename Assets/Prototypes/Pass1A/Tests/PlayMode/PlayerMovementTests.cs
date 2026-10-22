#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using GameJam.Movement;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GameJam.Movement.Tests
{
    public sealed class PlayerMovementTests
    {
        private const string Root = "Assets/Prototypes/Pass1A";
        private readonly List<GameObject> objects = new List<GameObject>();
        private Keyboard keyboard;
        private Gamepad gamepad;
        private GameObject player;
        private PlayerMovement2D movement;
        private Rigidbody2D body;
        private Scene prototypeScene;
        private InputSettings previousInputSettings;
        private InputSettings testInputSettings;
        private string previousInputSettingsJson;

        [SetUp]
        public void SetUp()
        {
            // Synthetic input should work even while the developer views the Test Runner.
            // Use a temporary settings copy, never change the project's Input Settings asset.
            previousInputSettings = InputSystem.settings;
            previousInputSettingsJson = JsonUtility.ToJson(previousInputSettings);
            testInputSettings = Object.Instantiate(previousInputSettings);
            testInputSettings.hideFlags = HideFlags.HideAndDontSave;
            testInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            testInputSettings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = testInputSettings;
            keyboard = InputSystem.AddDevice<Keyboard>();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/PlaceholderPlayer.prefab");
            Assert.That(prefab, Is.Not.Null, "Generate the prototype assets before running tests.");
            player = Object.Instantiate(prefab, new Vector3(0f, 1f, 0f), Quaternion.identity);
            objects.Add(player);
            movement = player.GetComponent<PlayerMovement2D>();
            body = player.GetComponent<Rigidbody2D>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject item in objects)
                if (item != null)
                    Object.Destroy(item);
            objects.Clear();
            if (prototypeScene.IsValid() && prototypeScene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(prototypeScene);
            if (keyboard != null && keyboard.added)
                InputSystem.RemoveDevice(keyboard);
            if (gamepad != null && gamepad.added)
                InputSystem.RemoveDevice(gamepad);
            // Unity can unload its unused default settings while the scene test loads.
            // Restore the same values even if that transient original no longer exists.
            if (previousInputSettings == null)
            {
                previousInputSettings = ScriptableObject.CreateInstance<InputSettings>();
                JsonUtility.FromJsonOverwrite(previousInputSettingsJson, previousInputSettings);
            }
            InputSystem.settings = previousInputSettings;
            Object.Destroy(testInputSettings);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator KeyboardMovesStopsAndReverses()
        {
            Floor();
            yield return WaitForGround();
            float initialX = body.position.x;
            Keys(Key.D);
            yield return new WaitForSeconds(0.2f);
            Assert.That(body.position.x, Is.GreaterThan(initialX + 0.5f));
            Assert.That(body.linearVelocity.x, Is.EqualTo(movement.MoveSpeed).Within(0.05f));
            Keys(Key.A);
            yield return new WaitForSeconds(0.1f);
            Assert.That(body.linearVelocity.x, Is.EqualTo(-movement.MoveSpeed).Within(0.05f));
            Keys();
            yield return new WaitForSeconds(0.1f);
            Assert.That(body.linearVelocity.x, Is.EqualTo(0f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator ArrowKeysAndGamepadUseExistingMoveBindings()
        {
            Floor();
            yield return WaitForGround();
            Keys(Key.RightArrow);
            yield return new WaitForSeconds(0.1f);
            Assert.That(body.linearVelocity.x, Is.GreaterThan(0f));
            Keys();
            gamepad = InputSystem.AddDevice<Gamepad>();
            InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = Vector2.left });
            yield return new WaitForSeconds(0.1f);
            Assert.That(body.linearVelocity.x, Is.LessThan(0f));
        }

        [UnityTest]
        public IEnumerator JumpPressIsConsumedOnceAndLandingAllowsAnotherJump()
        {
            Floor();
            yield return WaitForGround();
            Keys(Key.Space);
            yield return new WaitForSeconds(0.1f);
            Assert.That(body.linearVelocity.y, Is.GreaterThan(1f));
            Assert.That(movement.IsGrounded, Is.False);
            Keys();
            yield return new WaitForSeconds(0.1f);
            float velocityBefore = body.linearVelocity.y;
            Keys(Key.Space);
            yield return new WaitForSeconds(0.1f);
            Assert.That(body.linearVelocity.y, Is.LessThan(velocityBefore), "Airborne press must not restart jump.");
            Keys();
            yield return WaitForGround();
            Keys(Key.Space);
            yield return new WaitForSeconds(0.1f);
            Assert.That(body.linearVelocity.y, Is.GreaterThan(1f));
        }

        [UnityTest]
        public IEnumerator HeldJumpDoesNotAutomaticallyJumpAgainOnLanding()
        {
            Floor();
            yield return WaitForGround();
            Keys(Key.Space);
            yield return new WaitForSeconds(1.2f);
            Assert.That(movement.IsGrounded, Is.True);
            Assert.That(body.position.y, Is.EqualTo(0.8f).Within(0.08f));
            yield return new WaitForSeconds(0.2f);
            Assert.That(movement.IsGrounded, Is.True);
        }

        [UnityTest]
        public IEnumerator GamepadSouthButtonJumps()
        {
            Floor();
            yield return WaitForGround();
            gamepad = InputSystem.AddDevice<Gamepad>();
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South));
            yield return new WaitForSeconds(0.1f);
            Assert.That(body.linearVelocity.y, Is.GreaterThan(1f));
        }

        [UnityTest]
        public IEnumerator FloorStopsFalling()
        {
            Floor();
            yield return WaitForGround();
            yield return new WaitForSeconds(0.2f);
            Assert.That(body.position.y, Is.EqualTo(0.8f).Within(0.05f));
            Assert.That(body.linearVelocity.y, Is.EqualTo(0f).Within(0.05f));
        }

        [UnityTest]
        public IEnumerator WallBlocksMovementAndPlayerCanReverseAway()
        {
            Floor();
            Solid("Wall", new Vector2(2f, 2f), new Vector2(0.4f, 4f));
            yield return WaitForGround();
            Keys(Key.D);
            yield return new WaitForSeconds(0.6f);
            Assert.That(body.position.x, Is.LessThanOrEqualTo(1.43f));
            Keys(Key.A);
            yield return new WaitForSeconds(0.2f);
            Assert.That(body.position.x, Is.LessThan(1f));
        }

        [UnityTest]
        public IEnumerator WallContactAloneDoesNotAllowJump()
        {
            Solid("Wall", new Vector2(0.6f, 3f), new Vector2(0.4f, 8f));
            body.position = new Vector2(0f, 2f);
            body.gravityScale = 0f;
            body.linearVelocity = Vector2.zero;
            Keys(Key.D);
            yield return new WaitForSeconds(0.2f);
            Assert.That(movement.IsGrounded, Is.False);
            Keys(Key.D, Key.Space);
            yield return new WaitForSeconds(0.1f);
            Assert.That(body.linearVelocity.y, Is.EqualTo(0f).Within(0.01f));
            Assert.That(movement.IsGrounded, Is.False);
        }

        [UnityTest]
        public IEnumerator CeilingStopsUpwardMovementAndDoesNotBecomeGround()
        {
            Floor();
            Solid("Ceiling", new Vector2(0f, 2.5f), new Vector2(5f, 0.3f));
            yield return WaitForGround();
            Keys(Key.Space);
            yield return new WaitForSeconds(0.18f);
            Assert.That(body.position.y, Is.LessThanOrEqualTo(1.58f));
            Assert.That(body.linearVelocity.y, Is.LessThanOrEqualTo(0.1f));
            Assert.That(movement.IsGrounded, Is.False);
            Keys();
            yield return WaitForGround();
        }

        [UnityTest]
        public IEnumerator WalkingOffAnEdgeRemovesJumpEligibility()
        {
            Solid("Small Platform", new Vector2(0f, -0.5f), new Vector2(2f, 1f));
            yield return WaitForGround();
            Keys(Key.D);
            yield return new WaitForSeconds(0.35f);
            Assert.That(movement.IsGrounded, Is.False);
            float fallingSpeed = body.linearVelocity.y;
            Keys(Key.Space);
            yield return new WaitForSeconds(0.1f);
            Assert.That(body.linearVelocity.y, Is.LessThan(fallingSpeed));
        }

        [UnityTest]
        public IEnumerator TriggerBelowPlayerDoesNotCountAsGround()
        {
            Solid("Trigger", new Vector2(0f, -0.5f), new Vector2(10f, 1f)).isTrigger = true;
            body.position = new Vector2(0f, 0.81f);
            body.gravityScale = 0f;
            body.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.1f);
            Assert.That(movement.IsGrounded, Is.False);
            Keys(Key.Space);
            yield return new WaitForSeconds(0.1f);
            Assert.That(body.linearVelocity.y, Is.EqualTo(0f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator GroundLayerMaskIsRespected()
        {
            Floor();
            SerializedObject settings = new SerializedObject(movement);
            settings.FindProperty("groundLayers").intValue = 0;
            settings.ApplyModifiedPropertiesWithoutUndo();
            yield return new WaitForSeconds(0.4f);
            Assert.That(movement.IsGrounded, Is.False);
            Keys(Key.Space);
            yield return new WaitForSeconds(0.1f);
            Assert.That(body.linearVelocity.y, Is.LessThan(0.1f));
        }

        [UnityTest]
        public IEnumerator InspectorMovementTuningAffectsPhysics()
        {
            Floor();
            SerializedObject settings = new SerializedObject(movement);
            settings.FindProperty("moveSpeed").floatValue = 3f;
            settings.FindProperty("jumpSpeed").floatValue = 7f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            yield return WaitForGround();
            Keys(Key.D);
            yield return new WaitForSeconds(0.1f);
            Assert.That(body.linearVelocity.x, Is.EqualTo(3f).Within(0.05f));
            Keys(Key.Space);
            yield return new WaitForSeconds(0.06f);
            Assert.That(body.linearVelocity.y, Is.InRange(3f, 7.1f));
        }

        [UnityTest]
        public IEnumerator InputCanBeDisabledAndReenabledWithoutStaleJumpOrMovement()
        {
            Floor();
            yield return WaitForGround();
            PlayerMovementInput2D input = player.GetComponent<PlayerMovementInput2D>();
            for (int cycle = 0; cycle < 3; cycle++)
            {
                Keys(Key.D);
                yield return new WaitForSeconds(0.08f);
                Assert.That(body.linearVelocity.x, Is.GreaterThan(0f));
                input.enabled = false;
                Keys(Key.Space);
                yield return new WaitForSeconds(0.08f);
                Assert.That(body.linearVelocity.x, Is.EqualTo(0f).Within(0.01f));
                Assert.That(movement.IsGrounded, Is.True);
                Keys();
                yield return null;
                input.enabled = true;
                yield return new WaitForSeconds(0.08f);
                Assert.That(body.linearVelocity.x, Is.EqualTo(0f).Within(0.01f));
                Assert.That(movement.IsGrounded, Is.True);
            }
        }

        [UnityTest]
        public IEnumerator PrototypeSceneLoadsWithWorkingPrefabAndObstacles()
        {
            Object.Destroy(player);
            yield return null;
            prototypeScene = EditorSceneManager.LoadSceneInPlayMode(Root + "/Scenes/MovementTest.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            GameObject scenePlayer = null;
            int solidCount = 0;
            foreach (GameObject root in prototypeScene.GetRootGameObjects())
            {
                if (root.GetComponent<PlayerMovement2D>() != null)
                    scenePlayer = root;
                if (root.GetComponent<BoxCollider2D>() != null)
                    solidCount++;
            }
            Assert.That(scenePlayer, Is.Not.Null);
            Assert.That(solidCount, Is.GreaterThanOrEqualTo(7));
            movement = scenePlayer.GetComponent<PlayerMovement2D>();
            body = scenePlayer.GetComponent<Rigidbody2D>();
            yield return WaitForGround();
            float start = body.position.x;
            Keys(Key.D);
            yield return new WaitForSeconds(0.2f);
            Assert.That(body.position.x, Is.GreaterThan(start + 0.5f));
            Keys(Key.Space);
            yield return new WaitForSeconds(0.08f);
            Assert.That(body.linearVelocity.y, Is.GreaterThan(0f));
        }

        private void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));

        private void Floor() => Solid("Floor", new Vector2(0f, -0.5f), new Vector2(30f, 1f));

        private BoxCollider2D Solid(string name, Vector2 position, Vector2 size)
        {
            GameObject solid = new GameObject(name, typeof(BoxCollider2D));
            solid.transform.position = position;
            BoxCollider2D collider = solid.GetComponent<BoxCollider2D>();
            collider.size = size;
            objects.Add(solid);
            return collider;
        }

        private IEnumerator WaitForGround()
        {
            for (int frame = 0; frame < 120; frame++)
            {
                yield return new WaitForFixedUpdate();
                if (movement.IsGrounded)
                    yield break;
            }
            Assert.Fail("Player did not become grounded within 120 physics ticks.");
        }
    }
}
#endif
