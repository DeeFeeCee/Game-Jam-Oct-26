#if UNITY_EDITOR
using System.Collections;
using System.IO;
using GameJam.Movement;
using GameJam.Resources;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GameJam.UnifiedGameplay.Tests
{
    public sealed class KeyboardTravelTests
    {
        private Keyboard keyboard;
        private Gamepad gamepad;
        private InputSettings previous, settings;
        private HideFlags flags;
        private GameObject floor, actor;
        private float measured;
        [UnitySetUp] public IEnumerator Setup()
        {
            previous = InputSystem.settings; flags = previous.hideFlags;
            if (flags == HideFlags.HideAndDontSave) previous.hideFlags |= HideFlags.HideInInspector;
            settings = Object.Instantiate(previous); settings.hideFlags = HideFlags.HideAndDontSave;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = settings; keyboard = InputSystem.AddDevice<Keyboard>();
            Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/RunDashBank.prefab"));
            floor = new GameObject("Input comparison floor", typeof(BoxCollider2D));
            floor.transform.position = new Vector3(0, -.5f); floor.GetComponent<BoxCollider2D>().size = new Vector2(200, 1);
            yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (actor != null) Object.Destroy(actor); Object.Destroy(floor);
            if (RunDashBank.Instance != null) Object.Destroy(RunDashBank.Instance.gameObject);
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (gamepad != null && gamepad.added) InputSystem.RemoveDevice(gamepad);
            InputSystem.settings = previous; previous.hideFlags = flags; Object.Destroy(settings);
            yield return null; LogAssert.NoUnexpectedReceived();
        }
        private IEnumerator Travel(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            if (actor != null) { Object.Destroy(actor); yield return null; }
            actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/Player.prefab"), new Vector3(0, .81f), Quaternion.identity);
            var movement = actor.GetComponent<PlayerMovement2D>(); var body = actor.GetComponent<Rigidbody2D>();
            for (int i = 0; i < 100 && !movement.IsGrounded; i++) yield return new WaitForFixedUpdate();
            Assert.IsTrue(movement.IsGrounded);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); yield return null;
            yield return new WaitForFixedUpdate();
            float start = body.position.x, time = Time.fixedTime; int airborne = 0;
            for (int i = 0; i < 40; i++) { yield return new WaitForFixedUpdate(); if (!movement.IsGrounded) airborne++; }
            measured = body.position.x - start;
            Directory.CreateDirectory("Logs/UnifiedGameplay");
            File.AppendAllText("Logs/UnifiedGameplay/keyboard-travel.csv", string.Join("+", keys) + "," + measured.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "," + (Time.fixedTime-time).ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ",airborne=" + airborne + "\n");
            Assert.AreEqual(5, RunDashBank.Instance.RemainingCharges, "Walking/jumping must not spend a charge.");
        }
        private IEnumerator Compare(Key horizontal, Key vertical)
        {
            yield return Travel(horizontal); float normal = measured;
            yield return Travel(horizontal, vertical);
            Assert.AreEqual(normal, measured, .04f, "Vertical keyboard intent must not normalize horizontal travel.");
        }
        [UnityTest] public IEnumerator DMatchesWD() { yield return Compare(Key.D, Key.W); }
        [UnityTest] public IEnumerator AMatchesWA() { yield return Compare(Key.A, Key.W); }
        [UnityTest] public IEnumerator RightMatchesUpRight() { yield return Compare(Key.RightArrow, Key.UpArrow); }
        [UnityTest] public IEnumerator LeftMatchesUpLeft() { yield return Compare(Key.LeftArrow, Key.UpArrow); }
        [UnityTest] public IEnumerator DMatchesUpD() { yield return Compare(Key.D, Key.UpArrow); }
        [UnityTest] public IEnumerator AMatchesUpA() { yield return Compare(Key.A, Key.UpArrow); }
        [UnityTest] public IEnumerator AnalogStickRetainsProcessedPartialMagnitude()
        {
            gamepad=InputSystem.AddDevice<Gamepad>();
            actor=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/Player.prefab"),new Vector3(0,.81f),Quaternion.identity);
            yield return new WaitForSeconds(.08f);
            InputSystem.QueueStateEvent(gamepad,new GamepadState {leftStick=new Vector2(.5f,.3f)});
            yield return new WaitForSeconds(.08f);
            var body=actor.GetComponent<Rigidbody2D>(); float processed=gamepad.leftStick.ReadValue().x;
            Assert.That(processed,Is.InRange(.1f,.9f)); Assert.AreEqual(processed*6,body.linearVelocity.x,.01f);
            Assert.AreEqual(5,RunDashBank.Instance.RemainingCharges);
        }
    }
}
#endif
