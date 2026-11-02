using System;
using System.IO;
using System.Linq;
using GameJam.Levels;
using GameJam.Movement;
using GameJam.Resources;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace GameJam.LevelAuthoring.Editor
{
    /// <summary>One-time, guarded integration entry point. Designers use Create 32x18 Level afterward.</summary>
    public static class ProductionRoomBuilder
    {
        private const string Destination = "Assets/Game/Scenes/VerificationDestination.unity";
        public static void Integrate()
        {
            var pipeline = GraphicsSettings.currentRenderPipeline ?? GraphicsSettings.defaultRenderPipeline;
            if (pipeline == null || !pipeline.GetType().FullName.Contains("Universal"))
                throw new InvalidOperationException("The accepted URP pipeline is required; no pipeline is changed by this tool.");
            if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Stop Play Mode and save scene edits before integration.");
            if (File.Exists(LevelAuthoring32x18.Template) || File.Exists(LevelAuthoring32x18.Platform) || File.Exists(LevelAuthoring32x18.Legacy))
                throw new InvalidOperationException("Integration content already exists. This command will not regenerate accepted work.");
            if (AssetDatabase.AssetPathToGUID(LevelAuthoring32x18.Level01) != "54cd109dfa8beb54088109b65f2d16b5")
                throw new InvalidOperationException("Unexpected Level01 identity. Review the baseline before replacing its layout.");
            Folder("Assets/Game/Tests/Fixtures/Production32x18");
            if (!AssetDatabase.CopyAsset(LevelAuthoring32x18.Level01, LevelAuthoring32x18.Legacy))
                throw new IOException("Could not archive the original scrolling route. Production Level01 was not changed.");
            var working = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var platform = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/IndustrialPlatform.prefab"));
            PrefabUtility.UnpackPrefabInstance(platform, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            platform.name = "GridPlatform32x18";
            LevelAuthoring32x18.SetGeometry(platform, Vector2Int.zero, new Vector2Int(4, 1));
            PrefabUtility.SaveAsPrefabAsset(platform, LevelAuthoring32x18.Platform);
            Object.DestroyImmediate(platform);
            CreateRoom(working, true);
            EditorSceneManager.SaveScene(working, LevelAuthoring32x18.Level01);
            var template = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateRoom(template, false);
            EditorSceneManager.SaveScene(template, LevelAuthoring32x18.Template);
            AssetDatabase.SaveAssets();
            LevelAuthoring32x18.CreateFromTemplate("LevelExample32x18", false);
            EditorSceneManager.OpenScene(LevelAuthoring32x18.Level01);
            File.WriteAllText("Logs/Production32x18/integration.txt", "Level01 GUID preserved; old layout archived; canonical template and independent empty review example created. Build scene registration unchanged.");
        }

        private static void CreateRoom(Scene scene, bool demonstration)
        {
            var root = new GameObject("Level framework - 32 x 18", typeof(LevelSetup), typeof(LevelTransition), typeof(SceneDashSettings), typeof(RoomGrid32x18));
            var setup = root.GetComponent<LevelSetup>();
            setup.playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/Player.prefab");
            setup.bankPrefab = AssetDatabase.LoadAssetAtPath<RunDashBank>("Assets/Game/Prefabs/RunDashBank.prefab");
            var spawn = new GameObject("Player spawn"); spawn.transform.SetParent(root.transform, false);
            spawn.transform.position = new Vector3(3.5f, 1.81f);
            setup.spawn = spawn.transform;
            // The existing spawner supports a null follow camera. No second camera owner is needed.
            setup.levelCamera = null;
            root.GetComponent<LevelTransition>().destinationScene = demonstration ? Destination : "";
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(16, 9, -10);
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 9;
            camera.allowHDR = camera.allowMSAA = camera.allowDynamicResolution = false;
            camera.backgroundColor = Color.black;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            var pixelPerfect = cameraObject.AddComponent<PixelPerfectCamera>();
            pixelPerfect.assetsPPU = 20; pixelPerfect.refResolutionX = 640; pixelPerfect.refResolutionY = 360;
            pixelPerfect.gridSnapping = PixelPerfectCamera.GridSnapping.UpscaleRenderTexture;
            pixelPerfect.cropFrame = PixelPerfectCamera.CropFrame.Windowbox;

            var geometry = new GameObject("Geometry - static whole-cell platforms").transform;
            Solid("Floor", 0, 0, 32, 1); Solid("Ceiling", 0, 17, 32, 1);
            Solid("Left boundary", 0, 1, 1, 16); Solid("Right boundary", 31, 1, 1, 16);
            if (demonstration)
            {
                Solid("Step one", 6, 1, 4, 1); Solid("Step two", 11, 2, 4, 1);
                Solid("Dash launch ledge", 16, 3, 4, 1); Solid("Mandatory dash barrier", 21, 1, 1, 5);
                Solid("Landing ledge", 22, 4, 4, 1);
            }
            var decoration = new GameObject("Background and decoration").transform;
            var background = new GameObject("Blue industrial background", typeof(SpriteRenderer));
            background.transform.SetParent(decoration, false); background.transform.position = new Vector3(16, 9);
            var renderer = background.GetComponent<SpriteRenderer>();
            var material = AssetDatabase.LoadAssetAtPath<GameObject>(LevelAuthoring32x18.Platform).GetComponentInChildren<SpriteRenderer>().sharedMaterial;
            renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/BlueBG.png");
            renderer.sharedMaterial = material; renderer.sortingOrder = -20;
            renderer.drawMode = SpriteDrawMode.Tiled; renderer.size = new Vector2(32, 18);
            var exit = new GameObject("Exit - assign destination on LevelTransition", typeof(BoxCollider2D), typeof(LevelExit), typeof(SpriteRenderer));
            exit.transform.SetParent(root.transform, false); exit.transform.position = new Vector3(29, 2);
            exit.GetComponent<BoxCollider2D>().size = new Vector2(2, 2); exit.GetComponent<BoxCollider2D>().isTrigger = true;
            exit.GetComponent<LevelExit>().transition = root.GetComponent<LevelTransition>();
            renderer = exit.GetComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Tests/Fixtures/Movement/Art/PlaceholderSquare.png");
            renderer.sharedMaterial = material; renderer.color = new Color(.3f, 1, .75f, .6f); renderer.sortingOrder = 15;
            renderer.drawMode = SpriteDrawMode.Sliced; renderer.size = new Vector2(2, 2);

            void Solid(string name, int x, int y, int width, int height)
            {
                var solid = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(LevelAuthoring32x18.Platform), scene);
                solid.transform.SetParent(geometry, false); solid.name = name;
                LevelAuthoring32x18.SetGeometry(solid, new Vector2Int(x, y), new Vector2Int(width, height));
            }
        }

        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            Folder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        public static void BuildWindows()
        {
            Directory.CreateDirectory("Builds/Production32x18");
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = "Builds/Production32x18/GameJam-Production32x18.exe",
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
            });
            File.WriteAllText("Logs/Production32x18/build-summary.txt", result.summary.result + " errors=" + result.summary.totalErrors);
            if (result.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Windows development build failed.");
        }
    }
}
