using System;
using System.IO;
using System.Linq;
using GameJam.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using Object=UnityEngine.Object;

namespace GameJam.UI.Editor
{
    public static class GameOverBuilder
    {
        public const string PrefabPath="Assets/Game/UI/Resources/UI/GameOver.prefab";
        [MenuItem("Game Jam/UI/Create Game Over If Absent")]
        public static void Create()
        {
            if(File.Exists(PrefabPath))throw new InvalidOperationException("Game Over exists; edit the prefab rather than regenerate.");
            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Game/UI/TextResources/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if(font==null)throw new InvalidOperationException("Accepted menu font is required; no additional text resources are imported.");
            var root=new GameObject("GameOver");root.SetActive(false);
            var controller=root.AddComponent<GameOverController>();
            var canvasObject=new GameObject("GameOverOverlay",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));
            canvasObject.transform.SetParent(root.transform,false);controller.overlay=canvasObject;
            var canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=100;canvas.pixelPerfect=true;
            var scaler=canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
            var tint=Panel(canvas.transform,"BackgroundOverlayPlaceholder",new Color(.015f,.035f,.055f,.65f));Fit(tint,0,0,1,1);tint.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
            var panel=Panel(canvas.transform,"FailurePanel",new Color(.035f,.085f,.125f,.97f));Fit(panel,.14f,.18f,.86f,.84f);
            var outline=panel.gameObject.AddComponent<UnityEngine.UI.Outline>();outline.effectColor=new Color(.25f,.52f,.52f);outline.effectDistance=new Vector2(2,-2);
            Text(panel,"Headline","EXPERIMENT\nTERMINATED",40,new Color(.9f,.96f,.96f),.055f,.66f,.68f,.96f).fontStyle=FontStyles.Bold;
            Text(panel,"Reason","MOBILITY ALLOCATION EXHAUSTED",18,new Color(.85f,.48f,.43f),.055f,.54f,.68f,.65f);
            Text(panel,"SecondaryText","Subject failed to meet operational requirements.",18,new Color(.68f,.79f,.8f),.055f,.39f,.68f,.53f);
            var actions=new GameObject("FailureActions",typeof(RectTransform),typeof(UnityEngine.UI.VerticalLayoutGroup));actions.transform.SetParent(panel,false);
            Fit(actions.GetComponent<RectTransform>(),.055f,.065f,.67f,.35f);var layout=actions.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();layout.spacing=12;layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
            controller.restartButton=Button(actions.transform,"RetryLevel","RETRY LEVEL");controller.mainMenuButton=Button(actions.transform,"ReturnToMainMenu","RETURN TO MAIN MENU");
            Navigate(controller.restartButton,controller.restartButton,controller.mainMenuButton);Navigate(controller.mainMenuButton,controller.restartButton,controller.mainMenuButton);
            controller.status=Text(panel,"Status","",12,new Color(.5f,.8f,.78f),.055f,.005f,.70f,.06f);
            var art=Panel(panel,"FailureIllustrationPlaceholder",new Color(.07f,.17f,.21f));Fit(art,.73f,.23f,.95f,.78f);
            var mark=Panel(art,"ResearchStatusGraphicPlaceholder",new Color(.6f,.31f,.30f));Fit(mark,.2f,.25f,.8f,.75f);
            var cut=Panel(mark,"StatusMarkCutout",new Color(.07f,.17f,.21f));Fit(cut,.38f,0,.62f,1);
            Text(panel,"ResearchStatusLabel","STATUS / FAILED",13,new Color(.85f,.48f,.43f),.73f,.8f,.97f,.91f);
            var animation=Panel(art,"OptionalAnimationSlot",Color.clear);Fit(animation,0,0,1,1);

            var es=new GameObject("GameOverEventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));es.transform.SetParent(root.transform,false);
            var module=es.GetComponent<InputSystemUIInputModule>();module.enabled=false;
            controller.uiEventSystem=es.GetComponent<EventSystem>();controller.uiEventSystem.firstSelectedGameObject=controller.restartButton.gameObject;
            controller.inputScope=es.AddComponent<MenuInputScope>();controller.inputScope.inputActions=AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Settings/InputSystem_Actions.inputactions");controller.inputScope.enabled=false;
            es.SetActive(false);canvasObject.SetActive(false);
            foreach(var component in root.GetComponentsInChildren<Component>(true))if(PrefabUtility.IsPartOfPrefabInstance(component))PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            root.SetActive(true);PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);Object.DestroyImmediate(root);AssetDatabase.SaveAssets();

            TextMeshProUGUI Text(Transform parent,string name,string value,int size,Color color,float x0,float y0,float x1,float y1)
            {
                var obj=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));obj.transform.SetParent(parent,false);
                var text=obj.GetComponent<TextMeshProUGUI>();text.font=font;text.text=value;text.fontSize=size;text.color=color;text.alignment=TextAlignmentOptions.MidlineLeft;text.raycastTarget=false;text.textWrappingMode=TextWrappingModes.Normal;Fit(obj.GetComponent<RectTransform>(),x0,y0,x1,y1);return text;
            }
        }
        private static RectTransform Panel(Transform parent,string name,Color color)
        {
            var obj=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image));obj.transform.SetParent(parent,false);var image=obj.GetComponent<UnityEngine.UI.Image>();image.color=color;image.raycastTarget=false;return obj.GetComponent<RectTransform>();
        }
        private static void Fit(RectTransform rect,float x0,float y0,float x1,float y1)
        {rect.anchorMin=new Vector2(x0,y0);rect.anchorMax=new Vector2(x1,y1);rect.offsetMin=rect.offsetMax=Vector2.zero;}
        private static UnityEngine.UI.Button Button(Transform parent,string name,string label)
        {
            var obj=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(MenuBuilder.ButtonPath),parent);obj.name=name;obj.GetComponentInChildren<TMP_Text>().text=label;return obj.GetComponent<UnityEngine.UI.Button>();
        }
        private static void Navigate(UnityEngine.UI.Button button,UnityEngine.UI.Button up,UnityEngine.UI.Button down)
        {button.navigation=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.Explicit,selectOnUp=up,selectOnDown=down};}
        [MenuItem("Game Jam/UI/Build Game Over Windows Development Player")]
        public static void Build()
        {
            Directory.CreateDirectory("Builds/GameOver");Directory.CreateDirectory("Logs/GameOver");
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),locationPathName="Builds/GameOver/GameJam-GameOver.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            File.WriteAllText("Logs/GameOver/build-summary.txt",result.summary.result+" errors="+result.summary.totalErrors);
            if(result.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Game Over development build failed.");
        }
    }
}
