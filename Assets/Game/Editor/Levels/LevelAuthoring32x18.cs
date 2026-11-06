using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using GameJam.Foundation.Editor;
using GameJam.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GameJam.LevelAuthoring.Editor
{
    /// <summary>Explicit authoring actions. No automatic scene changes or runtime snapping.</summary>
    public static class LevelAuthoring32x18
    {
        public const string Levels = "Assets/Game/Scenes/Levels";
        public const string Level01 = Levels + "/Level01.unity";
        public const string Template = Levels + "/LevelTemplate32x18.unity";
        public const string Platform = "Assets/Game/Prefabs/GridPlatform32x18.prefab";
        public const string Legacy = "Assets/Game/Tests/Fixtures/Production32x18/LegacyLevel01.unity";
        public const string Example = Levels + "/LevelExample32x18.unity";

        public static string DestinationPath(string name)
        {
            name = (name ?? "").Trim();
            if (Regex.IsMatch(name, @"^\d{1,3}$")) name = "Level" + int.Parse(name).ToString("D2");
            if (!Regex.IsMatch(name, @"^[A-Za-z][A-Za-z0-9_-]{0,63}$"))
                throw new ArgumentException("Use a level number, or a name with letters, numbers, underscores or hyphens. No folders or extensions.");
            if (name.Equals("LevelTemplate32x18", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("The canonical template is reserved.");
            return Levels + "/" + name + ".unity";
        }

        public static string CreateFromTemplate(string name, bool openScene = true)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode && openScene)
                throw new InvalidOperationException("Stop Play Mode before opening a new level.");
            string path = DestinationPath(name);
            // Check the filesystem as well as the database, including case-insensitive collisions.
            if (Directory.GetFiles(Levels).Any(p => string.Equals(p.Replace('\\', '/'), path, StringComparison.OrdinalIgnoreCase)) ||
                File.Exists(path + ".meta") || !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)))
                throw new IOException("A scene or metadata file already exists at " + path + ". Nothing was overwritten.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Template) == null)
                throw new InvalidOperationException("The canonical 32x18 template is missing.");
            if (openScene && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return null;
            if (!AssetDatabase.CopyAsset(Template, path)) throw new IOException("Unity could not copy the template.");
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid) || guid == AssetDatabase.AssetPathToGUID(Template))
                throw new InvalidOperationException("Unity did not assign independent scene metadata. Inspect the copy before using it.");
            if (openScene) EditorSceneManager.OpenScene(path);
            return path;
        }

        [MenuItem("Game Jam/Levels/Create 32x18 Level")]
        private static void CreateWindow() => EditorWindow.GetWindow<CreateLevel32x18Window>(true, "Create 32x18 Level");

        [MenuItem("Game Jam/Levels/Register Current Completed Level In Build")]
        public static void RegisterCurrentLevel()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path == Template || scene.path == Example || !scene.path.StartsWith(Levels + "/", StringComparison.Ordinal) ||
                !scene.GetRootGameObjects().Any(r => r.GetComponent<LevelSetup>() != null && r.GetComponent<RoomGrid32x18>() != null))
                throw new InvalidOperationException("Open a completed production 32x18 level. Templates and review examples are not playable build entries.");
            if (scene.isDirty && !EditorSceneManager.SaveScene(scene)) return;
            EditorBuildSettings.scenes = RegisteredScenes(EditorBuildSettings.scenes, scene.path);
            Debug.Log("Registered " + scene.path + "; MainMenu and existing scene order were preserved.");
        }

        // Pure list operation so registration policy is testable without changing project settings.
        public static EditorBuildSettingsScene[] RegisteredScenes(EditorBuildSettingsScene[] existing, string path)
        {
            if (path == Template || path == Example || !path.StartsWith(Levels + "/", StringComparison.Ordinal) || !path.EndsWith(".unity", StringComparison.Ordinal))
                throw new ArgumentException("Only a completed level inside the production Levels folder can be registered.");
            if (existing.Length == 0 || existing[0].path != "Assets/Game/Scenes/MainMenu.unity" || !existing[0].enabled)
                throw new InvalidOperationException("MainMenu must remain the first enabled build scene. Review Build Profiles before registering.");
            var result = existing.Select(s => new EditorBuildSettingsScene(s.path, s.path == path || s.enabled)).ToList();
            if (!result.Any(s => s.path == path)) result.Add(new EditorBuildSettingsScene(path, true));
            return result.ToArray();
        }

        [MenuItem("Game Jam/Levels/Snap Selected Platform Bounds To Cells")]
        public static void SnapSelected()
        {
            foreach (var selected in Selection.gameObjects)
            {
                var box = RequirePlatform(selected);
                Vector2 min = box.bounds.min, max = box.bounds.max;
                min = new Vector2(Mathf.Round(min.x), Mathf.Round(min.y));
                max = new Vector2(Mathf.Round(max.x), Mathf.Round(max.y));
                if (max.x <= min.x || max.y <= min.y) throw new InvalidOperationException("Snapping would collapse this platform. Resize it to at least one cell first.");
                Resize(selected, Vector2Int.RoundToInt(min), Vector2Int.RoundToInt(max - min));
            }
        }

        [MenuItem("Game Jam/Levels/Resize Selected Platform")]
        private static void ResizeWindow() => EditorWindow.GetWindow<ResizePlatform32x18Window>(true, "Resize Grid Platform");

        private static BoxCollider2D RequirePlatform(GameObject obj)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Platform authoring is available outside Play Mode only.");
            if (obj == null || EditorUtility.IsPersistent(obj) || !obj.scene.IsValid() ||
                !obj.TryGetComponent(out BoxCollider2D box) || box.isTrigger || box.attachedRigidbody != null ||
                obj.transform.lossyScale != Vector3.one || obj.transform.rotation != Quaternion.identity ||
                obj.transform.Find("Floor visuals")?.GetComponent<SpriteRenderer>() == null)
                throw new InvalidOperationException("Select an unrotated, unit-scale static platform root with Floor visuals. Never select the player.");
            return box;
        }

        public static void Resize(GameObject obj, Vector2Int bottomLeft, Vector2Int cells)
        {
            var box = RequirePlatform(obj);
            if (cells.x < 1 || cells.y < 1 || bottomLeft.x < 0 || bottomLeft.y < 0 ||
                bottomLeft.x + cells.x > RoomGrid32x18.Columns || bottomLeft.y + cells.y > RoomGrid32x18.Rows)
                throw new ArgumentException("Static platform bounds must fit whole cells inside X 0..32 and Y 0..18.");
            Undo.RegisterFullObjectHierarchyUndo(obj, "Resize 32x18 platform");
            SetGeometry(obj, bottomLeft, cells);
            EditorSceneManager.MarkSceneDirty(obj.scene);
        }

        // Used by the one-time integration builder and Undo-wrapped designer operations.
        public static void SetGeometry(GameObject obj, Vector2Int bottomLeft, Vector2Int cells)
        {
            var box = obj.GetComponent<BoxCollider2D>();
            box.offset = Vector2.zero;
            box.size = cells;
            obj.transform.position = new Vector3(bottomLeft.x + cells.x / 2f, bottomLeft.y + cells.y / 2f, 0);
            var art = obj.transform.Find("Floor visuals").GetComponent<SpriteRenderer>();
            art.transform.localPosition = Vector3.zero;
            art.size = cells;
            SurfaceReadability.Apply(box);
            BluePlatformAuthoring.Fit(obj);
            foreach (var component in obj.GetComponentsInChildren<Component>())
                if (component is Transform || component is SpriteRenderer || component is BoxCollider2D)
                {
                    EditorUtility.SetDirty(component);
                    if (PrefabUtility.IsPartOfPrefabInstance(component))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                }
        }
    }
}
