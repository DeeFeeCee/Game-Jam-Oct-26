using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameJam.Movement
{
    /// <summary>Optional policy on one scene object; missing settings mean no air chaining.</summary>
    [DisallowMultipleComponent]
    public sealed class SceneDashSettings : MonoBehaviour
    {
        [Tooltip("Allow another completed dash without landing. Does not grant LM charges.")]
        [SerializeField] private bool allowAirDashChaining;

        public bool AllowAirDashChaining => allowAirDashChaining;

        // Read the active destination scene on each attempt, including for a persistent player.
        // No static cache or scene event subscriptions survive scene/Play Mode changes.
        public static bool AllowsAirDashChaining()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
                return false;

            SceneDashSettings found = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (SceneDashSettings settings in root.GetComponentsInChildren<SceneDashSettings>())
                {
                    if (!settings.isActiveAndEnabled)
                        continue;
                    // Conflicting authoring fails closed. Use exactly one enabled scene policy.
                    if (found != null)
                        return false;
                    found = settings;
                }
            return found != null && found.AllowAirDashChaining;
        }
    }
}
