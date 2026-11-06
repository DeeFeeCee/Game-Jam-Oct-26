using UnityEngine;

namespace GameJam.UI
{
    /// <summary>Magnifies the one-pixel status change in Ace's animated terminal frames.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class TerminalBlinkIndicator : MonoBehaviour
    {
        [SerializeField] private Sprite blinkFrame;
        [SerializeField] private SpriteRenderer indicator;
        private SpriteRenderer animatedRenderer;

        private void Awake() => animatedRenderer = GetComponent<SpriteRenderer>();

        private void LateUpdate()
        {
            if (indicator != null) indicator.enabled = blinkFrame != null && animatedRenderer.sprite == blinkFrame;
        }
    }
}
