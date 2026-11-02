using UnityEngine;

namespace GameJam.Levels
{
    /// <summary>Authoring guide only; never snaps a player or updates a camera.</summary>
    [DisallowMultipleComponent]
    public sealed class RoomGrid32x18 : MonoBehaviour
    {
        public const int Columns = 32;
        public const int Rows = 18;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(.35f, .8f, 1f, .3f);
            for (int x = 0; x <= Columns; x++)
                Gizmos.DrawLine(new Vector3(x, 0), new Vector3(x, Rows));
            for (int y = 0; y <= Rows; y++)
                Gizmos.DrawLine(new Vector3(0, y), new Vector3(Columns, y));
            Gizmos.color = Color.yellow;
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var solid in root.GetComponentsInChildren<BoxCollider2D>())
                    if (!solid.isTrigger && solid.attachedRigidbody == null)
                        Gizmos.DrawWireCube(solid.bounds.center, solid.bounds.size);
        }
#endif
    }
}
