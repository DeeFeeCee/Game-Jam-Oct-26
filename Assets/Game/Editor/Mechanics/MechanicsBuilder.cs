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
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace GameJam.Mechanics.Editor
{
    // Creates only this pass's fixtures. Never rebuilds or migrates accepted production content.
    public static class MechanicsBuilder
    {
        public const string ScenePath = "Assets/Game/Scenes/Verification/GameplayMechanicsVerification.unity";
        private const string Prefabs = "Assets/Game/Prefabs/";
        private static Sprite Square => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Tests/Fixtures/Movement/Art/PlaceholderSquare.png");
        private static Material Material => AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Tests/Fixtures/Movement/Art/PlaceholderUnlit.mat");
        private static PhysicsMaterial2D Frictionless => AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>("Assets/Game/Tests/Fixtures/Movement/Physics/PlayerFrictionless.physicsMaterial2D");
        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\','/'); Folder(parent);
            AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
        }
        private static SpriteRenderer Visual(Transform parent, string name, Vector2 size, Color color, Vector2 position = default)
        {
            var obj = new GameObject(name,typeof(SpriteRenderer)); obj.transform.SetParent(parent,false); obj.transform.localPosition = position;
            var sr = obj.GetComponent<SpriteRenderer>(); sr.sprite = Square; sr.sharedMaterial = Material;
            sr.drawMode = SpriteDrawMode.Sliced; sr.size = size; sr.color = color; sr.sortingOrder = 2; return sr;
        }
        private static GameObject Solid(string name, Vector2 center, Vector2 size)
        {
            var obj = new GameObject(name,typeof(BoxCollider2D)); obj.transform.position = center;
            obj.GetComponent<BoxCollider2D>().size = size; obj.GetComponent<BoxCollider2D>().sharedMaterial = Frictionless;
            Visual(obj.transform,"Replaceable surface",size,new Color(.12f,.19f,.28f));
            Visual(obj.transform,"Walkable top",new Vector2(size.x,.05f),new Color(.65f,.9f,1),new Vector2(0,(size.y-.05f)/2));
            return obj;
        }
        private static T Platform<T>(Transform parent, Color color) where T : PhysicsPlatform2D
        {
            var obj = new GameObject("Physics body",typeof(Rigidbody2D),typeof(BoxCollider2D)); obj.transform.SetParent(parent,false);
            obj.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            obj.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeRotation;
            obj.GetComponent<Rigidbody2D>().gravityScale = 0; obj.GetComponent<BoxCollider2D>().sharedMaterial = Frictionless;
            var platform = obj.AddComponent<T>();
            platform.surface = Visual(obj.transform,"Replaceable surface",platform.size,color);
            platform.topEdge = Visual(obj.transform,"Safe top",new Vector2(platform.size.x,.05f),Color.white);
            platform.topEdge.sortingOrder = platform.surface.sortingOrder+1;
            platform.ApplySize(); return platform;
        }
        private static void Save(GameObject root, string name)
        { PrefabUtility.SaveAsPrefabAsset(root,Prefabs+name+".prefab"); Object.DestroyImmediate(root); }
        private static GameObject Instance(string name, Vector2 position)
        {
            var obj = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs+name+".prefab"));
            obj.transform.position = position; return obj;
        }
        public static void CreateContent()
        {
            string[] names = {"FallDeathZone","MovingPlatform","TurretProjectile","Turret","BreakableWall","FallingCrusherPlatform"};
            if (File.Exists(ScenePath) || names.Any(n=>File.Exists(Prefabs+n+".prefab")))
                throw new InvalidOperationException("Mechanics content already exists; edit instances in Unity, do not overwrite GUIDs.");
            Folder("Assets/Game/Scenes/Verification");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root = new GameObject("FallDeathZone",typeof(BoxCollider2D),typeof(FallDeathZone2D));
            root.GetComponent<BoxCollider2D>().isTrigger = true; root.GetComponent<BoxCollider2D>().size = new Vector2(6,2); Save(root,"FallDeathZone");

            root = new GameObject("MovingPlatform");
            var a = new GameObject("Endpoint A").transform; a.SetParent(root.transform,false);
            var b = new GameObject("Endpoint B").transform; b.SetParent(root.transform,false); b.localPosition = new Vector3(6,0);
            var moving = Platform<MovingPlatform2D>(root.transform,new Color(.1f,.65f,.65f)); moving.pointA = a; moving.pointB = b;
            moving.endpointDelay = .25f; Save(root,"MovingPlatform");

            root = new GameObject("TurretProjectile",typeof(Rigidbody2D),typeof(CircleCollider2D),typeof(TurretProjectile2D));
            root.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic; root.GetComponent<Rigidbody2D>().gravityScale = 0;
            root.GetComponent<CircleCollider2D>().isTrigger = true; root.GetComponent<CircleCollider2D>().radius = .12f;
            Visual(root.transform,"Replaceable projectile",new Vector2(.24f,.24f),Color.yellow); Save(root,"TurretProjectile");
            root = new GameObject("Turret",typeof(Turret2D)); var turret = root.GetComponent<Turret2D>();
            turret.projectilePrefab = AssetDatabase.LoadAssetAtPath<TurretProjectile2D>(Prefabs+"TurretProjectile.prefab");
            turret.muzzle = new GameObject("Muzzle").transform; turret.muzzle.SetParent(root.transform,false); turret.muzzle.localPosition = Vector3.left*.6f;
            Visual(root.transform,"Replaceable turret",new Vector2(.8f,.8f),new Color(.9f,.2f,.15f)); Save(root,"Turret");

            root = Solid("BreakableWall",Vector2.zero,new Vector2(1,3)); root.AddComponent<BreakableWall2D>();
            root.GetComponentInChildren<SpriteRenderer>().color = new Color(.85f,.3f,.7f); Save(root,"BreakableWall");

            root = new GameObject("FallingCrusherPlatform");
            var crusher = Platform<FallingCrusherPlatform2D>(root.transform,new Color(.95f,.6f,.1f));
            crusher.size = new Vector2(4,.75f); crusher.ApplySize(); crusher.maximumDescent = 12;
            var zone = new GameObject("Stationary activation region",typeof(BoxCollider2D),typeof(CrusherActivationZone2D));
            zone.transform.SetParent(root.transform,false); zone.transform.localPosition = new Vector3(0,-6);
            zone.GetComponent<BoxCollider2D>().size = new Vector2(4,12); zone.GetComponent<BoxCollider2D>().isTrigger = true;
            zone.GetComponent<CrusherActivationZone2D>().crusher = crusher; Save(root,"FallingCrusherPlatform");

            var setupRoot = new GameObject("Mechanics verification — not a campaign level",typeof(LevelSetup),typeof(SceneDashSettings),typeof(LevelTransition));
            var setup = setupRoot.GetComponent<LevelSetup>(); setup.playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs+"Player.prefab");
            setup.bankPrefab = AssetDatabase.LoadAssetAtPath<RunDashBank>(Prefabs+"RunDashBank.prefab");
            setup.spawn = new GameObject("Player spawn").transform; setup.spawn.SetParent(setupRoot.transform); setup.spawn.position = new Vector3(1,1.81f);
            var transition = setupRoot.GetComponent<LevelTransition>(); transition.destinationScene = "Assets/Game/Scenes/VerificationDestination.unity";
            var cameraRoot = new GameObject("Stationary verification camera",typeof(Camera),typeof(AudioListener)); cameraRoot.tag = "MainCamera";
            cameraRoot.transform.position = new Vector3(16,9,-10); var camera = cameraRoot.GetComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 9;
            camera.allowHDR = camera.allowMSAA = camera.allowDynamicResolution = false; camera.backgroundColor = new Color(.08f,.12f,.18f);
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            var pp = cameraRoot.AddComponent<PixelPerfectCamera>(); pp.assetsPPU = 20; pp.refResolutionX = 640; pp.refResolutionY = 360;
            pp.gridSnapping = PixelPerfectCamera.GridSnapping.UpscaleRenderTexture; pp.cropFrame = PixelPerfectCamera.CropFrame.Windowbox;
            var bg = new GameObject("Existing Ace background",typeof(SpriteRenderer)); bg.transform.position = new Vector3(16,9);
            var sr = bg.GetComponent<SpriteRenderer>(); sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/BlueBG.png"); sr.sharedMaterial = Material;
            sr.sortingOrder = -20; sr.drawMode = SpriteDrawMode.Tiled; sr.size = new Vector2(32,18);
            Solid("Left floor / input comparison region",new Vector2(6,.5f),new Vector2(12,1));
            Solid("Right floor",new Vector2(23.5f,.5f),new Vector2(17,1));
            Solid("Left boundary",new Vector2(-.5f,9),new Vector2(1,18)); Solid("Right boundary",new Vector2(32.5f,9),new Vector2(1,18));
            Solid("Ceiling",new Vector2(16,18.5f),new Vector2(32,1));
            Instance("FallDeathZone",new Vector2(13.5f,-1.5f));
            Instance("MovingPlatform",new Vector2(4,2));
            var lift = Instance("MovingPlatform",new Vector2(10,3)); lift.name = "Vertical moving platform";
            var mp = lift.GetComponentInChildren<MovingPlatform2D>(); mp.pointB.localPosition = new Vector3(0,7); mp.speed = 2;
            Instance("Turret",new Vector2(22,5));
            Solid("Turret interaction ledge",new Vector2(20.5f,3.5f),new Vector2(5,.5f));
            Instance("BreakableWall",new Vector2(18,2.5f));
            Instance("FallingCrusherPlatform",new Vector2(27,13));
            Solid("Crusher upper approach",new Vector2(22.5f,12),new Vector2(3,.5f));
            var exit = Solid("Verification exit",new Vector2(31,2),new Vector2(.75f,2));
            exit.GetComponent<BoxCollider2D>().isTrigger = true; exit.AddComponent<LevelExit>().transition = transition;
            exit.GetComponentInChildren<SpriteRenderer>().color = new Color(.3f,1,.3f,.6f);
            EditorSceneManager.SaveScene(scene,ScenePath);
            var scenes = EditorBuildSettings.scenes.ToList(); scenes.Add(new EditorBuildSettingsScene(ScenePath,true)); EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
        }
        public static void DevelopmentBuild()
        {
            Directory.CreateDirectory("Builds/UnifiedGameplay");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),
                locationPathName = "Builds/UnifiedGameplay/PIDR.exe", target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
            });
            File.WriteAllText("Logs/UnifiedGameplay/build-summary.txt",report.summary.result+" errors="+report.summary.totalErrors+" warnings="+report.summary.totalWarnings);
            File.WriteAllLines("Logs/UnifiedGameplay/build-messages.txt",report.steps.SelectMany(s=>s.messages)
                .Where(m=>m.type==LogType.Warning || m.type==LogType.Error).Select(m=>m.type+": "+m.content));
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Development build failed.");
            File.WriteAllText("Builds/UnifiedGameplay/PlayVerification.cmd","@echo off\r\nstart \"\" \"%~dp0PIDR.exe\" -mechanics-room\r\n");
        }
    }
}
