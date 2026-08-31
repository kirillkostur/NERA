using NERA.Items;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NERA.Tests
{
    public sealed class WorldItemPhysicsTests
    {
        [Test]
        public void EveryWorldItemPrefabHasRestingRigidbody()
        {
            string[] prefabGuids = AssetDatabase.FindAssets(
                "t:Prefab",
                new[] { "Assets/_Project/NERA" });
            int worldItemCount = 0;

            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                foreach (WorldItem item in
                         prefab.GetComponentsInChildren<WorldItem>(true))
                {
                    worldItemCount++;
                    Rigidbody body = item.GetComponent<Rigidbody>();
                    Assert.That(
                        body,
                        Is.Not.Null,
                        $"{path}: WorldItem requires a Rigidbody.");
                    Assert.That(
                        body.useGravity,
                        Is.False,
                        $"{path}: authored WorldItem must not fall on load.");
                    Assert.That(
                        body.isKinematic,
                        Is.True,
                        $"{path}: authored WorldItem must start kinematic.");
                    Assert.That(
                        item.GetComponent<Collider>(),
                        Is.Not.Null,
                        $"{path}: WorldItem requires a Collider.");
                }
            }

            Assert.That(
                worldItemCount,
                Is.GreaterThan(0),
                "No WorldItem prefabs were found.");
        }
    }
}
