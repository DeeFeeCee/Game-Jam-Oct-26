using System;
using System.IO;
using System.Linq;
using GameJam.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

namespace GameJam.UI.Editor
{
    public static class MenuBuilder
    {
        public const string ScenePath="Assets/Game/Scenes/MainMenu.unity";
        public const string PrefabPath="Assets/Game/UI/Prefabs/MainMenu.prefab";
        public const string ButtonPath="Assets/Game/UI/Prefabs/MenuButton.prefab";
        internal const string ProductionMenuFontPath = "Assets/Game/UI/Fonts/DeeFont1.17 SDF.asset";
        private static TMP_FontAsset font;
        private static bool batchRequest;
        private static readonly Color Background=new Color(.025f,.055f,.095f);
        private static readonly Color Teal=new Color(.21f,.72f,.68f);
        private static readonly Color Ink=new Color(.87f,.94f,.94f);

        public static void CreateBatch() { batchRequest=true;Create(); }
        private static void Imported(string packageName)
        {
            AssetDatabase.importPackageCompleted-=Imported;AssetDatabase.importPackageFailed-=ImportFailed;
            EditorApplication.delayCall+=()=>
            {
                try {Create();} catch(Exception exception) {Debug.LogException(exception);if(batchRequest)EditorApplication.Exit(1);}
            };
        }
        private static void ImportFailed(string packageName,string error)
        {
            AssetDatabase.importPackageCompleted-=Imported;AssetDatabase.importPackageFailed-=ImportFailed;
            Debug.LogError("Text essentials import failed: "+error);if(batchRequest)EditorApplication.Exit(1);
        }

