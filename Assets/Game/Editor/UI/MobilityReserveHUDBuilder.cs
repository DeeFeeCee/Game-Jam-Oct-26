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
        [MenuItem("Game Jam/UI/Create Mobility Reserve HUD If Absent")]
        public static void Create()
        {
            if (File.Exists(PrefabPath)) throw new InvalidOperationException("HUD exists; edit its prefab rather than regenerate.");
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Game/UI/TextResources/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (font == null) throw new InvalidOperationException("Accepted menu font is required.");
            var root = new GameObject("MobilityReserveHUD", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(CanvasGroup));
            root.SetActive(false);
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10; canvas.pixelPerfect = true;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
            var group = root.GetComponent<CanvasGroup>(); group.interactable = false; group.blocksRaycasts = false;
            var c = root.AddComponent<MobilityReserveHUDController>();
            var panel = new GameObject("MonitorPanelBackground", typeof(RectTransform), typeof(UnityEngine.UI.Image)); panel.transform.SetParent(root.transform, false);
            c.panel = panel.GetComponent<RectTransform>(); c.panel.anchorMin = c.panel.anchorMax = c.panel.pivot = new Vector2(0, 1);
            c.panel.anchoredPosition = new Vector2(16, -16); c.panel.sizeDelta = new Vector2(320, 108);
            panel.GetComponent<UnityEngine.UI.Image>().color = new Color(.025f, .065f, .095f, .90f);
            var outline = panel.AddComponent<UnityEngine.UI.Outline>(); outline.effectColor = new Color(.21f, .55f, .55f, .7f); outline.effectDistance = new Vector2(1, -1);
            var ink = new Color(.9f, .96f, .96f); var teal = new Color(.21f, .72f, .68f);
            Text("PrimaryLabel", "MOBILITY RESERVE", 16, ink, new Vector2(12, -9), new Vector2(235, 24)).fontStyle = FontStyles.Bold;
            Text("ResearchIdentifier", "P.I.D.R. / SUBJECT MONITOR", 11, new Color(.54f, .72f, .73f), new Vector2(12, -35), new Vector2(270, 16));
            c.counter = Text("NumericDisplay", "--", 30, ink, new Vector2(252, -8), new Vector2(56, 38)); c.counter.alignment = TextAlignmentOptions.TopRight;
            var area = new GameObject("ChargeSegments", typeof(RectTransform), typeof(UnityEngine.UI.GridLayoutGroup)); area.transform.SetParent(panel.transform, false);
            c.segmentArea = area.GetComponent<RectTransform>(); Place(c.segmentArea, new Vector2(12, -56), new Vector2(296, 12));
            c.segmentLayout = area.GetComponent<UnityEngine.UI.GridLayoutGroup>(); c.segmentLayout.childAlignment = TextAnchor.UpperLeft;
            var template = new GameObject("ReplaceableSegmentTemplate", typeof(RectTransform), typeof(UnityEngine.UI.Image)); template.transform.SetParent(root.transform, false);
            c.segmentTemplate = template.GetComponent<UnityEngine.UI.Image>(); c.segmentTemplate.color = teal; template.SetActive(false);
            c.warning = Text("WarningMessage", "", 12, c.criticalColor, new Vector2(12, -80), new Vector2(296, 19));
            var logo = new GameObject("OptionalFacilityLogoSlot", typeof(RectTransform), typeof(UnityEngine.UI.Image)); logo.transform.SetParent(panel.transform, false);
            Place(logo.GetComponent<RectTransform>(), new Vector2(300, -39), new Vector2(7, 7)); logo.GetComponent<UnityEngine.UI.Image>().color = teal;
            foreach (var graphic in root.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) graphic.raycastTarget = false;
            // No EventSystem, action scope or GraphicRaycaster: this display cannot own input.
            root.SetActive(true); PrefabUtility.SaveAsPrefabAsset(root, PrefabPath); Object.DestroyImmediate(root); AssetDatabase.SaveAssets();

            TextMeshProUGUI Text(string name, string value, int size, Color color, Vector2 position, Vector2 dimensions)
            {
                var obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); obj.transform.SetParent(panel.transform, false);
                Place(obj.GetComponent<RectTransform>(), position, dimensions);
                var text = obj.GetComponent<TextMeshProUGUI>(); text.font = font; text.text = value; text.fontSize = size; text.color = color;
                text.alignment = TextAlignmentOptions.TopLeft; text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.NoWrap;
                return text;
            }
        }
        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = position; rect.sizeDelta = size;
        }
        [MenuItem("Game Jam/UI/Build Mobility HUD Windows Development Player")]
        public static void Build()
        {
            Directory.CreateDirectory("Builds/HUD"); Directory.CreateDirectory("Logs/HUD");
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(), locationPathName = "Builds/HUD/GameJam-MobilityHUD.exe", target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
            File.WriteAllText("Logs/HUD/build-summary.txt", result.summary.result + " errors=" + result.summary.totalErrors);
            if (result.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("HUD Windows build failed.");
        }
    }
}
