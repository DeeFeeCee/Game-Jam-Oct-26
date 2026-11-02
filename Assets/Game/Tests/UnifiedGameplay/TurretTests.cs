#if UNITY_EDITOR
using System.Collections;
using GameJam.Mechanics;
using GameJam.Resources;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GameJam.UnifiedGameplay.Tests
{
    public sealed class TurretTests : MechanicsTestFixture
    {
        private TurretProjectile2D Projectile(Vector2 position, Vector2 direction, float speed, float life = 5)
        {
            var obj = Own(new GameObject("Projectile", typeof(Rigidbody2D), typeof(CircleCollider2D)));
            obj.transform.position = position; obj.GetComponent<CircleCollider2D>().radius = .1f;
            var shot = obj.AddComponent<TurretProjectile2D>(); shot.lifetime = life; shot.Launch(direction, speed); return shot;
        }
        private Turret2D Turret(float delay = .1f, float interval = .2f)
        {
            var prototype = Projectile(new Vector2(100,100), Vector2.right, 0); prototype.gameObject.SetActive(false);
            var obj = Own(new GameObject("Turret")); obj.SetActive(false);
            var turret = obj.AddComponent<Turret2D>(); turret.projectilePrefab = prototype; turret.initialDelay = delay;
            turret.shotInterval = interval; turret.firingDirection = Vector2.right; obj.SetActive(true);
            return turret;
        }
        [UnityTest] public IEnumerator ProjectileMovesInConfiguredDirection()
        {
            var shot = Projectile(new Vector2(0,3), new Vector2(1,1), 5);
            yield return Ticks(10); Vector2 p = shot.GetComponent<Rigidbody2D>().position;
            Assert.Greater(p.x, .5f); Assert.AreEqual(p.x, p.y-3, .02f);
        }
        [UnityTest] public IEnumerator SweptProjectileHitsPlayerWithoutSpending()
        {
            Player(new Vector2(3,2)); var shot = Projectile(new Vector2(0,2), Vector2.right, 1000);
            int count = 0; Bank.GameOver += () => count++;
            yield return Ticks(4);
            Assert.IsTrue(Bank.IsGameOver); Assert.AreEqual(RunFailureCause.Environmental, Bank.FailureCause);
            Assert.AreEqual(5, Bank.RemainingCharges); Assert.AreEqual(1, count); Assert.IsTrue(shot == null || shot.IsConsumed);
        }
        [UnityTest] public IEnumerator FirstSolidProtectsPlayerBehindIt()
        {
            Solid(new Vector2(2,2), new Vector2(.2f,4)); Player(new Vector2(3,2));
            var shot = Projectile(new Vector2(0,2), Vector2.right, 1000); yield return Ticks(4);
            Assert.IsFalse(Bank.IsGameOver); Assert.IsTrue(shot == null || shot.IsConsumed);
        }
        [UnityTest] public IEnumerator ProjectileLifetimeAndPauseUseScaledPhysicsTime()
        {
            var shot = Projectile(new Vector2(0,3), Vector2.right, 0, .1f); Time.timeScale = 0;
            yield return new WaitForSecondsRealtime(.15f); Assert.IsFalse(shot.IsConsumed); Time.timeScale = 1;
            yield return Ticks(8); Assert.IsTrue(shot == null || shot.IsConsumed);
        }
        [UnityTest] public IEnumerator TurretTimingPauseAndGameOverSuppressNewShots()
        {
            var turret = Turret(.1f,.2f); yield return Ticks(3); Assert.AreEqual(0,turret.ShotsFired);
            yield return Ticks(4); Assert.AreEqual(1,turret.ShotsFired); Time.timeScale = 0;
            var active = Object.FindObjectsByType<TurretProjectile2D>(FindObjectsSortMode.None);
            Assert.AreEqual(1,active.Length); Assert.Greater(active[0].GetComponent<Rigidbody2D>().position.x,.6f);
            yield return new WaitForSecondsRealtime(.2f); Assert.AreEqual(1,turret.ShotsFired); Time.timeScale = 1;
            yield return Ticks(1); Assert.AreEqual(1,turret.ShotsFired);
            Bank.TryFailEnvironment(); yield return Ticks(30); Assert.AreEqual(1,turret.ShotsFired);
            foreach (var shot in Object.FindObjectsByType<TurretProjectile2D>(FindObjectsSortMode.None)) if (shot.gameObject.activeSelf) Assert.Fail("Live projectile survived game over.");
        }
        [UnityTest] public IEnumerator OptionalActivationAndFreshInstanceReset()
        {
            var turret = Turret(0,.1f); turret.SetFiring(false); yield return Ticks(10); Assert.AreEqual(0,turret.ShotsFired);
            turret.SetFiring(true); yield return Ticks(1); Assert.AreEqual(1,turret.ShotsFired);
            var fresh = Turret(.2f,.1f); Assert.AreEqual(0,fresh.ShotsFired); yield return Ticks(2); Assert.AreEqual(0,fresh.ShotsFired);
        }
        [UnityTest] public IEnumerator OwnerSceneUnloadCleansProjectiles()
        {
            var original = SceneManager.GetActiveScene(); var extra = SceneManager.CreateScene("Turret cleanup fixture");
            SceneManager.SetActiveScene(extra); var shot = Projectile(new Vector2(0,3),Vector2.right,2);
            SceneManager.SetActiveScene(original); yield return SceneManager.UnloadSceneAsync(extra); Assert.IsTrue(shot == null);
        }
    }
}
#endif
