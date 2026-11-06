using System;
using System.IO;
using System.Linq;
using GameJam.Levels;
using GameJam.LevelAuthoring.Editor;
using GameJam.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using Object = UnityEngine.Object;

namespace GameJam.BlueIntegration.Editor
{
    public static class BlueIntegrationBuilder
    {
        public const string Folder = "Assets/Game/Prefabs/BlueEnvironment";
        public const string Scene = "Assets/Game/Scenes/Verification/BlueArtTerminal.unity";
        private const string Art = "Assets/Art/Levels/";
        private static Sprite Sprite(string file, string name = null) => AssetDatabase.LoadAllAssetsAtPath(Art + file + ".png").OfType<Sprite>().First(s => name == null || s.name == name);
        [MenuItem("Game Jam/Blue Environment/Create Integration If Absent")]
        public static void Create()
        {
            if (File.Exists(Scene) || Directory.Exists(Folder) && Directory.GetFiles(Folder, "*.prefab").Length > 0) throw new InvalidOperationException("Integration exists; edit it rather than regenerating.");
            Directory.CreateDirectory(Folder); Directory.CreateDirectory(Path.GetDirectoryName(Scene)); AssetDatabase.Refresh();
            Door("BlueEntrance", "BlueEnter", false); Door("BlueExit", "BlueExit", true); Terminal();
            foreach (var sprite in AssetDatabase.LoadAllAssetsAtPath(Art + "BluePlatforms.png").OfType<Sprite>())
            {
                var obj = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(LevelAuthoring32x18.Platform));
                PrefabUtility.UnpackPrefabInstance(obj, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                obj.name = sprite.name; BluePlatformAuthoring.Apply(obj, sprite); Save(obj, sprite.name);
            }
            // Copy only into an isolated scene. The template and all production levels stay byte-identical.
            if (!AssetDatabase.CopyAsset(LevelAuthoring32x18.Template, Scene)) throw new IOException("Could not copy verification template.");
            var scene = EditorSceneManager.OpenScene(Scene);
            var setup = Object.FindFirstObjectByType<LevelSetup>();
            var entrance = Instance("BlueEntrance"); entrance.transform.position = new Vector3(setup.spawn.position.x, 1, 0);
            var terminal = Instance("BlueTerminal"); terminal.transform.position = new Vector3(8, 1.8f, 0);
            var oldExit = Object.FindFirstObjectByType<LevelExit>();
            if (oldExit != null)
            {
                var exit = Instance("BlueExit"); exit.transform.position = oldExit.transform.position;
                var source = oldExit.GetComponent<BoxCollider2D>(); var target = exit.GetComponent<BoxCollider2D>();
                target.size = source.size; target.offset = source.offset;
                float baseY = source.offset.y - source.size.y / 2f;
                exit.transform.Find("Door").localPosition += Vector3.up * baseY;
                exit.GetComponent<LevelTransition>().destinationScene = "Assets/Game/Scenes/VerificationDestination.unity";
                Object.DestroyImmediate(oldExit.gameObject);
            }
            foreach (var box in Object.FindObjectsByType<BoxCollider2D>(FindObjectsSortMode.None))
                if (!box.isTrigger && box.transform.Find("Floor visuals") != null)
                    BluePlatformAuthoring.Apply(box.gameObject, Sprite("BluePlatforms", "BluePlatform_Big"));
            EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("Blue integration authored; production scenes and build settings preserved.");
        }
        private static GameObject Instance(string name) => (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/" + name + ".prefab"));
        private static void Save(GameObject obj, string name)
        { PrefabUtility.SaveAsPrefabAsset(obj, Folder + "/" + name + ".prefab"); Object.DestroyImmediate(obj); }
        private static SpriteRenderer Visual(Transform root, string name, Sprite sprite)
        {
            var child = new GameObject(name, typeof(SpriteRenderer)); child.transform.SetParent(root, false);
            child.transform.localScale = Vector3.one * 5f;
            var r = child.GetComponent<SpriteRenderer>(); r.sprite = sprite;
            r.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Tests/Fixtures/Movement/Art/PlaceholderUnlit.mat");
            if (r.sharedMaterial == null) r.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            r.sortingOrder = -1; return r;
        }
        private static void Door(string name, string file, bool exit)
        {
            var root = new GameObject(name);
            // Ace left these two importers in Single mode; use the full doorway including its sign.
            var upper = Visual(root.transform, "Door", Sprite(file));
            upper.transform.localPosition = new Vector3(0, upper.sprite.rect.height / 40f, 0);
            if (exit)
            {
                var box = root.AddComponent<BoxCollider2D>(); box.isTrigger = true; box.size = new Vector2(1, 2); box.offset = new Vector2(0, 1);
                root.AddComponent<LevelExit>().transition = root.AddComponent<LevelTransition>();
            }
            Save(root, name);
        }
        private static void Terminal()
        {
            var root = new GameObject("BlueTerminal"); root.SetActive(false);
            var c = root.AddComponent<ResearchTerminal>();
            var visual = Visual(root.transform, "Blinking terminal", Sprite("BlueTerminal", "BlueTerminal_Norm"));
            var animator = visual.gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Art + "BlueAnims/TerminalBlink.controller");
            var canvas = new GameObject("Research display", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvas.transform.SetParent(root.transform, false); c.overlay = canvas;
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay; canvas.GetComponent<Canvas>().sortingOrder = 80;
            var scale = canvas.GetComponent<UnityEngine.UI.CanvasScaler>(); scale.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scale.referenceResolution = new Vector2(1280, 720); scale.matchWidthOrHeight = .5f;
            var frame = new GameObject("Ace textbox frame", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.AspectRatioFitter));
            frame.transform.SetParent(canvas.transform, false); Fit(frame, .1f, .1f, .9f, .9f);
            frame.GetComponent<UnityEngine.UI.Image>().sprite = Sprite("BlueTerminalTextbox");
            var aspect = frame.GetComponent<UnityEngine.UI.AspectRatioFitter>(); aspect.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.FitInParent; aspect.aspectRatio = 32f / 18f;
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Game/UI/TextResources/Resources/Fonts & Materials/LiberationSans SDF.asset");
            var lore = new GameObject("Lore - edit TMP text", typeof(RectTransform), typeof(TextMeshProUGUI)); lore.transform.SetParent(frame.transform, false);
            Fit(lore, .09f, .22f, .91f, .86f); c.lore = lore.GetComponent<TextMeshProUGUI>(); c.lore.font = font;
            c.lore.text = ""; c.lore.fontSize = 24; c.lore.color = new Color(.02f, .08f, .12f); c.lore.raycastTarget = false;
            c.lore.textWrappingMode = TextWrappingModes.Normal; c.lore.overflowMode = TextOverflowModes.Truncate;
            var button = new GameObject("Close", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button)); button.transform.SetParent(frame.transform, false);
            Fit(button, .72f, .07f, .91f, .17f); button.GetComponent<UnityEngine.UI.Image>().color = new Color(.04f, .15f, .24f);
            c.closeButton = button.GetComponent<UnityEngine.UI.Button>(); c.closeButton.targetGraphic = button.GetComponent<UnityEngine.UI.Image>();
            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)); label.transform.SetParent(button.transform, false); Fit(label, 0, 0, 1, 1);
            var text = label.GetComponent<TextMeshProUGUI>(); text.font = font; text.text = "CLOSE"; text.fontSize = 22; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
            var es = new GameObject("Terminal EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)); es.transform.SetParent(root.transform, false);
            es.GetComponent<InputSystemUIInputModule>().enabled = false; c.uiEventSystem = es.GetComponent<EventSystem>();
            c.inputScope = es.AddComponent<MenuInputScope>(); c.inputScope.inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Settings/InputSystem_Actions.inputactions"); c.inputScope.enabled = false;
            ConfigureTerminalPresentation(c);
            StyleResearchFrame(c);
            es.SetActive(false); canvas.SetActive(false); root.SetActive(true); Save(root, "BlueTerminal");
        }

        private static void ConfigureTerminalPresentation(ResearchTerminal terminal)
        {
            var canvas = terminal.overlay.transform;
            if (canvas.Find("Textbox bounds") != null) return;
            var frame = canvas.Find("Ace textbox frame");
            var backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            backdrop.transform.SetParent(canvas, false);
            backdrop.transform.SetAsFirstSibling();
            Fit(backdrop, 0, 0, 1, 1);
            backdrop.GetComponent<UnityEngine.UI.Image>().color = new Color(0, 0, 0, .68f);

            var bounds = new GameObject("Textbox bounds", typeof(RectTransform));
            bounds.transform.SetParent(canvas, false);
            Fit(bounds, .18f, .16f, .82f, .84f);
            frame.SetParent(bounds.transform, false);
            var image = frame.GetComponent<UnityEngine.UI.Image>();
            image.preserveAspect = true;
            frame.GetComponent<UnityEngine.UI.AspectRatioFitter>().aspectRatio = image.sprite.rect.width / image.sprite.rect.height;

            var body = terminal.lore;
            Fit(body.gameObject, .075f, .24f, .925f, .73f);
            body.color = new Color32(8, 24, 40, 255);
            body.enableAutoSizing = true;
            body.fontSizeMin = 18;
            body.fontSizeMax = 25;
            body.alignment = TextAlignmentOptions.TopLeft;

            terminal.title = AddText(frame, body, "Title - edit TMP text", "RESEARCH LOG");
            Fit(terminal.title.gameObject, .075f, .76f, .925f, .88f);
            terminal.title.fontStyle = FontStyles.Bold;
            terminal.title.fontSize = 34;
            terminal.title.fontSizeMin = 22;
            terminal.title.fontSizeMax = 34;

            terminal.emptyEntry = AddText(frame, body, "Empty entry", "No research entry has been assigned.");
            Fit(terminal.emptyEntry.gameObject, .075f, .24f, .925f, .73f);
            terminal.emptyEntry.gameObject.SetActive(false);
            Fit(terminal.closeButton.gameObject, .72f, .09f, .925f, .21f);

            var visual = terminal.GetComponentInChildren<Animator>(true).GetComponent<SpriteRenderer>();
            var accent = new GameObject("Blink status accent", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            accent.transform.SetParent(visual.transform, false);
            var blink = Sprite("BlueTerminal", "BlueTerminal_Blink");
            // The changed source pixel is (25,14) from the frame's top left.
            var pixelCenter = new Vector2(25.5f, blink.rect.height - 14.5f);
            accent.transform.localPosition = (pixelCenter - blink.pivot) / blink.pixelsPerUnit;
            accent.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Tests/Fixtures/Movement/Art/PlaceholderSquare.png");
            accent.sharedMaterial = visual.sharedMaterial;
            accent.sortingLayerID = visual.sortingLayerID;
            accent.sortingOrder = visual.sortingOrder + 1;
            accent.color = new Color32(166, 252, 219, 255);
            var dimensions = accent.sprite.bounds.size;
            float side = 3f / blink.pixelsPerUnit;
            accent.transform.localScale = new Vector3(side / dimensions.x, side / dimensions.y, 1);
            accent.enabled = false;
            var driver = new SerializedObject(visual.gameObject.AddComponent<TerminalBlinkIndicator>());
            driver.FindProperty("blinkFrame").objectReferenceValue = blink;
            driver.FindProperty("indicator").objectReferenceValue = accent;
            driver.ApplyModifiedPropertiesWithoutUndo();
        }

        private static TMP_Text AddText(Transform parent, TMP_Text reference, string name, string content)
        {
            var text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            text.transform.SetParent(parent, false);
            text.font = reference.font;
            text.color = reference.color;
            text.text = content;
            text.fontSize = 25;
            text.enableAutoSizing = true;
            text.fontSizeMin = 18;
            text.fontSizeMax = 25;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static void StyleResearchFrame(ResearchTerminal terminal)
        {
            var frame = terminal.overlay.transform.Find("Textbox bounds/Ace textbox frame");
            if (frame.Find("Research chassis") != null) return;
            terminal.overlay.transform.Find("Backdrop").GetComponent<UnityEngine.UI.Image>().color = new Color(0, 0, 0, .58f);

            // The sprite's colored corner and bottom stripe stay fully outside the reading surface.
            var chassis = Surface(frame, "Research chassis", new Color32(7, 23, 33, 255), .045f, .097f, .955f, .887f);
            var header = Surface(chassis, "Header", new Color32(18, 44, 59, 255), .008f, .812f, .992f, .99f);
            var reading = Surface(chassis, "Reading surface", new Color32(12, 32, 45, 255), .008f, .178f, .992f, .803f);
            var actions = Surface(chassis, "Action rail", new Color32(18, 44, 59, 255), .008f, .012f, .992f, .169f);
            Surface(header, "Status accent", new Color32(70, 185, 180, 255), .022f, .22f, .029f, .78f);

            terminal.title.transform.SetParent(header, false);
            Fit(terminal.title.gameObject, .052f, .12f, .96f, .89f);
            terminal.title.alignment = TextAlignmentOptions.MidlineLeft;
            terminal.title.color = new Color32(230, 249, 250, 255);
            foreach (var text in new[] { terminal.lore, terminal.emptyEntry })
            {
                text.transform.SetParent(reading, false);
                Fit(text.gameObject, .043f, .075f, .957f, .925f);
                text.color = new Color32(220, 238, 241, 255);
            }
            terminal.emptyEntry.color = new Color32(161, 201, 206, 255);

            terminal.closeButton.transform.SetParent(actions, false);
            Fit(terminal.closeButton.gameObject, .755f, .11f, .974f, .89f);
            terminal.closeButton.targetGraphic.color = new Color32(28, 85, 95, 255);
            var label = AddText(actions, terminal.title, "Panel identifier", "RESEARCH TERMINAL");
            Fit(label.gameObject, .043f, .12f, .65f, .88f);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.fontSizeMin = 13;
            label.fontSizeMax = 16;
            label.color = new Color32(151, 201, 205, 255);
        }

        private static Transform Surface(Transform parent, string name, Color32 color, float x0, float y0, float x1, float y1)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
            image.transform.SetParent(parent, false);
            image.color = color;
            image.raycastTarget = false;
            Fit(image.gameObject, x0, y0, x1, y1);
            return image.transform;
        }

        public static void ApplyRevision03()
        {
            string path = Folder + "/BlueTerminal.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                StyleResearchFrame(root.GetComponent<ResearchTerminal>());
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        public static void ApplyRevision02()
        {
            string path = Folder + "/BlueTerminal.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                ConfigureTerminalPresentation(root.GetComponent<ResearchTerminal>());
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            var scene = EditorSceneManager.OpenScene(Scene);
            var terminal = Object.FindFirstObjectByType<ResearchTerminal>();
            terminal.title.text = "RESEARCH LOG";
            terminal.lore.text = "TEMPORARY UI VERIFICATION\n\nThis sample checks that research text is readable inside the blue frame. Each terminal can display its own entry.";
            PrefabUtility.RecordPrefabInstancePropertyModifications(terminal.lore);
            var geometry = GameObject.Find("Geometry - static whole-cell platforms").transform;
            AddTraversalPlatform(geometry, "BluePlatforms_Small", "Verification platform - 3 cells", 12, 3);
            AddTraversalPlatform(geometry, "BluePlatform_Big", "Verification platform - 7 cells", 17, 7);
            EditorSceneManager.SaveScene(scene);
        }

        private static void AddTraversalPlatform(Transform geometry, string prefab, string name, int x, int width)
        {
            if (geometry.Find(name) != null) return;
            var platform = Instance(prefab);
            platform.name = name;
            platform.transform.SetParent(geometry, false);
            LevelAuthoring32x18.SetGeometry(platform, new Vector2Int(x, 1), new Vector2Int(width, 1));
        }
        private static void Fit(GameObject obj, float x0, float y0, float x1, float y1)
        { var r = obj.GetComponent<RectTransform>(); r.anchorMin = new Vector2(x0, y0); r.anchorMax = new Vector2(x1, y1); r.offsetMin = r.offsetMax = Vector2.zero; }
        public static void RefreshPlatformAdapters()
        {
            foreach (var sprite in AssetDatabase.LoadAllAssetsAtPath(Art + "BluePlatforms.png").OfType<Sprite>())
            {
                string path = Folder + "/" + sprite.name + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try { BluePlatformAuthoring.Apply(root, sprite); PrefabUtility.SaveAsPrefabAsset(root, path); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var scene = EditorSceneManager.OpenScene(Scene);
            foreach (var box in Object.FindObjectsByType<BoxCollider2D>(FindObjectsSortMode.None))
                if (box.transform.Find(BluePlatformAuthoring.ArtName) != null)
                    BluePlatformAuthoring.Apply(box.gameObject, Sprite("BluePlatforms", "BluePlatform_Big"));
            EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        }
        public static void Build()
        {
            const string buildFolder = "Builds/BlueArtTerminal/R03";
            Directory.CreateDirectory(buildFolder);
            var scenes = new[] { Scene }.Concat(EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path)).Distinct().ToArray();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = scenes, target = BuildTarget.StandaloneWindows64,
                locationPathName = buildFolder + "/ProjectPIDR-BlueCandidate-R03.exe", options = BuildOptions.Development });
            File.WriteAllText(buildFolder + "/build-summary.txt", report.summary.result + " errors=" + report.summary.totalErrors + " warnings=" + report.summary.totalWarnings);
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Blue candidate build failed.");
        }
    }
}

