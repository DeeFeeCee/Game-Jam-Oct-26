#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using GameJam.Levels;
using GameJam.Movement;
using GameJam.Resources;
using GameJam.UI;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GameJam.BlueIntegration.Tests
{
    public sealed class ResearchTerminalTests
    {
        private ResearchTerminal terminal;
        private PlayerMovement2D actor;
        private Keyboard keyboard;
        private Gamepad gamepad;
        private InputSettings previous, settings;
        private string previousJson;
        private HideFlags previousFlags;
        [UnitySetUp] public IEnumerator Setup()
        {
            Time.timeScale = 1;
            previous = InputSystem.settings; previousJson = JsonUtility.ToJson(previous); previousFlags = previous.hideFlags;
            settings = Object.Instantiate(previous); settings.hideFlags = HideFlags.HideAndDontSave;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = settings; keyboard = InputSystem.AddDevice<Keyboard>();
            EditorSceneManager.LoadSceneInPlayMode("Assets/Game/Scenes/Verification/BlueArtTerminal.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; yield return null;
            terminal = Object.FindFirstObjectByType<ResearchTerminal>(); actor = Object.FindFirstObjectByType<PlayerMovement2D>();
            terminal.SendMessage("OnApplicationFocus", true);
            Object.FindFirstObjectByType<PauseController>().SendMessage("OnApplicationFocus", true);
            actor.transform.position = terminal.transform.position;
            actor.GetComponent<Rigidbody2D>().gravityScale = 0; actor.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (terminal != null) Object.Destroy(terminal.gameObject);
            if (RunDashBank.Instance != null) Object.Destroy(RunDashBank.Instance.gameObject);
            yield return null;
            var old = SceneManager.GetActiveScene(); SceneManager.SetActiveScene(SceneManager.CreateScene("Blue test cleanup"));
            yield return SceneManager.UnloadSceneAsync(old);
            InputSystem.RemoveDevice(keyboard);
            if (gamepad != null && gamepad.added) InputSystem.RemoveDevice(gamepad);
            if (previous == null) { previous = ScriptableObject.CreateInstance<InputSettings>(); JsonUtility.FromJsonOverwrite(previousJson, previous); }
            InputSystem.settings = previous; previous.hideFlags = previousFlags; Object.Destroy(settings);
            Time.timeScale = 1; yield return null; LogAssert.NoUnexpectedReceived();
        }
        [UnityTest] public IEnumerator RequiresRangeAndPreservesBankAndMovement()
        {
            actor.transform.position += Vector3.right * 10; Assert.IsFalse(terminal.TryOpen());
            actor.transform.position = terminal.transform.position;
            int charges = RunDashBank.Instance.RemainingCharges;
            string authoredEntry = terminal.lore.text;
            Assert.IsTrue(terminal.TryOpen()); yield return null; yield return null;
            Assert.AreEqual(0, Time.timeScale); Assert.IsTrue(actor.enabled); Assert.IsFalse(actor.GetComponent<PlayerMovementInput2D>().enabled);
            Assert.IsTrue(terminal.InputArmed); Assert.AreEqual(authoredEntry, terminal.lore.text);
            Assert.AreEqual(1, Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Count(s => s.isActiveAndEnabled));
            terminal.closeButton.onClick.Invoke(); yield return null; yield return null;
            Assert.AreEqual(1, Time.timeScale); Assert.IsTrue(actor.GetComponent<PlayerMovementInput2D>().enabled);
            Assert.AreEqual(charges, RunDashBank.Instance.RemainingCharges);
        }
        [UnityTest] public IEnumerator HeldOpenAndCloseControlsCannotLeakIntoGameplayOrPause()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E, Key.Space)); yield return null; yield return null;
            Assert.IsTrue(terminal.IsOpen); Assert.IsFalse(terminal.InputArmed);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; yield return null;
            Assert.IsTrue(terminal.InputArmed);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape, Key.Space)); yield return null; yield return null;
            Assert.IsFalse(terminal.IsOpen); Assert.IsTrue(terminal.WaitingForRelease);
            Assert.IsFalse(Object.FindFirstObjectByType<PauseController>().IsPaused);
            Assert.IsFalse(actor.GetComponent<PlayerMovementInput2D>().enabled);
            int charges = RunDashBank.Instance.RemainingCharges;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; yield return null;
            Assert.IsTrue(actor.GetComponent<PlayerMovementInput2D>().enabled); Assert.AreEqual(charges, RunDashBank.Instance.RemainingCharges);
        }
        [UnityTest] public IEnumerator PauseAndFailureHaveExclusiveOwnership()
        {
            var pause = Object.FindFirstObjectByType<PauseController>();
            Assert.IsTrue(pause.TryPause()); Assert.IsFalse(terminal.TryOpen()); yield return null; yield return null;
            Assert.IsTrue(pause.TryResume()); yield return null; yield return null;
            Assert.IsTrue(terminal.TryOpen()); Assert.IsFalse(pause.TryPause()); yield return null;
            Assert.IsTrue(RunDashBank.Instance.TryFailEnvironment()); yield return null; yield return null;
            Assert.IsFalse(terminal.IsOpen); Assert.IsNull(ResearchTerminal.Owner); Assert.AreEqual(1, Time.timeScale);
            Assert.IsTrue(Object.FindFirstObjectByType<GameOverController>().IsVisible); Assert.IsFalse(terminal.TryOpen());
            Assert.IsFalse(actor.GetComponent<PlayerMovementInput2D>().enabled);
        }
        [UnityTest] public IEnumerator FocusLossAndDisableReleaseOwnership()
        {
            Assert.IsTrue(terminal.TryOpen()); yield return null; yield return null;
            terminal.SendMessage("OnApplicationFocus", false); terminal.Close(); Assert.IsTrue(terminal.IsOpen); Assert.IsFalse(terminal.InputArmed);
            terminal.SendMessage("OnApplicationFocus", true); yield return null; yield return null;
            terminal.gameObject.SetActive(false); Assert.IsNull(ResearchTerminal.Owner); Assert.AreEqual(1, Time.timeScale);
            Assert.IsTrue(actor.GetComponent<PlayerMovementInput2D>().enabled);
        }
        [UnityTest] public IEnumerator GamepadOpenAndCancelRequireRelease()
        {
            gamepad = InputSystem.AddDevice<Gamepad>();
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.West)); yield return null; yield return null;
            Assert.IsTrue(terminal.IsOpen); Assert.IsFalse(terminal.InputArmed);
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null; yield return null;
            Assert.IsTrue(terminal.InputArmed);
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.East)); yield return null; yield return null;
            Assert.IsFalse(terminal.IsOpen); Assert.IsTrue(terminal.WaitingForRelease);
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null; yield return null;
            Assert.IsNull(ResearchTerminal.Owner); Assert.IsTrue(actor.GetComponent<PlayerMovementInput2D>().enabled);
        }
        [UnityTest] public IEnumerator SceneUnloadRestoresTimeAndClearsOwner()
        {
            Assert.IsTrue(terminal.TryOpen()); yield return null;
            var old = SceneManager.GetActiveScene(); SceneManager.SetActiveScene(SceneManager.CreateScene("Blue unload check"));
            yield return SceneManager.UnloadSceneAsync(old);
            Assert.IsNull(ResearchTerminal.Owner); Assert.AreEqual(1, Time.timeScale);
        }

        [UnityTest] public IEnumerator VisibleTerminalRendererAlternatesBothFramesAcrossLoops()
        {
            var animator = terminal.GetComponentInChildren<Animator>();
            var renderer = animator.GetComponent<SpriteRenderer>();
            Vector3 position = terminal.transform.position;
            Vector3 visualPosition = renderer.transform.localPosition;
            var frames = new System.Collections.Generic.List<string>();
            float deadline = Time.realtimeSinceStartup + 5;
            while (frames.Count < 5 && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                string frame = renderer.sprite.name;
                if (frames.Count == 0 || frames[frames.Count - 1] != frame) frames.Add(frame);
                var accent = renderer.transform.Find("Blink status accent").GetComponent<SpriteRenderer>();
                Assert.AreEqual(frame == "BlueTerminal_Blink", accent.enabled);
                Assert.AreEqual(position, terminal.transform.position);
                Assert.AreEqual(visualPosition, renderer.transform.localPosition);
            }
            CollectionAssert.AreEquivalent(new[] { "BlueTerminal_Norm", "BlueTerminal_Blink" }, frames.Distinct());
            Assert.AreEqual(5, frames.Count, "Both sprite frames must recur across multiple loops.");
            Debug.Log("Visible terminal sprite sequence: " + string.Join(", ", frames));
        }

        [UnityTest] public IEnumerator EntriesStayLocalAndFallbackWorksOnRepeat()
        {
            var second = Object.Instantiate(terminal, terminal.transform.position + Vector3.right * 6, Quaternion.identity);
            second.title.text = "SECOND VERIFICATION ENTRY";
            second.lore.text = "Independent text, using the same frame.";
            terminal.title.text = "FIRST VERIFICATION ENTRY";
            terminal.lore.text = "Short entry.";
            Assert.IsTrue(terminal.TryOpen()); yield return null; yield return null;
            Assert.IsFalse(terminal.emptyEntry.gameObject.activeInHierarchy);
            Assert.AreEqual("FIRST VERIFICATION ENTRY", terminal.title.text);
            Assert.IsFalse(second.overlay.activeInHierarchy);
            terminal.Close(); yield return null; yield return null;
            actor.transform.position = second.transform.position;
            Assert.IsTrue(second.TryOpen()); yield return null; yield return null;
            Assert.AreEqual("SECOND VERIFICATION ENTRY", second.title.text);
            Assert.AreEqual("Independent text, using the same frame.", second.lore.text);
            Assert.IsFalse(terminal.overlay.activeInHierarchy);
            second.Close(); yield return null; yield return null;
            second.lore.text = "  ";
            Assert.IsTrue(second.TryOpen()); yield return null; yield return null;
            Assert.IsTrue(second.emptyEntry.gameObject.activeInHierarchy);
            second.Close(); yield return null; yield return null;
            second.lore = null;
            Assert.IsTrue(second.TryOpen()); yield return null; yield return null;
            Assert.IsTrue(second.emptyEntry.gameObject.activeInHierarchy);
            Assert.AreEqual(1, Object.FindObjectsByType<ResearchTerminal>(FindObjectsSortMode.None).Count(t => t.overlay.activeInHierarchy));
        }

        private const string Destination = "Assets/Game/Scenes/VerificationDestination.unity";
        private const string Level01 = "Assets/Game/Scenes/Levels/Level01.unity";

        [UnityTest] public IEnumerator HeldUpDoesNotJumpAgainAfterExit()
        { yield return CrossExitWith(Key.UpArrow); }

        [UnityTest] public IEnumerator HeldWDoesNotJumpAgainAfterExit()
        { yield return CrossExitWith(Key.W); }

        [UnityTest] public IEnumerator HeldSpaceDoesNotSpendAgainAfterExit()
        { yield return CrossExitWith(Key.Space); }

        [UnityTest] public IEnumerator HeldAContinuesWalkingAfterExit()
        { yield return CrossExitWith(Key.A); }

        [UnityTest] public IEnumerator HeldDContinuesWalkingAfterExit()
        { yield return CrossExitWith(Key.D); }

        [UnityTest] public IEnumerator ReleasedControlsLeaveDestinationIdle()
        { yield return CrossExitWith(null); }

        private IEnumerator CrossExitWith(Key? held)
        {
            var bank = RunDashBank.Instance;
            var exit = Object.FindObjectsByType<LevelExit>(FindObjectsSortMode.None)
                .Single(item => item.transition != null && item.transition.destinationScene == Destination);
            var exitCenter = exit.GetComponent<Collider2D>().bounds.center;
            int startingCharges = bank.RemainingCharges;
            if (held.HasValue) yield return QueueKeys(held.Value);
            else { yield return QueueKeys(Key.UpArrow, Key.Space); yield return QueueKeys(); }
            for (int tick = 0; tick < 12; tick++) yield return new WaitForFixedUpdate();
            if (held == Key.Space || !held.HasValue)
            {
                Assert.AreEqual(startingCharges - 1, bank.RemainingCharges, "The intentional source dash spends once before the exit.");
                Assert.IsFalse(actor.IsDashing);
            }
            if (held.HasValue) Assert.IsTrue(keyboard[held.Value].isPressed);
            var sourceBody = actor.GetComponent<Rigidbody2D>();
            sourceBody.position = exitCenter; // Fixture starts in the real exit trigger.
            sourceBody.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
            int before = bank.RemainingCharges;
            yield return WaitForScene(Destination);
            Assert.AreSame(bank, RunDashBank.Instance);
            if (held.HasValue) Assert.IsTrue(keyboard[held.Value].isPressed, "The control must stay held through scene load.");
            yield return ObserveEntry(held, before);
        }

        [UnityTest] public IEnumerator NewGameAndRestartKeepCanonicalSceneAndFreshInput()
        {
            RunDashBank.Instance.TryFailEnvironment();
            yield return null; yield return null;
            var failure = Object.FindFirstObjectByType<GameOverController>();
            Assert.IsTrue(failure.TryRestartRun());
            yield return WaitForScene(Level01);
            Assert.AreEqual(5, RunDashBank.Instance.RemainingCharges);
            yield return SceneManager.LoadSceneAsync("Assets/Game/Scenes/MainMenu.unity");
            yield return QueueKeys(Key.UpArrow, Key.Space);
            var launcher = Object.FindFirstObjectByType<NewGameLauncher>();
            Assert.IsTrue(launcher.TryStartNewGame());
            yield return WaitForScene(NarrativePrologue.ScenePath);
            var intro = Object.FindFirstObjectByType<NarrativePrologue>();
            Assert.IsTrue(keyboard.upArrowKey.isPressed && keyboard.spaceKey.isPressed);
            Assert.IsFalse(intro.InputArmed);
            yield return QueueKeys();
            yield return new WaitUntil(() => intro.InputArmed, System.TimeSpan.FromSeconds(6),
                () => Assert.Fail("Prologue did not rearm after release."));
            yield return QueueKeys(Key.UpArrow, Key.Space);
            Assert.IsTrue(intro.TrySkip());
            yield return new WaitForSecondsRealtime(.35f);
            Assert.AreEqual(NarrativePrologue.ScenePath, SceneManager.GetActiveScene().path);
            yield return QueueKeys();
            yield return WaitForScene(Level01);
            yield return ObserveEntry(null, 5, true);
        }

        private IEnumerator ObserveEntry(Key? held, int expectedCharges, bool checkFreshControls = false)
        {
            var setup = Object.FindFirstObjectByType<LevelSetup>();
            var player = setup.Player.GetComponent<PlayerMovement2D>();
            var body = player.GetComponent<Rigidbody2D>();
            float peakUp = Mathf.Max(0, body.linearVelocity.y);
            float startX = setup.spawn.position.x;
            bool dashed = player.IsDashing;
            for (int tick = 0; tick < 12; tick++)
            {
                yield return new WaitForFixedUpdate();
                peakUp = Mathf.Max(peakUp, body.linearVelocity.y);
                dashed |= player.IsDashing;
            }
            float travel = body.position.x - startX;
            int charges = RunDashBank.Instance.RemainingCharges;
            Debug.Log($"Entry {held?.ToString() ?? (checkFreshControls ? "New Game with Up and Space" : "neutral")}: peak upward velocity {peakUp:F3}, horizontal travel {travel:F3}, dash {dashed}, charges {charges}/{expectedCharges}");
            Assert.Less(peakUp, .1f, "A held control must not start an airborne jump.");
            Assert.IsFalse(dashed, "A held control must not start a new dash.");
            Assert.AreEqual(expectedCharges, charges);
            if (held == Key.A) Assert.Less(travel, -.3f);
            else if (held == Key.D) Assert.Greater(travel, .3f);
            else Assert.AreEqual(0, travel, .05f);
            yield return QueueKeys();
            if (held == Key.UpArrow || held == Key.W || checkFreshControls)
            {
                Assert.IsTrue(player.IsGrounded);
                yield return QueueKeys(held ?? Key.W);
                bool jumped = body.linearVelocity.y > 2;
                for (int tick = 0; tick < 4 && !jumped; tick++)
                { yield return new WaitForFixedUpdate(); jumped = body.linearVelocity.y > 2; }
                Assert.IsTrue(jumped, "A fresh jump after release must work.");
                yield return QueueKeys();
            }
            if (held == Key.Space || checkFreshControls)
            {
                yield return QueueKeys(Key.Space);
                for (int tick = 0; tick < 4 && RunDashBank.Instance.RemainingCharges == expectedCharges; tick++)
                    yield return new WaitForFixedUpdate();
                Assert.AreEqual(expectedCharges - 1, RunDashBank.Instance.RemainingCharges, "A fresh dash spends once.");
                yield return QueueKeys();
            }
        }

        private IEnumerator QueueKeys(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            yield return null; yield return null;
        }

        private static IEnumerator WaitForScene(string path)
        {
            float deadline = Time.realtimeSinceStartup + 8;
            while (SceneManager.GetActiveScene().path != path && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(path, SceneManager.GetActiveScene().path);
        }
    }
}
#endif
