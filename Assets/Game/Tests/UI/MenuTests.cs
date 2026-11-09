#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using GameJam.Levels;
using GameJam.Resources;
using GameJam.UI.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace GameJam.UI.Tests
{
    public sealed class MenuTests
    {
        private MainMenuView view;
        private Keyboard keyboard;
        private Gamepad gamepad;
        private Mouse mouse;
        private InputSettings previous,testSettings;
        private string previousJson;
        private HideFlags previousFlags;
        [UnitySetUp] public IEnumerator Setup()
        {
            Assert.IsNull(RunDashBank.Instance);
            previous=InputSystem.settings;previousJson=JsonUtility.ToJson(previous);previousFlags=previous.hideFlags;
            if(previousFlags==HideFlags.HideAndDontSave)previous.hideFlags|=HideFlags.HideInInspector;
            testSettings=Object.Instantiate(previous);testSettings.hideFlags=HideFlags.HideAndDontSave;
            testSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            testSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings=testSettings;keyboard=InputSystem.AddDevice<Keyboard>();
            EditorSceneManager.LoadSceneInPlayMode(MenuBuilder.ScenePath,new LoadSceneParameters(LoadSceneMode.Single));yield return null;yield return null;
            view=Object.FindFirstObjectByType<MainMenuView>();Assert.NotNull(view);
            Object.FindFirstObjectByType<InputSystemUIInputModule>().moveRepeatDelay=5;
        }
        [UnityTearDown] public IEnumerator Clean()
        {
            if(RunDashBank.Instance!=null)Object.Destroy(RunDashBank.Instance.gameObject);
            Scene old=SceneManager.GetActiveScene();SceneManager.SetActiveScene(SceneManager.CreateScene("Menu test cleanup"));
            if(old.IsValid()&&old.isLoaded)yield return SceneManager.UnloadSceneAsync(old);
            if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);
            if(gamepad!=null&&gamepad.added)InputSystem.RemoveDevice(gamepad);
            if(mouse!=null&&mouse.added)InputSystem.RemoveDevice(mouse);
            if(previous==null){previous=ScriptableObject.CreateInstance<InputSettings>();JsonUtility.FromJsonOverwrite(previousJson,previous);}
            InputSystem.settings=previous;previous.hideFlags=previousFlags;Object.Destroy(testSettings);
            yield return null;LogAssert.NoUnexpectedReceived();
        }
        private IEnumerator Press(Key key)
        {InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));yield return null;yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;}
        private IEnumerator Pad(GamepadButton button)
        {if(gamepad==null||!gamepad.added)gamepad=InputSystem.AddDevice<Gamepad>();InputSystem.QueueStateEvent(gamepad,new GamepadState().WithButton(button));yield return null;yield return null;InputSystem.QueueStateEvent(gamepad,new GamepadState());yield return null;}
        private IEnumerator WaitForDestination(string path)
        {
            yield return new WaitUntil(() => SceneManager.GetActiveScene().path == path, System.TimeSpan.FromSeconds(10),
                () => Assert.Fail("Expected scene " + path));
            yield return null;
        }
        private RunDashBank CreateBank()
        {var prefab=AssetDatabase.LoadAssetAtPath<RunDashBank>("Assets/Game/Prefabs/RunDashBank.prefab");return Object.Instantiate(prefab);}

        private static NarrativePrologue Intro => Object.FindFirstObjectByType<NarrativePrologue>();
        private static IEnumerator AwaitPrologueInput()
        {
            Assert.NotNull(Intro);
            yield return new WaitUntil(() => Intro.InputArmed, System.TimeSpan.FromSeconds(6),
                () => Assert.Fail("Prologue input did not rearm."));
        }

        private static IEnumerator AwaitRevealCompletion()
        {
            yield return new WaitUntil(() => !Intro.IsRevealing, System.TimeSpan.FromSeconds(8),
                () => Assert.Fail("The dialogue did not finish revealing."));
        }

        private static string VisiblePrefix(TMPro.TMP_Text dialogue)
        {
            int count = Mathf.Min(dialogue.maxVisibleCharacters, dialogue.textInfo.characterCount);
            if (count == 0) return "";
            var last = dialogue.textInfo.characterInfo[count - 1];
            return dialogue.text.Substring(0, last.index + last.stringLength).TrimEnd();
        }

        private IEnumerator SkipToLevel()
        {
            yield return AwaitPrologueInput();
            Assert.IsTrue(Intro.TrySkip());
            Assert.IsFalse(Intro.TrySkip());
            yield return WaitForDestination(NarrativePrologue.LevelPath);
        }

        private IEnumerator Click(UnityEngine.UI.Button button, float hold = 0)
        {
            if (mouse == null || !mouse.added) mouse = InputSystem.AddDevice<Mouse>();
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            var position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position, buttons = 1 });
            yield return null; yield return null;
            if (hold > 0) yield return new WaitForSecondsRealtime(hold);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            yield return null; yield return null;
        }

        [UnityTest]
        public IEnumerator CorporateLogoAnimatesAndLoopsWithoutInterceptingMenuInput()
        {
            var panel = view.mainPanel.transform.Find("IllustrationPanelPlaceholder");
            Assert.AreEqual("PROMISE BEACON BIOMEDICAL GROUP", panel.Find("PanelLabel").GetComponent<TMPro.TMP_Text>().text);
            Assert.AreEqual("PBBG", panel.Find("PanelFooter").GetComponent<TMPro.TMP_Text>().text);
            Assert.IsFalse(panel.Find("LogoPlaceholder").gameObject.activeSelf);
            var logos = panel.GetComponentsInChildren<Animator>(true);
            Assert.AreEqual(1, logos.Length);
            var animator = logos[0];
            var image = animator.GetComponent<UnityEngine.UI.Image>();
            Assert.IsFalse(image.transform.parent.GetComponent<UnityEngine.UI.Image>().enabled);
            Assert.IsTrue(image.preserveAspect);
            Assert.IsFalse(image.raycastTarget);
            Assert.AreEqual(view.newGameButton.gameObject, EventSystem.current.currentSelectedGameObject);
            var first = image.sprite;
            Assert.IsNotNull(first);
            float initialCycle = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            yield return new WaitUntil(() => image.sprite != first, System.TimeSpan.FromSeconds(5),
                () => Assert.Fail("Corporate logo did not advance a frame."));
            yield return new WaitUntil(() => animator.GetCurrentAnimatorStateInfo(0).normalizedTime > initialCycle + 1f,
                System.TimeSpan.FromSeconds(5), () => Assert.Fail("Corporate logo did not complete a loop."));
            Assert.IsNotNull(image.sprite);

            yield return Click(view.controlsButton);
            Assert.IsTrue(view.ControlsOpen);
            yield return Press(Key.Escape);
            Assert.IsFalse(view.ControlsOpen);
            Assert.AreEqual(view.controlsButton.gameObject, EventSystem.current.currentSelectedGameObject);
        }

        [UnityTest] public IEnumerator SceneContainsRequiredInteractiveComponents()
        {
            Assert.AreEqual(1,Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(1,Object.FindObjectsByType<UnityEngine.UI.GraphicRaycaster>(FindObjectsSortMode.None).Length);
            foreach(var b in new[]{view.newGameButton,view.controlsButton,view.quitButton,view.backButton})Assert.NotNull(b);
            Assert.IsTrue(view.newGameButton.interactable);Assert.IsFalse(view.ControlsOpen);Assert.IsNull(RunDashBank.Instance);yield return null;
        }
        [UnityTest] public IEnumerator NewGameButtonLoadsProductionLevelAndInitializesFirstBank()
        {
            string path=view.launcher.gameplayScene;Assert.AreEqual(NarrativePrologue.ScenePath,path);
            view.newGameButton.onClick.Invoke();yield return WaitForDestination(path);
            var bank = RunDashBank.Instance;
            Assert.NotNull(bank);Assert.AreEqual(bank.StartingDashCount,bank.RemainingCharges);
            Assert.IsNull(Object.FindFirstObjectByType<LevelSetup>());
            Assert.IsNull(Object.FindFirstObjectByType<GameJam.Movement.PlayerMovement2D>());
            yield return SkipToLevel();
            Assert.AreSame(bank, RunDashBank.Instance);
            Assert.NotNull(Object.FindFirstObjectByType<LevelSetup>().Player);
            Assert.AreEqual(1,Object.FindObjectsByType<RunDashBank>(FindObjectsSortMode.None).Length);
        }
        [UnityTest] public IEnumerator ExistingSpentBalanceResetsWithoutDuplicateBank()
        {
            var bank=CreateBank();bank.TrySpendDash();bank.TrySpendDash();Assert.Less(bank.RemainingCharges,bank.StartingDashCount);
            int resets = 0; bank.NewRunStarted += () => resets++;
            string path=view.launcher.gameplayScene;view.newGameButton.onClick.Invoke();yield return WaitForDestination(path);
            Assert.AreSame(bank,RunDashBank.Instance);Assert.AreEqual(bank.StartingDashCount,bank.RemainingCharges);
            Assert.AreEqual(1,Object.FindObjectsByType<RunDashBank>(FindObjectsSortMode.None).Length);
            yield return SkipToLevel();
            Assert.AreSame(bank, RunDashBank.Instance); Assert.AreEqual(1, resets);
        }
        [UnityTest] public IEnumerator FatalPreviousRunCanStartFresh()
        {
            var bank=CreateBank();while(bank.RemainingCharges>0)bank.TrySpendDash();Assert.IsTrue(bank.IsGameOver);
            string path=view.launcher.gameplayScene;view.newGameButton.onClick.Invoke();yield return WaitForDestination(path);
            Assert.IsFalse(bank.IsGameOver);Assert.AreEqual(bank.StartingDashCount,bank.RemainingCharges);
            yield return SkipToLevel();
            Assert.IsTrue(Object.FindFirstObjectByType<LevelSetup>().Player.GetComponent<Rigidbody2D>().simulated);
        }

        [UnityTest] public IEnumerator SixSourceBeatsDisplayInOrderAndCompletionLoadsLevelExactlyOnce()
        {
            string[] expectedText =
            {
                "…procedure status stable…some involuntary muscle activation in upper extremities, closing the incisi- Nurse, anesthesia report?",
                "He’s supposed to be out, his vitals need to be recalibrated.",
                "Addendum to Log 48 of procedure… ...subject exhibiting signs of e. delirium, reworking levels, procedure still viable. Will proceed with Stage 2 once the subject settles. End log. Nurse, can you expedite the process? Nurse!",
                "Everything hurts, there’s a burning, slippery liquid on me……gasoline smell.…or may..be..…iron……….help",
                "Restraints still intact, he’s going out again.",
                "Finally, no time to waste. Initializing Stage 2. Scalpel…."
            };
            string[] expectedSpeakers = { "Doctor", "Nurse", "Doctor", "JETT", "Nurse", "Doctor" };
            string[] expectedColors = { "#1155CC", "#00FFFF", "#1155CC", "#FF0000", "#00FFFF", "#1155CC" };
            var bank = CreateBank();
            bank.TrySpendDash();
            int resets = 0;
            bank.NewRunStarted += () => resets++;
            view.newGameButton.onClick.Invoke();
            yield return WaitForDestination(NarrativePrologue.ScenePath);
            var texts = Intro.GetComponentsInChildren<TMPro.TMP_Text>();
            var dialogue = texts.Single(text => text.name == "Spoken Dialogue");
            var speaker = texts.Single(text => text.name == "Speaker");
            var progress = texts.Single(text => text.name == "Progress");
            int loads = 0;
            void CountLoad(Scene scene, LoadSceneMode mode) { if (scene.path == NarrativePrologue.LevelPath) loads++; }
            SceneManager.sceneLoaded += CountLoad;
            try
            {
                for (int i = 0; i < expectedText.Length; i++)
                {
                    yield return AwaitPrologueInput();
                    Canvas.ForceUpdateCanvases();
                    dialogue.ForceMeshUpdate();
                    Assert.AreEqual(i, Intro.BeatIndex);
                    Assert.AreEqual(expectedText[i], dialogue.text);
                    Assert.AreEqual(expectedSpeakers[i], speaker.text);
                    Assert.AreEqual($"{i + 1:00} / 06", progress.text);
                    Assert.AreEqual(expectedColors[i], "#" + ColorUtility.ToHtmlStringRGB(dialogue.color));
                    Assert.AreEqual(i == 3, (dialogue.fontStyle & TMPro.FontStyles.Italic) != 0);
                    var lines = dialogue.textInfo.characterInfo.Take(dialogue.textInfo.characterCount)
                        .Select(character => character.lineNumber).ToArray();
                    yield return AwaitRevealCompletion();
                    dialogue.ForceMeshUpdate();
                    Assert.AreEqual(expectedText[i], VisiblePrefix(dialogue));
                    CollectionAssert.AreEqual(lines, dialogue.textInfo.characterInfo.Take(dialogue.textInfo.characterCount)
                        .Select(character => character.lineNumber).ToArray(), "Revealing words preserves line wrapping.");
                    Assert.IsFalse(dialogue.isTextOverflowing, "Spoken dialogue fits without rendering stage directions.");
                    yield return Press(Key.Enter);
                }
                yield return WaitForDestination(NarrativePrologue.LevelPath);
                Assert.AreEqual(1, loads);
                Assert.AreEqual(1, resets);
                Assert.AreSame(bank, RunDashBank.Instance);
                Assert.AreEqual(bank.StartingDashCount, bank.RemainingCharges);
            }
            finally { SceneManager.sceneLoaded -= CountLoad; }
        }

        [UnityTest] public IEnumerator WordRevealIsUnscaledAndPausesAtTheEllipsisWithoutSplittingWords()
        {
            view.newGameButton.onClick.Invoke();
            yield return WaitForDestination(NarrativePrologue.ScenePath);
            yield return AwaitPrologueInput();
            var dialogue = Intro.GetComponentsInChildren<TMPro.TMP_Text>().Single(text => text.name == "Spoken Dialogue");
            Assert.IsTrue(Intro.IsRevealing);
            Assert.AreEqual("…procedure", VisiblePrefix(dialogue));
            var originalTimeScale = Time.timeScale;
            Time.timeScale = 0;
            try
            {
                yield return new WaitUntil(() => VisiblePrefix(dialogue) == "…procedure status stable…",
                    System.TimeSpan.FromSeconds(4), () => Assert.Fail("Expected a whole-word ellipsis boundary."));
                yield return new WaitForSecondsRealtime(.15f);
                Assert.AreEqual("…procedure status stable…", VisiblePrefix(dialogue));
                yield return AwaitRevealCompletion();
                Assert.AreEqual(dialogue.text, VisiblePrefix(dialogue));
                Assert.AreEqual(0, Intro.BeatIndex, "A complete reveal waits for input.");
            }
            finally { Time.timeScale = originalTimeScale; }
        }

        [UnityTest] public IEnumerator HeldMenuSubmitAndHeldAdvanceRequireRelease()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
            yield return WaitForDestination(NarrativePrologue.ScenePath);
            yield return new WaitForSecondsRealtime(.4f);
            Assert.IsTrue(keyboard.enterKey.isPressed);
            Assert.IsFalse(Intro.InputArmed);
            Assert.AreEqual(0, Intro.BeatIndex);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return AwaitPrologueInput();

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
            yield return new WaitForSecondsRealtime(.4f);
            Assert.AreEqual(0, Intro.BeatIndex);
            Assert.IsFalse(Intro.IsRevealing);
            Assert.IsFalse(Intro.InputArmed);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return AwaitPrologueInput();
            yield return Press(Key.Enter);
            Assert.AreEqual(1, Intro.BeatIndex);
            Assert.IsTrue(Intro.IsRevealing);
        }

        [UnityTest] public IEnumerator HeldSpaceBlocksRearmAndAFreshPressStillWorks()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            yield return null;
            view.newGameButton.onClick.Invoke();
            yield return WaitForDestination(NarrativePrologue.ScenePath);
            yield return new WaitForSecondsRealtime(.4f);
            Assert.IsFalse(Intro.InputArmed);
            Assert.AreEqual(0, Intro.BeatIndex);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return AwaitPrologueInput();
            yield return Press(Key.Enter);
            Assert.AreEqual(0, Intro.BeatIndex);
            Assert.IsFalse(Intro.IsRevealing);
            yield return AwaitPrologueInput();
            yield return Press(Key.Enter);
            Assert.AreEqual(1, Intro.BeatIndex);
        }

        [UnityTest] public IEnumerator MouseAdvancesAndSkipsThroughCanvas()
        {
            view.newGameButton.onClick.Invoke();
            yield return WaitForDestination(NarrativePrologue.ScenePath);
            yield return AwaitPrologueInput();
            var buttons = Intro.GetComponentsInChildren<UnityEngine.UI.Button>();
            var advance = buttons.Single(button => button.name == "Continue");
            yield return Click(advance, .3f);
            Assert.AreEqual(0, Intro.BeatIndex);
            Assert.IsFalse(Intro.IsRevealing);
            yield return AwaitPrologueInput();
            yield return Click(advance);
            Assert.AreEqual(1, Intro.BeatIndex);
            yield return AwaitPrologueInput();
            Assert.IsTrue(Intro.IsRevealing);
            yield return Click(buttons.Single(button => button.name == "Skip"));
            Assert.IsTrue(Intro.IsLeaving);
            Assert.IsFalse(Intro.TrySkip());
            yield return WaitForDestination(NarrativePrologue.LevelPath);
        }

        [UnityTest] public IEnumerator SkipAfterACompletedRevealLoadsLevelOnce()
        {
            view.newGameButton.onClick.Invoke();
            yield return WaitForDestination(NarrativePrologue.ScenePath);
            yield return AwaitPrologueInput();
            yield return AwaitRevealCompletion();
            int loads = 0;
            void CountLoad(Scene scene, LoadSceneMode mode) { if (scene.path == NarrativePrologue.LevelPath) loads++; }
            SceneManager.sceneLoaded += CountLoad;
            try
            {
                yield return SkipToLevel();
                Assert.AreEqual(1, loads);
            }
            finally { SceneManager.sceneLoaded -= CountLoad; }
        }

        [UnityTest] public IEnumerator GamepadNavigatesAdvancesAndSkipsWithoutInheritingJumpOrDash()
        {
            view.newGameButton.onClick.Invoke();
            yield return WaitForDestination(NarrativePrologue.ScenePath);
            yield return AwaitPrologueInput();
            Assert.AreEqual("Continue", EventSystem.current.currentSelectedGameObject.name);
            if (gamepad == null || !gamepad.added) gamepad = InputSystem.AddDevice<Gamepad>();
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South));
            yield return new WaitForSecondsRealtime(.35f);
            Assert.AreEqual(0, Intro.BeatIndex);
            Assert.IsFalse(Intro.IsRevealing);
            Assert.IsFalse(Intro.InputArmed);
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return AwaitPrologueInput();
            yield return Pad(GamepadButton.South);
            yield return AwaitPrologueInput();
            Assert.AreEqual(1, Intro.BeatIndex);
            yield return Pad(GamepadButton.DpadRight);
            Assert.AreEqual("Skip", EventSystem.current.currentSelectedGameObject.name);
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South)
                .WithButton(GamepadButton.RightShoulder));
            yield return new WaitForSecondsRealtime(.4f);
            Assert.IsTrue(Intro.IsLeaving);
            Assert.AreEqual(NarrativePrologue.ScenePath, SceneManager.GetActiveScene().path);
            Assert.IsFalse(Intro.TrySkip());
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return WaitForDestination(NarrativePrologue.LevelPath);
            var actor = Object.FindFirstObjectByType<LevelSetup>().Player.GetComponent<GameJam.Movement.PlayerMovement2D>();
            for (int i = 0; i < 5; i++)
            {
                yield return new WaitForFixedUpdate();
                Assert.Less(actor.GetComponent<Rigidbody2D>().linearVelocity.y, .1f);
                Assert.IsFalse(actor.IsDashing);
            }
            Assert.AreEqual(RunDashBank.Instance.StartingDashCount, RunDashBank.Instance.RemainingCharges);
        }
        [UnityTest] public IEnumerator DuplicateAndReentrantPlayRequestsAreRejected()
        {
            var bank=CreateBank();var launcher=view.launcher;int resetEvents=0;bool reentrantAccepted=true;
            bank.NewRunStarted+=()=>{resetEvents++;reentrantAccepted=launcher.TryStartNewGame();};
            string path=launcher.gameplayScene;Assert.IsTrue(launcher.TryStartNewGame());Assert.IsFalse(launcher.TryStartNewGame());
            Assert.IsFalse(reentrantAccepted);Assert.AreEqual(1,resetEvents);
            Assert.IsFalse(view.newGameButton.interactable||view.controlsButton.interactable||view.quitButton.interactable);
            yield return WaitForDestination(path);
        }
        [UnityTest] public IEnumerator DestinationCanBeConfiguredWithoutChangingMenuView()
        {
            const string destination="Assets/Game/Scenes/VerificationDestination.unity";view.launcher.gameplayScene=destination;
            view.newGameButton.onClick.Invoke();yield return WaitForDestination(destination);Assert.NotNull(RunDashBank.Instance);
        }
        [UnityTest] public IEnumerator UnregisteredDestinationDoesNotResetOrLock()
        {
            var bank=CreateBank();bank.TrySpendDash();int balance=bank.RemainingCharges;
            view.launcher.gameplayScene="NotARegisteredScene";Assert.IsFalse(view.launcher.TryStartNewGame());
            Assert.AreEqual(balance,bank.RemainingCharges);Assert.IsFalse(view.launcher.IsLoading);Assert.IsNotEmpty(view.status.text);yield return null;
        }
        [UnityTest] public IEnumerator OpeningMenuOrControlsDoesNotResetExistingRun()
        {
            var bank=CreateBank();bank.TrySpendDash();int balance=bank.RemainingCharges;
            view.controlsButton.onClick.Invoke();view.backButton.onClick.Invoke();yield return null;Assert.AreEqual(balance,bank.RemainingCharges);
        }
        [UnityTest] public IEnumerator ControlsAndBackButtonsRestoreSelection()
        {
            view.controlsButton.onClick.Invoke();Assert.IsTrue(view.ControlsOpen);Assert.AreSame(view.backButton.gameObject,EventSystem.current.currentSelectedGameObject);
            view.backButton.onClick.Invoke();Assert.IsFalse(view.ControlsOpen);Assert.AreSame(view.controlsButton.gameObject,EventSystem.current.currentSelectedGameObject);yield return null;
        }
        [UnityTest] public IEnumerator EscapeClosesControlsAndDoesNotQuitAtRoot()
        {view.controlsButton.onClick.Invoke();yield return Press(Key.Escape);Assert.IsFalse(view.ControlsOpen);yield return Press(Key.Escape);Assert.IsFalse(view.QuitRequested);}
        [UnityTest] public IEnumerator KeyboardNavigationAndSubmitOpenControls()
        {yield return Press(Key.DownArrow);Assert.AreSame(view.controlsButton.gameObject,EventSystem.current.currentSelectedGameObject);yield return Press(Key.Enter);Assert.IsTrue(view.ControlsOpen);}
        [UnityTest] public IEnumerator GamepadNavigationSubmitAndCancelWork()
        {yield return Pad(GamepadButton.DpadDown);Assert.AreSame(view.controlsButton.gameObject,EventSystem.current.currentSelectedGameObject);yield return Pad(GamepadButton.South);Assert.IsTrue(view.ControlsOpen);yield return Pad(GamepadButton.East);Assert.IsFalse(view.ControlsOpen);}
        [UnityTest] public IEnumerator GamepadStickNavigates()
        {
            gamepad=InputSystem.AddDevice<Gamepad>();InputSystem.QueueStateEvent(gamepad,new GamepadState{leftStick=Vector2.down});yield return null;yield return null;
            Assert.AreSame(view.controlsButton.gameObject,EventSystem.current.currentSelectedGameObject);
        }
        [UnityTest] public IEnumerator MouseCanOpenControlsThroughGraphicRaycaster()
        {
            mouse=InputSystem.AddDevice<Mouse>();Canvas.ForceUpdateCanvases();var rect=view.controlsButton.GetComponent<RectTransform>();
            Vector2 position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
            InputSystem.QueueStateEvent(mouse,new MouseState{position=position});yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=position,buttons=1});yield return null;yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=position});yield return null;yield return null;
            Assert.IsTrue(view.ControlsOpen);
        }
        [UnityTest] public IEnumerator NavigatingToQuitDoesNotActivateIt()
        {yield return Press(Key.DownArrow);yield return Press(Key.DownArrow);yield return Press(Key.DownArrow);Assert.AreSame(view.quitButton.gameObject,EventSystem.current.currentSelectedGameObject);Assert.IsFalse(view.QuitRequested);yield return Press(Key.DownArrow);Assert.IsFalse(view.QuitRequested);}
        [UnityTest] public IEnumerator QuitButtonIsSafeInEditor()
        {view.quitButton.onClick.Invoke();Assert.IsTrue(view.QuitRequested);yield return null;Assert.IsTrue(Application.isPlaying);Assert.IsFalse(view.launcher.IsLoading);}
        [UnityTest] public IEnumerator MenuEnablesOnlyPrivateUIActions()
        {
            var shared=AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Settings/InputSystem_Actions.inputactions");string json=shared.ToJson();
            Assert.AreNotSame(shared,view.inputScope.RuntimeActions);Assert.IsFalse(view.inputScope.RuntimeActions.FindActionMap("Player").enabled);
            Assert.IsTrue(view.inputScope.RuntimeActions.FindActionMap("UI").enabled);yield return Press(Key.Space);Assert.AreEqual(json,shared.ToJson());
        }
        [UnityTest] public IEnumerator InputScopeReenableDestroysOldActionsAndRebindsOnce()
        {
            var scope=view.inputScope;var old=scope.RuntimeActions;scope.enabled=false;yield return null;Assert.IsTrue(old==null);
            scope.enabled=true;yield return null;Assert.NotNull(scope.RuntimeActions);view.controlsButton.onClick.Invoke();yield return Press(Key.Escape);Assert.IsFalse(view.ControlsOpen);
        }
        [UnityTest] public IEnumerator ReenabledViewKeepsFunctionalListeners()
        {view.enabled=false;view.enabled=true;view.controlsButton.onClick.Invoke();Assert.IsTrue(view.ControlsOpen);yield return Press(Key.Escape);Assert.IsFalse(view.ControlsOpen);}
        [UnityTest] public IEnumerator ControlsTextContainsAcceptedBindings()
        {
            view.controlsButton.onClick.Invoke();string text=string.Join("\n",view.controlsPanel.GetComponentsInChildren<TMPro.TMP_Text>().Select(t=>t.text));
            foreach(string token in new[]{"A/D","Left/Right","W or Up","Space","WASD","Left stick","South button","Right shoulder"})StringAssert.Contains(token,text);yield return null;
        }
        [UnityTest] public IEnumerator CanvasAndButtonStatesAreReadableAndWithinScreen()
        {
            var scaler=Object.FindFirstObjectByType<UnityEngine.UI.CanvasScaler>();Assert.AreEqual(new Vector2(1280,720),scaler.referenceResolution);
            Assert.AreEqual(UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize,scaler.uiScaleMode);Canvas.ForceUpdateCanvases();
            foreach(var button in new[]{view.newGameButton,view.controlsButton,view.quitButton})
            {
                Assert.AreNotEqual(button.colors.normalColor,button.colors.selectedColor);Assert.AreNotEqual(button.colors.selectedColor,button.colors.pressedColor);
                var corners=new Vector3[4];button.GetComponent<RectTransform>().GetWorldCorners(corners);
                foreach(var c in corners){Vector2 screen=RectTransformUtility.WorldToScreenPoint(null,c);Assert.That(screen.x,Is.InRange(0f,(float)Screen.width));Assert.That(screen.y,Is.InRange(0f,(float)Screen.height));}
            }
            yield return null;
        }
        [Test] public void MenuIsFirstAndExistingBuildScenesArePreserved()
        {
            Assert.AreEqual(MenuBuilder.ScenePath,EditorBuildSettings.scenes[0].path);
            foreach(string p in new[]{"Assets/Scenes/SampleScene.unity","Assets/Game/Scenes/Levels/Level01.unity","Assets/Game/Scenes/VerificationDestination.unity"})Assert.IsTrue(EditorBuildSettings.scenes.Any(s=>s.path==p&&s.enabled));
        }
    }
}
#endif
