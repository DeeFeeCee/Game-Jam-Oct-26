using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameJam.Resources
{
    /// <summary>One in-memory balance for the run. Scene changes never initialize a new run.</summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class RunDashBank : MonoBehaviour
    {
        [Tooltip("Provisional test value, not an approved final game balance.")]
        [SerializeField, Min(1)] private int startingDashCount = 5;
        [SerializeField, HideInInspector] private int remainingCharges;
        [SerializeField, HideInInspector] private bool isGameOver;
        [SerializeField, HideInInspector] private RunFailureCause failureCause;
        private bool notifying;
        private int checkpointSceneHandle;
        private bool retryRequested;

        public static RunDashBank Instance { get; private set; }
        public int StartingDashCount => startingDashCount;
        public int RemainingCharges => remainingCharges;
        public bool IsGameOver => isGameOver;
        public RunFailureCause FailureCause => failureCause;
        public string CheckpointScenePath { get; private set; } = "";
        public int CheckpointCharges { get; private set; }

        public event Action<int> ChargesChanged;
        public event Action GameOver;
        public event Action NewRunStarted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                enabled = false;
                Destroy(this); // A duplicate never initializes or replaces the authoritative balance.
                return;
            }
            if (transform.parent != null)
            {
                Debug.LogError("Place RunDashBank on a dedicated root GameObject.", this);
                enabled = false;
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeNewRun();
        }

        /// <summary>Explicit resource/run-state reset; does not reload levels or restore checkpoints.</summary>
        [ContextMenu("Initialize New Run (Prototype)")]
        public void InitializeNewRun()
        {
            if (!Application.isPlaying || Instance != this || notifying || retryRequested)
                return;
            CheckpointScenePath = "";
            CheckpointCharges = 0;
            checkpointSceneHandle = 0;
            remainingCharges = Mathf.Max(1, startingDashCount);
            isGameOver = false;
            failureCause = RunFailureCause.None;
            notifying = true;
            try
            {
                NewRunStarted?.Invoke();
                ChargesChanged?.Invoke(remainingCharges);
            }
            finally { notifying = false; }
        }

        public bool IsRetryEntry(Scene scene) => retryRequested && scene.path == CheckpointScenePath &&
            scene.handle != checkpointSceneHandle;

        /// <summary>Called by the production level setup after scene activation, before the first gameplay update.</summary>
        public void RecordLevelEntry(Scene scene)
        {
            if (Instance != this || !isActiveAndEnabled || notifying ||
                scene.handle != SceneManager.GetActiveScene().handle || !IsProductionLevel(scene.path))
                return;

            if (IsRetryEntry(scene))
            {
                retryRequested = false;
                checkpointSceneHandle = scene.handle;
                remainingCharges = CheckpointCharges;
                isGameOver = false;
                failureCause = RunFailureCause.None;
                notifying = true;
                try { ChargesChanged?.Invoke(remainingCharges); }
                finally { notifying = false; }
            }
            else if (!retryRequested && !isGameOver && scene.handle != checkpointSceneHandle)
            {
                CheckpointScenePath = scene.path;
                CheckpointCharges = remainingCharges;
                checkpointSceneHandle = scene.handle;
            }
        }

        /// <summary>Accept the reload while the failed player and balance are still frozen.</summary>
        public bool TryRetryLevel(Scene scene)
        {
            if (Instance != this || !isActiveAndEnabled || notifying || !isGameOver || retryRequested ||
                CheckpointCharges <= 0 || scene.handle != checkpointSceneHandle ||
                scene.path != CheckpointScenePath || scene.handle != SceneManager.GetActiveScene().handle ||
                !IsProductionLevel(CheckpointScenePath))
                return false;

            retryRequested = true;
            try
            {
                if (SceneManager.LoadSceneAsync(CheckpointScenePath, LoadSceneMode.Single) != null)
                    return true;
            }
            catch (ArgumentException) { /* The scene became unavailable after validation. */ }
            retryRequested = false;
            return false;
        }

        private static bool IsProductionLevel(string path)
        {
            const string prefix = "Assets/Game/Scenes/Levels/Level";
            return path != null && path.Length == prefix.Length + 8 &&
                path.StartsWith(prefix, StringComparison.Ordinal) &&
                path[prefix.Length] >= '0' && path[prefix.Length] <= '9' &&
                path[prefix.Length + 1] >= '0' && path[prefix.Length + 1] <= '9' &&
                path.EndsWith(".unity", StringComparison.Ordinal) &&
                SceneUtility.GetBuildIndexByScenePath(path) >= 0 && Application.CanStreamedLevelBeLoaded(path);
        }

        /// <summary>One eligible request spends one charge. The final spend is fatal, not movement approval.</summary>
        public bool TrySpendDash()
        {
            if (Instance != this || !isActiveAndEnabled || isGameOver || remainingCharges <= 0 || notifying)
                return false;
            remainingCharges--;
            isGameOver = remainingCharges == 0;
            if (isGameOver) failureCause = RunFailureCause.MobilityExhaustion;
            notifying = true; // Event listeners cannot recursively spend/reset this transaction.
            try
            {
                // State is committed before notification, so linked actors freeze synchronously at zero.
                ChargesChanged?.Invoke(remainingCharges);
                if (isGameOver)
                    GameOver?.Invoke();
            }
            finally { notifying = false; }
            return !isGameOver;
        }

        /// <summary>Fail the authoritative run without changing or impersonating a charge transaction.</summary>
        public bool TryFailEnvironment()
        {
            if (Instance != this || !isActiveAndEnabled || isGameOver || notifying) return false;
            isGameOver = true;
            failureCause = RunFailureCause.Environmental;
            notifying = true;
            try { GameOver?.Invoke(); }
            finally { notifying = false; }
            return true;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            ChargesChanged = null;
            GameOver = null;
            NewRunStarted = null;
        }

        private void OnValidate() => startingDashCount = Mathf.Max(1, startingDashCount);
    }
}
