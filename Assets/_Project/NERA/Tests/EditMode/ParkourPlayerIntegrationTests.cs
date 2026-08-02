using System.Linq;
using System.Collections.Generic;
using Climbing;
using NERA.Combat;
using NERA.Interaction;
using NERA.Inventory;
using NERA.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace NERA.Tests
{
    public sealed class ParkourPlayerIntegrationTests
    {
        private const string PlayerPrefabPath =
            "Assets/_Project/NERA/Prefabs/Player/Player.prefab";

        [Test]
        public void PlayerPrefabContainsParkourAndNeraGameplayContract()
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            Assert.That(prefab, Is.Not.Null);

            ParkourPlayerBridge bridge =
                prefab.GetComponentInChildren<ParkourPlayerBridge>(true);
            Assert.That(bridge, Is.Not.Null);

            GameObject model = bridge.gameObject;
            Assert.That(model.name, Is.EqualTo("PlayerModel"));
            Assert.That(model.CompareTag("Player"), Is.True);
            Assert.That(model.layer, Is.EqualTo(3));
            Assert.That(model.GetComponent<InputCharacterController>(), Is.Not.Null);
            Assert.That(model.GetComponent<ThirdPersonController>(), Is.Not.Null);
            Assert.That(model.GetComponent<PlayerInteractionController>(), Is.Not.Null);
            Assert.That(model.GetComponent<PlayerInventory>(), Is.Not.Null);
            Assert.That(model.GetComponent<PlayerEquipmentController>(), Is.Not.Null);
            Assert.That(model.GetComponent<PlayerEnergyWeaponController>(), Is.Not.Null);
            Assert.That(model.GetComponent<PlayerHealth>(), Is.Not.Null);

            SerializedObject interaction = new SerializedObject(
                model.GetComponent<PlayerInteractionController>());
            Assert.That(
                interaction.FindProperty("overlapMask").intValue,
                Is.EqualTo((1 << 6) | (1 << 7)));
            Assert.That(
                interaction.FindProperty("obstructionMask").intValue,
                Is.EqualTo(
                    (1 << 0) | (1 << 9) | (1 << 10) | (1 << 11) |
                    (1 << 14) | (1 << 15)));

            Camera[] cameras = prefab.GetComponentsInChildren<Camera>(true);
            Assert.That(cameras, Has.Length.EqualTo(1));
            Assert.That(cameras[0].CompareTag("MainCamera"), Is.True);

            CapsuleCollider[] motorColliders =
                model.GetComponents<CapsuleCollider>();
            Assert.That(motorColliders, Has.Length.EqualTo(2));
            Assert.That(
                motorColliders.Count(collider => collider.enabled),
                Is.EqualTo(1),
                "Exactly one parkour motor capsule must be active.");
        }

        [Test]
        public void PlayerPrefabHasSeparateDisabledRagdoll()
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            ParkourPlayerBridge bridge =
                prefab.GetComponentInChildren<ParkourPlayerBridge>(true);
            Rigidbody motor = bridge.GetComponent<Rigidbody>();
            Rigidbody[] bodies =
                prefab.GetComponentsInChildren<Rigidbody>(true);
            Rigidbody[] ragdollBodies = bodies
                .Where(body => body != motor)
                .ToArray();

            Assert.That(ragdollBodies.Length, Is.GreaterThanOrEqualTo(12));
            Assert.That(
                prefab.GetComponentsInChildren<CharacterJoint>(true).Length,
                Is.GreaterThanOrEqualTo(11));
            Assert.That(ragdollBodies.All(body => body.isKinematic), Is.True);
            Assert.That(
                ragdollBodies
                    .SelectMany(body => body.GetComponents<Collider>())
                    .All(collider => !collider.enabled),
                Is.True);
        }

        [Test]
        public void EnergyWeaponHitsEnemiesButStopsAtWorldGeometry()
        {
            WeaponDefinition weapon =
                AssetDatabase.LoadAssetAtPath<WeaponDefinition>(
                    "Assets/_Project/NERA/Configs/Combat/" +
                    "Weapon_EnergyPistol_01.asset");
            Assert.That(weapon, Is.Not.Null);

            int requiredMask =
                (1 << 0) | (1 << 6) | (1 << 7) | (1 << 8) |
                (1 << 9) | (1 << 10) | (1 << 11) |
                (1 << 14) | (1 << 15);
            Assert.That(
                weapon.HitMask.value & requiredMask,
                Is.EqualTo(requiredMask));
            Assert.That(
                weapon.HitMask.value & (1 << 3),
                Is.Zero,
                "The camera ray must not hit the Player layer.");
        }

        [Test]
        public void MainSceneUsesOnlyNewPlayerPrefab()
        {
            string sceneText = System.IO.File.ReadAllText(
                "Assets/_Project/NERA/Scenes/MainScene.unity");

            Assert.That(
                sceneText,
                Does.Contain("9e70d0b595d510742bfdf66867f64069"));
            Assert.That(
                sceneText,
                Does.Not.Contain("b9cff2f4ac4fb314fa7f17d66fd3b797"));
            Assert.That(
                sceneText,
                Does.Not.Contain("93f3eefd1d7c8ce4a8f1318b0b62983b"));
            Assert.That(sceneText, Does.Not.Contain("m_Name: AimCrosshair"));
        }

        [Test]
        public void ParkourPointPrefabsUseDedicatedDetectionLayer()
        {
            string[] paths =
            {
                "Assets/_Project/NERA/Resources/Parkour/Climbing/GPoint.prefab",
                "Assets/_Project/NERA/Prefabs/Parkour/Jump/Jump Points.prefab",
            };

            foreach (string path in paths)
            {
                GameObject prefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null, path);
                Assert.That(
                    prefab.GetComponentsInChildren<Transform>(true)
                        .All(child => child.gameObject.layer == 16),
                    Is.True,
                    $"Every object in {path} must use ParkourPoint layer.");
            }
        }

        [Test]
        public void ParkourAnimatorHasNoOrphanedPackageBehaviours()
        {
            string controllerText = System.IO.File.ReadAllText(
                "Assets/_Project/NERA/Art/Parkour/Animator Controller.controller");

            Assert.That(
                controllerText,
                Does.Not.Contain("f6f00a0a97a470646a3ad7f7283b34c4"));
        }

        [Test]
        public void PoleColliderFindsNestedParkourPoints()
        {
            GameObject playerPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            GameObject polePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/NERA/Prefabs/Parkour/Jump/Pole.prefab");
            GameObject player = Object.Instantiate(playerPrefab);
            GameObject pole = Object.Instantiate(polePrefab);

            try
            {
                DetectionCharacterController detection =
                    player.GetComponentInChildren<
                        DetectionCharacterController>(true);
                HandlePoints expected =
                    pole.GetComponentInChildren<HandlePoints>(true);
                pole.transform.position = detection.transform.position +
                                          detection.transform.forward * 2f;
                Physics.SyncTransforms();

                var found = new List<HandlePoints>();
                detection.FindAheadPoints(ref found);

                Assert.That(expected, Is.Not.Null);
                Assert.That(found, Does.Contain(expected));
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(pole);
            }
        }

        [Test]
        public void DevelopmentParkourSceneHasNoLegacyTags()
        {
            string sceneText = System.IO.File.ReadAllText(
                "Assets/_Project/NERA/_Development/Parkour/Testing.unity");
            string[] legacyTags =
            {
                "m_TagString: Pole",
                "m_TagString: Reach",
                "m_TagString: Slide",
                "m_TagString: VaultObstacle",
                "m_TagString: VaultOver",
            };

            foreach (string legacyTag in legacyTags)
                Assert.That(sceneText, Does.Not.Contain(legacyTag));
        }

        [Test]
        public void SlideRequiresForwardMomentumAndRejectsAscendingTravel()
        {
            Assert.That(
                VaultSlide.HasForwardMomentum(
                    Vector3.zero,
                    Vector3.forward),
                Is.False);
            Assert.That(
                VaultSlide.HasForwardMomentum(
                    Vector3.back * 4f,
                    Vector3.forward),
                Is.False);
            Assert.That(
                VaultSlide.HasForwardMomentum(
                    Vector3.forward * VaultSlide.MinimumForwardSpeed,
                    Vector3.forward),
                Is.True);

            Assert.That(
                VaultSlide.IsNonAscendingDestination(
                    Vector3.zero,
                    Vector3.forward * 3f),
                Is.True);
            Assert.That(
                VaultSlide.IsNonAscendingDestination(
                    Vector3.zero,
                    Vector3.forward * 3f +
                    Vector3.up * (VaultSlide.MaximumUpwardHeight + 0.01f)),
                Is.False);
        }

        [Test]
        public void LedgeDescentUsesBackwardMovementWithoutDropModifier()
        {
            Assert.That(
                ClimbController.WantsToDescend(Vector2.down),
                Is.True);
            Assert.That(
                ClimbController.WantsToDescend(
                    new Vector2(-1f, -1f).normalized),
                Is.True);
            Assert.That(
                ClimbController.WantsToDescend(
                    new Vector2(1f, -1f).normalized),
                Is.True);
            Assert.That(
                ClimbController.WantsToDescend(Vector2.left),
                Is.False);
        }

        [Test]
        public void TargetLedgeStateFollowsFootSupport()
        {
            Assert.That(
                ClimbController.ResolveTargetClimbState(true),
                Is.EqualTo(ClimbController.ClimbState.BHanging));
            Assert.That(
                ClimbController.ResolveTargetClimbState(false),
                Is.EqualTo(ClimbController.ClimbState.FHanging));
        }

        [Test]
        public void BracedHopsUseTargetStateForFastFreeHangTransitions()
        {
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    "Assets/_Project/NERA/Art/Parkour/" +
                    "Animator Controller.controller");
            Assert.That(controller, Is.Not.Null);

            AnimatorStateMachine root = controller.layers[0].stateMachine;
            AnimatorState freeHangConversion =
                FindAnimatorState(root, "Braced To FreeHang");
            Assert.That(freeHangConversion, Is.Not.Null);

            string[] hopNames =
            {
                "Braced Hang Hop Up",
                "Braced Hang Hop Down",
                "Braced Hang Hop Left",
                "Braced Hang Hop Right",
            };

            foreach (string hopName in hopNames)
            {
                AnimatorState hop = FindAnimatorState(root, hopName);
                Assert.That(hop, Is.Not.Null, hopName);

                AnimatorStateTransition freeTransition = hop.transitions
                    .SingleOrDefault(transition =>
                        transition.destinationState == freeHangConversion);
                AnimatorStateTransition bracedTransition = hop.transitions
                    .SingleOrDefault(transition =>
                        transition.destinationState != null &&
                        transition.destinationState.name ==
                        "Hanging Movement");

                Assert.That(freeTransition, Is.Not.Null, hopName);
                Assert.That(bracedTransition, Is.Not.Null, hopName);
                AssertClimbStateCondition(freeTransition, 2f, hopName);
                AssertClimbStateCondition(bracedTransition, 1f, hopName);
                Assert.That(
                    freeTransition.exitTime,
                    Is.LessThan(bracedTransition.exitTime),
                    $"{hopName} should switch to free hanging before " +
                    "the regular braced hop finishes.");
            }
        }

        [Test]
        public void InventoryToggleUsesTabAndClimbSolverDoesNotWriteIk()
        {
            string inventorySource = System.IO.File.ReadAllText(
                "Assets/_Project/NERA/Code/Runtime/Inventory/" +
                "InventoryLabHUDController.cs");
            Assert.That(
                inventorySource,
                Does.Contain("Input.GetKeyDown(KeyCode.Tab)"));
            Assert.That(
                inventorySource,
                Does.Not.Contain("Input.GetKeyDown(KeyCode.I)"));

            string climbSource = System.IO.File.ReadAllText(
                "Assets/_Project/NERA/Code/Runtime/Parkour/" +
                "System Controllers/ClimbController.cs");
            int solverStart = climbSource.IndexOf(
                "void IKSolver()",
                System.StringComparison.Ordinal);
            int solverEnd = climbSource.IndexOf(
                "bool CheckValidMovement",
                solverStart,
                System.StringComparison.Ordinal);
            Assert.That(solverStart, Is.GreaterThanOrEqualTo(0));
            Assert.That(solverEnd, Is.GreaterThan(solverStart));
            Assert.That(
                climbSource.Substring(solverStart, solverEnd - solverStart),
                Does.Not.Contain("SetIK"));
            Assert.That(
                climbSource,
                Does.Contain("GetNextAnimatorStateInfo(0)"));

            string animationSource = System.IO.File.ReadAllText(
                "Assets/_Project/NERA/Code/Runtime/Parkour/" +
                "System Controllers/AnimationCharacterController.cs");
            Assert.That(
                animationSource,
                Does.Contain("animator.IsInTransition(0) || " +
                             "animator.isMatchingTarget"));
        }

        private static AnimatorState FindAnimatorState(
            AnimatorStateMachine stateMachine,
            string stateName)
        {
            foreach (ChildAnimatorState child in stateMachine.states)
            {
                if (child.state.name == stateName)
                    return child.state;
            }

            foreach (ChildAnimatorStateMachine child in
                     stateMachine.stateMachines)
            {
                AnimatorState state =
                    FindAnimatorState(child.stateMachine, stateName);
                if (state != null)
                    return state;
            }

            return null;
        }

        private static void AssertClimbStateCondition(
            AnimatorStateTransition transition,
            float expectedValue,
            string message)
        {
            AnimatorCondition condition = transition.conditions
                .SingleOrDefault(candidate =>
                    candidate.parameter == "Climb State");
            Assert.That(condition.parameter, Is.EqualTo("Climb State"), message);
            Assert.That(condition.mode, Is.EqualTo(AnimatorConditionMode.Equals));
            Assert.That(condition.threshold, Is.EqualTo(expectedValue));
        }
    }
}
