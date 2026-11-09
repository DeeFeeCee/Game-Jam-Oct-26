using UnityEngine;

namespace GameJam.Presentation
{
    public enum EnvironmentZone
    {
        Blue,
        YellowGreen,
        Orange
    }

    /// <summary>Scene-owned art assignment. The Editor applies the selected theme to the existing room.</summary>
    [DisallowMultipleComponent]
    public sealed class EnvironmentTheme : MonoBehaviour
    {
        [SerializeField] private EnvironmentZone zone;
        [SerializeField, Tooltip("Assign only approved Orange source art. An empty slot keeps the temporary Blue background.")]
        private Sprite orangeBackground;

        public EnvironmentZone Zone => zone;
        public Sprite OrangeBackground => orangeBackground;
        public bool UsesTemporaryBlueBackground => zone == EnvironmentZone.Orange && orangeBackground == null;
    }
}
