using GameJam.Resources;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameJam.Resources.PrototypeEditor
{
    // Editor-only verification controls. These are not a shipped menu or level-transition system.
    public static class DashBankVerificationMenu
    {
        [MenuItem("Game Jam/Pass 1C/Load Verification Scene A")]
        private static void LoadA() => Load("DashBankTestA");

        [MenuItem("Game Jam/Pass 1C/Load Verification Scene B")]
        private static void LoadB() => Load("DashBankTestB");

        private static void Load(string scene)
        {
            if (!EditorApplication.isPlaying) return;
            EditorSceneManager.LoadSceneInPlayMode("Assets/Prototypes/Pass1C/Scenes/" + scene + ".unity",
                new LoadSceneParameters(LoadSceneMode.Single));
        }

        [MenuItem("Game Jam/Pass 1C/Log Bank State")]
        private static void LogState()
        {
            RunDashBank bank = RunDashBank.Instance;
            Debug.Log(bank == null ? "Pass 1C: no active run bank." :
                "Pass 1C: remaining=" + bank.RemainingCharges + ", gameOver=" + bank.IsGameOver);
        }

        [MenuItem("Game Jam/Pass 1C/Initialize New Run (Resource Reset Only)")]
        private static void NewRun()
        {
            if (EditorApplication.isPlaying && RunDashBank.Instance != null)
                RunDashBank.Instance.InitializeNewRun();
        }
    }
}
