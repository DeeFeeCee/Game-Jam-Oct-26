using GameJam.Movement;
using UnityEngine;

namespace GameJam.Mechanics
{
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public abstract class PhysicsPlatform2D : MonoBehaviour, IPlayerSupport2D
    {
        public Vector2 size = new Vector2(3, .5f);
        public SpriteRenderer surface, topEdge;
        protected Rigidbody2D Body;
        protected BoxCollider2D Shape;
        public Vector2 SupportVelocity { get; private set; }
        protected virtual void Awake()
        {
            Body = GetComponent<Rigidbody2D>(); Shape = GetComponent<BoxCollider2D>();
            Body.bodyType = RigidbodyType2D.Kinematic;
            Body.gravityScale = 0; Body.constraints = RigidbodyConstraints2D.FreezeRotation;
            size = ClampedSize(); Shape.size = size;
        }
        protected virtual void Start() => ApplySize();
        private Vector2 ClampedSize() => new Vector2(Mathf.Max(.1f,size.x),Mathf.Max(.1f,size.y));
        public void ApplySize()
        {
            size = ClampedSize();
            var box = GetComponent<BoxCollider2D>(); if (box != null && box.size != size) box.size = size;
            if (surface != null)
            {
                if (surface.drawMode != SpriteDrawMode.Sliced) surface.drawMode = SpriteDrawMode.Sliced;
                if (surface.size != size) surface.size = size;
            }
            if (topEdge != null)
            {
                if (topEdge.drawMode != SpriteDrawMode.Sliced) topEdge.drawMode = SpriteDrawMode.Sliced;
                Vector2 edgeSize = new Vector2(size.x,Mathf.Min(.05f,size.y));
                if (topEdge.size != edgeSize) topEdge.size = edgeSize;
                topEdge.transform.localPosition = new Vector3(0, (size.y-topEdge.size.y)/2);
            }
        }
        protected float StepDistance(float speed) => Mathf.Min(Mathf.Max(0, speed), Physics2D.maxTranslationSpeed) * Time.fixedDeltaTime;
        protected void MoveBody(Vector2 next)
        {
            Vector2 delta = Vector2.ClampMagnitude(next - Body.position, Physics2D.maxTranslationSpeed * Time.fixedDeltaTime);
            SupportVelocity = delta / Time.fixedDeltaTime;
            Body.MovePosition(Body.position + delta);
        }
        protected void Hold() { SupportVelocity = Vector2.zero; if (Body != null) Body.linearVelocity = Vector2.zero; }
        protected virtual void OnDisable() => Hold();
        protected virtual void OnValidate()
        {
            size = ClampedSize();
#if UNITY_EDITOR
            // Sprite tiling notifications are unsafe during OnValidate/asset consistency checks.
            UnityEditor.EditorApplication.update -= ApplyValidatedSize;
            UnityEditor.EditorApplication.update += ApplyValidatedSize;
#endif
        }
#if UNITY_EDITOR
        private void ApplyValidatedSize()
        {
            UnityEditor.EditorApplication.update -= ApplyValidatedSize;
            if (this != null) ApplySize();
        }
#endif
    }
}
