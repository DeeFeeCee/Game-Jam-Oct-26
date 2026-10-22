using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameJam.Movement
{
    // Dash physics is kept beside the walking controller without adding a second body owner.
    public sealed partial class PlayerMovement2D
    {
        [Header("Pass 1B: provisional dash (off on the Pass 1A prefab)")]
        [SerializeField] private bool enableDash;
        [SerializeField, Min(0.01f)] private float dashSpeed = 18f;
        [SerializeField, Min(0.01f)] private float dashDuration = 0.16f;
        [SerializeField, Range(0f, 0.9f)] private float dashDirectionDeadZone = 0.2f;
        [SerializeField, Range(0.001f, 0.1f)] private float dashCollisionSkin = 0.01f;

        private readonly List<RaycastHit2D> dashHits = new List<RaycastHit2D>(8);
        private bool dashRequested;
        private Vector2 requestedDashInput;
        private Vector2 dashDirection;
        private float horizontalFacing = 1f;
        private float dashSecondsLeft;
        private float gravityBeforeDash;
        private bool finishDashNextTick;
        private bool dashUsedSinceGrounding;

        public bool DashEnabled => enableDash;
        public bool IsDashing { get; private set; }
        public Vector2 DashDirection => dashDirection;
        public float DashSpeed => dashSpeed;
        public float DashDuration => dashDuration;

        /// <summary>
        /// Optional approval hook, called once in FixedUpdate before an activation.
        /// Null allows unlimited prototype testing. A later resource owner may return false.
        /// The owner must reassign the hook after this component is disabled/re-enabled.
        /// </summary>
        public Func<Vector2, bool> DashApproval { get; set; }

        public void RequestDash(Vector2 directionalInput)
        {
            if (!isActiveAndEnabled || !enableDash || IsDashing || dashRequested)
                return;
            requestedDashInput = directionalInput;
            dashRequested = true;
        }

        private Vector2 ResolveDashDirection(Vector2 input)
        {
            float x = Mathf.Abs(input.x);
            float y = Mathf.Abs(input.y);
            if (Mathf.Max(x, y) <= dashDirectionDeadZone)
                return new Vector2(horizontalFacing, 0f);
            // Choose the dominant axis. Exact ties deliberately choose horizontal.
            if (x >= y)
                return input.x > 0f ? Vector2.right : Vector2.left;
            return input.y > 0f ? Vector2.up : Vector2.down;
        }

        private bool StepDash()
        {
            if (IsDashing && finishDashNextTick)
                StopDash();

            if (!IsDashing)
            {
                IsGrounded = CheckGrounded();
                if (IsGrounded)
                    dashUsedSinceGrounding = false;
            }

            if (dashRequested)
            {
                dashRequested = false;
                Vector2 direction = ResolveDashDirection(requestedDashInput);
                bool eligible = enableDash && body.simulated && bodyCollider.enabled &&
                    (!dashUsedSinceGrounding || SceneDashSettings.AllowsAirDashChaining());
                if (eligible)
                {
                    bool approved = DashApproval == null || DashApproval(direction);
                    // An approval owner may disable this actor immediately (for example later game over).
                    if (!isActiveAndEnabled || !body.simulated)
                        return true;
                    if (approved)
                    {
                        gravityBeforeDash = body.gravityScale;
                        body.gravityScale = 0f;
                        dashDirection = direction;
                        dashSecondsLeft = dashDuration;
                        finishDashNextTick = false;
                        IsDashing = true;
                        dashUsedSinceGrounding = true;
                    }
                }
            }

            if (!IsDashing)
                return false;

            jumpRequested = false;
            IsGrounded = false;
            float stepSeconds = Mathf.Min(Time.fixedDeltaTime, dashSecondsLeft);
            float requestedDistance = dashSpeed * stepSeconds;
            float travelDistance = SafeDashDistance(requestedDistance, out bool blocked);
            // A partial final physics tick preserves speed * requested duration as total travel.
            body.linearVelocity = dashDirection * (travelDistance / Time.fixedDeltaTime);
            dashSecondsLeft -= stepSeconds;
            finishDashNextTick = blocked || dashSecondsLeft <= 0.000001f;
            // Restore gravity on the next tick, after this velocity has actually been simulated.
            return true;
        }

        private float SafeDashDistance(float requestedDistance, out bool blocked)
        {
            Bounds bounds = bodyCollider.bounds;
            Vector2 size = new Vector2(Mathf.Max(0.001f, bounds.size.x - 2f * dashCollisionSkin),
                Mathf.Max(0.001f, bounds.size.y - 2f * dashCollisionSkin));
            ContactFilter2D filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(Physics2D.GetLayerCollisionMask(gameObject.layer));
            dashHits.Clear();
            Physics2D.BoxCast(bounds.center, size, 0f, dashDirection, filter, dashHits,
                requestedDistance + dashCollisionSkin);

            float distance = requestedDistance;
            blocked = false;
            foreach (RaycastHit2D hit in dashHits)
            {
                if (hit.collider.attachedRigidbody == body ||
                    Physics2D.GetIgnoreCollision(bodyCollider, hit.collider) ||
                    Vector2.Dot(hit.normal, dashDirection) >= -0.01f)
                    continue;
                float clearance = Mathf.Max(0f, hit.distance - dashCollisionSkin);
                if (clearance <= distance)
                {
                    distance = clearance;
                    blocked = true;
                }
            }
            return distance;
        }

        private void OnCollisionEnter2D(Collision2D collision) => CheckDashContact(collision);
        private void OnCollisionStay2D(Collision2D collision) => CheckDashContact(collision);

        private void CheckDashContact(Collision2D collision)
        {
            if (!IsDashing)
                return;
            for (int i = 0; i < collision.contactCount; i++)
                if (Vector2.Dot(collision.GetContact(i).normal, dashDirection) < -0.5f)
                    finishDashNextTick = true;
        }

        private void StopDash()
        {
            if (IsDashing && body != null)
            {
                body.gravityScale = gravityBeforeDash;
                body.linearVelocity = Vector2.zero;
            }
            IsDashing = false;
            dashDirection = Vector2.zero;
            dashSecondsLeft = 0f;
            finishDashNextTick = false;
        }

        private void ResetDashState()
        {
            StopDash();
            dashRequested = false;
            requestedDashInput = Vector2.zero;
            horizontalFacing = 1f;
            dashUsedSinceGrounding = false;
        }

        private void ValidateDashSettings()
        {
            dashSpeed = Mathf.Max(0.01f, dashSpeed);
            dashDuration = Mathf.Max(0.01f, dashDuration);
            dashDirectionDeadZone = Mathf.Clamp(dashDirectionDeadZone, 0f, 0.9f);
            dashCollisionSkin = Mathf.Clamp(dashCollisionSkin, 0.001f, 0.1f);
        }
    }
}
