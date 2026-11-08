using System.Collections.Generic;
using UnityEngine;

namespace GameJam.Movement
{
    /// <summary>Basic platform movement. Input is supplied separately from the physics tick.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public sealed partial class PlayerMovement2D : MonoBehaviour
    {
        [Header("Provisional movement tuning")]
        [SerializeField, Min(0f)] private float moveSpeed = 6f;
        [SerializeField, Min(0f)] private float jumpSpeed = 10f;

        [Header("Jump forgiveness (provisional, in seconds)")]
        [SerializeField, Min(0f)] private float coyoteTime = 0.10f;
        [SerializeField, Min(0f)] private float jumpBufferTime = 0.08f;

        [Header("Ground detection")]
        [SerializeField] private LayerMask groundLayers = ~0;
        [SerializeField, Min(0.001f)] private float groundProbeDistance = 0.06f;
        [Tooltip("Inset on each side of the foot query, to avoid counting adjacent walls.")]
        [SerializeField, Min(0f)] private float groundProbeInset = 0.05f;
        [SerializeField, Range(0f, 80f)] private float maximumGroundAngle = 45f;

        private readonly List<RaycastHit2D> groundHits = new List<RaycastHit2D>(8);
        private Rigidbody2D body;
        private BoxCollider2D bodyCollider;
        private float horizontalInput;
        private double coyoteUntil = double.NegativeInfinity;
        private double jumpBufferUntil = double.NegativeInfinity;
        private bool jumpRequestedWithoutBuffer;

        public bool IsGrounded { get; private set; }
        public float MoveSpeed => moveSpeed;
        public float JumpSpeed => jumpSpeed;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<BoxCollider2D>();
        }

        private void OnEnable()
        {
            ClearInput();
            coyoteUntil = double.NegativeInfinity;
            ResetDashState();
            ClearSupport();
        }

        private void OnDisable()
        {
            StopDash();
            DashApproval = null;
            ClearInput();
            coyoteUntil = double.NegativeInfinity;
            IsGrounded = false;
            ClearSupport();
            if (body != null)
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
        }

        public void SetHorizontalInput(float value)
        {
            horizontalInput = Mathf.Clamp(value, -1f, 1f);
            if (Mathf.Abs(horizontalInput) > dashDirectionDeadZone)
                horizontalFacing = horizontalInput > 0f ? 1f : -1f;
        }

        public void RequestJump()
        {
            if (!isActiveAndEnabled || IsDashing || Time.timeScale == 0f)
                return;
            jumpRequestedWithoutBuffer = jumpBufferTime == 0f;
            jumpBufferUntil = jumpRequestedWithoutBuffer
                ? double.NegativeInfinity
                : Time.timeAsDouble + jumpBufferTime;
        }

        public void ClearInput(bool preserveBufferedJump = false)
        {
            horizontalInput = 0f;
            dashRequested = false;
            if (!preserveBufferedJump)
                ClearJumpBuffer();
        }

        private void ClearJumpBuffer()
        {
            jumpRequestedWithoutBuffer = false;
            jumpBufferUntil = double.NegativeInfinity;
        }

        private void FixedUpdate()
        {
            if (StepDash())
                return;

            // Both clocks use scaled game time, so Pause cannot age either window.
            double now = Time.fixedTimeAsDouble;
            if (IsGrounded)
                coyoteUntil = now + coyoteTime;
            bool hasJumpRequest = jumpRequestedWithoutBuffer || now <= jumpBufferUntil;
            jumpRequestedWithoutBuffer = false;

            Vector2 velocity = body.linearVelocity;
            velocity.x = horizontalInput * moveSpeed;

            if (hasJumpRequest && (IsGrounded || now <= coyoteUntil))
            {
                velocity.y = jumpSpeed;
                IsGrounded = false;
                coyoteUntil = double.NegativeInfinity;
                ClearJumpBuffer();
                ClearSupport(); // Jump/dash departure does not inherit platform momentum.
            }
            else ApplySupport(ref velocity);

            body.linearVelocity = velocity;
        }

        private bool CheckGrounded()
        {
            departedSupportVelocity = Vector2.zero;
            if (!bodyCollider.enabled) { DropSupport(); return false; }

            Bounds bounds = bodyCollider.bounds;
            const float probeHeight = 0.02f;
            Vector2 size = new Vector2(Mathf.Max(0.01f, bounds.size.x - groundProbeInset * 2f), probeHeight);
            Vector2 origin = new Vector2(bounds.center.x, bounds.min.y + probeHeight);
            ContactFilter2D filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(groundLayers);
            groundHits.Clear();
            Physics2D.BoxCast(origin, size, 0f, Vector2.down, filter, groundHits,
                groundProbeDistance + probeHeight);

            float minimumNormalY = Mathf.Cos(maximumGroundAngle * Mathf.Deg2Rad);
            foreach (RaycastHit2D hit in groundHits)
            {
                if (hit.collider.attachedRigidbody == body)
                    continue;
                if (hit.normal.y >= minimumNormalY && AcceptGroundSupport(hit))
                    return true;
            }
            DropSupport();
            return false;
        }

        private void OnValidate()
        {
            moveSpeed = Mathf.Max(0f, moveSpeed);
            jumpSpeed = Mathf.Max(0f, jumpSpeed);
            coyoteTime = Mathf.Max(0f, coyoteTime);
            jumpBufferTime = Mathf.Max(0f, jumpBufferTime);
            groundProbeDistance = Mathf.Max(0.001f, groundProbeDistance);
            groundProbeInset = Mathf.Max(0f, groundProbeInset);
            ValidateDashSettings();
        }
    }
}
