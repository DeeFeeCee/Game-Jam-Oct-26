#if UNITY_EDITOR
using System.Collections;
using GameJam.Mechanics;
using GameJam.Resources;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GameJam.Foundation.Tests
{
    public sealed partial class FoundationTests
    {
        private static MovingPlatform2D PresentationPlatform(Vector2 travel)
        {
            var root = new GameObject("Locomotion presentation fixture");
            var start = new GameObject("Start").transform;
            var end = new GameObject("End").transform;
            start.SetParent(root.transform);
            end.SetParent(root.transform);
            start.position = new Vector2(100f, 5f);
            end.position = (Vector2)start.position + travel;
            var platformObject = new GameObject("Moving support");
            platformObject.SetActive(false);
            platformObject.transform.SetParent(root.transform);
            var platform = platformObject.AddComponent<MovingPlatform2D>();
            platform.pointA = start;
            platform.pointB = end;
            platform.size = new Vector2(12f, .5f);
            platform.speed = 0f;
            platformObject.SetActive(true);
            return platform;
        }

        private static IEnumerator Board(Jett jett, MovingPlatform2D platform)
        {
            float top = platform.GetComponent<BoxCollider2D>().bounds.max.y;
            jett.Position(new Vector2(platform.EndpointA.x, top + .81f));
            yield return AwaitPose(() => jett.Movement.IsGrounded && jett.In("Idle_anim"), "Grounded on support");
        }

        [UnityTest]
        public IEnumerator HorizontalPlatformCarryStaysIdleAcrossReversal()
        {
            yield return Load(Level);
            var jett = new Jett();
            var platform = PresentationPlatform(new Vector2(1.2f, 0f));
            yield return Board(jett, platform);
            jett.Movement.SetHorizontalInput(-1f);
            yield return AwaitPose(() => jett.In("Walk") && jett.Sprite.flipX, "Face left before transport");
            jett.Movement.SetHorizontalInput(0f);
            yield return AwaitPose(() => jett.In("Idle_anim"), "Stop before transport");
            platform.speed = 2f;

            bool movedRight = false;
            bool reversed = false;
            double deadline = Time.realtimeSinceStartupAsDouble + 3;
            while (!reversed && Time.realtimeSinceStartupAsDouble < deadline)
            {
                yield return null;
                Assert.True(jett.Movement.IsGrounded);
                Assert.True(jett.In("Idle_anim"), "Transport must not play Walk.");
                Assert.True(jett.Sprite.flipX, "Platform velocity must not change facing.");
                movedRight |= jett.Body.linearVelocity.x > 1f;
                reversed = movedRight && jett.Body.linearVelocity.x < -1f;
            }
            Assert.True(reversed, "The rider was transported in both directions.");
        }

        [UnityTest]
        public IEnumerator VerticalPlatformCarryStaysIdleWhileAscendingAndDescending()
        {
            yield return Load(Level);
            var jett = new Jett();
            var platform = PresentationPlatform(new Vector2(0f, 1.2f));
            yield return Board(jett, platform);
            platform.speed = 2f;

            bool ascended = false;
            bool descended = false;
            double deadline = Time.realtimeSinceStartupAsDouble + 3;
            while (!descended && Time.realtimeSinceStartupAsDouble < deadline)
            {
                yield return null;
                Assert.True(jett.Movement.IsGrounded);
                Assert.True(jett.In("Idle_anim"));
                Assert.False(jett.Sprite.flipX);
                ascended |= jett.Body.linearVelocity.y > 1f;
                descended = ascended && jett.Body.linearVelocity.y < -1f;
            }
            Assert.True(descended, "The rider was transported up and down.");
        }

        [UnityTest]
        public IEnumerator WalkingAndFacingFollowRelativeTravelThenReturnToIdleWhileCarried()
        {
            yield return Load(Level);
            var jett = new Jett();
            var platform = PresentationPlatform(new Vector2(12f, 0f));
            yield return Board(jett, platform);
            platform.speed = 2f;
            yield return AwaitPose(() => jett.Body.linearVelocity.x > 1f, "Support carries rider right");

            jett.Movement.SetHorizontalInput(1f);
            yield return AwaitPose(() => jett.In("Walk") && !jett.Sprite.flipX, "Walk right with support");
            jett.Movement.SetHorizontalInput(-.25f);
            yield return AwaitPose(() => jett.In("Walk") && jett.Sprite.flipX, "Walk left against support");
            Assert.Greater(jett.Body.linearVelocity.x, .1f, "Jett still travels right in world space.");

            jett.Movement.SetHorizontalInput(0f);
            yield return AwaitPose(() => jett.In("Idle_anim") && jett.Body.linearVelocity.x > 1f, "Stop while transported");
            Assert.True(jett.Sprite.flipX);
        }

        [UnityTest]
        public IEnumerator BlockedMovementInputDoesNotPlayWalk()
        {
            yield return Load(Level);
            var jett = new Jett();
            PresentationSolid(new Vector2(100f, 0f), new Vector2(40f, 1f));
            PresentationSolid(new Vector2(101f, 2f), new Vector2(1f, 4f));
            jett.Position(new Vector2(100.251f, 1.31f));
            yield return AwaitPose(() => jett.Movement.IsGrounded && jett.In("Idle_anim"), "Idle beside wall");
            jett.Movement.SetHorizontalInput(1f);
            for (int i = 0; i < 8; i++)
            {
                yield return new WaitForFixedUpdate();
                yield return null;
                Assert.True(jett.In("Idle_anim"), "Blocked input must not play Walk.");
                Assert.Less(Mathf.Abs(jett.Body.linearVelocity.x), .05f);
            }
        }

        [UnityTest]
        public IEnumerator MovingSupportJumpAndDashReturnToNormalPresentation()
        {
            yield return Load(Level);
            var jett = new Jett();
            var platform = PresentationPlatform(new Vector2(12f, 0f));
            yield return Board(jett, platform);
            platform.speed = 2f;
            yield return AwaitPose(() => jett.Body.linearVelocity.x > 1f, "Moving support carries Jett");
            jett.Movement.RequestJump();
            yield return AwaitPose(() => !jett.Movement.IsGrounded, "Jump from moving support");
            jett.AssertNormal("Idle_anim");
            yield return AwaitPose(() => jett.Movement.IsGrounded && jett.Body.linearVelocity.x > 1f, "Land on moving support");
            jett.AssertNormal("Idle_anim");

            jett.Movement.RequestDash(Vector2.up);
            yield return AwaitPose(() => jett.In("Transform"), "Dash overrides support-relative pose");
            yield return AwaitPose(() => jett.In("Plasma"), "Dash enters plasma");
            yield return AwaitPose(() => !jett.Movement.IsDashing && jett.In("Idle_anim"), "Dash clears normally");
            Assert.AreEqual(4, RunDashBank.Instance.RemainingCharges);
        }

        [UnityTest]
        public IEnumerator MovingSupportPauseResumeAndRetryKeepIdlePresentation()
        {
            yield return Load(Level);
            var jett = new Jett();
            var platform = PresentationPlatform(new Vector2(12f, 0f));
            yield return Board(jett, platform);
            platform.speed = 2f;
            yield return AwaitPose(() => jett.Body.linearVelocity.x > 1f, "Moving support carries Jett");
            jett.AssertNormal("Idle_anim");
            float previousScale = Time.timeScale;
            try
            {
                Time.timeScale = 0f;
                yield return null;
                Vector2 pausedAt = jett.Body.position;
                yield return new WaitForSecondsRealtime(.15f);
                Assert.AreEqual(pausedAt, jett.Body.position);
                Assert.Zero(jett.Animator.speed);
                jett.AssertNormal("Idle_anim");
                Time.timeScale = previousScale;
                yield return AwaitPose(() => jett.Body.position.x > pausedAt.x + .05f, "Transport resumes");
                jett.AssertNormal("Idle_anim");
            }
            finally { Time.timeScale = previousScale; }

            var bank = RunDashBank.Instance;
            Assert.True(bank.TryFailEnvironment());
            int previousScene = loaded.handle;
            Assert.True(bank.TryRetryLevel(loaded));
            yield return AwaitPose(() => SceneManager.GetActiveScene().handle != previousScene, "Retry finishes");
            loaded = SceneManager.GetActiveScene();
            yield return null;
            new Jett().AssertNormal("Idle_anim");
            Assert.AreEqual(5, bank.RemainingCharges);
        }
    }
}
#endif
