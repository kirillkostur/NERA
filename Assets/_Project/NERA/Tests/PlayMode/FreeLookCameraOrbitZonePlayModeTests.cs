using System.Collections;
using NERA.CameraSystem;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.TestTools;

namespace NERA.Tests
{
    public sealed class FreeLookCameraOrbitZonePlayModeTests
    {
        [UnityTest]
        public IEnumerator TriggerAppliesProfileAndExitRestoresCamera()
        {
            GameObject cameraObject = new GameObject("Test_FreeLookCamera");
            GameObject zoneObject = new GameObject("Test_CameraOrbitZone");
            GameObject playerObject = new GameObject("Test_Player");
            FreeLookCameraOrbitProfile profile =
                ScriptableObject.CreateInstance<FreeLookCameraOrbitProfile>();

            try
            {
                CinemachineFreeLook freeLook =
                    cameraObject.AddComponent<CinemachineFreeLook>();
                freeLook.m_Orbits = CreateOrbits(
                    new FreeLookOrbitSettings(5f, 1.5f),
                    new FreeLookOrbitSettings(3f, 6f),
                    new FreeLookOrbitSettings(1f, 3.5f));

                FreeLookCameraOrbitController controller =
                    cameraObject.AddComponent<FreeLookCameraOrbitController>();
                controller.CaptureDefaultOrbits();

                profile.Configure(
                    new FreeLookOrbitSettings(3f, 1f),
                    new FreeLookOrbitSettings(2f, 2.5f),
                    new FreeLookOrbitSettings(0.5f, 1.5f),
                    0.15f,
                    0.15f);

                BoxCollider zoneCollider =
                    zoneObject.AddComponent<BoxCollider>();
                zoneCollider.size = Vector3.one * 4f;
                FreeLookCameraOrbitZone zone =
                    zoneObject.AddComponent<FreeLookCameraOrbitZone>();
                zone.Configure(profile, 0, controller);

                playerObject.tag = "Player";
                playerObject.transform.position = Vector3.left * 10f;
                playerObject.AddComponent<SphereCollider>();
                Rigidbody body = playerObject.AddComponent<Rigidbody>();
                body.useGravity = false;
                body.isKinematic = true;

                yield return new WaitForFixedUpdate();

                body.position = Vector3.zero;
                Physics.SyncTransforms();
                yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate();

                Assert.That(controller.IsTransitioning, Is.True);
                yield return new WaitForSeconds(0.2f);
                AssertOrbit(controller, 1, 2f, 2.5f);
                Assert.That(controller.ActiveZoneCount, Is.EqualTo(1));

                body.position = Vector3.right * 10f;
                Physics.SyncTransforms();
                yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate();

                Assert.That(controller.IsTransitioning, Is.True);
                yield return new WaitForSeconds(0.2f);
                AssertOrbit(controller, 0, 5f, 1.5f);
                AssertOrbit(controller, 1, 3f, 6f);
                AssertOrbit(controller, 2, 1f, 3.5f);
                Assert.That(controller.ActiveZoneCount, Is.Zero);
            }
            finally
            {
                Object.Destroy(profile);
                Object.Destroy(playerObject);
                Object.Destroy(zoneObject);
                Object.Destroy(cameraObject);
            }
        }

        private static CinemachineFreeLook.Orbit[] CreateOrbits(
            FreeLookOrbitSettings top,
            FreeLookOrbitSettings middle,
            FreeLookOrbitSettings bottom)
        {
            FreeLookOrbitSettings[] settings = { top, middle, bottom };
            var result = new CinemachineFreeLook.Orbit[settings.Length];
            for (int i = 0; i < settings.Length; i++)
            {
                result[i].m_Height = settings[i].Height;
                result[i].m_Radius = settings[i].Radius;
            }

            return result;
        }

        private static void AssertOrbit(
            FreeLookCameraOrbitController controller,
            int index,
            float height,
            float radius)
        {
            FreeLookOrbitSettings orbit = controller.GetCurrentOrbit(index);
            Assert.That(orbit.Height, Is.EqualTo(height).Within(0.001f));
            Assert.That(orbit.Radius, Is.EqualTo(radius).Within(0.001f));
        }
    }
}
