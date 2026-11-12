using GameJam.Movement;
using UnityEngine;

namespace GameJam.Presentation
{
    [DefaultExecutionOrder(100)] // Observe the dash after movement's physics step.
    [RequireComponent(typeof(Animator), typeof(SpriteRenderer))]
    public sealed class PlayerPresentation : MonoBehaviour
    {
        private enum Pose { Idle, Look, Walk, Transform, Plasma }

        private static readonly int[] StateHashes =
        {
            Animator.StringToHash("Base Layer.Idle_anim"),
            Animator.StringToHash("Base Layer.Look_anim"),
            Animator.StringToHash("Base Layer.Walk"),
            Animator.StringToHash("Base Layer.Transform"),
            Animator.StringToHash("Base Layer.Plasma")
        };

        // The existing idle/look art uses 100 PPU at five times scale. Walk and
        // transformation use 20 PPU, and the centered plasma sprite uses 10 PPU.
        private const float WalkAndTransformScale = 0.2f;
        private const float PlasmaScale = 0.1f;
        private const float WalkFootOffset = 0.2f;
        private const double TransformSeconds = 0.08;
        private const float WalkThreshold = 0.05f;

        [SerializeField, Min(0.01f)] private float inactivitySeconds = 7f;
        private readonly IdleLookClock clock = new IdleLookClock();
        private PlayerMovement2D movement;
        private PlayerMovementInput2D input;
        private Rigidbody2D body;
        private Animator animator;
        private SpriteRenderer sprite;
        private Vector3 restPosition;
        private Quaternion restRotation;
        private Vector3 restScale;
        private float lookLength = 1.116667f;
        private bool facingLeft;
        private bool dashObserved;
        private bool newDash;
        private double dashStartedAt;
        private Pose? pose;

        public bool IsLooking => clock.Looking;

        private void Awake()
        {
            movement = GetComponentInParent<PlayerMovement2D>();
            input = GetComponentInParent<PlayerMovementInput2D>();
            body = GetComponentInParent<Rigidbody2D>();
            animator = GetComponent<Animator>();
            sprite = GetComponent<SpriteRenderer>();
            restPosition = transform.localPosition;
            restRotation = transform.localRotation;
            restScale = transform.localScale;
            animator.applyRootMotion = false;
            if (animator.runtimeAnimatorController != null)
                foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                    if (clip.name == "Look_anim") lookLength = clip.length;
        }

        private void OnEnable()
        {
            facingLeft = sprite.flipX;
            if (input != null) input.Activity += RecordActivity;
        }

        private void OnDisable()
        {
            if (input != null) input.Activity -= RecordActivity;
            clock.Reset();
            dashObserved = false;
            newDash = false;
            Show(Pose.Idle);
            pose = null;
            RestoreVisualTransform();
            sprite.flipX = facingLeft;
        }

        public void RecordActivity()
        {
            clock.Reset();
            if (pose == Pose.Look && Time.timeScale > 0f)
                Show(Pose.Idle);
        }

        private void OnApplicationFocus(bool focus)
        {
            if (!focus) RecordActivity();
        }

        private void FixedUpdate()
        {
            bool dashing = movement != null && movement.IsDashing;
            if (dashing && !dashObserved)
            {
                dashStartedAt = Time.fixedTimeAsDouble;
                newDash = true;
            }
            dashObserved = dashing;
        }

        private void Update()
        {
            if (movement == null || body == null) return;
            if (!movement.isActiveAndEnabled || !body.simulated)
            {
                // A failure or exit can freeze gameplay before another physics tick.
                clock.Reset();
                dashObserved = false;
                newDash = false;
                Show(Pose.Idle);
                animator.speed = 0f;
                return;
            }

            // Pause can be requested after the frame's delta time was computed.
            // Stop the Animator explicitly as well as pausing phase selection.
            animator.speed = Time.timeScale > 0f ? 1f : 0f;
            if (Time.timeScale <= 0f) return;

            if (movement.IsDashing)
            {
                clock.Reset();
                Vector2 direction = movement.DashDirection;
                if (direction.x != 0f) facingLeft = direction.x < 0f;
                double elapsed = Time.fixedTimeAsDouble - dashStartedAt;
                Pose dashPose = elapsed + 0.000001 < TransformSeconds ? Pose.Transform : Pose.Plasma;
                Show(dashPose, newDash, direction);
                newDash = false;
                return;
            }

            newDash = false;
            Vector2 velocity = movement.SupportRelativeVelocity;
            bool grounded = movement.IsGrounded;
            bool movingHorizontally = Mathf.Abs(velocity.x) > WalkThreshold;
            // Require actual travel in the requested direction. Input alone can be
            // blocked, and transport alone must never walk or turn a grounded rider.
            bool walking = grounded && movingHorizontally && velocity.x * movement.HorizontalMovementIntent > 0f;
            if (walking || (!grounded && movingHorizontally)) facingLeft = velocity.x < 0f;
            bool idle = grounded && velocity.sqrMagnitude < WalkThreshold * WalkThreshold;
            clock.Step(Time.deltaTime, idle && Application.isFocused, inactivitySeconds, lookLength);
            Show(walking ? Pose.Walk : clock.Looking ? Pose.Look : Pose.Idle);
        }

        private void Show(Pose next, bool restart = false, Vector2 direction = default)
        {
            sprite.flipX = next != Pose.Plasma && facingLeft;
            if (pose == next && !restart) return;
            int state = StateHashes[(int)next];
            if (!animator.isActiveAndEnabled || !animator.HasState(0, state)) return;

            pose = next;
            RestoreVisualTransform();
            switch (next)
            {
                case Pose.Walk:
                    transform.localScale = restScale * WalkAndTransformScale;
                    transform.localPosition = restPosition + Vector3.up * WalkFootOffset;
                    break;
                case Pose.Transform:
                    transform.localScale = restScale * WalkAndTransformScale;
                    break;
                case Pose.Plasma:
                    transform.localScale = restScale * PlasmaScale;
                    float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                    transform.localRotation = restRotation * Quaternion.Euler(0f, 0f, angle);
                    break;
            }

            animator.Play(state, 0, 0f);
            animator.Update(0f); // Sample the new sprite before rendering its new pose.
        }

        private void RestoreVisualTransform()
        {
            transform.SetLocalPositionAndRotation(restPosition, restRotation);
            transform.localScale = restScale;
        }
    }
}
