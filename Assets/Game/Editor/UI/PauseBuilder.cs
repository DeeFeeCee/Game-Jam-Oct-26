using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using Object = UnityEngine.Object;

namespace GameJam.UI.Editor
{
    public static class PauseBuilder
    {
        public const string PrefabPath = "Assets/Game/UI/Resources/UI/Pause.prefab";

        [MenuItem("Game Jam/UI/Create Pause If Absent")]
        public static void Create()
        {
            if (File.Exists(PrefabPath)) throw new InvalidOperationException("Pause exists; edit it instead of regenerating.");
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Game/UI/TextResources/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (font == null) throw new InvalidOperationException("Accepted menu font is required.");
            var root = new GameObject("Pause"); root.SetActive(false);
            var c = root.AddComponent<PauseController>();
            var canvasObject = new GameObject("PauseOverlay", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvasObject.transform.SetParent(root.transform, false); c.overlay = canvasObject;
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 90; canvas.pixelPerfect = true;
            var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
            var tint = Panel(canvas.transform, "BackgroundOverlayPlaceholder", new Color(.015f, .035f, .055f, .65f));
            Fit(tint, 0, 0, 1, 1); tint.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            var panel = Panel(canvas.transform, "SuspensionPanel", new Color(.035f, .085f, .125f, .97f)); Fit(panel, .14f, .14f, .86f, .86f);
            var outline = panel.gameObject.AddComponent<UnityEngine.UI.Outline>(); outline.effectColor = new Color(.25f, .52f, .52f); outline.effectDistance = new Vector2(2, -2);
            var ink = new Color(.9f, .96f, .96f); var teal = new Color(.21f, .72f, .68f);
            c.rootView = new GameObject("RootView", typeof(RectTransform)); c.rootView.transform.SetParent(panel, false); Fit(c.rootView.GetComponent<RectTransform>(), 0, 0, 1, 1);
            Text(c.rootView.transform, "Headline", "EXPERIMENT\nSUSPENDED", 40, ink, .055f, .70f, .72f, .97f).fontStyle = FontStyles.Bold;
            Text(c.rootView.transform, "Subtitle", "Subject activity temporarily paused.", 20, new Color(.68f, .79f, .8f), .055f, .58f, .72f, .70f);
            var actions = new GameObject("PauseActions", typeof(RectTransform), typeof(UnityEngine.UI.VerticalLayoutGroup)); actions.transform.SetParent(c.rootView.transform, false);
            Fit(actions.GetComponent<RectTransform>(), .055f, .10f, .67f, .50f);
            var layout = actions.GetComponent<UnityEngine.UI.VerticalLayoutGroup>(); layout.spacing = 12; layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            c.resumeButton = Button(actions.transform, "ResumeExperiment", "RESUME EXPERIMENT");
            c.controlsButton = Button(actions.transform, "Controls", "CONTROLS"); c.mainMenuButton = Button(actions.transform, "ReturnToMainMenu", "RETURN TO MAIN MENU");
            Link(c.resumeButton, c.resumeButton, c.controlsButton); Link(c.controlsButton, c.resumeButton, c.mainMenuButton); Link(c.mainMenuButton, c.controlsButton, c.mainMenuButton);
            var art = Panel(c.rootView.transform, "SuspensionIllustrationPlaceholder", new Color(.07f, .17f, .21f)); Fit(art, .73f, .24f, .95f, .72f);
            var mark = Panel(art, "ResearchStatusGraphicPlaceholder", teal); Fit(mark, .2f, .25f, .8f, .75f);
            var cut = Panel(mark, "MarkCutout", new Color(.07f, .17f, .21f)); Fit(cut, .38f, 0, .62f, 1);
            Text(c.rootView.transform, "ResearchStatusLabel", "STATUS / SUSPENDED", 12, teal, .73f, .73f, .99f, .87f);
            var animation = Panel(art, "OptionalAnimationSlot", Color.clear); Fit(animation, 0, 0, 1, 1);

            c.controlsView = new GameObject("ControlsView", typeof(RectTransform)); c.controlsView.transform.SetParent(panel, false); Fit(c.controlsView.GetComponent<RectTransform>(), 0, 0, 1, 1);
            Text(c.controlsView.transform, "Heading", "CONTROLS", 36, ink, .055f, .78f, .94f, .94f).fontStyle = FontStyles.Bold;
            Text(c.controlsView.transform, "KeyboardControls", "KEYBOARD\n\nA/D or Left/Right    Move\nW or Up    Jump\nSpace    Dash\nWASD / Arrows    Dash direction\nEscape    Pause / Resume", 20, ink, .055f, .24f, .57f, .77f);
            Text(c.controlsView.transform, "GamepadControls", "GAMEPAD\n\nLeft stick    Move and aim\nSouth button    Jump\nRight shoulder    Dash\nStart / Menu    Pause / Resume", 20, ink, .59f, .24f, .98f, .77f);
            c.backButton = Button(c.controlsView.transform, "Back", "BACK"); Fit(c.backButton.GetComponent<RectTransform>(), .055f, .09f, .37f, .20f); Link(c.backButton, c.backButton, c.backButton);
            Text(c.controlsView.transform, "BackHint", "ESCAPE / GAMEPAD EAST (B): BACK", 13, teal, .42f, .09f, .97f, .20f);
            c.status = Text(panel, "Status", "", 12, teal, .055f, .015f, .98f, .07f);
            c.controlsView.SetActive(false);

            var es = new GameObject("PauseEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)); es.transform.SetParent(root.transform, false);
            es.GetComponent<InputSystemUIInputModule>().enabled = false;
            c.uiEventSystem = es.GetComponent<EventSystem>(); c.uiEventSystem.firstSelectedGameObject = c.resumeButton.gameObject;
            c.inputScope = es.AddComponent<MenuInputScope>(); c.inputScope.inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Settings/InputSystem_Actions.inputactions"); c.inputScope.enabled = false;
            es.SetActive(false); canvasObject.SetActive(false);
            foreach (var component in root.GetComponentsInChildren<Component>(true))
                if (PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            root.SetActive(true); PrefabUtility.SaveAsPrefabAsset(root, PrefabPath); Object.DestroyImmediate(root); AssetDatabase.SaveAssets();

            TextMeshProUGUI Text(Transform parent, string name, string value, int size, Color color, float x0, float y0, float x1, float y1)
            {
                var obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); obj.transform.SetParent(parent, false);
                var text = obj.GetComponent<TextMeshProUGUI>(); text.font = font; text.text = value; text.fontSize = size; text.color = color;
                text.alignment = TextAlignmentOptions.MidlineLeft; text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.Normal;
                Fit(obj.GetComponent<RectTransform>(), x0, y0, x1, y1); return text;
            }
        }
        private static RectTransform Panel(Transform parent, string name, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image)); obj.transform.SetParent(parent, false);
            var image = obj.GetComponent<UnityEngine.UI.Image>(); image.color = color; image.raycastTarget = false; return obj.GetComponent<RectTransform>();
        }
        private static void Fit(RectTransform rect, float x0, float y0, float x1, float y1)
        { rect.anchorMin = new Vector2(x0, y0); rect.anchorMax = new Vector2(x1, y1); rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static UnityEngine.UI.Button Button(Transform parent, string name, string label)
        {
            var obj = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(MenuBuilder.ButtonPath), parent);
            obj.name = name; obj.GetComponentInChildren<TMP_Text>().text = label; return obj.GetComponent<UnityEngine.UI.Button>();
        }
        private static void Link(UnityEngine.UI.Button button, UnityEngine.UI.Button up, UnityEngine.UI.Button down)
        { button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit, selectOnUp = up, selectOnDown = down }; }
        [MenuItem("Game Jam/UI/Build Pause Windows Development Player")]
        public static void Build()
        {
            Directory.CreateDirectory("Builds/Pause"); Directory.CreateDirectory("Logs/Pause");
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(), locationPathName = "Builds/Pause/GameJam-Pause.exe", target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
            File.WriteAllText("Logs/Pause/build-summary.txt", result.summary.result + " errors=" + result.summary.totalErrors);
            if (result.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Pause development build failed.");
        }
    }
}
