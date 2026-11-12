using System.Collections.Generic;
using GameJam.Movement;
using GameJam.Resources;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace GameJam.UI
{
    /// <summary>Owns pause time and input, without disabling or resetting movement physics.</summary>
    [DefaultExecutionOrder(-150)]
    public sealed class PauseController : MonoBehaviour
    {
        public GameObject overlay, rootView, controlsView;
        public EventSystem uiEventSystem;
        public MenuInputScope inputScope;
        public UnityEngine.UI.Button resumeButton, controlsButton, mainMenuButton, backButton;
        public TMP_Text status;
        public string mainMenuScene = "Assets/Game/Scenes/MainMenu.unity";
        public string releasePrompt = "Release controls to continue.";
        public string loadingPrompt = "Loading...";
        public string menuUnavailable = "Main menu destination is unavailable.";
        public bool IsPaused { get; private set; }
        public bool InputArmed { get; private set; }
        public bool IsNavigating { get; private set; }
        public bool WaitingForGameplayRelease { get; private set; }
        public bool ControlsOpen => controlsView.activeSelf;

        private InputAction toggle;
        private bool toggleRequested, backRequested;
        private bool hasFocus = true;
        private readonly List<PlayerMovementInput2D> heldInputs = new List<PlayerMovementInput2D>();
        private readonly List<UIOwner> suspendedUI = new List<UIOwner>();
        private sealed class UIOwner
        {
            public EventSystem system;
            public MenuInputScope scope;
            public readonly List<BaseInputModule> modules = new List<BaseInputModule>();
        }

        private void OnEnable()
        {
            toggle = new InputAction("Pause", InputActionType.Button);
            toggle.AddBinding("<Keyboard>/escape");
            toggle.AddBinding("<Gamepad>/start");
            toggle.performed += OnToggle;
            toggle.Enable();
            resumeButton.onClick.AddListener(Resume);
            controlsButton.onClick.AddListener(OpenControls);
            mainMenuButton.onClick.AddListener(ReturnToMenu);
            backButton.onClick.AddListener(CloseControls);
            inputScope.CancelPressed += OnCancel;
        }

        private void OnToggle(InputAction.CallbackContext context) => toggleRequested = true;
        private void OnCancel() { if (ControlsOpen) backRequested = true; }

        private void Update()
        {
            // Game Over owns the modal boundary even when it happened between UI updates.
            if (FailureHasPriority())
            {
                ReleaseForGameOver();
                toggleRequested = backRequested = false;
                return;
            }
            if (WaitingForGameplayRelease)
            {
                ClearPlayerRequests();
                if (hasFocus && PauseInputRelease.IsNeutral()) RestorePlayerInputs();
            }
            if (IsPaused) ClearPlayerRequests();
            if (IsPaused && !InputArmed && hasFocus && PauseInputRelease.IsNeutral())
            {
                InputArmed = true;
                SetButtons(true);
                inputScope.enabled = true;
                status.text = "";
                Select(ControlsOpen ? backButton : resumeButton);
            }
            if (backRequested && IsPaused && InputArmed) CloseControls();
            else if (toggleRequested && hasFocus && !IsNavigating)
            {
                if (!IsPaused) TryPause();
                else if (InputArmed && !ControlsOpen) TryResume();
            }
            toggleRequested = backRequested = false;
        }

        private static bool FailureHasPriority()
        {
            if (RunDashBank.Instance != null && RunDashBank.Instance.IsGameOver) return true;
            // Restart resets the bank before the old scene unloads. The failure UI still
            // owns that transition, even though the persistent bank is healthy again.
            var failure = Object.FindFirstObjectByType<GameOverController>();
            return failure != null && (failure.IsVisible || failure.IsNavigating);
        }
        public bool TryPause()
        {
            if (ResearchTerminal.BlocksPause) return false;
            if (IsPaused || IsNavigating || FailureHasPriority() || WaitingForGameplayRelease || !hasFocus) return false;
            var bank = RunDashBank.Instance;
            if (bank == null || !bank.isActiveAndEnabled) return false;
            IsPaused = true;
            InputArmed = false;
            // Suspend controls without clearing an earlier jump buffer or stopping an in-flight dash.
            foreach (var input in Object.FindObjectsByType<PlayerMovementInput2D>(FindObjectsSortMode.None))
                if (input.isActiveAndEnabled) { heldInputs.Add(input); input.SetPaused(true); }
            ClearPlayerRequests();
            Time.timeScale = 0f;
            SuspendOtherUI();
            rootView.SetActive(true); controlsView.SetActive(false);
            overlay.SetActive(true); uiEventSystem.gameObject.SetActive(true);
            SetButtons(false); status.text = releasePrompt; Select(resumeButton);
            return true;
        }

        public bool TryResume()
        {
            if (!IsPaused || !InputArmed || IsNavigating || FailureHasPriority() || !hasFocus) return false;
            ClearPlayerRequests();
            Hide();
            WaitingForGameplayRelease = true;
            // Restore simulation now; restore input only after held gameplay/confirm controls release.
            // Existing active dashes continue their remaining physics ticks, with no new spending.
            return true;
        }

        private void Resume() => TryResume();
        public void OpenControls()
        {
            if (!IsPaused || !InputArmed || IsNavigating || FailureHasPriority()) return;
            rootView.SetActive(false); controlsView.SetActive(true); Select(backButton);
        }
        public void CloseControls()
        {
            if (!IsPaused || !ControlsOpen || IsNavigating) return;
            controlsView.SetActive(false); rootView.SetActive(true); Select(controlsButton);
        }

        private void ReturnToMenu() => TryReturnToMainMenu();
        public bool TryReturnToMainMenu()
        {
            if (!IsPaused || !InputArmed || IsNavigating || FailureHasPriority()) return false;
            if (string.IsNullOrWhiteSpace(mainMenuScene) || !Application.CanStreamedLevelBeLoaded(mainMenuScene))
            { status.text = menuUnavailable; return false; }
            IsNavigating = true;
            SetButtons(false); status.text = loadingPrompt;
            Time.timeScale = 1f;
            ClearPlayerRequests(preserveBufferedJump: false);
            // Keep input suspended until the old scene unloads. Never initialize a run here.
            if (SceneManager.LoadSceneAsync(mainMenuScene, LoadSceneMode.Single) != null)
                GameJam.Levels.RunTimer.Instance?.Abandon();
            return true;
        }

        /// <summary>Called before the accepted failure overlay takes input ownership.</summary>
        public void ReleaseForGameOver()
        {
            if (IsPaused || IsNavigating) Hide();
            WaitingForGameplayRelease = false;
            heldInputs.Clear(); // The bank's failure link owns the frozen actors now.
        }

        private void ClearPlayerRequests(bool preserveBufferedJump = true)
        {
            foreach (var actor in Object.FindObjectsByType<PlayerMovement2D>(FindObjectsSortMode.None))
                actor.ClearInput(preserveBufferedJump);
        }
        private void RestorePlayerInputs()
        {
            ClearPlayerRequests();
            if (!FailureHasPriority()) foreach (var input in heldInputs) if (input != null) input.SetPaused(false);
            heldInputs.Clear(); WaitingForGameplayRelease = false;
        }
        private void Hide()
        {
            Time.timeScale = 1f;
            inputScope.enabled = false;
            uiEventSystem.gameObject.SetActive(false); overlay.SetActive(false);
            IsPaused = InputArmed = false;
            RestoreOtherUI();
        }
        private void SetButtons(bool value)
        { resumeButton.interactable = controlsButton.interactable = mainMenuButton.interactable = backButton.interactable = value; }
        private void Select(UnityEngine.UI.Button target)
        { uiEventSystem.SetSelectedGameObject(null); uiEventSystem.SetSelectedGameObject(target.gameObject); }

        private void SuspendOtherUI()
        {
            foreach (var system in Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
            {
                if (system == uiEventSystem || !system.enabled) continue;
                var owner = new UIOwner { system = system };
                var scope = system.GetComponent<MenuInputScope>();
                if (scope != null && scope.enabled) owner.scope = scope;
                foreach (var module in system.GetComponents<BaseInputModule>()) if (module.enabled) owner.modules.Add(module);
                suspendedUI.Add(owner);
                if (owner.scope != null) owner.scope.enabled = false;
                foreach (var module in owner.modules) module.enabled = false;
                system.enabled = false;
            }
        }
        private void RestoreOtherUI()
        {
            foreach (var owner in suspendedUI)
            {
                if (owner.system == null) continue;
                owner.system.enabled = true;
                if (owner.scope != null) owner.scope.enabled = true;
                foreach (var module in owner.modules) if (module != null) module.enabled = true;
            }
            suspendedUI.Clear();
        }
        private void OnApplicationFocus(bool focused)
        {
            hasFocus = focused;
            toggleRequested = backRequested = false;
            if (!focused)
            {
                ClearPlayerRequests(preserveBufferedJump: IsPaused || WaitingForGameplayRelease);
                if (IsPaused) { InputArmed = false; inputScope.enabled = false; SetButtons(false); status.text = releasePrompt; }
            }
        }
        private void OnDisable()
        {
            toggle.performed -= OnToggle; toggle.Disable(); toggle.Dispose(); toggle = null;
            resumeButton.onClick.RemoveListener(Resume); controlsButton.onClick.RemoveListener(OpenControls);
            mainMenuButton.onClick.RemoveListener(ReturnToMenu); backButton.onClick.RemoveListener(CloseControls);
            inputScope.CancelPressed -= OnCancel;
            if (IsPaused || IsNavigating) Hide();
            RestorePlayerInputs();
            toggleRequested = backRequested = false;
        }
    }
}
