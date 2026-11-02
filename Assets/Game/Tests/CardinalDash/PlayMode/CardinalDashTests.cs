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
    public sealed partial class CardinalDashTests
    {
        private const string Root = "Assets/Game/Tests/Fixtures/CardinalDash";
        private readonly List<GameObject> objects = new List<GameObject>();
        private GameObject player;
        private PlayerMovement2D movement;
        private PlayerMovementInput2D input;
        private Rigidbody2D body;
        private Keyboard keyboard;
        private Gamepad gamepad;
        private InputSettings oldSettings;
        private InputSettings testSettings;
        private string oldSettingsJson;
        private HideFlags oldSettingsFlags;
        private Scene loadedScene;
        private int activeTicks;

        [SetUp]
        public void SetUp()
        {
            oldSettings = InputSystem.settings;
            oldSettingsJson = JsonUtility.ToJson(oldSettings);
            oldSettingsFlags = oldSettings.hideFlags;
            // Prevent Input System from deleting the original temporary Editor settings on replacement.
            if (oldSettingsFlags == HideFlags.HideAndDontSave)
                oldSettings.hideFlags |= HideFlags.HideInInspector;
            testSettings = Object.Instantiate(oldSettings);
            testSettings.hideFlags = HideFlags.HideAndDontSave;
            testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = testSettings;
            keyboard = InputSystem.AddDevice<Keyboard>();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/CardinalDashPlayer.prefab");
            Assert.That(prefab, Is.Not.Null, "Create the Pass 1B prototype assets first.");
            player = Object.Instantiate(prefab, new Vector3(0f, 4f, 0f), Quaternion.identity);
            objects.Add(player);
            movement = player.GetComponent<PlayerMovement2D>();
            input = player.GetComponent<PlayerMovementInput2D>();
            input.enabled = false; // Most checks drive the physics request API; binding checks opt in.
            body = player.GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.linearVelocity = Vector2.zero;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject item in objects)
                if (item != null)
                    Object.Destroy(item);
            objects.Clear();
            yield return RestorePolicyScenes();
            if (loadedScene.IsValid() && loadedScene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(loadedScene);
            if (keyboard != null && keyboard.added)
                InputSystem.RemoveDevice(keyboard);
            if (gamepad != null && gamepad.added)
                InputSystem.RemoveDevice(gamepad);
            if (oldSettings == null)
            {
                oldSettings = ScriptableObject.CreateInstance<InputSettings>();
                JsonUtility.FromJsonOverwrite(oldSettingsJson, oldSettings);
            }
            InputSystem.settings = oldSettings;
            oldSettings.hideFlags = oldSettingsFlags;
            Object.Destroy(testSettings);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest] public IEnumerator DashRight() => CheckCardinal(Vector2.right);
        [UnityTest] public IEnumerator DashLeft() => CheckCardinal(Vector2.left);
        [UnityTest] public IEnumerator DashUp() => CheckCardinal(Vector2.up);
        [UnityTest] public IEnumerator DashDownInAir() => CheckCardinal(Vector2.down);

        [UnityTest]
        public IEnumerator ExactDiagonalTieChoosesHorizontal()
        {
            Vector2 start = body.position;
            yield return CompleteDash(new Vector2(-1f, 1f));
            Vector2 delta = body.position - start;
            Assert.That(delta.x, Is.LessThan(-2f));
            Assert.That(delta.y, Is.EqualTo(0f).Within(0.02f));
        }

        [UnityTest]
        public IEnumerator DominantVerticalInputCannotCreateDiagonalMotion()
        {
            Vector2 start = body.position;
            yield return CompleteDash(new Vector2(0.3f, -0.9f));
            Vector2 delta = body.position - start;
            Assert.That(delta.y, Is.LessThan(-2f));
            Assert.That(delta.x, Is.EqualTo(0f).Within(0.02f));
        }

        [UnityTest]
        public IEnumerator NeutralInputInitiallyFacesRight()
        {
            Vector2 start = body.position;
            yield return CompleteDash(Vector2.zero);
            Assert.That(body.position.x - start.x, Is.GreaterThan(2f));
            Assert.That(body.position.y, Is.EqualTo(start.y).Within(0.02f));
        }

        [UnityTest]
        public IEnumerator NeutralAndDeadZoneInputUseLastHorizontalFacing()
        {
            movement.SetHorizontalInput(-1f);
            movement.SetHorizontalInput(0f);
            Vector2 start = body.position;
            yield return CompleteDash(new Vector2(0.1f, -0.1f));
            Assert.That(body.position.x - start.x, Is.LessThan(-2f));
            Assert.That(body.position.y, Is.EqualTo(start.y).Within(0.02f));
        }

        [UnityTest]
        public IEnumerator DistanceAndActiveTickCountAreRepeatable()
        {
            ConfigureChaining(true);
            float start = body.position.x;
            yield return CompleteDash(Vector2.right);
            float firstDistance = body.position.x - start;
            int firstTicks = activeTicks;
            start = body.position.x;
            yield return CompleteDash(Vector2.right);
            Assert.That(body.position.x - start, Is.EqualTo(firstDistance).Within(0.005f));
            Assert.That(activeTicks, Is.EqualTo(firstTicks));
            Assert.That(firstDistance, Is.EqualTo(movement.DashSpeed * movement.DashDuration).Within(0.02f));
            Assert.That(firstTicks * Time.fixedDeltaTime,
                Is.InRange(movement.DashDuration - 0.001f, movement.DashDuration + Time.fixedDeltaTime + 0.001f));
        }

        [UnityTest]
        public IEnumerator InspectorSpeedAndFractionalDurationControlTravel()
        {
            SerializedObject settings = new SerializedObject(movement);
            settings.FindProperty("dashSpeed").floatValue = 12f;
            settings.FindProperty("dashDuration").floatValue = 0.11f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            float start = body.position.x;
            yield return CompleteDash(Vector2.right);
            Assert.That(body.position.x - start, Is.EqualTo(1.32f).Within(0.02f));
        }

        [UnityTest]
        public IEnumerator GravityAndWalkingResumeWithoutOldVerticalMomentum()
        {
            body.gravityScale = 2.5f;
            body.linearVelocity = new Vector2(0f, -3f);
            movement.RequestDash(Vector2.up);
            yield return new WaitForFixedUpdate();
            Assert.That(movement.IsDashing, Is.True);
            Assert.That(body.gravityScale, Is.EqualTo(0f));
            movement.SetHorizontalInput(1f);
            yield return FinishCurrentDash();
            Assert.That(body.gravityScale, Is.EqualTo(2.5f));
            Assert.That(body.linearVelocity.x, Is.EqualTo(movement.MoveSpeed).Within(0.02f));
            yield return new WaitForSeconds(0.1f);
            Assert.That(body.linearVelocity.y, Is.LessThan(0f));
        }

        [UnityTest]
        public IEnumerator RepeatedRequestsCannotRedirectOrRestartActiveDash()
        {
            int approvals = 0;
            movement.DashApproval = direction => { approvals++; return true; };
            movement.RequestDash(Vector2.right);
            yield return new WaitForFixedUpdate();
            for (int tick = 0; tick < 4; tick++)
            {
                movement.RequestDash(Vector2.up);
                movement.RequestJump();
                yield return new WaitForFixedUpdate();
                Assert.That(movement.DashDirection, Is.EqualTo(Vector2.right));
            }
            yield return FinishCurrentDash();
            Assert.That(approvals, Is.EqualTo(1));
            Assert.That(body.position.y, Is.EqualTo(4f).Within(0.02f));
        }

        [UnityTest]
        public IEnumerator GroundedHorizontalDashDoesNotMistakeFloorForWall()
        {
            Solid("Floor", new Vector2(0f, -0.5f), new Vector2(30f, 1f));
            body.position = new Vector2(0f, 0.8f);
            yield return WaitForGround();
            float start = body.position.x;
            yield return CompleteDash(Vector2.right);
            Assert.That(body.position.x - start, Is.EqualTo(movement.DashSpeed * movement.DashDuration).Within(0.04f));
            Assert.That(movement.IsGrounded, Is.True);
        }

        [UnityTest]
        public IEnumerator ThinWallStopsHighSpeedDashWithoutPenetration()
        {
            Solid("Thin Wall", new Vector2(2f, 4f), new Vector2(0.05f, 8f));
            SerializedObject settings = new SerializedObject(movement);
            settings.FindProperty("dashSpeed").floatValue = 100f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            yield return CompleteDash(Vector2.right);
            Assert.That(body.position.x, Is.InRange(1.5f, 1.585f));
            Assert.That(activeTicks, Is.LessThan(5));
        }

        [UnityTest]
        public IEnumerator CeilingStopsUpwardDashEarly()
        {
            Solid("Ceiling", new Vector2(0f, 6f), new Vector2(8f, 0.2f));
            yield return CompleteDash(Vector2.up);
            Assert.That(body.position.y, Is.InRange(4.8f, 5.11f));
            Assert.That(activeTicks, Is.LessThan(8));
        }

        [UnityTest]
        public IEnumerator FloorStopsDownwardDashAndAllowsGroundedJump()
        {
            Solid("Floor", new Vector2(0f, 1.5f), new Vector2(10f, 1f));
            yield return CompleteDash(Vector2.down);
            Assert.That(body.position.y, Is.InRange(2.79f, 2.83f));
            Assert.That(movement.IsGrounded, Is.True);
            movement.RequestJump();
            yield return new WaitForFixedUpdate();
            Assert.That(body.linearVelocity.y, Is.GreaterThan(0f));
        }

        [UnityTest]
        public IEnumerator TriggersDoNotStopDash()
        {
            Solid("Trigger", new Vector2(1f, 4f), new Vector2(0.1f, 6f)).isTrigger = true;
            yield return CompleteDash(Vector2.right);
            Assert.That(body.position.x, Is.GreaterThan(2.7f));
        }

        [UnityTest]
        public IEnumerator ApprovalHookCanRejectThenApproveWithoutResourceSystem()
        {
            int calls = 0;
            Vector2 approvedDirection = Vector2.zero;
            movement.DashApproval = direction => { calls++; approvedDirection = direction; return false; };
            movement.RequestDash(new Vector2(1f, 1f));
            yield return new WaitForFixedUpdate();
            Assert.That(movement.IsDashing, Is.False);
            Assert.That(body.position.x, Is.EqualTo(0f).Within(0.01f));
            Assert.That(body.gravityScale, Is.EqualTo(0f));
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(approvedDirection, Is.EqualTo(Vector2.right));
            movement.DashApproval = direction => { calls++; return true; };
            yield return CompleteDash(Vector2.left);
            Assert.That(calls, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator Pass1APrefabRemainsDashDisabled()
        {
            GameObject baseline = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Tests/Fixtures/Movement/Prefabs/PlaceholderPlayer.prefab");
            GameObject instance = Object.Instantiate(baseline, new Vector3(10f, 4f), Quaternion.identity);
            objects.Add(instance);
            PlayerMovement2D baselineMovement = instance.GetComponent<PlayerMovement2D>();
            Assert.That(baselineMovement.DashEnabled, Is.False);
            baselineMovement.RequestDash(Vector2.right);
            yield return new WaitForFixedUpdate();
            Assert.That(baselineMovement.IsDashing, Is.False);
        }

        [UnityTest]
        public IEnumerator KeyboardSpaceUsesMoveDirectionAndDoesNotModifySharedActions()
        {
            input.enabled = true;
            InputActionAsset shared = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Settings/InputSystem_Actions.inputactions");
            Assert.That(shared.FindAction("Player/Dash"), Is.Null);
            Keys(Key.W, Key.Space);
            yield return new WaitForSeconds(0.06f);
            Assert.That(movement.IsDashing, Is.True);
            Assert.That(movement.DashDirection, Is.EqualTo(Vector2.up));
            Assert.That(shared.FindAction("Player/Dash"), Is.Null);
        }

        [UnityTest]
        public IEnumerator GamepadRightShoulderUsesCardinalStickDirection()
        {
            input.enabled = true;
            gamepad = InputSystem.AddDevice<Gamepad>();
            InputSystem.QueueStateEvent(gamepad,
                new GamepadState { leftStick = new Vector2(-0.3f, 0.9f) }.WithButton(GamepadButton.RightShoulder));
            // Device creation can stall a frame longer than a short wall-clock wait.
            // Let input Update submit its request, then observe the following physics tick.
            yield return null;
            yield return new WaitForFixedUpdate();
            Assert.That(movement.IsDashing, Is.True);
            Assert.That(movement.DashDirection, Is.EqualTo(Vector2.up));
        }

        [UnityTest]
        public IEnumerator ClearInputDiscardsPendingDash()
        {
            movement.RequestDash(Vector2.right);
            movement.ClearInput();
            yield return new WaitForFixedUpdate();
            Assert.That(movement.IsDashing, Is.False);
        }

        [UnityTest]
        public IEnumerator ActorDisableRestoresGravityAndClearsApprovalAndInput()
        {
            for (int cycle = 0; cycle < 3; cycle++)
            {
                body.gravityScale = 2f;
                movement.DashApproval = direction => true;
                movement.RequestDash(Vector2.left);
                yield return new WaitForFixedUpdate();
                Assert.That(movement.IsDashing, Is.True);
                player.SetActive(false);
                Assert.That(movement.IsDashing, Is.False);
                Assert.That(body.gravityScale, Is.EqualTo(2f));
                Assert.That(movement.DashApproval, Is.Null);
                player.SetActive(true);
                input.enabled = false;
                body.gravityScale = 0f;
                body.linearVelocity = Vector2.zero;
                yield return new WaitForFixedUpdate();
                Assert.That(movement.IsDashing, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator InputDisableReenableDoesNotLeaveDashSubscriptions()
        {
            ConfigureChaining(true);
            input.enabled = true;
            for (int cycle = 0; cycle < 3; cycle++)
            {
                input.enabled = false;
                Keys(Key.Space);
                yield return new WaitForSeconds(0.04f);
                Assert.That(movement.IsDashing, Is.False);
                Keys();
                yield return null;
                input.enabled = true;
                Keys(Key.Space);
                yield return new WaitForSeconds(0.04f);
                Assert.That(movement.IsDashing, Is.True);
                Keys();
                yield return FinishCurrentDash();
            }
        }

        [UnityTest]
        public IEnumerator IsolatedDashSceneLoadsAndAcceptsDashInput()
        {
            Object.Destroy(player);
            yield return null;
            loadedScene = EditorSceneManager.LoadSceneInPlayMode(Root + "/Scenes/CardinalDashTest.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            foreach (GameObject root in loadedScene.GetRootGameObjects())
                if (root.GetComponent<PlayerMovement2D>() != null)
                {
                    movement = root.GetComponent<PlayerMovement2D>();
                    body = root.GetComponent<Rigidbody2D>();
                }
            Assert.That(movement.DashEnabled, Is.True);
            yield return WaitForGround();
            Keys(Key.D, Key.Space);
            yield return new WaitForSeconds(0.06f);
            Assert.That(movement.IsDashing, Is.True);
            Assert.That(movement.DashDirection, Is.EqualTo(Vector2.right));
        }

        private IEnumerator CheckCardinal(Vector2 direction)
        {
            Vector2 start = body.position;
            yield return CompleteDash(direction);
            Vector2 delta = body.position - start;
            Assert.That(Vector2.Dot(delta, direction), Is.EqualTo(movement.DashSpeed * movement.DashDuration).Within(0.02f));
            Assert.That(Vector2.Dot(delta, new Vector2(-direction.y, direction.x)), Is.EqualTo(0f).Within(0.02f));
        }

        private IEnumerator CompleteDash(Vector2 direction)
        {
            movement.RequestDash(direction);
            yield return new WaitForFixedUpdate();
            Assert.That(movement.IsDashing, Is.True);
            activeTicks = 1;
            for (int tick = 0; tick < 120; tick++)
            {
                yield return new WaitForFixedUpdate();
                if (!movement.IsDashing)
                    yield break;
                activeTicks++;
            }
            Assert.Fail("Dash did not finish within 120 physics ticks.");
        }

        private IEnumerator FinishCurrentDash()
        {
            for (int tick = 0; tick < 120; tick++)
            {
                yield return new WaitForFixedUpdate();
                if (!movement.IsDashing)
                    yield break;
            }
            Assert.Fail("Active dash did not finish.");
        }

        private IEnumerator WaitForGround()
        {
            for (int tick = 0; tick < 120; tick++)
            {
                yield return new WaitForFixedUpdate();
                if (movement.IsGrounded)
                    yield break;
            }
            Assert.Fail("Player did not become grounded.");
        }

        private void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));

        private BoxCollider2D Solid(string name, Vector2 position, Vector2 size)
        {
            GameObject solid = new GameObject(name, typeof(BoxCollider2D));
            solid.transform.position = position;
            BoxCollider2D collider = solid.GetComponent<BoxCollider2D>();
            collider.size = size;
            objects.Add(solid);
            return collider;
        }
    }
}
#endif
