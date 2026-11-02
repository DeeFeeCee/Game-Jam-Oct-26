#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using GameJam.Movement;
using GameJam.Resources;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameJam.UI
{
    /// <summary>Explicit standalone evidence only; never enabled during ordinary play.</summary>
    public sealed class PauseDevelopmentCheck : MonoBehaviour
    {
        private string output;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void LaunchIfRequested()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-pause-check")
            {
                var owner = new GameObject("Explicit Pause development verification"); DontDestroyOnLoad(owner);
                owner.AddComponent<PauseDevelopmentCheck>().output = Path.GetFullPath(args[i + 1]);
            }
        }
        private IEnumerator Start()
        {
            Directory.CreateDirectory(output); yield return new WaitForSecondsRealtime(.5f);
            var menu = FindFirstObjectByType<MainMenuView>(); if (!Check(menu != null, "Menu startup")) yield break;
            string level = menu.launcher.gameplayScene; int handle = SceneManager.GetActiveScene().handle;
            menu.newGameButton.onClick.Invoke(); yield return WaitForScene(level, handle);
            var bank = RunDashBank.Instance; if (!Check(bank != null, "One initialized bank")) yield break;
            yield return new WaitForSecondsRealtime(.3f);
            var actor = FindFirstObjectByType<PlayerMovement2D>(); actor.RequestDash(Vector2.up);
            yield return new WaitForFixedUpdate(); yield return new WaitForSecondsRealtime(.3f);
            if (!Check(bank.RemainingCharges == bank.StartingDashCount - 1, "Actual nonfatal dash spends exactly one")) yield break;
            var pause = FindFirstObjectByType<PauseController>(); if (!Check(pause != null && pause.TryPause(), "Pause opens")) yield break;
            yield return WaitForArmed(pause); Record("Paused", bank);
            Vector2 position = actor.GetComponent<Rigidbody2D>().position; float scaledTime = Time.time;
            yield return new WaitForSecondsRealtime(.3f);
            if (!Check(Time.timeScale == 0 && Time.time == scaledTime && actor.GetComponent<Rigidbody2D>().position == position, "Scaled time and physical world stay stopped")) yield break;
            yield return Capture("pause.png");
            pause.controlsButton.onClick.Invoke(); if (!Check(pause.ControlsOpen && Time.timeScale == 0, "Controls opens without resuming")) yield break;
            yield return Capture("pause-controls.png"); pause.backButton.onClick.Invoke();
            if (!Check(!pause.ControlsOpen && pause.IsPaused && Time.timeScale == 0, "Back returns to paused root")) yield break;
            pause.resumeButton.onClick.Invoke();
            double deadline = Time.realtimeSinceStartupAsDouble + 10;
            while (pause.WaitingForGameplayRelease && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            if (!Check(!pause.IsPaused && Time.timeScale == 1 && actor.GetComponent<PlayerMovementInput2D>().enabled, "Resume restores time and gameplay input")) yield break;
            if (!Check(bank.RemainingCharges == bank.StartingDashCount - 1, "Pause and Resume preserve exact bank balance")) yield break;
            Record("Resumed", bank); yield return Capture("resumed.png");
            if (!Check(pause.TryPause(), "Pause reopens")) yield break;
            yield return WaitForArmed(pause); handle = SceneManager.GetActiveScene().handle; var old = pause; string menuPath = pause.mainMenuScene;
            pause.mainMenuButton.onClick.Invoke(); yield return WaitForScene(menuPath, handle);
            if (!Check(old == null && Time.timeScale == 1 && bank.RemainingCharges == bank.StartingDashCount - 1 && !bank.IsGameOver, "Return cleans pause, restores time and preserves spent bank")) yield break;
            Record("Returned menu", bank); yield return Capture("returned-menu.png");
            menu = FindFirstObjectByType<MainMenuView>(); menu.controlsButton.onClick.Invoke();
            if (!Check(menu.ControlsOpen, "Returned menu remains interactive")) yield break;
            menu.backButton.onClick.Invoke(); handle = SceneManager.GetActiveScene().handle; menu.newGameButton.onClick.Invoke();
            yield return WaitForScene(level, handle); Record("New Game", bank);
            if (!Check(bank.RemainingCharges == bank.StartingDashCount && Time.timeScale == 1, "New Game explicitly resets fresh run")) yield break;
            actor = FindFirstObjectByType<PlayerMovement2D>(); actor.GetComponent<PlayerMovementInput2D>().enabled = false;
            while (bank.RemainingCharges > 1) bank.TrySpendDash();
            var body = actor.GetComponent<Rigidbody2D>(); position = body.position; actor.RequestDash(Vector2.up); yield return new WaitForFixedUpdate();
            if (!Check(bank.IsGameOver && bank.RemainingCharges == 0 && !body.simulated && body.position == position, "Fatal charge still freezes immediately without travel")) yield break;
            deadline = Time.realtimeSinceStartupAsDouble + 10;
            var failure = FindFirstObjectByType<GameOverController>();
            while (!failure.InputArmed && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            pause = FindFirstObjectByType<PauseController>();
            if (!Check(failure.IsVisible && !pause.TryPause() && Time.timeScale == 1, "Game Over takes priority over Pause")) yield break;
            Record("Failed after pause flow", bank); handle = SceneManager.GetActiveScene().handle;
            failure.restartButton.onClick.Invoke(); yield return WaitForScene(level, handle);
            if (!Check(!bank.IsGameOver && bank.RemainingCharges == bank.StartingDashCount && Time.timeScale == 1, "Accepted Game Over Restart still restores run")) yield break;
            if (!Check(FindObjectsByType<RunDashBank>(FindObjectsSortMode.None).Length == 1 && FindObjectsByType<PauseController>(FindObjectsSortMode.None).Length == 1, "Exactly one bank and one pause owner after restart")) yield break;
            Record("Game Over Restart", bank); handle = SceneManager.GetActiveScene().handle;
            SceneManager.LoadSceneAsync(menuPath); yield return WaitForScene(menuPath, handle);
            File.WriteAllText(Path.Combine(output, "complete.txt"), "Pause, Controls, Back, Resume, Return, New Game, fatal failure and Restart checks passed. Invoking accepted Quit.");
            FindFirstObjectByType<MainMenuView>().quitButton.onClick.Invoke();
        }
        private IEnumerator WaitForArmed(PauseController pause)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 10;
            while (!pause.InputArmed && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Check(pause.InputArmed, "Pause interface armed after release");
        }
        private IEnumerator WaitForScene(string path, int handle)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 20;
            while ((SceneManager.GetActiveScene().handle == handle || SceneManager.GetActiveScene().path != path) && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Check(SceneManager.GetActiveScene().handle != handle && SceneManager.GetActiveScene().path == path, "Loaded " + path);
            yield return null; yield return null;
        }
        private IEnumerator Capture(string name)
        {
            yield return new WaitForSecondsRealtime(.1f);
            string path = Path.Combine(output, name); ScreenCapture.CaptureScreenshot(path);
            double deadline = Time.realtimeSinceStartupAsDouble + 10;
            while (!File.Exists(path) && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Check(File.Exists(path), "Captured " + name); yield return new WaitForSecondsRealtime(.1f);
        }
        private void Record(string phase, RunDashBank bank) => File.AppendAllText(Path.Combine(output, "state.txt"), phase + ": timeScale=" + Time.timeScale + " remaining=" + bank.RemainingCharges + " starting=" + bank.StartingDashCount + " gameOver=" + bank.IsGameOver + "\n");
        private bool Check(bool success, string message)
        {
            File.AppendAllText(Path.Combine(output, "checks.txt"), (success ? "PASS " : "FAIL ") + message + "\n");
            if (!success) Application.Quit(1); return success;
        }
    }
}
#endif
