#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using UnityEngine;

namespace GameJam.Presentation
{
    internal static class WindowsDisplayStartup
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void UseNativeBorderlessFullscreen()
        {
            // Unity can restore a windowed session despite the Player Settings default.
            // Apply the startup policy once, using the native size of the launch display.
            var display = Screen.mainWindowDisplayInfo;
            if (Screen.fullScreenMode != FullScreenMode.FullScreenWindow ||
                Screen.width != display.width || Screen.height != display.height)
            {
                Screen.SetResolution(display.width, display.height, FullScreenMode.FullScreenWindow);
            }
        }
    }
}
#endif
