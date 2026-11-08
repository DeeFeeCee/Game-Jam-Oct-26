#if UNITY_EDITOR
using System;
using System.Collections;
using GameJam.Levels;
using GameJam.Movement;
using GameJam.Resources;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GameJam.UI.Tests
{
    public sealed class LevelCheckpointTests
    {
        private const string Menu = "Assets/Game/Scenes/MainMenu.unity";
        private const string Level01 = "Assets/Game/Scenes/Levels/Level01.unity";
        private const string Level03 = "Assets/Game/Scenes/Levels/Level03.unity";
        private const string Level04 = "Assets/Game/Scenes/Levels/Level04.unity";
        private const string Level05 = "Assets/Game/Scenes/Levels/Level05.unity";

        private RunDashBank bank;
        private Keyboard keyboard;
        private Gamepad pad;
        private InputSettings priorSettings, testSettings;
        private HideFlags priorFlags;

        [UnitySetUp] public IEnumerator Setup()
        {
            Assert.IsNull(RunDashBank.Instance);
            yield return Load(Level01);
            bank = RunDashBank.Instance;
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1;
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
            if (testSettings != null)
            {
                InputSystem.settings = priorSettings;
                priorSettings.hideFlags = priorFlags;
                Object.Destroy(testSettings);
            }
            keyboard = null;
            pad = null;
            testSettings = null;
            if (RunDashBank.Instance != null) Object.Destroy(RunDashBank.Instance.gameObject);
            var old = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("Level checkpoint test cleanup"));
            if (old.IsValid() && old.isLoaded) yield return SceneManager.UnloadSceneAsync(old);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest] public IEnumerator EntryAndRetryRestoreTheSameLevel04BalanceAfterRepeatedDeaths()
        {
            AssertCheckpoint(Level01, 5);
            Assert.IsTrue(bank.TrySpendDash());
            yield return Load(Level03);
            yield return TakeExit(Level04);
            AssertCheckpoint(Level04, 4);

            int resets = 0, balanceEvents = 0;
            bank.NewRunStarted += () => resets++;
            bank.ChargesChanged += _ => balanceEvents++;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                Assert.IsTrue(bank.TrySpendDash());
                Assert.IsTrue(bank.TrySpendDash());
                Assert.AreEqual(2, bank.RemainingCharges);
                yield return FailEnvironmentally();
                Assert.AreEqual(2, bank.RemainingCharges);
                var failure = Object.FindFirstObjectByType<GameOverController>();
                Assert.AreEqual("RETRY LEVEL", failure.restartButton.GetComponentInChildren<TMPro.TMP_Text>(true).text);
                Assert.IsFalse(failure.TryRestartRun());
                var oldBody = Object.FindFirstObjectByType<LevelSetup>().Player.GetComponent<Rigidbody2D>();
                int oldHandle = SceneManager.GetActiveScene().handle;
                int before = balanceEvents;
                Assert.IsTrue(failure.TryRetryLevel());
                Assert.IsFalse(failure.TryRetryLevel());
                Assert.IsFalse(oldBody.simulated);
                Assert.AreEqual(2, bank.RemainingCharges);
                Assert.IsTrue(bank.IsGameOver);
                yield return AwaitScene(Level04, oldHandle);
                Assert.AreEqual(before + 1, balanceEvents);
                Assert.AreEqual(0, resets);
                AssertCheckpoint(Level04, 4);
                Assert.AreEqual(RunFailureCause.None, bank.FailureCause);
                Assert.IsFalse(bank.IsGameOver);
                var setup = Object.FindFirstObjectByType<LevelSetup>();
                Assert.AreEqual(setup.spawn.position.x, setup.Player.transform.position.x, .001f);
                Assert.AreEqual(4, Object.FindFirstObjectByType<MobilityReserveHUDController>().DisplayedCharges);
                Assert.IsFalse(Object.FindFirstObjectByType<GameOverController>().IsVisible);
                Assert.AreEqual(1, Object.FindObjectsByType<RunDashBank>(FindObjectsSortMode.None).Length);
            }
        }

        [UnityTest] public IEnumerator EnvironmentalFailureAtOneChargeCanRetryThroughTheBank()
        {
            while (bank.RemainingCharges > 2) Assert.IsTrue(bank.TrySpendDash());
            yield return Load(Level04);
            AssertCheckpoint(Level04, 2);
            Assert.IsTrue(bank.TrySpendDash());
            yield return FailEnvironmentally();
            AssertCheckpoint(Level04, 2, remaining: 1);
            int resets = 0;
            bank.NewRunStarted += () => resets++;
            var scene = SceneManager.GetActiveScene();
            Assert.IsTrue(bank.TryRetryLevel(scene));
            Assert.IsFalse(bank.TryRetryLevel(scene));
            yield return AwaitScene(Level04, scene.handle);
            AssertCheckpoint(Level04, 2);
            Assert.AreEqual(RunFailureCause.None, bank.FailureCause);
            Assert.AreEqual(0, resets);
        }

        [UnityTest] public IEnumerator Level05ReplacesLevel04CheckpointAndNewGameClearsIt()
        {
            Assert.IsTrue(bank.TrySpendDash());
            yield return Load(Level04);
            AssertCheckpoint(Level04, 4);
            Assert.IsTrue(bank.TrySpendDash());
            yield return TakeExit(Level05);
            AssertCheckpoint(Level05, 3);
            Assert.IsTrue(bank.TrySpendDash());
            yield return FailEnvironmentally();
            int oldHandle = SceneManager.GetActiveScene().handle;
            Assert.IsTrue(Object.FindFirstObjectByType<GameOverController>().TryRetryLevel());
            yield return AwaitScene(Level05, oldHandle);
            AssertCheckpoint(Level05, 3);

            yield return FailEnvironmentally();
            oldHandle = SceneManager.GetActiveScene().handle;
            Assert.IsTrue(Object.FindFirstObjectByType<GameOverController>().TryReturnToMainMenu());
            yield return AwaitScene(Menu, oldHandle);
            AssertCheckpoint(Level05, 3);
            Assert.IsTrue(bank.IsGameOver);
            oldHandle = SceneManager.GetActiveScene().handle;
            Object.FindFirstObjectByType<MainMenuView>().newGameButton.onClick.Invoke();
            yield return AwaitScene(NarrativePrologue.ScenePath, oldHandle);
            AssertCheckpoint("", 0, remaining: 5);
            Assert.IsFalse(bank.IsGameOver);
            var intro = Object.FindFirstObjectByType<NarrativePrologue>();
            yield return Until(() => intro.InputArmed, "Prologue input");
            oldHandle = SceneManager.GetActiveScene().handle;
            Assert.IsTrue(intro.TrySkip());
            yield return AwaitScene(Level01, oldHandle);
            AssertCheckpoint(Level01, 5);
        }

        [UnityTest] public IEnumerator RetryDiscardsHeldKeyboardAndGamepadMovementUntilRelease()
        {
            priorSettings = InputSystem.settings;
            priorFlags = priorSettings.hideFlags;
            if (priorFlags == HideFlags.HideAndDontSave) priorSettings.hideFlags |= HideFlags.HideInInspector;
            testSettings = Object.Instantiate(priorSettings);
            testSettings.hideFlags = HideFlags.HideAndDontSave;
            testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = testSettings;
            keyboard = InputSystem.AddDevice<Keyboard>();
            pad = InputSystem.AddDevice<Gamepad>();

            yield return FailEnvironmentally();
            int oldHandle = SceneManager.GetActiveScene().handle;
            Assert.IsTrue(Object.FindFirstObjectByType<GameOverController>().TryRetryLevel());
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D, Key.W, Key.Space));
            InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = Vector2.right }.WithButton(GamepadButton.RightShoulder));
            yield return AwaitScene(Level01, oldHandle);
            var setup = Object.FindFirstObjectByType<LevelSetup>();
            var body = setup.Player.GetComponent<Rigidbody2D>();
            for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();
            Assert.IsTrue(keyboard.dKey.isPressed && pad.rightShoulder.isPressed);
            Assert.AreEqual(setup.spawn.position.x, body.position.x, .001f);
            Assert.IsTrue(setup.Player.GetComponent<PlayerMovement2D>().IsGrounded);
            AssertCheckpoint(Level01, 5);

            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
            for (int i = 0; i < 8; i++) yield return new WaitForFixedUpdate();
            Assert.Greater(body.position.x, setup.spawn.position.x + .3f);
            AssertCheckpoint(Level01, 5);
        }

        private IEnumerator TakeExit(string destination)
        {
            int oldHandle = SceneManager.GetActiveScene().handle;
            var setup = Object.FindFirstObjectByType<LevelSetup>();
            var exit = Object.FindFirstObjectByType<LevelTransition>();
            Assert.AreEqual(destination, exit.destinationScene);
            Assert.IsTrue(exit.TryExit(setup.Player.GetComponent<PlayerMovement2D>()));
            yield return AwaitScene(destination, oldHandle);
        }

        private IEnumerator FailEnvironmentally()
        {
            Assert.IsTrue(bank.TryFailEnvironment());
            Assert.AreEqual(RunFailureCause.Environmental, bank.FailureCause);
            var failure = Object.FindFirstObjectByType<GameOverController>();
            yield return Until(() => failure.IsVisible && failure.InputArmed, "Game Over input");
        }

        private void AssertCheckpoint(string scene, int entry, int? remaining = null)
        {
            Assert.AreSame(bank, RunDashBank.Instance);
            Assert.AreEqual(scene, bank.CheckpointScenePath);
            Assert.AreEqual(entry, bank.CheckpointCharges);
            Assert.AreEqual(remaining ?? entry, bank.RemainingCharges);
        }

        private static IEnumerator Load(string path)
        {
            int handle = SceneManager.GetActiveScene().handle;
            yield return SceneManager.LoadSceneAsync(path, LoadSceneMode.Single);
            yield return AwaitScene(path, handle);
        }

        private static IEnumerator AwaitScene(string path, int oldHandle)
        {
            yield return Until(() => SceneManager.GetActiveScene().path == path &&
                SceneManager.GetActiveScene().handle != oldHandle, path);
            yield return null;
            yield return null;
        }

        private static IEnumerator Until(Func<bool> ready, string description)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 10;
            while (!ready() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.IsTrue(ready(), description);
        }
    }
}
#endif
