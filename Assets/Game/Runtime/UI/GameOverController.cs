using System.Collections.Generic;
using GameJam.Resources;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace GameJam.UI
{
    public sealed class GameOverController : MonoBehaviour
    {
        public string mainMenuScene="Assets/Game/Scenes/MainMenu.unity";
        public GameObject overlay;
        public EventSystem uiEventSystem;
        public MenuInputScope inputScope;
        public UnityEngine.UI.Button restartButton,mainMenuButton;
        public TMP_Text status;
        public TMP_Text failureReason;
        public string mobilityFailureReason = "MOBILITY ALLOCATION EXHAUSTED";
        public string environmentalFailureReason = "SUBJECT INTEGRITY LOST";
        [Header("Editable interface messages")]
        public string releasePrompt="Release controls to continue.";
        public string loadingPrompt="Loading...";
        public string retryUnavailable="Retry Level is unavailable. You can return to the Main Menu.";
        public string menuUnavailable="Main menu destination is unavailable.";
        public bool IsVisible {get;private set;}
        public bool InputArmed {get;private set;}
        public bool IsNavigating {get;private set;}
        public int PresentationCount {get;private set;}
        private RunDashBank bank;
        private TMP_Text primaryActionLabel;
        private bool IsMobilityExhaustion => bank != null && bank.FailureCause == RunFailureCause.MobilityExhaustion;
        private bool pending;
        private readonly List<UIOwner> suspendedOwners=new List<UIOwner>();
        private sealed class UIOwner
        {
            public EventSystem system;
            public MenuInputScope scope;
            public readonly List<BaseInputModule> modules=new List<BaseInputModule>();
        }
        private void OnEnable()
        {
            primaryActionLabel = restartButton.GetComponentInChildren<TMP_Text>(true);
            restartButton.onClick.AddListener(ActivatePrimaryAction);mainMenuButton.onClick.AddListener(MainMenu);BindBank();
        }
        private void BindBank()
        {
            if(bank==RunDashBank.Instance)return;
            if(bank!=null)bank.GameOver-=QueueFailure;
            bank=RunDashBank.Instance;
            if(bank!=null){bank.GameOver+=QueueFailure;if(bank.IsGameOver)pending=true;}
        }
        // Resource transaction callback: never reset, navigate, or activate UI here.
        private void QueueFailure()=>pending=true;
        private void Update()
        {
            BindBank();
            if(pending&&bank!=null&&bank.IsGameOver&&!IsVisible&&!IsNavigating)Show();
            if(IsVisible&&!InputArmed&&!IsNavigating&&GameOverInputRelease.IsNeutral())ArmInput();
        }
        private void Show()
        {
            if (ResearchTerminal.Owner != null) ResearchTerminal.Owner.ReleaseForFailure();
            var pause=Object.FindFirstObjectByType<PauseController>();
            if(pause!=null)pause.ReleaseForGameOver();
            pending=false;IsVisible=true;PresentationCount++;
            // Optional Inspector reference; preserve the accepted prefab and all its object IDs.
            if(failureReason==null)failureReason=overlay.transform.Find("FailurePanel/Reason")?.GetComponent<TMP_Text>();
            if(failureReason!=null)failureReason.text=bank.FailureCause==RunFailureCause.Environmental?environmentalFailureReason:mobilityFailureReason;
            if (primaryActionLabel != null) primaryActionLabel.text = IsMobilityExhaustion ? "RESTART RUN" : "RETRY LEVEL";
            SuspendOtherUI();overlay.SetActive(true);uiEventSystem.gameObject.SetActive(true);
            restartButton.interactable=mainMenuButton.interactable=false;
            status.text=releasePrompt;FocusRestart();
        }
        private void ArmInput()
        {
            InputArmed=true;restartButton.interactable=mainMenuButton.interactable=true;
            inputScope.enabled=true;status.text="";FocusRestart();
        }
        private void FocusRestart()
        {uiEventSystem.SetSelectedGameObject(null);uiEventSystem.SetSelectedGameObject(restartButton.gameObject);}
        private bool CanAct()=>IsVisible&&InputArmed&&!IsNavigating&&bank!=null&&bank.IsGameOver;
        private void ActivatePrimaryAction()
        {
            if (IsMobilityExhaustion) TryRestartRun();
            else TryRetryLevel();
        }
        private void MainMenu()=>TryReturnToMainMenu();
        public bool TryRetryLevel()
        {
            if (!CanAct() || bank.FailureCause != RunFailureCause.Environmental) return false;
            IsNavigating=true;SetNavigationState(true);
            if(bank.TryRetryLevel(gameObject.scene))return true;
            IsNavigating=false;SetNavigationState(false);status.text=retryUnavailable;return false;
        }
        public bool TryRestartRun()
        {
            if (!CanAct() || !IsMobilityExhaustion) return false;
            const string entry = NarrativePrologue.ScenePath;
            const string unavailable = "Restart Run is unavailable. You can return to the Main Menu.";
            if (!Application.CanStreamedLevelBeLoaded(entry))
            {
                status.text = unavailable;
                return false;
            }

            IsNavigating = true;
            SetNavigationState(true);
            AsyncOperation load;
            try { load = SceneManager.LoadSceneAsync(entry, LoadSceneMode.Single); }
            catch (System.ArgumentException) { load = null; }
            if (load == null)
            {
                IsNavigating = false;
                SetNavigationState(false);
                status.text = unavailable;
                return false;
            }

            // Reset after the departing player is unloaded. NewRunStarted can re-enable a linked player.
            GameJam.Levels.RunTimer.Instance?.ArmNewRun();
            var failedBank = bank;
            load.completed += _ =>
            {
                if (failedBank != null && failedBank.FailureCause == RunFailureCause.MobilityExhaustion)
                    failedBank.InitializeNewRun();
            };
            return true;
        }

        public bool TryReturnToMainMenu()
        {
            if(!CanAct())return false;
            if(string.IsNullOrWhiteSpace(mainMenuScene)||!Application.CanStreamedLevelBeLoaded(mainMenuScene))
            {status.text=menuUnavailable;return false;}
            IsNavigating=true;SetNavigationState(true);
            SceneManager.LoadSceneAsync(mainMenuScene,LoadSceneMode.Single);return true;
        }
        private void SetNavigationState(bool loading)
        {
            restartButton.interactable=mainMenuButton.interactable=!loading;
            if(loading)status.text=loadingPrompt;
        }
        private void SuspendOtherUI()
        {
            foreach(var other in Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
            {
                if(other==uiEventSystem||!other.enabled)continue;
                var owner=new UIOwner{system=other};
                var scope=other.GetComponent<MenuInputScope>();if(scope!=null&&scope.enabled)owner.scope=scope;
                foreach(var module in other.GetComponents<BaseInputModule>())if(module.enabled)owner.modules.Add(module);
                suspendedOwners.Add(owner);
                if(owner.scope!=null)owner.scope.enabled=false;
                foreach(var module in owner.modules)module.enabled=false;
                other.enabled=false;
            }
        }
        private void OnDisable()
        {
            restartButton.onClick.RemoveListener(ActivatePrimaryAction);mainMenuButton.onClick.RemoveListener(MainMenu);
            if(bank!=null)bank.GameOver-=QueueFailure;bank=null;
            inputScope.enabled=false;uiEventSystem.gameObject.SetActive(false);overlay.SetActive(false);
            foreach(var owner in suspendedOwners)
            {
                if(owner.system==null)continue;owner.system.enabled=true;
                if(owner.scope!=null)owner.scope.enabled=true;
                foreach(var module in owner.modules)if(module!=null&&!module.enabled)module.enabled=true;
            }
            suspendedOwners.Clear();IsVisible=InputArmed=pending=false;
        }
    }
}
