#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using GameJam.Levels;
using GameJam.Movement;
using GameJam.Resources;
using GameJam.UI;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GameJam.BlueIntegration.Tests
{
    public sealed class ProductionTerminalTests
    {
        public static readonly int[] Rooms = { 1, 3, 4, 5, 6, 7, 8, 11 };
        private Keyboard keyboard;
        private InputSettings originalSettings, testSettings;
        private string originalSettingsJson;
        private HideFlags originalSettingsFlags;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            Time.timeScale = 1;
            originalSettings = InputSystem.settings;
            originalSettingsJson = JsonUtility.ToJson(originalSettings);
            originalSettingsFlags = originalSettings.hideFlags;
            if (originalSettingsFlags == HideFlags.HideAndDontSave)
                originalSettings.hideFlags |= HideFlags.HideInInspector;
            testSettings = Object.Instantiate(originalSettings);
            testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = testSettings;
            keyboard = InputSystem.AddDevice<Keyboard>();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            var old = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("Decorative terminal test cleanup"));
            if (old.IsValid() && old.isLoaded) yield return SceneManager.UnloadSceneAsync(old);
            if (RunDashBank.Instance != null) Object.Destroy(RunDashBank.Instance.gameObject);
            InputSystem.RemoveDevice(keyboard);
            if (originalSettings == null)
            {
                originalSettings = ScriptableObject.CreateInstance<InputSettings>();
                JsonUtility.FromJsonOverwrite(originalSettingsJson, originalSettings);
            }
            InputSystem.settings = originalSettings;
            originalSettings.hideFlags = originalSettingsFlags;
            Object.Destroy(testSettings);
            Time.timeScale = 1;
            yield return null;
        }

        [UnityTest]
        public IEnumerator JettFallsThroughFormerTopWalksPastAndJumps([ValueSource(nameof(Rooms))] int room)
        {
            yield return LoadRoom(room);
            var art = TerminalArt();
            var legacy = art.GetComponentInParent<BoxCollider2D>();
            Assert.IsFalse(legacy.enabled);
            Assert.IsFalse(legacy.isTrigger);
            Assert.IsFalse(legacy.GetComponentsInChildren<Collider2D>(true).Any(shape => shape.enabled));
            Assert.IsNull(Object.FindFirstObjectByType<ResearchTerminal>());
            var support = Support(art);
            float top = support.bounds.max.y;
            float x = legacy.transform.position.x;
            float oldTop = legacy.transform.TransformPoint(legacy.offset + Vector2.up * legacy.size.y / 2f).y;
            var actor = Object.FindFirstObjectByType<LevelSetup>().Player.GetComponent<PlayerMovement2D>();
            var body = actor.GetComponent<Rigidbody2D>();
            var playerBox = actor.GetComponent<BoxCollider2D>();
            var bank = RunDashBank.Instance;
            int balance = bank.RemainingCharges;

            Place(body, x, body.position.y + (oldTop + .3f - playerBox.bounds.min.y));
            yield return Until(() => actor.IsGrounded && Mathf.Abs(playerBox.bounds.min.y - top) < .045f,
                "Jett lands on the support through the old invisible top");
            Assert.Less(playerBox.bounds.min.y, oldTop - .5f);

            Press(Key.UpArrow);
            yield return Until(() => playerBox.bounds.min.y > top + .65f && !actor.IsGrounded, "normal jump from the platform");
            Press();
            yield return Until(() => actor.IsGrounded && Mathf.Abs(playerBox.bounds.min.y - top) < .045f, "landing after the jump");

            float direction = room is 5 or 6 or 8 ? -1f : 1f;
            Place(body, x - direction * .86f, body.position.y + .04f);
            yield return Until(() => actor.IsGrounded && Mathf.Abs(playerBox.bounds.min.y - top) < .045f, "settle beside the terminal");
            Press(direction > 0 ? Key.RightArrow : Key.LeftArrow);
            // The Level04 terminal center sits at the authored platform edge.
            float crossing = room == 4 ? .17f : .39f;
            yield return Until(() => (body.position.x - x) * direction > crossing, "walk through the old collision column");
            Press();
            Assert.Less(Mathf.Abs(playerBox.bounds.min.y - top), .065f);
            Assert.IsFalse(bank.IsGameOver);
            Assert.AreEqual(balance, bank.RemainingCharges);
            Assert.AreEqual($"Level{room:00}", SceneManager.GetActiveScene().name);
        }

        [UnityTest]
        public IEnumerator SourceFramesAndVisibleAccentLoopWithoutMovingTheSprite([ValueSource(nameof(Rooms))] int room)
        {
            yield return LoadRoom(room);
            var animator = TerminalArt();
            var art = animator.GetComponent<SpriteRenderer>();
            var accent = art.transform.Find("Blink status accent")?.GetComponent<SpriteRenderer>();
            var position = art.transform.position;
            var scale = art.transform.lossyScale;
            var support = Support(animator);
            bool yellow = room is >= 5 and <= 8;
            bool orange = room == 11;
            var orangeFrames = new System.Collections.Generic.HashSet<string>();
            var normal = yellow ? "YGTerminal_0" : "BlueTerminal_Norm";
            var blink = yellow ? "YGTerminal_1" : "BlueTerminal_Blink";
            Assert.AreEqual(1, art.GetComponents<SpriteRenderer>().Length);
            Assert.AreEqual(1, animator.transform.parent.GetComponentsInChildren<Animator>(true).Length);
            var firstCycle = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            var changes = 0;
            string previous = null;
            float deadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                Assert.IsNotNull(art.sprite);
                if (orange)
                {
                    Assert.IsNull(accent);
                    StringAssert.StartsWith("OrangeTerminal_", art.sprite.name);
                    orangeFrames.Add(art.sprite.name);
                }
                else
                {
                    Assert.IsTrue(art.sprite.name == normal || art.sprite.name == blink);
                    Assert.AreEqual(art.sprite.name == blink, accent.enabled);
                }
                Assert.AreEqual(position, art.transform.position);
                Assert.AreEqual(scale, art.transform.lossyScale);
                // The Editor suite checks visible alpha bounds for every Orange frame.
                if (!orange)
                {
                    float baseY = art.transform.TransformPoint(0f, -art.sprite.pivot.y / art.sprite.pixelsPerUnit, 0f).y;
                    Assert.AreEqual(support.bounds.max.y, baseY, .001f);
                }
                if (previous != null && previous != art.sprite.name) changes++;
                previous = art.sprite.name;
                if (changes >= 4 && (!orange || orangeFrames.Count == 6) &&
                    animator.GetCurrentAnimatorStateInfo(0).normalizedTime > firstCycle + 1f) break;
            }
            Assert.GreaterOrEqual(changes, 4, "Both visible frames advance across repeated cycles");
            if (orange) Assert.AreEqual(6, orangeFrames.Count);
            Assert.Greater(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, firstCycle + 1f);
        }

        private static IEnumerator LoadRoom(int room)
        {
            EditorSceneManager.LoadSceneInPlayMode($"Assets/Game/Scenes/Levels/Level{room:00}.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return null;
        }

        private static Animator TerminalArt() => Object.FindObjectsByType<Animator>(FindObjectsSortMode.None)
            .Single(animator => animator.name == "Environment terminal art");

        private static BoxCollider2D Support(Animator art)
        {
            var renderer = art.GetComponent<SpriteRenderer>();
            return Object.FindObjectsByType<BoxCollider2D>(FindObjectsSortMode.None).Single(shape => shape.enabled &&
                !shape.isTrigger && shape.bounds.min.x <= art.transform.position.x && shape.bounds.max.x >= art.transform.position.x &&
                Mathf.Abs(shape.bounds.max.y - renderer.bounds.min.y) < .001f);
        }

        private static void Place(Rigidbody2D body, float x, float y)
        {
            body.position = new Vector2(x, y);
            body.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
        }

        private void Press(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));

        private static IEnumerator Until(Func<bool> ready, string description)
        {
            yield return new WaitUntil(ready, TimeSpan.FromSeconds(5), () => Assert.Fail(description));
        }
    }
}
#endif

