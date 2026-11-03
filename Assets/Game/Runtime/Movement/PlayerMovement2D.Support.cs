using UnityEngine;

namespace GameJam.Movement
{
    public sealed partial class PlayerMovement2D
    {
        private IPlayerSupport2D support;
        private Behaviour supportOwner;
        private Vector2 appliedSupportVelocity, departedSupportVelocity;

        private bool AcceptGroundSupport(RaycastHit2D hit)
        {
            var candidate = hit.rigidbody != null ? hit.rigidbody.GetComponent<IPlayerSupport2D>() : null;
            var owner = candidate as Behaviour;
            Vector2 next = owner != null && owner.isActiveAndEnabled ? candidate.SupportVelocity : Vector2.zero;
            // The last applied velocity handles reversals. A newly contacted ascending
            // platform uses its own velocity so upward transport is not mistaken for a jump.
            float referenceY = owner != null && ReferenceEquals(owner, supportOwner) ? appliedSupportVelocity.y : next.y;
            if (body.linearVelocity.y - referenceY > .05f) return false;
            support = candidate; supportOwner = owner;
            return true;
        }

        private void DropSupport()
        {
            departedSupportVelocity = appliedSupportVelocity;
            support = null; supportOwner = null; appliedSupportVelocity = Vector2.zero;
        }

        private void ClearSupport()
        {
            support = null; supportOwner = null;
            appliedSupportVelocity = departedSupportVelocity = Vector2.zero;
        }

        private void ApplySupport(ref Vector2 velocity)
        {
            velocity.y -= departedSupportVelocity.y;
            departedSupportVelocity = Vector2.zero;
            if (!IsGrounded || supportOwner == null || !supportOwner.isActiveAndEnabled)
            { appliedSupportVelocity = Vector2.zero; return; }
            appliedSupportVelocity = support.SupportVelocity;
            velocity.x += appliedSupportVelocity.x;
            velocity.y = appliedSupportVelocity.y;
        }
    }
}
