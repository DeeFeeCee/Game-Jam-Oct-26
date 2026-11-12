using System;
using GameJam.Movement;
using GameJam.Resources;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameJam.Levels
{
    /// <summary>Owns the current attempt and observes successful campaign exits.</summary>
    public sealed class RunTimer : MonoBehaviour
    {
        private const string LevelFolder = "Assets/Game/Scenes/Levels/";
        private const string Prologue = "Assets/Game/Scenes/Intro/NarrativePrologue.unity";
        private const string Ending = "Assets/Game/Scenes/Ending/EndingSequence.unity";
        private const int FinalLevel = 12;

        private enum Phase { None, Armed, Playing, Loading, Completed }

        public static RunTimer Instance { get; private set; }
        private static RunBestTime best = new RunBestTime(RunBestTime.ProductionKey);
        public static int BestMilliseconds => best.Read();

        private Phase phase;
        private int level;
        private Scene room;
        private PlayerMovement2D player;
        private RunDashBank bank;
        private bool focused;
        private bool sampledGameplay;
        private double lastSample;
        private double elapsed;

        public long ElapsedMilliseconds => (long)Math.Floor(elapsed * 1000d);
        public long CompletedMilliseconds { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            best = new RunBestTime(RunBestTime.ProductionKey);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (Instance == null) new GameObject("Campaign Run Timer").AddComponent<RunTimer>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            focused = Application.isFocused;
            DontDestroyOnLoad(gameObject);
            SceneManager.activeSceneChanged += OnSceneChanged;
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            SceneManager.activeSceneChanged -= OnSceneChanged;
            Instance = null;
        }

        public void ArmNewRun()
        {
            Abandon();
            phase = Phase.Armed;
        }

        public void Abandon()
        {
            phase = Phase.None;
            room = default;
            level = 0;
            player = null;
            bank = null;
            elapsed = 0;
            CompletedMilliseconds = 0;
            ResetSample();
        }

        private void OnSceneChanged(Scene previous, Scene next)
        {
            ResetSample();
            if (phase == Phase.Completed && next.path == Ending) return;
            if (phase == Phase.Armed && (next.path == Prologue || next.path == RoomPath(1))) return;
            if (phase == Phase.Loading && next.path == RoomPath(level + 1)) return;
            // A checkpoint retry replaces the active scene with another instance of the same room.
            if (phase == Phase.Playing && next.path == RoomPath(level)) return;
            Abandon();
        }

        internal void EnterRoom(Scene scene, PlayerMovement2D actor, RunDashBank activeBank, bool retry)
        {
            if (scene != SceneManager.GetActiveScene() || actor == null || activeBank == null) return;
            if (phase == Phase.Playing && room == scene && player == actor) return;

            if (phase == Phase.Armed && scene.path == RoomPath(1))
                level = 1;
            else if (phase == Phase.Loading && scene.path == RoomPath(level + 1) && bank == activeBank)
                level++;
            else if (!(phase == Phase.Playing && retry && scene.path == RoomPath(level) && bank == activeBank))
            {
                Abandon();
                return;
            }

            phase = Phase.Playing;
            room = scene;
            player = actor;
            bank = activeBank;
            ResetSample();
        }

        internal void AcceptExit(Scene source, PlayerMovement2D actor, string destination)
        {
            if (phase != Phase.Playing || source != room || source != SceneManager.GetActiveScene() || actor != player)
                return;
            // The accepted exit freezes the actor before requesting the asynchronous load.
            Sample(focused && Time.timeScale > 0f && bank != null && !bank.IsGameOver);
            if (level == FinalLevel && destination == Ending)
            {
                phase = Phase.Completed;
                CompletedMilliseconds = Math.Max(1, ElapsedMilliseconds);
                try { best.TryImprove(CompletedMilliseconds); }
                catch (Exception exception)
                {
                    Debug.LogWarning("Could not save the best completion time: " + exception.Message);
                }
            }
            else if (level < FinalLevel && destination == RoomPath(level + 1))
                phase = Phase.Loading;
            else
                Abandon();
            ResetSample();
        }

        private void LateUpdate()
        {
            Sample(phase == Phase.Playing && focused && Time.timeScale > 0f &&
                room == SceneManager.GetActiveScene() && player != null && player.isActiveAndEnabled &&
                bank != null && bank == RunDashBank.Instance && !bank.IsGameOver);
        }

        private void OnApplicationFocus(bool value)
        {
            focused = value;
            ResetSample();
        }

        private void Sample(bool gameplay)
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (gameplay && sampledGameplay) elapsed += Math.Max(0d, now - lastSample);
            sampledGameplay = gameplay;
            lastSample = now;
        }

        private void ResetSample() => sampledGameplay = false;

        private static string RoomPath(int number) => $"{LevelFolder}Level{number:D2}.unity";

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static void UseVerificationStore(Guid? session)
        {
            best = new RunBestTime(session.HasValue ? "PIDR.Verification.BestCompletionTimeMs." + session.Value.ToString("N") :
                RunBestTime.ProductionKey);
        }
#endif
    }
}
