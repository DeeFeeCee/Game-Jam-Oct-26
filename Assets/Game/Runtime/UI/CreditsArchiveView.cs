using UnityEngine;

namespace GameJam.UI
{
    /// <summary>Presentation only. The existing menu owns Cancel, focus and scene navigation.</summary>
    public sealed class CreditsArchiveView : MonoBehaviour
    {
        public GameObject panel;
        public UnityEngine.UI.ScrollRect archive;
        public UnityEngine.UI.Scrollbar scrollbar;
        public UnityEngine.UI.Button returnButton;
        public bool IsOpen => panel != null && panel.activeSelf;

        public bool Show()
        {
            if(panel==null||archive==null||archive.content==null||archive.viewport==null||scrollbar==null||returnButton==null)
            {Debug.LogError("Assign the Credits archive presentation references.",this);return false;}
            panel.SetActive(true);
            Canvas.ForceUpdateCanvases();
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(archive.content);
            archive.StopMovement();archive.verticalNormalizedPosition=1f;
            return true;
        }
        public void Hide()
        {
            if(archive!=null){archive.StopMovement();archive.verticalNormalizedPosition=1f;}
            if(panel!=null)panel.SetActive(false);
        }
    }
}
