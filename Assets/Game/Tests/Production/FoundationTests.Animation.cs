#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using GameJam.Levels;
using GameJam.Movement;
using GameJam.Presentation;
using GameJam.Resources;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GameJam.Foundation.Tests
{
    public sealed partial class FoundationTests
    {
        private sealed class Jett
        {
            public readonly PlayerMovement2D Movement = Object.FindFirstObjectByType<PlayerMovement2D>();
            public readonly Rigidbody2D Body;
            public readonly Animator Animator;
            public readonly SpriteRenderer Sprite;

            public Jett()
            {
                Assert.AreEqual(1, Object.FindObjectsByType<PlayerMovement2D>(FindObjectsSortMode.None).Length);
                Movement.GetComponent<PlayerMovementInput2D>().enabled = false;
                Body = Movement.GetComponent<Rigidbody2D>();
                Animator = Movement.GetComponentInChildren<PlayerPresentation>().GetComponent<Animator>();
                Sprite = Animator.GetComponent<SpriteRenderer>();
                Assert.False(Animator.applyRootMotion);
                Assert.NotNull(Sprite.sprite);
            }

            public bool In(string pose) =>
                Animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer." + pose);

            public void Position(Vector2 position)
            {
                Body.position = position;
                Body.linearVelocity = Vector2.zero;
                Physics2D.SyncTransforms();
            }

            public void AssertNormal(string pose)
            {
                Assert.True(In(pose), $"Expected {pose}, sprite: {Sprite.sprite.name}");
                Assert.AreEqual(Quaternion.identity, Sprite.transform.localRotation);
                Assert.AreEqual(pose == "Walk" ? Vector3.one : Vector3.one * 5f, Sprite.transform.localScale);
            }
        }

        private static IEnumerator AwaitPose(Func<bool> condition, string description)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 3;
            while (!condition() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.True(condition(), description);
        }

        private static void PresentationSolid(Vector2 position, Vector2 size)
        {
            var wall = new GameObject("Presentation test solid", typeof(BoxCollider2D));
            wall.transform.position = position;
            wall.GetComponent<BoxCollider2D>().size = size;
            Physics2D.SyncTransforms();
        }

        [UnityTest]
        public IEnumerator WalkUsesSixFramesLoopsAndReturnsToIdle()
        {
            yield return Load(Level);
            var jett = new Jett();
            PresentationSolid(new Vector2(100, 0), new Vector2(40, 1));
            jett.Position(new Vector2(100, 1.31f));
            yield return AwaitPose(() => jett.Movement.IsGrounded, "Test player grounded");
            jett.Movement.SetHorizontalInput(1f);
            yield return AwaitPose(() => jett.In("Walk"), "Walk starts");
            var frames = new HashSet<Sprite>();
            double deadline = Time.realtimeSinceStartupAsDouble + 3;
            while (jett.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1.1f &&
                   Time.realtimeSinceStartupAsDouble < deadline)
            {
                frames.Add(jett.Sprite.sprite);
                Assert.True(jett.In("Walk"));
                Assert.False(jett.Sprite.flipX);
                yield return null;
            }
            Assert.That(jett.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.GreaterThanOrEqualTo(1.1f));
            Assert.AreEqual(6, frames.Count);
            Assert.True(jett.Animator.GetCurrentAnimatorStateInfo(0).loop);
            jett.AssertNormal("Walk");
            jett.Movement.SetHorizontalInput(-1f);
            yield return AwaitPose(() => jett.Sprite.flipX, "Left-facing walk");
            jett.Movement.SetHorizontalInput(0f);
            yield return AwaitPose(() => jett.In("Idle_anim"), "Idle resumes on stop");
            jett.AssertNormal("Idle_anim");
        }

        [UnityTest]
        public IEnumerator DashPresentationUsesResolvedCardinalDirectionAndOriginalTravel()
        {
            yield return Load(Level);
            var jett = new Jett();
            var bank = RunDashBank.Instance;
            var requests = new[]
            {
                (Vector2.one, Vector2.right),
                (Vector2.up, Vector2.up),
                (Vector2.down, Vector2.down),
                (Vector2.zero, Vector2.left)
            };
            for (int i = 0; i < requests.Length; i++)
            {
                if (i > 0) bank.InitializeNewRun(); // Reset eligibility between isolated attempts.
                if (i == 3) jett.Movement.SetHorizontalInput(-1f);
                jett.Position(new Vector2(100f, 8f));
                Vector2 start = jett.Body.position;
                var request = requests[i];
                jett.Movement.RequestDash(request.Item1);
                yield return AwaitPose(() => jett.In("Transform"), "A-Dash appears after approval");
                Assert.True(jett.Movement.IsDashing);
                Assert.AreEqual(request.Item2, jett.Movement.DashDirection);
                Assert.AreEqual(bank.StartingDashCount - 1, bank.RemainingCharges);
                Assert.That(jett.Sprite.sprite.name, Does.StartWith("JettADash_"));
                Assert.False(jett.Animator.GetCurrentAnimatorStateInfo(0).loop);
                // Repeated requests during an active dash must not replay the transformation.
                jett.Movement.RequestDash(-request.Item2);
                yield return AwaitPose(() => jett.In("Plasma"), "B-Dash follows A-Dash");
                Assert.AreEqual("JettBDash_0", jett.Sprite.sprite.name);
                Assert.That(Vector2.Dot(jett.Sprite.transform.right, request.Item2), Is.GreaterThan(.999f));
                Assert.AreEqual(Vector3.zero, jett.Sprite.transform.localPosition);
                Assert.AreEqual(Vector3.one * .5f, jett.Sprite.transform.localScale);
                Assert.AreEqual(Quaternion.identity, jett.Movement.transform.rotation);
                Assert.That(jett.Movement.DashDuration, Is.EqualTo(.20f).Within(.0001f));
                yield return AwaitPose(() => !jett.Movement.IsDashing && jett.In("Idle_anim"), "Airborne idle after dash");
                Assert.That(Vector2.Dot(jett.Body.position - start, request.Item2),
                    Is.EqualTo(jett.Movement.DashSpeed * .20f).Within(.15f));
                Assert.AreEqual(bank.StartingDashCount - 1, bank.RemainingCharges);
                Assert.That(jett.Body.gravityScale, Is.EqualTo(3f));
                jett.AssertNormal("Idle_anim");
            }
        }

        [UnityTest]
        public IEnumerator BlockedRequestStaysIdleAndShortDashClearsOnCollision()
        {
            yield return Load(Level);
            var jett = new Jett();
            var bank = RunDashBank.Instance;
            PresentationSolid(new Vector2(100, 0), new Vector2(40, 1));
            PresentationSolid(new Vector2(101, 2), new Vector2(1, 4));
            jett.Position(new Vector2(99.6f, 1.31f));
            yield return AwaitPose(() => jett.Movement.IsGrounded, "Test player grounded");
            // Place flush before the request, before contact separation can create clearance.
            jett.Position(new Vector2(100.251f, 1.31f));
            jett.Movement.RequestDash(Vector2.right);
            for (int i = 0; i < 4; i++)
            {
                yield return new WaitForFixedUpdate();
                yield return null;
                jett.AssertNormal("Idle_anim");
                Assert.False(jett.Movement.IsDashing);
            }
            Assert.AreEqual(bank.StartingDashCount, bank.RemainingCharges);

            jett.Position(new Vector2(99.6f, 1.31f));
            jett.Movement.RequestDash(Vector2.right);
            yield return AwaitPose(() => jett.In("Transform"), "Short dash begins before the wall");
            yield return AwaitPose(() => !jett.Movement.IsDashing, "Wall stops the dash");
            yield return null;
            jett.AssertNormal("Idle_anim");
            Assert.LessOrEqual(jett.Body.position.x, 100.26f);
            Assert.AreEqual(bank.StartingDashCount - 1, bank.RemainingCharges);
        }

        [UnityTest]
        public IEnumerator WalkingDashReturnsToWalkAndAllowsAnotherGroundedDash()
        {
            yield return Load(Level);
            var jett = new Jett();
            PresentationSolid(new Vector2(100, 0), new Vector2(40, 1));
            jett.Position(new Vector2(100, 1.31f));
            yield return AwaitPose(() => jett.Movement.IsGrounded, "Test player grounded");
            jett.Movement.SetHorizontalInput(1f);
            for (int i = 0; i < 2; i++)
            {
                yield return AwaitPose(() => jett.In("Walk"), "Grounded walk");
                jett.Movement.RequestDash(Vector2.right);
                yield return AwaitPose(() => jett.In("Transform"), "Walk to A-Dash");
                yield return AwaitPose(() => jett.In("Plasma"), "A-Dash to B-Dash");
                yield return AwaitPose(() => jett.In("Walk"), "Dash back to walk");
                jett.AssertNormal("Walk");
            }
            Assert.AreEqual(3, RunDashBank.Instance.RemainingCharges);
        }

        [UnityTest]
        public IEnumerator PauseFreezesBothDashPosesAndResumeCompletesDash()
        {
            yield return Load(Level);
            var jett = new Jett();
            jett.Position(new Vector2(100, 8));
            jett.Movement.RequestDash(Vector2.up);
            foreach (string phase in new[] { "Transform", "Plasma" })
            {
                yield return AwaitPose(() => jett.In(phase), phase);
                float priorScale = Time.timeScale;
                try
                {
                    Time.timeScale = 0f;
                    yield return null; // The request was made after this frame's Update.
                    Sprite sprite = jett.Sprite.sprite;
                    float time = jett.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                    Vector2 position = jett.Body.position;
                    yield return new WaitForSecondsRealtime(.15f);
                    Assert.True(jett.In(phase));
                    Assert.True(jett.Movement.IsDashing);
                    Assert.AreSame(sprite, jett.Sprite.sprite);
                    Assert.AreEqual(time, jett.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime);
                    Assert.AreEqual(position, jett.Body.position);
                }
                finally { Time.timeScale = priorScale; }
            }
            yield return AwaitPose(() => jett.In("Idle_anim"), "Normal pose resumes");
            jett.AssertNormal("Idle_anim");
        }

        [UnityTest]
        public IEnumerator EnvironmentalFailureClearsPlasmaAndRetryRespawnsNormal()
        {
            yield return Load(Level);
            var jett = new Jett();
            var bank = RunDashBank.Instance;
            jett.Position(new Vector2(100, 8));
            jett.Movement.RequestDash(Vector2.up);
            yield return AwaitPose(() => jett.In("Plasma"), "Active plasma");
            Assert.True(bank.TryFailEnvironment());
            yield return AwaitPose(() => jett.In("Idle_anim"), "Failure clears plasma");
            Assert.False(jett.Body.simulated);
            Assert.AreEqual(4, bank.RemainingCharges);
            jett.AssertNormal("Idle_anim");
            int oldHandle = loaded.handle;
            Assert.True(bank.TryRetryLevel(loaded));
            yield return AwaitPose(() => SceneManager.GetActiveScene().handle != oldHandle, "Retry reloads room");
            loaded = SceneManager.GetActiveScene();
            yield return null;
            Assert.AreEqual(Level, loaded.path);
            Assert.AreEqual(5, bank.RemainingCharges);
            new Jett().AssertNormal("Idle_anim");
        }

        [UnityTest]
        public IEnumerator FinalChargeCannotStartDashAnimationAndNewRunStartsNormal()
        {
            yield return Load(Level);
            var jett = new Jett();
            var bank = RunDashBank.Instance;
            while (bank.RemainingCharges > 1) bank.TrySpendDash();
            jett.Position(new Vector2(100, 8));
            jett.Movement.RequestDash(Vector2.up);
            yield return AwaitPose(() => bank.IsGameOver, "Final spend is fatal");
            yield return null;
            jett.AssertNormal("Idle_anim");
            Assert.False(jett.Movement.IsDashing);
            Assert.AreEqual(0, bank.RemainingCharges);
            bank.InitializeNewRun();
            yield return null;
            Assert.True(jett.Body.simulated);
            jett.AssertNormal("Idle_anim");
        }

        [UnityTest]
        public IEnumerator TransitionDuringDashSpawnsNormalPlayerAndKeepsBalance()
        {
            yield return Load(Level);
            var jett = new Jett();
            var bank = RunDashBank.Instance;
            jett.Position(new Vector2(100, 8));
            jett.Movement.RequestDash(Vector2.up);
            yield return AwaitPose(() => jett.In("Plasma"), "Active plasma before exit");
            Assert.True(Object.FindFirstObjectByType<LevelTransition>().TryExit(jett.Movement));
            yield return AwaitPose(() => SceneManager.GetActiveScene().path == CampaignNextLevel, "Next room loads");
            loaded = SceneManager.GetActiveScene();
            yield return null;
            new Jett().AssertNormal("Idle_anim");
            Assert.AreSame(bank, RunDashBank.Instance);
            Assert.AreEqual(4, bank.RemainingCharges);
        }
    }
}
#endif
