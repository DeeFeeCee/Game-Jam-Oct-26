#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using GameJam.LevelAuthoring.Editor;
using GameJam.Levels;
using GameJam.Movement;
using GameJam.Presentation;
using GameJam.Resources;
using GameJam.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GameJam.LevelAuthoring.Tests
{
    public sealed class ProductionRoomTests
    {
        private const string Destination = "Assets/Game/Scenes/Levels/Level02.unity";
        private PlayerMovement2D actor;

        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1;
            if (RunDashBank.Instance != null) Object.Destroy(RunDashBank.Instance.gameObject);
            var old = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("Production room test cleanup"));
            if (old.IsValid() && old.isLoaded) yield return SceneManager.UnloadSceneAsync(old);
            yield return null; LogAssert.NoUnexpectedReceived();
        }

        private IEnumerator Load(string path = LevelAuthoring32x18.Level01)
        {
            EditorSceneManager.LoadSceneInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; yield return null;
            actor = Object.FindFirstObjectByType<PlayerMovement2D>();
            if (actor != null) actor.GetComponent<PlayerMovementInput2D>().enabled = false;
        }

        [UnityTest] public IEnumerator ProductionCameraHasOfficialContractAndNoFollowOwner()
        {
            yield return Load();
            var camera = Camera.main; var pp = camera.GetComponent<PixelPerfectCamera>();
            Assert.IsTrue(camera.orthographic); Assert.AreEqual(9, camera.orthographicSize);
            Assert.AreEqual(new Vector3(16, 9, -10), camera.transform.position);
            Assert.IsNull(camera.GetComponent<LevelCamera2D>());
            Assert.IsNull(Object.FindFirstObjectByType<LevelSetup>().levelCamera);
            Assert.AreEqual(20, pp.assetsPPU); Assert.AreEqual(640, pp.refResolutionX); Assert.AreEqual(360, pp.refResolutionY);
            Assert.AreEqual(PixelPerfectCamera.GridSnapping.UpscaleRenderTexture, pp.gridSnapping);
            Assert.AreEqual(PixelPerfectCamera.CropFrame.Windowbox, pp.cropFrame);
            Assert.IsFalse(camera.allowHDR || camera.allowMSAA || camera.allowDynamicResolution);
            Assert.IsFalse(camera.GetUniversalAdditionalCameraData().renderPostProcessing);
        }

        [UnityTest] public IEnumerator ProductionCameraRemainsStationaryDuringContinuousMovement()
        {
            yield return Load(); Vector3 position = Camera.main.transform.position;
            float before = actor.transform.position.x;
            for (int i = 0; i < 20; i++) { actor.SetHorizontalInput(1); yield return new WaitForFixedUpdate(); }
            Assert.Greater(actor.transform.position.x, before + 1);
            Assert.AreEqual(position, Camera.main.transform.position);
        }

        [UnityTest] public IEnumerator AllNineSolidsMatchAuthoredBoundsInsideTheRoom()
        {
            yield return Load(); var solids = Solids(); Assert.AreEqual(9, solids.Length);
            foreach (var solid in solids)
            {
                Assert.That(solid.bounds.min.x, Is.InRange(0f, 32f)); Assert.That(solid.bounds.max.x, Is.InRange(0f, 32f));
                Assert.That(solid.bounds.min.y, Is.InRange(0f, 18f)); Assert.That(solid.bounds.max.y, Is.InRange(0f, 18f));
            }
            var authored = new[]
            {
                ("Floor", new Vector2(16, .5f), new Vector2(32, 1)),
                ("Ceiling", new Vector2(16, 17.5f), new Vector2(32, 1)),
                ("Left boundary", new Vector2(.5f, 9), new Vector2(1, 16)),
                ("Right boundary", new Vector2(31.5f, 9), new Vector2(1, 16)),
                ("GridPlatform32x18 (1)", new Vector2(9, 3.5f), new Vector2(4, 1)),
                ("GridPlatform32x18 (3)", new Vector2(15.5f, 6.5f), new Vector2(2, 1)),
                ("GridPlatform32x18 (2)", new Vector2(23.5f, 6.5f), new Vector2(4, 1)),
                ("Landing ledge (1)", new Vector2(29, 9.5f), new Vector2(4, 1)),
                ("Terminal placeholder", new Vector2(9, 5.25f), new Vector2(1, 1.6f))
            };
            foreach (var (name, center, size) in authored)
            {
                var bounds = solids.Single(s => s.name == name).bounds;
                AssertBoundsXY(center, bounds.center, name);
                AssertBoundsXY(size, bounds.size, name);
            }
        }

        [UnityTest] public IEnumerator ForegroundFramesAndArtMatchEverySolid()
        {
            yield return Load();
            foreach (var solid in Solids())
            {
                var art = solid.transform.Find("Floor visuals").GetComponent<SpriteRenderer>();
                var frame = solid.transform.Find("Gameplay surface frame");
                var backing = frame.Find("Backing").GetComponent<SpriteRenderer>();
                var top = frame.Find("Top").GetComponent<SpriteRenderer>();
                // Sprite bounds include renderer depth; only XY bounds participate in 2D collision.
                AssertBoundsXY(solid.bounds.center, art.bounds.center, solid.name);
                AssertBoundsXY(solid.bounds.size, art.bounds.size, solid.name);
                AssertBoundsXY(solid.bounds.center, backing.bounds.center, solid.name);
                AssertBoundsXY(solid.bounds.size, backing.bounds.size, solid.name);
                Assert.AreEqual(.05f * Mathf.Abs(solid.transform.lossyScale.y), top.bounds.size.y, .001f, solid.name);
                Assert.AreEqual(solid.bounds.max.y, top.bounds.max.y, .001f, solid.name);
                Assert.Greater(top.color.grayscale, backing.color.grayscale + .5f);
                Assert.IsEmpty(frame.GetComponentsInChildren<Collider2D>());
            }
        }

        [UnityTest] public IEnumerator PlayerSpawnsOnceAtAssignedTransformWithUnchangedPhysics()
        {
            yield return Load(); var setup = Object.FindFirstObjectByType<LevelSetup>();
            Assert.AreEqual(1, Object.FindObjectsByType<PlayerMovement2D>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(1, Object.FindObjectsByType<RunDashBank>(FindObjectsSortMode.None).Length);
            Assert.AreSame(setup.Player, actor.gameObject); Assert.AreEqual(setup.spawn.position.x, actor.transform.position.x, .001f);
            Assert.AreEqual(new Vector3(3.5f, 1.81f), setup.spawn.position);
            Assert.AreEqual(Vector3.one, actor.transform.localScale);
            Assert.AreEqual(new Vector2(.8f, 1.6f), actor.GetComponent<BoxCollider2D>().size);
            Assert.AreEqual(5.25f, actor.MoveSpeed); Assert.AreEqual(14, actor.JumpSpeed);
            Assert.AreEqual(18, actor.DashSpeed); Assert.AreEqual(.20f, actor.DashDuration, .001f);
            Assert.AreEqual(3, actor.GetComponent<Rigidbody2D>().gravityScale);
            Assert.NotNull(actor.GetComponentInChildren<PlayerPresentation>());
            Assert.AreEqual(5, RunDashBank.Instance.RemainingCharges); Assert.IsFalse(SceneDashSettings.AllowsAirDashChaining());
            for (int i = 0; i < 30 && !actor.IsGrounded; i++) yield return new WaitForFixedUpdate();
            Assert.IsTrue(actor.IsGrounded);
        }

        [UnityTest] public IEnumerator EntireRoomRendersAtFourOutputSizesWithIntegerWindowboxing()
        {
            yield return Load(); var camera = Camera.main;
            foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(2560, 1440), new Vector2Int(1366, 900) })
            {
                var previous = camera.targetTexture;
                var target = new RenderTexture(size.x, size.y, 24) { filterMode = FilterMode.Point };
                try
                {
                    camera.targetTexture = target;
                    RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                    var pp = camera.GetComponent<PixelPerfectCamera>();
                    int scale = Mathf.Min(size.x / 640, size.y / 360);
                    Assert.AreEqual(scale, pp.pixelRatio);
                    Assert.AreEqual(640 * scale, camera.pixelRect.width, .01f); Assert.AreEqual(360 * scale, camera.pixelRect.height, .01f);
                    Assert.AreEqual(new Vector3(0, 0, 0), camera.ViewportToWorldPoint(new Vector3(0, 0, 10)));
                    Assert.AreEqual(new Vector3(32, 18, 0), camera.ViewportToWorldPoint(new Vector3(1, 1, 10)));
                }
                finally { camera.targetTexture = previous; target.Release(); Object.Destroy(target); }
                yield return null;
            }
        }

        [UnityTest] public IEnumerator ActualRouteJumpsDashesAndEntersExitWithPersistentBalance() => Traverse(true);

        [UnityTest] public IEnumerator ActualRouteCanReachLevel02WithoutSpendingAnyDash() => Traverse(false);

        private IEnumerator Traverse(bool spendDash)
        {
            yield return Load();
            var bank = RunDashBank.Instance;
            yield return LandAt(1, "floor");
            yield return MoveRightTo(4.9f);
            actor.RequestJump();
            yield return MoveRightTo(7.7f);
            yield return LandAt(4, "first platform");
            actor.RequestJump();
            yield return MoveRightTo(9.0f);
            yield return LandAt(6.05f, "terminal top");
            yield return MoveRightTo(9.72f);
            Assert.IsTrue(actor.IsGrounded, "The terminal edge must still support the jump.");
            actor.RequestJump();
            yield return MoveRightTo(14.7f);
            yield return LandAt(7, "middle platform");
            yield return MoveRightTo(16.68f);
            Assert.IsTrue(actor.IsGrounded, "The middle platform edge must still support the jump.");
            actor.RequestJump();
            yield return MoveRightTo(22.4f);
            yield return LandAt(7, "far platform");
            yield return MoveRightTo(24.8f);
            actor.RequestJump();
            yield return MoveRightTo(26.05f);
            if (spendDash)
            {
                actor.RequestDash(Vector2.up);
                yield return new WaitForFixedUpdate();
                Assert.IsTrue(actor.IsDashing, "The ledge approach must execute a real upward dash.");
            }
            actor.SetHorizontalInput(1);
            yield return AwaitScene(Destination);
            Assert.AreSame(bank, RunDashBank.Instance);
            Assert.AreEqual(spendDash ? 4 : 5, bank.RemainingCharges);
            Assert.IsFalse(bank.IsGameOver);
        }

        [UnityTest] public IEnumerator ExitLoadsOnceAndPreservesBankAcrossScenes()
        {
            yield return Load(); var bank = RunDashBank.Instance; bank.TrySpendDash();
            var transition = Object.FindFirstObjectByType<LevelTransition>();
            Assert.IsTrue(transition.TryExit(actor)); Assert.IsFalse(transition.TryExit(actor));
            yield return AwaitScene(Destination);
            Assert.AreSame(bank, RunDashBank.Instance); Assert.AreEqual(4, bank.RemainingCharges);
            Assert.AreEqual(1, Object.FindObjectsByType<RunDashBank>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(1, Object.FindObjectsByType<PlayerMovement2D>(FindObjectsSortMode.None).Length);
            Assert.IsFalse(SceneDashSettings.AllowsAirDashChaining());
        }

        [UnityTest] public IEnumerator FinalChargeHasNoTravelAndCannotCompleteProductionExit()
        {
            yield return Load(); var bank = RunDashBank.Instance;
            while (bank.RemainingCharges > 1) bank.TrySpendDash();
            var body = actor.GetComponent<Rigidbody2D>(); Vector2 position = body.position;
            actor.RequestDash(Vector2.up); yield return new WaitForFixedUpdate();
            Assert.IsTrue(bank.IsGameOver); Assert.AreEqual(0, bank.RemainingCharges);
            Assert.AreEqual(position, body.position); Assert.IsFalse(body.simulated || actor.IsDashing || actor.enabled);
            Assert.IsFalse(Object.FindFirstObjectByType<LevelTransition>().TryExit(actor));
        }

        [UnityTest] public IEnumerator MainMenuNewGameLoadsConvertedLevelAndFreshBank()
        {
            yield return Load("Assets/Game/Scenes/MainMenu.unity");
            Object.FindFirstObjectByType<MainMenuView>().newGameButton.onClick.Invoke();
            yield return AwaitScene(NarrativePrologue.ScenePath);
            var intro = Object.FindFirstObjectByType<NarrativePrologue>();
            yield return new WaitUntil(() => intro.InputArmed, System.TimeSpan.FromSeconds(6),
                () => Assert.Fail("Prologue did not accept input."));
            Assert.IsTrue(intro.TrySkip());
            yield return AwaitScene(LevelAuthoring32x18.Level01);
            Assert.NotNull(Object.FindFirstObjectByType<RoomGrid32x18>());
            Assert.AreEqual(new Vector3(16, 9, -10), Camera.main.transform.position);
            Assert.AreEqual(5, RunDashBank.Instance.RemainingCharges);
        }

        [UnityTest] public IEnumerator MidDashPauseResumePreservesPhysicsAndBalance()
        {
            yield return Load(); actor.RequestDash(Vector2.up); yield return new WaitForFixedUpdate();
            Assert.IsTrue(actor.IsDashing); var bank = RunDashBank.Instance;
            var pause = Object.FindFirstObjectByType<PauseController>(); pause.SendMessage("OnApplicationFocus", true);
            Assert.IsTrue(pause.TryPause()); var body = actor.GetComponent<Rigidbody2D>(); Vector2 position = body.position;
            yield return new WaitForSecondsRealtime(.1f);
            Assert.AreEqual(position, body.position); Assert.IsTrue(actor.IsDashing); Assert.AreEqual(4, bank.RemainingCharges);
            Assert.IsTrue(pause.TryResume());
            for (int i = 0; i < 20 && actor.IsDashing; i++) yield return new WaitForFixedUpdate();
            Assert.IsFalse(actor.IsDashing); Assert.AreEqual(3, body.gravityScale); Assert.AreEqual(4, bank.RemainingCharges);
        }

        [UnityTest] public IEnumerator HudTracksAnActualProductionDash()
        {
            yield return Load(); var hud = Object.FindFirstObjectByType<MobilityReserveHUDController>();
            Assert.AreEqual(5, hud.DisplayedCharges);
            actor.RequestDash(Vector2.up); yield return new WaitForFixedUpdate(); yield return null;
            Assert.AreEqual(4, hud.DisplayedCharges); Assert.AreEqual("04", hud.counter.text);
            Assert.AreSame(RunDashBank.Instance, hud.BoundBank);
        }

        [UnityTest] public IEnumerator GameOverRestartReturnsToConvertedLevelWithOnePlayerAndFreshRun()
        {
            yield return Load(); var bank = RunDashBank.Instance;
            while (bank.RemainingCharges > 1) bank.TrySpendDash();
            actor.RequestDash(Vector2.up); yield return new WaitForFixedUpdate(); yield return null; yield return null;
            var failure = Object.FindFirstObjectByType<GameOverController>();
            Assert.IsTrue(failure.IsVisible); Assert.IsTrue(failure.InputArmed);
            int handle = SceneManager.GetActiveScene().handle;
            failure.restartButton.onClick.Invoke();
            for (int i = 0; i < 300 && SceneManager.GetActiveScene().handle == handle; i++) yield return null;
            yield return null; yield return null;
            Assert.AreNotEqual(handle, SceneManager.GetActiveScene().handle);
            Assert.AreEqual(LevelAuthoring32x18.Level01, SceneManager.GetActiveScene().path);
            Assert.AreEqual(5, bank.RemainingCharges); Assert.IsFalse(bank.IsGameOver);
            Assert.AreEqual(1, Object.FindObjectsByType<PlayerMovement2D>(FindObjectsSortMode.None).Length);
            Assert.NotNull(Object.FindFirstObjectByType<RoomGrid32x18>());
        }

        [UnityTest] public IEnumerator CanonicalTemplateHasOnlyPerimeterAndUnconfiguredExit()
        {
            yield return Load(LevelAuthoring32x18.Template);
            Assert.AreEqual(4, Solids().Length); Assert.AreEqual(1, Object.FindObjectsByType<LevelSetup>(FindObjectsSortMode.None).Length);
            Assert.AreEqual("", Object.FindFirstObjectByType<LevelTransition>().destinationScene);
            Assert.NotNull(Object.FindFirstObjectByType<LevelExit>().transition);
            Assert.IsFalse(SceneDashSettings.AllowsAirDashChaining()); Assert.AreEqual(5, RunDashBank.Instance.RemainingCharges);
            Assert.AreEqual(new Vector3(16, 9, -10), Camera.main.transform.position);
        }

        [UnityTest] public IEnumerator ExampleCopyHasIndependentIdentityAndAcceptedPrefabReferences()
        {
            yield return Load(LevelAuthoring32x18.Example);
            Assert.AreNotEqual(AssetDatabase.AssetPathToGUID(LevelAuthoring32x18.Template), AssetDatabase.AssetPathToGUID(LevelAuthoring32x18.Example));
            var setup = Object.FindFirstObjectByType<LevelSetup>();
            Assert.AreEqual("Assets/Game/Prefabs/Player.prefab", AssetDatabase.GetAssetPath(setup.playerPrefab));
            Assert.AreEqual("Assets/Game/Prefabs/RunDashBank.prefab", AssetDatabase.GetAssetPath(setup.bankPrefab));
            Assert.AreEqual(4, Solids().Length);
        }

        [UnityTest] public IEnumerator RepeatedSceneEntryPreservesExactlyOneBankAndUiOwners()
        {
            yield return Load(); var bank = RunDashBank.Instance; bank.TrySpendDash();
            for (int i = 0; i < 3; i++)
            {
                yield return Load();
                Assert.AreSame(bank, RunDashBank.Instance); Assert.AreEqual(4, bank.RemainingCharges);
                Assert.AreEqual(1, Object.FindObjectsByType<RunDashBank>(FindObjectsSortMode.None).Length);
                Assert.AreEqual(1, Object.FindObjectsByType<PlayerMovement2D>(FindObjectsSortMode.None).Length);
                Assert.AreEqual(1, Object.FindObjectsByType<MobilityReserveHUDController>(FindObjectsSortMode.None).Length);
                Assert.AreEqual(1, Object.FindObjectsByType<PauseController>(FindObjectsSortMode.None).Length);
                Assert.AreEqual(1, Object.FindObjectsByType<GameOverController>(FindObjectsSortMode.None).Length);
            }
        }

        private static BoxCollider2D[] Solids() => Object.FindObjectsByType<BoxCollider2D>(FindObjectsSortMode.None)
            .Where(c => !c.isTrigger && c.attachedRigidbody == null).ToArray();

        private static void AssertBoundsXY(Vector2 expected, Vector2 actual, string name)
        {
            Assert.AreEqual(expected.x, actual.x, .001f, name + " X");
            Assert.AreEqual(expected.y, actual.y, .001f, name + " Y");
        }

        private IEnumerator MoveRightTo(float x)
        {
            var body = actor.GetComponent<Rigidbody2D>();
            actor.SetHorizontalInput(1);
            for (int tick = 0; tick < 100 && body.position.x < x; tick++)
                yield return new WaitForFixedUpdate();
            actor.SetHorizontalInput(0);
            Assert.GreaterOrEqual(body.position.x, x, $"Expected to reach X={x}. Stopped at {body.position}.");
        }

        private IEnumerator LandAt(float feetY, string surface)
        {
            var box = actor.GetComponent<BoxCollider2D>();
            for (int tick = 0; tick < 100; tick++)
            {
                if (actor.IsGrounded && Mathf.Abs(box.bounds.min.y - feetY) < .07f)
                    yield break;
                yield return new WaitForFixedUpdate();
            }
            Assert.Fail($"Expected to land on {surface} at Y={feetY}. Feet ended at {box.bounds.min}, grounded={actor.IsGrounded}.");
        }

        private static IEnumerator AwaitScene(string path)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 6;
            while (SceneManager.GetActiveScene().path != path && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.AreEqual(path, SceneManager.GetActiveScene().path); yield return null; yield return null;
        }
    }
}
#endif
