using GameJam.Movement;
using GameJam.Resources;
using UnityEngine;

namespace GameJam.Levels
{
    [DefaultExecutionOrder(-200)]
    public sealed class LevelSetup : MonoBehaviour
    {
        public GameObject playerPrefab;
        public RunDashBank bankPrefab;
        public Transform spawn;
        public LevelCamera2D levelCamera;
        public GameObject Player { get; private set; }
        private void Awake()
        {
            if (playerPrefab == null || bankPrefab == null || spawn == null)
            { Debug.LogError("LevelSetup needs a player, bank and spawn point.", this); enabled = false; return; }
            if (RunDashBank.Instance == null) Instantiate(bankPrefab); // Dedicated root, not a child of this level.
            Player = Instantiate(playerPrefab, spawn.position, Quaternion.identity);
            if (levelCamera != null) levelCamera.target = Player.transform;
        }

        private void Start()
        {
            var bank = RunDashBank.Instance;
            if (bank == null || Player == null) return;
            if (bank.IsRetryEntry(gameObject.scene) && Player.TryGetComponent(out PlayerMovementInput2D input))
                input.RequireNeutralInput();
            bank.RecordLevelEntry(gameObject.scene);
        }
    }
}
