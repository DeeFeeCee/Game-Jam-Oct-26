using UnityEngine;

namespace GameJam.SingleScreen
{
    /// <summary>Editor guide only. No transform snapping, camera control or gameplay updates.</summary>
    public sealed class RoomGridGuide : MonoBehaviour
    {
        public const int Columns = 32, Rows = 18;
#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(.35f,.8f,1f,.3f);
            for(int x=0;x<=Columns;x++) Gizmos.DrawLine(new Vector3(x,0),new Vector3(x,Rows));
            for(int y=0;y<=Rows;y++) Gizmos.DrawLine(new Vector3(0,y),new Vector3(Columns,y));
            Gizmos.color = Color.yellow;
            foreach(var solid in FindObjectsByType<BoxCollider2D>(FindObjectsSortMode.None))
                if(!solid.isTrigger&&solid.attachedRigidbody==null) Gizmos.DrawWireCube(solid.bounds.center,solid.bounds.size);
        }
#endif
    }
}
