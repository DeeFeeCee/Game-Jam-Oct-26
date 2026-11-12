#if UNITY_EDITOR
using System;
using NUnit.Framework.Interfaces;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestRunner;
using Object = UnityEngine.Object;

[assembly: TestRunCallback(typeof(GameJam.UI.Tests.PlayModeAudioFixture))]

namespace GameJam.UI.Tests
{
    public sealed class PlayModeAudioFixture : ITestRunCallback
    {
        // AudioManager is in Unity's predefined assembly, which test asmdefs cannot reference.
        private static readonly Type ManagerType = Type.GetType("AudioManager, Assembly-CSharp");
        private static PlayModeAudioFixture activeRun;
        private AudioListener fixtureListener;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearPreviousRun() => activeRun?.Dispose();

        public void RunStarted(ITest testsToRun)
        {
            if (!Application.isPlaying) return;
            ClearPreviousRun();
            activeRun = this;

            var fixture = new GameObject("PlayMode test audio listener");
            fixture.SetActive(false);
            Object.DontDestroyOnLoad(fixture);
            fixtureListener = fixture.AddComponent<AudioListener>();
            SceneManager.activeSceneChanged += ActiveSceneChanged;
            SceneManager.sceneLoaded += SceneLoaded;
            SceneManager.sceneUnloaded += SceneUnloaded;
            RefreshListener();
        }

        public void RunFinished(ITestResult testResults) => Dispose();

        private void Dispose()
        {
            SceneManager.activeSceneChanged -= ActiveSceneChanged;
            SceneManager.sceneLoaded -= SceneLoaded;
            SceneManager.sceneUnloaded -= SceneUnloaded;
            if (fixtureListener != null) Object.Destroy(fixtureListener.gameObject);
            fixtureListener = null;
            if (activeRun == this) activeRun = null;
        }

        public void TestStarted(ITest test) { }
        public void TestFinished(ITestResult result) { }

        private void ActiveSceneChanged(Scene previous, Scene current)
        {
            if (IsInMemoryScene(current))
                DisposePersistentMusic();
            RefreshListener();
        }

        private void SceneLoaded(Scene scene, LoadSceneMode mode) => RefreshListener();
        private void SceneUnloaded(Scene scene) => RefreshListener();

        private static bool IsInMemoryScene(Scene scene) =>
            scene.IsValid() && string.IsNullOrEmpty(scene.path);

        private void RefreshListener()
        {
            if (fixtureListener == null) return;

            bool needsListener = IsInMemoryScene(SceneManager.GetActiveScene());
            if (needsListener)
            {
                foreach (AudioListener listener in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                {
                    if (listener == fixtureListener || !listener.isActiveAndEnabled) continue;
                    needsListener = false;
                    break;
                }
            }

            fixtureListener.gameObject.SetActive(needsListener);
        }

        private static void DisposePersistentMusic()
        {
            if (ManagerType == null) return;
            foreach (var component in Object.FindObjectsByType(ManagerType, FindObjectsSortMode.None))
            {
                var root = ((Component)component).gameObject;

                // Stop the stems before teardown unloads the outgoing camera.
                foreach (AudioSource track in root.GetComponentsInChildren<AudioSource>())
                    track.Stop();
                root.SetActive(false);
                Object.Destroy(root);
            }
        }
    }
}
#endif

