using GameJam.Levels;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameJam.UI
{
    /// <summary>Scene-owned pause presentation for the accepted single-scene level framework.</summary>
    public static class PauseSceneBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSubscriptions()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Time.timeScale = 1f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Begin()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Install(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Install(scene);

        public static void Install(Scene scene)
        {
            if (!Application.isPlaying || !scene.IsValid() || !scene.isLoaded) return;
            bool gameplay = false;
            foreach (var root in scene.GetRootGameObjects())
                if (root.GetComponentInChildren<LevelSetup>() != null) { gameplay = true; break; }
            if (!gameplay || Object.FindFirstObjectByType<PauseController>(FindObjectsInactive.Include) != null) return;
            var prefab = UnityEngine.Resources.Load<GameObject>("UI/Pause");
            if (prefab == null) { Debug.LogError("Pause prefab missing from Resources/UI/Pause."); return; }
            var instance = Object.Instantiate(prefab);
            SceneManager.MoveGameObjectToScene(instance, scene);
        }
    }
}
