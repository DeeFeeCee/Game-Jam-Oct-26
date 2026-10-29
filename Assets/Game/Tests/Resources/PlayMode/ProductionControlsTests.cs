#if UNITY_EDITOR
using System.Collections;
using GameJam.Movement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace GameJam.Resources.Tests
{
    public sealed partial class DashBankTests
    {
        private Keyboard revisionKeyboard;
        private Gamepad revisionGamepad;
        private void EnableControls()
        {
            testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            revisionKeyboard = InputSystem.AddDevice<Keyboard>();
            movement.GetComponent<PlayerMovementInput2D>().enabled = true;
        }
        private void Press(params Key[] keys) => InputSystem.QueueStateEvent(revisionKeyboard, new KeyboardState(keys));
        private IEnumerator StandOnFloor()
        {
            Solid("Controls floor",new Vector2(0,-.5f),new Vector2(20,1));
            body.position = new Vector2(0,.8f);
            yield return Ground();
        }

        [UnityTest] public IEnumerator UpArrowAloneJumpsWithoutSpending()
        {
            yield return StandOnFloor(); EnableControls(); Press(Key.UpArrow);
            yield return new WaitForSeconds(.06f);
            Assert.Greater(body.linearVelocity.y,0); Assert.AreEqual(4,bank.RemainingCharges);
        }
        [UnityTest] public IEnumerator WIsEquivalentJumpAndHeldKeyDoesNotAutoJumpOnLanding()
        {
            yield return StandOnFloor(); EnableControls(); Press(Key.W);
            yield return new WaitForSeconds(.06f); Assert.Greater(body.linearVelocity.y,0);
            body.linearVelocity=Vector2.zero; body.position=new Vector2(0,.8f);
            yield return Ground(); yield return new WaitForSeconds(.08f);
            Assert.IsTrue(movement.IsGrounded); Assert.AreEqual(0,body.linearVelocity.y,.01f);
        }
        [UnityTest] public IEnumerator SpaceAloneDashesAndSpendsOnce()
        {
            EnableControls(); Press(Key.Space); yield return new WaitForSeconds(.06f);
            Assert.IsTrue(movement.IsDashing); Assert.AreEqual(Vector2.right,movement.DashDirection);
            yield return Finish(); yield return new WaitForSeconds(.06f); Assert.AreEqual(3,bank.RemainingCharges);
        }
        [UnityTest] public IEnumerator UpAndSpaceDashUpwardWithoutExtraJump()
        {
            yield return StandOnFloor(); EnableControls(); Press(Key.UpArrow,Key.Space);
            yield return new WaitForSeconds(.06f);
            Assert.AreEqual(Vector2.up,movement.DashDirection); Assert.AreEqual(movement.DashSpeed,body.linearVelocity.y,.01f);
            yield return Finish(); Assert.AreEqual(0,body.linearVelocity.y,.01f); Assert.AreEqual(3,bank.RemainingCharges);
        }
        [UnityTest] public IEnumerator SpaceBeforeUpInSameInputUpdateStillUsesUpwardAim()
        {
            EnableControls(); Press(Key.Space); Press(Key.Space,Key.UpArrow);
            yield return new WaitForSeconds(.06f);
            Assert.AreEqual(Vector2.up,movement.DashDirection); Assert.AreEqual(3,bank.RemainingCharges);
        }
        [UnityTest] public IEnumerator UpBeforeSpaceInSameInputUpdateStillSuppressesJump()
        {
            yield return StandOnFloor(); EnableControls(); Press(Key.UpArrow); Press(Key.UpArrow,Key.Space);
            yield return new WaitForSeconds(.06f);
            Assert.AreEqual(Vector2.up,movement.DashDirection); Assert.AreEqual(movement.DashSpeed,body.linearVelocity.y,.01f);
        }
        [UnityTest] public IEnumerator BlockedSimultaneousDashDoesNotFallBackToJumpOrSpend()
        {
            yield return StandOnFloor();
            Solid("Flush ceiling",new Vector2(0,1.7f),new Vector2(4,.2f)); Physics2D.SyncTransforms();
            EnableControls(); Press(Key.UpArrow,Key.Space); yield return new WaitForSeconds(.06f);
            Assert.IsFalse(movement.IsDashing); Assert.AreEqual(4,bank.RemainingCharges);
            Assert.AreEqual(0,body.linearVelocity.y,.01f);
            // Remove ceiling while keys stay held: no delayed jump may have been queued.
            Object.Destroy(objects[objects.Count-1]); yield return null; yield return new WaitForSeconds(.06f);
            Assert.AreEqual(0,body.linearVelocity.y,.01f);
        }
        [UnityTest] public IEnumerator RejectedAirborneSpaceDoesNotSpendAgain()
        {
            EnableControls(); Press(Key.Space); yield return new WaitForSeconds(.06f); yield return Finish();
            Press(); yield return null; Press(Key.UpArrow,Key.Space); yield return new WaitForSeconds(.06f);
            Assert.IsFalse(movement.IsDashing); Assert.AreEqual(3,bank.RemainingCharges); Assert.AreEqual(0,body.linearVelocity.y,.01f);
        }
        [UnityTest] public IEnumerator DownArrowAndSpaceAimDownward()
        { EnableControls(); Press(Key.DownArrow,Key.Space); yield return new WaitForSeconds(.06f); Assert.AreEqual(Vector2.down,movement.DashDirection); }
        [UnityTest] public IEnumerator WAndSpaceAimUpward()
        { EnableControls(); Press(Key.W,Key.Space); yield return new WaitForSeconds(.06f); Assert.AreEqual(Vector2.up,movement.DashDirection); }
        [UnityTest] public IEnumerator SAndSpaceAimDownward()
        { EnableControls(); Press(Key.S,Key.Space); yield return new WaitForSeconds(.06f); Assert.AreEqual(Vector2.down,movement.DashDirection); }
        [UnityTest] public IEnumerator ShiftNoLongerDashesOrSpends()
        { EnableControls(); Press(Key.LeftShift); yield return new WaitForSeconds(.06f); Assert.IsFalse(movement.IsDashing); Assert.AreEqual(4,bank.RemainingCharges); }
        [UnityTest] public IEnumerator GamepadSouthStillJumps()
        {
            yield return StandOnFloor(); EnableControls(); revisionGamepad=InputSystem.AddDevice<Gamepad>();
            InputSystem.QueueStateEvent(revisionGamepad,new GamepadState().WithButton(GamepadButton.South));
            yield return new WaitForSeconds(.06f); Assert.Greater(body.linearVelocity.y,0); Assert.AreEqual(4,bank.RemainingCharges);
        }
        [UnityTest] public IEnumerator GamepadShoulderAndSouthUseSameDashPrecedence()
        {
            yield return StandOnFloor(); EnableControls(); revisionGamepad=InputSystem.AddDevice<Gamepad>();
            InputSystem.QueueStateEvent(revisionGamepad,new GamepadState {leftStick=Vector2.up}.WithButton(GamepadButton.South).WithButton(GamepadButton.RightShoulder));
            yield return new WaitForSeconds(.06f); Assert.AreEqual(Vector2.up,movement.DashDirection);
            Assert.AreEqual(movement.DashSpeed,body.linearVelocity.y,.01f); Assert.AreEqual(3,bank.RemainingCharges);
        }
        [UnityTest] public IEnumerator FinalSpaceChargeFreezesWithoutFatalDashTravel()
        {
            SetStartingCount(1); bank.InitializeNewRun(); EnableControls(); Vector2 start=body.position;
            Press(Key.UpArrow,Key.Space); yield return new WaitForSeconds(.06f);
            Assert.IsTrue(bank.IsGameOver); Assert.IsFalse(movement.enabled); Assert.IsFalse(body.simulated);
            Assert.AreEqual(start,body.position); Assert.AreEqual(0,bank.RemainingCharges);
        }
        [UnityTest] public IEnumerator FocusLossClearsPendingJumpAndDashIntents()
        {
            yield return StandOnFloor(); EnableControls(); Press(Key.UpArrow,Key.Space); InputSystem.Update();
            movement.GetComponent<PlayerMovementInput2D>().SendMessage("OnApplicationFocus",false);
            yield return new WaitForSeconds(.06f);
            Assert.IsFalse(movement.IsDashing); Assert.AreEqual(4,bank.RemainingCharges); Assert.AreEqual(0,body.linearVelocity.y,.01f);
        }
        [UnityTest] public IEnumerator DisableAndReenableClearsDiscreteRequests()
        {
            yield return StandOnFloor(); EnableControls(); Press(Key.UpArrow,Key.Space); InputSystem.Update();
            var adapter=movement.GetComponent<PlayerMovementInput2D>(); adapter.enabled=false;
            Press(); InputSystem.Update(); adapter.enabled=true; yield return new WaitForSeconds(.06f);
            Assert.IsFalse(movement.IsDashing); Assert.AreEqual(4,bank.RemainingCharges); Assert.AreEqual(0,body.linearVelocity.y,.01f);
        }
    }
}
#endif
