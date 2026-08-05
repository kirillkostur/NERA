using System.Collections.Generic;
using UnityEngine;

namespace NERA.CameraSystem
{
    [DisallowMultipleComponent]
    public sealed class FreeLookCameraOrbitZone : MonoBehaviour
    {
        [SerializeField] private FreeLookCameraOrbitProfile profile;
        [Tooltip("A zone with a higher value wins while zones overlap.")]
        [SerializeField] private int priority;
        [SerializeField] private string playerTag = "Player";

        private readonly HashSet<Collider> playerColliders = new();

        private FreeLookCameraOrbitController controller;
        private bool registered;

        public FreeLookCameraOrbitProfile Profile => profile;
        public int Priority => priority;

        private void Reset()
        {
            EnsureTrigger(true);
        }

        private void Awake()
        {
            EnsureTrigger(true);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (profile == null || !IsPlayer(other))
                return;

            if (!playerColliders.Add(other) || playerColliders.Count != 1)
                return;

            Activate();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!playerColliders.Remove(other) || playerColliders.Count > 0)
                return;

            Deactivate();
        }

        private void OnDisable()
        {
            playerColliders.Clear();
            Deactivate();
        }

        public void Configure(
            FreeLookCameraOrbitProfile orbitProfile,
            int zonePriority = 0,
            FreeLookCameraOrbitController orbitController = null)
        {
            profile = orbitProfile;
            priority = zonePriority;

            if (orbitController != null)
                controller = orbitController;
        }

        private void Activate()
        {
            if (controller == null)
            {
                controller = FindFirstObjectByType<
                    FreeLookCameraOrbitController>();
            }

            if (controller == null)
            {
                Debug.LogWarning(
                    $"{nameof(FreeLookCameraOrbitZone)} on '{name}' could " +
                    $"not find {nameof(FreeLookCameraOrbitController)}.",
                    this);
                return;
            }

            registered = controller.EnterZone(this, profile, priority);
        }

        private void Deactivate()
        {
            if (!registered)
                return;

            if (controller != null)
                controller.ExitZone(this);

            registered = false;
        }

        private bool IsPlayer(Collider other)
        {
            if (other == null || string.IsNullOrWhiteSpace(playerTag))
                return false;

            Transform current = other.transform;
            while (current != null)
            {
                if (current.CompareTag(playerTag))
                    return true;

                current = current.parent;
            }

            return false;
        }

        private void EnsureTrigger(bool createIfMissing)
        {
            Collider zoneCollider = GetComponent<Collider>();
            if (zoneCollider == null && createIfMissing)
                zoneCollider = gameObject.AddComponent<BoxCollider>();

            if (zoneCollider != null)
                zoneCollider.isTrigger = true;
        }

        private void OnValidate()
        {
            playerTag = playerTag?.Trim();
            EnsureTrigger(false);

            if (!Application.isPlaying || controller == null)
                return;

            if (registered && profile == null)
                Deactivate();
            else if (playerColliders.Count > 0)
                Activate();
        }
    }
}
