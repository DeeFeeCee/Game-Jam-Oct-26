#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using GameJam.Levels;
using GameJam.Mechanics;
using GameJam.Movement;
using GameJam.Resources;
using NUnit.Framework;
using UnityEditor;
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

        private LevelSetup SpawnLevelPlayer(Vector2 position)
        {
            var root = Own(new GameObject("Level setup"));
            root.SetActive(false);
            root.transform.position = position;
            var level = root.AddComponent<LevelSetup>();
            level.spawn = root.transform;
            level.playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/Player.prefab");
            level.bankPrefab = AssetDatabase.LoadAssetAtPath<RunDashBank>("Assets/Game/Prefabs/RunDashBank.prefab");
            root.SetActive(true);
            Own(level.Player);
            level.Player.GetComponent<PlayerMovementInput2D>().enabled = false;
            level.Player.GetComponent<Rigidbody2D>().gravityScale = 0;
            return level;
        }

        private Turret2D TargetingTurret(Vector2 muzzlePosition, float interval = 5)
        {
            var turret = Turret(.1f, interval);
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(turret.transform);
            muzzle.position = muzzlePosition;
            turret.muzzle = muzzle;
            turret.aimAtPlayer = true;
            return turret;
        }

        private static Rigidbody2D ShotBody()
        {
            return Object.FindObjectsByType<TurretProjectile2D>(FindObjectsSortMode.None)
                .Single().GetComponent<Rigidbody2D>();
        }

        private static void AssertHeading(Vector2 displacement, Vector2 expected)
        {
            Assert.That(displacement.sqrMagnitude, Is.GreaterThan(.0001f));
            Assert.That(Vector2.Angle(displacement, expected), Is.LessThan(.5f));
        }

        [UnityTest]
        public IEnumerator MirroredBarrelTracksAroundItsMountWithAFixedMuzzle()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<Turret2D>("Assets/Game/Prefabs/Turret.prefab");
            var turret = Own(Object.Instantiate(prefab.gameObject)).GetComponent<Turret2D>();
            turret.SetFiring(false);
            var art = turret.transform.Find("Environment turret art");
            var barrel = art.Find("Barrel");
            var mount = art.Find("Mount");
            art.localScale = new Vector3(-1, 1, 1);
            var pivot = new Vector3(mount.localPosition.x, barrel.localPosition.y, barrel.localPosition.z);
            float radius = Vector3.Distance(barrel.localPosition, pivot);
            Vector3 mountPosition = mount.position;
            Vector3 muzzlePosition = turret.muzzle.position;
            Quaternion rest = barrel.localRotation;
            var player = SpawnLevelPlayer(new Vector2(5, 3)).Player.GetComponent<Rigidbody2D>();

            yield return null;
            Assert.AreEqual(rest, barrel.localRotation, "Fixed turret keeps its authored pose.");
            turret.aimAtPlayer = true;
            foreach (var target in new[] { new Vector2(5, 3), new Vector2(-5, 6), new Vector2(-5, -4) })
            {
                player.position = target;
                yield return null;
                yield return null;
                AssertHeading(barrel.TransformVector(Vector3.left), target - (Vector2)muzzlePosition);
                Assert.That(Vector3.Distance(barrel.localPosition, pivot), Is.EqualTo(radius).Within(.001f));
                Assert.AreEqual(mountPosition, mount.position);
                Assert.AreEqual(muzzlePosition, turret.muzzle.position);
            }

            var lastPose = barrel.localRotation;
            Time.timeScale = 0;
            player.position = new Vector2(6, 6);
            yield return new WaitForSecondsRealtime(.05f);
            Assert.AreEqual(lastPose, barrel.localRotation, "The barrel stays still during Pause.");

            Time.timeScale = 1;
            yield return null;
            yield return null;
            AssertHeading(barrel.TransformVector(Vector3.left), player.position - (Vector2)muzzlePosition);
            lastPose = barrel.localRotation;
            player.gameObject.SetActive(false);
            yield return null;
            yield return null;
            Assert.AreEqual(lastPose, barrel.localRotation, "The barrel holds its last pose without a player.");

            player.gameObject.SetActive(true);
            Assert.IsTrue(Bank.TryFailEnvironment());
            player.position = new Vector2(-6, -3);
            yield return null;
            yield return null;
            Assert.AreEqual(lastPose, barrel.localRotation, "Game Over freezes tracking.");
        }

        [UnityTest] public IEnumerator FixedModeIgnoresTheLevelPlayerAndKeepsItsDefaultOrigin()
        {
            SpawnLevelPlayer(new Vector2(-8, 10));
            var turret = Turret(.1f, 5);
            Assert.IsFalse(turret.aimAtPlayer);
            yield return Ticks(8);

            Assert.AreEqual(1, turret.ShotsFired);
            AssertHeading(ShotBody().position - new Vector2(.6f, 0), Vector2.right);
        }

        [UnityTest] public IEnumerator TargetingUsesTheMuzzleAndTheLevelOwnedPlayer()
        {
            var muzzle = new Vector2(3, 2);
            var target = new Vector2(8, 12);
            SpawnLevelPlayer(target);
            Player(new Vector2(-3, 4)); // Bank-linked fixture with no owning LevelSetup.
            TargetingTurret(muzzle);
            yield return Ticks(8);

            AssertHeading(ShotBody().position - muzzle, target - muzzle);
        }

        [UnityTest] public IEnumerator NewShotRetargetsWhileTheFirstShotKeepsItsHeading()
        {
            var firstTarget = new Vector2(8, 10);
            var secondTarget = new Vector2(-8, 10);
            var player = SpawnLevelPlayer(firstTarget).Player.GetComponent<Rigidbody2D>();
            var turret = TargetingTurret(Vector2.zero, .45f);
            yield return Ticks(8);
            var first = ShotBody();
            var before = first.position;
            player.position = secondTarget;

            yield return Ticks(25);

            Assert.AreEqual(2, turret.ShotsFired);
            var second = Object.FindObjectsByType<TurretProjectile2D>(FindObjectsSortMode.None)
                .Select(shot => shot.GetComponent<Rigidbody2D>()).Single(body => body != first);
            AssertHeading(first.position - before, firstTarget);
            AssertHeading(second.position, secondTarget);
        }

        [UnityTest] public IEnumerator MissingLevelPlayerFallsBackToFixedAim()
        {
            var turret = Turret(.1f, 5);
            turret.aimAtPlayer = true;
            yield return Ticks(8);
            AssertHeading(ShotBody().position - new Vector2(.6f, 0), Vector2.right);
        }

        [UnityTest] public IEnumerator InactiveLevelPlayerIsNotTargeted()
        {
            SpawnLevelPlayer(new Vector2(-8, 10)).Player.SetActive(false);
            TargetingTurret(Vector2.zero);
            yield return Ticks(8);
            AssertHeading(ShotBody().position, Vector2.right);
        }

        [UnityTest] public IEnumerator NonSimulatingLevelPlayerIsNotTargeted()
        {
            var player = SpawnLevelPlayer(new Vector2(-8, 10)).Player.GetComponent<Rigidbody2D>();
            player.simulated = false;
            TargetingTurret(Vector2.zero);
            yield return Ticks(8);
            AssertHeading(ShotBody().position, Vector2.right);
        }

        [UnityTest] public IEnumerator PlayerAtMuzzleFallsBackToFixedAim()
        {
            var player = SpawnLevelPlayer(Vector2.zero).Player;
            player.GetComponent<Collider2D>().enabled = false;
            TargetingTurret(Vector2.zero);
            yield return Ticks(8);
            AssertHeading(ShotBody().position, Vector2.right);
        }

        [UnityTest] public IEnumerator LevelPlayerInAnotherLoadedSceneIsNotTargeted()
        {
            var other = SceneManager.CreateScene("Other turret level");
            var level = SpawnLevelPlayer(new Vector2(-8, 10));
            SceneManager.MoveGameObjectToScene(level.gameObject, other);
            SceneManager.MoveGameObjectToScene(level.Player, other);
            var turret = TargetingTurret(Vector2.zero);
            yield return Ticks(8);
            var shot = Object.FindFirstObjectByType<TurretProjectile2D>();
            yield return SceneManager.UnloadSceneAsync(other);

            Assert.AreEqual(1, turret.ShotsFired);
            Assert.IsNotNull(shot);
            AssertHeading(shot.GetComponent<Rigidbody2D>().position, Vector2.right);
        }

        [UnityTest] public IEnumerator TargetingStillRespectsPauseAndGameOver()
        {
            SpawnLevelPlayer(new Vector2(-8, 10));
            var turret = TargetingTurret(Vector2.zero, .25f);
            yield return Ticks(8);
            Assert.AreEqual(1, turret.ShotsFired);

            Time.timeScale = 0;
            yield return new WaitForSecondsRealtime(.35f);
            Assert.AreEqual(1, turret.ShotsFired);
            Time.timeScale = 1;
            Bank.TryFailEnvironment();
            yield return Ticks(20);

            Assert.AreEqual(1, turret.ShotsFired);
            Assert.IsNull(Object.FindFirstObjectByType<TurretProjectile2D>());
        }

        [UnityTest] public IEnumerator TargetingStillRespectsFiringActivation()
        {
            var target = new Vector2(-8, 10);
            SpawnLevelPlayer(target);
            var turret = TargetingTurret(Vector2.zero, .25f);
            turret.SetFiring(false);
            yield return Ticks(10);
            Assert.AreEqual(0, turret.ShotsFired);

            turret.SetFiring(true);
            yield return Ticks(8);
            Assert.AreEqual(1, turret.ShotsFired);
            AssertHeading(ShotBody().position, target);

            turret.SetFiring(false);
            yield return Ticks(20);
            Assert.AreEqual(1, turret.ShotsFired);
        }

        [UnityTest] public IEnumerator SolidTerrainBlocksAnAimedShot()
        {
            SpawnLevelPlayer(new Vector2(-4, 2));
            Solid(new Vector2(-2, 2), new Vector2(.25f, 2));
            var turret = TargetingTurret(new Vector2(0, 2));
            turret.projectileSpeed = 1000;
            yield return Ticks(14);

            Assert.AreEqual(1, turret.ShotsFired);
            Assert.IsFalse(Bank.IsGameOver);
            Assert.IsNull(Object.FindFirstObjectByType<TurretProjectile2D>());
        }

        [UnityTest] public IEnumerator AimedHitCausesEnvironmentalFailureWithoutSpending()
        {
            SpawnLevelPlayer(new Vector2(-4, 2));
            var turret = TargetingTurret(new Vector2(0, 2));
            turret.projectileSpeed = 1000;
            yield return Ticks(14);

            Assert.AreEqual(1, turret.ShotsFired);
            Assert.IsTrue(Bank.IsGameOver);
            Assert.AreEqual(RunFailureCause.Environmental, Bank.FailureCause);
            Assert.AreEqual(5, Bank.RemainingCharges);
        }
    }
}
#endif
