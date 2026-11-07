#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using GameJam.Movement;
using GameJam.Resources;
using GameJam.UI.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GameJam.UI.Tests
{
    public sealed class PauseTests
    {
        private const string Level = "Assets/Game/Scenes/Levels/Level01.unity";
        private PauseController pause;
        private PlayerMovement2D actor;
        private Rigidbody2D body;
        private RunDashBank bank;
        private Keyboard keyboard;
        private Gamepad gamepad;
        private Mouse mouse;
        private InputSettings previous, settings;
        private string previousJson;
        private HideFlags previousFlags;

        [UnitySetUp] public IEnumerator Setup()
        {
            Assert.IsNull(RunDashBank.Instance);
            previous = InputSystem.settings; previousJson = JsonUtility.ToJson(previous); previousFlags = previous.hideFlags;
            if (previousFlags == HideFlags.HideAndDontSave) previous.hideFlags |= HideFlags.HideInInspector;
            settings = Object.Instantiate(previous); settings.hideFlags = HideFlags.HideAndDontSave;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = settings; keyboard = InputSystem.AddDevice<Keyboard>();
            yield return Load(Level);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1;
            if (RunDashBank.Instance != null) Object.Destroy(RunDashBank.Instance.gameObject);
            var old = SceneManager.GetActiveScene(); SceneManager.SetActiveScene(SceneManager.CreateScene("Pause test cleanup"));
            if (old.IsValid() && old.isLoaded) yield return SceneManager.UnloadSceneAsync(old);
            foreach (var device in new InputDevice[] { keyboard, gamepad, mouse }) if (device != null && device.added) InputSystem.RemoveDevice(device);
            keyboard = null; gamepad = null; mouse = null;
            if (previous == null) { previous = ScriptableObject.CreateInstance<InputSettings>(); JsonUtility.FromJsonOverwrite(previousJson, previous); }
            InputSystem.settings = previous; previous.hideFlags = previousFlags; Object.Destroy(settings);
            yield return null; LogAssert.NoUnexpectedReceived();
        }
        private IEnumerator Load(string path)
        {
            EditorSceneManager.LoadSceneInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; yield return null; Refresh();
        }
        private void Refresh()
        {
            pause = Object.FindFirstObjectByType<PauseController>(); Assert.NotNull(pause);
            pause.SendMessage("OnApplicationFocus", true);
            actor = Object.FindFirstObjectByType<PlayerMovement2D>(); body = actor.GetComponent<Rigidbody2D>(); bank = RunDashBank.Instance;
            body.gravityScale = 0; body.linearVelocity = Vector2.zero;
        }
        private IEnumerator Paused()
        {
            Assert.IsTrue(pause.TryPause()); yield return null; yield return null;
            Assert.IsTrue(pause.InputArmed); Assert.AreEqual(0, Time.timeScale);
        }
        private IEnumerator PressKey(Key value)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(value)); yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; yield return null;
        }
        private IEnumerator Pad(GamepadButton value)
        {
            if (gamepad == null) gamepad = InputSystem.AddDevice<Gamepad>();
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(value)); yield return null; yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null; yield return null;
        }
        private IEnumerator WaitForLoad(string path, int oldHandle)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 6;
            while ((SceneManager.GetActiveScene().handle == oldHandle || SceneManager.GetActiveScene().path != path) &&
                Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.AreNotEqual(oldHandle, SceneManager.GetActiveScene().handle); Assert.AreEqual(path, SceneManager.GetActiveScene().path);
            yield return null; yield return null;
        }
        private int GameOverHandlers()
        {
            var handlers = (Delegate)typeof(RunDashBank).GetField("GameOver", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(bank);
            return handlers?.GetInvocationList().Length ?? 0;
        }
        [UnityTest] public IEnumerator EscapeOpensAndResumesRoot()
        { yield return PressKey(Key.Escape); Assert.IsTrue(pause.IsPaused); yield return PressKey(Key.Escape); Assert.IsFalse(pause.IsPaused); Assert.AreEqual(1, Time.timeScale); }
        [UnityTest] public IEnumerator GamepadStartOpensAndResumesRoot()
        { yield return Pad(GamepadButton.Start); Assert.IsTrue(pause.IsPaused); yield return Pad(GamepadButton.Start); Assert.IsFalse(pause.IsPaused); }
        [UnityTest] public IEnumerator HeldToggleDoesNotOscillate()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); yield return new WaitForSecondsRealtime(.15f);
            Assert.IsTrue(pause.IsPaused); Assert.IsFalse(pause.InputArmed);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; yield return null; Assert.IsTrue(pause.InputArmed);
        }
        [UnityTest] public IEnumerator PauseStopsPhysicsAndScaledTime()
        {
            body.linearVelocity = new Vector2(4, 3); yield return Paused();
            Vector2 position = body.position; float scaled = Time.time; float step = Time.fixedDeltaTime;
            yield return new WaitForSecondsRealtime(.15f);
            Assert.AreEqual(position, body.position); Assert.AreEqual(scaled, Time.time); Assert.AreEqual(step, Time.fixedDeltaTime);
            Assert.IsTrue(actor.enabled); Assert.IsTrue(body.simulated); Assert.IsFalse(actor.GetComponent<PlayerMovementInput2D>().enabled);
        }
        [UnityTest] public IEnumerator ResumeRestoresWalkingWithoutChangingBalance()
        {
            bank.TrySpendDash(); int charges = bank.RemainingCharges; yield return Paused();
            Assert.IsTrue(pause.TryResume()); yield return null; yield return null;
            Assert.IsTrue(actor.GetComponent<PlayerMovementInput2D>().enabled); Assert.AreEqual(charges, bank.RemainingCharges);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D)); yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.Greater(body.linearVelocity.x, 0);
        }
        [UnityTest] public IEnumerator DashAndJumpWhilePausedDoNotSpendOrBuffer()
        {
            yield return Paused(); int charges = bank.RemainingCharges;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space, Key.W)); yield return new WaitForSecondsRealtime(.1f);
            actor.RequestDash(Vector2.up); actor.RequestJump(); Assert.AreEqual(charges, bank.RemainingCharges);
            Assert.IsTrue(pause.TryResume()); yield return new WaitForFixedUpdate();
            Assert.AreEqual(charges, bank.RemainingCharges); Assert.IsFalse(actor.IsDashing); Assert.AreEqual(0, body.linearVelocity.y);
            Assert.IsFalse(actor.GetComponent<PlayerMovementInput2D>().enabled);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; yield return null; yield return new WaitForFixedUpdate();
            Assert.IsTrue(actor.GetComponent<PlayerMovementInput2D>().enabled); Assert.AreEqual(charges, bank.RemainingCharges);
        }
        [UnityTest] public IEnumerator HeldSubmitCannotCauseGameplayAfterResume()
        {
            yield return Paused(); InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter, Key.Space));
            yield return null; yield return null; Assert.IsFalse(pause.IsPaused); Assert.IsTrue(pause.WaitingForGameplayRelease);
            Assert.IsFalse(actor.GetComponent<PlayerMovementInput2D>().enabled); Assert.AreEqual(5, bank.RemainingCharges);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; yield return null; Assert.IsFalse(pause.WaitingForGameplayRelease);
        }
        [UnityTest] public IEnumerator ActiveDashRetainsStateAndCompletesAfterResume()
        {
            actor.GetComponent<PlayerMovementInput2D>().enabled = false; actor.RequestDash(Vector2.up); yield return new WaitForFixedUpdate();
            Assert.IsTrue(actor.IsDashing); int charges = bank.RemainingCharges;
            var timer = typeof(PlayerMovement2D).GetField("dashSecondsLeft", BindingFlags.Instance | BindingFlags.NonPublic);
            yield return Paused(); float remaining = (float)timer.GetValue(actor); Vector2 position = body.position;
            yield return new WaitForSecondsRealtime(.15f); Assert.AreEqual(remaining, (float)timer.GetValue(actor)); Assert.AreEqual(position, body.position);
            Assert.IsTrue(pause.TryResume()); for (int i = 0; i < 20 && actor.IsDashing; i++) yield return new WaitForFixedUpdate();
            Assert.IsFalse(actor.IsDashing); Assert.AreEqual(0, body.gravityScale); Assert.AreEqual(charges, bank.RemainingCharges);
        }
        [UnityTest] public IEnumerator PauseDoesNotRearmAirborneDashEligibility()
        {
            actor.GetComponent<PlayerMovementInput2D>().enabled = false; actor.RequestDash(Vector2.up);
            for (int i = 0; i < 15; i++) yield return new WaitForFixedUpdate();
            int charges = bank.RemainingCharges; yield return Paused(); pause.TryResume(); yield return null; yield return null;
            actor.RequestDash(Vector2.up); yield return new WaitForFixedUpdate(); Assert.AreEqual(charges, bank.RemainingCharges); Assert.IsFalse(actor.IsDashing);
        }
        [UnityTest] public IEnumerator DefaultFocusAndOnlyOneUIOwner()
        {
            yield return Paused(); Assert.AreEqual(pause.resumeButton.gameObject, EventSystem.current.currentSelectedGameObject);
            Assert.AreEqual(1, Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Count(e => e.isActiveAndEnabled));
            Assert.IsTrue(pause.inputScope.RuntimeActions.FindActionMap("UI").enabled); Assert.IsFalse(pause.inputScope.RuntimeActions.FindActionMap("Player").enabled);
        }
        [UnityTest] public IEnumerator KeyboardNavigatesControlsWhileTimeIsZero()
        {
            yield return Paused(); yield return PressKey(Key.DownArrow); Assert.AreEqual(pause.controlsButton.gameObject, EventSystem.current.currentSelectedGameObject);
            yield return PressKey(Key.Enter); Assert.IsTrue(pause.ControlsOpen); Assert.AreEqual(0, Time.timeScale);
            yield return PressKey(Key.Escape); Assert.IsFalse(pause.ControlsOpen); Assert.IsTrue(pause.IsPaused);
            Assert.AreEqual(pause.controlsButton.gameObject, EventSystem.current.currentSelectedGameObject);
        }
        [UnityTest] public IEnumerator GamepadNavigatesAndBackDoesNotResume()
        {
            yield return Paused(); yield return Pad(GamepadButton.DpadDown); yield return Pad(GamepadButton.South);
            Assert.IsTrue(pause.ControlsOpen); yield return Pad(GamepadButton.East); Assert.IsFalse(pause.ControlsOpen); Assert.IsTrue(pause.IsPaused);
            yield return Pad(GamepadButton.East); Assert.IsTrue(pause.IsPaused);
        }
        [UnityTest] public IEnumerator StartDoesNotResumeFromControls()
        { yield return Paused(); pause.OpenControls(); yield return Pad(GamepadButton.Start); Assert.IsTrue(pause.ControlsOpen); Assert.IsTrue(pause.IsPaused); }
        [UnityTest] public IEnumerator ControlsBackButtonReturnsFocus()
        {
            yield return Paused(); pause.controlsButton.onClick.Invoke(); Assert.IsTrue(pause.ControlsOpen);
            pause.backButton.onClick.Invoke(); Assert.IsFalse(pause.ControlsOpen); Assert.AreEqual(pause.controlsButton.gameObject, EventSystem.current.currentSelectedGameObject); Assert.AreEqual(0, Time.timeScale);
        }
        [UnityTest] public IEnumerator MouseCanResume()
        {
            yield return Paused(); mouse = InputSystem.AddDevice<Mouse>(); Canvas.ForceUpdateCanvases();
            var rect = pause.resumeButton.GetComponent<RectTransform>(); Vector2 point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point, buttons = 1 }); yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null; yield return null;
            Assert.IsFalse(pause.IsPaused); Assert.AreEqual(1, Time.timeScale);
        }
        [UnityTest] public IEnumerator DuplicatePauseAndResumeRequestsAreRejected()
        {
            yield return Paused(); Assert.IsFalse(pause.TryPause()); Assert.IsTrue(pause.TryResume()); Assert.IsFalse(pause.TryResume());
            Assert.IsFalse(pause.TryPause()); yield return null; yield return null;
            Assert.AreEqual(1, Object.FindObjectsByType<PauseController>(FindObjectsSortMode.None).Length);
        }
        [UnityTest] public IEnumerator ReturnPreservesBalanceAndNewGameWorks()
        {
            bank.TrySpendDash(); bank.TrySpendDash(); yield return Paused(); var old = pause; var actions = pause.inputScope.RuntimeActions;
            int handle = SceneManager.GetActiveScene().handle; Assert.IsTrue(pause.TryReturnToMainMenu()); Assert.IsFalse(pause.TryReturnToMainMenu());
            Assert.AreEqual(1, Time.timeScale); yield return WaitForLoad(MenuBuilder.ScenePath, handle);
            Assert.AreEqual(3, bank.RemainingCharges); Assert.IsFalse(bank.IsGameOver); Assert.IsTrue(old == null && actions == null);
            Assert.IsNull(Object.FindFirstObjectByType<PauseController>()); Assert.AreEqual(1, Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length);
            var menu = Object.FindFirstObjectByType<MainMenuView>(); handle = SceneManager.GetActiveScene().handle; menu.newGameButton.onClick.Invoke();
            yield return WaitForLoad(NarrativePrologue.ScenePath, handle);
            var intro = Object.FindFirstObjectByType<NarrativePrologue>();
            yield return new WaitUntil(() => intro.InputArmed, System.TimeSpan.FromSeconds(6),
                () => Assert.Fail("Prologue did not accept input."));
            Assert.IsTrue(intro.TrySkip());
            yield return WaitForLoad(Level, handle); Assert.AreEqual(5, bank.RemainingCharges); Assert.AreEqual(1, Time.timeScale);
        }
        [UnityTest] public IEnumerator InvalidMenuDestinationKeepsSafePausedAlternatives()
        {
            yield return Paused(); pause.mainMenuScene = "Missing Pause Destination"; Assert.IsFalse(pause.TryReturnToMainMenu());
            Assert.IsTrue(pause.IsPaused); Assert.IsFalse(pause.IsNavigating); Assert.AreEqual(0, Time.timeScale); Assert.IsNotEmpty(pause.status.text);
            Assert.IsTrue(pause.TryResume());
        }
        [UnityTest] public IEnumerator FailureWhilePausedHandsOffToGameOverAndRestart()
        {
            yield return Paused(); while (bank.RemainingCharges > 0) bank.TrySpendDash();
            Assert.IsFalse(body.simulated); Assert.IsFalse(actor.enabled);
            yield return null; yield return null;
            var failure = Object.FindFirstObjectByType<GameOverController>(); Assert.IsTrue(failure.IsVisible); Assert.IsFalse(pause.IsPaused); Assert.AreEqual(1, Time.timeScale);
            Assert.IsFalse(pause.overlay.activeSelf); Assert.IsFalse(pause.TryPause()); Assert.IsFalse(pause.TryResume());
            yield return PressKey(Key.Escape); Assert.IsTrue(failure.IsVisible); Assert.AreEqual(0, bank.RemainingCharges);
            int handle = SceneManager.GetActiveScene().handle; Assert.IsTrue(failure.TryRestartRun()); yield return WaitForLoad(Level, handle);
            Refresh(); Assert.AreEqual(1, Time.timeScale); Assert.AreEqual(5, bank.RemainingCharges); Assert.IsTrue(body.simulated);
            Assert.AreEqual(1, Object.FindObjectsByType<PauseController>(FindObjectsSortMode.None).Length);
        }
        [UnityTest] public IEnumerator FinalDashAfterResumeStillHasNoTravel()
        {
            yield return Paused(); pause.TryResume(); yield return null; yield return null;
            actor.GetComponent<PlayerMovementInput2D>().enabled = false; while (bank.RemainingCharges > 1) bank.TrySpendDash();
            Vector2 before = body.position; actor.RequestDash(Vector2.up); yield return new WaitForFixedUpdate();
            Assert.AreEqual(before, body.position); Assert.AreEqual(0, bank.RemainingCharges); Assert.IsFalse(body.simulated);
            yield return null; Assert.IsFalse(pause.TryPause());
        }
        [UnityTest] public IEnumerator RestartTransitionCannotOpenPause()
        {
            while (bank.RemainingCharges > 0) bank.TrySpendDash();
            yield return null; yield return null;
            var failure = Object.FindFirstObjectByType<GameOverController>();
            int handle = SceneManager.GetActiveScene().handle;
            Assert.IsTrue(failure.TryRestartRun());
            Assert.IsFalse(bank.IsGameOver); // Reset precedes asynchronous replacement of the failed scene.
            bool openedDuringRestart = pause.TryPause();
            yield return WaitForLoad(Level, handle);
            Assert.IsFalse(openedDuringRestart, "The visible/navigating failure UI must retain priority after the bank reset.");
            Assert.AreEqual(1, Time.timeScale);
        }
        [UnityTest] public IEnumerator ThreeCyclesDoNotAccumulateUIOrSubscriptions()
        {
            int handlers = GameOverHandlers();
            for (int i = 0; i < 3; i++)
            {
                yield return Paused(); var actions = pause.inputScope.RuntimeActions; pause.TryResume(); yield return null; yield return null;
                Assert.IsTrue(actions == null); Assert.AreEqual(handlers, GameOverHandlers()); Assert.AreEqual(5, bank.RemainingCharges);
                Assert.AreEqual(0, Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Count(e => e.isActiveAndEnabled));
            }
        }
        [UnityTest] public IEnumerator PausedSceneUnloadRestoresTimeAndFreshScene()
        {
            bank.TrySpendDash(); yield return Paused(); var old = pause;
            yield return Load("Assets/Game/Scenes/VerificationDestination.unity");
            Assert.IsTrue(old == null); Assert.AreEqual(1, Time.timeScale); Assert.AreEqual(4, bank.RemainingCharges);
            Assert.IsFalse(pause.IsPaused); Assert.IsTrue(actor.GetComponent<PlayerMovementInput2D>().enabled); yield return Paused();
        }
        [UnityTest] public IEnumerator ExperimentalSceneSupportsPauseAndControls()
        {
            yield return Load("Assets/Game/Experiments/SingleScreen/Scenes/SingleScreen32x18.unity");
            yield return Paused(); pause.OpenControls(); Assert.IsTrue(pause.ControlsOpen); pause.CloseControls(); Assert.IsTrue(pause.TryResume());
        }
        [UnityTest] public IEnumerator BootstrapIsIdempotentAndDoesNotAddBanks()
        {
            for (int i = 0; i < 5; i++) PauseSceneBootstrap.Install(SceneManager.GetActiveScene());
            Assert.AreEqual(1, Object.FindObjectsByType<PauseController>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(1, Object.FindObjectsByType<RunDashBank>(FindObjectsSortMode.None).Length); yield return null;
        }
        [UnityTest] public IEnumerator DisableRestoresTimeInputsAndCanReenable()
        {
            yield return Paused(); pause.enabled = false; Assert.AreEqual(1, Time.timeScale); Assert.IsTrue(actor.GetComponent<PlayerMovementInput2D>().enabled);
            Assert.IsFalse(pause.overlay.activeSelf); pause.enabled = true; yield return null; yield return PressKey(Key.Escape); Assert.IsTrue(pause.IsPaused);
        }
        [UnityTest] public IEnumerator FocusLossKeepsPauseAndRequiresReleaseAfterReturn()
        {
            yield return Paused(); pause.SendMessage("OnApplicationFocus", false); Assert.IsFalse(pause.InputArmed); Assert.AreEqual(0, Time.timeScale);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space)); pause.SendMessage("OnApplicationFocus", true); yield return null; Assert.IsFalse(pause.InputArmed);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; yield return null; Assert.IsTrue(pause.InputArmed); Assert.IsTrue(pause.IsPaused);
        }
        [UnityTest] public IEnumerator DeviceRemovalDoesNotLeaveHeldInputOrDuplicateOwners()
        {
            gamepad = InputSystem.AddDevice<Gamepad>(); InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.Start));
            yield return null; yield return null; Assert.IsTrue(pause.IsPaused); Assert.IsFalse(pause.InputArmed);
            InputSystem.RemoveDevice(gamepad); gamepad = null; yield return null; yield return null;
            Assert.IsTrue(pause.InputArmed); gamepad = InputSystem.AddDevice<Gamepad>(); yield return Pad(GamepadButton.Start); Assert.IsFalse(pause.IsPaused);
        }
        [UnityTest] public IEnumerator OtherUIOwnerIsSuspendedAndRestored()
        {
            var menu = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(MenuBuilder.PrefabPath)); yield return null;
            var other = menu.GetComponentInChildren<EventSystem>(); var scope = other.GetComponent<MenuInputScope>();
            yield return Paused(); Assert.IsFalse(other.enabled); Assert.IsFalse(scope.enabled);
            pause.TryResume(); yield return null; yield return null; Assert.IsTrue(other.enabled); Assert.IsTrue(scope.enabled);
            Object.Destroy(menu); yield return null;
        }
        [Test] public void PrefabIsReusableAndAuthoredInactive()
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(PauseBuilder.PrefabPath).GetComponent<PauseController>();
            Assert.IsFalse(p.overlay.activeSelf); Assert.IsFalse(p.controlsView.activeSelf); Assert.IsFalse(p.uiEventSystem.gameObject.activeSelf);
            Assert.AreEqual(90, p.overlay.GetComponent<Canvas>().sortingOrder); Assert.IsFalse(p.inputScope.enabled); Assert.NotNull(p.resumeButton);
        }
    }
}
#endif
