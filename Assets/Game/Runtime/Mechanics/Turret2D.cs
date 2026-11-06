using GameJam.Levels;
using GameJam.Movement;
using GameJam.Resources;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameJam.Mechanics
{
    public sealed class Turret2D : MonoBehaviour
    {
        public TurretProjectile2D projectilePrefab;
        public Transform muzzle;
        public Vector2 firingDirection = Vector2.left;
        [Tooltip("Aim each new shot at this level's active player. Uses Firing Direction if there is no valid target.")]
        public bool aimAtPlayer;
        [Min(.05f)] public float shotInterval = 1.5f;
        [Min(0)] public float initialDelay = .5f;
        [Min(0)] public float projectileSpeed = 6;
        public bool automaticActivation = true;
        public int ShotsFired { get; private set; }
        public bool IsFiring { get; private set; }
        private float timer;
        private void Awake() { timer = Mathf.Max(0, initialDelay); IsFiring = automaticActivation; }
        public void SetFiring(bool active) => IsFiring = active;
        private void FixedUpdate()
        {
            if (!IsFiring || HazardPlayer2D.RunEnded || projectilePrefab == null) return;
            timer -= Time.fixedDeltaTime;
            if (timer > 0) return;
            timer = Mathf.Max(.05f, shotInterval); // One shot per due tick, never a catch-up burst.
            Vector2 aim = firingDirection.sqrMagnitude > .0001f ? firingDirection.normalized : Vector2.left;
            Vector3 origin = muzzle != null ? muzzle.position : transform.position + (Vector3)(aim*.6f);
            if (aimAtPlayer && TryGetPlayerPosition(out var playerPosition))
            {
                Vector2 offset = playerPosition - (Vector2)origin;
                if (offset.sqrMagnitude > .0001f) aim = offset.normalized;
            }
            var shot = Instantiate(projectilePrefab, origin, Quaternion.identity);
            SceneManager.MoveGameObjectToScene(shot.gameObject, gameObject.scene);
            shot.gameObject.SetActive(true);
            shot.Launch(aim, projectileSpeed); ShotsFired++;
        }

        private bool TryGetPlayerPosition(out Vector2 position)
        {
            position = default;
            var bank = RunDashBank.Instance;
            if (bank == null || !bank.isActiveAndEnabled) return false;

            var scene = gameObject.scene;
            foreach (var level in FindObjectsByType<LevelSetup>(FindObjectsSortMode.None))
            {
                var player = level.Player;
                if (!level.isActiveAndEnabled || level.gameObject.scene != scene || player == null ||
                    !player.activeInHierarchy || player.scene != scene) continue;
                if (!player.TryGetComponent(out PlayerDashBankLink link) || !link.isActiveAndEnabled ||
                    !player.TryGetComponent(out PlayerMovement2D movement) || !movement.isActiveAndEnabled ||
                    !player.TryGetComponent(out Rigidbody2D body) || !body.simulated) continue;

                position = body.position;
                return true;
            }
            return false;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Vector3 origin = muzzle != null ? muzzle.position : transform.position;
            Gizmos.DrawLine(origin, origin + (Vector3)firingDirection.normalized*2);
        }
    }
}
