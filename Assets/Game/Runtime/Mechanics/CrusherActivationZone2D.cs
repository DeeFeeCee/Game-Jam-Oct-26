using UnityEngine;

namespace GameJam.Mechanics
{
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class CrusherActivationZone2D : MonoBehaviour
    {
        public FallingCrusherPlatform2D crusher;
        private void Awake() => GetComponent<BoxCollider2D>().isTrigger = true;
        private void Reset() => GetComponent<BoxCollider2D>().isTrigger = true;
        private void OnTriggerEnter2D(Collider2D other) { if (crusher != null) crusher.TryActivate(other); }
        private void OnTriggerStay2D(Collider2D other) { if (crusher != null) crusher.TryActivate(other); }
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1,.6f,0,.6f); Gizmos.matrix = transform.localToWorldMatrix;
            var box = GetComponent<BoxCollider2D>(); Gizmos.DrawWireCube(box.offset,box.size);
        }
    }
}
