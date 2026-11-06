using System.IO;
using GameJam.Levels;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using GameJam.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GameJam.BlueIntegration.Editor
{
    /// <summary>Offscreen authoring previews, without saving temporary UI/text changes to any asset.</summary>
    public static class BlueIntegrationReview
    {
        private static void Save(Camera camera, int width, int height, string path)
        {
            var previousTarget = camera.targetTexture; var previousActive = RenderTexture.active;
            var target = new RenderTexture(width, height, 24) { filterMode = FilterMode.Point };
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget; RenderTexture.active = previousActive;
                target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(image);
            }
        }
        public static void Capture()
        {
            EditorSceneManager.OpenScene(BlueIntegrationBuilder.Scene);
            var setup = Object.FindFirstObjectByType<LevelSetup>();
            var player = (GameObject)PrefabUtility.InstantiatePrefab(setup.playerPrefab);
            player.transform.position = setup.spawn.position;
            var camera = Camera.main;
            var terminal = Object.FindFirstObjectByType<ResearchTerminal>();
            Directory.CreateDirectory("Logs/BlueIntegration/previews");
            Save(camera, 1280, 720, "Logs/BlueIntegration/previews/room.png");
            terminal.overlay.SetActive(true);
            var canvas = terminal.overlay.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera; canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases();
            Save(camera, 1280, 720, "Logs/BlueIntegration/previews/terminal-empty.png");
            // UI fixture text only; it is never saved to the scene, prefab, or build.
            terminal.lore.text = "UI VERIFICATION\n\nEditable TextMeshPro text.\n0123456789\n\nThis preview checks text contrast, wrapping, and margins.";
            Canvas.ForceUpdateCanvases();
            Save(camera, 1280, 720, "Logs/BlueIntegration/previews/terminal-text-check.png");
            Save(camera, 800, 900, "Logs/BlueIntegration/previews/terminal-narrow.png");
            // Discard the preview-only changes explicitly.
            EditorSceneManager.OpenScene(BlueIntegrationBuilder.Scene);
        }
    }
}

