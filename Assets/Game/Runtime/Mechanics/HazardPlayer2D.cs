using GameJam.Movement;
using GameJam.Resources;
using UnityEngine;

namespace GameJam.Mechanics
{
    public static class HazardPlayer2D
    {
        public static PlayerMovement2D Find(Collider2D other)
        {
            if (other == null || other.attachedRigidbody == null) return null;
            var actor = other.attachedRigidbody.GetComponent<PlayerMovement2D>();
            var link = other.attachedRigidbody.GetComponent<PlayerDashBankLink>();
            return actor != null && link != null && link.isActiveAndEnabled ? actor : null;
        }

        public static bool Fail(Collider2D other)
        {
            var actor = Find(other);
            var bank = RunDashBank.Instance;
            return actor != null && actor.isActiveAndEnabled && actor.GetComponent<Rigidbody2D>().simulated &&
                bank != null && bank.TryFailEnvironment();
        }
        public static bool RunEnded => RunDashBank.Instance != null && RunDashBank.Instance.IsGameOver;
    }
}
