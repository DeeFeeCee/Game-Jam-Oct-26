using UnityEngine;

namespace GameJam.Levels
{
    public sealed class LevelCamera2D : MonoBehaviour
    {
        public Transform target;
        public float minimumX = -3f;
        public float maximumX = 21f;
        private void LateUpdate()
        {
            if (target == null) return;
            float x = Mathf.Clamp(target.position.x, minimumX, maximumX);
            transform.position = new Vector3(Mathf.Round(x * 20f) / 20f, 0f, -10f);
        }
    }
}
