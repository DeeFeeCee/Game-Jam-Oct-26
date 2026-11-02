using UnityEngine;

namespace GameJam.Mechanics
{
    [DefaultExecutionOrder(-120)]
    public sealed class MovingPlatform2D : PhysicsPlatform2D
    {
        [Tooltip("Assign stationary endpoint objects, outside the moving body hierarchy.")]
        public Transform pointA, pointB;
        [Min(0)] public float speed = 1.5f;
        [Min(0)] public float endpointDelay;
        public bool startAtB;
        private Vector2 a, b;
        private bool towardB;
        private float waiting;
        public Vector2 EndpointA => a;
        public Vector2 EndpointB => b;
        protected override void Awake()
        {
            base.Awake();
            if (pointA == null || pointB == null) { Debug.LogError("MovingPlatform2D needs two stationary endpoints.", this); enabled = false; return; }
            a = pointA.position; b = pointB.position; towardB = !startAtB;
            Body.position = startAtB ? b : a; // Initialization only; ordinary movement uses MovePosition.
        }
        private void FixedUpdate()
        {
            if (HazardPlayer2D.RunEnded) { Hold(); return; }
            if (waiting > 0) { waiting = Mathf.Max(0, waiting-Time.fixedDeltaTime); Hold(); return; }
            Vector2 target = towardB ? b : a;
            Vector2 next = Vector2.MoveTowards(Body.position, target, StepDistance(speed));
            MoveBody(next);
            if ((next-target).sqrMagnitude < .0000001f) { towardB = !towardB; waiting = endpointDelay; }
        }
        private void OnDrawGizmos()
        {
            if (pointA == null || pointB == null) return;
            Gizmos.color = Color.cyan; Gizmos.DrawLine(pointA.position, pointB.position);
            Gizmos.DrawWireSphere(pointA.position, .15f); Gizmos.DrawWireSphere(pointB.position, .15f);
        }
    }
}
