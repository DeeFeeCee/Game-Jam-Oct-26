using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GameJam.UI
{
    public sealed class MainMenuView : MonoBehaviour
    {
        public NewGameLauncher launcher;
        public MenuInputScope inputScope;
        public GameObject mainPanel,controlsPanel;
        public UnityEngine.UI.Button newGameButton,controlsButton,quitButton,backButton;
        public UnityEngine.UI.Button creditsButton;
        public CreditsArchiveView credits;
        public TMP_Text status;
        public bool ControlsOpen=>controlsPanel.activeSelf;
        public bool CreditsOpen=>credits!=null&&credits.IsOpen;
        public bool QuitRequested { get; private set; }
        private void OnEnable()
        {
            newGameButton.onClick.AddListener(NewGame);controlsButton.onClick.AddListener(OpenControls);
            quitButton.onClick.AddListener(Quit);backButton.onClick.AddListener(CloseControls);
            if(creditsButton!=null)creditsButton.onClick.AddListener(OpenCredits);
            if(credits!=null){credits.returnButton.onClick.AddListener(CloseCredits);credits.Hide();}
            inputScope.CancelPressed+=Cancel;launcher.StateChanged+=RefreshLoading;
            controlsPanel.SetActive(false);mainPanel.SetActive(true);RefreshLoading();Select(newGameButton.gameObject);
        }
        private void Start()=>Select(newGameButton.gameObject);
        private void OnDisable()
        {
            newGameButton.onClick.RemoveListener(NewGame);controlsButton.onClick.RemoveListener(OpenControls);
            quitButton.onClick.RemoveListener(Quit);backButton.onClick.RemoveListener(CloseControls);
            if(creditsButton!=null)creditsButton.onClick.RemoveListener(OpenCredits);
            if(credits!=null){credits.returnButton.onClick.RemoveListener(CloseCredits);credits.Hide();}
            inputScope.CancelPressed-=Cancel;launcher.StateChanged-=RefreshLoading;
        }
        private static void Select(GameObject target)
        {
            if(EventSystem.current==null)return;
            EventSystem.current.SetSelectedGameObject(null);EventSystem.current.SetSelectedGameObject(target);
        }
        private void NewGame()=>launcher.TryStartNewGame();
        public void OpenControls()
        {
            if(launcher.IsLoading)return;
            if(credits!=null)credits.Hide();
            Select(null);mainPanel.SetActive(false);controlsPanel.SetActive(true);Select(backButton.gameObject);
        }
        public void CloseControls()
        {
            if(!ControlsOpen)return;
            Select(null);controlsPanel.SetActive(false);mainPanel.SetActive(true);Select(controlsButton.gameObject);
        }
        public void OpenCredits()
        {
            if(launcher.IsLoading||credits==null||!credits.Show())return;
            controlsPanel.SetActive(false);mainPanel.SetActive(false);Select(credits.scrollbar.gameObject);
        }
        public void CloseCredits()
        {
            if(!CreditsOpen)return;
            credits.Hide();mainPanel.SetActive(true);Select(creditsButton.gameObject);
        }
        private void Cancel(){if(CreditsOpen)CloseCredits();else if(ControlsOpen)CloseControls();}
        private void RefreshLoading()
        {
            newGameButton.interactable=controlsButton.interactable=quitButton.interactable=!launcher.IsLoading;
            if(creditsButton!=null)creditsButton.interactable=!launcher.IsLoading;
            status.text=launcher.Status;
        }
        public void Quit()
        {
            if(launcher.IsLoading)return;
            QuitRequested=true;
#if UNITY_EDITOR
            Debug.Log("Quit requested: standalone player exits; Unity Editor stays open.");
#else
            Application.Quit(0);
#endif
        }
    }
}
