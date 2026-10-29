using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameJam.Foundation.Editor
{
    /// <summary>Bakes static foreground trim without adding a runtime update or changing physics.</summary>
    public static class SurfaceReadability
    {
        public const string FrameName = "Gameplay surface frame";
        private const float Edge = .05f; // One pixel at the production world's 20 PPU.
        [MenuItem("Game Jam/Refresh Selected Surface Borders")]
        public static void RefreshSelected()
        {
            foreach (GameObject selected in Selection.gameObjects)
                if (selected.TryGetComponent(out BoxCollider2D solid) && !solid.isTrigger)
                {
                    Undo.RegisterFullObjectHierarchyUndo(selected, "Refresh surface borders");
                    Apply(solid);
                    EditorSceneManager.MarkSceneDirty(selected.scene);
                }
        }

        public static void Apply(BoxCollider2D solid)
        {
            if (solid.isTrigger || solid.attachedRigidbody != null)
                throw new InvalidOperationException("Surface trim is for explicitly selected static solids.");
            var art = solid.transform.Find("Floor visuals").GetComponent<SpriteRenderer>();
            var square = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Game/Tests/Fixtures/Movement/Art/PlaceholderSquare.png");
            var frame = solid.transform.Find(FrameName);
            if (frame == null)
            {
                frame = new GameObject(FrameName).transform;
                frame.SetParent(solid.transform, false);
            }
            Vector2 size = solid.size;
            float edge = Mathf.Min(Edge, size.x / 2f, size.y / 2f);
            Strip("Backing", Vector2.zero, size, new Color(.035f,.065f,.095f), -1);
            Strip("Top", new Vector2(0, (size.y-edge)/2), new Vector2(size.x,edge), new Color(.7f,.9f,.95f), 2);
            Strip("Bottom", new Vector2(0, -(size.y-edge)/2), new Vector2(size.x,edge), new Color(.2f,.4f,.5f), 1);
            Strip("Left", new Vector2(-(size.x-edge)/2,0), new Vector2(edge,size.y), new Color(.3f,.56f,.65f), 1);
            Strip("Right", new Vector2((size.x-edge)/2,0), new Vector2(edge,size.y), new Color(.3f,.56f,.65f), 1);

            void Strip(string name, Vector2 center, Vector2 dimensions, Color color, int order)
            {
                var child = frame.Find(name);
                if (child == null) { child = new GameObject(name, typeof(SpriteRenderer)).transform; child.SetParent(frame,false); }
                child.localPosition = (Vector3)(solid.offset + center);
                child.localScale = new Vector3(dimensions.x / square.bounds.size.x, dimensions.y / square.bounds.size.y, 1);
                var renderer = child.GetComponent<SpriteRenderer>();
                renderer.sprite = square; renderer.sharedMaterial = art.sharedMaterial;
                renderer.color = color; renderer.sortingLayerID = art.sortingLayerID;
                renderer.sortingOrder = art.sortingOrder + order;
                EditorUtility.SetDirty(renderer);
            }
        }

        public static void ApplyProduction()
        {
            const string prefabPath = "Assets/Game/Prefabs/IndustrialPlatform.prefab";
            var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
            try { Apply(prefab.GetComponent<BoxCollider2D>()); PrefabUtility.SaveAsPrefabAsset(prefab,prefabPath); }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            foreach (string path in new[] { FoundationBuilder.Level, FoundationBuilder.Destination })
            {
                Scene scene = SceneManager.GetSceneByPath(path);
                bool alreadyOpen = scene.IsValid() && scene.isLoaded;
                if (alreadyOpen && scene.isDirty) throw new InvalidOperationException("Save scene edits first: " + path);
                if (!alreadyOpen) scene = EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                foreach (var solid in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BoxCollider2D>()))
                    if (!solid.isTrigger && solid.attachedRigidbody == null && solid.transform.Find("Floor visuals") != null)
                        Apply(solid);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                if (!alreadyOpen) EditorSceneManager.CloseScene(scene,true);
            }
        }
    }
}
