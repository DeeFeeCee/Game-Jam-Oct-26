#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using GameJam.Mechanics;
using GameJam.Movement;
using GameJam.Resources;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GameJam.UnifiedGameplay.Tests
{
    public sealed class MovingPlatformTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private MovingPlatform2D platform;
        private PlayerMovement2D actor;
        private Rigidbody2D body;
        private GameObject Track(GameObject obj) { objects.Add(obj); return obj; }
        [UnitySetUp] public IEnumerator Setup()
        {
            Assert.IsNull(RunDashBank.Instance);
            Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/RunDashBank.prefab"));
            yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1;
            foreach (var obj in objects) if (obj != null) Object.Destroy(obj); objects.Clear();
            if (RunDashBank.Instance != null) Object.Destroy(RunDashBank.Instance.gameObject);
            yield return null; LogAssert.NoUnexpectedReceived();
        }
        private void Platform(Vector2 end, float speed = 2, float delay = 0, bool atB = false)
        {
            var root = Track(new GameObject("Moving platform test"));
            var a = new GameObject("Endpoint A").transform; a.SetParent(root.transform); a.position = Vector2.zero;
            var b = new GameObject("Endpoint B").transform; b.SetParent(root.transform); b.position = end;
            var obj = new GameObject("Moving solid"); obj.SetActive(false); obj.transform.SetParent(root.transform);
            var box = obj.AddComponent<BoxCollider2D>(); box.sharedMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>("Assets/Game/Tests/Fixtures/Movement/Physics/PlayerFrictionless.physicsMaterial2D");
            obj.AddComponent<Rigidbody2D>(); platform = obj.AddComponent<MovingPlatform2D>();
            platform.pointA = a; platform.pointB = b; platform.speed = speed; platform.endpointDelay = delay; platform.startAtB = atB;
            obj.SetActive(true);
        }
        private IEnumerator Rider()
        {
            platform.speed = 0;
            var obj = Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/Player.prefab"), platform.transform.position + new Vector3(0, 1.06f), Quaternion.identity));
            actor = obj.GetComponent<PlayerMovement2D>(); body = obj.GetComponent<Rigidbody2D>(); obj.GetComponent<PlayerMovementInput2D>().enabled = false;
            for (int i = 0; i < 100 && !actor.IsGrounded; i++) yield return new WaitForFixedUpdate();
            Assert.IsTrue(actor.IsGrounded); yield return null;
        }
        private static IEnumerator Ticks(int ticks) { for (int i = 0; i < ticks; i++) yield return new WaitForFixedUpdate(); yield return null; }
        [UnityTest] public IEnumerator EndpointsAndReversalAreAccurate()
        {
            Platform(new Vector2(2,0)); yield return Ticks(50);
            Assert.AreEqual(2, platform.GetComponent<Rigidbody2D>().position.x, .08f);
            yield return Ticks(25); Assert.That(platform.GetComponent<Rigidbody2D>().position.x, Is.InRange(.8f, 1.2f));
            Assert.AreEqual(Vector3.zero, platform.pointA.position); Assert.AreEqual(new Vector3(2,0), platform.pointB.position);
        }
        [UnityTest] public IEnumerator StartAtBAndDiagonalMotionWork()
        {
            Platform(new Vector2(2,2), 2, 0, true); Assert.AreEqual(new Vector2(2,2), platform.GetComponent<Rigidbody2D>().position);
            yield return Ticks(15); var p = platform.GetComponent<Rigidbody2D>().position;
            Assert.Less(p.x, 2); Assert.AreEqual(p.x, p.y, .001f);
        }
        [UnityTest] public IEnumerator EndpointDelayDoesNotAccumulateMotion()
        {
            Platform(new Vector2(1,0), 2, .5f); yield return Ticks(30);
            var p = platform.GetComponent<Rigidbody2D>().position; yield return Ticks(10);
            Assert.AreEqual(p, platform.GetComponent<Rigidbody2D>().position);
        }
        [UnityTest] public IEnumerator HorizontalRiderStaysGroundedWithoutParenting()
        {
            Platform(new Vector2(10,0)); yield return Rider(); var parent = actor.transform.parent;
            float offset = body.position.x-platform.GetComponent<Rigidbody2D>().position.x; platform.speed = 2;
            yield return Ticks(50);
            Assert.IsTrue(actor.IsGrounded); Assert.AreEqual(offset, body.position.x-platform.GetComponent<Rigidbody2D>().position.x, .06f);
            Assert.AreSame(parent, actor.transform.parent); Assert.AreEqual(5, RunDashBank.Instance.RemainingCharges);
        }
        [UnityTest] public IEnumerator VerticalRiderStaysGroundedAscendingAndDescending()
        {
            Platform(new Vector2(0,2)); yield return Rider(); float offset = body.position.y-platform.GetComponent<Rigidbody2D>().position.y; platform.speed = 2;
            for (int i = 0; i < 120; i++)
            {
                yield return new WaitForFixedUpdate(); Assert.IsTrue(actor.IsGrounded, "Tick " + i);
                Assert.AreEqual(offset, body.position.y-platform.GetComponent<Rigidbody2D>().position.y, .06f);
            }
        }
        [UnityTest] public IEnumerator RiderWalksRelativeToPlatformAndLosesTransportAfterLeaving()
        {
            Platform(new Vector2(20,0)); yield return Rider(); platform.speed = 2; actor.SetHorizontalInput(1);
            float offset = body.position.x-platform.GetComponent<Rigidbody2D>().position.x;
            yield return Ticks(8); Assert.Greater(body.position.x-platform.GetComponent<Rigidbody2D>().position.x, offset+.7f);
            yield return Ticks(30); Assert.IsFalse(actor.IsGrounded); Assert.AreEqual(actor.MoveSpeed, body.linearVelocity.x, .01f);
            actor.SetHorizontalInput(0); yield return Ticks(2); Assert.AreEqual(0, body.linearVelocity.x, .001f);
        }
        [UnityTest] public IEnumerator JumpAndDashLeaveSupportWithAcceptedRules()
        {
            Platform(new Vector2(10,0)); yield return Rider(); platform.speed = 2; yield return Ticks(5);
            actor.RequestJump(); yield return new WaitForFixedUpdate();
            Assert.IsFalse(actor.IsGrounded); Assert.AreEqual(actor.JumpSpeed, body.linearVelocity.y, .7f); Assert.AreEqual(0, body.linearVelocity.x, .01f);
            actor.RequestDash(Vector2.up); yield return new WaitForFixedUpdate(); Assert.IsTrue(actor.IsDashing);
            Assert.AreEqual(4, RunDashBank.Instance.RemainingCharges); Assert.AreEqual(0, body.linearVelocity.x, .01f);
        }
        [UnityTest] public IEnumerator PauseAndFailureStopMotion()
        {
            Platform(new Vector2(10,0)); yield return Ticks(10); Time.timeScale = 0;
            Vector2 p = platform.GetComponent<Rigidbody2D>().position; yield return new WaitForSecondsRealtime(.1f);
            Assert.AreEqual(p, platform.GetComponent<Rigidbody2D>().position); Time.timeScale = 1;
            RunDashBank.Instance.TryFailEnvironment(); yield return Ticks(5);
            Assert.AreEqual(p, platform.GetComponent<Rigidbody2D>().position);
        }
    }
}
#endif
