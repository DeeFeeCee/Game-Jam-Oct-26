using System;
using GameJam.Movement;
using UnityEngine;

namespace GameJam.Resources
{
    /// <summary>Connects an opt-in player to the run bank and freezes it on missing bank/game over.</summary>
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerMovement2D), typeof(Rigidbody2D))]
    public sealed class PlayerDashBankLink : MonoBehaviour
    {
        private RunDashBank bank;
        private PlayerMovement2D movement;
        private PlayerMovementInput2D input;
        private Rigidbody2D body;
        private Func<Vector2, bool> approval;
        private bool locked;
        private bool restoreMovement;
        private bool restoreInput;
        private bool restoreSimulation;

        private void Awake()
        {
            movement = GetComponent<PlayerMovement2D>();
            input = GetComponent<PlayerMovementInput2D>();
            body = GetComponent<Rigidbody2D>();
            approval = ApproveDash;
        }

        private void OnEnable() => RefreshBinding();
        private void Update() => RefreshBinding();
        // Rebind before movement's physics tick if another system re-enables that component.
        private void FixedUpdate() => RefreshBinding();

        private void RefreshBinding()
        {
            RunDashBank current = RunDashBank.Instance;
            if (bank != current)
            {
                Unsubscribe();
                bank = current;
                if (bank != null)
                {
                    bank.ChargesChanged += OnChargesChanged;
                    bank.GameOver += FreezePlayer;
                    bank.NewRunStarted += OnNewRunStarted;
                }
            }
            if (bank == null || !bank.isActiveAndEnabled || bank.IsGameOver)
            {
                FreezePlayer();
                return;
            }
            RestorePlayer();
            if (movement.isActiveAndEnabled)
            {
                if (movement.DashApproval != null && movement.DashApproval != approval)
                {
                    Debug.LogError("PlayerDashBankLink requires exclusive ownership of DashApproval.", this);
                    enabled = false;
                    return;
                }
                movement.DashApproval = approval;
            }
        }

        private bool ApproveDash(Vector2 direction)
        {
            return isActiveAndEnabled && bank != null && bank == RunDashBank.Instance &&
                bank.TrySpendDash();
        }

        private void OnChargesChanged(int remaining) => RefreshBinding();

        private void FreezePlayer()
        {
            if (movement == null || body == null || locked)
                return;
            restoreMovement = movement.enabled;
            restoreInput = input != null && input.enabled;
            restoreSimulation = body.simulated;
            locked = true;
            movement.enabled = false; // Cancels a current dash and restores its captured gravity first.
            if (input != null) input.enabled = false;
            body.linearVelocity = Vector2.zero;
            body.simulated = false; // Walking, jumping, gravity and collisions cannot move a failed player.
        }

        private void RestorePlayer()
        {
            if (!locked)
                return;
            locked = false;
            body.simulated = restoreSimulation;
            movement.enabled = restoreMovement;
            if (input != null) input.enabled = restoreInput;
        }

        private void OnNewRunStarted()
        {
            RestorePlayer();
            bool wasEnabled = movement.enabled;
            movement.enabled = false;
            movement.enabled = wasEnabled; // Fresh movement eligibility, without granting extra charges.
            movement.ClearInput();
            body.linearVelocity = Vector2.zero;
            RefreshBinding();
        }

        private void Unsubscribe()
        {
            if (bank == null)
                return;
            bank.ChargesChanged -= OnChargesChanged;
            bank.GameOver -= FreezePlayer;
            bank.NewRunStarted -= OnNewRunStarted;
        }

        private void OnDisable()
        {
            Unsubscribe();
            bank = null;
            if (movement != null && movement.DashApproval == approval)
                movement.DashApproval = null;
            FreezePlayer(); // Disabling the integration cannot turn this player into an unlimited prototype.
        }
    }
}
