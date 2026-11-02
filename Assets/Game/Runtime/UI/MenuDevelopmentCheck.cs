#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using GameJam.Resources;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameJam.UI
{
    /// <summary>Explicit development-build verification; never starts in ordinary play.</summary>
    public sealed class MenuDevelopmentCheck : MonoBehaviour
    {
        private string output;
        private string menuScene;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void LaunchIfRequested()
        {
            string[] args=Environment.GetCommandLineArgs();
            for(int i=0;i<args.Length-1;i++)if(args[i]=="-menu-check")
            {
                var owner=new GameObject("Explicit menu build verification");DontDestroyOnLoad(owner);
                var check=owner.AddComponent<MenuDevelopmentCheck>();check.output=Path.GetFullPath(args[i+1]);
            }
        }
        private IEnumerator Start()
        {
            Directory.CreateDirectory(output);menuScene=SceneManager.GetActiveScene().path;
            if(!Check(SceneManager.GetActiveScene().name=="MainMenu","Build starts in MainMenu"))yield break;
            yield return new WaitForSecondsRealtime(1);
            var view=FindFirstObjectByType<MainMenuView>();
            if(!Check(view!=null,"Main menu view present"))yield break;
            yield return Capture("main-menu.png");
            foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1024,768)})
            {
                Screen.SetResolution(size.x,size.y,FullScreenMode.Windowed);yield return new WaitForSecondsRealtime(.5f);
                if(!Check(Screen.width==size.x&&Screen.height==size.y,"Requested output "+size))yield break;
                Canvas.ForceUpdateCanvases();
                foreach(var button in new[]{view.newGameButton,view.controlsButton,view.quitButton})
                {
                    var corners=new Vector3[4];button.GetComponent<RectTransform>().GetWorldCorners(corners);
                    foreach(var corner in corners)
                    {
                        Vector2 screen=RectTransformUtility.WorldToScreenPoint(null,corner);
                        if(!Check(screen.x>=0&&screen.y>=0&&screen.x<=Screen.width&&screen.y<=Screen.height,"Visible button at "+size))yield break;
                    }
                }
                yield return Capture("main-menu-"+size.x+"x"+size.y+".png");
            }
            Screen.SetResolution(1280,720,FullScreenMode.Windowed);yield return new WaitForSecondsRealtime(.5f);
            view.controlsButton.onClick.Invoke();yield return new WaitForSecondsRealtime(.2f);
            if(!Check(view.ControlsOpen,"Controls button opens view"))yield break;
            yield return Capture("controls.png");view.backButton.onClick.Invoke();
            string destination=view.launcher.gameplayScene;
            view.newGameButton.onClick.Invoke();yield return WaitForScene(destination);
            var bank=RunDashBank.Instance;
            if(!Check(bank!=null&&bank.RemainingCharges==bank.StartingDashCount,"First run initializes bank"))yield break;
            bank.TrySpendDash();int spent=bank.RemainingCharges;
            SceneManager.LoadSceneAsync(menuScene);yield return WaitForScene(menuScene);
            view=FindFirstObjectByType<MainMenuView>();
            if(!Check(RunDashBank.Instance==bank&&bank.RemainingCharges==spent,"Menu alone does not reset bank"))yield break;
            view.newGameButton.onClick.Invoke();yield return WaitForScene(destination);
            if(!Check(RunDashBank.Instance==bank&&bank.RemainingCharges==bank.StartingDashCount,"New Game resets existing bank"))yield break;
            SceneManager.LoadSceneAsync(menuScene);yield return WaitForScene(menuScene);
            view=FindFirstObjectByType<MainMenuView>();
            File.WriteAllText(Path.Combine(output,"quit-requested.txt"),"All menu flow checks passed; invoking the standalone Quit button.");
            view.quitButton.onClick.Invoke();
        }
        private IEnumerator WaitForScene(string path)
        {
            double deadline=Time.realtimeSinceStartupAsDouble+20;
            while(SceneManager.GetActiveScene().path!=path&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Check(SceneManager.GetActiveScene().path==path,"Loaded "+path);yield return null;
        }
        private IEnumerator Capture(string name)
        {
            string file=Path.Combine(output,name);ScreenCapture.CaptureScreenshot(file);
            double deadline=Time.realtimeSinceStartupAsDouble+10;
            while(!File.Exists(file)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Check(File.Exists(file),"Captured "+name);yield return new WaitForSecondsRealtime(.1f);
        }
        private bool Check(bool condition,string message)
        {
            File.AppendAllText(Path.Combine(output,"checks.txt"),(condition?"PASS ":"FAIL ")+message+"\n");
            if(!condition)Application.Quit(1);return condition;
        }
    }
}
#endif
