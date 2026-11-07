using System;
using System.IO;
using System.Linq;
using GameJam.LevelAuthoring.Editor;
using GameJam.Levels;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace GameJam.LevelAuthoring.Tests
{
    public sealed class LevelAuthoringTests
    {
        [TestCase("../Level02")]
        [TestCase("Level02.unity")]
        [TestCase("folder/Level02")]
        [TestCase("LevelTemplate32x18")]
        public void InvalidDestinationsAreRejected(string name) => Assert.Throws<ArgumentException>(() => LevelAuthoring32x18.DestinationPath(name));

        [Test] public void NumberHasPredictableLevelName() => Assert.AreEqual("Assets/Game/Scenes/Levels/Level02.unity", LevelAuthoring32x18.DestinationPath("2"));

        [Test] public void CopyGetsIndependentGuidAndPreservesTemplateAndReferences()
        {
            string name = "LevelAudit" + Guid.NewGuid().ToString("N");
            string path = LevelAuthoring32x18.DestinationPath(name);
            byte[] original = File.ReadAllBytes(LevelAuthoring32x18.Template);
            Scene opened = default;
            try
            {
                Assert.AreEqual(path, LevelAuthoring32x18.CreateFromTemplate(name, false));
                Assert.AreNotEqual(AssetDatabase.AssetPathToGUID(path), AssetDatabase.AssetPathToGUID(LevelAuthoring32x18.Template));
                CollectionAssert.AreEqual(original, File.ReadAllBytes(path));
                opened = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                var setup = opened.GetRootGameObjects().Single(r => r.GetComponent<LevelSetup>() != null).GetComponent<LevelSetup>();
                Assert.AreEqual("Assets/Game/Prefabs/Player.prefab", AssetDatabase.GetAssetPath(setup.playerPrefab));
                Assert.AreEqual("Assets/Game/Prefabs/RunDashBank.prefab", AssetDatabase.GetAssetPath(setup.bankPrefab));
                Assert.NotNull(setup.spawn);
                Assert.AreEqual(4, opened.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BoxCollider2D>()).Count(c => !c.isTrigger));
            }
            finally
            {
                if (opened.IsValid() && opened.isLoaded) EditorSceneManager.CloseScene(opened, true);
                if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
            }
            CollectionAssert.AreEqual(original, File.ReadAllBytes(LevelAuthoring32x18.Template));
        }

        [Test] public void ExistingOfficialSceneCannotBeOverwritten()
        {
            byte[] original = File.ReadAllBytes(LevelAuthoring32x18.Level01);
            string guid = AssetDatabase.AssetPathToGUID(LevelAuthoring32x18.Level01);
            Assert.Throws<IOException>(() => LevelAuthoring32x18.CreateFromTemplate("Level01", false));
            CollectionAssert.AreEqual(original, File.ReadAllBytes(LevelAuthoring32x18.Level01));
            Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(LevelAuthoring32x18.Level01));
        }

        [Test] public void RegistrationAppendsWithoutChangingMenuOrExistingOrder()
        {
            const string newLevel = "Assets/Game/Scenes/Levels/RegistrationFixture.unity";
            var before = EditorBuildSettings.scenes;
            var result = LevelAuthoring32x18.RegisteredScenes(before, newLevel);
            Assert.AreEqual(before.Length + 1, result.Length);
            CollectionAssert.AreEqual(before.Select(s => s.path), result.Take(before.Length).Select(s => s.path));
            Assert.AreEqual("Assets/Game/Scenes/MainMenu.unity", result[0].path); Assert.IsTrue(result[0].enabled);
            Assert.AreEqual(newLevel, result.Last().path); Assert.IsTrue(result.Last().enabled);
            // The pure preview function must not mutate Build Settings.
            CollectionAssert.AreEqual(before.Select(s => s.path), EditorBuildSettings.scenes.Select(s => s.path));
        }

        [Test] public void RegistrationDoesNotDuplicateAnExistingEntry()
        {
            var before = EditorBuildSettings.scenes;
            var result = LevelAuthoring32x18.RegisteredScenes(before, LevelAuthoring32x18.Level01);
            Assert.AreEqual(before.Length, result.Length); Assert.AreEqual(1, result.Count(s => s.path == LevelAuthoring32x18.Level01));
            Assert.IsTrue(result.Single(s => s.path == LevelAuthoring32x18.Level01).enabled);
        }

        [Test] public void TemplateAndReviewExampleCannotEnterThePlayableBuildList()
        {
            Assert.Throws<ArgumentException>(() => LevelAuthoring32x18.RegisteredScenes(EditorBuildSettings.scenes, LevelAuthoring32x18.Template));
            Assert.Throws<ArgumentException>(() => LevelAuthoring32x18.RegisteredScenes(EditorBuildSettings.scenes, LevelAuthoring32x18.Example));
            Assert.IsFalse(EditorBuildSettings.scenes.Any(s => s.path == LevelAuthoring32x18.Template || s.path == LevelAuthoring32x18.Example));
        }

        [Test] public void PlatformResizeAlignsAllGeometryAndSupportsUndo()
        {
            var scene = EditorSceneManager.OpenScene(LevelAuthoring32x18.Template, OpenSceneMode.Additive);
            try
            {
                var obj = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(LevelAuthoring32x18.Platform), scene);
                Vector3 originalPosition = obj.transform.position; Vector2 originalSize = obj.GetComponent<BoxCollider2D>().size;
                Undo.IncrementCurrentGroup();
                LevelAuthoring32x18.Resize(obj, new Vector2Int(5, 5), new Vector2Int(3, 2));
                Assert.AreEqual(new Vector3(6.5f, 6), obj.transform.position);
                Assert.AreEqual(new Vector2(3, 2), obj.GetComponent<BoxCollider2D>().size);
                Assert.AreEqual(new Vector2(3, 2), obj.transform.Find("Floor visuals").GetComponent<SpriteRenderer>().size);
                Assert.AreEqual(3, obj.transform.Find("Gameplay surface frame/Backing").GetComponent<SpriteRenderer>().bounds.size.x, .001f);
                Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
                Assert.AreEqual(originalPosition, obj.transform.position); Assert.AreEqual(originalSize, obj.GetComponent<BoxCollider2D>().size);
                Assert.AreEqual(originalSize, obj.transform.Find("Floor visuals").GetComponent<SpriteRenderer>().size);
                Assert.AreEqual(originalSize.x, obj.transform.Find("Gameplay surface frame/Backing").GetComponent<SpriteRenderer>().bounds.size.x, .001f);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        [Test] public void ResizingOutsideRoomRefusesBeforeChangingGeometry()
        {
            var scene = EditorSceneManager.OpenScene(LevelAuthoring32x18.Template, OpenSceneMode.Additive);
            try
            {
                var obj = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(LevelAuthoring32x18.Platform), scene);
                Vector3 original = obj.transform.position;
                Assert.Throws<ArgumentException>(() => LevelAuthoring32x18.Resize(obj, new Vector2Int(31, 5), new Vector2Int(4, 1)));
                Assert.AreEqual(original, obj.transform.position);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        [Test] public void OfficialLevelIdentityAndHistoricalFixturesRemainAvailable()
        {
            Assert.AreEqual("54cd109dfa8beb54088109b65f2d16b5", AssetDatabase.AssetPathToGUID(LevelAuthoring32x18.Level01));
            Assert.NotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(LevelAuthoring32x18.Legacy));
            Assert.NotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Game/Experiments/SingleScreen/Scenes/SingleScreen32x18.unity"));
        }

        [Test] public void ProductionToolsAndScenesHaveNoExperimentalDependencies()
        {
            foreach (var path in new[] { LevelAuthoring32x18.Level01, LevelAuthoring32x18.Template, LevelAuthoring32x18.Platform })
                Assert.IsFalse(AssetDatabase.GetDependencies(path, true).Any(p => p.StartsWith("Assets/Game/Experiments/", StringComparison.Ordinal)), path);
            Assert.IsFalse(File.ReadAllText("Assets/Game/Editor/Levels/GameJam.LevelAuthoring.Editor.asmdef").Contains("GameJam.SingleScreen"));
        }
    }
}
