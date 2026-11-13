using GameJam.Movement;
using UnityEngine;

namespace GameJam.Levels
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class LevelExit : MonoBehaviour
    {
        private const float DoorwayTriggerWidth = .3f;
        public LevelTransition transition;

        private void Awake()
        {
            var collider = GetComponent<Collider2D>();
            collider.isTrigger = true;
            if (collider is BoxCollider2D doorway && transform.Find("Environment exit art") != null)
            {
                // Authored door art is centered on the trigger. Keep its height and use a narrow entrance.
                var size = doorway.size;
                float scale = Mathf.Abs(transform.lossyScale.x);
                if (scale > 0f) size.x = Mathf.Min(size.x, DoorwayTriggerWidth / scale);
                doorway.size = size;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (transition != null && other.attachedRigidbody != null)
                transition.TryExit(other.attachedRigidbody.GetComponent<PlayerMovement2D>());
        }
    }
}
