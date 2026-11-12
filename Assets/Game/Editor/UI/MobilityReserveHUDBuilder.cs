using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameJam.UI.Editor
{
    public static class MobilityReserveHUDBuilder
    {
        public const string PrefabPath = "Assets/Game/UI/Resources/UI/MobilityReserveHUD.prefab";
        internal const string IconPath = "Assets/Game/UI/DashIcon.png";

        [MenuItem("Game Jam/UI/Create Mobility Reserve HUD If Absent")]
        public static void Create()
        {
            if (File.Exists(PrefabPath)) throw new InvalidOperationException("HUD exists; edit its prefab rather than regenerate.");
            var root = new GameObject("MobilityReserveHUD", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(CanvasGroup), typeof(MobilityReserveHUDController));
            root.SetActive(false);
            try
            {
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 10;
                canvas.pixelPerfect = true;
                var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280, 720);
                scaler.matchWidthOrHeight = .5f;
                var group = root.GetComponent<CanvasGroup>();
                group.interactable = false;
                group.blocksRaycasts = false;
                ApplyCompactLayout(root);
                root.SetActive(true);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        internal static void ApplyCompactLayout(GameObject root)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MenuBuilder.ProductionMenuFontPath);
            var icon = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
            if (font == null || !font.HasCharacters("RUN TIME 0123456789:.-") || icon == null)
                throw new InvalidOperationException("The HUD requires its DeeFont glyphs and dash icon.");

            var controller = root.GetComponent<MobilityReserveHUDController>();
            var panel = Image(root.transform, "MonitorPanelBackground");
            controller.panel = panel.rectTransform;
            Place(controller.panel, new Vector2(0, 1), new Vector2(16, -16), new Vector2(90, 44));
            var ink = new Color(.84f, .96f, .94f);
            var background = new Color(.035f, .075f, .105f, .96f);
            panel.color = background;
            var outline = panel.GetComponent<UnityEngine.UI.Outline>();
            if (outline == null) outline = panel.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(.21f, .55f, .55f, .7f);
            outline.effectDistance = new Vector2(1, -1);
            foreach (var name in new[] { "PrimaryLabel", "ResearchIdentifier", "ChargeSegments", "WarningMessage", "OptionalFacilityLogoSlot" })
                if (panel.transform.Find(name) is Transform obsolete) Object.DestroyImmediate(obsolete.gameObject);
            if (root.transform.Find("ReplaceableSegmentTemplate") is Transform template) Object.DestroyImmediate(template.gameObject);

            controller.dashIcon = Image(panel.transform, "DashIcon");
            controller.dashIcon.sprite = icon;
            controller.dashIcon.preserveAspect = true;
            controller.dashIcon.color = controller.activeColor;
            Place(controller.dashIcon.rectTransform, new Vector2(0, 1), new Vector2(11, -10), new Vector2(24, 24));
            controller.counter = Label(panel.transform, "NumericDisplay", font, "--", 24, controller.activeColor);
            Place(controller.counter.rectTransform, new Vector2(0, 1), new Vector2(40, -3), new Vector2(43, 38));
            controller.counter.alignment = TextAlignmentOptions.Center;
            controller.counter.enableAutoSizing = true;
            controller.counter.fontSizeMin = 5;
            controller.counter.fontSizeMax = 24;

            var timerPanel = Image(root.transform, "RunTimerBackground");
            timerPanel.color = background;
            Place(timerPanel.rectTransform, Vector2.one, new Vector2(-16, -16), new Vector2(224, 44));
            controller.timer = Label(timerPanel.transform, "RunTime", font, "RUN TIME  00:00.00", 19, ink);
            Place(controller.timer.rectTransform, Vector2.one, new Vector2(-10, -3), new Vector2(204, 38));
            controller.timer.alignment = TextAlignmentOptions.MidlineRight;
            controller.timer.enableAutoSizing = true;
            controller.timer.fontSizeMin = 12;
            controller.timer.fontSizeMax = 19;
        }

        private static T Child<T>(Transform parent, string name) where T : Component
        {
            if (parent.Find(name) is Transform existing) return existing.GetComponent<T>();
            var child = new GameObject(name, typeof(RectTransform), typeof(T));
            child.transform.SetParent(parent, false);
            return child.GetComponent<T>();
        }

        private static UnityEngine.UI.Image Image(Transform parent, string name)
        {
            var image = Child<UnityEngine.UI.Image>(parent, name);
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text Label(Transform parent, string name, TMP_FontAsset font, string value, int size, Color color)
        {
            var text = Child<TextMeshProUGUI>(parent, name);
            text.font = font;
            text.fontSharedMaterial = font.material;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.fontStyle = FontStyles.Normal;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        [MenuItem("Game Jam/UI/Build Mobility HUD Windows Development Player")]
        public static void Build()
        {
            Directory.CreateDirectory("Builds/HUD");
            Directory.CreateDirectory("Logs/HUD");
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = "Builds/HUD/GameJam-MobilityHUD.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            File.WriteAllText("Logs/HUD/build-summary.txt", result.summary.result + " errors=" + result.summary.totalErrors);
            if (result.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("HUD Windows build failed.");
        }
    }
}
