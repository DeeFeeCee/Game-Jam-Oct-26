using GameJam.Movement;
using UnityEngine;

namespace GameJam.Levels
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class LevelExit : MonoBehaviour
    {
        public LevelTransition transition;
        private void Awake() => GetComponent<Collider2D>().isTrigger = true;
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (transition != null && other.attachedRigidbody != null)
                transition.TryExit(other.attachedRigidbody.GetComponent<PlayerMovement2D>());
        }
    }
}
