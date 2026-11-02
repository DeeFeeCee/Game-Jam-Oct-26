using UnityEngine;

namespace GameJam.Mechanics
{
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class FallDeathZone2D : MonoBehaviour
    {
        private void Reset() => GetComponent<BoxCollider2D>().isTrigger = true;
        private void Awake() => GetComponent<BoxCollider2D>().isTrigger = true;
        private void OnTriggerEnter2D(Collider2D other) => HazardPlayer2D.Fail(other);
        private void OnTriggerStay2D(Collider2D other) => HazardPlayer2D.Fail(other);
        private void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider2D>(); if (box == null) return;
            Matrix4x4 previous = Gizmos.matrix; Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1, .25f, .25f, .15f); Gizmos.DrawCube(box.offset, box.size);
            Gizmos.color = new Color(1, .25f, .25f, .8f); Gizmos.DrawWireCube(box.offset, box.size);
            Gizmos.matrix = previous;
        }
    }
}
