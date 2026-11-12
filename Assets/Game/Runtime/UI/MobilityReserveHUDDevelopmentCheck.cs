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
    /// <summary>Opt-in runtime evidence; no diagnostic behavior runs in ordinary play.</summary>
    public sealed class MobilityReserveHUDDevelopmentCheck : MonoBehaviour
    {
        private string output;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void LaunchIfRequested()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-hud-check")
            {
                var owner = new GameObject("Explicit Mobility HUD development verification"); DontDestroyOnLoad(owner);
                owner.AddComponent<MobilityReserveHUDDevelopmentCheck>().output = Path.GetFullPath(args[i + 1]);
            }
        }
        private IEnumerator Start()
        {
            Directory.CreateDirectory(output); yield return new WaitForSecondsRealtime(.5f);
            var menu = FindFirstObjectByType<MainMenuView>();
            if (!Check(menu != null && FindFirstObjectByType<MobilityReserveHUDController>() == null, "MainMenu starts without gameplay HUD")) yield break;
            string level = NarrativePrologue.LevelPath; int handle = SceneManager.GetActiveScene().handle;
            menu.newGameButton.onClick.Invoke(); yield return WaitForScene(level, handle);
            if (!CheckDisplay(5, "New Game")) yield break;
            foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(2560, 1440), new Vector2Int(1024, 768) })
            {
                // Use native borderless output for QHD so desktop window borders cannot reduce its client area.
                var mode = size.x == 2560 ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
                Screen.SetResolution(size.x, size.y, mode); yield return new WaitForSecondsRealtime(.6f);
                if (!Check(Screen.width == size.x && Screen.height == size.y, "Actual output " + size.x + "x" + size.y)) yield break;
                Canvas.ForceUpdateCanvases(); var hud = FindFirstObjectByType<MobilityReserveHUDController>(); var corners = new Vector3[4]; hud.panel.GetWorldCorners(corners);
                foreach (var point in corners)
                    if (!Check(point.x >= 0 && point.y >= 0 && point.x <= Screen.width && point.y <= Screen.height, "HUD panel within output bounds")) yield break;
                foreach (var text in hud.GetComponentsInChildren<TMPro.TMP_Text>())
                    if (!Check(!text.isTextOverflowing, "No text overflow: " + text.name)) yield break;
                File.AppendAllText(Path.Combine(output, "resolutions.txt"), Screen.width + "x" + Screen.height + " mode=" + Screen.fullScreenMode + " panel=" + (corners[2] - corners[0]) + "\n");
                yield return Capture("five-" + size.x + "x" + size.y + ".png");
            }
            Screen.SetResolution(1280, 720, FullScreenMode.Windowed); yield return new WaitForSecondsRealtime(.4f);
            yield return SpendActualDash(); yield return SpendActualDash();
            if (!CheckDisplay(3, "After two actual dashes")) yield break; yield return Capture("three.png");
            yield return SpendActualDash(); yield return SpendActualDash();
            if (!CheckDisplay(1, "Critical reserve")) yield break;
            if (!Check(FindFirstObjectByType<MobilityReserveHUDController>().IsCritical, "Critical last-charge indicator")) yield break;
            yield return Capture("one-critical.png"); yield return SpendActualDash();
            if (!CheckDisplay(0, "Fatal dash")) yield break;
            double deadline = Time.realtimeSinceStartupAsDouble + 10;
            var failure = FindFirstObjectByType<GameOverController>();
            while (!failure.InputArmed && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            if (!Check(failure.IsVisible && failure.InputArmed, "Game Over owns presentation and input")) yield break;
            yield return Capture("zero-game-over.png"); handle = SceneManager.GetActiveScene().handle;
            failure.restartButton.onClick.Invoke(); yield return WaitForScene(level, handle);
            if (!CheckDisplay(5, "Restart Run")) yield break;
            var pause = FindFirstObjectByType<PauseController>(); if (!Check(pause.TryPause(), "Pause opens with HUD")) yield break;
            yield return WaitForPause(pause);
            if (!CheckDisplay(5, "Paused") || !Check(Time.timeScale == 0, "HUD remains accurate at time zero")) yield break;
            yield return Capture("hud-behind-pause.png"); pause.resumeButton.onClick.Invoke();
            deadline = Time.realtimeSinceStartupAsDouble + 10;
            while (pause.WaitingForGameplayRelease && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            if (!CheckDisplay(5, "Resumed") || !Check(Time.timeScale == 1 && !pause.IsPaused, "Resume retains balance and normal time")) yield break;
            yield return SpendActualDash(); if (!CheckDisplay(4, "Spent before menu return")) yield break;
            if (!Check(pause.TryPause(), "Pause reopens")) yield break; yield return WaitForPause(pause);
            string menuPath = pause.mainMenuScene; handle = SceneManager.GetActiveScene().handle; pause.mainMenuButton.onClick.Invoke();
            yield return WaitForScene(menuPath, handle);
            if (!Check(FindFirstObjectByType<MobilityReserveHUDController>() == null && RunDashBank.Instance.RemainingCharges == 4 && Time.timeScale == 1, "Menu return removes HUD without refill")) yield break;
            Record("Returned menu"); menu = FindFirstObjectByType<MainMenuView>(); handle = SceneManager.GetActiveScene().handle;
            menu.newGameButton.onClick.Invoke(); yield return WaitForScene(level, handle);
            if (!CheckDisplay(5, "Second New Game")) yield break;
            if (!Check(FindObjectsByType<MobilityReserveHUDController>(FindObjectsSortMode.None).Length == 1 && FindObjectsByType<RunDashBank>(FindObjectsSortMode.None).Length == 1, "One HUD and one bank")) yield break;
            handle = SceneManager.GetActiveScene().handle; SceneManager.LoadSceneAsync(menuPath); yield return WaitForScene(menuPath, handle);
            File.WriteAllText(Path.Combine(output, "complete.txt"), "HUD balance sequence, four actual output sizes, fatal failure, Restart, Pause/Resume, menu return and New Game passed. Invoking accepted Quit.");
            FindFirstObjectByType<MainMenuView>().quitButton.onClick.Invoke();
        }
        private IEnumerator SpendActualDash()
        {
            var actor = FindFirstObjectByType<PlayerMovement2D>(); var bank = RunDashBank.Instance;
            double deadline = Time.realtimeSinceStartupAsDouble + 10;
            while (!actor.IsGrounded && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            if (!Check(actor.IsGrounded, "Landing restores existing dash eligibility")) yield break;
            int before = bank.RemainingCharges; var body = actor.GetComponent<Rigidbody2D>(); Vector2 position = body.position;
            var input = actor.GetComponent<PlayerMovementInput2D>(); bool restore = input.enabled; input.enabled = false;
            actor.RequestDash(Vector2.up); yield return new WaitForFixedUpdate();
            if (!Check(bank.RemainingCharges == before - 1, "Actual eligible dash spends exactly one")) yield break;
            if (bank.IsGameOver)
            {
                Check(bank.RemainingCharges == 0 && !actor.enabled && !actor.IsDashing && !body.simulated && body.position == position, "Fatal dash remains frozen with zero travel");
            }
            else
            {
                deadline = Time.realtimeSinceStartupAsDouble + 10;
                while ((actor.IsDashing || !actor.IsGrounded) && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                if (!Check(actor.IsGrounded && !actor.IsDashing, "Actual dash completes and lands")) yield break;
                if (restore) input.enabled = true;
            }
            CheckDisplay(bank.RemainingCharges, "Actual dash result");
        }
        private bool CheckDisplay(int count, string phase)
        {
            var bank = RunDashBank.Instance; var hud = FindFirstObjectByType<MobilityReserveHUDController>();
            bool correct = bank != null && hud != null && bank.RemainingCharges == count &&
                hud.DisplayedCharges == count && hud.counter.text == count.ToString() &&
                hud.dashIcon.color == (count == 1 ? hud.criticalColor : count == 0 ? hud.exhaustedColor : hud.activeColor);
            Record(phase);
            return Check(correct, phase + " synchronized bank, counter and icon");
        }
        private IEnumerator WaitForPause(PauseController pause)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 10;
            while (!pause.InputArmed && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Check(pause.IsPaused && pause.InputArmed, "Pause interface armed after release");
        }
        private IEnumerator WaitForScene(string path, int handle)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 20;
            while ((SceneManager.GetActiveScene().handle == handle || SceneManager.GetActiveScene().path != path) && Time.realtimeSinceStartupAsDouble < deadline)
            {
                if (path == NarrativePrologue.LevelPath) FindFirstObjectByType<NarrativePrologue>()?.TrySkip();
                yield return null;
            }
            Check(SceneManager.GetActiveScene().handle != handle && SceneManager.GetActiveScene().path == path, "Loaded " + path); yield return null; yield return null;
        }
        private IEnumerator Capture(string name)
        {
            yield return new WaitForSecondsRealtime(.1f); string path = Path.Combine(output, name); ScreenCapture.CaptureScreenshot(path);
            double deadline = Time.realtimeSinceStartupAsDouble + 10;
            while (!File.Exists(path) && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Check(File.Exists(path), "Captured " + name); yield return new WaitForSecondsRealtime(.1f);
        }
        private void Record(string phase)
        {
            var bank = RunDashBank.Instance; var hud = FindFirstObjectByType<MobilityReserveHUDController>();
            File.AppendAllText(Path.Combine(output, "state.txt"), phase + ": remaining=" + (bank != null ? bank.RemainingCharges : -1) + " displayed=" + (hud != null ? hud.DisplayedCharges : -1) + " timeScale=" + Time.timeScale + " gameOver=" + (bank != null && bank.IsGameOver) + "\n");
        }
        private bool Check(bool correct, string message)
        {
            File.AppendAllText(Path.Combine(output, "checks.txt"), (correct ? "PASS " : "FAIL ") + message + "\n");
            if (!correct) Application.Quit(1); return correct;
        }
    }
}
#endif
