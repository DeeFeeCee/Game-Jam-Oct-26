using UnityEngine;

namespace GameJam.Movement
{
    /// <summary>The support publishes its planned physics velocity; only the player motor moves the player.</summary>
    public interface IPlayerSupport2D { Vector2 SupportVelocity { get; } }
}
