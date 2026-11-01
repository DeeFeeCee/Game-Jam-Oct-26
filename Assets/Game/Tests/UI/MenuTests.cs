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
        {if(gamepad==null)gamepad=InputSystem.AddDevice<Gamepad>();InputSystem.QueueStateEvent(gamepad,new GamepadState().WithButton(button));yield return null;yield return null;InputSystem.QueueStateEvent(gamepad,new GamepadState());yield return null;}
        private IEnumerator WaitForDestination(string path)
        {for(int i=0;i<240&&SceneManager.GetActiveScene().path!=path;i++)yield return null;Assert.AreEqual(path,SceneManager.GetActiveScene().path);yield return null;}
        private RunDashBank CreateBank()
        {var prefab=AssetDatabase.LoadAssetAtPath<RunDashBank>("Assets/Game/Prefabs/RunDashBank.prefab");return Object.Instantiate(prefab);}

        [UnityTest] public IEnumerator SceneContainsRequiredInteractiveComponents()
        {
            Assert.AreEqual(1,Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(1,Object.FindObjectsByType<UnityEngine.UI.GraphicRaycaster>(FindObjectsSortMode.None).Length);
            foreach(var b in new[]{view.newGameButton,view.controlsButton,view.quitButton,view.backButton})Assert.NotNull(b);
            Assert.IsTrue(view.newGameButton.interactable);Assert.IsFalse(view.ControlsOpen);Assert.IsNull(RunDashBank.Instance);yield return null;
        }
        [UnityTest] public IEnumerator NewGameButtonLoadsProductionLevelAndInitializesFirstBank()
        {
            string path=view.launcher.gameplayScene;Assert.AreEqual("Assets/Game/Scenes/Levels/Level01.unity",path);
            view.newGameButton.onClick.Invoke();yield return WaitForDestination(path);
            Assert.NotNull(RunDashBank.Instance);Assert.AreEqual(RunDashBank.Instance.StartingDashCount,RunDashBank.Instance.RemainingCharges);
            Assert.NotNull(Object.FindFirstObjectByType<LevelSetup>().Player);
            Assert.AreEqual(1,Object.FindObjectsByType<RunDashBank>(FindObjectsSortMode.None).Length);
        }
        [UnityTest] public IEnumerator ExistingSpentBalanceResetsWithoutDuplicateBank()
        {
            var bank=CreateBank();bank.TrySpendDash();bank.TrySpendDash();Assert.Less(bank.RemainingCharges,bank.StartingDashCount);
            string path=view.launcher.gameplayScene;view.newGameButton.onClick.Invoke();yield return WaitForDestination(path);
            Assert.AreSame(bank,RunDashBank.Instance);Assert.AreEqual(bank.StartingDashCount,bank.RemainingCharges);
            Assert.AreEqual(1,Object.FindObjectsByType<RunDashBank>(FindObjectsSortMode.None).Length);
        }
        [UnityTest] public IEnumerator FatalPreviousRunCanStartFresh()
        {
            var bank=CreateBank();while(bank.RemainingCharges>0)bank.TrySpendDash();Assert.IsTrue(bank.IsGameOver);
            string path=view.launcher.gameplayScene;view.newGameButton.onClick.Invoke();yield return WaitForDestination(path);
            Assert.IsFalse(bank.IsGameOver);Assert.AreEqual(bank.StartingDashCount,bank.RemainingCharges);
            Assert.IsTrue(Object.FindFirstObjectByType<LevelSetup>().Player.GetComponent<Rigidbody2D>().simulated);
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
        {yield return Press(Key.DownArrow);yield return Press(Key.DownArrow);Assert.AreSame(view.quitButton.gameObject,EventSystem.current.currentSelectedGameObject);Assert.IsFalse(view.QuitRequested);yield return Press(Key.DownArrow);Assert.IsFalse(view.QuitRequested);}
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
