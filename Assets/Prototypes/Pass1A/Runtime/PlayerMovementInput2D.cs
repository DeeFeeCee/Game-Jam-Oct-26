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

        private void Awake() => movement = GetComponent<PlayerMovement2D>();

        private void OnEnable()
        {
            movement.ClearInput();
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
            jump.performed += OnJump;
            move.Enable();
            jump.Enable();
        }

        private void Update() => movement.SetHorizontalInput(move.ReadValue<Vector2>().x);

        private void OnJump(InputAction.CallbackContext context) => movement.RequestJump();

        private void OnDisable()
        {
            if (jump != null)
                jump.performed -= OnJump;
            if (runtimeActions != null)
            {
                runtimeActions.Disable();
                Destroy(runtimeActions);
            }
            runtimeActions = null;
            move = null;
            jump = null;
            movement.ClearInput();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
                movement.ClearInput();
        }
    }
}
