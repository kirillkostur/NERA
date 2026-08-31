using System.Collections;
using NERA.Items;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NERA.Tests
{
    public sealed class WorldItemPhysicsPlayModeTests
    {
        private GameObject floor;
        private GameObject itemObject;

        [TearDown]
        public void TearDown()
        {
            if (itemObject != null)
                Object.DestroyImmediate(itemObject);
            if (floor != null)
                Object.DestroyImmediate(floor);
        }

        [UnityTest]
        public IEnumerator DroppedItemFreezesImmediatelyOnFirstGroundContact()
        {
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "WorldItemPhysicsTestFloor";
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(10f, 1f, 10f);

            itemObject = new GameObject("DroppedWorldItemPhysicsTest");
            itemObject.transform.position = new Vector3(0f, 1f, 0f);
            itemObject.AddComponent<BoxCollider>();
            WorldItem item = itemObject.AddComponent<WorldItem>();

            Rigidbody body = itemObject.GetComponent<Rigidbody>();
            Assert.That(body, Is.Not.Null);

            item.ActivateDropPhysics();
            Assert.That(body.useGravity, Is.True);
            Assert.That(body.isKinematic, Is.False);

            float initialHeight = itemObject.transform.position.y;
            float activatedAt = Time.time;
            float timeoutAt = Time.realtimeSinceStartup + 3f;
            while (!body.isKinematic &&
                   Time.realtimeSinceStartup < timeoutAt)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(
                itemObject.transform.position.y,
                Is.LessThan(initialHeight));
            Assert.That(
                body.isKinematic,
                Is.True,
                "Rigidbody did not lock on its first supported contact.");
            Assert.That(
                Time.time - activatedAt,
                Is.LessThan(1f),
                "WorldItem remained dynamic after reaching the ground.");
            Assert.That(body.useGravity, Is.False);
            Assert.That(body.linearVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(body.angularVelocity, Is.EqualTo(Vector3.zero));
        }
    }
}
