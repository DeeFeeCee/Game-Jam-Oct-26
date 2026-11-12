#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using GameJam.LevelAuthoring.Editor;
using GameJam.Levels;
using GameJam.Mechanics;
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
        private const string Level05 = "Assets/Game/Scenes/Levels/Level05.unity";
        private const string Level11 = "Assets/Game/Scenes/Levels/Level11.unity";
        private const string Level12 = "Assets/Game/Scenes/Levels/Level12.unity";

        private static readonly (string Source, string Destination, EnvironmentZone Zone)[] CampaignRoute =
        {
            ("Level01", "Level02", EnvironmentZone.Blue),
            ("Level02", "Level03", EnvironmentZone.Blue),
            ("Level03", "Level04", EnvironmentZone.Blue),
            ("Level04", "Level05", EnvironmentZone.Blue),
            ("Level05", "Level06", EnvironmentZone.YellowGreen),
            ("Level06", "Level07", EnvironmentZone.YellowGreen),
            ("Level07", "Level08", EnvironmentZone.YellowGreen),
            ("Level08", "Level09", EnvironmentZone.YellowGreen),
            ("Level09", "Level10", EnvironmentZone.Orange),
            ("Level10", "Level11", EnvironmentZone.Orange),
            ("Level11", "Level12", EnvironmentZone.Orange),
            ("Level12", "EndingSequence", EnvironmentZone.Orange)
        };

        [UnityTest]
        public IEnumerator RegisteredCampaignExitsKeepOneBankAndRecordEveryEntry()
        {
            const string levels = "Assets/Game/Scenes/Levels/";
            const string ending = "Assets/Game/Scenes/Ending/EndingSequence.unity";
            var registered = EditorBuildSettings.scenes.Where(scene => scene.enabled &&
                scene.path.StartsWith(levels, System.StringComparison.Ordinal)).Select(scene => scene.path);
            CollectionAssert.AreEqual(CampaignRoute.Select(step => levels + step.Source + ".unity"), registered);
            yield return Load();

            var bank = RunDashBank.Instance;
            Assert.AreEqual(5, bank.RemainingCharges);
            foreach (var step in CampaignRoute)
            {
                string source = levels + step.Source + ".unity";
                string destination = step.Destination == "EndingSequence" ? ending : levels + step.Destination + ".unity";
                Assert.AreEqual(source, SceneManager.GetActiveScene().path);
                Assert.IsNotEmpty(AssetDatabase.AssetPathToGUID(source));
                Assert.AreSame(bank, Object.FindObjectsByType<RunDashBank>(FindObjectsSortMode.None).Single());
                var setup = Object.FindObjectsByType<LevelSetup>(FindObjectsSortMode.None).Single();
                Assert.IsNotNull(setup.spawn);
                Assert.IsNotNull(setup.playerPrefab);
                Assert.IsNotNull(setup.bankPrefab);
                Assert.AreSame(setup.Player, Object.FindObjectsByType<PlayerMovement2D>(FindObjectsSortMode.None).Single().gameObject);
                Assert.AreEqual(step.Zone, Object.FindObjectsByType<EnvironmentTheme>(FindObjectsSortMode.None).Single().Zone);
                Assert.AreEqual(source, bank.CheckpointScenePath);
                Assert.AreEqual(bank.RemainingCharges, bank.CheckpointCharges);

                // Spend at representative levels. Exit coverage uses placement, not a solved route.
                if (step.Source is "Level03" or "Level06" or "Level10")
                    Assert.IsTrue(bank.TrySpendDash());
                int balance = bank.RemainingCharges;
                var exit = Object.FindObjectsByType<LevelExit>(FindObjectsSortMode.None).Single();
                Assert.IsNotNull(exit.transition);
                Assert.AreEqual(destination, exit.transition.destinationScene);
                var trigger = exit.GetComponent<Collider2D>();
                Assert.IsTrue(trigger.isTrigger);

                var body = setup.Player.GetComponent<Rigidbody2D>();
                body.linearVelocity = Vector2.zero;
                body.position = trigger.bounds.center;
                Physics2D.SyncTransforms();
                yield return AwaitScene(destination);
                yield return null;
                Assert.AreSame(bank, RunDashBank.Instance);
                Assert.AreEqual(balance, bank.RemainingCharges);
            }
            Assert.AreEqual(levels + "Level12.unity", bank.CheckpointScenePath);
            Assert.IsNotNull(Object.FindFirstObjectByType<EndingSequence>());
        }

        [UnityTest]
        public IEnumerator Level05FloorRouteRequiresTwoApprovedWallDashesAndReachesLevel06()
        {
            yield return Load(Level05);
            var bank = RunDashBank.Instance;
            var walls = Object.FindObjectsByType<BreakableWall2D>(FindObjectsSortMode.None)
                .OrderBy(wall => wall.transform.position.x).ToArray();
            Assert.AreEqual(2, walls.Length);
            var floor = Solids().Single(solid => solid.name == "Floor");
            var floorBounds = floor.bounds;
            var body = actor.GetComponent<Rigidbody2D>();
            yield return MoveRightTo(13f);
            yield return MoveRightTo(14.1f);

            actor.SetHorizontalInput(1f);
            for (int tick = 0; tick < 15; tick++) yield return new WaitForFixedUpdate();
            Assert.IsFalse(walls[0].IsBroken);
            Assert.Less(body.position.x, 15f);
            actor.RequestJump();
            for (int tick = 0; tick < 15; tick++) yield return new WaitForFixedUpdate();
            Assert.Greater(body.position.y, 2.5f);
            Assert.IsFalse(walls[0].IsBroken);
            actor.SetHorizontalInput(0f);
            yield return LandAt(1f, "Level05 floor");

            for (int i = 0; i < walls.Length; i++)
            {
                var wall = walls[i];
                float left = wall.GetComponent<Collider2D>().bounds.min.x;
                if (i > 0) yield return MoveRightTo(left - .6f);
                actor.RequestDash(Vector2.right);
                for (int tick = 0; tick < 25 && (wall != null || actor.IsDashing); tick++)
                    yield return new WaitForFixedUpdate();
                Assert.IsTrue(wall == null || wall.IsBroken, "The intended wall should disappear.");
                Assert.AreEqual(4 - i, bank.RemainingCharges);
                Assert.IsTrue(floor.enabled);
                Assert.AreEqual(floorBounds, floor.bounds);
                yield return LandAt(1f, "Level05 floor after breaking a wall");
            }
            actor.SetHorizontalInput(1f);
            yield return AwaitScene("Assets/Game/Scenes/Levels/Level06.unity");
            Assert.AreSame(bank, RunDashBank.Instance);
            Assert.AreEqual(3, bank.CheckpointCharges);
        }

        [UnityTest]
        public IEnumerator Level05IneligibleSecondAirDashCannotBreakTheWallOrSpendAgain()
        {
            yield return Load(Level05);
            yield return MoveRightTo(13f);
            yield return MoveRightTo(14.3f);
            yield return LandAt(1f, "floor beside the first wall");
            var wall = Object.FindObjectsByType<BreakableWall2D>(FindObjectsSortMode.None)
                .OrderBy(item => item.transform.position.x).First();
            var bank = RunDashBank.Instance;
            actor.RequestDash(Vector2.up);
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(actor.IsDashing);
            for (int tick = 0; tick < 20 && actor.IsDashing; tick++) yield return new WaitForFixedUpdate();
            Assert.IsFalse(actor.IsDashing || actor.IsGrounded);
            Assert.AreEqual(4, bank.RemainingCharges);
            actor.RequestDash(Vector2.right);
            yield return new WaitForFixedUpdate();
            Assert.IsFalse(actor.IsDashing || wall.IsBroken);
            Assert.AreEqual(4, bank.RemainingCharges);
        }

        [UnityTest]
        public IEnumerator Level05UpperRoutePreservesBothWallsAndAllFiveCharges()
        {
            yield return Load(Level05);
            var body = actor.GetComponent<Rigidbody2D>();
            var shape = actor.GetComponent<BoxCollider2D>();
            var bank = RunDashBank.Instance;
            actor.SetHorizontalInput(-1f);
            while (body.position.x > 2.6f) yield return new WaitForFixedUpdate();
            actor.SetHorizontalInput(0f);
            yield return LandAt(1f, "Level05 spawn floor");
            foreach (float height in new[] { 4f, 7f, 10f, 13f })
            {
                actor.RequestJump();
                actor.SetHorizontalInput(1f);
                for (int tick = 0; tick < 8 && body.position.x < 2.62f; tick++)
                    yield return new WaitForFixedUpdate();
                actor.SetHorizontalInput(0f);
                for (int tick = 0; tick < 25 && shape.bounds.min.y < height + .01f; tick++)
                    yield return new WaitForFixedUpdate();
                Assert.Greater(shape.bounds.min.y, height, "Rise alongside the ledge at " + height);
                actor.SetHorizontalInput(-1f);
                for (int tick = 0; tick < 10 && body.position.x > 2.22f; tick++)
                    yield return new WaitForFixedUpdate();
                actor.SetHorizontalInput(0f);
                yield return LandAt(height, "Level05 left stair");
            }

            foreach (var (takeoff, target) in new[] { (2.4f, 7.8f), (9.89f, 15.3f), (17.39f, 22.8f) })
            {
                yield return MoveRightTo(takeoff);
                actor.RequestJump();
                yield return MoveRightTo(target);
                yield return LandAt(13f, "Level05 upper route");
            }
            Assert.AreEqual(2, Object.FindObjectsByType<BreakableWall2D>(FindObjectsSortMode.None).Length);
            actor.SetHorizontalInput(1f);
            yield return AwaitScene("Assets/Game/Scenes/Levels/Level06.unity");
            Assert.AreEqual(5, bank.RemainingCharges);
        }

        [UnityTest]
        public IEnumerator Level05FatalDashDoesNotMoveJettOrDestroyTheWall()
        {
            yield return Load(Level05);
            var bank = RunDashBank.Instance;
            for (int i = 0; i < 4; i++) Assert.IsTrue(bank.TrySpendDash());
            yield return MoveRightTo(13f);
            yield return MoveRightTo(14.3f);
            yield return LandAt(1f, "floor beside the first wall");
            var wall = Object.FindObjectsByType<BreakableWall2D>(FindObjectsSortMode.None)
                .OrderBy(item => item.transform.position.x).First();
            var body = actor.GetComponent<Rigidbody2D>();
            var before = body.position;
            actor.RequestDash(Vector2.right);
            yield return new WaitForFixedUpdate();
            Assert.AreEqual(0, bank.RemainingCharges);
            Assert.AreEqual(RunFailureCause.MobilityExhaustion, bank.FailureCause);
            Assert.IsFalse(wall.IsBroken);
            Assert.IsFalse(actor.IsDashing);
            Assert.AreEqual(before, body.position);
        }

        [UnityTest]
        public IEnumerator Level11MovingPlatformsAdvanceAndPitRetryRestoresEntry()
        {
            yield return Load();
            var bank = RunDashBank.Instance;
            Assert.IsTrue(bank.TrySpendDash());
            yield return Load(Level11);
            var platforms = Object.FindObjectsByType<MovingPlatform2D>(FindObjectsSortMode.None);
            Assert.AreEqual(8, platforms.Length);
            var bodies = platforms.Select(item => item.GetComponent<Rigidbody2D>()).ToArray();
            var positions = bodies.Select(body => body.position).ToArray();
            for (int tick = 0; tick < 30; tick++) yield return new WaitForFixedUpdate();
            for (int i = 0; i < platforms.Length; i++)
            {
                Assert.AreNotEqual(platforms[i].EndpointA, platforms[i].EndpointB, platforms[i].name);
                Assert.Greater(Vector2.Distance(positions[i], bodies[i].position), .03f, platforms[i].name);
            }

            Assert.AreEqual(4, bank.CheckpointCharges);
            Assert.IsTrue(bank.TrySpendDash());
            var pit = Object.FindObjectsByType<FallDeathZone2D>(FindObjectsSortMode.None).Single();
            var player = actor.GetComponent<Rigidbody2D>();
            player.position = pit.GetComponent<Collider2D>().bounds.center;
            Physics2D.SyncTransforms();
            yield return new WaitUntil(() => bank.IsGameOver, System.TimeSpan.FromSeconds(4), () => Assert.Fail("Level11 pit was not lethal."));
            Assert.AreEqual(RunFailureCause.Environmental, bank.FailureCause);
            Assert.AreEqual(3, bank.RemainingCharges);
            Assert.IsTrue(bank.TryRetryLevel(SceneManager.GetActiveScene()));
            yield return new WaitUntil(() => !bank.IsGameOver, System.TimeSpan.FromSeconds(5), () => Assert.Fail("Level11 retry did not finish."));
            Assert.AreEqual(Level11, SceneManager.GetActiveScene().path);
            Assert.AreSame(bank, RunDashBank.Instance);
            Assert.AreEqual(4, bank.RemainingCharges);
            var setup = Object.FindFirstObjectByType<LevelSetup>();
            Assert.AreEqual(setup.spawn.position.x, setup.Player.transform.position.x, .001f);
        }

        [UnityTest]
        public IEnumerator Level12SpawnsSafelyAndAuthoredCrusherAndTurretActivate()
        {
            yield return Load(Level12);
            var bank = RunDashBank.Instance;
            var turret = Object.FindObjectsByType<Turret2D>(FindObjectsSortMode.None).Single();
            Assert.IsTrue(turret.aimAtPlayer);
            Assert.AreEqual(2f, turret.initialDelay);
            Assert.AreEqual(.4f, turret.shotInterval);
            Assert.AreEqual(10f, turret.projectileSpeed);
            Assert.AreEqual(8f, turret.projectilePrefab.lifetime);
            Assert.AreEqual(2, Object.FindObjectsByType<MovingPlatform2D>(FindObjectsSortMode.None).Length);
            var zone = Object.FindObjectsByType<CrusherActivationZone2D>(FindObjectsSortMode.None).Single();
            var crusher = zone.crusher;
            Assert.IsNotNull(crusher);
            Assert.AreEqual(CrusherState.Armed, crusher.State);
            yield return LandAt(1f, "Level12 spawn floor");
            yield return new WaitUntil(() => turret.ShotsFired >= 1, System.TimeSpan.FromSeconds(4), () => Assert.Fail("Level12 turret did not fire."));
            Assert.IsFalse(bank.IsGameOver);
            Assert.AreEqual(5, bank.RemainingCharges);

            var player = actor.GetComponent<Rigidbody2D>();
            player.position = zone.GetComponent<Collider2D>().bounds.center;
            player.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
            float before = crusher.GetComponent<Rigidbody2D>().position.y;
            yield return new WaitUntil(() => crusher.State != CrusherState.Armed, System.TimeSpan.FromSeconds(4), () => Assert.Fail("Level12 trigger did not activate the crusher."));
            yield return new WaitUntil(() => bank.IsGameOver || crusher.GetComponent<Rigidbody2D>().position.y < before - .05f,
                System.TimeSpan.FromSeconds(5), () => Assert.Fail("Level12 crusher did not descend."));
            Assert.AreEqual(5, bank.RemainingCharges);
        }

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

        [UnityTest] public IEnumerator OriginalRoomBoundsRemainAndOnlyTheDecorativeTerminalIsDisabled()
        {
            yield return Load(); var solids = Solids(); Assert.AreEqual(9, solids.Length);
            Assert.AreEqual(8, solids.Count(solid => solid.enabled));
            Assert.IsFalse(solids.Single(solid => solid.name == "Terminal placeholder").enabled);
            foreach (var solid in solids)
            {
                var bounds = AuthoredBounds(solid);
                Assert.That(bounds.min.x, Is.InRange(0f, 32f)); Assert.That(bounds.max.x, Is.InRange(0f, 32f));
                Assert.That(bounds.min.y, Is.InRange(0f, 18f)); Assert.That(bounds.max.y, Is.InRange(0f, 18f));
            }
            var authored = new[]
            {
                ("Floor", new Vector2(16, .5f), new Vector2(32, 1)),
                ("Ceiling", new Vector2(16, 17.5f), new Vector2(32, 1)),
                ("Left boundary", new Vector2(.5f, 9), new Vector2(1, 16)),
                ("Right boundary", new Vector2(31.5f, 9), new Vector2(1, 16)),
                ("GridPlatform32x18 (1)", new Vector2(9, 3.75f), new Vector2(4, .5f)),
                ("GridPlatform32x18 (3)", new Vector2(15.5f, 6.75f), new Vector2(2, .5f)),
                ("GridPlatform32x18 (2)", new Vector2(23, 6.75f), new Vector2(3, .5f)),
                ("Landing ledge (1)", new Vector2(29, 9.75f), new Vector2(4, .5f)),
                ("Terminal placeholder", new Vector2(9, 5.25f), new Vector2(1, 1.6f))
            };
            foreach (var (name, center, size) in authored)
            {
                var bounds = AuthoredBounds(solids.Single(s => s.name == name));
                AssertBoundsXY(center, bounds.center, name);
                AssertBoundsXY(size, bounds.size, name);
            }
        }

        [UnityTest] public IEnumerator ForegroundFramesAndVisibleArtMatchEverySolid()
        {
            yield return Load();
            foreach (var solid in Solids())
            {
                var bounds = AuthoredBounds(solid);
                var art = solid.transform.Find("Environment surface art")?.GetComponent<SpriteRenderer>()
                    ?? solid.transform.Find("Floor visuals").GetComponent<SpriteRenderer>();
                var frame = solid.transform.Find("Gameplay surface frame");
                var backing = frame.Find("Backing").GetComponent<SpriteRenderer>();
                var top = frame.Find("Top").GetComponent<SpriteRenderer>();
                Assert.IsFalse(backing.enabled);
                if (solid.enabled)
                {
                    Assert.IsTrue(art.enabled, solid.name);
                    Assert.AreEqual(bounds.size.x, art.bounds.size.x, .001f, solid.name);
                    Assert.AreEqual(bounds.max.y, art.bounds.max.y, .001f, solid.name);
                }
                Assert.AreEqual(.05f * Mathf.Abs(solid.transform.lossyScale.y), top.bounds.size.y, .001f, solid.name);
                if (top.enabled) Assert.AreEqual(bounds.max.y, top.bounds.max.y, .001f, solid.name);
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
            var values = new SerializedObject(actor);
            Assert.AreEqual(.075f, values.FindProperty("coyoteTime").floatValue);
            Assert.AreEqual(.08f, values.FindProperty("jumpBufferTime").floatValue);
            Assert.NotNull(actor.GetComponentInChildren<PlayerPresentation>());
            Assert.AreEqual(5, RunDashBank.Instance.RemainingCharges); Assert.IsFalse(SceneDashSettings.AllowsAirDashChaining());
            for (int i = 0; i < 30 && !actor.IsGrounded; i++) yield return new WaitForFixedUpdate();
            Assert.IsTrue(actor.IsGrounded);
        }

        private static readonly string[] CameraRooms = { LevelAuthoring32x18.Level01, Level11, Level12 };

        [UnityTest] public IEnumerator EntireRoomRendersAtFourOutputSizesWithIntegerWindowboxing(
            [ValueSource(nameof(CameraRooms))] string room)
        {
            yield return Load(room); var camera = Camera.main;
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
            yield return MoveRightTo(11.22f);
            Assert.IsTrue(actor.IsGrounded, "The first platform edge must still support the jump.");
            actor.RequestJump();
            yield return MoveRightTo(14.65f);
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

        [UnityTest] public IEnumerator BufferedJumpDoesNotCrossAnAuthoredExit()
        {
            yield return Load();
            var bank = RunDashBank.Instance;
            var oldActor = actor;
            var body = actor.GetComponent<Rigidbody2D>();
            body.position += Vector2.up * .3f;
            yield return new WaitForFixedUpdate();
            Assert.IsFalse(actor.IsGrounded);
            actor.RequestJump();
            Assert.IsTrue(Object.FindFirstObjectByType<LevelTransition>().TryExit(actor));
            Assert.IsFalse(body.simulated);
            yield return AwaitScene(Destination);
            var replacement = Object.FindFirstObjectByType<PlayerMovement2D>();
            var replacementBody = replacement.GetComponent<Rigidbody2D>();
            for (int tick = 0; tick < 8; tick++) yield return new WaitForFixedUpdate();
            Assert.IsTrue(oldActor == null);
            Assert.IsTrue(replacement.IsGrounded);
            Assert.AreEqual(0, replacementBody.linearVelocity.y, .05f);
            Assert.AreSame(bank, RunDashBank.Instance);
            Assert.AreEqual(5, bank.RemainingCharges);
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
            Assert.AreEqual(4, hud.DisplayedCharges); Assert.AreEqual("4", hud.counter.text);
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
            yield return new WaitUntil(() => SceneManager.GetActiveScene().path == NarrativePrologue.ScenePath,
                System.TimeSpan.FromSeconds(6), () => Assert.Fail("Restart did not load the prologue."));
            Assert.AreNotEqual(handle, SceneManager.GetActiveScene().handle);
            Assert.AreEqual("", bank.CheckpointScenePath);
            var intro = Object.FindFirstObjectByType<NarrativePrologue>();
            yield return new WaitUntil(() => intro.InputArmed, System.TimeSpan.FromSeconds(6),
                () => Assert.Fail("Prologue did not accept input."));
            handle = SceneManager.GetActiveScene().handle;
            Assert.IsTrue(intro.TrySkip());
            yield return new WaitUntil(() => SceneManager.GetActiveScene().path == LevelAuthoring32x18.Level01,
                System.TimeSpan.FromSeconds(6), () => Assert.Fail("Prologue did not load Level01."));
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

        private static Bounds AuthoredBounds(BoxCollider2D shape)
        {
            if (shape.enabled) return shape.bounds;
            Assert.AreEqual("Terminal placeholder", shape.name);
            var center = shape.transform.TransformPoint(shape.offset);
            var size = Vector2.Scale(shape.size, shape.transform.lossyScale);
            return new Bounds(center, size);
        }

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
