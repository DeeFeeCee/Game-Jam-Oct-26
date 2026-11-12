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
        [SerializeField] private Transform aimingBarrel;
        [SerializeField] private Transform barrelMount;
        [Min(.05f)] public float shotInterval = 1.5f;
        [Min(0)] public float initialDelay = .5f;
        [Min(0)] public float projectileSpeed = 6;
        public bool automaticActivation = true;
        public int ShotsFired { get; private set; }
        public bool IsFiring { get; private set; }
        private float timer;
        private Vector3 barrelRestPosition;
        private Quaternion barrelRestRotation;
        private Vector3 barrelPivot;

        private void Awake()
        {
            timer = Mathf.Max(0, initialDelay);
            IsFiring = automaticActivation;
            if (aimingBarrel == null || barrelMount == null) return;

            barrelRestPosition = aimingBarrel.localPosition;
            barrelRestRotation = aimingBarrel.localRotation;
            // The mount art sits below the joint. Use its X at the barrel's authored height.
            barrelPivot = new Vector3(barrelMount.localPosition.x, barrelRestPosition.y, barrelRestPosition.z);
        }
        public void SetFiring(bool active) => IsFiring = active;
        private void FixedUpdate()
        {
            if (!IsFiring || HazardPlayer2D.RunEnded || projectilePrefab == null) return;
            timer -= Time.fixedDeltaTime;
            if (timer > 0) return;
            timer = Mathf.Max(.05f, shotInterval); // One shot per due tick, never a catch-up burst.
            Vector2 aim = FixedAim;
            Vector3 origin = ShotOrigin(aim);
            if (aimAtPlayer && TryGetTargetDirection(origin, out var targetDirection))
                aim = targetDirection;
            var shot = Instantiate(projectilePrefab, origin, Quaternion.identity);
            SceneManager.MoveGameObjectToScene(shot.gameObject, gameObject.scene);
            shot.gameObject.SetActive(true);
            shot.Launch(aim, projectileSpeed); ShotsFired++;
        }

        private void LateUpdate()
        {
            if (!aimAtPlayer || aimingBarrel == null || barrelMount == null ||
                Time.timeScale == 0f || HazardPlayer2D.RunEnded ||
                !TryGetTargetDirection(ShotOrigin(FixedAim), out var worldAim))
                return;

            // InverseTransformVector accounts for the negative scale on mirrored turret art.
            Vector2 localAim = aimingBarrel.parent.InverseTransformVector(worldAim);
            Vector2 restAim = barrelRestRotation * Vector3.left;
            var turn = Quaternion.Euler(0, 0, Vector2.SignedAngle(restAim, localAim));
            aimingBarrel.localPosition = barrelPivot + turn * (barrelRestPosition - barrelPivot);
            aimingBarrel.localRotation = turn * barrelRestRotation;
        }

        private Vector2 FixedAim =>
            firingDirection.sqrMagnitude > .0001f ? firingDirection.normalized : Vector2.left;

        private Vector3 ShotOrigin(Vector2 fixedAim) =>
            muzzle != null ? muzzle.position : transform.position + (Vector3)(fixedAim * .6f);

        private bool TryGetTargetDirection(Vector2 origin, out Vector2 direction)
        {
            direction = default;
            if (!TryGetPlayerPosition(out var playerPosition)) return false;

            Vector2 offset = playerPosition - origin;
            if (offset.sqrMagnitude <= .0001f) return false;
            direction = offset.normalized;
            return true;
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
