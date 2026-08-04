using System.Collections.Generic;
using Climbing;
using NERA.Interaction;
using NERA.Inventory;
using Unity.Cinemachine;
using UnityEngine;

namespace NERA.Player
{
    /// <summary>
    /// Stable NERA-facing API over the imported parkour controller. Gameplay
    /// systems should depend on this component instead of individual package
    /// controllers or a specific camera implementation.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(InputCharacterController))]
    [RequireComponent(typeof(ThirdPersonController))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ParkourPlayerBridge : MonoBehaviour
    {
        [Header("Parkour")]
        [SerializeField] private InputCharacterController inputController;
        [SerializeField] private ThirdPersonController parkourController;
        [SerializeField] private MovementCharacterController movementController;
        [SerializeField] private VaultingController vaultingController;
        [SerializeField] private ClimbController climbController;
        [SerializeField] private JumpPredictionController jumpController;
        [SerializeField] private Rigidbody locomotionBody;
        [SerializeField] private Collider[] locomotionColliders;

        [Header("NERA Gameplay")]
        [SerializeField] private PlayerInteractionController interactionController;
        [SerializeField] private PlayerEquipmentController equipmentController;
        [SerializeField] private Camera gameplayCamera;

        private readonly HashSet<object> inputLocks = new HashSet<object>();
        private readonly object directInputLock = new object();
        private Behaviour[] cameraInputBehaviours = System.Array.Empty<Behaviour>();
        private Behaviour[] parkourBehaviours = System.Array.Empty<Behaviour>();
        private CinemachineVirtualCameraBase[] virtualCameras =
            System.Array.Empty<CinemachineVirtualCameraBase>();
        private readonly Dictionary<CinemachineVirtualCameraBase, Transform>
            originalFollowTargets =
                new Dictionary<CinemachineVirtualCameraBase, Transform>();
        private readonly Dictionary<CinemachineVirtualCameraBase, Transform>
            originalLookAtTargets =
                new Dictionary<CinemachineVirtualCameraBase, Transform>();
        private bool isDead;

        public bool IsInputEnabled => !isDead && inputLocks.Count == 0;
        public bool IsDead => isDead;
        public Rigidbody LocomotionBody => locomotionBody;
        public Camera GameplayCamera => gameplayCamera;

        private void Awake()
        {
            ResolveReferences();
            ApplyInputState();
        }

        public void SetInputEnabled(bool enabled)
        {
            SetInputEnabled(directInputLock, enabled);
        }

        public void SetInputEnabled(object owner, bool enabled)
        {
            owner ??= directInputLock;

            if (enabled)
                inputLocks.Remove(owner);
            else
                inputLocks.Add(owner);

            ApplyInputState();
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            ResolveReferences();
            if (locomotionBody == null)
            {
                transform.SetPositionAndRotation(position, rotation);
                Physics.SyncTransforms();
                return;
            }

            Vector3 positionDelta = position - transform.position;

            // Parkour actions temporarily make the motor kinematic. Keeping
            // that transient state after a scene teleport would freeze a
            // living player at the spawn point.
            bool keepDisabled = isDead;
            if (!locomotionBody.isKinematic)
            {
                locomotionBody.linearVelocity = Vector3.zero;
                locomotionBody.angularVelocity = Vector3.zero;
            }

            ResetExternalParkourState();
            parkourController?.characterAnimation?.ResetForTeleport();
            if (!keepDisabled)
                parkourController?.EnableController();
            locomotionBody.isKinematic = true;

            locomotionBody.position = position;
            locomotionBody.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            Physics.SyncTransforms();

            CinemachineCore.OnTargetObjectWarped(transform, positionDelta);
            Transform focus = transform.Find("Focus");
            if (focus != null)
                CinemachineCore.OnTargetObjectWarped(focus, positionDelta);

            locomotionBody.isKinematic = keepDisabled;
            locomotionBody.useGravity = !keepDisabled;
            locomotionBody.detectCollisions = !keepDisabled;
            if (!keepDisabled)
            {
                locomotionBody.linearVelocity = Vector3.zero;
                locomotionBody.angularVelocity = Vector3.zero;
                locomotionBody.WakeUp();
            }
        }

        public void HandleDeath(Transform ragdollCameraTarget = null)
        {
            if (isDead)
                return;

            isDead = true;
            inputLocks.Add(this);
            ApplyInputState();
            ResetExternalParkourState();

            if (ragdollCameraTarget != null)
            {
                CaptureGameplayCameraTargets();
                RetargetGameplayCameras(ragdollCameraTarget);
            }

            foreach (Behaviour behaviour in parkourBehaviours)
            {
                if (behaviour != null)
                    behaviour.enabled = false;
            }

            if (locomotionBody != null)
            {
                if (!locomotionBody.isKinematic)
                {
                    locomotionBody.linearVelocity = Vector3.zero;
                    locomotionBody.angularVelocity = Vector3.zero;
                }
                locomotionBody.useGravity = false;
                locomotionBody.detectCollisions = false;
                locomotionBody.isKinematic = true;
            }

            foreach (Collider collider in locomotionColliders)
            {
                if (collider != null)
                    collider.enabled = false;
            }
        }

        public void Revive()
        {
            if (!isDead)
                return;

            ResolveReferences();
            isDead = false;
            inputLocks.Remove(this);
            ResetExternalParkourState();
            RestoreGameplayCameraTargets();

            foreach (Behaviour behaviour in parkourBehaviours)
            {
                if (behaviour != null)
                    behaviour.enabled = true;
            }

            foreach (Collider collider in locomotionColliders)
            {
                if (collider != null)
                    collider.enabled = true;
            }

            if (locomotionBody != null)
            {
                locomotionBody.isKinematic = false;
                locomotionBody.useGravity = true;
                locomotionBody.detectCollisions = true;
                locomotionBody.linearVelocity = Vector3.zero;
                locomotionBody.angularVelocity = Vector3.zero;
                locomotionBody.WakeUp();
            }

            parkourController?.EnableController();
            ApplyInputState();
        }

        private void ResolveReferences()
        {
            inputController ??= GetComponent<InputCharacterController>();
            parkourController ??= GetComponent<ThirdPersonController>();
            movementController ??= GetComponent<MovementCharacterController>();
            vaultingController ??= GetComponent<VaultingController>();
            climbController ??= GetComponent<ClimbController>();
            jumpController ??= GetComponent<JumpPredictionController>();
            locomotionBody ??= GetComponent<Rigidbody>();
            interactionController ??= GetComponent<PlayerInteractionController>();
            equipmentController ??= GetComponent<PlayerEquipmentController>();

            if (locomotionColliders == null || locomotionColliders.Length == 0)
                locomotionColliders = GetComponents<CapsuleCollider>();

            // The component lives on PlayerModel while the imported camera rig
            // is its direct parent. RuntimeRoot can contain unrelated cameras
            // and UI, so transform.root is intentionally not used here.
            Transform playerRoot = transform.parent != null
                ? transform.parent
                : transform;
            if (gameplayCamera == null)
                gameplayCamera = playerRoot.GetComponentInChildren<Camera>(true);

            var cameraInputs = new List<Behaviour>();
            MonoBehaviour[] rootBehaviours =
                playerRoot.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (MonoBehaviour behaviour in rootBehaviours)
            {
                if (behaviour == null)
                    continue;

                System.Type type = behaviour.GetType();
                if (type.Namespace == "Unity.Cinemachine" &&
                    type.Name.Contains("Input"))
                {
                    cameraInputs.Add(behaviour);
                }
            }

            cameraInputBehaviours = cameraInputs.ToArray();
            virtualCameras = playerRoot
                .GetComponentsInChildren<CinemachineVirtualCameraBase>(true);
            parkourBehaviours = new Behaviour[]
            {
                inputController,
                parkourController,
                movementController,
                vaultingController,
                GetComponent<DetectionCharacterController>(),
                GetComponent<AnimationCharacterController>(),
                jumpController,
                climbController,
            };
        }

        private void ResetExternalParkourState()
        {
            parkourController?.ResetForTeleport();
            vaultingController?.ResetForTeleport();
            climbController?.ResetForTeleport();
            jumpController?.ResetForTeleport();
        }

        private void RetargetGameplayCameras(Transform target)
        {
            foreach (CinemachineVirtualCameraBase virtualCamera in
                     virtualCameras)
            {
                if (virtualCamera == null)
                    continue;

                Transform follow = virtualCamera.Follow;
                if (follow != null &&
                    (follow == transform || follow.IsChildOf(transform)))
                {
                    virtualCamera.Follow = target;
                }

                Transform lookAt = virtualCamera.LookAt;
                if (lookAt != null &&
                    (lookAt == transform || lookAt.IsChildOf(transform)))
                {
                    virtualCamera.LookAt = target;
                }
            }
        }

        private void CaptureGameplayCameraTargets()
        {
            if (originalFollowTargets.Count > 0 ||
                originalLookAtTargets.Count > 0)
            {
                return;
            }

            foreach (CinemachineVirtualCameraBase virtualCamera in
                     virtualCameras)
            {
                if (virtualCamera == null)
                    continue;

                originalFollowTargets[virtualCamera] = virtualCamera.Follow;
                originalLookAtTargets[virtualCamera] = virtualCamera.LookAt;
            }
        }

        private void RestoreGameplayCameraTargets()
        {
            foreach (KeyValuePair<CinemachineVirtualCameraBase, Transform>
                     pair in originalFollowTargets)
            {
                if (pair.Key != null)
                    pair.Key.Follow = pair.Value;
            }

            foreach (KeyValuePair<CinemachineVirtualCameraBase, Transform>
                     pair in originalLookAtTargets)
            {
                if (pair.Key != null)
                    pair.Key.LookAt = pair.Value;
            }
        }

        private void ApplyInputState()
        {
            bool enabled = IsInputEnabled;
            inputController?.SetInputEnabled(enabled);

            foreach (Behaviour behaviour in cameraInputBehaviours)
            {
                if (behaviour != null)
                    behaviour.enabled = enabled;
            }

            if (interactionController != null)
                interactionController.enabled = enabled;
            equipmentController?.SetInputEnabled(enabled);
        }
    }
}
