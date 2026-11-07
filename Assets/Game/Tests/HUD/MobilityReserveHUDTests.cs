#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using GameJam.Levels;
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
    public sealed class MobilityReserveHUDTests
    {
        private const string Level = "Assets/Game/Scenes/Levels/Level01.unity";
        private const string Destination = "Assets/Game/Scenes/VerificationDestination.unity";
        private MobilityReserveHUDController hud;
        private RunDashBank bank;
        private PlayerMovement2D actor;
        private Rigidbody2D body;
        private InputSettings previous, settings;
        private string previousJson;
        private HideFlags previousFlags;
        private Keyboard keyboard;
        private Gamepad gamepad;
        private Mouse mouse;

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
            var old = SceneManager.GetActiveScene(); SceneManager.SetActiveScene(SceneManager.CreateScene("HUD test cleanup"));
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
            hud = Object.FindFirstObjectByType<MobilityReserveHUDController>(); Assert.NotNull(hud);
            bank = RunDashBank.Instance; actor = Object.FindFirstObjectByType<PlayerMovement2D>(); body = actor.GetComponent<Rigidbody2D>();
            actor.GetComponent<PlayerMovementInput2D>().enabled = false; body.gravityScale = 0; body.linearVelocity = Vector2.zero;
            Object.FindFirstObjectByType<PauseController>().SendMessage("OnApplicationFocus", true);
        }
        private void SpendTo(int remaining) { while (bank.RemainingCharges > remaining) bank.TrySpendDash(); }
        private int Subscribers(string name)
        {
            var handlers = (Delegate)typeof(RunDashBank).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(bank);
            return handlers?.GetInvocationList().Count(d => d.Target == hud) ?? 0;
        }
        private void AssertDisplay(int count)
        {
            Assert.AreEqual(bank, hud.BoundBank); Assert.AreEqual(count, hud.DisplayedCharges); Assert.AreEqual(count.ToString("D2"), hud.counter.text);
            Assert.AreEqual(bank.StartingDashCount, hud.Segments.Count);
            for (int i = 0; i < hud.Segments.Count; i++)
                Assert.AreEqual(i >= count ? hud.inactiveColor : (count == 1 ? hud.criticalColor : hud.activeColor), hud.Segments[i].color, "Segment " + i);
        }
        private IEnumerator WaitForLoad(string path, int oldHandle)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 6;
            while ((SceneManager.GetActiveScene().handle == oldHandle || SceneManager.GetActiveScene().path != path) &&
                Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.AreNotEqual(oldHandle, SceneManager.GetActiveScene().handle); Assert.AreEqual(path, SceneManager.GetActiveScene().path); yield return null; yield return null;
        }
        private IEnumerator Fail()
        {
            SpendTo(1); actor.RequestDash(Vector2.up); yield return new WaitForFixedUpdate(); yield return null; yield return null;
            Assert.IsTrue(bank.IsGameOver); AssertDisplay(0);
        }
        private void SetAllocation(int count)
        {
            var serialized = new SerializedObject(bank); serialized.FindProperty("startingDashCount").intValue = count;
            serialized.ApplyModifiedPropertiesWithoutUndo(); bank.InitializeNewRun();
        }
        [UnityTest] public IEnumerator DirectEntryBindsAuthoritativeStartingBalance()
        { AssertDisplay(5); Assert.AreEqual(5, hud.DisplayedStartingCount); Assert.IsEmpty(hud.warning.text); yield return null; }
        [UnityTest] public IEnumerator FiveChargesHaveFiveTealSegments()
        { Assert.AreEqual(5, hud.Segments.Count); Assert.IsTrue(hud.Segments.All(i => i.color == hud.activeColor)); yield return null; }
        [UnityTest] public IEnumerator SpendingUpdatesSynchronouslyInTheResourceCallback()
        {
            bool observed = false; bank.ChargesChanged += count => { observed = true; Assert.AreEqual(count, hud.DisplayedCharges); };
            bank.TrySpendDash(); Assert.IsTrue(observed); AssertDisplay(4); yield return null;
        }
        [UnityTest] public IEnumerator ThreeAndTwoChargesHaveMatchingSegmentsAndNumerals()
        { SpendTo(3); AssertDisplay(3); SpendTo(2); AssertDisplay(2); Assert.IsEmpty(hud.warning.text); yield return null; }
        [UnityTest] public IEnumerator OneChargeShowsMandatoryWarningAndCriticalStyling()
        {
            SpendTo(1); AssertDisplay(1); Assert.IsTrue(hud.IsCritical); Assert.AreEqual(hud.criticalColor, hud.counter.color);
            Assert.AreEqual("NEXT DASH WILL TERMINATE RUN", hud.warning.text); yield return null;
        }
        [UnityTest] public IEnumerator FatalDashStillHasNoTravelAndDisplaysZero()
        {
            SpendTo(1); Vector2 before = body.position; actor.RequestDash(Vector2.up); yield return new WaitForFixedUpdate();
            AssertDisplay(0); Assert.AreEqual(before, body.position); Assert.IsFalse(body.simulated); Assert.IsFalse(actor.enabled);
            Assert.AreEqual("ALLOCATION EXHAUSTED", hud.warning.text); Assert.IsFalse(hud.IsCritical);
        }
        [UnityTest] public IEnumerator GameOverKeepsVisualAndInputPriority()
        {
            yield return Fail(); var failure = Object.FindFirstObjectByType<GameOverController>(); Assert.IsTrue(failure.IsVisible);
            Assert.Greater(failure.overlay.GetComponent<Canvas>().sortingOrder, hud.GetComponent<Canvas>().sortingOrder);
            Assert.IsTrue(hud.gameObject.activeInHierarchy); Assert.AreEqual(1, Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Count(e => e.isActiveAndEnabled));
            Assert.IsFalse(Object.FindFirstObjectByType<PauseController>().TryPause());
        }
        [UnityTest] public IEnumerator RestartReplacesHUDAndRestoresAllocation()
        {
            yield return Fail(); var old = hud; var failure = Object.FindFirstObjectByType<GameOverController>(); int handle = SceneManager.GetActiveScene().handle;
            Assert.IsTrue(failure.TryRestartRun()); yield return WaitForLoad(Level, handle); Refresh(); AssertDisplay(5);
            Assert.IsTrue(old == null); Assert.AreEqual(1, Subscribers("ChargesChanged")); Assert.AreEqual(1, Subscribers("NewRunStarted"));
        }
        [UnityTest] public IEnumerator ExplicitNewRunUpdatesExistingHUDImmediately()
        { SpendTo(2); var before = hud; bank.InitializeNewRun(); AssertDisplay(5); Assert.AreSame(before, hud); yield return null; }
        [UnityTest] public IEnumerator PauseAndResumeKeepDisplayAndResourcesUnchanged()
        {
            SpendTo(3); var pause = Object.FindFirstObjectByType<PauseController>(); Assert.IsTrue(pause.TryPause()); yield return null; yield return null;
            Assert.AreEqual(0, Time.timeScale); AssertDisplay(3); Assert.IsTrue(hud.gameObject.activeInHierarchy);
            Assert.Less(hud.GetComponent<Canvas>().sortingOrder, pause.overlay.GetComponent<Canvas>().sortingOrder);
            Assert.IsTrue(pause.TryResume()); yield return null; AssertDisplay(3); Assert.AreEqual(1, Time.timeScale);
        }
        [UnityTest] public IEnumerator ResourceEventsStillRenderWhileTimeIsZero()
        {
            var pause = Object.FindFirstObjectByType<PauseController>(); pause.TryPause(); yield return null;
            // Explicit fixture mutation verifies observation at time zero, not gameplay spending.
            bank.TrySpendDash(); AssertDisplay(4); Assert.AreEqual(0, Time.timeScale);
        }
        [UnityTest] public IEnumerator AcceptedLevelTransitionPreservesBankAndDisplay()
        {
            const string nextLevel = "Assets/Game/Scenes/Levels/Level02.unity";
            SpendTo(3); var originalBank = bank; var old = hud; int handle = SceneManager.GetActiveScene().handle;
            var exit = Object.FindFirstObjectByType<LevelTransition>(); Assert.AreEqual(nextLevel, exit.destinationScene); Assert.IsTrue(exit.TryExit(actor));
            yield return WaitForLoad(nextLevel, handle); Refresh(); AssertDisplay(3); Assert.AreSame(originalBank, bank); Assert.IsTrue(old == null);
        }
        [UnityTest] public IEnumerator RepeatedSceneLoadsDoNotAccumulateHUDsOrSubscriptions()
        {
            SpendTo(4);
            foreach (var path in new[] { Destination, Level, Destination, Level })
            {
                yield return Load(path); AssertDisplay(4);
                Assert.AreEqual(1, Object.FindObjectsByType<MobilityReserveHUDController>(FindObjectsSortMode.None).Length);
                Assert.AreEqual(1, Subscribers("ChargesChanged")); Assert.AreEqual(1, Subscribers("NewRunStarted"));
            }
        }
        [UnityTest] public IEnumerator PauseReturnRemovesHUDWithoutRefillAndNewGameReinitializes()
        {
            SpendTo(3); var old = hud; var pause = Object.FindFirstObjectByType<PauseController>(); pause.TryPause(); yield return null; yield return null;
            int handle = SceneManager.GetActiveScene().handle; Assert.IsTrue(pause.TryReturnToMainMenu()); yield return WaitForLoad(MenuBuilder.ScenePath, handle);
            Assert.IsTrue(old == null); Assert.IsNull(Object.FindFirstObjectByType<MobilityReserveHUDController>()); Assert.AreEqual(3, bank.RemainingCharges);
            Assert.AreEqual(0, Subscribers("ChargesChanged")); Assert.AreEqual(0, Subscribers("NewRunStarted"));
            handle = SceneManager.GetActiveScene().handle; Object.FindFirstObjectByType<MainMenuView>().newGameButton.onClick.Invoke();
            yield return WaitForLoad(NarrativePrologue.ScenePath, handle);
            var intro = Object.FindFirstObjectByType<NarrativePrologue>();
            yield return new WaitUntil(() => intro.InputArmed, System.TimeSpan.FromSeconds(6),
                () => Assert.Fail("Prologue did not accept input."));
            Assert.IsTrue(intro.TrySkip());
            yield return WaitForLoad(Level, handle); Refresh(); AssertDisplay(5);
        }
        [UnityTest] public IEnumerator FailureReturnRemovesHUDAndPreservesFailedZero()
        {
            yield return Fail(); var old = hud; var failure = Object.FindFirstObjectByType<GameOverController>(); int handle = SceneManager.GetActiveScene().handle;
            Assert.IsTrue(failure.TryReturnToMainMenu()); yield return WaitForLoad(MenuBuilder.ScenePath, handle);
            Assert.IsTrue(old == null); Assert.IsNull(Object.FindFirstObjectByType<MobilityReserveHUDController>()); Assert.AreEqual(0, bank.RemainingCharges); Assert.IsTrue(bank.IsGameOver);
        }
        [UnityTest] public IEnumerator BootstrapIsIdempotent()
        {
            for (int i = 0; i < 5; i++) MobilityReserveHUDBootstrap.Install(SceneManager.GetActiveScene());
            Assert.AreEqual(1, Object.FindObjectsByType<MobilityReserveHUDController>(FindObjectsSortMode.None).Length); AssertDisplay(5); yield return null;
        }
        [UnityTest] public IEnumerator DuplicateAuthoredHUDIsRemovedWithoutExtraSubscriptions()
        {
            var duplicate = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(MobilityReserveHUDBuilder.PrefabPath)); yield return null;
            Assert.IsTrue(duplicate == null); Assert.AreEqual(1, Object.FindObjectsByType<MobilityReserveHUDController>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(1, Subscribers("ChargesChanged")); AssertDisplay(5);
        }
        [UnityTest] public IEnumerator ExperimentalRoomSupportsTheSameReadOnlyHUD()
        { SpendTo(3); yield return Load("Assets/Game/Experiments/SingleScreen/Scenes/SingleScreen32x18.unity"); AssertDisplay(3); }
        [UnityTest] public IEnumerator MissingBankShowsOfflineWithoutCreatingResources()
        {
            Object.Destroy(bank.gameObject); yield return null; yield return null;
            Assert.IsNull(RunDashBank.Instance); Assert.IsNull(hud.BoundBank); Assert.AreEqual("--", hud.counter.text); Assert.AreEqual("MONITOR OFFLINE", hud.warning.text);
            Assert.AreEqual(0, hud.Segments.Count); Assert.AreEqual(0, Object.FindObjectsByType<RunDashBank>(FindObjectsSortMode.None).Length);
        }
        [UnityTest] public IEnumerator LateReplacementBankBindsItsOwnAllocation()
        {
            Object.Destroy(bank.gameObject); yield return null; yield return null;
            var owner = new GameObject("Explicit replacement bank fixture"); owner.SetActive(false); bank = owner.AddComponent<RunDashBank>();
            var serialized = new SerializedObject(bank); serialized.FindProperty("startingDashCount").intValue = 8; serialized.ApplyModifiedPropertiesWithoutUndo();
            owner.SetActive(true); yield return null; yield return null; AssertDisplay(8); Assert.AreEqual(1, Subscribers("ChargesChanged"));
        }
        [UnityTest] public IEnumerator TemporarilyDisabledBankRebindsWithoutRefill()
        {
            SpendTo(3); bank.enabled = false; yield return null; yield return null;
            Assert.IsNull(hud.BoundBank); Assert.AreEqual("--", hud.counter.text); Assert.AreEqual(3, bank.RemainingCharges);
            bank.enabled = true; yield return null; yield return null; AssertDisplay(3);
        }
        [UnityTest] public IEnumerator RepeatedEnableDisableDoesNotAccumulateSubscriptions()
        {
            for (int i = 0; i < 4; i++)
            {
                hud.enabled = false; Assert.AreEqual(0, Subscribers("ChargesChanged")); Assert.AreEqual(0, Subscribers("NewRunStarted"));
                hud.enabled = true; AssertDisplay(5); Assert.AreEqual(1, Subscribers("ChargesChanged")); Assert.AreEqual(1, Subscribers("NewRunStarted"));
            }
            Assert.AreEqual(0, Subscribers("GameOver")); yield return null;
        }
        [UnityTest] public IEnumerator ReenableReadsChargesSpentWhileDisabled()
        { hud.enabled = false; SpendTo(2); hud.enabled = true; AssertDisplay(2); yield return null; }
        [UnityTest] public IEnumerator OtherPositiveAllocationsCreateCorrectSegmentsWithoutClipping()
        {
            foreach (int count in new[] { 1, 3, 8, 17, 101 })
            {
                SetAllocation(count); AssertDisplay(count); yield return null; Canvas.ForceUpdateCanvases();
                Assert.IsTrue(hud.segmentLayout.cellSize.x > 0 && hud.segmentLayout.cellSize.y > 0);
                foreach (var image in hud.Segments)
                {
                    var corners = new Vector3[4]; image.rectTransform.GetWorldCorners(corners);
                    foreach (var point in corners)
                    {
                        var local = hud.segmentArea.InverseTransformPoint(point); var bounds = hud.segmentArea.rect;
                        Assert.IsTrue(local.x >= bounds.xMin - .01f && local.x <= bounds.xMax + .01f && local.y >= bounds.yMin - .01f && local.y <= bounds.yMax + .01f, "Segment outside compact area at " + count);
                    }
                }
            }
        }
        [UnityTest] public IEnumerator SingleStartingChargeIsImmediatelyCritical()
        { SetAllocation(1); AssertDisplay(1); Assert.AreEqual("NEXT DASH WILL TERMINATE RUN", hud.warning.text); yield return null; }
        [UnityTest] public IEnumerator RenderingNeverSpendsOrResetsResources()
        {
            SpendTo(2); int changed = 0, resets = 0, failures = 0;
            bank.ChargesChanged += _ => changed++; bank.NewRunStarted += () => resets++; bank.GameOver += () => failures++;
            for (int i = 0; i < 100; i++) hud.RefreshPresentation(); yield return null;
            Assert.AreEqual(2, bank.RemainingCharges); Assert.IsFalse(bank.IsGameOver); Assert.AreEqual(0, changed); Assert.AreEqual(0, resets); Assert.AreEqual(0, failures);
        }
        [UnityTest] public IEnumerator SpendingAndIdleFramesReuseTheSegmentObjects()
        {
            var ids = hud.Segments.Select(i => i.GetInstanceID()).ToArray(); SpendTo(2);
            for (int i = 0; i < 12; i++) yield return null;
            CollectionAssert.AreEqual(ids, hud.Segments.Select(i => i.GetInstanceID()).ToArray()); AssertDisplay(2);
        }
        [UnityTest] public IEnumerator LayoutColumnsCanBeConfiguredWithoutChangingAllocation()
        {
            hud.maximumColumns = 3; hud.RefreshPresentation(); Canvas.ForceUpdateCanvases();
            Assert.AreEqual(3, hud.segmentLayout.constraintCount); AssertDisplay(5); yield return null;
        }
        [UnityTest] public IEnumerator HUDHasNoInputOwnerOrPointerInterception()
        {
            Assert.IsEmpty(hud.GetComponentsInChildren<EventSystem>(true)); Assert.IsEmpty(hud.GetComponentsInChildren<MenuInputScope>(true));
            Assert.IsEmpty(hud.GetComponentsInChildren<UnityEngine.UI.GraphicRaycaster>(true)); Assert.IsEmpty(hud.GetComponentsInChildren<UnityEngine.UI.Selectable>(true));
            Assert.IsTrue(hud.GetComponentsInChildren<UnityEngine.UI.Graphic>(true).All(g => !g.raycastTarget));
            Assert.IsFalse(hud.GetComponent<CanvasGroup>().blocksRaycasts); Assert.IsFalse(hud.GetComponent<CanvasGroup>().interactable); yield return null;
        }
        [UnityTest] public IEnumerator KeyboardAndGamepadPauseNavigationStillWork()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; yield return null;
            var pause = Object.FindFirstObjectByType<PauseController>(); Assert.IsTrue(pause.IsPaused); AssertDisplay(5);
            gamepad = InputSystem.AddDevice<Gamepad>(); InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.DpadDown)); yield return null; yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null; yield return null;
            Assert.AreEqual(pause.controlsButton.gameObject, EventSystem.current.currentSelectedGameObject); AssertDisplay(5);
        }
        [UnityTest] public IEnumerator PointerCanResumeWhileHUDIsPresent()
        {
            var pause = Object.FindFirstObjectByType<PauseController>(); pause.TryPause(); yield return null; yield return null; mouse = InputSystem.AddDevice<Mouse>(); Canvas.ForceUpdateCanvases();
            var rect = pause.resumeButton.GetComponent<RectTransform>(); var point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point, buttons = 1 }); yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null; yield return null;
            Assert.IsFalse(pause.IsPaused); AssertDisplay(5);
        }
        [UnityTest] public IEnumerator ReferenceCanvasLayoutsStayOnScreenAtRequiredAspectRatios()
        {
            var parent = new GameObject("Inactive layout-only fixture"); parent.SetActive(false);
            var clone = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(MobilityReserveHUDBuilder.PrefabPath), parent.transform);
            var c = clone.GetComponent<MobilityReserveHUDController>(); var canvas = clone.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            c.enabled = false; parent.SetActive(true);
            var root = clone.GetComponent<RectTransform>();
            foreach (var size in new[] { new Vector2(1280, 720), new Vector2(1920, 1080), new Vector2(2560, 1440), new Vector2(1024, 768) })
            {
                // Same .5-match CanvasScaler equation; actual output dimensions are checked in standalone.
                float scale = Mathf.Sqrt((size.x / 1280f) * (size.y / 720f));
                root.sizeDelta = size / scale; Canvas.ForceUpdateCanvases();
                var corners = new Vector3[4]; c.panel.GetWorldCorners(corners);
                foreach (var point in corners) Assert.IsTrue(root.rect.Contains(root.InverseTransformPoint(point)), "Panel outside " + size);
                Assert.GreaterOrEqual(c.counter.fontSize * scale, 24); Assert.GreaterOrEqual(c.warning.fontSize * scale, 10);
            }
            Object.Destroy(parent); yield return null;
        }
        [Test] public void PrefabKeepsEditableReferencesAndSortingBelowModals()
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(MobilityReserveHUDBuilder.PrefabPath).GetComponent<MobilityReserveHUDController>();
            Assert.NotNull(p.panel.GetComponent<UnityEngine.UI.Image>()); Assert.NotNull(p.segmentTemplate); Assert.NotNull(p.counter); Assert.NotNull(p.warning);
            Assert.AreEqual(10, p.GetComponent<Canvas>().sortingOrder); Assert.AreEqual(new Vector2(0, 1), p.panel.anchorMin); Assert.IsFalse(p.segmentTemplate.gameObject.activeSelf);
        }
    }
}
#endif
