using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace GameJam.SingleScreen.Editor
{
    /// <summary>Explicit Scene view review setup and capture from the Editor's rendered target.</summary>
    public static class RoomReview
    {
        private static SceneView view;
        private static int frames;
        private static bool closeWhenCaptured;
        public static void CaptureAndExit() { closeWhenCaptured=true; Show(); }
        [MenuItem("Game Jam/Single Screen Experiment/Show Editor Grid And Capture")]
        public static void Show()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save current scene edits first.");
            EditorSceneManager.OpenScene(RoomBuilder.Scene);
            view=EditorWindow.GetWindow<SceneView>();view.in2DMode=true;view.orthographic=true;view.sceneLighting=false;view.drawGizmos=true;
            Selection.activeGameObject=UnityEngine.Object.FindFirstObjectByType<RoomGridGuide>().gameObject;
            view.LookAt(new Vector3(16,9,0),Quaternion.identity,20,true,true);
            frames=0;RenderPipelineManager.endCameraRendering+=Rendered;view.Focus();view.Repaint();
        }
        private static void Rendered(ScriptableRenderContext context,Camera camera)
        {
            if(view==null||camera!=view.camera)return;
            if(++frames<3){view.Repaint();return;}
            RenderPipelineManager.endCameraRendering-=Rendered;
            Save();
        }
        private static void Save()
        {
            Directory.CreateDirectory("Logs/SingleScreen");
            var target=view.camera.targetTexture;
            if(target==null)
            {
                File.WriteAllText("Logs/SingleScreen/editor-grid-unavailable.txt","Scene view did not expose a rendered target; capture the configured Scene view manually.");
                if(closeWhenCaptured) EditorApplication.delayCall+=()=>EditorApplication.Exit(0);return;
            }
            var old=RenderTexture.active;RenderTexture.active=target;
            var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();RenderTexture.active=old;
            File.WriteAllBytes("Logs/SingleScreen/editor-grid.png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
            File.WriteAllText("Logs/SingleScreen/editor-camera.txt",view.camera.transform.position+" ortho="+view.camera.orthographicSize+" rotation="+view.camera.transform.rotation);
            if(closeWhenCaptured) EditorApplication.delayCall+=()=>EditorApplication.Exit(0);
        }
    }
}
