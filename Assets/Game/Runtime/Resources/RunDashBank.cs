using System;
using UnityEngine;

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
        private bool notifying;

        public static RunDashBank Instance { get; private set; }
        public int StartingDashCount => startingDashCount;
        public int RemainingCharges => remainingCharges;
        public bool IsGameOver => isGameOver;

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
            if (!Application.isPlaying || Instance != this || notifying)
                return;
            remainingCharges = Mathf.Max(1, startingDashCount);
            isGameOver = false;
            notifying = true;
            try
            {
                NewRunStarted?.Invoke();
                ChargesChanged?.Invoke(remainingCharges);
            }
            finally { notifying = false; }
        }

        /// <summary>One eligible request spends one charge. The final spend is fatal, not movement approval.</summary>
        public bool TrySpendDash()
        {
            if (Instance != this || !isActiveAndEnabled || isGameOver || remainingCharges <= 0 || notifying)
                return false;
            remainingCharges--;
            isGameOver = remainingCharges == 0;
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
