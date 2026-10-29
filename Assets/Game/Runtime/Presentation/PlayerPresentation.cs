using GameJam.Movement;
using UnityEngine;

namespace GameJam.Presentation
{
    [RequireComponent(typeof(Animator), typeof(SpriteRenderer))]
    public sealed class PlayerPresentation : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float inactivitySeconds = 7f;
        private readonly IdleLookClock clock = new IdleLookClock();
        private PlayerMovement2D movement;
        private PlayerMovementInput2D input;
        private Rigidbody2D body;
        private Animator animator;
        private SpriteRenderer sprite;
        private float lookLength = 1.116667f;
        public bool IsLooking => clock.Looking;

        private void Awake()
        {
            movement = GetComponentInParent<PlayerMovement2D>();
            input = GetComponentInParent<PlayerMovementInput2D>();
            body = GetComponentInParent<Rigidbody2D>();
            animator = GetComponent<Animator>();
            sprite = GetComponent<SpriteRenderer>();
            animator.applyRootMotion = false;
            if (animator.runtimeAnimatorController != null)
                foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                    if (clip.name == "Look_anim") lookLength = clip.length;
        }
        private void OnEnable()
        {
            if (input != null) input.Activity += RecordActivity;
            RecordActivity();
        }
        private void OnDisable()
        {
            if (input != null) input.Activity -= RecordActivity;
            clock.Reset();
        }
        public void RecordActivity()
        {
            bool wasLooking = clock.Looking;
            clock.Reset();
            if (wasLooking && animator != null) animator.Play("Idle_anim", 0, 0f);
        }
        private void OnApplicationFocus(bool focus) { if (!focus) RecordActivity(); }
        private void Update()
        {
            if (movement == null || body == null) return;
            bool controlled = movement.isActiveAndEnabled && body.simulated;
            bool eligible = controlled && Application.isFocused && Time.timeScale > 0f &&
                movement.IsGrounded && !movement.IsDashing && body.linearVelocity.sqrMagnitude < 0.0025f;
            bool wasLooking = clock.Looking;
            clock.Step(Time.deltaTime, eligible, inactivitySeconds, lookLength);
            animator.speed = controlled ? 1f : 0f;
            if (clock.Looking != wasLooking)
                animator.Play(clock.Looking ? "Look_anim" : "Idle_anim", 0, 0f);
            if (Mathf.Abs(body.linearVelocity.x) > 0.05f) sprite.flipX = body.linearVelocity.x < 0f;
        }
    }
}
