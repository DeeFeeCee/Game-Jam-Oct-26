using UnityEngine.InputSystem;

namespace GameJam.UI
{
    /// <summary>Require a fresh press after opening or leaving the modal interface.</summary>
    public static class PauseInputRelease
    {
        public static bool IsNeutral()
        {
            if (!GameOverInputRelease.IsNeutral()) return false;
            foreach (var device in InputSystem.devices)
                if (device is Gamepad pad && pad.startButton.isPressed) return false;
            return true;
        }
    }
}
