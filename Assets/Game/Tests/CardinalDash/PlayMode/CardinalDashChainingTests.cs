#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace GameJam.Movement.Tests
{
    public sealed partial class CardinalDashTests
    {
        private readonly List<Scene> policyScenes = new List<Scene>();
        private Scene previousPolicyScene;

        [UnityEngine.TestTools.UnityTest]
        public IEnumerator MissingScenePolicyDefaultsToNoChaining()
        {
            Scene destination = CreatePolicyScene(null);
            SceneManager.SetActiveScene(destination);
            Assert.That(SceneDashSettings.AllowsAirDashChaining(), Is.False);
            yield return null;
        }

        [UnityEngine.TestTools.UnityTest]
        public IEnumerator SceneComponentDefaultsToFalse()
        {
            GameObject policy = new GameObject("Default dash settings");
            objects.Add(policy);
            Assert.That(policy.AddComponent<SceneDashSettings>().AllowAirDashChaining, Is.False);
            yield return null;
        }

        [UnityEngine.TestTools.UnityTest]
        public IEnumerator FirstAirDashSucceedsSecondIsRejectedBeforeApproval()
        {
            ConfigureChaining(false);
            int approvals = 0;
            movement.DashApproval = direction => { approvals++; return true; };
            yield return CompleteDash(Vector2.right);
            Vector2 end = body.position;
            movement.RequestDash(Vector2.up);
            yield return new WaitForFixedUpdate();
            Assert.That(movement.IsDashing, Is.False);
            Assert.That(body.position, Is.EqualTo(end));
            Assert.That(approvals, Is.EqualTo(1), "Ineligible requests must not consume future LM charges.");
        }

        [UnityEngine.TestTools.UnityTest]
        public IEnumerator LandingRestoresAirDashEligibility()
        {
            ConfigureChaining(false);
            yield return CompleteDash(Vector2.right);
            Solid("Landing floor", new Vector2(0f, -0.5f), new Vector2(40f, 1f));
            body.position = new Vector2(0f, 0.8f);
            body.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
            yield return WaitForGround();
            yield return CompleteDash(Vector2.up);
            Assert.That(body.position.y, Is.GreaterThan(3f));
        }

        [UnityEngine.TestTools.UnityTest]
        public IEnumerator SceneOverrideAllowsConsecutiveCompletedDashesAndApprovesEach()
        {
            ConfigureChaining(true);
            int approvals = 0;
            movement.DashApproval = direction => { approvals++; return true; };
            yield return CompleteDash(Vector2.right);
            yield return CompleteDash(Vector2.up);
            Assert.That(approvals, Is.EqualTo(2));
            Assert.That(body.position.x, Is.GreaterThan(2.7f));
            Assert.That(body.position.y, Is.GreaterThan(6.7f));
        }

        [UnityEngine.TestTools.UnityTest]
        public IEnumerator ChainingOverrideCannotOverlapOrRestartActiveDash()
        {
            ConfigureChaining(true);
            int approvals = 0;
            movement.DashApproval = direction => { approvals++; return true; };
            movement.RequestDash(Vector2.right);
            yield return new WaitForFixedUpdate();
            for (int tick = 0; tick < 3; tick++)
            {
                movement.RequestDash(Vector2.up);
                yield return new WaitForFixedUpdate();
                Assert.That(movement.DashDirection, Is.EqualTo(Vector2.right));
            }
            yield return FinishCurrentDash();
            Assert.That(approvals, Is.EqualTo(1));
            Assert.That(body.position.x, Is.EqualTo(2.88f).Within(0.02f));
            Assert.That(body.position.y, Is.EqualTo(4f).Within(0.02f));
        }

        [UnityEngine.TestTools.UnityTest]
        public IEnumerator DestinationScenePolicyReplacesPreviousPolicyWithoutResettingEligibility()
        {
            Scene allowed = CreatePolicyScene(true);
            Scene restricted = CreatePolicyScene(false);
            Scene missing = CreatePolicyScene(null);
            int approvals = 0;
            movement.DashApproval = direction => { approvals++; return true; };
            SceneManager.SetActiveScene(allowed);
            yield return CompleteDash(Vector2.right);
            SceneManager.SetActiveScene(restricted);
            movement.RequestDash(Vector2.up);
            yield return new WaitForFixedUpdate();
            Assert.That(movement.IsDashing, Is.False);
            SceneManager.SetActiveScene(missing);
            movement.RequestDash(Vector2.up);
            yield return new WaitForFixedUpdate();
            Assert.That(movement.IsDashing, Is.False);
            Assert.That(approvals, Is.EqualTo(1));
            SceneManager.SetActiveScene(allowed);
            yield return CompleteDash(Vector2.up);
            Assert.That(approvals, Is.EqualTo(2));
        }

        [UnityEngine.TestTools.UnityTest]
        public IEnumerator GroundedHorizontalDashRearmsWhileStillOnFloor()
        {
            ConfigureChaining(false);
            Solid("Floor", new Vector2(0f, -0.5f), new Vector2(40f, 1f));
            body.position = new Vector2(0f, 0.8f);
            yield return WaitForGround();
            yield return CompleteDash(Vector2.right);
            yield return CompleteDash(Vector2.left);
            Assert.That(body.position.x, Is.EqualTo(0f).Within(0.04f));
        }

        [UnityEngine.TestTools.UnityTest]
        public IEnumerator GroundedUpDashConsumesEligibilityUntilLanding()
        {
            ConfigureChaining(false);
            Solid("Floor", new Vector2(0f, -0.5f), new Vector2(40f, 1f));
            body.position = new Vector2(0f, 0.8f);
            yield return WaitForGround();
            yield return CompleteDash(Vector2.up);
            movement.RequestDash(Vector2.right);
            yield return new WaitForFixedUpdate();
            Assert.That(movement.IsDashing, Is.False);
        }

        [UnityEngine.TestTools.UnityTest]
        public IEnumerator ResourceRejectionDoesNotSpendMovementEligibility()
        {
            ConfigureChaining(false);
            movement.DashApproval = direction => false;
            movement.RequestDash(Vector2.right);
            yield return new WaitForFixedUpdate();
            Assert.That(movement.IsDashing, Is.False);
            movement.DashApproval = direction => true;
            yield return CompleteDash(Vector2.up);
        }

        [UnityEngine.TestTools.UnityTest]
        public IEnumerator DisabledSceneOverrideCannotLeaveChainingEnabled()
        {
            SceneDashSettings settings = ConfigureChaining(true);
            yield return CompleteDash(Vector2.right);
            settings.enabled = false;
            movement.RequestDash(Vector2.up);
            yield return new WaitForFixedUpdate();
            Assert.That(movement.IsDashing, Is.False);
        }

        [UnityEngine.TestTools.UnityTest]
        public IEnumerator MultipleScenePoliciesFailClosed()
        {
            ConfigureChaining(true);
            ConfigureChaining(true);
            Assert.That(SceneDashSettings.AllowsAirDashChaining(), Is.False);
            yield return null;
        }

        [UnityEngine.TestTools.UnityTest]
        public IEnumerator SpaceDashesWithoutEnablingOrPerformingSprint()
        {
            input.enabled = true;
            InputActionAsset clone = (InputActionAsset)typeof(PlayerMovementInput2D)
                .GetField("runtimeActions", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(input);
            InputAction sprint = clone.FindAction("Player/Sprint", true);
            int sprintCalls = 0;
            sprint.performed += context => sprintCalls++;
            Assert.That(sprint.enabled, Is.False);
            Keys(Key.Space);
            yield return new WaitForSeconds(0.06f);
            Assert.That(movement.IsDashing, Is.True);
            Assert.That(sprint.enabled, Is.False);
            Assert.That(sprintCalls, Is.EqualTo(0));
            Assert.That(body.linearVelocity.x, Is.EqualTo(movement.DashSpeed).Within(0.01f));
        }

        [UnityEngine.TestTools.UnityTest]
        public IEnumerator FormerQBindingDoesNotActivateDash()
        {
            input.enabled = true;
            Keys(Key.Q);
            yield return new WaitForSeconds(0.06f);
            Assert.That(movement.IsDashing, Is.False);
        }

        [UnityEngine.TestTools.UnityTest]
        public IEnumerator ReenabledActorStartsFreshWithoutStaleSceneOverride()
        {
            SceneDashSettings settings = ConfigureChaining(true);
            yield return CompleteDash(Vector2.right);
            player.SetActive(false);
            SetChaining(settings, false);
            player.SetActive(true);
            input.enabled = false;
            body.gravityScale = 0f;
            yield return CompleteDash(Vector2.left);
            movement.RequestDash(Vector2.up);
            yield return new WaitForFixedUpdate();
            Assert.That(movement.IsDashing, Is.False);
        }

        private SceneDashSettings ConfigureChaining(bool allow)
        {
            GameObject policy = new GameObject("Scene dash settings (test)");
            objects.Add(policy);
            SceneDashSettings settings = policy.AddComponent<SceneDashSettings>();
            SetChaining(settings, allow);
            return settings;
        }

        private static void SetChaining(SceneDashSettings settings, bool allow)
        {
            SerializedObject serialized = new SerializedObject(settings);
            serialized.FindProperty("allowAirDashChaining").boolValue = allow;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private Scene CreatePolicyScene(bool? allow)
        {
            if (policyScenes.Count == 0)
                previousPolicyScene = SceneManager.GetActiveScene();
            Scene scene = SceneManager.CreateScene("Dash policy test " + System.Guid.NewGuid().ToString("N"));
            policyScenes.Add(scene);
            if (allow.HasValue)
            {
                SceneDashSettings settings = ConfigureChaining(allow.Value);
                SceneManager.MoveGameObjectToScene(settings.gameObject, scene);
            }
            return scene;
        }

        private IEnumerator RestorePolicyScenes()
        {
            if (previousPolicyScene.IsValid() && previousPolicyScene.isLoaded)
                SceneManager.SetActiveScene(previousPolicyScene);
            foreach (Scene scene in policyScenes)
                if (scene.IsValid() && scene.isLoaded)
                    yield return SceneManager.UnloadSceneAsync(scene);
            policyScenes.Clear();
            previousPolicyScene = default;
        }
    }
}
#endif
