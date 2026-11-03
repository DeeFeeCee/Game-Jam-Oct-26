using UnityEngine;

namespace GameJam.Movement
{
    // The query must be side-effect free. Only the approved physics step may react.
    public interface IDashImpactSurface2D
    {
        bool CanReactToDash(PlayerMovement2D player, Vector2 direction);
        bool TryReactToApprovedDash(PlayerMovement2D player, Vector2 direction);
    }
}
