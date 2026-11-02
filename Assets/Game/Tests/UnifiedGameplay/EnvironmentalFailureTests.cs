#if UNITY_EDITOR
using System.Collections;
using GameJam.Levels;
using GameJam.Mechanics;
using GameJam.Movement;
using GameJam.Resources;
using GameJam.UI;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GameJam.UnifiedGameplay.Tests
{
    public sealed class EnvironmentalFailureTests
    {
        private RunDashBank bank;
        private PlayerMovement2D actor;
        private Rigidbody2D body;
        [UnitySetUp] public IEnumerator Setup()
        {
            Assert.IsNull(RunDashBank.Instance);
            EditorSceneManager.LoadSceneInPlayMode("Assets/Game/Scenes/Levels/Level01.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; yield return null;
            bank = RunDashBank.Instance; actor = Object.FindFirstObjectByType<PlayerMovement2D>(); body = actor.GetComponent<Rigidbody2D>();
            actor.GetComponent<PlayerMovementInput2D>().enabled = false; body.gravityScale = 0; body.linearVelocity = Vector2.zero;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1;
            if (RunDashBank.Instance != null) Object.Destroy(RunDashBank.Instance.gameObject);
            var old = SceneManager.GetActiveScene(); SceneManager.SetActiveScene(SceneManager.CreateScene("Environmental test cleanup"));
            if (old.IsValid() && old.isLoaded) yield return SceneManager.UnloadSceneAsync(old);
            yield return null; LogAssert.NoUnexpectedReceived();
        }
        [UnityTest] public IEnumerator FailurePreservesChargesFreezesPlayerAndRejectsExit()
        {
            int changed = 0, failed = 0; bank.ChargesChanged += _ => changed++; bank.GameOver += () => failed++;
            Vector2 position = body.position;
            Assert.IsTrue(bank.TryFailEnvironment()); Assert.IsTrue(bank.IsGameOver);
            Assert.AreEqual(RunFailureCause.Environmental, bank.FailureCause); Assert.AreEqual(5, bank.RemainingCharges);
            Assert.AreEqual(0, changed); Assert.AreEqual(1, failed);
            Assert.IsFalse(body.simulated || actor.enabled || actor.IsDashing);
            Assert.IsFalse(Object.FindFirstObjectByType<LevelTransition>().TryExit(actor));
            actor.RequestJump(); actor.RequestDash(Vector2.up); yield return new WaitForFixedUpdate();
            Assert.AreEqual(position, body.position); Assert.AreEqual(5, bank.RemainingCharges);
        }
        [UnityTest] public IEnumerator RepeatedAndReentrantFailuresAreIdempotent()
        {
            int count = 0; bank.GameOver += () =>
            {
                count++; Assert.IsFalse(bank.TryFailEnvironment()); Assert.IsFalse(bank.TrySpendDash());
                bank.InitializeNewRun(); Assert.IsTrue(bank.IsGameOver);
            };
            Assert.IsTrue(bank.TryFailEnvironment());
            for (int i = 0; i < 20; i++) Assert.IsFalse(bank.TryFailEnvironment());
            Assert.AreEqual(1, count); Assert.AreEqual(5, bank.RemainingCharges); yield return null;
        }
        [UnityTest] public IEnumerator ChargeCallbackCannotReenterEnvironmentalFailure()
        {
            bank.ChargesChanged += _ => Assert.IsFalse(bank.TryFailEnvironment());
            Assert.IsTrue(bank.TrySpendDash()); Assert.IsFalse(bank.IsGameOver);
            Assert.AreEqual(RunFailureCause.None, bank.FailureCause); yield return null;
        }
        [UnityTest] public IEnumerator CauseSpecificGameOverAndHudPreserveActualBalance()
        {
            bank.TrySpendDash(); bank.TryFailEnvironment(); yield return null; yield return null;
            var failure = Object.FindFirstObjectByType<GameOverController>();
            Assert.IsTrue(failure.IsVisible); Assert.AreEqual("SUBJECT INTEGRITY LOST", failure.failureReason.text);
            var hud = Object.FindFirstObjectByType<MobilityReserveHUDController>();
            Assert.AreEqual(4, hud.DisplayedCharges); Assert.AreEqual("04", hud.counter.text);
            Assert.AreEqual("SUBJECT INTEGRITY LOST", hud.warning.text);
        }
        [UnityTest] public IEnumerator ExhaustionStillHasZeroBalanceAndFatalZeroTravel()
        {
            while (bank.RemainingCharges > 1) bank.TrySpendDash();
            Vector2 position = body.position; actor.RequestDash(Vector2.up); yield return new WaitForFixedUpdate();
            Assert.IsTrue(bank.IsGameOver); Assert.AreEqual(0, bank.RemainingCharges);
            Assert.AreEqual(RunFailureCause.MobilityExhaustion, bank.FailureCause); Assert.AreEqual(position, body.position);
            yield return null; yield return null;
            Assert.AreEqual("MOBILITY ALLOCATION EXHAUSTED", Object.FindFirstObjectByType<GameOverController>().failureReason.text);
        }
        [UnityTest] public IEnumerator ExplicitNewRunClearsCauseAndRestoresStartingBank()
        {
            bank.TrySpendDash(); bank.TryFailEnvironment(); bank.InitializeNewRun();
            Assert.IsFalse(bank.IsGameOver); Assert.AreEqual(RunFailureCause.None, bank.FailureCause);
            Assert.AreEqual(5, bank.RemainingCharges); Assert.IsTrue(body.simulated && actor.enabled); yield return null;
        }
        [UnityTest] public IEnumerator EnvironmentalFailureTakesPriorityOverPause()
        {
            var pause = Object.FindFirstObjectByType<PauseController>(); pause.SendMessage("OnApplicationFocus", true);
            Assert.IsTrue(pause.TryPause()); bank.TryFailEnvironment(); yield return null; yield return null;
            Assert.IsTrue(Object.FindFirstObjectByType<GameOverController>().IsVisible);
            Assert.IsFalse(pause.IsPaused || pause.TryPause()); Assert.AreEqual(1, Time.timeScale);
        }
        [UnityTest] public IEnumerator RestartUsesCanonicalLevel01AndFreshAllocation()
        {
            bank.TryFailEnvironment(); yield return null; yield return null;
            int old = SceneManager.GetActiveScene().handle;
            Assert.IsTrue(Object.FindFirstObjectByType<GameOverController>().TryRestartRun());
            for (int i = 0; i < 300 && SceneManager.GetActiveScene().handle == old; i++) yield return null;
            Assert.AreNotEqual(old, SceneManager.GetActiveScene().handle);
            Assert.AreEqual("Assets/Game/Scenes/Levels/Level01.unity", SceneManager.GetActiveScene().path);
            Assert.AreEqual(5, bank.RemainingCharges); Assert.AreEqual(RunFailureCause.None, bank.FailureCause);
        }
        [UnityTest] public IEnumerator ActualFallIntoTriggerFailsWithoutSpending()
        {
            var zone = new GameObject("Accessible fall zone", typeof(BoxCollider2D), typeof(FallDeathZone2D));
            zone.transform.position = new Vector3(-7, -1); zone.GetComponent<BoxCollider2D>().size = new Vector2(4, .5f);
            body.position = new Vector2(-7, 2); body.gravityScale = 3;
            for (int i = 0; i < 120 && !bank.IsGameOver; i++) yield return new WaitForFixedUpdate();
            Assert.IsTrue(bank.IsGameOver); Assert.AreEqual(5, bank.RemainingCharges);
            Assert.AreEqual(RunFailureCause.Environmental, bank.FailureCause); Assert.IsEmpty(zone.GetComponents<SpriteRenderer>());
        }
        [UnityTest] public IEnumerator FallTriggerIgnoresUnrelatedBodies()
        {
            var zone = new GameObject("Remote zone", typeof(BoxCollider2D), typeof(FallDeathZone2D)); zone.transform.position = new Vector3(15, 5);
            var crate = new GameObject("Unrelated body", typeof(Rigidbody2D), typeof(BoxCollider2D));
            crate.transform.position = zone.transform.position; crate.GetComponent<Rigidbody2D>().gravityScale = 0;
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate(); Assert.IsFalse(bank.IsGameOver);
        }
    }
}
#endif
