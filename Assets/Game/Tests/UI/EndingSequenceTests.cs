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
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GameJam.UI.Tests
{
    public sealed class EndingSequenceTests
    {
        private const string Level01 = "Assets/Game/Scenes/Levels/Level01.unity";
        private const string Level02 = "Assets/Game/Scenes/Levels/Level02.unity";
        private const string Level12 = "Assets/Game/Scenes/Levels/Level12.unity";
        private Keyboard keyboard;
        private Gamepad pad;
        private InputSettings original, settings;
        private HideFlags originalFlags;
        private string originalJson;
        private EndingSequence ending;

        [SetUp] public void SetUp()
        {
            Assert.IsNull(RunDashBank.Instance);
            original = InputSystem.settings;
            originalFlags = original.hideFlags;
            originalJson = JsonUtility.ToJson(original);
            if (originalFlags == HideFlags.HideAndDontSave) original.hideFlags |= HideFlags.HideInInspector;
            settings = Object.Instantiate(original);
            settings.hideFlags = HideFlags.HideAndDontSave;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = settings;
            keyboard = InputSystem.AddDevice<Keyboard>();
            pad = InputSystem.AddDevice<Gamepad>();
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            if (RunDashBank.Instance != null) Object.Destroy(RunDashBank.Instance.gameObject);
            var active = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("Ending test cleanup"));
            if (active.IsValid() && active.isLoaded) yield return SceneManager.UnloadSceneAsync(active);
            InputSystem.RemoveDevice(keyboard);
            InputSystem.RemoveDevice(pad);
            if (original == null)
            {
                original = ScriptableObject.CreateInstance<InputSettings>();
                JsonUtility.FromJsonOverwrite(originalJson, original);
            }
            InputSystem.settings = original;
            original.hideFlags = originalFlags;
            Object.Destroy(settings);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest] public IEnumerator AuthoredLevel12ExitUsesPhysicsAndStartsTheEndingOnce()
        {
            yield return Load(Level12);
            var bank = RunDashBank.Instance;
            Assert.AreEqual(5, bank.CheckpointCharges);
            Assert.IsTrue(bank.TrySpendDash());
            Assert.AreEqual(4, bank.RemainingCharges);
            var exit = Object.FindFirstObjectByType<LevelExit>();
            Assert.AreEqual(EndingSequence.ScenePath, exit.transition.destinationScene);
            Assert.AreEqual(new Vector2(29, 14), (Vector2)exit.GetComponent<BoxCollider2D>().bounds.center);
            Assert.AreEqual(new Vector2(2, 2), exit.GetComponent<BoxCollider2D>().size);
            Assert.GreaterOrEqual(SceneUtility.GetBuildIndexByScenePath(EndingSequence.ScenePath), 0);
            int entries = 0;
            UnityEngine.Events.UnityAction<Scene, LoadSceneMode> entered = (scene, _) =>
            { if (scene.path == EndingSequence.ScenePath) entries++; };
            SceneManager.sceneLoaded += entered;
            try
            {
                yield return WalkIntoExit(exit, EndingSequence.ScenePath);
                Assert.AreEqual(1, entries);
                Assert.AreSame(bank, RunDashBank.Instance);
                Assert.AreEqual(4, bank.RemainingCharges);
                Assert.AreEqual(Level12, bank.CheckpointScenePath);
                Assert.AreEqual(5, bank.CheckpointCharges);
                Assert.IsFalse(bank.IsGameOver);
            }
            finally { SceneManager.sceneLoaded -= entered; }
        }

        [UnityTest] public IEnumerator IntermediateExitKeepsItsNormalDestinationAndEntryBalance()
        {
            yield return Load(Level01);
            var bank = RunDashBank.Instance;
            bank.TrySpendDash();
            yield return WalkIntoExit(Object.FindFirstObjectByType<LevelExit>(), Level02);
            Assert.IsNull(Object.FindFirstObjectByType<EndingSequence>());
            Assert.AreSame(bank, RunDashBank.Instance);
            Assert.AreEqual(Level02, bank.CheckpointScenePath);
            Assert.AreEqual(4, bank.CheckpointCharges);
        }

        [UnityTest] public IEnumerator AConfiguredExitInAnotherLevelCanFinishTheRun()
        {
            yield return Load(Level01);
            var exit = Object.FindFirstObjectByType<LevelExit>();
            exit.transition.destinationScene = EndingSequence.ScenePath;
            yield return WalkIntoExit(exit, EndingSequence.ScenePath);
            Assert.NotNull(Object.FindFirstObjectByType<EndingSequence>());
            Assert.AreEqual(Level01, RunDashBank.Instance.CheckpointScenePath);
            Assert.AreEqual(5, RunDashBank.Instance.RemainingCharges);
        }

        [UnityTest] public IEnumerator WhiteFadesAndHoldsForTheAuthoredThreeSecondsBeforeCredits()
        {
            yield return LoadEnding(fast: false);
            var backdrop = Field<UnityEngine.UI.Image>("backdrop");
            Assert.AreEqual(3f, Field<float>("whiteHoldSeconds"));
            Assert.AreEqual(EndingSequence.Stage.WhiteFade, ending.CurrentStage);
            Assert.Less(backdrop.color.r, 1f);
            yield return Until(() => ending.CurrentStage == EndingSequence.Stage.WhiteHold, "White hold");
            double began = Time.realtimeSinceStartupAsDouble;
            Assert.AreEqual(Color.white, backdrop.color);
            Assert.IsFalse(Field<RectTransform>("creditsViewport").gameObject.activeSelf);
            Assert.IsFalse(Field<TMP_Text>("reveal").gameObject.activeSelf);
            Assert.IsFalse(ending.TryContinue());
            yield return new WaitForSecondsRealtime(.35f);
            Assert.AreEqual(EndingSequence.Stage.WhiteHold, ending.CurrentStage);
            Assert.AreEqual(Color.white, backdrop.color);
            yield return Until(() => ending.CurrentStage == EndingSequence.Stage.DarknessFade, "Dark transition");
            Assert.GreaterOrEqual(Time.realtimeSinceStartupAsDouble - began, 2.9);
            Assert.IsFalse(Field<RectTransform>("creditsViewport").gameObject.activeSelf);
            yield return Until(() => ending.CurrentStage == EndingSequence.Stage.Credits, "Credits start");
            Assert.AreEqual(Color.black, backdrop.color);
        }

        [UnityTest] public IEnumerator CreditsScrollFullyBeforeTheExactRevealAndContinueStayOnScreen()
        {
            yield return LoadEnding();
            yield return Until(() => ending.CurrentStage == EndingSequence.Stage.Credits && ending.CreditsProgress > 0f, "Credits scroll starts");
            var credits = Field<TMP_Text>("credits");
            float firstY = credits.rectTransform.anchoredPosition.y;
            float firstProgress = ending.CreditsProgress;
            Assert.IsFalse(Field<TMP_Text>("reveal").gameObject.activeSelf);
            yield return Until(() => ending.CreditsProgress > firstProgress + .1f, "Credits move");
            Assert.Greater(credits.rectTransform.anchoredPosition.y, firstY);
            yield return Ready();
            Assert.AreEqual(1f, ending.CreditsProgress);
            Assert.IsFalse(Field<RectTransform>("creditsViewport").gameObject.activeSelf);
            Assert.AreEqual("Stage 2 successful, initializing Stage 3", Field<TMP_Text>("reveal").text);
            Assert.IsFalse(credits.text.Contains("Stage 2 successful"));
            yield return new WaitForSecondsRealtime(.3f);
            Assert.AreEqual(EndingSequence.Stage.Reveal, ending.CurrentStage);
            Assert.IsTrue(Field<TMP_Text>("reveal").gameObject.activeSelf);
        }

        [UnityTest] public IEnumerator CreditsKeepTheApprovedArchiveContributionsAndExistingFont()
        {
            yield return LoadEnding();
            var credits = Field<TMP_Text>("credits");
            var archive = AssetDatabase.LoadAssetAtPath<GameObject>(MenuBuilder.PrefabPath)
                .GetComponent<MainMenuView>().credits.archive.content;
            foreach (var heading in new[] { "GAME DEVELOPMENT", "ART & ANIMATION", "LEVEL & PUZZLE DESIGN", "AUDIO & MUSIC", "TYPOGRAPHY & FONT DESIGN" })
                StringAssert.Contains(archive.Find(heading + "Body").GetComponent<TMP_Text>().text, credits.text);
            foreach (var name in new[] { "Devin Padin / Jarheadd1", "Douglas (DeeFeeCee)", "Sarah (AceofHome)", "Alexzander (mrdrworm)" })
                StringAssert.Contains(name, credits.text);
            Assert.NotNull(credits.font);
            Assert.AreSame(credits.font, Field<TMP_Text>("reveal").font);
            StringAssert.EndsWith("/LiberationSans SDF.asset", AssetDatabase.GetAssetPath(credits.font));
        }

        [UnityTest]
        public IEnumerator CinematicRollFullyDisplaysDeesConfirmedAudioAndTypefaceCredits()
        {
            const string audio = "<b>AUDIO & MUSIC</b>\n\n<b>Douglas (DeeFeeCee)</b>\nMusic & Sound FX";
            const string typography = "<b>TYPOGRAPHY & FONT DESIGN</b>\n\n<b>Douglas (DeeFeeCee)</b>\nOriginal Typeface: DeeFont1.17";
            yield return LoadEnding();
            var credits = Field<TMP_Text>("credits");
            var viewport = Field<RectTransform>("creditsViewport");
            StringAssert.Contains("<b>Douglas (DeeFeeCee)</b>\nRepository coordination, integration and gameplay mechanics.", credits.text);
            StringAssert.Contains(audio, credits.text);
            Assert.IsTrue(credits.text.EndsWith(typography, StringComparison.Ordinal));

            yield return Until(() => ending.CurrentStage == EndingSequence.Stage.Credits, "Credits start");
            credits.ForceMeshUpdate();
            Assert.IsFalse(credits.isTextOverflowing);
            Assert.LessOrEqual(credits.textBounds.size.x, credits.rectTransform.rect.width);
            foreach (string section in new[] { audio, typography })
            {
                int start = credits.text.IndexOf(section, StringComparison.Ordinal);
                var characters = credits.textInfo.characterInfo.Take(credits.textInfo.characterCount)
                    .Where(character => character.isVisible && character.index >= start && character.index < start + section.Length).ToArray();
                Assert.IsNotEmpty(characters);
                yield return Until(() =>
                {
                    var toViewport = viewport.worldToLocalMatrix * credits.rectTransform.localToWorldMatrix;
                    return ending.CurrentStage == EndingSequence.Stage.Credits && characters.All(character =>
                        viewport.rect.Contains(toViewport.MultiplyPoint3x4(character.bottomLeft)) &&
                        viewport.rect.Contains(toViewport.MultiplyPoint3x4(character.topRight)));
                }, "The complete credit section scrolls into view: " + section);
            }
            yield return Ready();
            Assert.AreEqual(1f, ending.CreditsProgress);
        }

        [UnityTest] public IEnumerator CinematicRollDisplaysSarahsFullCredit()
        {
            yield return LoadEnding();
            yield return Until(() => ending.CurrentStage == EndingSequence.Stage.Credits, "Cinematic credits are active");
            var credits = Field<TMP_Text>("credits");
            Assert.IsTrue(credits.gameObject.activeInHierarchy);
            StringAssert.Contains("<b>Sarah (AceofHome)</b>\nArtwork, animation and narrative collaboration.", credits.text);
        }

        [UnityTest] public IEnumerator EndingRunsAtZeroTimeScaleWithoutGameplayOrBankOwners()
        {
            Time.timeScale = 0;
            yield return LoadEnding();
            yield return Ready();
            Assert.IsNull(RunDashBank.Instance);
            Assert.IsNull(Object.FindFirstObjectByType<PlayerMovement2D>());
            Assert.IsNull(Object.FindFirstObjectByType<PauseController>(FindObjectsInactive.Include));
            Assert.IsNull(Object.FindFirstObjectByType<GameOverController>(FindObjectsInactive.Include));
            Assert.IsNull(Object.FindFirstObjectByType<MobilityReserveHUDController>(FindObjectsInactive.Include));
            Assert.IsFalse(Field<MenuInputScope>("inputScope").RuntimeActions.FindActionMap("Player").enabled);
            Assert.IsTrue(ending.TryContinue());
            yield return AwaitScene(MenuBuilder.ScenePath);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [UnityTest] public IEnumerator HeldKeyboardCannotSkipAndReturnWaitsForRelease()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
            yield return null;
            yield return LoadEnding();
            yield return Until(() => ending.CurrentStage == EndingSequence.Stage.Reveal, "Reveal while Enter is held");
            Assert.IsTrue(keyboard.enterKey.isPressed);
            Assert.IsFalse(ending.InputArmed);
            Assert.IsFalse(ending.TryContinue());
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return Ready();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
            yield return Until(() => ending.CurrentStage == EndingSequence.Stage.Returning, "Keyboard Continue");
            Assert.AreEqual(EndingSequence.ScenePath, SceneManager.GetActiveScene().path);
            Assert.IsFalse(ending.TryContinue());
            yield return new WaitForSecondsRealtime(.12f);
            Assert.AreEqual(EndingSequence.ScenePath, SceneManager.GetActiveScene().path);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return AwaitScene(MenuBuilder.ScenePath);
            Assert.IsFalse(Object.FindFirstObjectByType<MainMenuView>().launcher.IsLoading);
        }

        [UnityTest] public IEnumerator GamepadRequiresFreshConfirmationAndReturnsToMenu()
        {
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.South));
            yield return null;
            yield return LoadEnding();
            yield return Until(() => ending.CurrentStage == EndingSequence.Stage.Reveal, "Reveal while South is held");
            Assert.IsFalse(ending.InputArmed);
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return Ready();
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.South));
            yield return Until(() => ending.CurrentStage == EndingSequence.Stage.Returning, "Gamepad Continue");
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return AwaitScene(MenuBuilder.ScenePath);
            Assert.IsFalse(Object.FindFirstObjectByType<MainMenuView>().launcher.IsLoading);
        }

        [UnityTest] public IEnumerator MissingMainMenuKeepsTheRevealAndReportsTheFailure()
        {
            yield return LoadEnding();
            yield return Ready();
            FieldInfo("mainMenuScene").SetValue(ending, "Assets/MissingMainMenu.unity");
            Assert.IsFalse(ending.TryContinue());
            Assert.AreEqual("Main Menu is unavailable.", Field<TMP_Text>("status").text);
            Assert.AreEqual(EndingSequence.Stage.Reveal, ending.CurrentStage);
            Assert.IsTrue(ending.InputArmed);
            FieldInfo("mainMenuScene").SetValue(ending, MenuBuilder.ScenePath);
            Assert.IsTrue(ending.TryContinue());
            yield return AwaitScene(MenuBuilder.ScenePath);
        }

        [UnityTest] public IEnumerator MissingExitDestinationRejectsTheRequestWithoutFreezingOrSpending()
        {
            yield return Load(Level12);
            var actor = Object.FindFirstObjectByType<PlayerMovement2D>();
            var exit = Object.FindFirstObjectByType<LevelExit>();
            exit.transition.destinationScene = "Assets/MissingEnding.unity";
            LogAssert.Expect(LogType.Warning, "Exit destination is unavailable: Assets/MissingEnding.unity");
            Assert.IsFalse(exit.transition.TryExit(actor));
            Assert.IsFalse(exit.transition.TryExit(actor));
            Assert.IsFalse(exit.transition.TransitionStarted);
            Assert.IsTrue(actor.isActiveAndEnabled && actor.GetComponent<Rigidbody2D>().simulated);
            Assert.AreEqual(5, RunDashBank.Instance.RemainingCharges);
        }

        [UnityTest] public IEnumerator ReturnPreservesTheRunAndCreditsUntilGenuineNewGame()
        {
            yield return Load(Level01);
            var bank = RunDashBank.Instance;
            bank.TrySpendDash();
            var checkpoint = bank.CheckpointScenePath;
            var entry = bank.CheckpointCharges;
            int resets = 0, spends = 0;
            bank.NewRunStarted += () => resets++;
            bank.ChargesChanged += _ => spends++;
            yield return LoadEnding();
            yield return Ready();
            Assert.AreSame(bank, RunDashBank.Instance);
            Assert.AreEqual(checkpoint, bank.CheckpointScenePath);
            Assert.AreEqual(entry, bank.CheckpointCharges);
            Assert.AreEqual(4, bank.RemainingCharges);
            int arrivals = 0;
            UnityEngine.Events.UnityAction<Scene, LoadSceneMode> arrived = (scene, _) =>
            { if (scene.path == MenuBuilder.ScenePath) arrivals++; };
            SceneManager.sceneLoaded += arrived;
            try
            {
                Assert.IsTrue(ending.TryContinue());
                Assert.IsFalse(ending.TryContinue());
                yield return AwaitScene(MenuBuilder.ScenePath);
                Assert.AreEqual(1, arrivals);
            }
            finally { SceneManager.sceneLoaded -= arrived; }
            var menu = Object.FindFirstObjectByType<MainMenuView>();
            menu.creditsButton.onClick.Invoke();
            Assert.IsTrue(menu.CreditsOpen);
            menu.credits.returnButton.onClick.Invoke();
            Assert.IsFalse(menu.CreditsOpen);
            Assert.AreEqual(0, spends);
            Assert.AreEqual(0, resets);
            Assert.AreEqual(4, bank.RemainingCharges);
            menu.newGameButton.onClick.Invoke();
            yield return AwaitScene(NarrativePrologue.ScenePath);
            Assert.AreEqual(1, resets);
            Assert.AreEqual(5, bank.RemainingCharges);
            Assert.AreEqual("", bank.CheckpointScenePath);
        }

        private IEnumerator LoadEnding(bool fast = true)
        {
            UnityEngine.Events.UnityAction<Scene, LoadSceneMode> tune = (scene, _) =>
            {
                if (scene.path != EndingSequence.ScenePath) return;
                ending = Object.FindFirstObjectByType<EndingSequence>();
                ending.SendMessage("OnApplicationFocus", true);
                if (!fast) return;
                FieldInfo("whiteFadeSeconds").SetValue(ending, .05f);
                FieldInfo("whiteHoldSeconds").SetValue(ending, .08f);
                FieldInfo("darknessFadeSeconds").SetValue(ending, .06f);
                FieldInfo("creditsUnitsPerSecond").SetValue(ending, 2400f);
            };
            SceneManager.sceneLoaded += tune;
            try { yield return Load(EndingSequence.ScenePath); }
            finally { SceneManager.sceneLoaded -= tune; }
        }

        private static IEnumerator WalkIntoExit(LevelExit exit, string destination)
        {
            var actor = Object.FindFirstObjectByType<PlayerMovement2D>();
            actor.GetComponent<PlayerMovementInput2D>().enabled = false;
            var body = actor.GetComponent<Rigidbody2D>();
            var trigger = exit.GetComponent<BoxCollider2D>();
            body.position = new Vector2(trigger.bounds.min.x - .55f, trigger.bounds.center.y);
            body.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
            Assert.IsFalse(trigger.bounds.Intersects(actor.GetComponent<BoxCollider2D>().bounds));
            actor.SetHorizontalInput(1f);
            yield return AwaitScene(destination);
        }

        private static IEnumerator Load(string path)
        {
            yield return SceneManager.LoadSceneAsync(path, LoadSceneMode.Single);
            yield return AwaitScene(path);
        }

        private static IEnumerator AwaitScene(string path)
        {
            yield return Until(() => SceneManager.GetActiveScene().path == path, path);
            yield return null;
            yield return null;
        }

        private IEnumerator Ready() => Until(() => ending.CurrentStage == EndingSequence.Stage.Reveal && ending.InputArmed, "Ending is ready for Continue");

        private T Field<T>(string name) => (T)FieldInfo(name).GetValue(ending);
        private static FieldInfo FieldInfo(string name) => typeof(EndingSequence).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);

        private static IEnumerator Until(Func<bool> check, string label)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 9;
            while (!check() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.IsTrue(check(), label);
        }
    }
}
#endif
