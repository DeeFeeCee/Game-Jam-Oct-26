using System.Linq;
using GameJam.Levels;
using GameJam.LevelAuthoring.Editor;
using GameJam.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GameJam.BlueIntegration.Tests
{
    public sealed class BlueAssetTests
    {
        private const string Folder = "Assets/Game/Prefabs/BlueEnvironment/";
        [Test] public void EntranceIsVisualOnlyAndExitUsesAcceptedComponents()
        {
            var entrance = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "BlueEntrance.prefab");
            Assert.IsEmpty(entrance.GetComponentsInChildren<MonoBehaviour>(true)); Assert.IsEmpty(entrance.GetComponentsInChildren<Collider2D>(true));
            var exit = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "BlueExit.prefab");
            Assert.AreSame(exit.GetComponent<LevelTransition>(), exit.GetComponent<LevelExit>().transition);
            Assert.IsTrue(exit.GetComponent<BoxCollider2D>().isTrigger);
        }
        [Test] public void BlinkKeysResolveAndLoreStartsBlank()
        {
            var terminal = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "BlueTerminal.prefab");
            Assert.AreEqual("", terminal.GetComponent<ResearchTerminal>().lore.text);
            var animator = terminal.GetComponentInChildren<Animator>(); Assert.NotNull(animator.runtimeAnimatorController);
            var clip = animator.runtimeAnimatorController.animationClips.Single(); Assert.IsTrue(clip.isLooping);
            var binding = AnimationUtility.GetObjectReferenceCurveBindings(clip).Single(); Assert.AreEqual("", binding.path);
            var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            Assert.AreEqual(2, keys.Select(k => k.value).Distinct().Count()); Assert.IsTrue(keys.All(k => k.value is Sprite));
            Assert.NotNull(animator.GetComponent<SpriteRenderer>());
        }
        [Test] public void PlatformArtAndResizePreserveExpectedColliderGeometry()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(LevelAuthoring32x18.Platform);
            foreach (var sprite in AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Levels/BluePlatforms.png").OfType<Sprite>())
            {
                var obj = Object.Instantiate(source);
                try
                {
                    var collider = obj.GetComponent<BoxCollider2D>(); string before = EditorJsonUtility.ToJson(collider);
                    BluePlatformAuthoring.Apply(obj, sprite); Assert.AreEqual(before, EditorJsonUtility.ToJson(collider));
                    LevelAuthoring32x18.SetGeometry(obj, new Vector2Int(2, 3), new Vector2Int(7, 2));
                    Physics2D.SyncTransforms();
                    Assert.AreEqual(new Vector2(7, 2), collider.size);
                    var art = obj.transform.Find(BluePlatformAuthoring.ArtName).GetComponent<SpriteRenderer>();
                    Assert.AreEqual(7, art.size.x * art.transform.localScale.x, .001);
                    Assert.AreEqual(100, art.sprite.pixelsPerUnit); Assert.AreEqual(SpriteDrawMode.Tiled, art.drawMode);
                    Assert.AreEqual(collider.bounds.max.y, art.bounds.max.y, .001);
                }
                finally { Object.DestroyImmediate(obj); }
            }
        }
    }
}
