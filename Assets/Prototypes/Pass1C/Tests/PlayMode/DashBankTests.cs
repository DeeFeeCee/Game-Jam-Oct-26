#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using GameJam.Movement;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GameJam.Resources.Tests
{
    public sealed class DashBankTests
    {
        private const string Root = "Assets/Prototypes/Pass1C";
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly List<Scene> scenes = new List<Scene>();
        private RunDashBank bank;
        private PlayerMovement2D movement;
        private PlayerDashBankLink link;
        private Rigidbody2D body;
        private InputSettings oldSettings;
        private InputSettings testSettings;
        private string oldSettingsJson;
        private HideFlags oldSettingsFlags;

        [SetUp]
        public void SetUp()
        {
            Assert.That(RunDashBank.Instance, Is.Null, "A previous resource test leaked its bank.");
            oldSettings = InputSystem.settings;
            oldSettingsJson = JsonUtility.ToJson(oldSettings);
            oldSettingsFlags = oldSettings.hideFlags;
            if (oldSettingsFlags == HideFlags.HideAndDontSave)
                oldSettings.hideFlags |= HideFlags.HideInInspector;
            testSettings = Object.Instantiate(oldSettings);
            testSettings.hideFlags = HideFlags.HideAndDontSave;
            testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings = testSettings;
            bank = CreateBank(4);
            movement = CreatePlayer();
            body = movement.GetComponent<Rigidbody2D>();
            link = movement.GetComponent<PlayerDashBankLink>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject item in objects)
                if (item != null) Object.Destroy(item);
            objects.Clear();
            if (scenes.Count > 0)
            {
                // Single-scene transition tests can replace the Test Runner's starting scene.
                Scene clean = SceneManager.CreateScene("Resource test cleanup " + Guid.NewGuid().ToString("N"));
                SceneManager.SetActiveScene(clean);
                foreach (Scene scene in scenes)
                    if (scene.IsValid() && scene.isLoaded)
                        yield return SceneManager.UnloadSceneAsync(scene);
                scenes.Clear();
            }
            if (oldSettings == null)
            {
                oldSettings = ScriptableObject.CreateInstance<InputSettings>();
                JsonUtility.FromJsonOverwrite(oldSettingsJson, oldSettings);
            }
            InputSystem.settings = oldSettings;
            oldSettings.hideFlags = oldSettingsFlags;
            Object.Destroy(testSettings);
            yield return null;
            Assert.That(RunDashBank.Instance, Is.Null);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator InspectorCountInitializesOneAuthoritativeBalance()
        {
            Assert.That(bank.StartingDashCount, Is.EqualTo(4));
            Assert.That(bank.RemainingCharges, Is.EqualTo(4));
            Assert.That(bank.IsGameOver, Is.False);
            Assert.That(RunDashBank.Instance, Is.SameAs(bank));
            yield return null;
        }

        [UnityTest]
        public IEnumerator AcceptedDashSpendsExactlyOneCharge()
        {
            yield return Dash(Vector2.right);
            Assert.That(bank.RemainingCharges, Is.EqualTo(3));
            Assert.That(body.position.x, Is.EqualTo(2.88f).Within(0.02f));
        }

        [UnityTest]
        public IEnumerator ActiveRequestsNeitherRestartNorSpendAgain()
        {
            ConfigureChaining(true);
            movement.RequestDash(Vector2.right);
            yield return new WaitForFixedUpdate();
            for (int i = 0; i < 3; i++)
            {
                movement.RequestDash(Vector2.up);
                yield return new WaitForFixedUpdate();
                Assert.That(movement.DashDirection, Is.EqualTo(Vector2.right));
            }
            yield return Finish();
            Assert.That(bank.RemainingCharges, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator IneligibleAirborneRequestDoesNotSpend()
        {
            ConfigureChaining(false);
            yield return Dash(Vector2.right);
            movement.RequestDash(Vector2.up);
            yield return new WaitForFixedUpdate();
            Assert.That(movement.IsDashing, Is.False);
            Assert.That(bank.RemainingCharges, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator FullyBlockedRequestSpendsNothingAndKeepsMovementEligibility()
        {
            Solid("Flush wall", new Vector2(0.5f, 4f), new Vector2(0.2f, 8f));
            Physics2D.SyncTransforms();
            movement.RequestDash(Vector2.right);
            yield return new WaitForFixedUpdate();
            Assert.That(movement.IsDashing, Is.False);
            Assert.That(bank.RemainingCharges, Is.EqualTo(4));
            yield return Dash(Vector2.left);
            Assert.That(bank.RemainingCharges, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator PartiallyObstructedDashSpendsOnceAndStopsSafely()
        {
            Solid("Near wall", new Vector2(1f, 4f), new Vector2(0.2f, 8f));
            yield return Dash(Vector2.right);
            Assert.That(bank.RemainingCharges, Is.EqualTo(3));
            Assert.That(body.position.x, Is.InRange(0.4f, 0.51f));
            Assert.That(body.gravityScale, Is.EqualTo(0f));
        }

        [UnityTest]
        public IEnumerator FinalChargeCausesImmediateFailureWithoutFatalDashTravel()
        {
            SetStartingCount(1);
            bank.InitializeNewRun();
            Vector2 start = body.position;
            movement.SetHorizontalInput(1f);
            movement.RequestJump();
            movement.RequestDash(Vector2.right);
            yield return new WaitForFixedUpdate();
            Assert.That(bank.RemainingCharges, Is.Zero);
            Assert.That(bank.IsGameOver, Is.True);
            Assert.That(movement.IsDashing, Is.False);
            Assert.That(movement.enabled, Is.False);
            Assert.That(movement.GetComponent<PlayerMovementInput2D>().enabled, Is.False);
            Assert.That(body.simulated, Is.False);
            Assert.That(body.linearVelocity, Is.EqualTo(Vector2.zero));
            Assert.That(body.position, Is.EqualTo(start));
            yield return new WaitForSeconds(0.1f);
            Assert.That(body.position, Is.EqualTo(start));
        }

        [UnityTest]
        public IEnumerator NoFurtherSpendingOrMovementAfterGameOver()
        {
            SetStartingCount(1);
            bank.InitializeNewRun();
            Assert.That(bank.TrySpendDash(), Is.False);
            Vector2 failedAt = body.position;
            for (int i = 0; i < 3; i++)
            {
                Assert.That(bank.TrySpendDash(), Is.False);
                movement.SetHorizontalInput(-1f);
                movement.RequestDash(Vector2.left);
                movement.RequestJump();
                yield return new WaitForFixedUpdate();
            }
            Assert.That(bank.RemainingCharges, Is.Zero);
            Assert.That(body.position, Is.EqualTo(failedAt));
        }

        [UnityTest]
        public IEnumerator ChargeAndFailureEventsObserveCommittedStateExactlyOnce()
        {
            SetStartingCount(2);
            bank.InitializeNewRun();
            List<int> changes = new List<int>();
            int failures = 0;
            bank.ChargesChanged += remaining => {
                changes.Add(remaining);
                Assert.That(bank.RemainingCharges, Is.EqualTo(remaining));
                if (remaining == 0) Assert.That(bank.IsGameOver, Is.True);
            };
            bank.GameOver += () => failures++;
            Assert.That(bank.TrySpendDash(), Is.True);
            Assert.That(bank.TrySpendDash(), Is.False);
            Assert.That(bank.TrySpendDash(), Is.False);
            Assert.That(changes, Is.EqualTo(new[] { 1, 0 }));
            Assert.That(failures, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator LandingRearmsMovementButNeverRechargesTheBank()
        {
            yield return Dash(Vector2.right);
            Solid("Floor", new Vector2(0f, -0.5f), new Vector2(40f, 1f));
            body.position = new Vector2(0f, 0.8f);
            body.linearVelocity = Vector2.zero;
            yield return Ground();
            Assert.That(bank.RemainingCharges, Is.EqualTo(3));
            movement.RequestJump();
            yield return new WaitForFixedUpdate();
            Assert.That(bank.RemainingCharges, Is.EqualTo(3));
            yield return Dash(Vector2.up);
            Assert.That(bank.RemainingCharges, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator EveryChainedDashSpendsIndependentlyWithoutRecharge()
        {
            ConfigureChaining(true);
            yield return Dash(Vector2.right);
            Assert.That(bank.RemainingCharges, Is.EqualTo(3));
            yield return Dash(Vector2.up);
            Assert.That(bank.RemainingCharges, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator ExplicitNewRunResetsFailureBalanceAndPlayerControls()
        {
            SetStartingCount(2);
            bank.InitializeNewRun();
            bank.TrySpendDash();
            bank.TrySpendDash();
            Assert.That(body.simulated, Is.False);
            int resets = 0;
            bank.NewRunStarted += () => resets++;
            bank.InitializeNewRun();
            Assert.That(bank.RemainingCharges, Is.EqualTo(2));
            Assert.That(bank.IsGameOver, Is.False);
            Assert.That(body.simulated, Is.True);
            Assert.That(movement.enabled, Is.True);
            Assert.That(resets, Is.EqualTo(1));
            yield return Dash(Vector2.right);
            Assert.That(bank.RemainingCharges, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DuplicateManagerCannotReplaceOrReinitializeTheBalance()
        {
            yield return Dash(Vector2.right);
            RunDashBank duplicate = CreateBank(99);
            yield return null;
            Assert.That(duplicate == null, Is.True);
            Assert.That(RunDashBank.Instance, Is.SameAs(bank));
            Assert.That(bank.RemainingCharges, Is.EqualTo(3));
            Assert.That(Object.FindObjectsByType<RunDashBank>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator LinkSubscriptionsAreRemovedAndDoNotAccumulate()
        {
            for (int cycle = 0; cycle < 3; cycle++)
            {
                AssertSubscribers(1);
                link.enabled = false;
                AssertSubscribers(0);
                link.enabled = true;
                AssertSubscribers(1);
                yield return null;
            }
            Object.Destroy(movement.gameObject);
            yield return null;
            AssertSubscribers(0);
            bank.InitializeNewRun();
        }

        [UnityTest]
        public IEnumerator NewlySpawnedPlayerIsFrozenIfTheRunAlreadyFailed()
        {
            SetStartingCount(1);
            bank.InitializeNewRun();
            bank.TrySpendDash();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/BankedPlayer.prefab");
            GameObject actor = Object.Instantiate(prefab, new Vector3(0f, 8f, 0f), Quaternion.identity);
            objects.Add(actor);
            PlayerMovement2D newcomer = actor.GetComponent<PlayerMovement2D>();
            Assert.That(newcomer.enabled, Is.False);
            Assert.That(newcomer.GetComponent<Rigidbody2D>().simulated, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MissingBankFailsClosedAndBindingRecoversWhenBankAppears()
        {
            Object.Destroy(bank.gameObject);
            yield return null;
            yield return new WaitForFixedUpdate();
            Assert.That(movement.enabled, Is.False);
            Assert.That(body.simulated, Is.False);
            bank = CreateBank(3);
            yield return null;
            yield return new WaitForFixedUpdate();
            Assert.That(movement.enabled, Is.True);
            Assert.That(body.simulated, Is.True);
            yield return Dash(Vector2.right);
            Assert.That(bank.RemainingCharges, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator FinalSpendFreezesAllLinkedPlayersIncludingAnActiveDash()
        {
            PlayerMovement2D second = CreatePlayer(new Vector3(0f, 8f, 0f));
            SetStartingCount(2);
            bank.InitializeNewRun();
            movement.RequestDash(Vector2.right);
            yield return new WaitForFixedUpdate();
            Assert.That(movement.IsDashing, Is.True);
            second.RequestDash(Vector2.left);
            yield return new WaitForFixedUpdate();
            Assert.That(bank.IsGameOver, Is.True);
            Assert.That(movement.IsDashing, Is.False);
            Assert.That(second.IsDashing, Is.False);
            Assert.That(body.simulated, Is.False);
            Assert.That(second.GetComponent<Rigidbody2D>().simulated, Is.False);
        }

        [UnityTest]
        public IEnumerator OrdinarySingleSceneTransitionsKeepOneBankAndItsBalance()
        {
            yield return LoadVerificationScene("DashBankTestA");
            RunDashBank original = bank;
            Assert.That(bank.RemainingCharges, Is.EqualTo(4));
            yield return Dash(Vector2.right);
            yield return LoadVerificationScene("DashBankTestB");
            Assert.That(RunDashBank.Instance, Is.SameAs(original));
            Assert.That(bank.RemainingCharges, Is.EqualTo(3));
            Assert.That(SceneDashSettings.AllowsAirDashChaining(), Is.True);
            yield return Dash(Vector2.left);
            yield return Dash(Vector2.up);
            Assert.That(bank.RemainingCharges, Is.EqualTo(1));
            yield return LoadVerificationScene("DashBankTestA");
            Assert.That(bank.RemainingCharges, Is.EqualTo(1));
            Assert.That(SceneDashSettings.AllowsAirDashChaining(), Is.False);
            Assert.That(Object.FindObjectsByType<RunDashBank>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator NewRunKeepsSceneAndPositionButRestoresAirEligibility()
        {
            yield return Dash(Vector2.right);
            Vector2 position = body.position;
            Scene scene = SceneManager.GetActiveScene();
            bank.InitializeNewRun();
            Assert.That(bank.RemainingCharges, Is.EqualTo(4));
            Assert.That(body.position, Is.EqualTo(position));
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(scene));
            yield return Dash(Vector2.up);
            Assert.That(bank.RemainingCharges, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator EnablingPuzzleChainingDoesNotGrantResourceCharges()
        {
            SceneDashSettings settings = ConfigureChaining(false);
            yield return Dash(Vector2.right);
            SerializedObject serialized = new SerializedObject(settings);
            serialized.FindProperty("allowAirDashChaining").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(bank.RemainingCharges, Is.EqualTo(3));
            yield return Dash(Vector2.up);
            Assert.That(bank.RemainingCharges, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator EventCallbacksCannotRecursivelySpendOrResetTheTransaction()
        {
            int callbacks = 0;
            bank.ChargesChanged += remaining => {
                callbacks++;
                Assert.That(bank.TrySpendDash(), Is.False);
                bank.InitializeNewRun();
            };
            Assert.That(bank.TrySpendDash(), Is.True);
            Assert.That(bank.RemainingCharges, Is.EqualTo(3));
            Assert.That(callbacks, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator BankDisableReenableDoesNotRestoreSpentCharges()
        {
            yield return Dash(Vector2.right);
            bank.enabled = false;
            yield return new WaitForFixedUpdate();
            Assert.That(body.simulated, Is.False);
            bank.enabled = true;
            yield return new WaitForFixedUpdate();
            Assert.That(body.simulated, Is.True);
            Assert.That(bank.RemainingCharges, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator DisabledLinkCannotBypassResourcesOrReceiveResetCallbacks()
        {
            link.enabled = false;
            Assert.That(body.simulated, Is.False);
            bank.InitializeNewRun();
            Assert.That(body.simulated, Is.False);
            movement.RequestDash(Vector2.right);
            yield return new WaitForFixedUpdate();
            Assert.That(bank.RemainingCharges, Is.EqualTo(4));
            link.enabled = true;
            yield return Dash(Vector2.right);
            Assert.That(bank.RemainingCharges, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator UnsimulatedBodyCannotSpendACharge()
        {
            body.simulated = false;
            movement.RequestDash(Vector2.right);
            yield return new WaitForFixedUpdate();
            Assert.That(bank.RemainingCharges, Is.EqualTo(4));
            Assert.That(movement.IsDashing, Is.False);
        }

        [UnityTest]
        public IEnumerator CompetingApprovalOwnerCannotBeSilentlyBypassed()
        {
            LogAssert.Expect(LogType.Error, "PlayerDashBankLink requires exclusive ownership of DashApproval.");
            movement.DashApproval = direction => false;
            yield return new WaitForFixedUpdate();
            Assert.That(link.enabled, Is.False);
            Assert.That(body.simulated, Is.False);
            Assert.That(bank.RemainingCharges, Is.EqualTo(4));
        }

        private RunDashBank CreateBank(int count)
        {
            GameObject owner = new GameObject("Run bank (test)");
            objects.Add(owner);
            owner.SetActive(false);
            RunDashBank result = owner.AddComponent<RunDashBank>();
            SerializedObject serialized = new SerializedObject(result);
            serialized.FindProperty("startingDashCount").intValue = count;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            owner.SetActive(true);
            return result;
        }

        private PlayerMovement2D CreatePlayer(Vector3? position = null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prototypes/Pass1B/Prefabs/CardinalDashPlayer.prefab");
            GameObject actor = Object.Instantiate(prefab, position ?? new Vector3(0f, 4f, 0f), Quaternion.identity);
            objects.Add(actor);
            actor.GetComponent<PlayerMovementInput2D>().enabled = false;
            Rigidbody2D physics = actor.GetComponent<Rigidbody2D>();
            physics.gravityScale = 0f;
            physics.linearVelocity = Vector2.zero;
            actor.AddComponent<PlayerDashBankLink>();
            return actor.GetComponent<PlayerMovement2D>();
        }

        private void SetStartingCount(int count)
        {
            SerializedObject serialized = new SerializedObject(bank);
            serialized.FindProperty("startingDashCount").intValue = count;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private SceneDashSettings ConfigureChaining(bool allow)
        {
            GameObject owner = new GameObject("Scene dash policy (test)");
            objects.Add(owner);
            SceneDashSettings settings = owner.AddComponent<SceneDashSettings>();
            SerializedObject serialized = new SerializedObject(settings);
            serialized.FindProperty("allowAirDashChaining").boolValue = allow;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return settings;
        }

        private BoxCollider2D Solid(string name, Vector2 position, Vector2 size)
        {
            GameObject owner = new GameObject(name, typeof(BoxCollider2D));
            objects.Add(owner);
            owner.transform.position = position;
            BoxCollider2D collider = owner.GetComponent<BoxCollider2D>();
            collider.size = size;
            return collider;
        }

        private IEnumerator Dash(Vector2 direction)
        {
            movement.RequestDash(direction);
            yield return new WaitForFixedUpdate();
            Assert.That(movement.IsDashing, Is.True, "Expected an eligible nonfatal dash.");
            yield return Finish();
        }

        private IEnumerator Finish()
        {
            for (int i = 0; i < 120; i++)
            {
                yield return new WaitForFixedUpdate();
                if (!movement.IsDashing) yield break;
            }
            Assert.Fail("Dash did not finish.");
        }

        private IEnumerator Ground()
        {
            Physics2D.SyncTransforms();
            for (int i = 0; i < 120; i++)
            {
                yield return new WaitForFixedUpdate();
                if (movement.IsGrounded) yield break;
            }
            Assert.Fail("Landing was not detected.");
        }

        private void AssertSubscribers(int expected)
        {
            foreach (string name in new[] { "ChargesChanged", "GameOver", "NewRunStarted" })
            {
                Delegate handlers = (Delegate)typeof(RunDashBank).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(bank);
                Assert.That(handlers?.GetInvocationList().Length ?? 0, Is.EqualTo(expected), name);
            }
        }

        private IEnumerator LoadVerificationScene(string name)
        {
            Scene scene = EditorSceneManager.LoadSceneInPlayMode(Root + "/Scenes/" + name + ".unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            scenes.Add(scene);
            yield return null;
            Assert.That(RunDashBank.Instance, Is.SameAs(bank));
            movement = Object.FindFirstObjectByType<PlayerDashBankLink>().GetComponent<PlayerMovement2D>();
            movement.GetComponent<PlayerMovementInput2D>().enabled = false;
            body = movement.GetComponent<Rigidbody2D>();
            body.position = new Vector2(0f, 8f);
            body.linearVelocity = Vector2.zero;
            body.gravityScale = 0f;
            yield return new WaitForFixedUpdate();
        }
    }
}
#endif
