using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace GameJam.UI
{
    /// <summary>Scene-local presentation for the authored opening dialogue.</summary>
    public sealed class NarrativePrologue : MonoBehaviour
    {
        public const string ScenePath = "Assets/Game/Scenes/Intro/NarrativePrologue.unity";
        public const string LevelPath = "Assets/Game/Scenes/Levels/Level01.unity";
        private const string MenuPath = "Assets/Game/Scenes/MainMenu.unity";

        [Serializable]
        private sealed class Script
        {
            public Beat[] beats = Array.Empty<Beat>();
        }

        [Serializable]
        private sealed class Beat
        {
            public string speaker = "";
            public string displayName = "";
            public string color = "";
            public string dialogue = "";
            public bool internalSpeech = false;
        }

        [SerializeField] private TextAsset script;
        [SerializeField] private CanvasGroup dialoguePanel;
        [SerializeField] private TMP_Text speaker;
        [SerializeField] private TMP_Text dialogue;
        [SerializeField] private TMP_Text progress;
        [SerializeField] private TMP_Text status;
        [SerializeField] private UnityEngine.UI.Button advanceButton;
        [SerializeField] private UnityEngine.UI.Button skipButton;
        [SerializeField] private EventSystem eventSystem;
        [SerializeField, Min(0)] private float fadeSeconds = .22f;

        [Header("Word Reveal (seconds)")]
        [SerializeField, Min(0)] private float wordSeconds = .09f;
        [SerializeField, Min(0)] private float phraseSeconds = .15f;
        [SerializeField, Min(0)] private float sentenceSeconds = .30f;
        [SerializeField, Min(0)] private float hesitationSeconds = .35f;

        private readonly List<(int characters, double due)> revealSteps = new List<(int, double)>();
        private int nextRevealStep;
        private double revealElapsed;
        private Beat[] beats;
        private bool ready;
        private bool focused = true;
        public int BeatIndex { get; private set; }
        public bool InputArmed { get; private set; }
        public bool IsLeaving { get; private set; }
        public bool IsRevealing => nextRevealStep < revealSteps.Count;

        private void Awake()
        {
            try
            {
                beats = script != null ? JsonUtility.FromJson<Script>(script.text)?.beats : null;
            }
            catch (ArgumentException exception)
            {
                Debug.LogWarning($"Prologue dialogue could not be read. {exception.Message}", this);
            }
            beats ??= Array.Empty<Beat>();
            dialoguePanel.alpha = 0;
            dialogue.useMaxVisibleDescender = false;
            ShowBeat();
            Disarm();
        }

        private void OnEnable()
        {
            advanceButton.onClick.AddListener(Advance);
            skipButton.onClick.AddListener(Skip);
        }

        private IEnumerator Start()
        {
            yield return FadeTo(1);
            ready = true;
        }

        private void OnDisable()
        {
            advanceButton.onClick.RemoveListener(Advance);
            skipButton.onClick.RemoveListener(Skip);
        }

        private void Update()
        {
            if (!ready || IsLeaving || !focused) return;
            UpdateReveal(Time.unscaledDeltaTime);
            if (InputArmed) return;
            if (!PauseInputRelease.IsNeutral())
            {
                status.text = "Release controls to continue.";
                return;
            }

            InputArmed = true;
            advanceButton.interactable = beats.Length > 0;
            skipButton.interactable = true;
            status.text = "";
            if (eventSystem.currentSelectedGameObject == null ||
                eventSystem.currentSelectedGameObject == advanceButton.gameObject && !advanceButton.interactable)
                eventSystem.SetSelectedGameObject(advanceButton.interactable ? advanceButton.gameObject : skipButton.gameObject);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            focused = hasFocus;
            if (!hasFocus && !IsLeaving) Disarm();
        }

        private void Disarm()
        {
            InputArmed = false;
            advanceButton.interactable = false;
            skipButton.interactable = false;
        }

        private void ShowBeat()
        {
            var beat = beats.Length > 0 ? beats[BeatIndex] : null;
            speaker.text = string.IsNullOrEmpty(beat?.displayName) ? beat?.speaker ?? "" : beat.displayName;
            dialogue.text = beat?.dialogue ?? "";
            dialogue.fontStyle = (dialogue.fontStyle & ~FontStyles.Italic) |
                (beat?.internalSpeech == true ? FontStyles.Italic : FontStyles.Normal);
            progress.text = beats.Length > 0 ? $"{BeatIndex + 1:00} / {beats.Length:00}" : "";
            dialogue.color = beat != null && ColorUtility.TryParseHtmlString(beat.color, out var parsed)
                ? parsed : Color.white;
            BeginReveal();
        }

        private void BeginReveal()
        {
            revealSteps.Clear();
            nextRevealStep = 0;
            revealElapsed = 0;

            // TMP measures and wraps the complete line. Only the glyph visibility limit changes.
            dialogue.maxVisibleCharacters = 0;
            Canvas.ForceUpdateCanvases();
            dialogue.ForceMeshUpdate();
            var layout = dialogue.textInfo;
            double due = 0;
            for (int i = 0; i < layout.wordCount - 1; i++)
            {
                var word = layout.wordInfo[i];
                if (word.characterCount == 0) continue;
                int end = layout.wordInfo[i + 1].firstCharacterIndex;
                revealSteps.Add((end, due));
                due += PauseAfter(layout.characterInfo, word.lastCharacterIndex, end);
            }
            revealSteps.Add((layout.characterCount, due));
            UpdateReveal(0);
        }

        private float PauseAfter(TMP_CharacterInfo[] characters, int lastWordCharacter, int end)
        {
            // A trailing hyphen marks the Doctor's interrupted word. Internal hyphens stay in a word.
            float pause = characters[lastWordCharacter].character == '-' ? hesitationSeconds : wordSeconds;
            int periods = 0;
            for (int i = lastWordCharacter + 1; i < end; i++)
            {
                switch (characters[i].character)
                {
                    case '…': pause = Mathf.Max(pause, hesitationSeconds); break;
                    case '.': periods++; break;
                    case '?':
                    case '!': pause = Mathf.Max(pause, sentenceSeconds); break;
                    case ',':
                    case ';':
                    case ':': pause = Mathf.Max(pause, phraseSeconds); break;
                }
            }
            if (periods > 1) return Mathf.Max(pause, hesitationSeconds);
            if (periods == 1) return Mathf.Max(pause, sentenceSeconds);
            return pause;
        }

        private void UpdateReveal(float delta)
        {
            if (!IsRevealing) return;
            revealElapsed += delta;
            int previousStep = nextRevealStep;
            while (IsRevealing && revealSteps[nextRevealStep].due <= revealElapsed) nextRevealStep++;
            if (previousStep == nextRevealStep) return;
            dialogue.maxVisibleCharacters = IsRevealing ? revealSteps[nextRevealStep - 1].characters : int.MaxValue;
        }

        private void Advance()
        {
            if (!InputArmed || IsLeaving || beats.Length == 0) return;
            if (IsRevealing)
            {
                Disarm();
                nextRevealStep = revealSteps.Count;
                dialogue.maxVisibleCharacters = int.MaxValue;
                return;
            }
            if (BeatIndex == beats.Length - 1)
            {
                TrySkip();
                return;
            }
            Disarm();
            BeatIndex++;
            ShowBeat();
        }

        private void Skip() => TrySkip();

        public bool TrySkip()
        {
            if (!InputArmed || IsLeaving) return false;
            var target = Application.CanStreamedLevelBeLoaded(LevelPath) ? LevelPath : MenuPath;
            if (!Application.CanStreamedLevelBeLoaded(target))
            {
                status.text = "No destination is available.";
                return false;
            }

            IsLeaving = true;
            Disarm();
            StartCoroutine(Leave(target));
            return true;
        }

        private IEnumerator Leave(string destination)
        {
            yield return FadeTo(0);
            while (!focused || !PauseInputRelease.IsNeutral())
            {
                status.text = "Release controls to continue.";
                yield return null;
            }
            status.text = "";
            SceneManager.LoadSceneAsync(destination, LoadSceneMode.Single);
        }

        private IEnumerator FadeTo(float target)
        {
            var start = dialoguePanel.alpha;
            for (float elapsed = 0; elapsed < fadeSeconds; elapsed += Time.unscaledDeltaTime)
            {
                dialoguePanel.alpha = Mathf.Lerp(start, target, elapsed / fadeSeconds);
                yield return null;
            }
            dialoguePanel.alpha = target;
        }
    }
}
