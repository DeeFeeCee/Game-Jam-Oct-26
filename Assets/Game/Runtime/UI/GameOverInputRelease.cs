using UnityEngine.InputSystem;

namespace GameJam.UI
{
    /// <summary>Do not inherit a fatal dash, held Submit, pointer press or navigation into the modal.</summary>
    public static class GameOverInputRelease
    {
        public static bool IsNeutral()
        {
            foreach(var device in InputSystem.devices)
            {
                if(device is Keyboard key&&(key.spaceKey.isPressed||key.enterKey.isPressed||key.numpadEnterKey.isPressed||
                    key.escapeKey.isPressed||key.wKey.isPressed||key.aKey.isPressed||key.sKey.isPressed||key.dKey.isPressed||
                    key.upArrowKey.isPressed||key.downArrowKey.isPressed||key.leftArrowKey.isPressed||key.rightArrowKey.isPressed))return false;
                if(device is Gamepad pad&&(pad.buttonSouth.isPressed||pad.buttonEast.isPressed||pad.rightShoulder.isPressed||
                    pad.leftStick.ReadValue().sqrMagnitude>.04f||pad.rightStick.ReadValue().sqrMagnitude>.04f||pad.dpad.ReadValue().sqrMagnitude>.04f))return false;
                if(device is Mouse mouse&&mouse.leftButton.isPressed)return false;
            }
            return true;
        }
    }
}
