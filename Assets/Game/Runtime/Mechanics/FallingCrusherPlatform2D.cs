using System.Collections.Generic;
using UnityEngine;

namespace GameJam.Mechanics
{
    public enum CrusherState { Armed, Warning, Falling, Landed }

    [DefaultExecutionOrder(-120)]
    public sealed class FallingCrusherPlatform2D : PhysicsPlatform2D
    {
        [Min(0)] public float warningDelay = .6f;
        [Min(.01f)] public float fallSpeed = 6;
        [Min(0)] public float maximumDescent = 10;
        private const float LandingSkin = .01f;
        private readonly List<RaycastHit2D> hits = new List<RaycastHit2D>();
        private float remainingWarning, bottomLimit;
        private bool descendingThisStep;
        public CrusherState State { get; private set; }
        protected override void Awake()
        {
            base.Awake();
            bottomLimit = Body.position.y - maximumDescent;
            State = CrusherState.Armed;
        }
        public bool TryActivate(Collider2D other)
        {
            if (State != CrusherState.Armed || HazardPlayer2D.RunEnded || HazardPlayer2D.Find(other) == null) return false;
            State = CrusherState.Warning; remainingWarning = warningDelay;
            return true;
        }
        private void FixedUpdate()
        {
            descendingThisStep = false;
            if (HazardPlayer2D.RunEnded || State == CrusherState.Armed || State == CrusherState.Landed) { Hold(); return; }
            if (State == CrusherState.Warning)
            {
                remainingWarning -= Time.fixedDeltaTime;
                if (remainingWarning > 0) { Hold(); return; }
                State = CrusherState.Falling;
            }
            float distance = Mathf.Min(StepDistance(fallSpeed), Mathf.Max(0, Body.position.y-bottomLimit));
            bool landed = distance <= .00001f;
            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(Physics2D.GetLayerCollisionMask(gameObject.layer));
            hits.Clear(); Shape.Cast(Vector2.down, filter, hits, distance+LandingSkin);
            hits.Sort((a,b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.collider.attachedRigidbody == Body || Physics2D.GetIgnoreCollision(Shape, hit.collider) || hit.normal.y < .5f) continue;
                var player = HazardPlayer2D.Find(hit.collider);
                if (player != null)
                {
                    float playerStepY = hit.rigidbody != null ? hit.rigidbody.linearVelocity.y*Time.fixedDeltaTime : 0;
                    float closingDistance = distance + playerStepY;
                    // A player falling away faster is not an impact with the descending underside.
                    if (IsUnderneath(hit.collider) && closingDistance > 0 && hit.distance <= closingDistance+LandingSkin)
                    { HazardPlayer2D.Fail(hit.collider); Hold(); return; }
                    continue; // A rider on top never becomes a lethal target or landing stop.
                }
                distance = Mathf.Min(distance, Mathf.Max(0, hit.distance-LandingSkin));
                landed = true; break;
            }
            descendingThisStep = distance > .00001f;
            MoveBody(Body.position + Vector2.down*distance);
            if (landed || Body.position.y-distance <= bottomLimit+.00001f) State = CrusherState.Landed;
        }
        private bool IsUnderneath(Collider2D other) => other.bounds.center.y < Shape.bounds.center.y &&
            Mathf.Min(other.bounds.max.x, Shape.bounds.max.x) - Mathf.Max(other.bounds.min.x, Shape.bounds.min.x) > .01f;
        private void OnCollisionEnter2D(Collision2D collision) => CheckUnderside(collision);
        private void OnCollisionStay2D(Collision2D collision) => CheckUnderside(collision);
        private void CheckUnderside(Collision2D collision)
        {
            if (!descendingThisStep || HazardPlayer2D.RunEnded || !IsUnderneath(collision.collider)) return;
            for (int i = 0; i < collision.contactCount; i++)
                if (collision.GetContact(i).normal.y > .5f)
                { if (HazardPlayer2D.Fail(collision.collider)) Hold(); return; }
        }
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1,.6f,0);
            Vector3 start = transform.position; Vector3 end = start + Vector3.down*maximumDescent;
            Gizmos.DrawLine(start,end); Gizmos.DrawWireCube(end,size);
        }
    }
}
