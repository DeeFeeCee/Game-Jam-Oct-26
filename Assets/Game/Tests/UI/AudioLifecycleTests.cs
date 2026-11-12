#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GameJam.UI.Tests
{
    public sealed class AudioLifecycleTests
    {
        private const string MainMenu = "Assets/Game/Scenes/MainMenu.unity";

        [UnityTest]
        public IEnumerator EmptyFixtureStopsPersistentMusicAndNextMenuStartsFresh()
        {
            LoadMenu();
            yield return WaitForTitle();
            Assert.That(ActiveListeners(), Has.Length.EqualTo(1));
            Assert.That(ActiveListeners()[0].gameObject.scene.path, Is.EqualTo(MainMenu));
            var firstSource = FindTitle();

            yield return ClearMenu();
            Assert.That(firstSource == null, Is.True, "The fixture must destroy the persistent music owner.");
            Assert.That(ActiveListeners(), Has.Length.EqualTo(1));
            LogAssert.NoUnexpectedReceived();

            LoadMenu();
            yield return WaitForTitle();
            Assert.That(ActiveListeners(), Has.Length.EqualTo(1));
            Assert.That(ActiveListeners()[0].gameObject.scene.path, Is.EqualTo(MainMenu));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (SceneManager.GetActiveScene().path == MainMenu)
                yield return ClearMenu();
        }

        private static void LoadMenu() =>
            EditorSceneManager.LoadSceneInPlayMode(MainMenu, new LoadSceneParameters(LoadSceneMode.Single));

        private static IEnumerator ClearMenu()
        {
            Scene menu = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("Audio lifecycle test cleanup"));
            yield return SceneManager.UnloadSceneAsync(menu);
            yield return null;
        }

        private static IEnumerator WaitForTitle()
        {
            float deadline = Time.realtimeSinceStartup + 8f;
            while (FindTitle() == null && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(FindTitle(), Is.Not.Null, "Expected the production menu track to play.");
        }

        private static AudioSource FindTitle() =>
            Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).FirstOrDefault(source =>
                source.isPlaying && source.volume > 0f && source.clip != null && source.clip.name == "Submergence (Title)");

        private static AudioListener[] ActiveListeners() =>
            Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Where(listener => listener.isActiveAndEnabled).ToArray();
    }
}
#endif
