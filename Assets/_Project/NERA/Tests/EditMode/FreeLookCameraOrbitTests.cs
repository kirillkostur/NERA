using NERA.CameraSystem;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;

namespace NERA.Tests
{
    public sealed class FreeLookCameraOrbitTests
    {
        [Test]
        public void PlayerPrefabFreeLookHasOrbitController()
        {
            const string playerPrefabPath =
                "Assets/_Project/NERA/Prefabs/Player/Player.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                playerPrefabPath);

            Assert.That(prefab, Is.Not.Null);
            Transform freeLook = prefab.transform.Find("FreeLookCam");
            Assert.That(freeLook, Is.Not.Null);
            Assert.That(
                freeLook.GetComponent<FreeLookCameraOrbitController>(),
                Is.Not.Null);
        }

        [Test]
        public void HigherPriorityZoneWinsAndExitRestoresPreviousOrbits()
        {
            GameObject cameraObject = new GameObject("Test_FreeLookCamera");
            GameObject corridorZone = new GameObject("Test_CorridorZone");
            GameObject narrowZone = new GameObject("Test_NarrowZone");
            FreeLookCameraOrbitProfile corridorProfile =
                ScriptableObject.CreateInstance<FreeLookCameraOrbitProfile>();
            FreeLookCameraOrbitProfile narrowProfile =
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

                corridorProfile.Configure(
                    new FreeLookOrbitSettings(4f, 1f),
                    new FreeLookOrbitSettings(2.5f, 3f),
                    new FreeLookOrbitSettings(1f, 2f),
                    0f,
                    0f);
                narrowProfile.Configure(
                    new FreeLookOrbitSettings(3f, 0.75f),
                    new FreeLookOrbitSettings(2f, 2f),
                    new FreeLookOrbitSettings(0.5f, 1.25f),
                    0f,
                    0f);

                Assert.That(controller.EnterZone(
                    corridorZone,
                    corridorProfile,
                    0), Is.True);
                AssertOrbit(controller, 1, 2.5f, 3f);

                Assert.That(controller.EnterZone(
                    narrowZone,
                    narrowProfile,
                    10), Is.True);
                AssertOrbit(controller, 1, 2f, 2f);

                controller.ExitZone(narrowZone);
                AssertOrbit(controller, 1, 2.5f, 3f);

                controller.ExitZone(corridorZone);
                AssertOrbit(controller, 0, 5f, 1.5f);
                AssertOrbit(controller, 1, 3f, 6f);
                AssertOrbit(controller, 2, 1f, 3.5f);
                Assert.That(controller.ActiveZoneCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(narrowProfile);
                Object.DestroyImmediate(corridorProfile);
                Object.DestroyImmediate(narrowZone);
                Object.DestroyImmediate(corridorZone);
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void TransitionUsesSmoothStepForEntryAndExit()
        {
            GameObject cameraObject = new GameObject("Test_FreeLookCamera");
            GameObject zoneObject = new GameObject("Test_SmoothZone");
            FreeLookCameraOrbitProfile profile =
                ScriptableObject.CreateInstance<FreeLookCameraOrbitProfile>();

            try
            {
                CinemachineFreeLook freeLook =
                    cameraObject.AddComponent<CinemachineFreeLook>();
                freeLook.m_Orbits = CreateOrbits(
                    new FreeLookOrbitSettings(5f, 5f),
                    new FreeLookOrbitSettings(3f, 6f),
                    new FreeLookOrbitSettings(1f, 3f));

                FreeLookCameraOrbitController controller =
                    cameraObject.AddComponent<FreeLookCameraOrbitController>();
                controller.CaptureDefaultOrbits();
                profile.Configure(
                    new FreeLookOrbitSettings(3f, 3f),
                    new FreeLookOrbitSettings(1f, 2f),
                    new FreeLookOrbitSettings(0f, 1f),
                    1f,
                    2f);

                controller.EnterZone(zoneObject, profile, 0);
                Assert.That(controller.IsTransitioning, Is.True);
                AssertOrbit(controller, 1, 3f, 6f);

                controller.AdvanceTransition(0.5f);
                AssertOrbit(controller, 1, 2f, 4f);

                controller.AdvanceTransition(0.5f);
                AssertOrbit(controller, 1, 1f, 2f);
                Assert.That(controller.IsTransitioning, Is.False);

                controller.ExitZone(zoneObject);
                Assert.That(controller.IsTransitioning, Is.True);
                controller.AdvanceTransition(1f);
                AssertOrbit(controller, 1, 2f, 4f);

                controller.AdvanceTransition(1f);
                AssertOrbit(controller, 1, 3f, 6f);
                Assert.That(controller.IsTransitioning, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(profile);
                Object.DestroyImmediate(zoneObject);
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void MostRecentlyEnteredZoneWinsWhenPriorityMatches()
        {
            GameObject cameraObject = new GameObject("Test_FreeLookCamera");
            GameObject firstZone = new GameObject("Test_FirstZone");
            GameObject secondZone = new GameObject("Test_SecondZone");
            FreeLookCameraOrbitProfile firstProfile = CreateProfile(4f);
            FreeLookCameraOrbitProfile secondProfile = CreateProfile(2f);

            try
            {
                CinemachineFreeLook freeLook =
                    cameraObject.AddComponent<CinemachineFreeLook>();
                freeLook.m_Orbits = CreateOrbits(
                    new FreeLookOrbitSettings(5f, 5f),
                    new FreeLookOrbitSettings(3f, 3f),
                    new FreeLookOrbitSettings(1f, 1f));

                FreeLookCameraOrbitController controller =
                    cameraObject.AddComponent<FreeLookCameraOrbitController>();
                controller.CaptureDefaultOrbits();

                controller.EnterZone(firstZone, firstProfile, 0);
                controller.EnterZone(secondZone, secondProfile, 0);
                AssertOrbit(controller, 1, 2f, 2f);

                controller.ExitZone(secondZone);
                AssertOrbit(controller, 1, 4f, 4f);
            }
            finally
            {
                Object.DestroyImmediate(secondProfile);
                Object.DestroyImmediate(firstProfile);
                Object.DestroyImmediate(secondZone);
                Object.DestroyImmediate(firstZone);
                Object.DestroyImmediate(cameraObject);
            }
        }

        private static FreeLookCameraOrbitProfile CreateProfile(float value)
        {
            var profile =
                ScriptableObject.CreateInstance<FreeLookCameraOrbitProfile>();
            var orbit = new FreeLookOrbitSettings(value, value);
            profile.Configure(orbit, orbit, orbit, 0f, 0f);
            return profile;
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
