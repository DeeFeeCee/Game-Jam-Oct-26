using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GameJam.UI
{
    /// <summary>Scene-owned ending presentation. Any configured level exit can load it.</summary>
    public sealed class EndingSequence : MonoBehaviour
    {
        public const string ScenePath = "Assets/Game/Scenes/Ending/EndingSequence.unity";
        public const string RevealMessage = "Stage 2 successful, initializing Stage 3";

        public enum Stage { WhiteFade, WhiteHold, DarknessFade, Credits, Reveal, Returning }

        [SerializeField] private UnityEngine.UI.Image backdrop;
        [SerializeField] private RectTransform creditsViewport;
        [SerializeField] private TMP_Text credits;
        [SerializeField] private TMP_Text reveal;
        [SerializeField] private TMP_Text status;
        [SerializeField] private UnityEngine.UI.Button continueButton;
        [SerializeField] private EventSystem eventSystem;
        [SerializeField] private MenuInputScope inputScope;
        [SerializeField] private string mainMenuScene = "Assets/Game/Scenes/MainMenu.unity";

        [SerializeField, Min(0f)] private float whiteFadeSeconds = .45f;
        [SerializeField, Min(0f)] private float whiteHoldSeconds = 3f;
        [SerializeField, Min(0f)] private float darknessFadeSeconds = .6f;
        [SerializeField, Min(1f)] private float creditsUnitsPerSecond = 80f;

        private bool focused = true;
        public Stage CurrentStage { get; private set; }
        public float CreditsProgress { get; private set; }
        public bool InputArmed { get; private set; }

        private void Awake()
        {
            backdrop.color = Color.black;
            creditsViewport.gameObject.SetActive(false);
            reveal.text = RevealMessage;
            reveal.gameObject.SetActive(false);
            continueButton.gameObject.SetActive(false);
            status.text = "";
            SetInputArmed(false);
        }

        private void OnEnable() => continueButton.onClick.AddListener(Continue);
        private void OnDisable() => continueButton.onClick.RemoveListener(Continue);

        private IEnumerator Start()
        {
            CurrentStage = Stage.WhiteFade;
            yield return FadeTo(Color.white, whiteFadeSeconds);
            CurrentStage = Stage.WhiteHold;
            yield return new WaitForSecondsRealtime(whiteHoldSeconds);
            CurrentStage = Stage.DarknessFade;
            yield return FadeTo(Color.black, darknessFadeSeconds);
            CurrentStage = Stage.Credits;
            yield return ScrollCredits();
            CurrentStage = Stage.Reveal;
            reveal.gameObject.SetActive(true);
            continueButton.gameObject.SetActive(true);
        }

        private IEnumerator FadeTo(Color color, float seconds)
        {
            Color start = backdrop.color;
            for (float elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
            {
                backdrop.color = Color.Lerp(start, color, elapsed / seconds);
                yield return null;
            }
            backdrop.color = color;
        }

        private IEnumerator ScrollCredits()
        {
            creditsViewport.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            RectTransform roll = credits.rectTransform;
            float textHeight = credits.GetPreferredValues(credits.text, roll.rect.width, Mathf.Infinity).y;
            roll.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, textHeight);
            float distance = creditsViewport.rect.height + textHeight;
            float seconds = distance / creditsUnitsPerSecond;
            for (float elapsed = 0f; elapsed < seconds; elapsed += Mathf.Min(Time.unscaledDeltaTime, .1f))
            {
                CreditsProgress = elapsed / seconds;
                roll.anchoredPosition = new Vector2(0f, Mathf.Lerp(-creditsViewport.rect.height, textHeight, CreditsProgress));
                yield return null;
            }
            CreditsProgress = 1f;
            creditsViewport.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (CurrentStage != Stage.Reveal || !focused || InputArmed)
                return;
            if (!PauseInputRelease.IsNeutral())
            {
                status.text = "Release controls to continue.";
                return;
            }
            // The EventSystem takes an update to activate a newly enabled input module.
            // Expose Continue only when a fresh Submit can actually be processed.
            if (!inputScope.enabled)
            {
                inputScope.enabled = true;
                return;
            }
            if (eventSystem.currentInputModule == null)
                return;
            status.text = "";
            SetInputArmed(true);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            focused = hasFocus;
            if (!hasFocus)
                SetInputArmed(false);
        }

        private void SetInputArmed(bool armed)
        {
            InputArmed = armed;
            continueButton.interactable = armed;
            inputScope.enabled = armed;
            eventSystem.SetSelectedGameObject(armed ? continueButton.gameObject : null);
        }

        private void Continue() => TryContinue();

        public bool TryContinue()
        {
            if (CurrentStage != Stage.Reveal || !InputArmed)
                return false;
            if (string.IsNullOrWhiteSpace(mainMenuScene) || !Application.CanStreamedLevelBeLoaded(mainMenuScene))
            {
                status.text = "Main Menu is unavailable.";
                return false;
            }
            CurrentStage = Stage.Returning;
            SetInputArmed(false);
            StartCoroutine(ReturnWhenReleased());
            return true;
        }

        private IEnumerator ReturnWhenReleased()
        {
            while (!focused || !PauseInputRelease.IsNeutral())
            {
                status.text = "Release controls to continue.";
                yield return null;
            }
            AsyncOperation load;
            try { load = SceneManager.LoadSceneAsync(mainMenuScene, LoadSceneMode.Single); }
            catch (ArgumentException) { load = null; }
            if (load == null)
            {
                CurrentStage = Stage.Reveal;
                SetInputArmed(true);
                status.text = "Main Menu is unavailable.";
                yield break;
            }
            status.text = "";
            Time.timeScale = 1f;
            yield return load;
        }
    }
}
