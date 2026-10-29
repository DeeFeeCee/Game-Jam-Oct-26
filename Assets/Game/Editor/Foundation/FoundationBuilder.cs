using System;
using System.IO;
using System.Linq;
using GameJam.Levels;
using GameJam.Movement;
using GameJam.Presentation;
using GameJam.Resources;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace GameJam.Foundation.Editor
{
    public static class FoundationBuilder
    {
        public const string Level = "Assets/Game/Scenes/Levels/Level01.unity";
        public const string Destination = "Assets/Game/Scenes/VerificationDestination.unity";
        public const string Player = "Assets/Game/Prefabs/Player.prefab";
        private const string Fixtures = "Assets/Game/Tests/Fixtures";

        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            Folder(Path.GetDirectoryName(path).Replace('\\', '/'));
            AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
        }
        private static void Move(string from, string to)
        {
            if (!AssetDatabase.IsValidFolder(from)) return;
            Folder(Path.GetDirectoryName(to).Replace('\\', '/'));
            string error = AssetDatabase.MoveAsset(from, to);
            if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
        }
        public static void Migrate()
        {
            Move("Assets/Prototypes/Pass1A/Runtime", "Assets/Game/Runtime/Movement");
            Move("Assets/Prototypes/Pass1C/Runtime", "Assets/Game/Runtime/Resources");
            Move("Assets/Prototypes/Pass1A/Tests", "Assets/Game/Tests/Movement");
            Move("Assets/Prototypes/Pass1B/Tests", "Assets/Game/Tests/CardinalDash");
            Move("Assets/Prototypes/Pass1C/Tests", "Assets/Game/Tests/Resources");
            Move("Assets/Prototypes/Pass1C/Editor", "Assets/Game/Editor/ResourceVerification");
            // Retain every accepted prototype as a regression fixture, with its original GUIDs.
            Move("Assets/Prototypes/Pass1A", Fixtures + "/Movement");
            Move("Assets/Prototypes/Pass1B", Fixtures + "/CardinalDash");
            Move("Assets/Prototypes/Pass1C", Fixtures + "/Resources");
            foreach (string file in Directory.GetFiles("Assets/Game", "*.cs", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles("Assets/Game", "*.md", SearchOption.AllDirectories)))
            {
                if (file.EndsWith("FoundationBuilder.cs")) continue;
                string text = File.ReadAllText(file);
                string updated = text.Replace("Assets/Prototypes/Pass1A", Fixtures + "/Movement")
                    .Replace("Assets/Prototypes/Pass1B", Fixtures + "/CardinalDash")
                    .Replace("Assets/Prototypes/Pass1C", Fixtures + "/Resources");
                if (updated != text) File.WriteAllText(file, updated);
            }
            AssetDatabase.Refresh();
        }

        private static bool DetectPipeline()
        {
            var pipeline = GraphicsSettings.currentRenderPipeline ?? GraphicsSettings.defaultRenderPipeline;
            return pipeline != null && pipeline.GetType().FullName.Contains("Universal");
        }
        public static void BuildContent()
        {
            if (File.Exists(Level) || File.Exists(Destination) || File.Exists(Player))
                throw new InvalidOperationException("Production content already exists. Edit it in Unity; this generator will not overwrite it.");
            if (!DetectPipeline()) throw new InvalidOperationException("This foundation expects the existing URP renderer.");
            Folder("Assets/Game/Prefabs"); Folder("Assets/Game/Scenes/Levels");
            // Tiled SpriteRenderer requires a full rectangular mesh. Pixels, PPU and GUIDs stay intact.
            foreach (string path in new[] { "Assets/Art/BlueBG.png", "Assets/Art/BlueLvlGround-newt.png" })
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                var textureSettings = new TextureImporterSettings();
                importer.ReadTextureSettings(textureSettings);
                textureSettings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(textureSettings);
                importer.SaveAndReimport();
            }
            Scene working = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var banked = AssetDatabase.LoadAssetAtPath<GameObject>(Fixtures + "/Resources/Prefabs/BankedPlayer.prefab");
            GameObject actor = (GameObject)PrefabUtility.InstantiatePrefab(banked);
            PrefabUtility.UnpackPrefabInstance(actor, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            actor.name = "Player";
            var oldRenderer = actor.GetComponentInChildren<SpriteRenderer>();
            Material material = oldRenderer.sharedMaterial;
            if (oldRenderer.gameObject == actor) Object.DestroyImmediate(oldRenderer);
            else Object.DestroyImmediate(oldRenderer.gameObject);
            GameObject visuals = new GameObject("Visuals", typeof(SpriteRenderer), typeof(Animator));
            visuals.transform.SetParent(actor.transform, false);
            visuals.transform.localScale = Vector3.one * 5f; // 100 PPU art -> 20 effective PPU; ink height ~1.5 units.
            var renderer = visuals.GetComponent<SpriteRenderer>();
            renderer.sharedMaterial = material; renderer.sortingOrder = 10;
            renderer.sprite = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Idle_anim2.png").OfType<Sprite>().First(s => s.name == "Idle_1");
            var animator = visuals.GetComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Art/Animations/Player_anim.controller");
            animator.applyRootMotion = false;
            visuals.AddComponent<PlayerPresentation>();
            PrefabUtility.SaveAsPrefabAsset(actor, Player);
            Object.DestroyImmediate(actor);
            GameObject bank = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Fixtures + "/Resources/Prefabs/RunDashBank.prefab"));
            PrefabUtility.UnpackPrefabInstance(bank, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            PrefabUtility.SaveAsPrefabAsset(bank, "Assets/Game/Prefabs/RunDashBank.prefab"); Object.DestroyImmediate(bank);
            var platform = Platform("Industrial platform", Vector2.zero, new Vector2(3.2f, 1f), material);
            PrefabUtility.SaveAsPrefabAsset(platform, "Assets/Game/Prefabs/IndustrialPlatform.prefab"); Object.DestroyImmediate(platform);
            SaveLevel(working, Level, false, material);
            SaveLevel(EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single), Destination, true, material);
            var existing = EditorBuildSettings.scenes.Where(s => s.path != Level && s.path != Destination).ToList();
            existing.Add(new EditorBuildSettingsScene(Level, true)); existing.Add(new EditorBuildSettingsScene(Destination, true));
            EditorBuildSettings.scenes = existing.ToArray();
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(Level);
        }
        private static GameObject Platform(string name, Vector2 position, Vector2 size, Material material)
        {
            GameObject root = new GameObject(name, typeof(BoxCollider2D));
            root.transform.position = position; root.GetComponent<BoxCollider2D>().size = size;
            GameObject art = new GameObject("Floor visuals", typeof(SpriteRenderer)); art.transform.SetParent(root.transform, false);
            var sr = art.GetComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/BlueLvlGround-newt.png");
            sr.sharedMaterial = material; sr.sortingOrder = -5;
            sr.drawMode = SpriteDrawMode.Tiled; sr.size = size;
            return root;
        }
        private static void SaveLevel(Scene scene, string path, bool verification, Material material)
        {
            GameObject root = new GameObject(verification ? "Verification destination" : "Level 01");
            var setup = root.AddComponent<LevelSetup>();
            setup.playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Player);
            setup.bankPrefab = AssetDatabase.LoadAssetAtPath<RunDashBank>("Assets/Game/Prefabs/RunDashBank.prefab");
            GameObject spawn = new GameObject("Player spawn"); spawn.transform.SetParent(root.transform);
            spawn.transform.position = new Vector3(-7f, -3.2f, 0f); setup.spawn = spawn.transform;
            var transition = root.AddComponent<LevelTransition>(); transition.destinationScene = verification ? "" : Destination;
            root.AddComponent<SceneDashSettings>();
            if (verification)
            {
                var policy = new SerializedObject(root.GetComponent<SceneDashSettings>());
                policy.FindProperty("allowAirDashChaining").boolValue = true; policy.ApplyModifiedPropertiesWithoutUndo();
            }
            GameObject cameraRoot = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(LevelCamera2D));
            cameraRoot.tag = "MainCamera"; cameraRoot.transform.position = new Vector3(-3f, 0f, -10f);
            Camera camera = cameraRoot.GetComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 4.5f;
            camera.allowHDR = camera.allowMSAA = camera.allowDynamicResolution = false;
            camera.backgroundColor = new Color(0.15f, 0.2f, 0.28f);
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            var pp = cameraRoot.AddComponent<UnityEngine.Rendering.Universal.PixelPerfectCamera>();
            pp.assetsPPU = 20; pp.refResolutionX = 320; pp.refResolutionY = 180;
            pp.gridSnapping = UnityEngine.Rendering.Universal.PixelPerfectCamera.GridSnapping.UpscaleRenderTexture;
            pp.cropFrame = UnityEngine.Rendering.Universal.PixelPerfectCamera.CropFrame.Windowbox;
            setup.levelCamera = cameraRoot.GetComponent<LevelCamera2D>();
            GameObject bg = new GameObject("Blue industrial background", typeof(SpriteRenderer));
            bg.transform.position = new Vector3(9f, 0.57f, 0f);
            var background = bg.GetComponent<SpriteRenderer>(); background.sharedMaterial = material;
            background.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/BlueBG.png");
            background.sortingOrder = -20; background.drawMode = SpriteDrawMode.Tiled; background.size = new Vector2(40f, 11f);
            Platform("Floor", new Vector2(9f, -4.5f), new Vector2(40f, 1f), material);
            Platform("Left boundary", new Vector2(-11.5f, 0f), new Vector2(1f, 10f), material);
            Platform("Right boundary", new Vector2(29.5f, 0f), new Vector2(1f, 10f), material);
            if (!verification)
            {
                Platform("Practice step", new Vector2(1f, -3.15f), new Vector2(3.2f, 0.5f), material);
                Platform("Dash barrier", new Vector2(10f, -2.9f), new Vector2(0.8f, 2.2f), material);
                GameObject exit = new GameObject("Exit to verification", typeof(BoxCollider2D), typeof(LevelExit), typeof(SpriteRenderer));
                exit.transform.position = new Vector3(25f, -2.8f, 0f);
                exit.GetComponent<BoxCollider2D>().size = new Vector2(1f, 2.4f);
                exit.GetComponent<BoxCollider2D>().isTrigger = true;
                exit.GetComponent<LevelExit>().transition = transition;
                var sr = exit.GetComponent<SpriteRenderer>(); sr.sharedMaterial = material;
                sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Fixtures + "/Movement/Art/PlaceholderSquare.png");
                sr.color = new Color(0.3f, 1f, 0.75f, 0.6f); sr.sortingOrder = 15; sr.drawMode = SpriteDrawMode.Sliced; sr.size = new Vector2(1f, 2.4f);
            }
            EditorSceneManager.SaveScene(scene, path);
        }
        public static void DevelopmentBuild()
        {
            Directory.CreateDirectory("Builds/Pass1D");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { Level, Destination }, locationPathName = "Builds/Pass1D/GameJam.exe",
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
            });
            File.WriteAllText("Logs/Pass1D/build-summary.txt", report.summary.result + " errors=" + report.summary.totalErrors);
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Development build failed.");
        }
        public static void RepairPreviewReference()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Art tests.unity");
            var animator = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Animator>()).Single();
            var renderer = animator.GetComponent<SpriteRenderer>();
            if (renderer.sprite == null)
            {
                renderer.sprite = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Idle_anim2.png")
                    .OfType<Sprite>().Single(s => s.name == "Idle_1");
                EditorSceneManager.SaveScene(scene);
            }
            EditorSceneManager.OpenScene(Level);
        }
    }
}
