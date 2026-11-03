using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using GameJam.Levels;
using GameJam.Mechanics;
using GameJam.Movement;
using GameJam.Resources;
using GameJam.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace GameJam.MechanicsVerification
{
    // Explicit development-build opt-in. No component is attached to ordinary gameplay scenes.
    public sealed class MechanicsStandaloneCheck : MonoBehaviour
    {
        private const string ScenePath = "Assets/Game/Scenes/Verification/GameplayMechanicsVerification.unity";
        private const string CanonicalLevel = "Assets/Game/Scenes/Levels/Level01.unity";
        private PlayerMovement2D player;
        private Rigidbody2D body;
        private Keyboard keyboard;
        private string folder, sample = "setup";
        private StreamWriter trace, results;
        private float inputDistance;
        private RunDashBank Bank => RunDashBank.Instance;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
#if DEVELOPMENT_BUILD && !UNITY_EDITOR
            string[] args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args,"-unified-check");
            if (index < 0 || index+1 >= args.Length)
            {
                // Explicit manual test entry; ordinary builds still start at the accepted Main Menu.
                if (Array.IndexOf(args,"-mechanics-room")>=0)
                    SceneManager.LoadSceneAsync(ScenePath).completed += _ => Debug.Log("Mechanics verification room ready: "+ScenePath);
                return;
            }
            var check = new GameObject("Opt-in mechanics standalone verification").AddComponent<MechanicsStandaloneCheck>();
            check.folder = Path.GetFullPath(args[index+1]); DontDestroyOnLoad(check.gameObject); check.StartCoroutine(check.Drive());
#endif
        }
        private IEnumerator Drive()
        {
            Directory.CreateDirectory(folder);
            trace = new StreamWriter(Path.Combine(folder,"physics.csv"));
            results = new StreamWriter(Path.Combine(folder,"results.txt")) { AutoFlush = true };
            trace.WriteLine("case,fixedTime,x,y,vx,vy,grounded,dashing,charges");
            var stack = new Stack<IEnumerator>(); stack.Push(Run());
            while (stack.Count > 0)
            {
                object current;
                try
                {
                    var routine = stack.Peek();
                    if (!routine.MoveNext()) { stack.Pop(); continue; }
                    current = routine.Current;
                    if (current is IEnumerator nested) { stack.Push(nested); continue; }
                }
                catch (Exception error) { results.WriteLine("FAIL: "+error); trace.Dispose(); results.Dispose(); Application.Quit(1); yield break; }
                yield return current;
            }
            results.WriteLine("PASS: all standalone checks. Simulated input and real Rigidbody2D simulation; no hardware/manual claim.");
            trace.Dispose(); results.Dispose(); Application.Quit(0);
        }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
        private IEnumerator Ticks(int count)
        { for (int i=0;i<count;i++) { yield return new WaitForFixedUpdate(); Record(); } yield return null; }
        private void Record()
        {
            if (body == null) return;
            trace.WriteLine(string.Join(",",sample,Time.fixedTime.ToString("R",CultureInfo.InvariantCulture),
                body.position.x.ToString("R",CultureInfo.InvariantCulture),body.position.y.ToString("R",CultureInfo.InvariantCulture),
                body.linearVelocity.x.ToString("R",CultureInfo.InvariantCulture),body.linearVelocity.y.ToString("R",CultureInfo.InvariantCulture),
                player.IsGrounded,player.IsDashing,Bank.RemainingCharges));
        }
        private IEnumerator Load(string name, Vector2 start, bool input = false)
        {
            Time.timeScale = 1;
            if (keyboard != null) { InputSystem.QueueStateEvent(keyboard,new KeyboardState()); yield return null; }
            // Remove old hazards AND presenters before resetting; otherwise a fresh gameplay
            // scene would correctly present the still-failed bank before the diagnostic reset.
            yield return SceneManager.LoadSceneAsync("Assets/Game/Scenes/MainMenu.unity");
            yield return null;
            if (Bank != null) Bank.InitializeNewRun();
            yield return SceneManager.LoadSceneAsync(ScenePath);
            yield return null; yield return null;
            Require(!FindFirstObjectByType<GameOverController>().IsVisible,"Fresh diagnostic case inherited a failure overlay.");
            sample = name; player = FindFirstObjectByType<LevelSetup>().Player.GetComponent<PlayerMovement2D>();
            player.GetComponent<PlayerMovementInput2D>().enabled = input; body = player.GetComponent<Rigidbody2D>();
            // Case setup only. Every measured traversal below uses the accepted player controller and physics.
            body.position = start; body.linearVelocity = Vector2.zero; Physics2D.SyncTransforms();
            foreach (var turret in FindObjectsByType<Turret2D>(FindObjectsSortMode.None)) turret.SetFiring(false);
            foreach (var shot in FindObjectsByType<TurretProjectile2D>(FindObjectsSortMode.None)) Destroy(shot.gameObject);
            yield return null;
        }
        private IEnumerator Capture(string name)
        { yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(folder,name+".png")); yield return null; }
        private IEnumerator InputTravel(bool jump)
        {
            yield return Load(jump?"W+D":"D",new Vector2(.6f,1.81f),true); yield return Ticks(8);
            InputSystem.QueueStateEvent(keyboard,jump?new KeyboardState(Key.W,Key.D):new KeyboardState(Key.D)); yield return null;
            yield return new WaitForFixedUpdate(); float x = body.position.x; yield return Ticks(10); inputDistance = body.position.x-x;
            Require(Bank.RemainingCharges == 5,"Input travel spent a charge."); yield return Capture(jump?"02-WD":"01-D");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState()); yield return null;
        }
        private IEnumerator Run()
        {
            float deadline = Time.realtimeSinceStartup+60;
            while (!Application.isFocused && Time.realtimeSinceStartup < deadline) yield return null;
            Require(Application.isFocused,"Standalone did not receive window focus within 60 seconds.");
            keyboard = InputSystem.AddDevice<Keyboard>();
            yield return InputTravel(false); float alone = inputDistance;
            yield return InputTravel(true); Require(Mathf.Abs(alone-inputDistance)<.05f && alone>1,"W+D horizontal distance differs from D.");
            results.WriteLine("Input actual travel: D="+alone+", W+D="+inputDistance+" (10 fixed ticks). PASS");

            yield return Load("fall zone",new Vector2(13.5f,1.8f)); yield return Ticks(60);
            Require(Bank.IsGameOver && Bank.FailureCause == RunFailureCause.Environmental && Bank.RemainingCharges == 5 && !body.simulated,"Fall-zone failure incorrect.");
            yield return Capture("03-fall-gameover"); results.WriteLine("Fall zone, actual falling player, preserved five charges and froze: PASS");
            var gameOver = FindFirstObjectByType<GameOverController>();
            Require(gameOver != null && gameOver.IsVisible && gameOver.InputArmed && gameOver.TryRestartRun(),"Existing Restart Run could not start.");
            for (int i=0;i<300 && SceneManager.GetActiveScene().path!=CanonicalLevel;i++) yield return null;
            Require(SceneManager.GetActiveScene().path==CanonicalLevel && Bank.RemainingCharges==5 && !Bank.IsGameOver,"Restart did not restore canonical Level01.");
            results.WriteLine("Existing Game Over Restart -> canonical Level01 and new run: PASS");

            yield return Load("horizontal rider",new Vector2(4,3.06f));
            var platforms = FindObjectsByType<MovingPlatform2D>(FindObjectsSortMode.None);
            var horizontal = Array.Find(platforms,p=>Mathf.Abs(p.pointA.position.y-p.pointB.position.y)<.01f); horizontal.speed=0;
            yield return Ticks(15); Require(player.IsGrounded,"Horizontal rider did not land.");
            float offset = body.position.x-horizontal.GetComponent<Rigidbody2D>().position.x; float start = body.position.x; horizontal.speed=1.5f;
            yield return Ticks(40);
            Require(player.IsGrounded && body.position.x>start+.8f && Mathf.Abs(body.position.x-horizontal.GetComponent<Rigidbody2D>().position.x-offset)<.08f,"Horizontal rider transport incorrect.");
            yield return Capture("04-horizontal-rider");
            var pause = FindFirstObjectByType<PauseController>(); Require(pause.TryPause(),"Pause failed.");
            var paused = body.position; yield return new WaitForSecondsRealtime(.15f);
            Require(body.position==paused && pause.IsPaused,"Pause moved player.");
            yield return Capture("05-paused-platform"); Require(pause.TryResume(),"Resume failed."); yield return Ticks(5);
            Require(body.position.x>paused.x,"Rider did not resume."); results.WriteLine("Horizontal carry + existing Pause/Resume: PASS");

            yield return Load("vertical rider",new Vector2(10,4.06f));
            var vertical = Array.Find(FindObjectsByType<MovingPlatform2D>(FindObjectsSortMode.None),p=>Mathf.Abs(p.pointA.position.x-p.pointB.position.x)<.01f); vertical.speed=0;
            yield return Ticks(15); Require(player.IsGrounded,"Vertical rider did not land.");
            float height = body.position.y-vertical.GetComponent<Rigidbody2D>().position.y; start = body.position.y; vertical.speed=2;
            yield return Ticks(40); Require(player.IsGrounded && body.position.y>start+1 && Mathf.Abs(body.position.y-vertical.GetComponent<Rigidbody2D>().position.y-height)<.08f,"Vertical rider transport incorrect.");
            yield return Capture("06-vertical-rider"); player.RequestJump(); yield return new WaitForFixedUpdate(); Require(body.linearVelocity.y>9,"Jump inherited wrong platform velocity.");
            player.RequestDash(Vector2.right); yield return Ticks(2); Require(player.IsDashing && Bank.RemainingCharges==4,"Platform dash incorrect.");
            results.WriteLine("Vertical carry, jump and charged dash departure: PASS");

            yield return Load("turret hit",new Vector2(20.5f,5)); body.gravityScale=0;
            var turret = FindFirstObjectByType<Turret2D>(); turret.SetFiring(true); yield return Ticks(80);
            Require(Bank.IsGameOver && Bank.FailureCause==RunFailureCause.Environmental && Bank.RemainingCharges==5,"Turret did not fail player without spending.");
            yield return Capture("07-turret-gameover"); int shots=turret.ShotsFired; yield return Ticks(50); Require(shots==turret.ShotsFired,"Turret fired after game over.");
            results.WriteLine("Turret swept projectile -> environmental failure, no later shots: PASS");

            yield return Load("wall impact",new Vector2(16.2f,1.81f)); yield return Ticks(8);
            var wall = FindFirstObjectByType<BreakableWall2D>(); player.RequestDash(Vector2.right); yield return Ticks(10);
            Require(wall==null && body.position.x>18.5f && Bank.RemainingCharges==4,"Approved dash wall destruction failed.");
            yield return Capture("08-broken-wall"); results.WriteLine("Approved physical dash destroyed wall, one charge: PASS");
            yield return SceneManager.LoadSceneAsync("Assets/Game/Scenes/VerificationDestination.unity"); yield return null;
            Require(Bank.RemainingCharges==4 && !Bank.IsGameOver,"Ordinary scene transition recharged bank."); results.WriteLine("Scene transition preserves bank: PASS");

            yield return Load("crusher underside",new Vector2(27,1.81f)); yield return Ticks(150);
            Require(Bank.IsGameOver && Bank.FailureCause==RunFailureCause.Environmental && Bank.RemainingCharges==5,"Descending crusher underside failed.");
            yield return Capture("09-crusher-underside"); results.WriteLine("Triggered descending crusher underside -> environmental failure: PASS");
            yield return Load("crusher top",new Vector2(27,14.2f)); var crusher=FindFirstObjectByType<FallingCrusherPlatform2D>();
            yield return Ticks(15); Require(player.IsGrounded,"Crusher rider did not land on top.");
            crusher.TryActivate(player.GetComponent<Collider2D>()); height=body.position.y-crusher.transform.position.y; yield return Ticks(50);
            Require(player.IsGrounded && !Bank.IsGameOver && Mathf.Abs(body.position.y-crusher.transform.position.y-height)<.08f,"Crusher top carry unsafe.");
            yield return Capture("10-crusher-top"); player.SetHorizontalInput(1); yield return Ticks(5); player.SetHorizontalInput(0);
            player.RequestJump(); yield return new WaitForFixedUpdate(); Require(body.linearVelocity.y>9,"Crusher top jump failed.");
            player.RequestDash(Vector2.left); yield return Ticks(2); Require(player.IsDashing && Bank.RemainingCharges==4 && !Bank.IsGameOver,"Crusher top dash incorrect.");
            results.WriteLine("Crusher top: safe descent, walk, jump, one-charge dash: PASS");
            InputSystem.RemoveDevice(keyboard); keyboard=null;
        }
    }
}
