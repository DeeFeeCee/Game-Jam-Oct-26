#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using GameJam.Levels;
using GameJam.Movement;
using GameJam.Resources;
using GameJam.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace GameJam.LevelVerification
{
    /// <summary>Opt-in development evidence only. No behavior is installed during normal play.</summary>
    public sealed class ProductionRoomDevelopmentCheck : MonoBehaviour
    {
        private const string Level = "Assets/Game/Scenes/Levels/Level01.unity";
        private const string Destination = "Assets/Game/Scenes/VerificationDestination.unity";
        private string output;
        private Keyboard keyboard;
        private bool complete;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void LaunchIfRequested()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-production-room-check")
                {
                    var owner = new GameObject("Explicit production room verification"); DontDestroyOnLoad(owner);
                    owner.AddComponent<ProductionRoomDevelopmentCheck>().output = Path.GetFullPath(args[i + 1]);
                    break;
                }
        }

        private IEnumerator Start()
        {
            Directory.CreateDirectory(output);
            keyboard = InputSystem.AddDevice<Keyboard>();
            yield return WaitForFocus();
            yield return new WaitForSecondsRealtime(.5f);
            Require(FindFirstObjectByType<MainMenuView>() != null, "MainMenu launches first");
            yield return Capture("main-menu.png");
            // Exercise the menu's real UI input path with a virtual keyboard, not physical hardware.
            yield return Press(Key.Enter);
            yield return WaitForScene(Level);
            var bank = RunDashBank.Instance;
            Require(bank != null && bank.RemainingCharges == 5, "New Game loads converted Level01 with provisional five charges");
            Require(FindObjectsByType<PlayerMovement2D>(FindObjectsSortMode.None).Length == 1 && FindObjectsByType<RunDashBank>(FindObjectsSortMode.None).Length == 1, "Exactly one player and authoritative bank");
            var actor = FindFirstObjectByType<PlayerMovement2D>();
            actor.GetComponent<PlayerMovementInput2D>().enabled = false;
            foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(2560, 1440), new Vector2Int(1366, 900) })
            {
                Screen.SetResolution(size.x, size.y, size.x == 2560 ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
                yield return new WaitForSecondsRealtime(.7f); yield return new WaitForEndOfFrame();
                yield return WaitForFocus();
                Require(Screen.width == size.x && Screen.height == size.y, "Actual output " + size.x + "x" + size.y);
                var camera = Camera.main; var pp = camera.GetComponent<PixelPerfectCamera>();
                int scale = Mathf.Min(size.x / 640, size.y / 360);
                Vector3 min = camera.ViewportToWorldPoint(new Vector3(0, 0, 10));
                Vector3 max = camera.ViewportToWorldPoint(new Vector3(1, 1, 10));
                Require(pp.pixelRatio == scale && Mathf.Abs(camera.pixelRect.width - 640 * scale) < .01f && Mathf.Abs(camera.pixelRect.height - 360 * scale) < .01f, "Integer-scaled windowboxed viewport");
                Require((min - Vector3.zero).sqrMagnitude < .000001f && (max - new Vector3(32, 18)).sqrMagnitude < .000001f, "Whole 32x18 room visible");
                Require(camera.transform.position == new Vector3(16, 9, -10) && camera.orthographicSize == 9, "Stationary camera contract");
                var hud = FindFirstObjectByType<MobilityReserveHUDController>();
                Require(hud != null && hud.DisplayedCharges == 5, "HUD accurate at actual output size");
                Canvas.ForceUpdateCanvases(); var corners = new Vector3[4]; hud.panel.GetWorldCorners(corners);
                foreach (var corner in corners) Require(corner.x >= 0 && corner.y >= 0 && corner.x <= Screen.width && corner.y <= Screen.height, "HUD inside output bounds");
                File.AppendAllText(Path.Combine(output, "resolutions.txt"), Screen.width + "x" + Screen.height + " pixelRatio=" + pp.pixelRatio + " viewport=" + camera.pixelRect + " world=" + min + ".." + max + "\n");
                yield return Capture("level01-" + size.x + "x" + size.y + ".png");
            }
            Screen.SetResolution(1280, 720, FullScreenMode.Windowed); yield return new WaitForSecondsRealtime(.4f);
            yield return WaitForFocus();

            // Drive the accepted request API through actual Rigidbody2D simulation. Never teleport the route.
            bool first = false, second = false, third = false, launch = false, dash = false, paused = false;
            int afterLaunch = 0;
            File.WriteAllText(Path.Combine(output, "physical-route.csv"), "tick,x,y,vx,vy,grounded,dashing,remaining\n");
            for (int tick = 0; tick < 1200 && SceneManager.GetActiveScene().path == Level; tick++)
            {
                actor.SetHorizontalInput(1); float x = actor.transform.position.x;
                if (!first && x > 4.7f && actor.IsGrounded) { actor.RequestJump(); first = true; }
                if (!second && first && x > 9.2f && actor.IsGrounded) { actor.RequestJump(); second = true; }
                if (!third && second && x > 14.2f && actor.IsGrounded) { actor.RequestJump(); third = true; }
                if (!launch && third && x > 18.8f && actor.IsGrounded) { actor.RequestJump(); launch = true; }
                if (launch && !dash && ++afterLaunch >= 7) { actor.RequestDash(Vector2.up); dash = true; }
                yield return new WaitForFixedUpdate();
                if (actor == null) break;
                var body = actor.GetComponent<Rigidbody2D>();
                File.AppendAllText(Path.Combine(output, "physical-route.csv"), FormattableString.Invariant($"{tick},{body.position.x},{body.position.y},{body.linearVelocity.x},{body.linearVelocity.y},{actor.IsGrounded},{actor.IsDashing},{bank.RemainingCharges}\n"));
                if (!paused && actor.IsDashing)
                {
                    // Finish this physics/render frame before taking the pause snapshot.
                    // A WaitForFixedUpdate continuation may precede completion of its simulation.
                    yield return new WaitForEndOfFrame();
                    var pause = FindFirstObjectByType<PauseController>();
                    Require(pause.TryPause(), "Pause accepts mid-dash");
                    Vector2 position = body.position; int balance = bank.RemainingCharges;
                    yield return new WaitForSecondsRealtime(.2f);
                    File.AppendAllText(Path.Combine(output, "pause-state.txt"), "timeScale=" + Time.timeScale + " before=" + position + " after=" + body.position + " dashing=" + actor.IsDashing + " beforeCharges=" + balance + " afterCharges=" + bank.RemainingCharges + "\n");
                    Require(Time.timeScale == 0 && body.position == position && actor.IsDashing && bank.RemainingCharges == balance, "Mid-dash pause preserves state and resources");
                    Require(FindFirstObjectByType<MobilityReserveHUDController>().DisplayedCharges == balance, "HUD accurate behind Pause");
                    yield return Capture("mid-dash-pause.png");
                    Require(pause.TryResume(), "Resume accepts paused dash");
                    Require(Time.timeScale == 1 && bank.RemainingCharges == balance, "Resume restores time without refill");
                    paused = true;
                }
                if (dash && !actor.IsDashing && !complete)
                {
                    complete = true; yield return Capture("route-above-barrier.png");
                }
            }
            yield return WaitForScene(Destination);
            Require(first && second && third && launch && dash && paused, "Jump/dash demonstration route physically completed");
            Require(ReferenceEquals(bank, RunDashBank.Instance) && bank.RemainingCharges == 4 && !bank.IsGameOver, "Actual exit preserves the same bank and one-dash balance");
            Require(FindObjectsByType<PlayerMovement2D>(FindObjectsSortMode.None).Length == 1 && FindObjectsByType<RunDashBank>(FindObjectsSortMode.None).Length == 1, "Destination has one player and bank");
            yield return Capture("destination-bank-four.png");

            // Spend actual eligible dashes, landing between them. No synthetic resource reset or bypass.
            while (!bank.IsGameOver)
            {
                actor = FindFirstObjectByType<PlayerMovement2D>();
                actor.GetComponent<PlayerMovementInput2D>().enabled = false;
                double deadline = Time.realtimeSinceStartupAsDouble + 10;
                while (!actor.IsGrounded && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Require(actor.IsGrounded, "Landing rearms dash without replenishment");
                int before = bank.RemainingCharges; var body = actor.GetComponent<Rigidbody2D>(); Vector2 position = body.position;
                actor.RequestDash(Vector2.up); yield return new WaitForFixedUpdate();
                Require(bank.RemainingCharges == before - 1, "Eligible dash spends exactly one charge");
                if (bank.IsGameOver)
                    Require(bank.RemainingCharges == 0 && body.position == position && !body.simulated && !actor.enabled && !actor.IsDashing, "Final charge fails immediately with zero dash travel");
                else
                {
                    deadline = Time.realtimeSinceStartupAsDouble + 10;
                    while ((actor.IsDashing || !actor.IsGrounded) && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                    Require(actor.IsGrounded && bank.RemainingCharges == before - 1, "Completed dash and landing never refill bank");
                }
                yield return null;
            }
            var failure = FindFirstObjectByType<GameOverController>();
            double limit = Time.realtimeSinceStartupAsDouble + 10;
            while (!failure.InputArmed && Time.realtimeSinceStartupAsDouble < limit) yield return null;
            Require(failure.IsVisible && failure.InputArmed && !FindFirstObjectByType<PauseController>().TryPause(), "Game Over owns input and outranks Pause");
            Require(FindFirstObjectByType<MobilityReserveHUDController>().DisplayedCharges == 0, "Exhausted HUD balance");
            yield return Capture("game-over.png");
            failure.restartButton.onClick.Invoke(); yield return WaitForScene(Level);
            Require(ReferenceEquals(bank, RunDashBank.Instance) && bank.RemainingCharges == 5 && !bank.IsGameOver, "Restart Run returns to converted Level01 and explicitly resets same bank");
            Require(FindObjectsByType<PlayerMovement2D>(FindObjectsSortMode.None).Length == 1 && FindFirstObjectByType<MobilityReserveHUDController>().DisplayedCharges == 5, "Restart restores one player and accurate HUD");
            yield return Capture("restarted-level01.png");
            var finalPause = FindFirstObjectByType<PauseController>();
            Require(finalPause.TryPause(), "Pause after restart");
            limit = Time.realtimeSinceStartupAsDouble + 10;
            while (!finalPause.InputArmed && Time.realtimeSinceStartupAsDouble < limit) yield return null;
            Require(finalPause.InputArmed, "Pause navigation armed");
            finalPause.mainMenuButton.onClick.Invoke(); yield return WaitForScene("Assets/Game/Scenes/MainMenu.unity");
            Require(Time.timeScale == 1 && bank.RemainingCharges == 5, "Return to Main Menu preserves bank and restores time");
            File.WriteAllText(Path.Combine(output, "complete.txt"), "Actual four-size output, physical route, persistent bank, HUD, mid-dash Pause/Resume, final-charge failure, Restart and menu return verified. Virtual keyboard menu input; no claim of developer manual validation.");
            FindFirstObjectByType<MainMenuView>().quitButton.onClick.Invoke();
        }

        private IEnumerator Press(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; yield return null;
        }
        private IEnumerator WaitForFocus()
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 60;
            while (!Application.isFocused && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Require(Application.isFocused, "Verification game window focused");
        }
        private IEnumerator WaitForScene(string path)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 20;
            while (SceneManager.GetActiveScene().path != path && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Require(SceneManager.GetActiveScene().path == path, "Loaded " + path); yield return null; yield return null;
        }
        private IEnumerator Capture(string name)
        {
            yield return new WaitForSecondsRealtime(.1f);
            string path = Path.Combine(output, name); ScreenCapture.CaptureScreenshot(path);
            double deadline = Time.realtimeSinceStartupAsDouble + 10;
            while (!File.Exists(path) && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Require(File.Exists(path), "Captured " + name); yield return new WaitForSecondsRealtime(.1f);
        }
        private void Require(bool correct, string message)
        {
            File.AppendAllText(Path.Combine(output, "checks.txt"), (correct ? "PASS " : "FAIL ") + message + "\n");
            if (!correct) { Application.Quit(1); throw new InvalidOperationException(message); }
        }
        private void OnDestroy()
        {
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
        }
    }
}
#endif
