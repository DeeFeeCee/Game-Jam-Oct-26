using System;
using System.IO;
using GameJam.Foundation.Editor;
using GameJam.Levels;
using GameJam.Movement;
using GameJam.Resources;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

namespace GameJam.SingleScreen.Editor
{
    public static class RoomBuilder
    {
        public const string Root="Assets/Game/Experiments/SingleScreen";
        public const string Scene=Root+"/Scenes/SingleScreen32x18.unity";
        public const string Platform=Root+"/Prefabs/GridPlatform.prefab";
        [MenuItem("Game Jam/Single Screen Experiment/Create Room If Absent")]
        public static void Create()
        {
            // Detect before configuring URP-specific components. No package/settings changes.
            var pipeline=GraphicsSettings.currentRenderPipeline??GraphicsSettings.defaultRenderPipeline;
            if(pipeline==null||!pipeline.GetType().FullName.Contains("Universal")) throw new InvalidOperationException("Existing URP pipeline required.");
            if(File.Exists(Scene)) throw new InvalidOperationException("Room exists; edit it in Unity rather than regenerate.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=new GameObject("32 x 18 experiment",typeof(RoomGridGuide),typeof(LevelSetup),typeof(LevelTransition),typeof(SceneDashSettings));
            var setup=root.GetComponent<LevelSetup>();
            setup.playerPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(FoundationBuilder.Player);
            setup.bankPrefab=AssetDatabase.LoadAssetAtPath<RunDashBank>("Assets/Game/Prefabs/RunDashBank.prefab");
            var spawn=new GameObject("Spawn - cell center; feet just above y1");spawn.transform.SetParent(root.transform);
            spawn.transform.position=new Vector3(3.5f,1.81f);setup.spawn=spawn.transform;
            // levelCamera intentionally null: this scene uses a stationary camera, no follow component.
            root.GetComponent<LevelTransition>().destinationScene=FoundationBuilder.Destination;
            var cameraObject=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener));
            cameraObject.tag="MainCamera";cameraObject.transform.position=new Vector3(16,9,-10);
            var camera=cameraObject.GetComponent<Camera>();camera.orthographic=true;camera.orthographicSize=9;
            camera.allowHDR=camera.allowMSAA=camera.allowDynamicResolution=false;camera.backgroundColor=Color.black;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            var pp=cameraObject.AddComponent<PixelPerfectCamera>();
            pp.assetsPPU=20;pp.refResolutionX=640;pp.refResolutionY=360;
            pp.gridSnapping=PixelPerfectCamera.GridSnapping.UpscaleRenderTexture;pp.cropFrame=PixelPerfectCamera.CropFrame.Windowbox;
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/IndustrialPlatform.prefab");
            var material=prefab.GetComponentInChildren<SpriteRenderer>().sharedMaterial;
            var prototype=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
            PrefabUtility.UnpackPrefabInstance(prototype,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            prototype.name="Grid platform";SetSize(prototype,new Vector2(4,1));
            PrefabUtility.SaveAsPrefabAsset(prototype,Platform);Object.DestroyImmediate(prototype);
            Solid("Floor",0,0,32,1);Solid("Ceiling",0,17,32,1);
            Solid("Left boundary",0,1,1,16);Solid("Right boundary",31,1,1,16);
            Solid("Step one",6,1,4,1);Solid("Step two",11,2,4,1);Solid("Dash launch ledge",16,3,4,1);
            Solid("Mandatory dash barrier",21,1,1,5);Solid("Landing ledge",22,4,4,1);
            var bg=new GameObject("Ace industrial background",typeof(SpriteRenderer));bg.transform.position=new Vector3(16,9);
            var sr=bg.GetComponent<SpriteRenderer>();sr.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/BlueBG.png");
            sr.sharedMaterial=material;sr.sortingOrder=-20;sr.drawMode=SpriteDrawMode.Tiled;sr.size=new Vector2(32,18);
            var exit=new GameObject("Exit - cells x28..30 y1..3",typeof(BoxCollider2D),typeof(LevelExit),typeof(SpriteRenderer));
            exit.transform.position=new Vector3(29,2);exit.GetComponent<BoxCollider2D>().size=new Vector2(2,2);exit.GetComponent<BoxCollider2D>().isTrigger=true;
            exit.GetComponent<LevelExit>().transition=root.GetComponent<LevelTransition>();
            sr=exit.GetComponent<SpriteRenderer>();sr.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Tests/Fixtures/Movement/Art/PlaceholderSquare.png");
            sr.sharedMaterial=material;sr.color=new Color(.3f,1,.75f,.6f);sr.sortingOrder=15;
            exit.transform.localScale=new Vector3(1,1,1);sr.drawMode=SpriteDrawMode.Sliced;sr.size=new Vector2(2,2);
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),Scene);
            AssetDatabase.SaveAssets();

            void Solid(string name,int x,int y,int width,int height)
            {
                var solid=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Platform));
                solid.name=name;solid.transform.position=new Vector3(x+width/2f,y+height/2f);
                SetSize(solid,new Vector2(width,height));
            }
        }
        private static void SetSize(GameObject solid,Vector2 size)
        {
            solid.GetComponent<BoxCollider2D>().size=size;
            solid.transform.Find("Floor visuals").GetComponent<SpriteRenderer>().size=size;
            SurfaceReadability.Apply(solid.GetComponent<BoxCollider2D>());
            foreach(var component in solid.GetComponentsInChildren<Component>())
                if(component is Transform||component is SpriteRenderer||component is BoxCollider2D)
                    PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        }
        [MenuItem("Game Jam/Single Screen Experiment/Snap Selected Solid Bounds To Cells")]
        public static void Snap()
        {
            foreach(var obj in Selection.gameObjects)
            {
                if(obj.scene.path!=Scene||!obj.TryGetComponent(out BoxCollider2D box)||box.isTrigger||box.attachedRigidbody!=null) continue;
                if(obj.transform.localScale!=Vector3.one||obj.transform.rotation!=Quaternion.identity) continue;
                Undo.RegisterFullObjectHierarchyUndo(obj,"Snap experiment solid to cells");
                Vector2 min=box.bounds.min,max=box.bounds.max;
                min=new Vector2(Mathf.Round(min.x),Mathf.Round(min.y));max=new Vector2(Mathf.Round(max.x),Mathf.Round(max.y));
                if(max.x<=min.x||max.y<=min.y) continue;
                obj.transform.position=(Vector3)((min+max)/2f-box.offset);SetSize(obj,max-min);
                PrefabUtility.RecordPrefabInstancePropertyModifications(box);EditorSceneManager.MarkSceneDirty(obj.scene);
            }
        }
        [MenuItem("Game Jam/Single Screen Experiment/Build Windows Development Player")]
        public static void Build()
        {
            Directory.CreateDirectory("Builds/SingleScreen");Directory.CreateDirectory("Logs/SingleScreen");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Scene,FoundationBuilder.Destination},
                locationPathName="Builds/SingleScreen/GameJam-SingleScreen.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            File.WriteAllText("Logs/SingleScreen/build-summary.txt",report.summary.result+" errors="+report.summary.totalErrors);
            if(report.summary.result!=BuildResult.Succeeded) throw new InvalidOperationException("Experimental build failed.");
        }
    }
}
