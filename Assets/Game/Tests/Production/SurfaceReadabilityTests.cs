#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameJam.Foundation.Tests
{
    public sealed partial class FoundationTests
    {
        private static void CheckSurface(BoxCollider2D solid)
        {
            Transform frame=solid.transform.Find("Gameplay surface frame"); Assert.NotNull(frame,solid.name);
            Assert.AreEqual(5,frame.childCount); Assert.IsEmpty(frame.GetComponentsInChildren<Collider2D>());
            var backing=frame.Find("Backing").GetComponent<SpriteRenderer>();
            var top=frame.Find("Top").GetComponent<SpriteRenderer>();
            var art=solid.transform.Find("Floor visuals").GetComponent<SpriteRenderer>();
            Assert.AreEqual(art.sprite.name,"BlueLvlGround-newt");
            Assert.Less(backing.sortingOrder,art.sortingOrder); Assert.Greater(top.sortingOrder,art.sortingOrder);
            Assert.AreEqual(solid.bounds.center.x,backing.bounds.center.x,.001f);
            Assert.AreEqual(solid.bounds.center.y,backing.bounds.center.y,.001f);
            Assert.AreEqual(solid.bounds.size.x,backing.bounds.size.x,.001f);
            Assert.AreEqual(solid.bounds.size.y,backing.bounds.size.y,.001f);
            Assert.AreEqual(solid.bounds.max.y,top.bounds.max.y,.001f);
            Assert.AreEqual(.05f,top.bounds.size.y,.001f);
            Assert.Greater(top.color.grayscale,backing.color.grayscale+.5f);
        }
        [UnityTest] public IEnumerator AllIntendedLevelSolidsHaveAlignedForegroundEdges()
        {
            yield return Load(LegacyLevel);
            var solids=loaded.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<BoxCollider2D>())
                .Where(c=>!c.isTrigger&&c.attachedRigidbody==null).ToArray();
            Assert.AreEqual(5,solids.Length); foreach(var solid in solids) CheckSurface(solid);
        }
        [UnityTest] public IEnumerator DestinationSolidsUseSameReadableTreatment()
        {
            yield return Load(Destination);
            var solids=loaded.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<BoxCollider2D>())
                .Where(c=>!c.isTrigger&&c.attachedRigidbody==null).ToArray();
            Assert.AreEqual(3,solids.Length); foreach(var solid in solids) CheckSurface(solid);
        }
        [Test] public void ReusablePlatformHasTrimAndUnchangedCollisionSize()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/IndustrialPlatform.prefab");
            var collider=prefab.GetComponent<BoxCollider2D>();
            Assert.AreEqual(new Vector2(3.2f,1),collider.size); Assert.IsFalse(collider.isTrigger);
            Assert.NotNull(prefab.transform.Find("Gameplay surface frame/Top"));
        }
        [UnityTest] public IEnumerator IndustrialBackgroundRemainsSeparateFromGameplayTrim()
        {
            yield return Load(LegacyLevel);
            var background=loaded.GetRootGameObjects().Single(g=>g.name=="Blue industrial background");
            var renderer=background.GetComponent<SpriteRenderer>();
            Assert.AreEqual("BlueBG",renderer.sprite.name); Assert.AreEqual(-20,renderer.sortingOrder);
            Assert.AreEqual(new Vector2(40,11),renderer.size);
            Assert.IsEmpty(background.GetComponentsInChildren<Collider2D>());
            Assert.IsNull(background.transform.Find("Gameplay surface frame"));
        }
    }
}
#endif
