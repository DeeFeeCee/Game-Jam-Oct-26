using System;
using GameJam.Resources;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameJam.UI
{
    /// <summary>Menu-to-level navigation. Resource transactions remain owned by RunDashBank.</summary>
    public sealed class NewGameLauncher : MonoBehaviour
    {
        [Tooltip("Temporary approved destination; replace with the official first level when approved.")]
        public string gameplayScene="Assets/Game/Scenes/Levels/Level01.unity";
        public bool IsLoading { get; private set; }
        public string Status { get; private set; }="";
        public event Action StateChanged;

        public bool TryStartNewGame()
        {
            if(IsLoading) return false;
            if(string.IsNullOrWhiteSpace(gameplayScene)||!Application.CanStreamedLevelBeLoaded(gameplayScene))
            {
                Status="Gameplay destination is unavailable.";StateChanged?.Invoke();return false;
            }
            // Lock before events/reset: listeners cannot recursively start a second load.
            IsLoading=true;Status="Starting new run...";StateChanged?.Invoke();
            var bank=RunDashBank.Instance;
            if(bank!=null)
            {
                bank.InitializeNewRun();
                if(bank.IsGameOver||bank.RemainingCharges!=bank.StartingDashCount)
                {
                    IsLoading=false;Status="New run could not be initialized.";StateChanged?.Invoke();return false;
                }
            }
            // A missing bank is intentional: the accepted LevelSetup initializes it in the destination.
            SceneManager.LoadSceneAsync(gameplayScene,LoadSceneMode.Single);
            return true;
        }
    }
}
