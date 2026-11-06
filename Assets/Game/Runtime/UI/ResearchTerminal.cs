using System.Collections.Generic;
using GameJam.Levels;
using GameJam.Movement;
using GameJam.Resources;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace GameJam.UI
{
    /// <summary>Scene-owned modal with per-terminal text authored on its TMP components.</summary>
    [DefaultExecutionOrder(-175)]
    public sealed class ResearchTerminal : MonoBehaviour
    {
        public GameObject overlay;
        public TMP_Text title;
        public TMP_Text lore;
        public TMP_Text emptyEntry;
        public UnityEngine.UI.Button closeButton;
        public EventSystem uiEventSystem;
        public MenuInputScope inputScope;
        [Min(.1f)] public float interactionRadius = 2f;
        public bool IsOpen { get; private set; }
        public bool InputArmed { get; private set; }
        public bool WaitingForRelease { get; private set; }
        public static ResearchTerminal Owner { get; private set; }
        private static int consumedPauseFrame = -1;
        public static bool BlocksPause => Owner != null || consumedPauseFrame == Time.frameCount;
        private readonly List<PlayerMovementInput2D> held = new List<PlayerMovementInput2D>();
        private readonly List<EventSystem> suspended = new List<EventSystem>();
        private bool focused = true;
        private float previousTimeScale;
        private InputAction interact, cancel;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState() { Owner = null; consumedPauseFrame = -1; }
        private void OnEnable()
        {
            interact = new InputAction("Research terminal", InputActionType.Button);
            interact.AddBinding("<Keyboard>/e"); interact.AddBinding("<Gamepad>/buttonWest"); interact.Enable();
            cancel = new InputAction("Close terminal", InputActionType.Button);
            cancel.AddBinding("<Keyboard>/escape"); cancel.AddBinding("<Gamepad>/buttonEast");
            cancel.AddBinding("<Gamepad>/start"); cancel.Enable();
            closeButton.onClick.AddListener(Close);
        }
        private static bool Failed()
        {
            var failure = Object.FindFirstObjectByType<GameOverController>();
            return (RunDashBank.Instance != null && RunDashBank.Instance.IsGameOver) ||
                (failure != null && (failure.IsVisible || failure.IsNavigating));
        }
        private static bool Neutral()
        {
            if (!PauseInputRelease.IsNeutral()) return false;
            foreach (var device in InputSystem.devices)
                if (device is Keyboard k && k.eKey.isPressed || device is Gamepad p && p.buttonWest.isPressed) return false;
            return true;
        }
        private void Update()
        {
            if (Owner == this && Failed()) { ReleaseForFailure(); return; }
            if (Owner == this)
            {
                ClearRequests();
                if (WaitingForRelease && focused && Neutral()) RestoreInputs();
                if (IsOpen && !InputArmed && focused && Neutral())
                {
                    InputArmed = true; closeButton.interactable = true; inputScope.enabled = true;
                    uiEventSystem.SetSelectedGameObject(closeButton.gameObject);
                }
                if (IsOpen && InputArmed && focused && (cancel.WasPressedThisFrame() || interact.WasPressedThisFrame())) Close();
            }
            else if (focused && interact.WasPressedThisFrame()) TryOpen();
        }
        public bool TryOpen()
        {
            if (!focused || Owner != null || Failed() || Time.timeScale <= 0 || overlay == null) return false;
            var pause = Object.FindFirstObjectByType<PauseController>();
            if (pause != null && (pause.IsPaused || pause.IsNavigating || pause.WaitingForGameplayRelease)) return false;
            foreach (var exit in Object.FindObjectsByType<LevelTransition>(FindObjectsSortMode.None))
                if (exit.TransitionStarted) return false;
            var actor = Object.FindFirstObjectByType<PlayerMovement2D>();
            if (actor == null || !actor.isActiveAndEnabled || Vector2.Distance(actor.transform.position, transform.position) > interactionRadius) return false;
            // Deterministic nearest terminal wins when interaction ranges overlap.
            float distance = (actor.transform.position - transform.position).sqrMagnitude;
            foreach (var terminal in Object.FindObjectsByType<ResearchTerminal>(FindObjectsSortMode.None))
            {
                float other = (actor.transform.position - terminal.transform.position).sqrMagnitude;
                if (terminal != this && terminal.isActiveAndEnabled && (other < distance || other == distance && terminal.GetInstanceID() < GetInstanceID())) return false;
            }
            Owner = this; IsOpen = true; InputArmed = false; previousTimeScale = Time.timeScale;
            foreach (var input in Object.FindObjectsByType<PlayerMovementInput2D>(FindObjectsSortMode.None))
                if (input.isActiveAndEnabled) { held.Add(input); input.enabled = false; }
            ClearRequests(); Time.timeScale = 0;
            foreach (var system in Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
                if (system != uiEventSystem && system.enabled) { suspended.Add(system); system.enabled = false; }
            if (emptyEntry != null) emptyEntry.gameObject.SetActive(lore == null || string.IsNullOrWhiteSpace(lore.text));
            overlay.SetActive(true); uiEventSystem.gameObject.SetActive(true); closeButton.interactable = false;
            return true;
        }
        public void Close()
        {
            if (!IsOpen || !InputArmed || !focused || Failed()) return;
            Hide(); WaitingForRelease = true;
        }
        private void Hide()
        {
            consumedPauseFrame = Time.frameCount;
            inputScope.enabled = false; uiEventSystem.gameObject.SetActive(false); overlay.SetActive(false);
            IsOpen = InputArmed = false; Time.timeScale = previousTimeScale;
            foreach (var system in suspended) if (system != null) system.enabled = true;
            suspended.Clear();
        }
        private void ClearRequests()
        { foreach (var input in held) if (input != null) input.GetComponent<PlayerMovement2D>().ClearInput(); }
        private void RestoreInputs()
        {
            ClearRequests();
            if (!Failed()) foreach (var input in held) if (input != null) input.enabled = true;
            held.Clear(); WaitingForRelease = false; if (Owner == this) Owner = null;
        }
        public void ReleaseForFailure()
        {
            if (Owner != this) return;
            if (IsOpen) Hide();
            held.Clear(); WaitingForRelease = false; Owner = null;
        }
        private void OnApplicationFocus(bool value)
        {
            focused = value;
            if (!value && IsOpen) { InputArmed = false; closeButton.interactable = false; inputScope.enabled = false; ClearRequests(); }
        }
        private void OnDisable()
        {
            interact?.Dispose(); cancel?.Dispose(); closeButton.onClick.RemoveListener(Close);
            if (Owner == this) { if (IsOpen) Hide(); RestoreInputs(); }
        }
    }
}
