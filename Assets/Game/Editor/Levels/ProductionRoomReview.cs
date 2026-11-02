using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace GameJam.LevelAuthoring.Editor
{
    /// <summary>Explicit Editor evidence capture. The room guide remains visible for designers.</summary>
    public static class ProductionRoomReview
    {
        private static SceneView view;
        private static int renderedFrames;

        [MenuItem("Game Jam/Levels/Show Production Room Grid")]
        public static void Show()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(LevelAuthoring32x18.Level01);
            view = EditorWindow.GetWindow<SceneView>();
            view.in2DMode = true; view.orthographic = true; view.sceneLighting = false; view.drawGizmos = true;
            view.LookAt(new Vector3(16, 9, 0), Quaternion.identity, 22, true, true);
            Selection.activeGameObject = UnityEngine.Object.FindFirstObjectByType<GameJam.Levels.RoomGrid32x18>().gameObject;
            renderedFrames = 0;
            RenderPipelineManager.endCameraRendering -= Rendered;
            RenderPipelineManager.endCameraRendering += Rendered;
            view.Focus(); view.Repaint();
            Directory.CreateDirectory("Logs/Production32x18");
            File.WriteAllLines("Logs/Production32x18/editor-hierarchy.txt", UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r => Lines(r.transform, "")));
        }

        private static System.Collections.Generic.IEnumerable<string> Lines(Transform transform, string indent)
        {
            yield return indent + transform.name;
            foreach (Transform child in transform)
                foreach (string line in Lines(child, indent + "  ")) yield return line;
        }

        private static void Rendered(ScriptableRenderContext context, Camera camera)
        {
            if (view == null || view.camera != camera) return;
            if (++renderedFrames < 3) { view.Repaint(); return; }
            RenderPipelineManager.endCameraRendering -= Rendered;
            var target = view.camera.targetTexture;
            if (target == null) return;
            var previous = RenderTexture.active;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            try
            {
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
                File.WriteAllBytes("Logs/Production32x18/editor-grid.png", image.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(image); }
        }
    }
}
