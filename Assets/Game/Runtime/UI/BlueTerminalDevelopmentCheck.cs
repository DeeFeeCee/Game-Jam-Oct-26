using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using GameJam.Movement;
using GameJam.Resources;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace GameJam.UI
{
    /// <summary>Opt-in keyboard, mouse, and physics verification for the isolated blue room.</summary>
    public sealed class BlueTerminalDevelopmentCheck : MonoBehaviour
    {
        public const string Room = "Assets/Game/Scenes/Verification/BlueArtTerminal.unity";
        private const string Destination = "Assets/Game/Scenes/VerificationDestination.unity";
        private const string LongEntry = "TEMPORARY UI VERIFICATION\n\nThis deliberately longer entry checks that the same terminal frame can display several lines of readable text. Each terminal stores its own title and body in the Inspector. This sample verifies wrapping, contrast, and margins at different window sizes. It is placeholder text for interface testing.";
        private string output;
        private Action<bool> completed;
        private Keyboard keyboard;
        private Mouse mouse;
        private ResearchTerminal terminal;
        private PlayerMovement2D actor;
        private Rigidbody2D body;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
#if DEVELOPMENT_BUILD && !UNITY_EDITOR
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-blue-check");
            if (index >= 0 && index + 1 < args.Length) Begin(args[index + 1], null);
#endif
        }

        public static void Begin(string folder, Action<bool> finished)
        {
            var check = new GameObject("Blue terminal verification").AddComponent<BlueTerminalDevelopmentCheck>();
            DontDestroyOnLoad(check.gameObject);
            check.output = Path.GetFullPath(folder);
            check.completed = finished;
            check.StartCoroutine(check.Drive());
        }

        private IEnumerator Drive()
        {
            Directory.CreateDirectory(output);
            var steps = new Stack<IEnumerator>();
            steps.Push(Run());
            while (steps.Count > 0)
            {
                object item;
                try
                {
                    var step = steps.Peek();
                    if (!step.MoveNext()) { steps.Pop(); continue; }
                    item = step.Current;
                    if (item is IEnumerator nested) { steps.Push(nested); continue; }
                }
                catch (Exception error)
                {
                    Finish(false, error.ToString());
                    yield break;
                }
                yield return item;
            }
            Finish(true, "Focused virtual keyboard and mouse, real physics, and runtime captures.");
        }

        private IEnumerator Run()
        {
            yield return WaitFor(() => Application.isFocused, "Application focus", 25);
            yield return new WaitForSecondsRealtime(.3f);
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            yield return BindRoom();
            var bank = RunDashBank.Instance;
            int startingCharges = bank.RemainingCharges;
            var renderer = terminal.GetComponentInChildren<Animator>().GetComponent<SpriteRenderer>();
            var accent = renderer.transform.Find("Blink status accent").GetComponent<SpriteRenderer>();
            Vector3 terminalPosition = terminal.transform.position;
            Color32 normalPixel = default;
            Color32 blinkPixel = default;
            yield return CaptureBlinkFrame(renderer, accent, false, "01-terminal-normal.png", pixel => normalPixel = pixel);
            yield return CaptureBlinkFrame(renderer, accent, true, "02-terminal-blink.png", pixel => blinkPixel = pixel);
            Require(normalPixel.r != blinkPixel.r && normalPixel.g != blinkPixel.g, "Rendered LED pixels differ across frames");
            yield return WaitFor(() => renderer.sprite.name == "BlueTerminal_Norm" && !accent.enabled, "Loop back to normal frame");
            Require(terminal.transform.position == terminalPosition, "Stationary terminal");
            Pass("Both visible frames and looping");

            float startX = body.position.x;
            yield return WalkTo(startX - .6f, Key.A);
            yield return WalkTo(7.25f, Key.D);
            yield return Open();
            Require(terminal.title.text == "RESEARCH LOG" && terminal.lore.text.StartsWith("TEMPORARY UI VERIFICATION"), "Authored title and body");
            CheckLayout(false);
            yield return Capture("03-research-short.png");
            yield return CloseWithMouse();
            Pass("Move left/right, open with E, and close with the visible button");

            var sizes = Application.isEditor
                ? new[] { new Vector2Int(Screen.width, Screen.height) }
                : new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(2560, 1440), new Vector2Int(1366, 900) };
            terminal.lore.text = LongEntry;
            foreach (var size in sizes)
            {
                if (!Application.isEditor)
                {
                    var mode = size.x == 2560 ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
                    Screen.SetResolution(size.x, size.y, mode);
                    yield return new WaitForSecondsRealtime(.6f);
                    yield return WaitFor(() => Screen.width == size.x && Screen.height == size.y, $"Actual viewport {size}");
                }
                yield return Open();
                CheckLayout(true);
                yield return Capture($"04-research-{Screen.width}x{Screen.height}.png");
                yield return CloseWithEscape();
                Pass($"Readable wrapped entry and close at {size.x}x{size.y}");
            }

            terminal.lore.text = " ";
            yield return Open();
            Require(terminal.emptyEntry.isActiveAndEnabled, "Blank entry fallback visible");
            yield return Capture("05-empty-entry.png");
            yield return CloseWithEscape();
            Require(bank.RemainingCharges == startingCharges, "Terminal interactions preserve charges");
            Pass("Repeated opens, empty entry, and release gating");

            var pause = FindFirstObjectByType<PauseController>();
            yield return Tap(Key.Escape);
            yield return WaitFor(() => pause.IsPaused && pause.InputArmed, "Pause opens and arms");
            Require(Time.timeScale == 0 && !terminal.IsOpen, "Pause owns modal input");
            yield return Tap(Key.Escape);
            yield return WaitFor(() => !pause.IsPaused && actor.GetComponent<PlayerMovementInput2D>().enabled, "Resume returns gameplay input");

            var narrow = GameObject.Find("Verification platform - 3 cells").GetComponent<BoxCollider2D>();
            var wide = GameObject.Find("Verification platform - 7 cells").GetComponent<BoxCollider2D>();
            CheckPlatform(narrow, 3);
            CheckPlatform(wide, 7);
            yield return WalkTo(10.35f, Key.D);
            yield return JumpTo(narrow);
            yield return WalkTo(14.25f, Key.D);
            Require(OnTop(narrow), "Walk across narrow platform stays supported");
            yield return JumpTo(wide);
            yield return WalkTo(19.5f, Key.D);
            Require(OnTop(wide), "Walk across wide platform stays supported");
            yield return Keys(Key.D, Key.Space);
            yield return WaitFor(() => actor.IsDashing, "Dash starts near wide platform");
            yield return Keys();
            yield return WaitFor(() => !actor.IsDashing && OnTop(wide), "Dash finishes on wide platform");
            Require(bank.RemainingCharges == startingCharges - 1, "Dash spends exactly one charge");
            yield return Capture("06-platform-traversal.png");
            Pass("Land, walk, jump between and dash across 3-cell and 7-cell solids");

            yield return WalkTo(26, Key.D);
            yield return Keys(Key.D, Key.UpArrow);
            yield return WaitFor(() => SceneManager.GetActiveScene().path == Destination, "Physical exit transition", 8);
            var arrivingPlayer = FindFirstObjectByType<PlayerMovement2D>();
            var arrivingBody = arrivingPlayer.GetComponent<Rigidbody2D>();
            float entryX = arrivingBody.position.x;
            for (int tick = 0; tick < 12; tick++)
            {
                Require(keyboard.upArrowKey.isPressed && arrivingBody.linearVelocity.y < .1f,
                    "Held Up crosses the exit without a new jump");
                yield return new WaitForFixedUpdate();
            }
            Require(arrivingBody.position.x > entryX + .3f, "Held D still moves after entry");
            yield return Keys();
            Require(RunDashBank.Instance == bank && bank.RemainingCharges == startingCharges - 1, "Exit preserves bank");
            Pass("Physical exit with Up and D held. No new jump, D keeps walking, same dash balance.");
            if (!Application.isEditor)
            {
                bank.InitializeNewRun();
                yield return SceneManager.LoadSceneAsync(Room);
                yield return BindRoom();
                yield return WalkTo(7.25f, Key.D);
                yield return Open();
                yield return CloseWithMouse();
                Pass("Fresh verification-room run repeats terminal interaction");
            }
        }

        private IEnumerator BindRoom()
        {
            Require(SceneManager.GetActiveScene().path == Room, "BlueArtTerminal scene is active");
            yield return WaitFor(() => FindFirstObjectByType<PlayerMovement2D>() != null, "LevelSetup spawns player");
            terminal = FindFirstObjectByType<ResearchTerminal>();
            actor = FindFirstObjectByType<PlayerMovement2D>();
            body = actor.GetComponent<Rigidbody2D>();
            var bank = RunDashBank.Instance;
            Require(FindObjectsByType<PlayerMovement2D>(FindObjectsSortMode.None).Length == 1 &&
                    FindObjectsByType<RunDashBank>(FindObjectsSortMode.None).Length == 1, "One player and one bank");
            Require(terminal != null && bank != null && !bank.IsGameOver && bank.RemainingCharges == 5 &&
                    Time.timeScale == 1 && body.simulated && actor.GetComponent<PlayerMovementInput2D>().enabled &&
                    !terminal.overlay.activeInHierarchy, "Healthy direct-entry gameplay state");
        }

        private IEnumerator WalkTo(float x, Key key)
        {
            yield return Keys(key);
            yield return WaitFor(() => key == Key.A ? body.position.x <= x : body.position.x >= x, $"Walk {key} to {x}", 8);
            yield return Keys();
        }

        private IEnumerator JumpTo(BoxCollider2D platform)
        {
            yield return Keys(Key.D, Key.W);
            yield return WaitFor(() => body.linearVelocity.y > 2, "W jumps");
            yield return Keys(Key.D);
            yield return WaitFor(() => OnTop(platform), "Land on " + platform.name);
            yield return Keys();
        }

        private bool OnTop(BoxCollider2D platform)
        {
            float bottom = actor.GetComponent<BoxCollider2D>().bounds.min.y;
            return actor.IsGrounded && Mathf.Abs(bottom - platform.bounds.max.y) < .07f &&
                body.position.x >= platform.bounds.min.x && body.position.x <= platform.bounds.max.x;
        }

        private static void CheckPlatform(BoxCollider2D platform, int width)
        {
            var art = platform.transform.Find("Blue platform art").GetComponent<SpriteRenderer>();
            Require(platform.size == new Vector2(width, 1) && platform.transform.lossyScale == Vector3.one &&
                Mathf.Abs(art.bounds.min.x - platform.bounds.min.x) < .01f &&
                Mathf.Abs(art.bounds.max.x - platform.bounds.max.x) < .01f &&
                Mathf.Abs(art.bounds.max.y - platform.bounds.max.y) < .01f &&
                art.sprite.texture.filterMode == FilterMode.Point, "Point-filtered art and collider alignment at width " + width);
        }

        private IEnumerator Open()
        {
            yield return Tap(Key.E);
            yield return WaitFor(() => terminal.IsOpen && terminal.InputArmed, "Terminal opens and arms");
            Require(Time.timeScale == 0 && !actor.GetComponent<PlayerMovementInput2D>().enabled &&
                terminal.closeButton.interactable, "Modal suspends movement and enables Close after release");
        }

        private IEnumerator CloseWithMouse()
        {
            var rect = (RectTransform)terminal.closeButton.transform;
            Vector2 point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point, buttons = 1 });
            yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return WaitFor(Closed, "Visible Close button and neutral gate");
        }

        private IEnumerator CloseWithEscape()
        {
            yield return Tap(Key.Escape);
            yield return WaitFor(Closed, "Escape closes and returns gameplay");
            Require(!FindFirstObjectByType<PauseController>().IsPaused, "Close key does not open Pause");
        }

        private bool Closed() => !terminal.IsOpen && ResearchTerminal.Owner == null && Time.timeScale == 1 &&
            actor.GetComponent<PlayerMovementInput2D>().enabled;

        private void CheckLayout(bool wrapped)
        {
            Canvas.ForceUpdateCanvases();
            terminal.title.ForceMeshUpdate();
            terminal.lore.ForceMeshUpdate();
            var frame = terminal.overlay.transform.Find("Textbox bounds/Ace textbox frame").GetComponent<UnityEngine.UI.Image>();
            var rect = frame.rectTransform.rect;
            Require(frame.sprite.name == "BlueTerminalTextbox_0" && Mathf.Abs(rect.width / rect.height - 16f / 9) < .01f,
                "Correct frame sprite and aspect");
            var corners = new Vector3[4];
            frame.rectTransform.GetWorldCorners(corners);
            Require(corners[2].x - corners[0].x < Screen.width * .7f && corners[2].y - corners[0].y < Screen.height * .75f,
                "Textbox is bounded within the viewport");
            Require(terminal.title.textInfo.characterCount > 0 && !terminal.title.isTextTruncated, "Title fits");
            Require(terminal.lore.textInfo.characterCount > 0 && !terminal.lore.isTextTruncated &&
                (!wrapped || terminal.lore.textInfo.lineCount >= 3), "Body text fits and wraps");
            Require(InsideSurface((RectTransform)terminal.title.transform) &&
                InsideSurface((RectTransform)terminal.lore.transform) &&
                InsideSurface((RectTransform)terminal.closeButton.transform), "Title, body and Close fit their surfaces");
        }

        private static bool InsideSurface(RectTransform content)
        {
            var corners = new Vector3[4];
            content.GetWorldCorners(corners);
            var surface = (RectTransform)content.parent;
            foreach (Vector3 corner in corners)
                if (!surface.rect.Contains(surface.InverseTransformPoint(corner))) return false;
            return true;
        }

        private IEnumerator Keys(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            yield return null;
            yield return null;
        }

        private IEnumerator Tap(Key key) { yield return Keys(key); yield return Keys(); }

        private static IEnumerator WaitFor(Func<bool> predicate, string detail, float timeout = 5)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
            Require(predicate(), detail);
        }

        private IEnumerator Capture(string file)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output, file));
            yield return WaitFor(() => File.Exists(Path.Combine(output, file)), "Runtime capture " + file);
        }

        private IEnumerator CaptureBlinkFrame(SpriteRenderer renderer, SpriteRenderer accent, bool blinking,
            string file, Action<Color32> sampled)
        {
            string expected = blinking ? "BlueTerminal_Blink" : "BlueTerminal_Norm";
            float deadline = Time.realtimeSinceStartup + 5;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return new WaitForEndOfFrame();
                if (renderer.sprite.name != expected || accent.enabled != blinking) continue;

                // Synchronously read this rendered frame before the animation can advance.
                var image = ScreenCapture.CaptureScreenshotAsTexture();
                try
                {
                    Vector3 center = Camera.main.WorldToScreenPoint(accent.transform.position);
                    sampled(image.GetPixel(Mathf.FloorToInt(center.x), Mathf.FloorToInt(center.y)));
                    File.WriteAllBytes(Path.Combine(output, file), image.EncodeToPNG());
                }
                finally { Destroy(image); }
                yield break;
            }
            throw new InvalidOperationException("Could not capture rendered frame " + expected);
        }

        private static void Require(bool condition, string detail)
        {
            if (!condition) throw new InvalidOperationException(detail);
        }

        private void Pass(string detail) => File.AppendAllText(Path.Combine(output, "result.txt"), "PASS " + detail + "\n");

        private void Finish(bool passed, string detail)
        {
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            File.AppendAllText(Path.Combine(output, "result.txt"), (passed ? "PASS: " : "FAIL: ") + detail + "\n");
            if (completed != null) completed(passed);
            else Application.Quit(passed ? 0 : 1);
        }
    }
}
