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
        public void KinematicKeepsInterpolationUntilClimbAnimationOwnsMotion()
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            GameObject instance = Object.Instantiate(prefab);

            try
            {
                ParkourPlayerBridge bridge =
                    instance.GetComponentInChildren<ParkourPlayerBridge>(true);
                MovementCharacterController movement =
                    bridge.GetComponent<MovementCharacterController>();
                Rigidbody body = bridge.GetComponent<Rigidbody>();
                movement.rb = body;
                body.interpolation = RigidbodyInterpolation.Interpolate;

                movement.SetKinematic(true);
                Assert.That(
                    body.interpolation,
                    Is.EqualTo(RigidbodyInterpolation.Interpolate));

                movement.SetAnimationDrivenClimb(true);
                Assert.That(
                    body.interpolation,
                    Is.EqualTo(RigidbodyInterpolation.None));

                movement.SetAnimationDrivenClimb(false);
                Assert.That(
                    body.interpolation,
                    Is.EqualTo(RigidbodyInterpolation.Interpolate));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
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
        public void AirborneBracedHangHasGuardedAnimatorChain()
        {
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    "Assets/_Project/NERA/Art/Parkour/" +
                    "Animator Controller.controller");
            Assert.That(controller, Is.Not.Null);

            AnimatorState bracedHang = FindAnimatorState(
                controller.layers[0].stateMachine,
                "Braced Hang");
            Assert.That(bracedHang, Is.Not.Null);
            Assert.That(bracedHang.tag, Is.EqualTo("Root"));
            Assert.That(
                bracedHang.transitions.Any(
                    transition => transition.destinationState != null &&
                                  transition.destinationState.name ==
                                  "Hanging Movement"),
                Is.True);

            foreach (string sourceName in new[] { "Fall Idle", "Predicted Jump" })
            {
                AnimatorState source = FindAnimatorState(
                    controller.layers[0].stateMachine,
                    sourceName);
                AnimatorStateTransition transition = source.transitions
                    .FirstOrDefault(candidate =>
                        candidate.destinationState == bracedHang);

                Assert.That(transition, Is.Not.Null, sourceName);
                Assert.That(transition.hasExitTime, Is.False, sourceName);
                Assert.That(
                    transition.conditions.Any(condition =>
                        condition.parameter == "Hanging" &&
                        condition.mode == AnimatorConditionMode.If),
                    Is.True,
                    sourceName);
            }
        }

        [Test]
        public void BracedHopsUseDirectionalBlendTree()
        {
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    "Assets/_Project/NERA/Art/Parkour/" +
                    "Animator Controller.controller");
            Assert.That(controller, Is.Not.Null);

            AnimatorStateMachine root = controller.layers[0].stateMachine;
            Assert.That(
                controller.parameters.Select(parameter => parameter.name),
                Does.Contain("HopHorizontal"));
            Assert.That(
                controller.parameters.Select(parameter => parameter.name),
                Does.Contain("HopVertical"));

            AnimatorState hopState = FindAnimatorState(root, "Braced Hang Hop");
            Assert.That(hopState, Is.Not.Null);
            Assert.That(hopState.tag, Is.EqualTo("Root"));

            BlendTree tree = hopState.motion as BlendTree;
            Assert.That(tree, Is.Not.Null);
            Assert.That(
                tree.blendType,
                Is.EqualTo(BlendTreeType.SimpleDirectional2D));
            Assert.That(tree.blendParameter, Is.EqualTo("HopHorizontal"));
            Assert.That(tree.blendParameterY, Is.EqualTo("HopVertical"));
            Assert.That(
                tree.children.Select(child => child.position),
                Is.EquivalentTo(new[]
                {
                    Vector2.left,
                    Vector2.right,
                    Vector2.up,
                    Vector2.down,
                }));

            string source = System.IO.File.ReadAllText(
                "Assets/_Project/NERA/Code/Runtime/Parkour/" +
                "System Controllers/AnimationCharacterController.cs");
            Assert.That(
                source,
                Does.Contain("CrossFade(BracedHangHopStateName"));
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
        public void AirborneGrabUsesLowerSweepForThinLedge()
        {
            GameObject playerPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            GameObject ledgePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/NERA/Prefabs/Parkour/Climb/Ledge.prefab");
            GameObject player = Object.Instantiate(playerPrefab);
            GameObject ledge = Object.Instantiate(ledgePrefab);

            try
            {
                DetectionCharacterController detection =
                    player.GetComponentInChildren<
                        DetectionCharacterController>(true);
                detection.transform.SetPositionAndRotation(
                    Vector3.zero,
                    Quaternion.identity);
                ledge.transform.SetPositionAndRotation(
                    new Vector3(0f, 0.9f, 1.1f),
                    Quaternion.identity);
                ledge.transform.localScale = new Vector3(2f, 0.2f, 0.2f);
                Physics.SyncTransforms();

                Assert.That(
                    detection.FindLedgeCollision(out _),
                    Is.False,
                    "The ground ray sweep starts above this thin ledge.");
                Assert.That(
                    detection.FindAirborneLedgeCollision(out RaycastHit hit),
                    Is.True,
                    "The airborne sweep must include thin ledges below the " +
                    "normal shoulder-height origin.");
                Assert.That(hit.collider.gameObject, Is.EqualTo(ledge));
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(ledge);
            }
        }

        [Test]
        public void SingleLedgePointsProvideMovementBounds()
        {
            GameObject ledgePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/NERA/Prefabs/Parkour/Climb/Ledge.prefab");
            GameObject ledge = Object.Instantiate(ledgePrefab);

            try
            {
                HandlePoints handle =
                    ledge.GetComponentInChildren<HandlePoints>(true);
                Assert.That(handle, Is.Not.Null);
                Assert.That(handle.furthestLeft, Is.Not.Null);
                Assert.That(handle.furthestRight, Is.Not.Null);

                Vector3 left = handle.furthestLeft.transform.position;
                Vector3 right = handle.furthestRight.transform.position;
                Vector3 centre = (left + right) * 0.5f;
                Vector3 axis = (right - left).normalized;

                Assert.That(
                    ClimbController.CanMoveWithinPointSpan(
                        handle,
                        centre,
                        axis),
                    Is.True);
                Assert.That(
                    ClimbController.CanMoveWithinPointSpan(
                        handle,
                        centre,
                        -axis),
                    Is.True);
                Assert.That(
                    ClimbController.CanMoveWithinPointSpan(
                        handle,
                        right,
                        axis),
                    Is.False);
                Assert.That(
                    ClimbController.CanMoveWithinPointSpan(
                        handle,
                        left,
                        -axis),
                    Is.False);
            }
            finally
            {
                Object.DestroyImmediate(ledge);
            }
        }

        [Test]
        public void InitialGrabAndLedgeJumpSharePointAnchor()
        {
            GameObject ledgePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/NERA/Prefabs/Parkour/Climb/Ledge.prefab");
            GameObject ledge = Object.Instantiate(ledgePrefab);

            try
            {
                HandlePoints handle =
                    ledge.GetComponentInChildren<HandlePoints>(true);
                Assert.That(handle, Is.Not.Null);
                Assert.That(handle.pointsInOrder, Has.Count.GreaterThan(2));

                Point centrePoint = handle.pointsInOrder[1];
                Assert.That(
                    ClimbController.TryGetPointGrabTarget(
                        handle,
                        centrePoint,
                        out Vector3 centreTarget),
                    Is.True);
                Assert.That(
                    centreTarget,
                    Is.EqualTo(centrePoint.transform.position));

                Assert.That(
                    ClimbController.TryGetPointGrabTarget(
                        handle,
                        handle.furthestRight,
                        out Vector3 rightTarget),
                    Is.True);
                Assert.That(
                    rightTarget,
                    Is.EqualTo(
                        handle.furthestRight.transform.position -
                        handle.transform.right * 0.5f));
            }
            finally
            {
                Object.DestroyImmediate(ledge);
            }
        }

        [Test]
        public void ManualReleaseBlocksOnlyTheReleasedHandleUntilLanding()
        {
            GameObject ledgePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/NERA/Prefabs/Parkour/Climb/Ledge.prefab");
            GameObject releasedLedge = Object.Instantiate(ledgePrefab);
            GameObject otherLedge = Object.Instantiate(ledgePrefab);

            try
            {
                HandlePoints released =
                    releasedLedge.GetComponentInChildren<HandlePoints>(true);
                HandlePoints other =
                    otherLedge.GetComponentInChildren<HandlePoints>(true);

                Assert.That(
                    ClimbController.ShouldBlockLedgeRegrab(
                        released,
                        released,
                        false),
                    Is.True);
                Assert.That(
                    ClimbController.ShouldBlockLedgeRegrab(
                        released,
                        other,
                        false),
                    Is.False,
                    "A different lower ledge must remain grabbable.");
                Assert.That(
                    ClimbController.ShouldBlockLedgeRegrab(
                        released,
                        released,
                        true),
                    Is.False,
                    "Landing must unlock the released ledge.");
            }
            finally
            {
                Object.DestroyImmediate(releasedLedge);
                Object.DestroyImmediate(otherLedge);
            }
        }

        [Test]
        public void ClimbIkBlendIsFrameRateIndependentAndBounded()
        {
            float oneFrame = ClimbController.ExponentialBlend(18f, 1f / 60f);
            float twoFrames = 1f - Mathf.Pow(1f - oneFrame, 2f);
            float combined = ClimbController.ExponentialBlend(18f, 2f / 60f);

            Assert.That(oneFrame, Is.GreaterThan(0f).And.LessThan(1f));
            Assert.That(twoFrames, Is.EqualTo(combined).Within(0.0001f));
            Assert.That(ClimbController.ExponentialBlend(0f, 1f), Is.Zero);
        }

        [Test]
        public void ClimbTargetCannotJumpFromLedgeToWorldOrigin()
        {
            Vector3 playerPosition = new Vector3(-2f, 5f, 76f);

            Assert.That(
                ClimbController.IsLocalClimbTarget(
                    playerPosition,
                    Vector3.zero),
                Is.False);
            Assert.That(
                ClimbController.IsLocalClimbTarget(
                    playerPosition,
                    playerPosition + Vector3.up * 2f),
                Is.True);
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
        public void AirMovementDoesNotPushThePlayerIntoWallsOrUpCorners()
        {
            Vector3 fallingIntoWall = new Vector3(0f, -5f, 6f);
            Vector3 wallLimited =
                MovementCharacterController.RemoveVelocityIntoSurface(
                    fallingIntoWall,
                    Vector3.back);

            Assert.That(wallLimited.x, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(wallLimited.y, Is.EqualTo(-5f).Within(0.0001f));
            Assert.That(wallLimited.z, Is.EqualTo(0f).Within(0.0001f));

            Vector3 cornerLimited =
                MovementCharacterController.RemoveVelocityIntoSurface(
                    new Vector3(4f, -5f, 6f),
                    Vector3.back);
            cornerLimited =
                MovementCharacterController.RemoveVelocityIntoSurface(
                    cornerLimited,
                    Vector3.left);

            Assert.That(cornerLimited.x, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(cornerLimited.y, Is.EqualTo(-5f).Within(0.0001f));
            Assert.That(cornerLimited.z, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void PredictedJumpChecksTheCapsuleBeforeMoving()
        {
            string source = System.IO.File.ReadAllText(
                "Assets/_Project/NERA/Code/Runtime/Parkour/" +
                "System Controllers/JumpPredictionController.cs");

            Assert.That(source, Does.Contain("rb.SweepTestAll("));
            Assert.That(
                source,
                Does.Not.Contain(
                    "rb.position = SampleParabola("));
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
        public void DiagonalLedgeHopPreservesBothBlendAxes()
        {
            Vector2 upRight =
                AnimationCharacterController.GetHopBlendDirection(
                    new Vector3(1f, 1f, 0f));
            Assert.That(upRight.x, Is.EqualTo(0.7071f).Within(0.0001f));
            Assert.That(upRight.y, Is.EqualTo(0.7071f).Within(0.0001f));

            Vector2 downLeft =
                AnimationCharacterController.GetHopBlendDirection(
                    new Vector3(-1f, -1f, 0f));
            Assert.That(downLeft.x, Is.EqualTo(-0.7071f).Within(0.0001f));
            Assert.That(downLeft.y, Is.EqualTo(-0.7071f).Within(0.0001f));

            Assert.That(
                AnimationCharacterController.GetHopBlendDirection(Vector3.left),
                Is.EqualTo(Vector2.left));
            Assert.That(
                AnimationCharacterController.GetHopBlendDirection(Vector3.down),
                Is.EqualTo(Vector2.down));
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
        }

        private static AnimatorState FindAnimatorState(
            AnimatorStateMachine stateMachine,
            string stateName)
        {
            AnimatorState state = stateMachine.states
                .Select(child => child.state)
                .FirstOrDefault(candidate => candidate.name == stateName);
            if (state != null)
                return state;

            foreach (ChildAnimatorStateMachine child in
                     stateMachine.stateMachines)
            {
                state = FindAnimatorState(child.stateMachine, stateName);
                if (state != null)
                    return state;
            }

            return null;
        }
    }
}
