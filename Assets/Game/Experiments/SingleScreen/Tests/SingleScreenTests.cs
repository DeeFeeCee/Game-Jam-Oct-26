#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using GameJam.Foundation.Editor;
using GameJam.Levels;
using GameJam.Movement;
using GameJam.Presentation;
using GameJam.Resources;
using GameJam.SingleScreen.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace GameJam.SingleScreen.Tests
{
    public sealed class SingleScreenTests
    {
        private Scene scene;
        [UnityTearDown] public IEnumerator Clean()
        {
            if(RunDashBank.Instance!=null) Object.Destroy(RunDashBank.Instance.gameObject);
            if(scene.IsValid()&&scene.isLoaded){SceneManager.SetActiveScene(SceneManager.CreateScene("Experiment test cleanup"));yield return SceneManager.UnloadSceneAsync(scene);}
            yield return null;LogAssert.NoUnexpectedReceived();
        }
        private IEnumerator Load()
        {
            Assert.IsNull(RunDashBank.Instance);
            scene=EditorSceneManager.LoadSceneInPlayMode(RoomBuilder.Scene,new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            Object.FindFirstObjectByType<PlayerMovementInput2D>().enabled=false;
        }
        [UnityTest] public IEnumerator CameraContractAndNoFollowOwner()
        {
            yield return Load();var c=Camera.main;var p=c.GetComponent<PixelPerfectCamera>();
            Assert.AreEqual(new Vector3(16,9,-10),c.transform.position);Assert.AreEqual(9,c.orthographicSize);
            Assert.IsNull(c.GetComponent<LevelCamera2D>());Assert.IsNull(Object.FindFirstObjectByType<LevelSetup>().levelCamera);
            Assert.AreEqual(20,p.assetsPPU);Assert.AreEqual(640,p.refResolutionX);Assert.AreEqual(360,p.refResolutionY);
            Assert.AreEqual(PixelPerfectCamera.CropFrame.Windowbox,p.cropFrame);
            Assert.IsFalse(c.allowHDR||c.allowMSAA||c.allowDynamicResolution);
        }
        [UnityTest] public IEnumerator CameraRemainsStationaryWhilePlayerWalks()
        {
            yield return Load();var actor=Object.FindFirstObjectByType<PlayerMovement2D>();Vector3 before=Camera.main.transform.position;
            for(int i=0;i<20;i++){actor.SetHorizontalInput(1);yield return new WaitForFixedUpdate();}
            Assert.Greater(actor.transform.position.x,4);Assert.AreEqual(before,Camera.main.transform.position);
        }
        [UnityTest] public IEnumerator AllSolidsAreInsideRoomAndTheirBoundsAreWholeCells()
        {
            yield return Load();var solids=Object.FindObjectsByType<BoxCollider2D>(FindObjectsSortMode.None).Where(c=>!c.isTrigger&&c.attachedRigidbody==null).ToArray();
            Assert.AreEqual(9,solids.Length);
            foreach(var box in solids)
            {
                Assert.That(box.bounds.min.x,Is.InRange(0f,32f));Assert.That(box.bounds.max.x,Is.InRange(0f,32f));
                Assert.That(box.bounds.min.y,Is.InRange(0f,18f));Assert.That(box.bounds.max.y,Is.InRange(0f,18f));
                foreach(float v in new[]{box.bounds.min.x,box.bounds.max.x,box.bounds.min.y,box.bounds.max.y})Assert.AreEqual(Mathf.Round(v),v,.001f,box.name);
                var backing=box.transform.Find("Gameplay surface frame/Backing").GetComponent<SpriteRenderer>();
                Assert.AreEqual(box.bounds.size.x,backing.bounds.size.x,.001f);Assert.AreEqual(box.bounds.size.y,backing.bounds.size.y,.001f);
                Assert.AreEqual(box.bounds.center,backing.bounds.center);
            }
        }
        [UnityTest] public IEnumerator SpawnAndAcceptedPlayerRemainUnscaled()
        {
            yield return Load();var actor=Object.FindFirstObjectByType<PlayerMovement2D>();
            Assert.AreEqual(3.5f,actor.transform.position.x,.01f);Assert.AreEqual(6,actor.MoveSpeed);Assert.AreEqual(10,actor.JumpSpeed);
            Assert.AreEqual(18,actor.DashSpeed);Assert.AreEqual(.16f,actor.DashDuration,.001f);
            Assert.AreEqual(new Vector2(.8f,1.6f),actor.GetComponent<BoxCollider2D>().size);Assert.AreEqual(Vector3.one,actor.transform.localScale);
            Assert.NotNull(actor.GetComponentInChildren<PlayerPresentation>());Assert.AreEqual(5,RunDashBank.Instance.RemainingCharges);
            Assert.IsFalse(SceneDashSettings.AllowsAirDashChaining());
        }
        [UnityTest] public IEnumerator RoomRendersAllBoundsAtRequiredResolutions()
        {
            yield return Load();var actor=Object.FindFirstObjectByType<PlayerMovement2D>();
            foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(2560,1440),new Vector2Int(1366,900)})
            {
                var m=RoomCapture.Save(Camera.main,size.x,size.y,"Logs/SingleScreen/room-"+size.x+"x"+size.y+".png",actor.GetComponentInChildren<SpriteRenderer>());
                Assert.AreEqual(9,m.orthographicSize,.001f);Assert.AreEqual(0,m.lowerLeft.x,.001f);Assert.AreEqual(0,m.lowerLeft.y,.001f);
                Assert.AreEqual(32,m.upperRight.x,.001f);Assert.AreEqual(18,m.upperRight.y,.001f);
                int expected=Mathf.Min(size.x/640,size.y/360);Assert.AreEqual(expected,m.pixelRatio);
                Assert.AreEqual(640*expected,m.viewport.width,.01f);Assert.AreEqual(360*expected,m.viewport.height,.01f);
                yield return null;
            }
        }
        [UnityTest] public IEnumerator PhysicalRouteJumpsAndDashesThroughActualExitWithPersistentBank()
        {
            yield return Load();var actor=Object.FindFirstObjectByType<PlayerMovement2D>();var bank=RunDashBank.Instance;
            bool first=false,second=false,third=false,launch=false,dash=false;int afterLaunch=0;
            for(int tick=0;tick<1200&&SceneManager.GetActiveScene().path==RoomBuilder.Scene;tick++)
            {
                actor.SetHorizontalInput(1);float x=actor.transform.position.x;
                if(!first&&x>4.7f&&actor.IsGrounded){actor.RequestJump();first=true;}
                if(!second&&first&&x>9.2f&&actor.IsGrounded){actor.RequestJump();second=true;}
                if(!third&&second&&x>14.2f&&actor.IsGrounded){actor.RequestJump();third=true;}
                if(!launch&&third&&x>18.8f&&actor.IsGrounded){actor.RequestJump();launch=true;}
                if(launch&&!dash&&++afterLaunch>=7){actor.RequestDash(Vector2.up);dash=true;}
                yield return new WaitForFixedUpdate();
            }
            scene=SceneManager.GetActiveScene();Assert.AreEqual(FoundationBuilder.Destination,scene.path,"Actual trigger traversal must succeed.");
            Assert.IsTrue(first&&second&&third&&launch&&dash);Assert.AreSame(bank,RunDashBank.Instance);
            Assert.AreEqual(4,bank.RemainingCharges);Assert.IsFalse(bank.IsGameOver);
        }
        [Test] public void ExperimentalPlatformDefaultsAreWholeCells()
        {
            var p=AssetDatabase.LoadAssetAtPath<GameObject>(RoomBuilder.Platform);
            Assert.AreEqual(new Vector2(4,1),p.GetComponent<BoxCollider2D>().size);Assert.AreEqual(Vector3.one,p.transform.localScale);
        }
    }
}
#endif
