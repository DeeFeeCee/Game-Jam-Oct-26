using System.Collections;
using GameJam.Movement;
using GameJam.Resources;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameJam.Levels
{
    public sealed class LevelTransition : MonoBehaviour
    {
        [Tooltip("A registered scene path. Empty means this verification destination has no exit.")]
        public string destinationScene;
        public bool TransitionStarted { get; private set; }
        private bool reportedInvalidDestination;
        public bool CanExit(PlayerMovement2D actor)
        {
            RunDashBank bank = RunDashBank.Instance;
            return !TransitionStarted && actor != null && actor.isActiveAndEnabled &&
                actor.GetComponent<PlayerDashBankLink>() != null && bank != null &&
                !bank.IsGameOver && bank.RemainingCharges > 0 &&
                !string.IsNullOrEmpty(destinationScene) && Application.CanStreamedLevelBeLoaded(destinationScene);
        }
        public bool TryExit(PlayerMovement2D actor)
        {
            if (!CanExit(actor))
            {
                if (!reportedInvalidDestination && actor != null && actor.isActiveAndEnabled &&
                    !string.IsNullOrWhiteSpace(destinationScene) && !Application.CanStreamedLevelBeLoaded(destinationScene))
                {
                    Debug.LogWarning($"Exit destination is unavailable: {destinationScene}", this);
                    reportedInvalidDestination = true;
                }
                return false;
            }
            TransitionStarted = true;
            // Once an exit is accepted, prevent further input/spending while its scene unloads.
            actor.ClearInput();
            actor.enabled = false;
            PlayerMovementInput2D input = actor.GetComponent<PlayerMovementInput2D>();
            if (input != null) input.enabled = false;
            Rigidbody2D body = actor.GetComponent<Rigidbody2D>();
            body.linearVelocity = Vector2.zero;
            body.simulated = false;
            StartCoroutine(LoadDestination());
            return true;
        }
        private IEnumerator LoadDestination()
        {
            yield return SceneManager.LoadSceneAsync(destinationScene, LoadSceneMode.Single);
        }
    }
}
