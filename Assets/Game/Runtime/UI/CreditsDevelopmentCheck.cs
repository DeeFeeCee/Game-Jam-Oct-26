#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using GameJam.Resources;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace GameJam.UI
{
    /// <summary>Explicit programmatic standalone evidence, absent from ordinary play.</summary>
    public sealed class CreditsDevelopmentCheck : MonoBehaviour
    {
        private string output;
        private Keyboard keyboard;
        private Mouse mouse;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void LaunchIfRequested()
        {
            var args=Environment.GetCommandLineArgs();
            for(int i=0;i<args.Length-1;i++)if(args[i]=="-credits-check")
            {var owner=new GameObject("Explicit Credits development verification");DontDestroyOnLoad(owner);owner.AddComponent<CreditsDevelopmentCheck>().output=Path.GetFullPath(args[i+1]);}
        }
        private IEnumerator Start()
        {
            Directory.CreateDirectory(output);yield return new WaitForSecondsRealtime(.5f);
            var view=FindFirstObjectByType<MainMenuView>();if(!Check(view!=null&&view.creditsButton.IsActive(),"Menu startup with Credits button"))yield break;
            keyboard=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();
            yield return Capture("main-menu-credits.png");yield return Press(Key.DownArrow);yield return Press(Key.DownArrow);yield return Press(Key.Enter);
            if(!Check(view.CreditsOpen,"Keyboard opens Credits"))yield break;
            yield return Capture("credits-top.png");var credits=view.credits;
            yield return Wheel(credits);
            if(!Check(credits.archive.verticalNormalizedPosition<.999f&&credits.archive.verticalNormalizedPosition>.01f,"One normalized mouse-wheel tick scrolls partway through archive"))yield break;
            yield return Capture("credits-scrolled.png");
            for(int i=0;i<16&&credits.archive.verticalNormalizedPosition>.001f;i++)yield return Press(Key.DownArrow);
            if(!Check(credits.archive.verticalNormalizedPosition<.001f,"Keyboard reaches archive bottom"))yield break;
            yield return Capture("credits-bottom.png");yield return Press(Key.RightArrow);yield return Press(Key.Enter);
            if(!Check(!view.CreditsOpen&&UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject==view.creditsButton.gameObject,"Fixed Return works and restores Credits focus"))yield break;
            view.controlsButton.onClick.Invoke();if(!Check(view.ControlsOpen,"Controls still opens"))yield break;view.backButton.onClick.Invoke();
            foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(2560,1440),new Vector2Int(1024,768)})
            {
                Screen.SetResolution(size.x,size.y,size.x==2560?FullScreenMode.FullScreenWindow:FullScreenMode.Windowed);yield return new WaitForSecondsRealtime(.6f);
                if(!Check(Screen.width==size.x&&Screen.height==size.y,"Actual output "+size))yield break;
                view.creditsButton.onClick.Invoke();Canvas.ForceUpdateCanvases();
                if(!Check(Mathf.Abs(credits.archive.verticalNormalizedPosition-1)<.001f,"Reopen starts at top"))yield break;
                foreach(var text in credits.archive.content.GetComponentsInChildren<TMPro.TMP_Text>())
                {text.ForceMeshUpdate();if(!Check(!text.isTextOverflowing,"Text fits: "+text.name))yield break;}
                var points=new Vector3[4];credits.returnButton.GetComponent<RectTransform>().GetWorldCorners(points);
                foreach(var p in points)if(!Check(p.x>=0&&p.y>=0&&p.x<=Screen.width&&p.y<=Screen.height,"Fixed Return on screen"))yield break;
                File.AppendAllText(Path.Combine(output,"resolutions.txt"),Screen.width+"x"+Screen.height+" content="+credits.archive.content.rect.height+" viewport="+credits.archive.viewport.rect.height+"\n");
                yield return Capture("credits-"+size.x+"x"+size.y+".png");yield return Press(Key.Escape);
                if(!Check(!view.CreditsOpen&&!view.QuitRequested,"Escape returns without Quit"))yield break;
            }
            Screen.SetResolution(1280,720,FullScreenMode.Windowed);yield return new WaitForSecondsRealtime(.4f);
            if(!Check(RunDashBank.Instance==null,"Browsing alone never creates a bank"))yield break;
            string level=view.launcher.gameplayScene;int handle=SceneManager.GetActiveScene().handle;view.newGameButton.onClick.Invoke();yield return WaitForScene(level,handle);
            var bank=RunDashBank.Instance;if(!Check(bank!=null&&bank.RemainingCharges==5,"New Game initializes accepted run"))yield break;
            // Explicit diagnostic setup for the existing-bank menu contract; no gameplay return feature is added.
            bank.TrySpendDash();handle=SceneManager.GetActiveScene().handle;const string menu="Assets/Game/Scenes/MainMenu.unity";SceneManager.LoadSceneAsync(menu);yield return WaitForScene(menu,handle);
            view=FindFirstObjectByType<MainMenuView>();view.creditsButton.onClick.Invoke();yield return Press(Key.DownArrow);yield return Press(Key.Escape);
            if(!Check(bank.RemainingCharges==4&&!bank.IsGameOver,"Credits preserves existing four-charge bank"))yield break;
            handle=SceneManager.GetActiveScene().handle;view.newGameButton.onClick.Invoke();yield return WaitForScene(level,handle);
            if(!Check(RunDashBank.Instance==bank&&bank.RemainingCharges==5,"New Game resets the same bank after Credits"))yield break;
            handle=SceneManager.GetActiveScene().handle;SceneManager.LoadSceneAsync(menu);yield return WaitForScene(menu,handle);
            RemoveDevices();File.WriteAllText(Path.Combine(output,"complete.txt"),"Menu, Credits, actual wheel/keyboard scrolling, Return/Escape, Controls, four resolutions, bank preservation and New Game passed. Invoking accepted Quit.");
            FindFirstObjectByType<MainMenuView>().quitButton.onClick.Invoke();
        }
        private IEnumerator Press(Key key)
        {InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));yield return null;yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;yield return null;}
        private IEnumerator Wheel(CreditsArchiveView c)
        {
            var rect=c.archive.viewport;var p=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
            InputSystem.QueueStateEvent(mouse,new MouseState{position=p});yield return null;
            float tick=InputSystem.settings.scrollDeltaBehavior==InputSettings.ScrollDeltaBehavior.UniformAcrossAllPlatforms?1:120;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=p,scroll=new Vector2(0,-tick)});yield return null;yield return null;
        }
        private IEnumerator WaitForScene(string path,int handle)
        {
            double end=Time.realtimeSinceStartupAsDouble+20;
            while((SceneManager.GetActiveScene().handle==handle||SceneManager.GetActiveScene().path!=path)&&Time.realtimeSinceStartupAsDouble<end)yield return null;
            Check(SceneManager.GetActiveScene().path==path&&SceneManager.GetActiveScene().handle!=handle,"Loaded "+path);yield return null;yield return null;
        }
        private IEnumerator Capture(string name)
        {
            yield return new WaitForSecondsRealtime(.1f);string path=Path.Combine(output,name);ScreenCapture.CaptureScreenshot(path);
            double end=Time.realtimeSinceStartupAsDouble+10;while(!File.Exists(path)&&Time.realtimeSinceStartupAsDouble<end)yield return null;
            Check(File.Exists(path),"Captured "+name);yield return new WaitForSecondsRealtime(.1f);
        }
        private bool Check(bool value,string text)
        {File.AppendAllText(Path.Combine(output,"checks.txt"),(value?"PASS ":"FAIL ")+text+"\n");if(!value)Application.Quit(1);return value;}
        private void RemoveDevices()
        {foreach(var d in new InputDevice[]{keyboard,mouse})if(d!=null&&d.added)InputSystem.RemoveDevice(d);keyboard=null;mouse=null;}
        private void OnDestroy()=>RemoveDevices();
    }
}
#endif
