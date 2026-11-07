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
using Object=UnityEngine.Object;

namespace GameJam.UI.Tests
{
    public sealed class GameOverTests
    {
        private const string Level="Assets/Game/Scenes/Levels/Level01.unity";
        private GameOverController controller;
        private PlayerMovement2D actor;
        private Rigidbody2D body;
        private RunDashBank bank;
        private Keyboard keyboard;
        private Gamepad gamepad;
        private Mouse mouse;
        private InputSettings previous,settings;
        private string previousJson;
        private HideFlags previousFlags;
        [UnitySetUp] public IEnumerator Setup()
        {
            Assert.IsNull(RunDashBank.Instance);
            previous=InputSystem.settings;previousJson=JsonUtility.ToJson(previous);previousFlags=previous.hideFlags;
            if(previousFlags==HideFlags.HideAndDontSave)previous.hideFlags|=HideFlags.HideInInspector;
            settings=Object.Instantiate(previous);settings.hideFlags=HideFlags.HideAndDontSave;
            settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings=settings;keyboard=InputSystem.AddDevice<Keyboard>();
            yield return Load(Level);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(RunDashBank.Instance!=null)Object.Destroy(RunDashBank.Instance.gameObject);
            Scene old=SceneManager.GetActiveScene();SceneManager.SetActiveScene(SceneManager.CreateScene("Game Over test cleanup"));
            if(old.IsValid()&&old.isLoaded)yield return SceneManager.UnloadSceneAsync(old);
            foreach(var device in new InputDevice[]{keyboard,gamepad,mouse})if(device!=null&&device.added)InputSystem.RemoveDevice(device);
            keyboard=null;gamepad=null;mouse=null;
            if(previous==null){previous=ScriptableObject.CreateInstance<InputSettings>();JsonUtility.FromJsonOverwrite(previousJson,previous);}
            InputSystem.settings=previous;previous.hideFlags=previousFlags;Object.Destroy(settings);yield return null;LogAssert.NoUnexpectedReceived();
        }
        private IEnumerator Load(string path)
        {
            EditorSceneManager.LoadSceneInPlayMode(path,new LoadSceneParameters(LoadSceneMode.Single));yield return null;yield return null;Refresh();
        }
        private void Refresh()
        {
            controller=Object.FindFirstObjectByType<GameOverController>();Assert.NotNull(controller);
            actor=Object.FindFirstObjectByType<PlayerMovement2D>();body=actor.GetComponent<Rigidbody2D>();bank=RunDashBank.Instance;
            actor.GetComponent<PlayerMovementInput2D>().enabled=false;body.gravityScale=0;body.linearVelocity=Vector2.zero;
        }
        private IEnumerator Fail()
        {
            while(bank.RemainingCharges>1)bank.TrySpendDash();actor.RequestDash(Vector2.up);yield return new WaitForFixedUpdate();
            for(int i=0;i<30&&!controller.IsVisible;i++)yield return null;
            Assert.IsTrue(bank.IsGameOver);Assert.AreEqual(0,bank.RemainingCharges);Assert.IsTrue(controller.IsVisible);
        }
        private IEnumerator WaitForLoad(string path,int oldHandle)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 6;
            while ((SceneManager.GetActiveScene().handle == oldHandle || SceneManager.GetActiveScene().path != path) &&
                Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.AreNotEqual(oldHandle,SceneManager.GetActiveScene().handle);Assert.AreEqual(path,SceneManager.GetActiveScene().path);yield return null;yield return null;
        }
        private IEnumerator Key(Key key)
        {InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));yield return null;yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;}
        private IEnumerator Pad(GamepadButton button)
        {if(gamepad==null)gamepad=InputSystem.AddDevice<Gamepad>();InputSystem.QueueStateEvent(gamepad,new GamepadState().WithButton(button));yield return null;yield return null;InputSystem.QueueStateEvent(gamepad,new GamepadState());yield return null;}
        private int BankHandlers(string name)
        {var d=(Delegate)typeof(RunDashBank).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(bank);return d?.GetInvocationList().Length??0;}
        [UnityTest] public IEnumerator FinalDashFreezesImmediatelyAndDoesNotTravel()
        {
            while(bank.RemainingCharges>1)bank.TrySpendDash();Vector2 before=body.position;
            actor.RequestDash(Vector2.up);yield return new WaitForFixedUpdate();
            Assert.AreEqual(before,body.position);Assert.IsFalse(body.simulated);Assert.IsFalse(actor.enabled);Assert.IsFalse(actor.IsDashing);
            yield return null;Assert.IsTrue(controller.IsVisible);
        }
        [UnityTest] public IEnumerator FailureCallbackOnlyQueuesAndCannotResetInsideTransaction()
        {
            bool callback=false;int resets=0;bool restarted=true;
            bank.NewRunStarted+=()=>resets++;
            bank.GameOver+=()=>{callback=true;Assert.IsFalse(controller.IsVisible);Assert.AreEqual(0,bank.RemainingCharges);restarted=controller.TryRestartRun();};
            while(bank.RemainingCharges>0)bank.TrySpendDash();Assert.IsTrue(callback);Assert.IsFalse(restarted);Assert.AreEqual(0,resets);
            yield return null;Assert.IsTrue(controller.IsVisible);Assert.AreEqual(0,bank.RemainingCharges);
        }
        [UnityTest] public IEnumerator RejectedSpendsDoNotDuplicatePresentation()
        {
            yield return Fail();for(int i=0;i<5;i++){Assert.IsFalse(bank.TrySpendDash());GameOverSceneBootstrap.Install(SceneManager.GetActiveScene());yield return null;}
            Assert.AreEqual(1,controller.PresentationCount);Assert.AreEqual(1,Object.FindObjectsByType<GameOverController>(FindObjectsSortMode.None).Length);
        }
        [UnityTest] public IEnumerator FailureWaitDoesNotRefundOrPauseTime()
        {yield return Fail();float scale=Time.timeScale;yield return new WaitForSecondsRealtime(.1f);Assert.AreEqual(0,bank.RemainingCharges);Assert.IsTrue(bank.IsGameOver);Assert.AreEqual(scale,Time.timeScale);}
        [UnityTest] public IEnumerator HeldKeyboardDashAndSubmitMustReleaseBeforeActionsArm()
        {
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.Space,UnityEngine.InputSystem.Key.Enter));InputSystem.Update();
            yield return Fail();Assert.IsFalse(controller.InputArmed);Assert.IsFalse(controller.TryRestartRun());Assert.IsFalse(controller.TryReturnToMainMenu());
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;yield return null;
            Assert.IsTrue(controller.InputArmed);Assert.IsFalse(controller.IsNavigating);Assert.AreEqual(0,bank.RemainingCharges);
        }
        [UnityTest] public IEnumerator HeldGamepadFatalDashDoesNotAutoConfirm()
        {
            gamepad=InputSystem.AddDevice<Gamepad>();InputSystem.QueueStateEvent(gamepad,new GamepadState().WithButton(GamepadButton.RightShoulder).WithButton(GamepadButton.South));InputSystem.Update();
            yield return Fail();Assert.IsFalse(controller.InputArmed);
            InputSystem.QueueStateEvent(gamepad,new GamepadState());yield return null;yield return null;Assert.IsTrue(controller.InputArmed);Assert.AreEqual(0,bank.RemainingCharges);
        }
        [UnityTest] public IEnumerator HeldMousePressCannotCarryIntoFailure()
        {
            mouse=InputSystem.AddDevice<Mouse>();InputSystem.QueueStateEvent(mouse,new MouseState{buttons=1});InputSystem.Update();
            yield return Fail();Assert.IsFalse(controller.InputArmed);
            InputSystem.QueueStateEvent(mouse,new MouseState());yield return null;yield return null;Assert.IsTrue(controller.InputArmed);
        }
        [UnityTest] public IEnumerator FocusAndPrivateUIOwnershipTransferAfterFailure()
        {
            actor.GetComponent<PlayerMovementInput2D>().enabled=true;yield return Fail();
            Assert.IsTrue(controller.InputArmed);Assert.AreSame(controller.restartButton.gameObject,EventSystem.current.currentSelectedGameObject);
            Assert.IsFalse(actor.GetComponent<PlayerMovementInput2D>().enabled);Assert.IsFalse(controller.inputScope.RuntimeActions.FindActionMap("Player").enabled);
            Assert.AreEqual(1,Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Count(e=>e.isActiveAndEnabled));
        }
        [UnityTest] public IEnumerator EscapeAndGamepadCancelCannotDismissFailure()
        {yield return Fail();yield return Key(UnityEngine.InputSystem.Key.Escape);yield return Pad(GamepadButton.East);Assert.IsTrue(controller.IsVisible);Assert.IsTrue(bank.IsGameOver);Assert.IsFalse(body.simulated);}
        [UnityTest] public IEnumerator KeyboardNavigationAndExplicitSubmitReturnToMenu()
        {
            yield return Fail();yield return Key(UnityEngine.InputSystem.Key.DownArrow);Assert.AreSame(controller.mainMenuButton.gameObject,EventSystem.current.currentSelectedGameObject);Assert.IsFalse(controller.IsNavigating);
            int handle=SceneManager.GetActiveScene().handle;yield return Key(UnityEngine.InputSystem.Key.Enter);yield return WaitForLoad(MenuBuilder.ScenePath,handle);Assert.AreEqual(0,bank.RemainingCharges);
        }
        [UnityTest] public IEnumerator GamepadNavigationAndSubmitWork()
        {
            yield return Fail();yield return Pad(GamepadButton.DpadDown);Assert.AreSame(controller.mainMenuButton.gameObject,EventSystem.current.currentSelectedGameObject);
            int handle=SceneManager.GetActiveScene().handle;yield return Pad(GamepadButton.South);yield return WaitForLoad(MenuBuilder.ScenePath,handle);Assert.IsTrue(bank.IsGameOver);
        }
        [UnityTest] public IEnumerator MouseCanActivateReturnButton()
        {
            yield return Fail();mouse=InputSystem.AddDevice<Mouse>();Canvas.ForceUpdateCanvases();var rect=controller.mainMenuButton.GetComponent<RectTransform>();Vector2 pos=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
            InputSystem.QueueStateEvent(mouse,new MouseState{position=pos});yield return null;InputSystem.QueueStateEvent(mouse,new MouseState{position=pos,buttons=1});yield return null;yield return null;
            int handle=SceneManager.GetActiveScene().handle;InputSystem.QueueStateEvent(mouse,new MouseState{position=pos});yield return WaitForLoad(MenuBuilder.ScenePath,handle);Assert.AreEqual(0,bank.RemainingCharges);
        }
        [UnityTest] public IEnumerator RestartRestoresBankAndFreezesDepartingPlayer()
        {
            yield return Fail();int handle=SceneManager.GetActiveScene().handle;var previousController=controller;var oldActions=controller.inputScope.RuntimeActions;
            Assert.IsTrue(controller.TryRestartRun());Assert.AreEqual(bank.StartingDashCount,bank.RemainingCharges);Assert.IsFalse(bank.IsGameOver);
            Assert.IsFalse(body.simulated);Assert.IsFalse(actor.enabled);yield return WaitForLoad(Level,handle);
            Assert.IsTrue(previousController==null);Assert.IsTrue(oldActions==null);Assert.AreSame(bank,RunDashBank.Instance);
            Assert.AreEqual(1,Object.FindObjectsByType<RunDashBank>(FindObjectsSortMode.None).Length);
            Assert.IsTrue(Object.FindFirstObjectByType<PlayerMovement2D>().enabled);Assert.IsFalse(Object.FindFirstObjectByType<GameOverController>().IsVisible);
        }
        [UnityTest] public IEnumerator DuplicateAndReentrantRestartCannotLoadTwice()
        {
            yield return Fail();var old=controller;int resets=0,loads=0;bool reentrant=true;
            bank.NewRunStarted+=()=>{resets++;reentrant=old.TryReturnToMainMenu();};
            UnityEngine.Events.UnityAction<Scene,LoadSceneMode> onLoad=(s,m)=>{if(s.path==Level)loads++;};SceneManager.sceneLoaded+=onLoad;
            int handle=SceneManager.GetActiveScene().handle;Assert.IsTrue(controller.TryRestartRun());Assert.IsFalse(controller.TryRestartRun());Assert.IsFalse(controller.TryReturnToMainMenu());
            yield return WaitForLoad(Level,handle);SceneManager.sceneLoaded-=onLoad;Assert.AreEqual(1,resets);Assert.AreEqual(1,loads);Assert.IsFalse(reentrant);
        }
        [UnityTest] public IEnumerator InvalidRestartPreservesFailureAndCanRecover()
        {
            yield return Fail();controller.restartLauncher.gameplayScene="MissingScene";Assert.IsFalse(controller.TryRestartRun());Assert.IsFalse(controller.IsNavigating);Assert.IsNotEmpty(controller.status.text);Assert.AreEqual(0,bank.RemainingCharges);
            controller.restartLauncher.gameplayScene=Level;int handle=SceneManager.GetActiveScene().handle;Assert.IsTrue(controller.TryRestartRun());yield return WaitForLoad(Level,handle);
        }
        [UnityTest] public IEnumerator InvalidMenuPreservesFailureAndDoesNotLockUI()
        {
            yield return Fail();controller.mainMenuScene="";Assert.IsFalse(controller.TryReturnToMainMenu());Assert.IsFalse(controller.IsNavigating);Assert.AreEqual(0,bank.RemainingCharges);Assert.IsTrue(controller.restartButton.interactable);yield return null;
        }
        [UnityTest] public IEnumerator ReturnKeepsFailedBankAndNewGameStillWorks()
        {
            yield return Fail();var old=controller;var actions=controller.inputScope.RuntimeActions;int handle=SceneManager.GetActiveScene().handle;
            Assert.IsTrue(controller.TryReturnToMainMenu());Assert.IsFalse(controller.TryReturnToMainMenu());Assert.IsFalse(controller.TryRestartRun());yield return WaitForLoad(MenuBuilder.ScenePath,handle);
            Assert.IsTrue(old==null);Assert.IsTrue(actions==null);Assert.IsNull(Object.FindFirstObjectByType<GameOverController>());Assert.AreEqual(0,bank.RemainingCharges);Assert.IsTrue(bank.IsGameOver);
            Assert.AreEqual(1,Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length);
            var menu=Object.FindFirstObjectByType<MainMenuView>();handle=SceneManager.GetActiveScene().handle;menu.newGameButton.onClick.Invoke();
            yield return WaitForLoad(NarrativePrologue.ScenePath, handle);
            var intro = Object.FindFirstObjectByType<NarrativePrologue>();
            yield return new WaitUntil(() => intro.InputArmed, System.TimeSpan.FromSeconds(6),
                () => Assert.Fail("Prologue did not accept input."));
            Assert.IsTrue(intro.TrySkip());
            yield return WaitForLoad(Level,handle);Assert.AreEqual(bank.StartingDashCount,bank.RemainingCharges);
        }
        [UnityTest] public IEnumerator SubscriptionsCleanUpOnMenuTransition()
        {
            Assert.AreEqual(2,BankHandlers("GameOver"));yield return Fail();int handle=SceneManager.GetActiveScene().handle;controller.TryReturnToMainMenu();yield return WaitForLoad(MenuBuilder.ScenePath,handle);
            Assert.AreEqual(0,BankHandlers("GameOver"));Assert.AreEqual(0,BankHandlers("ChargesChanged"));Assert.AreEqual(0,BankHandlers("NewRunStarted"));
        }
        [UnityTest] public IEnumerator ThreeFailureRestartCyclesDoNotAccumulateObjectsOrListeners()
        {
            for(int cycle=0;cycle<3;cycle++)
            {
                yield return Fail();Assert.AreEqual(1,controller.PresentationCount);Assert.AreEqual(2,BankHandlers("GameOver"));int handle=SceneManager.GetActiveScene().handle;
                controller.restartButton.onClick.Invoke();yield return WaitForLoad(Level,handle);Refresh();Assert.AreEqual(2,BankHandlers("GameOver"));
                Assert.AreEqual(1,Object.FindObjectsByType<GameOverController>(FindObjectsSortMode.None).Length);Assert.AreEqual(0,Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Count(e=>e.isActiveAndEnabled));
            }
        }
        [UnityTest] public IEnumerator OtherUIOwnerIsSuspendedAndRestoredWithoutResumingPlayer()
        {
            var menu=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(MenuBuilder.PrefabPath));yield return null;
            var other=menu.GetComponentInChildren<EventSystem>();var scope=other.GetComponent<MenuInputScope>();var oldActions=scope.RuntimeActions;
            yield return Fail();yield return null;Assert.IsFalse(other.enabled);Assert.IsFalse(scope.enabled);Assert.IsTrue(oldActions==null);
            Assert.AreEqual(1,Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Count(e=>e.isActiveAndEnabled));
            controller.enabled=false;Assert.IsTrue(other.enabled);Assert.IsTrue(scope.enabled);Assert.IsFalse(body.simulated);Object.Destroy(menu);yield return null;
        }
        [UnityTest] public IEnumerator BootstrapIsIdempotentForSameScene()
        {for(int i=0;i<5;i++)GameOverSceneBootstrap.Install(SceneManager.GetActiveScene());Assert.AreEqual(1,Object.FindObjectsByType<GameOverController>(FindObjectsSortMode.None).Length);yield return null;}
        [UnityTest] public IEnumerator VerificationDestinationSupportsFailureOverlay()
        {yield return Load("Assets/Game/Scenes/VerificationDestination.unity");yield return Fail();Assert.AreEqual(1,controller.PresentationCount);Assert.AreEqual(Level,controller.restartLauncher.gameplayScene);}
        [UnityTest] public IEnumerator ExperimentalFailureRestartsRunAtProductionBeginning()
        {
            yield return Load("Assets/Game/Experiments/SingleScreen/Scenes/SingleScreen32x18.unity");yield return Fail();int handle=SceneManager.GetActiveScene().handle;
            controller.restartButton.onClick.Invoke();yield return WaitForLoad(Level,handle);Assert.AreEqual(bank.StartingDashCount,bank.RemainingCharges);
        }
        [Test] public void PresentationPrefabIsReusableAndInitiallyInactive()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(GameOverBuilder.PrefabPath);var c=prefab.GetComponent<GameOverController>();
            Assert.IsFalse(c.overlay.activeSelf);Assert.IsFalse(c.uiEventSystem.gameObject.activeSelf);Assert.IsFalse(c.inputScope.enabled);
            Assert.AreEqual(100,c.overlay.GetComponent<Canvas>().sortingOrder);Assert.IsNotNull(c.restartLauncher);
        }
    }
}
#endif
