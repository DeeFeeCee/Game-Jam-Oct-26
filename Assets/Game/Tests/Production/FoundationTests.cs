#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameJam.Levels;
using GameJam.Movement;
using GameJam.Presentation;
using GameJam.Resources;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GameJam.Foundation.Tests
{
    public sealed partial class FoundationTests
    {
        private const string Level = "Assets/Game/Scenes/Levels/Level01.unity";
        private const string LegacyLevel = "Assets/Game/Tests/Fixtures/Production32x18/LegacyLevel01.unity";
        private const string Destination = "Assets/Game/Scenes/VerificationDestination.unity";
        private const string Player = "Assets/Game/Prefabs/Player.prefab";
        private Scene loaded;
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (RunDashBank.Instance != null) Object.Destroy(RunDashBank.Instance.gameObject);
            if (loaded.IsValid() && loaded.isLoaded)
            {
                var clean = SceneManager.CreateScene("Foundation test cleanup");
                SceneManager.SetActiveScene(clean);
                yield return SceneManager.UnloadSceneAsync(loaded);
            }
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }
        [Test] public void LookStartsAtSevenSeconds()
        { var c = new IdleLookClock(); c.Step(6.99f,true,7,1); Assert.False(c.Looking); c.Step(.02f,true,7,1); Assert.True(c.Looking); }
        [Test] public void LookRunsOnlyOncePerInactivityPeriod()
        { var c = new IdleLookClock(); c.Step(7,true,7,1); c.Step(1,true,7,1); c.Step(20,true,7,1); Assert.False(c.Looking); Assert.True(c.Consumed); }
        [Test] public void ActivityRearmsLook()
        { var c = new IdleLookClock(); c.Step(7,true,7,1); c.Step(1,true,7,1); c.Reset(); c.Step(7,true,7,1); Assert.True(c.Looking); }
        [Test] public void ActivityInterruptsLookImmediately()
        { var c = new IdleLookClock(); c.Step(7,true,7,1); c.Reset(); Assert.False(c.Looking); Assert.Zero(c.Elapsed); }
        [Test] public void AirborneOrFailedStateClearsInactivity()
        { var c = new IdleLookClock(); c.Step(6,true,7,1); c.Step(1,false,7,1); c.Step(2,true,7,1); Assert.False(c.Looking); }
        [Test] public void PauseDoesNotAdvanceClock()
        { var c = new IdleLookClock(); c.Step(6,true,7,1); c.Step(0,true,7,1); Assert.AreEqual(6,c.Elapsed); }
        [Test] public void FocusLossOrInitializationResetsClock()
        { var c = new IdleLookClock(); c.Step(7,true,7,1); c.Step(0,false,7,1); Assert.False(c.Looking); Assert.False(c.Consumed); }

        [Test] public void ProductionPrefabRetainsValidatedPhysicsAndBankLink()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Player);
            Assert.NotNull(prefab.GetComponent<PlayerDashBankLink>());
            Assert.AreEqual(Vector3.one, prefab.transform.localScale);
            Assert.AreEqual(new Vector2(.8f,1.6f), prefab.GetComponent<BoxCollider2D>().size);
            Assert.AreEqual(3, prefab.GetComponent<Rigidbody2D>().gravityScale);
            Assert.AreEqual(CollisionDetectionMode2D.Continuous,prefab.GetComponent<Rigidbody2D>().collisionDetectionMode);
            Assert.AreEqual(6,prefab.GetComponent<PlayerMovement2D>().MoveSpeed);
            Assert.AreEqual(10,prefab.GetComponent<PlayerMovement2D>().JumpSpeed);
            Assert.AreEqual(18,prefab.GetComponent<PlayerMovement2D>().DashSpeed);
            Assert.IsNull(prefab.GetComponent<Animator>());
            Assert.NotNull(prefab.transform.Find("Visuals").GetComponent<Animator>());
        }
        [Test] public void AuthoredClipsHaveSpriteOnlyBindingsAndExpectedFrames()
        {
            foreach (var pair in new[] { ("Idle_anim",11), ("Look_anim",14) })
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Animations/"+pair.Item1+".anim");
                var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
                Assert.AreEqual(1,bindings.Length); Assert.AreEqual("",bindings[0].path);
                Assert.AreEqual(typeof(SpriteRenderer),bindings[0].type);
                Assert.AreEqual(pair.Item2,AnimationUtility.GetObjectReferenceCurve(clip,bindings[0]).Length);
                Assert.Zero(AnimationUtility.GetCurveBindings(clip).Length);
            }
        }
        [Test] public void PixelArtImportsPreserveIndependentAuthoredScales()
        {
            foreach(var pair in new[] { ("BlueBG",12), ("BlueLvlGround-newt",20), ("Idle_anim2",100) })
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath("Assets/Art/"+pair.Item1+".png");
                Assert.AreEqual(FilterMode.Point,importer.filterMode);
                Assert.AreEqual(TextureImporterCompression.Uncompressed,importer.textureCompression);
                Assert.False(importer.mipmapEnabled); Assert.AreEqual(pair.Item2,importer.spritePixelsPerUnit);
            }
        }
        [UnityTest] public IEnumerator ProductionSceneSpawnsOneBankedPlayer()
        {
            yield return Load(Level);
            Assert.AreEqual(1,Object.FindObjectsByType<PlayerMovement2D>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(1,Object.FindObjectsByType<RunDashBank>(FindObjectsSortMode.None).Length);
            var actor=Object.FindFirstObjectByType<PlayerMovement2D>();
            for(int i=0;i<30&&!actor.IsGrounded;i++) yield return new WaitForFixedUpdate();
            Assert.True(actor.IsGrounded);
        }
        [UnityTest] public IEnumerator BothAnimationsChangeActualSpritesWithoutChangingPhysics()
        {
            yield return Load(Level);
            var actor=Object.FindFirstObjectByType<PlayerMovement2D>();
            actor.GetComponent<PlayerMovementInput2D>().enabled=false;
            var animator=actor.GetComponentInChildren<Animator>();
            actor.GetComponentInChildren<PlayerPresentation>().enabled=false;
            Vector3 position=actor.transform.position;
            foreach(string state in new[]{"Idle_anim","Look_anim"})
            {
                var frames=new HashSet<Sprite>(); animator.Play(state,0,0); animator.Update(0);
                for(int i=0;i<10;i++) { frames.Add(animator.GetComponent<SpriteRenderer>().sprite); animator.Update(.15f); }
                Assert.Greater(frames.Count,1,state);
            }
            Assert.AreEqual(position,actor.transform.position);
            Assert.AreEqual(Vector3.one,actor.transform.localScale);
        }
        [UnityTest] public IEnumerator PresentationInitializesAndResetsOnActivity()
        {
            yield return Load(Level);
            var presentation=Object.FindFirstObjectByType<PlayerPresentation>();
            Assert.False(presentation.IsLooking); presentation.RecordActivity();
            Assert.False(presentation.IsLooking);
            Assert.False(presentation.GetComponent<Animator>().applyRootMotion);
        }
        [UnityTest] public IEnumerator ExitTransitionsOnceAndPreservesBankAndDestinationPolicy()
        {
            yield return Load(Level);
            var bank=RunDashBank.Instance; bank.TrySpendDash(); int balance=bank.RemainingCharges;
            var coordinator=Object.FindFirstObjectByType<LevelTransition>();
            var actor=Object.FindFirstObjectByType<PlayerMovement2D>();
            Assert.True(coordinator.TryExit(actor)); Assert.False(coordinator.TryExit(actor));
            for(int i=0;i<120&&SceneManager.GetActiveScene().path!=Destination;i++) yield return null;
            loaded=SceneManager.GetActiveScene(); Assert.AreEqual(Destination,loaded.path);
            Assert.AreSame(bank,RunDashBank.Instance); Assert.AreEqual(balance,bank.RemainingCharges);
            Assert.AreEqual(1,Object.FindObjectsByType<RunDashBank>(FindObjectsSortMode.None).Length);
            Assert.True(SceneDashSettings.AllowsAirDashChaining());
        }
        [UnityTest] public IEnumerator FatalChargePreventsExitSuccess()
        {
            yield return Load(Level);
            var bank=RunDashBank.Instance;
            while(bank.RemainingCharges>1) bank.TrySpendDash();
            var actor=Object.FindFirstObjectByType<PlayerMovement2D>();
            actor.RequestDash(Vector2.up); yield return new WaitForFixedUpdate();
            Assert.True(bank.IsGameOver);
            Assert.False(Object.FindFirstObjectByType<LevelTransition>().TryExit(actor));
            Assert.AreEqual(Level,SceneManager.GetActiveScene().path);
        }
        [UnityTest] public IEnumerator NonPlayerColliderCannotTriggerExit()
        {
            yield return Load(Level);
            var exit=Object.FindFirstObjectByType<LevelExit>();
            var crate=new GameObject("Test crate",typeof(Rigidbody2D),typeof(BoxCollider2D));
            crate.GetComponent<Rigidbody2D>().gravityScale=0; crate.transform.position=exit.transform.position;
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.False(exit.transition.TransitionStarted); Object.Destroy(crate);
        }
        [UnityTest] public IEnumerator CameraUsesSceneLocalURPPixelPerfectRendering()
        {
            yield return Load(Level); var camera=Camera.main;
            Assert.True(camera.orthographic); Assert.False(camera.allowMSAA);
            Assert.NotNull(camera.GetComponent<UnityEngine.Rendering.Universal.PixelPerfectCamera>());
        }
        [UnityTest] public IEnumerator AcePreviewScenePlaysIndependently()
        {
            yield return Load("Assets/Scenes/Art tests.unity");
            Assert.IsNull(RunDashBank.Instance);
            var animator=Object.FindFirstObjectByType<Animator>();
            Assert.NotNull(animator); animator.Update(.2f);
            Assert.NotNull(animator.GetComponent<SpriteRenderer>().sprite);
        }
        [UnityTest] public IEnumerator CaptureIndustrialLevelAndVerifyVisiblePixels()
        {
            yield return Load(Level); yield return null;
            var camera=Camera.main;
            var texture=new RenderTexture(1280,720,24);
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=texture});
            var previous=RenderTexture.active; RenderTexture.active=texture;
            var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);
            pixels.ReadPixels(new Rect(0,0,1280,720),0,0); pixels.Apply(); RenderTexture.active=previous;
            Directory.CreateDirectory("Logs/Pass1D");
            File.WriteAllBytes("Logs/Pass1D/level01.png",pixels.EncodeToPNG());
            Assert.Greater(pixels.GetPixels32().Distinct().Count(),10);
            Object.Destroy(pixels); Object.Destroy(texture);
        }
        [UnityTest] public IEnumerator LegacyScrollingRouteCanReachExitUsingOneNonfatalDash()
        {
            yield return Load(LegacyLevel);
            var actor=Object.FindFirstObjectByType<PlayerMovement2D>();
            actor.GetComponent<PlayerMovementInput2D>().enabled=false;
            var bank=RunDashBank.Instance;
            bool stepJump=false, barrierJump=false, dashed=false;
            int afterJump=0;
            for(int tick=0;tick<1000&&SceneManager.GetActiveScene().path==LegacyLevel;tick++)
            {
                if(actor != null && actor.enabled)
                {
                    actor.SetHorizontalInput(1f);
                    float x=actor.transform.position.x;
                    if(!stepJump&&x>-2.5f&&actor.IsGrounded) { actor.RequestJump(); stepJump=true; }
                    if(!barrierJump&&x>7.5f&&actor.IsGrounded) { actor.RequestJump(); barrierJump=true; }
                    if(barrierJump&&!dashed&&++afterJump>=5) { actor.RequestDash(Vector2.up); dashed=true; }
                }
                yield return new WaitForFixedUpdate();
            }
            loaded=SceneManager.GetActiveScene();
            Assert.AreEqual(Destination,loaded.path,"The authored route must be physically traversable, not only a working exit API.");
            Assert.True(stepJump&&dashed); Assert.False(bank.IsGameOver);
            Assert.AreEqual(bank.StartingDashCount-1,bank.RemainingCharges);
        }

        private IEnumerator Load(string path)
        {
            Assert.IsNull(RunDashBank.Instance);
            loaded=EditorSceneManager.LoadSceneInPlayMode(path,new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
        }
    }
}
#endif