        [MenuItem("Game Jam/UI/Create Main Menu If Absent")]
        public static void Create()
        {
            if(File.Exists(ScenePath)||File.Exists(PrefabPath)) throw new InvalidOperationException("Menu exists; edit it rather than regenerate.");
            const string textRoot="Assets/Game/UI/TextResources";
            if(!AssetDatabase.IsValidFolder(textRoot))
            {
                if(!AssetDatabase.IsValidFolder("Assets/TextMesh Pro"))
                {
                    AssetDatabase.importPackageCompleted+=Imported;AssetDatabase.importPackageFailed+=ImportFailed;
                    TMP_PackageResourceImporter.ImportResources(true,false,false);return;
                }
                AssetDatabase.Refresh();
                if(!AssetDatabase.IsValidFolder("Assets/TextMesh Pro")) throw new InvalidOperationException("Text essentials import pending; retry after import completes.");
                string error=AssetDatabase.MoveAsset("Assets/TextMesh Pro",textRoot);
                if(!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
            }
            font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(textRoot+"/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if(font==null) throw new InvalidOperationException("TMP font resource not ready.");
            var menuFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ProductionMenuFontPath);
            if (menuFont == null) throw new InvalidOperationException("DeeFont is required for the production Main Menu.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var template=Button(null,"MenuButton","BUTTON");
            PrefabUtility.SaveAsPrefabAsset(template.gameObject,ButtonPath);Object.DestroyImmediate(template.gameObject);
            font = menuFont;

            var root=new GameObject("MainMenu");root.SetActive(false);
            var launcher=root.AddComponent<NewGameLauncher>();var view=root.AddComponent<MainMenuView>();view.launcher=launcher;
            launcher.gameplayScene=NarrativePrologue.ScenePath;
            launcher.initialBankPrefab=AssetDatabase.LoadAssetAtPath<GameJam.Resources.RunDashBank>("Assets/Game/Prefabs/RunDashBank.prefab");
            var es=new GameObject("MenuEventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));es.transform.SetParent(root.transform);
            var module=es.GetComponent<InputSystemUIInputModule>();module.enabled=false;
            var scope=es.AddComponent<MenuInputScope>();scope.inputActions=AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Settings/InputSystem_Actions.inputactions");view.inputScope=scope;
            var canvasObject=new GameObject("MenuCanvas",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));
            canvasObject.transform.SetParent(root.transform,false);
            var canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.pixelPerfect=true;
            var scaler=canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1280,720);scaler.screenMatchMode=UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;scaler.matchWidthOrHeight=.5f;
            var background=Panel(canvas.transform,"BackgroundPlaceholder",Background);Stretch(background);
            var top=Panel(canvas.transform,"TopRule",Teal);Anchors(top,0,1,1,1);top.pivot=new Vector2(.5f,1);top.sizeDelta=new Vector2(0,3);
            var bottom=Panel(canvas.transform,"BottomRule",new Color(.08f,.22f,.28f));Anchors(bottom,0,0,1,0);bottom.pivot=new Vector2(.5f,0);bottom.sizeDelta=new Vector2(0,2);
            var header=Text(canvas.transform,"DivisionLabel","P.I.D.R.  /  RESEARCH SYSTEMS",16,Teal);Anchors(header.rectTransform,.075f,.9f,.92f,.97f);
            var footer=Text(canvas.transform,"NavigationHint","MOUSE  /  ARROWS + ENTER  /  GAMEPAD",14,new Color(.42f,.59f,.64f));Anchors(footer.rectTransform,.075f,.03f,.92f,.09f);

            var main=Panel(canvas.transform,"MainView",Color.clear);Stretch(main);view.mainPanel=main.gameObject;
            var title=Text(main,"WorkingTitle","PROJECT:\nP.I.D.R.",56,Ink);Anchors(title.rectTransform,.075f,.60f,.53f,.87f);title.fontStyle=FontStyles.Bold;
            var subtitle=Text(main,"Subtitle","HUMAN POTENTIAL. MEASURED.",18,Teal);Anchors(subtitle.rectTransform,.078f,.52f,.53f,.59f);
            var actions=new GameObject("PrimaryActions",typeof(RectTransform),typeof(UnityEngine.UI.VerticalLayoutGroup));actions.transform.SetParent(main,false);
            Anchors(actions.GetComponent<RectTransform>(),.075f,.19f,.45f,.48f);
            var layout=actions.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();layout.spacing=12;layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
            view.newGameButton=InstanceButton(actions.transform,"NewGame","NEW GAME");view.controlsButton=InstanceButton(actions.transform,"Controls","CONTROLS");view.quitButton=InstanceButton(actions.transform,"Quit","QUIT");
            Link(view.newGameButton,view.newGameButton,view.controlsButton);Link(view.controlsButton,view.newGameButton,view.quitButton);Link(view.quitButton,view.controlsButton,view.quitButton);
            var status=Text(main,"Status","",16,Ink);Anchors(status.rectTransform,.075f,.105f,.54f,.175f);view.status=status;

            var art=Panel(main,"IllustrationPanelPlaceholder",new Color(.045f,.105f,.15f));Anchors(art,.59f,.20f,.925f,.84f);
            var border=art.gameObject.AddComponent<UnityEngine.UI.Outline>();border.effectColor=new Color(.14f,.37f,.40f);border.effectDistance=new Vector2(2,-2);
            var artSlot=Panel(art,"CharacterIllustrationSlot",new Color(.1f,.23f,.28f));Anchors(artSlot,.25f,.23f,.75f,.76f);
            var logo=Panel(art,"LogoPlaceholder",Teal);Anchors(logo,.39f,.39f,.61f,.61f);
            var cross=Panel(logo,"MarkCutout",new Color(.1f,.23f,.28f));Anchors(cross,.4f,0,.6f,1);
            var crossHorizontal=Panel(logo,"MarkCutoutHorizontal",new Color(.1f,.23f,.28f));Anchors(crossHorizontal,0,.4f,1,.6f);
            var artLabel=Text(art,"PanelLabel","RESEARCH DIVISION",18,Ink);Anchors(artLabel.rectTransform,.07f,.83f,.94f,.95f);
            var artFoot=Text(art,"PanelFooter","ADVANCEMENT THROUGH DISCOVERY",12,Teal);Anchors(artFoot.rectTransform,.07f,.05f,.95f,.15f);

            var controls=Panel(canvas.transform,"ControlsView",new Color(.035f,.085f,.125f));Anchors(controls,.075f,.15f,.925f,.86f);view.controlsPanel=controls.gameObject;
            var controlsTitle=Text(controls,"Heading","CONTROLS",36,Ink);controlsTitle.fontStyle=FontStyles.Bold;Anchors(controlsTitle.rectTransform,.055f,.78f,.94f,.94f);
            var keyboard=Text(controls,"KeyboardControls","KEYBOARD\n\nA/D or Left/Right    Move\nW or Up    Jump\nSpace    Dash\nWASD / Arrows    Dash direction",22,Ink);Anchors(keyboard.rectTransform,.055f,.23f,.54f,.77f);
            var gamepad=Text(controls,"GamepadControls","GAMEPAD\n\nLeft stick    Move and aim\nSouth button    Jump\nRight shoulder    Dash",22,Ink);Anchors(gamepad.rectTransform,.55f,.23f,.96f,.77f);
            view.backButton=InstanceButton(controls,"Back","BACK");var backRect=view.backButton.GetComponent<RectTransform>();Anchors(backRect,.055f,.05f,.37f,.18f);Link(view.backButton,view.backButton,view.backButton);
            var backHint=Text(controls,"BackHint","ESCAPE / GAMEPAD EAST (B): BACK",14,Teal);Anchors(backHint.rectTransform,.42f,.05f,.96f,.18f);
            controls.gameObject.SetActive(false);
            foreach(var component in root.GetComponentsInChildren<Component>(true))
                if(PrefabUtility.IsPartOfPrefabInstance(component))PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            es.GetComponent<EventSystem>().firstSelectedGameObject=view.newGameButton.gameObject;
            PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);Object.DestroyImmediate(root);
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            // Active state is authored into the scene, so the reusable prefab is immediately usable too.
            var prefabRoot=PrefabUtility.LoadPrefabContents(PrefabPath);prefabRoot.SetActive(true);PrefabUtility.SaveAsPrefabAsset(prefabRoot,PrefabPath);PrefabUtility.UnloadPrefabContents(prefabRoot);
            Object.FindFirstObjectByType<MainMenuView>(FindObjectsInactive.Include).gameObject.SetActive(true);
            var cameraRoot=new GameObject("MenuCamera",typeof(Camera),typeof(AudioListener));cameraRoot.tag="MainCamera";
            var camera=cameraRoot.GetComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Background;
            camera.orthographic=true;camera.transform.position=new Vector3(0,0,-10);camera.allowHDR=camera.allowMSAA=camera.allowDynamicResolution=false;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),ScenePath);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)}.Concat(EditorBuildSettings.scenes.Where(s=>s.path!=ScenePath)).ToArray();
            AssetDatabase.SaveAssets();
            if(batchRequest)EditorApplication.Exit(0);
        }
        private static RectTransform Panel(Transform parent,string name,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image));if(parent!=null)go.transform.SetParent(parent,false);
            var image=go.GetComponent<UnityEngine.UI.Image>();image.color=color;image.raycastTarget=false;return go.GetComponent<RectTransform>();
        }
        private static void Stretch(RectTransform rect)=>Anchors(rect,0,0,1,1);
        private static void Anchors(RectTransform rect,float x0,float y0,float x1,float y1)
        {rect.anchorMin=new Vector2(x0,y0);rect.anchorMax=new Vector2(x1,y1);rect.pivot=new Vector2(.5f,.5f);rect.offsetMin=rect.offsetMax=Vector2.zero;}
        private static TextMeshProUGUI Text(Transform parent,string name,string value,int size,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);
            var text=go.GetComponent<TextMeshProUGUI>();text.font=font;text.text=value;text.fontSize=size;text.color=color;
            text.alignment=TextAlignmentOptions.MidlineLeft;text.raycastTarget=false;text.textWrappingMode=TextWrappingModes.Normal;return text;
        }
        private static UnityEngine.UI.Button Button(Transform parent,string name,string label)
        {
            var rect=Panel(parent,name,new Color(.055f,.145f,.205f));rect.sizeDelta=new Vector2(440,58);
            rect.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
            var button=rect.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=rect.GetComponent<UnityEngine.UI.Image>();
            var colors=button.colors;colors.normalColor=new Color(.055f,.145f,.205f);colors.highlightedColor=new Color(.10f,.30f,.35f);colors.selectedColor=new Color(.10f,.38f,.39f);colors.pressedColor=new Color(.18f,.55f,.50f);colors.disabledColor=new Color(.08f,.11f,.13f);colors.fadeDuration=.06f;button.colors=colors;
            rect.GetComponent<UnityEngine.UI.Image>().color=Color.white;
            var element=rect.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();element.minHeight=48;element.preferredHeight=58;
            var text=Text(rect,"Label",label,22,Ink);Anchors(text.rectTransform,.055f,0,.95f,1);return button;
        }
        private static UnityEngine.UI.Button InstanceButton(Transform parent,string name,string label)
        {
            var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ButtonPath),parent);go.name=name;
            var text = go.GetComponentInChildren<TMP_Text>();
            text.text = label;
            text.font = font;
            text.fontSharedMaterial = font.material;
            return go.GetComponent<UnityEngine.UI.Button>();
        }
        private static void Link(UnityEngine.UI.Button button,UnityEngine.UI.Button up,UnityEngine.UI.Button down)
        {var nav=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.Explicit,selectOnUp=up,selectOnDown=down};button.navigation=nav;}
        [MenuItem("Game Jam/UI/Build Menu Windows Development Player")]
        public static void Build()
        {
            Directory.CreateDirectory("Builds/Menu");Directory.CreateDirectory("Logs/Menu");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),locationPathName="Builds/Menu/GameJam-Menu.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            File.WriteAllText("Logs/Menu/build-summary.txt",report.summary.result+" errors="+report.summary.totalErrors);
            if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Menu development build failed.");
        }
    }
}
