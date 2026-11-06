using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace GameJam.Movement
{
    /// <summary>Maps a private action copy to discrete intents before the physics tick.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerMovement2D))]
    public sealed class PlayerMovementInput2D : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;

        private PlayerMovement2D movement;
        private InputActionAsset runtimeActions;
        private InputAction move;
        private InputAction jump;
        private InputAction dash;
        private bool dashPressed;
        private bool dashAttempted;
        private bool jumpPressed;
        private bool jumpNeedsRelease;
        public event System.Action Activity;

        private void Awake() => movement = GetComponent<PlayerMovement2D>();

        private void OnEnable()
        {
            movement.ClearInput();
            dashPressed = false;
            dashAttempted = false;
            jumpPressed = false;
            if (inputActions == null)
            {
                Debug.LogError("Assign the project's InputSystem_Actions asset to the player input component.", this);
                enabled = false;
                return;
            }

            // Each player owns its enabled actions; the shared project asset stays untouched.
            runtimeActions = Instantiate(inputActions);
            move = runtimeActions.FindAction("Player/Move", true);
            ConfigureKeyboardMovement();
            jump = runtimeActions.FindAction("Player/Jump", true);
            // Remap keyboard only on this private copy. Preserve gamepad/XR bindings.
            for (int i = jump.bindings.Count - 1; i >= 0; i--)
                if (jump.bindings[i].path.StartsWith("<Keyboard>/")) jump.ChangeBinding(i).Erase();
            jump.AddBinding("<Keyboard>/upArrow", groups: "Keyboard&Mouse");
            jump.AddBinding("<Keyboard>/w", groups: "Keyboard&Mouse");
            if (movement.DashEnabled)
            {
                dash = runtimeActions.FindAction("Player/Dash");
                if (dash == null)
                {
                    // Temporary bindings exist only on this private runtime copy.
                    dash = runtimeActions.FindActionMap("Player", true).AddAction("Dash", InputActionType.Button);
                    dash.AddBinding("<Keyboard>/space", groups: "Keyboard&Mouse");
                    dash.AddBinding("<Gamepad>/rightShoulder", groups: "Gamepad");
                }
                // This private copy enables only Move, Jump and Dash. Sprint stays disabled,
                // so its shared Left Shift binding cannot also activate gameplay here.
                runtimeActions.FindAction("Player/Sprint")?.Disable();
                dash.performed += OnDash;
                dash.Enable();
            }
            jump.performed += OnJump;
            // Move checks held controls on enable. W and Up share its bindings with Jump,
            // so that initial notification can also look like a new jump on scene entry.
            jumpNeedsRelease = HasHeldJumpControl();
            move.Enable();
            if (!jumpNeedsRelease) jump.Enable();
        }

        private void Update()
        {
            if (jumpNeedsRelease && !HasHeldJumpControl())
            {
                jumpNeedsRelease = false;
                jump.Enable();
            }

            Vector2 direction = move.ReadValue<Vector2>();
            movement.SetHorizontalInput(direction.x);
            if (direction.sqrMagnitude > 0.04f) Activity?.Invoke();
            // Read aim after all actions have processed the current input update.
            if (dashPressed)
            {
                dashPressed = false;
                movement.RequestDash(direction);
            }
            else if (jumpPressed && !dashAttempted)
                movement.RequestJump();
            // One input update has one discrete intent: Dash wins, even when rejected.
            // Held directional input cannot enqueue another jump without a new press.
            jumpPressed = false;
            dashAttempted = false;
        }

        private void OnJump(InputAction.CallbackContext context) { Activity?.Invoke(); jumpPressed = true; }

        private bool HasHeldJumpControl()
        {
            foreach (var control in jump.controls)
                if (control is ButtonControl button ? button.isPressed :
                    control.IsActuated(InputSystem.settings.defaultButtonPressPoint)) return true;
            return false;
        }

        private void ConfigureKeyboardMovement()
        {
            for (int i = 0; i < move.bindings.Count; i++)
            {
                if (!move.bindings[i].isComposite) continue;
                bool keyboardOnly = false;
                int part = i + 1;
                for (; part < move.bindings.Count && move.bindings[part].isPartOfComposite; part++)
                {
                    if (!move.bindings[part].path.StartsWith("<Keyboard>/")) { keyboardOnly = false; break; }
                    keyboardOnly = true;
                }
                // Digital mode preserves each keyboard axis at +/-1. Analog stick bindings
                // and the shared asset stay unchanged; cardinal dash arbitration still owns aim.
                if (keyboardOnly && (move.bindings[i].path == "Dpad" || move.bindings[i].path.StartsWith("2DVector")))
                    move.ChangeBinding(i).WithPath("2DVector(mode=1)");
            }
        }
        private void OnDash(InputAction.CallbackContext context)
        {
            Activity?.Invoke();
            dashAttempted = true;
            if (!movement.IsDashing)
                dashPressed = true;
        }

        private void OnDisable()
        {
            if (jump != null)
                jump.performed -= OnJump;
            if (dash != null)
                dash.performed -= OnDash;
            if (runtimeActions != null)
            {
                runtimeActions.Disable();
                Destroy(runtimeActions);
            }
            runtimeActions = null;
            move = null;
            jump = null;
            dash = null;
            dashPressed = false;
            dashAttempted = false;
            jumpPressed = false;
            jumpNeedsRelease = false;
            // A resource gate can disable input before this component's Awake on a failed scene load.
            if (movement != null) movement.ClearInput();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                dashPressed = false;
                dashAttempted = false;
                jumpPressed = false;
                movement.ClearInput();
            }
        }
    }
}
