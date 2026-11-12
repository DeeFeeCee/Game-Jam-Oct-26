#if UNITY_EDITOR
using System;
using System.Collections;
using GameJam.Levels;
using GameJam.Movement;
using GameJam.Resources;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GameJam.UI.Tests
{
    public sealed class RunTimingTests
    {
        private const string Menu = "Assets/Game/Scenes/MainMenu.unity";
        private const string First = "Assets/Game/Scenes/Levels/Level01.unity";
        private const string Final = "Assets/Game/Scenes/Levels/Level12.unity";
        private Guid store;
        private string key;
        private RunTimer Timer => RunTimer.Instance;

        [SetUp]
        public void Setup()
        {
            store = Guid.NewGuid();
            key = "PIDR.Verification.BestCompletionTimeMs." + store.ToString("N");
            RunTimer.UseVerificationStore(store);
            Timer.Abandon();
            Timer.SendMessage("OnApplicationFocus", true);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1;
            if (RunDashBank.Instance != null) Object.Destroy(RunDashBank.Instance.gameObject);
            Timer.Abandon();
            RunTimer.UseVerificationStore(null);
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            var old = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("Run timer cleanup " + store.ToString("N")));
            if (old.IsValid() && old.isLoaded) yield return SceneManager.UnloadSceneAsync(old);
            yield return null;
        }

        [TestCase(0, "00:00.00")]
        [TestCase(258350, "04:18.35")]
        [TestCase(3600000, "1:00:00.00")]
        [TestCase(36001009, "10:00:01.00")]
        [TestCase(-1, "00:00.00")]
        public void FormatsMinutesSecondsCentisecondsAndHours(long milliseconds, string expected) =>
            Assert.AreEqual(expected, RunBestTime.Format(milliseconds));

        [Test]
        public void RecordsOnlyPositiveImprovementsInIsolatedStore()
        {
            var best = new RunBestTime(key);
            Assert.Zero(best.Read());
            PlayerPrefs.SetInt(key, -100);
            Assert.Zero(best.Read());
            PlayerPrefs.SetString(key, "invalid");
            Assert.Zero(best.Read());
            Assert.IsFalse(best.TryImprove(0));
            Assert.IsFalse(best.TryImprove((long)int.MaxValue + 1));
            Assert.IsTrue(best.TryImprove(258350));
            Assert.IsFalse(best.TryImprove(258350));
            Assert.IsFalse(best.TryImprove(305110));
            Assert.IsTrue(best.TryImprove(222680));
            Assert.AreEqual(222680, new RunBestTime(key).Read());
        }

        [UnityTest]
        public IEnumerator MenuAndPrologueAreUntimedAndNewGameKeepsBest()
        {
            new RunBestTime(key).TryImprove(222680);
            yield return Load(Menu);
            var menu = Object.FindFirstObjectByType<MainMenuView>();
            Assert.AreEqual("BEST TIME  03:42.68", menu.bestTime.text);
            Assert.IsTrue(menu.launcher.TryStartNewGame());
            yield return Arrive(NarrativePrologue.ScenePath);
            Assert.Zero(Timer.ElapsedMilliseconds);
            yield return new WaitForSecondsRealtime(.12f);
            Assert.Zero(Timer.ElapsedMilliseconds);
            yield return SkipIntro();
            yield return Advances();
            Assert.AreEqual(222680, RunTimer.BestMilliseconds);
            Assert.AreEqual(1, Object.FindObjectsByType<RunTimer>(FindObjectsSortMode.None).Length);
        }

        [UnityTest]
        public IEnumerator PausedAndUnfocusedIntervalsAreExcluded()
        {
            yield return StartAtFirst();
            yield return Advances();
            var pause = Object.FindFirstObjectByType<PauseController>();
            pause.SendMessage("OnApplicationFocus", true);
            Assert.IsTrue(pause.TryPause());
            long paused = Timer.ElapsedMilliseconds;
            yield return new WaitForSecondsRealtime(.16f);
            Assert.AreEqual(paused, Timer.ElapsedMilliseconds);
            yield return Await(() => pause.InputArmed, "pause input");
            Assert.IsTrue(pause.TryResume());
            yield return Advances();

            Timer.SendMessage("OnApplicationFocus", false);
            long unfocused = Timer.ElapsedMilliseconds;
            yield return new WaitForSecondsRealtime(.16f);
            Assert.AreEqual(unfocused, Timer.ElapsedMilliseconds);
            Timer.SendMessage("OnApplicationFocus", true);
            yield return Advances();
            Assert.Less(Timer.ElapsedMilliseconds - unfocused, 145, "Resume excludes the unfocused interval");
        }

        [UnityTest]
        public IEnumerator AuthorizedSceneTransitionAndEnvironmentalRetryPreserveTime()
        {
            yield return StartAtFirst();
            yield return Advances();
            long first = Timer.ElapsedMilliseconds;
            yield return ExitTo("Assets/Game/Scenes/Levels/Level02.unity");
            Assert.GreaterOrEqual(Timer.ElapsedMilliseconds, first);
            yield return Advances();
            var bank = RunDashBank.Instance;
            Assert.IsTrue(bank.TrySpendDash());
            Assert.IsTrue(bank.TryFailEnvironment());
            long failed = Timer.ElapsedMilliseconds;
            yield return new WaitForSecondsRealtime(.16f);
            Assert.AreEqual(failed, Timer.ElapsedMilliseconds);
            var scene = SceneManager.GetActiveScene();
            Assert.IsTrue(bank.TryRetryLevel(scene));
            yield return Await(() => SceneManager.GetActiveScene() != scene && !bank.IsGameOver, "checkpoint retry");
            Assert.GreaterOrEqual(Timer.ElapsedMilliseconds, failed);
            Assert.AreEqual(5, bank.RemainingCharges);
            yield return Advances();
            Assert.Zero(RunTimer.BestMilliseconds);
        }

        [UnityTest]
        public IEnumerator MobilityRestartWaitsForLevelOneAndMenuAbandonsAttempt()
        {
            new RunBestTime(key).TryImprove(60000);
            yield return StartAtFirst();
            yield return Advances();
            var bank = RunDashBank.Instance;
            while (bank.RemainingCharges > 0) bank.TrySpendDash();
            var failure = Object.FindFirstObjectByType<GameOverController>();
            yield return Await(() => failure.InputArmed, "game-over input");
            Assert.IsTrue(failure.TryRestartRun());
            yield return Arrive(NarrativePrologue.ScenePath);
            Assert.Zero(Timer.ElapsedMilliseconds);
            yield return SkipIntro();
            yield return Advances();
            var pause = Object.FindFirstObjectByType<PauseController>();
            pause.SendMessage("OnApplicationFocus", true);
            Assert.IsTrue(pause.TryPause());
            yield return Await(() => pause.InputArmed, "pause input");
            Assert.IsTrue(pause.TryReturnToMainMenu());
            yield return Arrive(Menu);
            Assert.Zero(Timer.ElapsedMilliseconds);
            Assert.AreEqual(60000, RunTimer.BestMilliseconds);
        }

        [UnityTest]
        public IEnumerator DirectLevelTwelveAndDirectEndingCannotSave()
        {
            yield return StartAtFirst();
            yield return Advances();
            yield return Load(Final);
            yield return ExitTo(EndingSequence.ScenePath);
            Assert.Zero(Timer.CompletedMilliseconds);
            Assert.Zero(RunTimer.BestMilliseconds);

            yield return StartAtFirst();
            yield return Advances();
            yield return Load(EndingSequence.ScenePath);
            Assert.Zero(Timer.CompletedMilliseconds);
            Assert.Zero(RunTimer.BestMilliseconds);
        }

        [UnityTest]
        public IEnumerator ConsecutiveAuthorizedExitsRecordExactlyOneCompletion()
        {
            yield return StartAtFirst();
            for (int level = 2; level <= 12; level++)
            {
                yield return Advances();
                yield return ExitTo($"Assets/Game/Scenes/Levels/Level{level:D2}.unity");
            }
            yield return Advances();
            var exit = Object.FindFirstObjectByType<LevelTransition>();
            var actor = Object.FindFirstObjectByType<LevelSetup>().Player.GetComponent<PlayerMovement2D>();
            Assert.AreEqual(EndingSequence.ScenePath, exit.destinationScene);
            Assert.IsTrue(exit.TryExit(actor));
            long time = Timer.CompletedMilliseconds;
            Assert.Greater(time, 0);
            Assert.IsFalse(exit.TryExit(actor));
            yield return Arrive(EndingSequence.ScenePath);
            Assert.AreEqual(time, Timer.CompletedMilliseconds);
            Assert.AreEqual(time, RunTimer.BestMilliseconds);
            long ending = Timer.ElapsedMilliseconds;
            yield return new WaitForSecondsRealtime(.15f);
            Assert.AreEqual(ending, Timer.ElapsedMilliseconds);
            yield return Load(Menu);
            Assert.AreEqual("BEST TIME  " + RunBestTime.Format(time), Object.FindFirstObjectByType<MainMenuView>().bestTime.text);
            Assert.Zero(Timer.ElapsedMilliseconds);
        }

        private IEnumerator StartAtFirst()
        {
            yield return Load(Menu);
            Assert.AreEqual(RunTimer.BestMilliseconds == 0 ? "BEST TIME  --:--.--" :
                "BEST TIME  " + RunBestTime.Format(RunTimer.BestMilliseconds),
                Object.FindFirstObjectByType<MainMenuView>().bestTime.text);
            Assert.IsTrue(Object.FindFirstObjectByType<NewGameLauncher>().TryStartNewGame());
            yield return Arrive(NarrativePrologue.ScenePath);
            yield return SkipIntro();
        }

        private static IEnumerator SkipIntro()
        {
            var intro = Object.FindFirstObjectByType<NarrativePrologue>();
            intro.SendMessage("OnApplicationFocus", true);
            yield return Await(() => intro.InputArmed, "prologue input");
            Assert.IsTrue(intro.TrySkip());
            yield return Arrive(First);
        }

        private static IEnumerator Load(string path)
        {
            yield return SceneManager.LoadSceneAsync(path, LoadSceneMode.Single);
            yield return Arrive(path);
        }

        private static IEnumerator ExitTo(string path)
        {
            var exit = Object.FindFirstObjectByType<LevelTransition>();
            var actor = Object.FindFirstObjectByType<LevelSetup>().Player.GetComponent<PlayerMovement2D>();
            Assert.AreEqual(path, exit.destinationScene);
            Assert.IsTrue(exit.TryExit(actor));
            yield return Arrive(path);
        }

        private IEnumerator Advances()
        {
            long previous = Timer.ElapsedMilliseconds;
            yield return Await(() => Timer.ElapsedMilliseconds >= previous + 25, "timer advancement");
        }

        private static IEnumerator Arrive(string path)
        {
            yield return Await(() => SceneManager.GetActiveScene().path == path, path);
            yield return null;
            yield return null;
        }

        private static IEnumerator Await(Func<bool> check, string label)
        {
            yield return new WaitUntil(check, TimeSpan.FromSeconds(6), () => Assert.Fail("Timed out: " + label));
        }
    }
}
#endif
