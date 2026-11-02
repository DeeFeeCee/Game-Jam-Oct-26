using GameJam.Movement;
using UnityEngine;

namespace GameJam.Mechanics
{
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class BreakableWall2D : MonoBehaviour, IDashImpactSurface2D
    {
        public bool IsBroken { get; private set; }
        public bool CanReactToDash(PlayerMovement2D player, Vector2 direction) =>
            !IsBroken && isActiveAndEnabled && !HazardPlayer2D.RunEnded &&
            player != null && HazardPlayer2D.Find(player.GetComponent<Collider2D>()) == player;

        public bool TryReactToApprovedDash(PlayerMovement2D player, Vector2 direction)
        {
            if (!CanReactToDash(player, direction) || !player.IsDashing) return false;
            IsBroken = true;
            GetComponent<BoxCollider2D>().enabled = false;
            gameObject.SetActive(false);
            Destroy(gameObject);
            return true;
        }
    }
}
