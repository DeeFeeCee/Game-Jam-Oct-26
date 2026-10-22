using UnityEngine;
using UnityEngine.InputSystem;

namespace GameJam.Movement
{
    /// <summary>Reads the existing Player/Move and Player/Jump actions.</summary>
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

        private void Awake() => movement = GetComponent<PlayerMovement2D>();

        private void OnEnable()
        {
            movement.ClearInput();
            dashPressed = false;
            if (inputActions == null)
            {
                Debug.LogError("Assign the project's InputSystem_Actions asset to the player input component.", this);
                enabled = false;
                return;
            }

            // Each player owns its enabled actions; the shared project asset stays untouched.
            runtimeActions = Instantiate(inputActions);
            move = runtimeActions.FindAction("Player/Move", true);
            jump = runtimeActions.FindAction("Player/Jump", true);
            if (movement.DashEnabled)
            {
                dash = runtimeActions.FindAction("Player/Dash");
                if (dash == null)
                {
                    // Temporary bindings exist only on this private runtime copy.
                    dash = runtimeActions.FindActionMap("Player", true).AddAction("Dash", InputActionType.Button);
                    dash.AddBinding("<Keyboard>/leftShift", groups: "Keyboard&Mouse");
                    dash.AddBinding("<Gamepad>/rightShoulder", groups: "Gamepad");
                }
                // This private copy enables only Move, Jump and Dash. Sprint stays disabled,
                // so its shared Left Shift binding cannot also activate gameplay here.
                runtimeActions.FindAction("Player/Sprint")?.Disable();
                dash.performed += OnDash;
                dash.Enable();
            }
            jump.performed += OnJump;
            move.Enable();
            jump.Enable();
        }

        private void Update()
        {
            Vector2 direction = move.ReadValue<Vector2>();
            movement.SetHorizontalInput(direction.x);
            // Read aim after all actions have processed the current input update.
            if (dashPressed)
            {
                dashPressed = false;
                movement.RequestDash(direction);
            }
        }

        private void OnJump(InputAction.CallbackContext context) => movement.RequestJump();
        private void OnDash(InputAction.CallbackContext context)
        {
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
            movement.ClearInput();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                dashPressed = false;
                movement.ClearInput();
            }
        }
    }
}
