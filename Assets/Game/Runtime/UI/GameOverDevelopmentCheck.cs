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
    /// <summary>Opt-in development evidence, absent from ordinary play and release compilation.</summary>
    public sealed class GameOverDevelopmentCheck : MonoBehaviour
    {
        private string output;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void LaunchIfRequested()
        {
            string[] args=Environment.GetCommandLineArgs();
            for(int i=0;i<args.Length-1;i++)if(args[i]=="-gameover-check")
            {
                var owner=new GameObject("Explicit Game Over development verification");DontDestroyOnLoad(owner);
                owner.AddComponent<GameOverDevelopmentCheck>().output=Path.GetFullPath(args[i+1]);
            }
        }
        private IEnumerator Start()
        {
            Directory.CreateDirectory(output);yield return new WaitForSecondsRealtime(.5f);
            var menu=FindFirstObjectByType<MainMenuView>();if(!Check(menu!=null,"Menu startup"))yield break;
            string level=menu.launcher.gameplayScene;int handle=SceneManager.GetActiveScene().handle;
            menu.newGameButton.onClick.Invoke();yield return WaitForScene(level,handle);
            var bank=RunDashBank.Instance;if(!Check(bank!=null,"Run bank initialized"))yield break;
            WriteBalance("First run",bank);
            yield return FailThroughDash();var controller=FindFirstObjectByType<GameOverController>();
            if(!Check(controller!=null&&controller.IsVisible&&controller.InputArmed,"Failure interface armed"))yield break;
            yield return Capture("game-over.png");WriteBalance("Failed run",bank);
            handle=SceneManager.GetActiveScene().handle;var previous=controller;
            controller.restartButton.onClick.Invoke();yield return WaitForScene(level,handle);
            if(!Check(previous==null&&RunDashBank.Instance==bank&&!bank.IsGameOver&&bank.RemainingCharges==bank.StartingDashCount,"Restart restores existing bank and unloads old UI"))yield break;
            if(!Check(FindObjectsByType<RunDashBank>(FindObjectsSortMode.None).Length==1,"Exactly one authoritative bank"))yield break;
            if(!Check(FindFirstObjectByType<PlayerMovement2D>().GetComponent<Rigidbody2D>().simulated,"Restarted player initialized normally"))yield break;
            WriteBalance("Restarted run",bank);yield return new WaitForSecondsRealtime(.3f);yield return Capture("restarted-run.png");
            yield return FailThroughDash();controller=FindFirstObjectByType<GameOverController>();
            handle=SceneManager.GetActiveScene().handle;previous=controller;string menuPath=controller.mainMenuScene;
            controller.mainMenuButton.onClick.Invoke();yield return WaitForScene(menuPath,handle);
            if(!Check(previous==null&&FindFirstObjectByType<GameOverController>()==null,"Return cleans up failure presentation"))yield break;
            if(!Check(RunDashBank.Instance==bank&&bank.IsGameOver&&bank.RemainingCharges==0,"Return preserves failed zero balance"))yield break;
            WriteBalance("Returned to menu",bank);yield return Capture("returned-main-menu.png");
            menu=FindFirstObjectByType<MainMenuView>();handle=SceneManager.GetActiveScene().handle;
            menu.newGameButton.onClick.Invoke();yield return WaitForScene(level,handle);
            if(!Check(bank.RemainingCharges==bank.StartingDashCount&&!bank.IsGameOver,"New Game works after failed run"))yield break;
            WriteBalance("New Game after failure",bank);
            handle=SceneManager.GetActiveScene().handle;SceneManager.LoadSceneAsync(menuPath);yield return WaitForScene(menuPath,handle);
            File.WriteAllText(Path.Combine(output,"complete.txt"),"Game Over, Restart Run, Return to Main Menu and New Game checks passed. Invoking accepted standalone Quit.");
            FindFirstObjectByType<MainMenuView>().quitButton.onClick.Invoke();
        }
        private IEnumerator FailThroughDash()
        {
            yield return new WaitForSecondsRealtime(.2f);
            var actor=FindFirstObjectByType<PlayerMovement2D>();actor.GetComponent<PlayerMovementInput2D>().enabled=false;
            var bank=RunDashBank.Instance;while(bank.RemainingCharges>1)bank.TrySpendDash();WriteBalance("Before final dash",bank);
            var body=actor.GetComponent<Rigidbody2D>();Vector2 before=body.position;actor.RequestDash(Vector2.up);yield return new WaitForFixedUpdate();
            Check(bank.IsGameOver&&bank.RemainingCharges==0,"Final charge is immediately fatal");
            Check(Vector2.Distance(before,body.position)<.0001f&&!body.simulated&&!actor.enabled&&!actor.IsDashing,"Fatal dash has no travel; player remains frozen");
            double deadline=Time.realtimeSinceStartupAsDouble+10;
            while(Time.realtimeSinceStartupAsDouble<deadline)
            {
                var controller=FindFirstObjectByType<GameOverController>();if(controller!=null&&controller.IsVisible&&controller.InputArmed)yield break;
                yield return null;
            }
            Check(false,"Failure input did not arm before timeout");
        }
        private IEnumerator WaitForScene(string path,int previousHandle)
        {
            double deadline=Time.realtimeSinceStartupAsDouble+20;
            while((SceneManager.GetActiveScene().handle==previousHandle||SceneManager.GetActiveScene().path!=path)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Check(SceneManager.GetActiveScene().path==path&&SceneManager.GetActiveScene().handle!=previousHandle,"Loaded "+path);yield return null;yield return null;
        }
        private IEnumerator Capture(string name)
        {
            yield return new WaitForSecondsRealtime(.1f);string path=Path.Combine(output,name);ScreenCapture.CaptureScreenshot(path);
            double deadline=Time.realtimeSinceStartupAsDouble+10;while(!File.Exists(path)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Check(File.Exists(path),"Captured "+name);yield return new WaitForSecondsRealtime(.1f);
        }
        private void WriteBalance(string phase,RunDashBank bank)=>File.AppendAllText(Path.Combine(output,"balances.txt"),phase+": remaining="+bank.RemainingCharges+" starting="+bank.StartingDashCount+" gameOver="+bank.IsGameOver+"\n");
        private bool Check(bool value,string message)
        {File.AppendAllText(Path.Combine(output,"checks.txt"),(value?"PASS ":"FAIL ")+message+"\n");if(!value)Application.Quit(1);return value;}
    }
}
#endif
