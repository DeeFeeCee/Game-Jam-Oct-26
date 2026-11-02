#if UNITY_EDITOR
using System.Collections;
using GameJam.Mechanics;
using GameJam.Resources;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameJam.UnifiedGameplay.Tests
{
    public sealed class CrusherTests : MechanicsTestFixture
    {
        private FallingCrusherPlatform2D Crusher(Vector2 position, float speed = 6, float delay = 0, float descent = 6)
        {
            var obj = Own(new GameObject("Test crusher")); obj.SetActive(false); obj.transform.position = position;
            obj.AddComponent<Rigidbody2D>(); obj.AddComponent<BoxCollider2D>();
            var crusher = obj.AddComponent<FallingCrusherPlatform2D>();
            crusher.size = new Vector2(4,.5f); crusher.fallSpeed = speed; crusher.warningDelay = delay; crusher.maximumDescent = descent;
            obj.SetActive(true); return crusher;
        }
        [UnityTest] public IEnumerator ActivationWarningAndMaximumDescentAreDeterministic()
        {
            var crusher = Crusher(new Vector2(0,8), 4,.2f,2); var player = Player(new Vector2(10,0));
            Assert.AreEqual(CrusherState.Armed,crusher.State); Assert.IsTrue(crusher.TryActivate(player.GetComponent<Collider2D>()));
            Assert.IsFalse(crusher.TryActivate(player.GetComponent<Collider2D>())); yield return Ticks(5);
            Assert.AreEqual(CrusherState.Warning,crusher.State); Assert.AreEqual(8,crusher.transform.position.y,.001f);
            yield return Ticks(40); Assert.AreEqual(CrusherState.Landed,crusher.State); Assert.AreEqual(6,crusher.transform.position.y,.02f);
        }
        [UnityTest] public IEnumerator FastUndersideSweepFailsWithoutSpendingOrTunneling()
        {
            var crusher = Crusher(new Vector2(0,5),1000); var player = Player(new Vector2(0,1)); int deaths = 0;
            Bank.GameOver += () => deaths++; crusher.TryActivate(player.GetComponent<Collider2D>()); yield return Ticks(10);
            Assert.IsTrue(Bank.IsGameOver); Assert.AreEqual(RunFailureCause.Environmental,Bank.FailureCause);
            Assert.AreEqual(5,Bank.RemainingCharges); Assert.AreEqual(1,deaths); Assert.Greater(crusher.transform.position.y,player.transform.position.y);
        }
        [UnityTest] public IEnumerator WorldFloorStopsCrusherAndLandedTopIsSafe()
        {
            Solid(Vector2.zero,new Vector2(20,1)); var crusher = Crusher(new Vector2(0,5),1000); var activator = Player(new Vector2(10,2));
            crusher.TryActivate(activator.GetComponent<Collider2D>()); yield return Ticks(10);
            Assert.AreEqual(CrusherState.Landed,crusher.State); Assert.AreEqual(.76f,crusher.transform.position.y,.04f);
            var rider = Player(new Vector2(0,3),3); yield return Ticks(45);
            Assert.IsTrue(rider.IsGrounded); Assert.IsFalse(Bank.IsGameOver); Assert.AreEqual(5,Bank.RemainingCharges);
        }
        [UnityTest] public IEnumerator DescendingTopSupportsRiderAndWalkingIsSafe()
        {
            var crusher = Crusher(new Vector2(0,8),2); var rider = Player(new Vector2(0,9.1f),3);
            yield return Ticks(15); Assert.IsTrue(rider.IsGrounded);
            crusher.TryActivate(rider.GetComponent<Collider2D>());
            float offset = rider.transform.position.y-crusher.transform.position.y;
            yield return Ticks(30); Assert.IsTrue(rider.IsGrounded); Assert.IsFalse(Bank.IsGameOver);
            Assert.AreEqual(offset,rider.transform.position.y-crusher.transform.position.y,.07f);
            rider.SetHorizontalInput(1); float x = rider.transform.position.x; yield return Ticks(6);
            Assert.Greater(rider.transform.position.x,x+.5f); Assert.IsFalse(Bank.IsGameOver);
        }
        [UnityTest] public IEnumerator JumpAndDashFromTopUseAcceptedRules()
        {
            var crusher = Crusher(new Vector2(0,8),2); var rider = Player(new Vector2(0,9.1f),3);
            yield return Ticks(15); crusher.TryActivate(rider.GetComponent<Collider2D>()); yield return Ticks(5);
            rider.RequestJump(); yield return new WaitForFixedUpdate();
            Assert.Greater(rider.GetComponent<Rigidbody2D>().linearVelocity.y,9); Assert.IsFalse(rider.IsGrounded);
            rider.RequestDash(Vector2.right); yield return new WaitForFixedUpdate();
            Assert.IsTrue(rider.IsDashing); Assert.AreEqual(4,Bank.RemainingCharges); Assert.IsFalse(Bank.IsGameOver);
        }
        [UnityTest] public IEnumerator SideContactAndStationaryUndersideAreNonlethal()
        {
            var crusher = Crusher(new Vector2(0,5),2); var player = Player(new Vector2(2.45f,4.5f));
            crusher.TryActivate(player.GetComponent<Collider2D>()); yield return Ticks(25);
            Assert.IsFalse(Bank.IsGameOver);
            var stationary = Crusher(new Vector2(10,5)); var below = Player(new Vector2(10,3.9f));
            below.GetComponent<Rigidbody2D>().linearVelocity = Vector2.up; yield return Ticks(10);
            Assert.AreEqual(CrusherState.Armed,stationary.State); Assert.IsFalse(Bank.IsGameOver);
        }
        [UnityTest] public IEnumerator PauseFailureAndFreshInstancePreserveStateRules()
        {
            var crusher = Crusher(new Vector2(0,8),2); var player = Player(new Vector2(10,0));
            crusher.TryActivate(player.GetComponent<Collider2D>()); yield return Ticks(5);
            Time.timeScale = 0; var position = crusher.transform.position; yield return new WaitForSecondsRealtime(.1f);
            Assert.AreEqual(position,crusher.transform.position); Time.timeScale = 1;
            Bank.TryFailEnvironment(); yield return Ticks(5); Assert.AreEqual(position,crusher.transform.position);
            var fresh = Crusher(new Vector2(10,8)); Assert.AreEqual(CrusherState.Armed,fresh.State);
        }
        [UnityTest] public IEnumerator ActivationVolumeAcceptsPlayerButIgnoresUnrelatedBody()
        {
            var crusher = Crusher(new Vector2(0,8),2,.5f); var trigger = Own(new GameObject("Activation region"));
            var box = trigger.AddComponent<BoxCollider2D>(); box.isTrigger = true; box.size = new Vector2(4,4);
            trigger.AddComponent<CrusherActivationZone2D>().crusher = crusher;
            var crate = Own(new GameObject("Unrelated",typeof(Rigidbody2D),typeof(BoxCollider2D))); crate.GetComponent<Rigidbody2D>().gravityScale = 0;
            yield return Ticks(3); Assert.AreEqual(CrusherState.Armed,crusher.State);
            Player(new Vector2(1,0)); yield return Ticks(3); Assert.AreEqual(CrusherState.Warning,crusher.State);
        }
        [UnityTest] public IEnumerator PlayerFallingAwayFasterThanCrusherIsNotAFalseUndersideHit()
        {
            var crusher=Crusher(new Vector2(0,5),6);
            var player=Player(new Vector2(0,3.85f));
            player.GetComponent<Rigidbody2D>().linearVelocity=new Vector2(0,-10);
            crusher.TryActivate(player.GetComponent<Collider2D>()); yield return Ticks(3);
            Assert.IsFalse(Bank.IsGameOver,"Separated bodies moving apart must not count as an underside impact.");
            Assert.AreEqual(5,Bank.RemainingCharges);
        }
    }
}
#endif
