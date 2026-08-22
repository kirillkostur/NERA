using System.Linq;
using System.Reflection;
using NERA.Drone;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace NERA.Tests
{
    public sealed class DroneAnimationTests
    {
        [TestCase(
            "Assets/_Project/NERA/Art/Animations/Station_Drone.controller",
            DroneAnimationView.MainControllerName,
            DroneAnimationView.MainLaunchStateName,
            DroneAnimationView.MainReturnStateName)]
        [TestCase(
            "Assets/_Project/NERA/Art/Animations/Station_Mini_Drone.controller",
            DroneAnimationView.MiniControllerName,
            DroneAnimationView.MiniLaunchStateName,
            DroneAnimationView.MiniReturnStateName)]
        public void DroneControllerHasExpectedOneShotStates(
            string assetPath,
            string controllerName,
            string launchStateName,
            string returnStateName)
        {
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(assetPath);
            Assert.That(controller, Is.Not.Null, assetPath);
            Assert.That(controller.name, Is.EqualTo(controllerName));
            Assert.That(
                DroneAnimationView.SupportsController(controller),
                Is.True);

            string[] stateNames = controller.layers[0].stateMachine.states
                .Select(child => child.state.name)
                .ToArray();
            Assert.That(stateNames, Does.Contain(launchStateName));
            Assert.That(stateNames, Does.Contain(returnStateName));

            ChildAnimatorState launchState =
                controller.layers[0].stateMachine.states.Single(
                    child => child.state.name == launchStateName);
            ChildAnimatorState returnState =
                controller.layers[0].stateMachine.states.Single(
                    child => child.state.name == returnStateName);
            Assert.That(launchState.state.speed, Is.EqualTo(1f));
            Assert.That(returnState.state.speed, Is.EqualTo(1f));

            AnimationClip launchClip = controller.animationClips.Single(
                clip => clip.name == launchStateName);
            AnimationClip returnClip = controller.animationClips.Single(
                clip => clip.name == returnStateName);
            Assert.That(launchClip.isLooping, Is.False);
            Assert.That(returnClip.isLooping, Is.False);
        }

        [TestCase(
            "Assets/_Project/NERA/Art/Animations/Dron_Start.anim",
            "Start_Scan")]
        [TestCase(
            "Assets/_Project/NERA/Art/Animations/Dron_End.anim",
            "End_Scan")]
        public void MainDroneClipContainsExpeditionEvent(
            string assetPath,
            string expectedFunction)
        {
            AnimationClip clip =
                AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            Assert.That(clip, Is.Not.Null, assetPath);
            Assert.That(
                AnimationUtility.GetAnimationEvents(clip)
                    .Select(animationEvent => animationEvent.functionName),
                Does.Contain(expectedFunction));
        }

        [Test]
        public void NonExpeditionScanCompleteKeepsDroneAtHomePose()
        {
            ResetDroneStatics();
            GameObject controllerObject = null;
            GameObject viewObject = null;
            try
            {
                controllerObject = new GameObject("Test_DroneController");
                DroneScanController drone =
                    controllerObject.AddComponent<DroneScanController>();

                viewObject = new GameObject("Test_DroneView");
                Animator animator = viewObject.AddComponent<Animator>();
                animator.runtimeAnimatorController =
                    AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                        "Assets/_Project/NERA/Art/Animations/" +
                        "Station_Drone.controller");
                animator.Rebind();
                animator.Update(0f);
                DroneAnimationView view =
                    viewObject.AddComponent<DroneAnimationView>();

                drone.RestoreBatteryCharge(drone.BatteryCapacity * 0.5f);
                SetPrivateField(view, "animator", animator);
                SetPrivateField(view, "controller", drone);
                InvokePrivate(view, "ConfigureStateNames");
                InvokePrivate(view, "ApplyState", drone.State);

                Assert.That(drone.State, Is.EqualTo(DroneState.ScanComplete));
                Assert.That(drone.IsExpeditionInProgress, Is.False);
                Assert.That(animator.speed, Is.EqualTo(0f));
            }
            finally
            {
                if (viewObject != null)
                    Object.DestroyImmediate(viewObject);
                if (controllerObject != null)
                    Object.DestroyImmediate(controllerObject);
                ResetDroneStatics();
            }
        }

        private static void ResetDroneStatics()
        {
            typeof(DroneScanController).GetMethod(
                    "ResetStatics",
                    BindingFlags.Static | BindingFlags.NonPublic)
                ?.Invoke(null, null);
        }

        private static void SetPrivateField(
            object target,
            string fieldName,
            object value)
        {
            target.GetType().GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(target, value);
        }

        private static void InvokePrivate(
            object target,
            string methodName,
            params object[] arguments)
        {
            target.GetType().GetMethod(
                    methodName,
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(target, arguments);
        }
    }
}
