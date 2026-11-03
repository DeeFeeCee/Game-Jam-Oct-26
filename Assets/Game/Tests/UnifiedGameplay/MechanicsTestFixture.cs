#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using GameJam.Movement;
using GameJam.Mechanics;
using GameJam.Resources;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GameJam.UnifiedGameplay.Tests
{
    public abstract class MechanicsTestFixture
    {
        protected readonly List<GameObject> Objects = new List<GameObject>();
        protected RunDashBank Bank => RunDashBank.Instance;
        protected GameObject Own(GameObject obj) { Objects.Add(obj); return obj; }
        protected PlayerMovement2D Player(Vector2 position, float gravity = 0)
        {
            var obj = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/Player.prefab"), position, Quaternion.identity));
            obj.GetComponent<PlayerMovementInput2D>().enabled = false;
            obj.GetComponent<Rigidbody2D>().gravityScale = gravity;
            return obj.GetComponent<PlayerMovement2D>();
        }
        protected BoxCollider2D Solid(Vector2 center, Vector2 size)
        {
            var obj = Own(new GameObject("Test solid", typeof(BoxCollider2D))); obj.transform.position = center;
            var box = obj.GetComponent<BoxCollider2D>(); box.size = size; return box;
        }
        protected static IEnumerator Ticks(int count) { for (int i = 0; i < count; i++) yield return new WaitForFixedUpdate(); yield return null; }
        [UnitySetUp] public IEnumerator Begin()
        {
            Assert.IsNull(Bank);
            Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/RunDashBank.prefab"));
            yield return null;
        }
        [UnityTearDown] public IEnumerator End()
        {
            Time.timeScale = 1;
            foreach (var obj in Objects) if (obj != null) Object.Destroy(obj); Objects.Clear();
            foreach (var shot in Object.FindObjectsByType<TurretProjectile2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.Destroy(shot.gameObject);
            if (Bank != null) Object.Destroy(Bank.gameObject);
            yield return null; LogAssert.NoUnexpectedReceived();
        }
    }
}
#endif
