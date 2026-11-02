using System.Linq;
using System.Collections;
using GameJam.Levels;
using GameJam.Mechanics;
using GameJam.Movement;
using GameJam.Resources;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GameJam.UnifiedGameplay.EditorTests
{
    public sealed class MechanicsAuthoringTests
    {
        private SceneSetup[] playSceneSetup;
        private const string ScenePath = "Assets/Game/Scenes/Verification/GameplayMechanicsVerification.unity";
        private GameObject Prefab(string name) => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/"+name+".prefab");
        [Test] public void AllSixPrefabsHavePersistentGuidsAndNoMissingScripts()
        {
            foreach (string name in new[]{"FallDeathZone","MovingPlatform","Turret","TurretProjectile","BreakableWall","FallingCrusherPlatform"})
            {
                var root = Prefab(name); Assert.IsNotNull(root,name);
                Assert.IsNotEmpty(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(root)));
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    Assert.AreEqual(0,GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject),name);
            }
        }
        [Test] public void KillPlaneIsInvisibleResizableTrigger()
        {
            var root = Prefab("FallDeathZone"); Assert.IsTrue(root.GetComponent<BoxCollider2D>().isTrigger);
            Assert.IsEmpty(root.GetComponentsInChildren<Renderer>()); Assert.IsNotNull(root.GetComponent<FallDeathZone2D>());
        }
        [Test] public void PlatformEndpointsAreOutsideMovingBodyAndVisualsAreReplaceable()
        {
            var platform = Prefab("MovingPlatform").GetComponentInChildren<MovingPlatform2D>();
            Assert.IsFalse(platform.pointA.IsChildOf(platform.transform)); Assert.IsFalse(platform.pointB.IsChildOf(platform.transform));
            Assert.IsNotNull(platform.surface.sprite); Assert.IsNotNull(platform.topEdge);
            Assert.Greater(platform.topEdge.sortingOrder,platform.surface.sortingOrder);
            Assert.AreEqual(RigidbodyType2D.Kinematic,platform.GetComponent<Rigidbody2D>().bodyType);
        }
        [Test] public void TurretProjectileReferenceAndMuzzleAreAssigned()
        {
            var turret = Prefab("Turret").GetComponent<Turret2D>(); Assert.IsNotNull(turret.projectilePrefab); Assert.IsNotNull(turret.muzzle);
            Assert.AreEqual(Prefab("TurretProjectile"),turret.projectilePrefab.gameObject);
            Assert.IsTrue(turret.projectilePrefab.GetComponent<Collider2D>().isTrigger);
        }
        [Test] public void CrusherTriggerRemainsStationaryOutsideFallingBody()
        {
            var root = Prefab("FallingCrusherPlatform"); var zone = root.GetComponentInChildren<CrusherActivationZone2D>();
            Assert.IsNotNull(zone.crusher); Assert.IsFalse(zone.transform.IsChildOf(zone.crusher.transform));
            Assert.IsTrue(zone.GetComponent<BoxCollider2D>().isTrigger); Assert.Greater(zone.crusher.maximumDescent,0);
            Assert.Greater(zone.crusher.topEdge.sortingOrder,zone.crusher.surface.sortingOrder);
        }
        [Test] public void BuildStartupAndAcceptedSceneGuidsRemainUnchanged()
        {
            Assert.AreEqual("Assets/Game/Scenes/MainMenu.unity",EditorBuildSettings.scenes.First(s=>s.enabled).path);
            Assert.AreEqual("54cd109dfa8beb54088109b65f2d16b5",AssetDatabase.AssetPathToGUID("Assets/Game/Scenes/Levels/Level01.unity"));
            Assert.IsTrue(EditorBuildSettings.scenes.Any(s=>s.enabled && s.path == ScenePath));
        }
        [Test] public void VerificationSceneUsesAcceptedSpawnerAndStationaryPixelCamera()
        {
            var scene = SceneManager.GetSceneByPath(ScenePath); bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
            try
            {
                var all = scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
                var setup = all.Select(t=>t.GetComponent<LevelSetup>()).Single(s=>s!=null);
                Assert.AreEqual(Prefab("Player"),setup.playerPrefab); Assert.AreEqual(Prefab("RunDashBank"),setup.bankPrefab.gameObject);
                Assert.IsNull(setup.levelCamera);
                var camera = all.Select(t=>t.GetComponent<Camera>()).Single(c=>c!=null);
                Assert.AreEqual(new Vector3(16,9,-10),camera.transform.position); Assert.AreEqual(9,camera.orthographicSize);
                var pp = camera.GetComponent<PixelPerfectCamera>(); Assert.AreEqual(640,pp.refResolutionX); Assert.AreEqual(360,pp.refResolutionY); Assert.AreEqual(20,pp.assetsPPU);
                Assert.AreEqual(2,all.Count(t=>t.GetComponent<MovingPlatform2D>()!=null));
                Assert.AreEqual(1,all.Count(t=>t.GetComponent<FallingCrusherPlatform2D>()!=null));
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene,true); }
        }
        [UnityTest] public IEnumerator InspectorSizeUpdatesColliderAndVisualsWithoutUnsafeTilingNotifications()
        {
            var scene=EditorSceneManager.NewPreviewScene();
            int unsafeMessages=0;
            Application.LogCallback listener=(message,stack,type)=>{if(message.Contains("SendMessage cannot be called")) unsafeMessages++;};
            Application.logMessageReceived+=listener;
            try
            {
                var obj=(GameObject)PrefabUtility.InstantiatePrefab(Prefab("MovingPlatform"),scene);
                var platform=obj.GetComponentInChildren<MovingPlatform2D>(); var serialized=new SerializedObject(platform);
                Vector2 size=new Vector2(4,.75f); serialized.FindProperty("size").vector2Value=size;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                for(int i=0;i<10 && platform.surface.size!=size;i++) yield return null;
                Assert.AreEqual(size,platform.GetComponent<BoxCollider2D>().size); Assert.AreEqual(size,platform.surface.size);
                Assert.AreEqual(size.x,platform.topEdge.size.x); Assert.AreEqual(0,unsafeMessages);
            }
            finally
            {
                Application.logMessageReceived-=listener; EditorSceneManager.ClosePreviewScene(scene);
            }
        }
        [UnityTest] public IEnumerator RepeatedPlayEntryWithoutDomainReloadResetsRunAndMechanics()
        {
            Assert.IsTrue(EditorSettings.enterPlayModeOptionsEnabled);
            Assert.IsTrue((EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload)!=0);
            playSceneSetup=EditorSceneManager.GetSceneManagerSetup();
            EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            for(int session=0;session<2;session++)
            {
                yield return new EnterPlayMode(false); yield return null; yield return null;
                var bank=RunDashBank.Instance; Assert.IsNotNull(bank); Assert.AreEqual(5,bank.RemainingCharges);
                Assert.AreEqual(RunFailureCause.None,bank.FailureCause); Assert.IsFalse(bank.IsGameOver);
                Assert.AreEqual(1,Object.FindObjectsByType<RunDashBank>(FindObjectsSortMode.None).Length);
                var player=Object.FindFirstObjectByType<LevelSetup>().Player.GetComponent<PlayerMovement2D>();
                Assert.IsTrue(player.enabled); Assert.IsTrue(player.GetComponent<Rigidbody2D>().simulated);
                var crusher=Object.FindFirstObjectByType<FallingCrusherPlatform2D>(); Assert.AreEqual(CrusherState.Armed,crusher.State);
                player.GetComponent<PlayerMovementInput2D>().enabled=false;
                player.RequestDash(Vector2.up);
                // EditMode coroutines advance on Editor updates, not the PlayMode fixed-yield runner.
                double deadline=EditorApplication.timeSinceStartup+2;
                while(bank.RemainingCharges==5 && EditorApplication.timeSinceStartup<deadline) yield return null;
                Assert.IsTrue(player.IsDashing); Assert.AreEqual(4,bank.RemainingCharges);
                Assert.IsTrue(crusher.TryActivate(player.GetComponent<Collider2D>()));
                Assert.IsTrue(bank.TryFailEnvironment()); yield return null;
                Assert.IsFalse(player.enabled); Assert.AreEqual(4,bank.RemainingCharges);
                yield return new ExitPlayMode();
            }
        }
        [UnityTearDown] public IEnumerator RestoreEditorAfterPlayEntryTest()
        {
            if(EditorApplication.isPlaying) yield return new ExitPlayMode();
            if(playSceneSetup!=null)
            {
                var setup=playSceneSetup; playSceneSetup=null;
                if(setup.Any(s=>s.isActive && s.isLoaded)) EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            }
        }
    }
}
