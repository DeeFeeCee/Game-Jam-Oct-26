#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using GameJam.Levels;
using GameJam.Mechanics;
using GameJam.Movement;
using GameJam.Resources;
using GameJam.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GameJam.UnifiedGameplay.Tests
{
    public sealed class MechanicsSceneTests
    {
        private const string ScenePath="Assets/Game/Scenes/Verification/GameplayMechanicsVerification.unity";
        [UnitySetUp] public IEnumerator Load()
        { yield return SceneManager.LoadSceneAsync(ScenePath); yield return null; yield return null; }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale=1;
            var old=SceneManager.GetActiveScene();
            var empty=SceneManager.CreateScene("Mechanics cleanup"); SceneManager.SetActiveScene(empty);
            if(old.isLoaded) yield return SceneManager.UnloadSceneAsync(old);
            if(RunDashBank.Instance!=null) Object.Destroy(RunDashBank.Instance.gameObject);
            yield return null; LogAssert.NoUnexpectedReceived();
        }
        [UnityTest] public IEnumerator DirectSceneEntryHasSingleSpawnerBankAndEventSystem()
        {
            Assert.AreEqual(1,Object.FindObjectsByType<PlayerMovement2D>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(1,Object.FindObjectsByType<RunDashBank>(FindObjectsSortMode.None).Length);
            Assert.LessOrEqual(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length,1);
            Assert.IsNotNull(Object.FindFirstObjectByType<LevelSetup>().Player);
            Assert.AreEqual(5,RunDashBank.Instance.RemainingCharges); yield return null;
        }
        [UnityTest] public IEnumerator SceneReloadRestoresMechanicsWithoutRechargingBank()
        {
            var bank=RunDashBank.Instance; Assert.IsTrue(bank.TrySpendDash());
            Object.Destroy(Object.FindFirstObjectByType<BreakableWall2D>().gameObject);
            yield return SceneManager.LoadSceneAsync(ScenePath); yield return null;
            Assert.AreSame(bank,RunDashBank.Instance); Assert.AreEqual(4,bank.RemainingCharges);
            Assert.IsNotNull(Object.FindFirstObjectByType<BreakableWall2D>());
            Assert.AreEqual(CrusherState.Armed,Object.FindFirstObjectByType<FallingCrusherPlatform2D>().State);
            foreach(var platform in Object.FindObjectsByType<MovingPlatform2D>(FindObjectsSortMode.None))
                Assert.Less(Vector2.Distance(platform.EndpointA,platform.GetComponent<Rigidbody2D>().position),.2f);
        }
        [UnityTest] public IEnumerator NewPrefabsDoNotContainAdditionalPlayersBanksOrAnimators()
        {
            var roots=SceneManager.GetActiveScene().GetRootGameObjects().Where(r=>r.GetComponent<LevelSetup>()==null).ToArray();
            foreach(var root in roots)
            {
                Assert.IsEmpty(root.GetComponentsInChildren<RunDashBank>(true));
                if(root.GetComponent<PlayerMovement2D>()==null) Assert.IsEmpty(root.GetComponentsInChildren<Animator>(true));
            }
            Assert.IsFalse(SceneDashSettings.AllowsAirDashChaining()); yield return null;
        }
        [UnityTest] public IEnumerator CrusherFailureUsesExistingRestartWithoutOldHazardRefailure()
        {
            var player=Object.FindFirstObjectByType<LevelSetup>().Player.GetComponent<PlayerMovement2D>();
            player.GetComponent<PlayerMovementInput2D>().enabled=false;
            player.GetComponent<Rigidbody2D>().position=new Vector2(27,1.81f); Physics2D.SyncTransforms();
            for(int i=0;i<150 && !RunDashBank.Instance.IsGameOver;i++) yield return new WaitForFixedUpdate();
            Assert.IsTrue(RunDashBank.Instance.IsGameOver); yield return null; yield return null;
            var failure=Object.FindFirstObjectByType<GameOverController>();
            Assert.IsTrue(failure.IsVisible); Assert.IsTrue(failure.TryRestartRun());
            for(int i=0;i<300 && SceneManager.GetActiveScene().path==ScenePath;i++) yield return null;
            yield return new WaitForSeconds(.1f);
            Assert.AreEqual("Assets/Game/Scenes/Levels/Level01.unity",SceneManager.GetActiveScene().path);
            Assert.AreEqual(5,RunDashBank.Instance.RemainingCharges); Assert.IsFalse(RunDashBank.Instance.IsGameOver);
            Assert.IsTrue(Object.FindFirstObjectByType<LevelSetup>().Player.GetComponent<Rigidbody2D>().simulated);
        }
    }
}
#endif
