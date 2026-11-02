#if UNITY_EDITOR
using System.Collections;
using GameJam.Mechanics;
using GameJam.Movement;
using GameJam.Resources;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameJam.UnifiedGameplay.Tests
{
    public sealed class BreakableWallTests : MechanicsTestFixture
    {
        private BreakableWall2D Wall(float x = 1.3f)
        {
            var box = Solid(new Vector2(x, 0), new Vector2(.5f, 5));
            return box.gameObject.AddComponent<BreakableWall2D>();
        }
        [UnityTest] public IEnumerator WalkingAndJumpingDoNotBreakSolidWall()
        {
            Solid(new Vector2(0,-1.3f),new Vector2(20,1));
            var wall = Wall(); var player = Player(Vector2.zero,3);
            player.SetHorizontalInput(1); yield return Ticks(25);
            Assert.IsFalse(wall.IsBroken); Assert.Less(player.transform.position.x, .7f);
            Assert.IsTrue(player.IsGrounded);
            player.RequestJump(); yield return Ticks(5);
            Assert.Greater(player.GetComponent<Rigidbody2D>().linearVelocity.y,0);
            Assert.IsFalse(wall.IsBroken); Assert.AreEqual(5, Bank.RemainingCharges);
        }
        [UnityTest] public IEnumerator ApprovedDashBreaksWallAndSpendsOnlyOneCharge()
        {
            var wall = Wall(); var player = Player(Vector2.zero);
            player.RequestDash(Vector2.right); yield return Ticks(9);
            Assert.IsTrue(wall == null || wall.IsBroken); Assert.Greater(player.transform.position.x, 2);
            Assert.AreEqual(4, Bank.RemainingCharges);
        }
        [UnityTest] public IEnumerator FlushReactiveWallCanRequestApproval()
        {
            var player = Player(Vector2.zero); var wall = Wall(.7f); // player half-width .45 + wall .25
            Physics2D.SyncTransforms(); player.RequestDash(Vector2.right); yield return Ticks(3);
            Assert.IsTrue(wall == null || wall.IsBroken); Assert.AreEqual(4, Bank.RemainingCharges);
        }
        [UnityTest] public IEnumerator RejectedApprovalCannotBreakOrSpend()
        {
            var wall = Wall(); var player = Player(Vector2.zero); int calls = 0;
            player.GetComponent<PlayerDashBankLink>().enabled = false;
            // Isolate the movement hook; disabling the production link deliberately freezes its actor.
            player.GetComponent<Rigidbody2D>().simulated = true; player.enabled = true;
            player.DashApproval = _ => { calls++; return false; };
            player.RequestDash(Vector2.right); yield return Ticks(9);
            Assert.IsFalse(wall.IsBroken); Assert.AreEqual(1, calls); Assert.AreEqual(5, Bank.RemainingCharges);
        }
        [UnityTest] public IEnumerator IneligibleAirborneDashCannotReachWallOrApproval()
        {
            var player = Player(Vector2.zero); player.RequestDash(Vector2.up); yield return Ticks(10);
            var wall = Wall(player.transform.position.x + 1.3f);
            wall.transform.position = new Vector2(wall.transform.position.x, player.transform.position.y);
            int calls = 0; Bank.ChargesChanged += _ => calls++;
            player.RequestDash(Vector2.right); yield return Ticks(10);
            Assert.IsFalse(wall.IsBroken); Assert.AreEqual(0, calls); Assert.AreEqual(4, Bank.RemainingCharges);
        }
        [UnityTest] public IEnumerator FatalFinalChargeDoesNotBreakOrTravel()
        {
            for (int i = 0; i < 4; i++) Assert.IsTrue(Bank.TrySpendDash());
            var wall = Wall(.7f); var player = Player(Vector2.zero);
            var start = player.transform.position; player.RequestDash(Vector2.right); yield return Ticks(3);
            Assert.IsFalse(wall.IsBroken); Assert.AreEqual(start, player.transform.position);
            Assert.IsTrue(Bank.IsGameOver); Assert.AreEqual(0, Bank.RemainingCharges);
        }
        [UnityTest] public IEnumerator CloserOrdinarySolidProtectsWall()
        {
            var wall = Wall(2); Solid(new Vector2(1,0), new Vector2(.3f,5)); var player = Player(Vector2.zero);
            player.RequestDash(Vector2.right); yield return Ticks(10);
            Assert.IsFalse(wall.IsBroken); Assert.Less(player.transform.position.x, .5f);
        }
        [UnityTest] public IEnumerator SolidBehindBrokenWallStillStopsDash()
        {
            var wall = Wall(); Solid(new Vector2(2.2f,0), new Vector2(.3f,5)); var player = Player(Vector2.zero);
            player.RequestDash(Vector2.right); yield return Ticks(10);
            Assert.IsTrue(wall == null || wall.IsBroken); Assert.Less(player.transform.position.x, 1.65f);
            Assert.AreEqual(4, Bank.RemainingCharges);
        }
        [UnityTest] public IEnumerator OverlappingOrdinarySolidPreventsReactionAndFreshWallIsSolid()
        {
            var wall = Wall(); Solid(wall.transform.position, new Vector2(.5f,5)); var player = Player(Vector2.zero);
            player.RequestDash(Vector2.right); yield return Ticks(10);
            Assert.IsFalse(wall.IsBroken); var fresh = Wall(10);
            Assert.IsFalse(fresh.IsBroken); Assert.IsTrue(fresh.GetComponent<Collider2D>().enabled);
        }
    }
}
#endif
