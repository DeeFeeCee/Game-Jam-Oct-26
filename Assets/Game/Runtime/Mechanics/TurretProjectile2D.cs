using System.Collections.Generic;
using UnityEngine;

namespace GameJam.Mechanics
{
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public sealed class TurretProjectile2D : MonoBehaviour
    {
        [Min(.01f)] public float lifetime = 5;
        private Rigidbody2D body;
        private Collider2D shape;
        private Vector2 direction;
        private float speed, remaining;
        private readonly List<RaycastHit2D> hits = new List<RaycastHit2D>(8);
        public bool IsConsumed { get; private set; }
        private void Awake()
        {
            body = GetComponent<Rigidbody2D>(); shape = GetComponent<Collider2D>();
            body.bodyType = RigidbodyType2D.Kinematic; body.gravityScale = 0;
            shape.isTrigger = true; remaining = lifetime;
        }
        public void Launch(Vector2 aim, float unitsPerSecond)
        {
            direction = aim.sqrMagnitude > .0001f ? aim.normalized : Vector2.right;
            speed = Mathf.Max(0, unitsPerSecond); remaining = Mathf.Max(.01f, lifetime);
        }
        private void FixedUpdate()
        {
            if (IsConsumed) return;
            if (HazardPlayer2D.RunEnded || (remaining -= Time.fixedDeltaTime) <= 0) { Consume(); return; }
            float distance = Mathf.Min(speed, Physics2D.maxTranslationSpeed) * Time.fixedDeltaTime;
            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(Physics2D.GetLayerCollisionMask(gameObject.layer));
            hits.Clear(); shape.Cast(direction, filter, hits, distance);
            hits.Sort((a,b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.collider == null || hit.collider.attachedRigidbody == body || Physics2D.GetIgnoreCollision(shape, hit.collider)) continue;
                HazardPlayer2D.Fail(hit.collider); Consume(); return; // First solid blocks every collider behind it.
            }
            body.MovePosition(body.position + direction * distance);
        }
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (IsConsumed || other.isTrigger) return;
            HazardPlayer2D.Fail(other); Consume();
        }
        private void Consume()
        {
            if (IsConsumed) return;
            IsConsumed = true; shape.enabled = false; body.linearVelocity = Vector2.zero; body.simulated = false;
            gameObject.SetActive(false); Destroy(gameObject);
        }
    }
}
