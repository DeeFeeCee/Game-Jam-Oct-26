using GameJam.Levels;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameJam.UI
{
    /// <summary>One scene-owned presenter for loaded gameplay using the accepted LevelSetup.</summary>
    public static class GameOverSceneBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSubscriptions()=>SceneManager.sceneLoaded-=OnSceneLoaded;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Begin()
        {
            SceneManager.sceneLoaded-=OnSceneLoaded;SceneManager.sceneLoaded+=OnSceneLoaded;
            Install(SceneManager.GetActiveScene());
        }
        private static void OnSceneLoaded(Scene scene,LoadSceneMode mode)=>Install(scene);
        public static void Install(Scene scene)
        {
            if(!Application.isPlaying||!scene.IsValid()||!scene.isLoaded)return;
            bool gameplay=false;
            foreach(var root in scene.GetRootGameObjects())if(root.GetComponentInChildren<LevelSetup>()!=null){gameplay=true;break;}
            if(!gameplay||Object.FindFirstObjectByType<GameOverController>(FindObjectsInactive.Include)!=null)return;
            var prefab=UnityEngine.Resources.Load<GameObject>("UI/GameOver");
            if(prefab==null){Debug.LogError("Game Over prefab missing from Resources/UI/GameOver.");return;}
            var instance=Object.Instantiate(prefab);SceneManager.MoveGameObjectToScene(instance,scene);
        }
    }
}
