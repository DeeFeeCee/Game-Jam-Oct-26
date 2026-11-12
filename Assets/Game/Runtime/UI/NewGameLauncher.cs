using System;
using GameJam.Levels;
using GameJam.Resources;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameJam.UI
{
    /// <summary>Starts a run and loads its configured entry scene.</summary>
    public sealed class NewGameLauncher : MonoBehaviour
    {
        [Tooltip("First scene for a genuinely new run.")]
        public string gameplayScene="Assets/Game/Scenes/Levels/Level01.unity";
        [Tooltip("Assign on the Main Menu to start a new bank before entering the prologue.")]
        public RunDashBank initialBankPrefab;
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
            if(bank!=null) bank.InitializeNewRun();
            else if(initialBankPrefab!=null) bank=Instantiate(initialBankPrefab); // Awake initializes the new bank once.
            if(bank!=null&&(bank.IsGameOver||bank.RemainingCharges!=bank.StartingDashCount))
            {
                IsLoading=false;Status="New run could not be initialized.";StateChanged?.Invoke();return false;
            }
            // Direct gameplay launchers can still let LevelSetup supply a missing bank.
            if (SceneManager.LoadSceneAsync(gameplayScene, LoadSceneMode.Single) != null)
                RunTimer.Instance?.ArmNewRun();
            return true;
        }
    }
}
