using NERA.Core;
using NERA.Player;
using UnityEngine;

namespace NERA.Save
{
    [DisallowMultipleComponent]
    public sealed class AutoSaveCheckpoint : MonoBehaviour
    {
        [SerializeField] private string checkpointId = "checkpoint";
        [SerializeField] private bool onlyOnce = true;

        private bool triggered;

        public string CheckpointId => checkpointId;
        public bool HasTriggered => triggered;

        private void Start()
        {
            if (!SceneTransitionState.TryConsumeSpawnPoint(checkpointId))
                return;

            ParkourPlayerBridge player =
                FindFirstObjectByType<ParkourPlayerBridge>();
            player?.Teleport(transform.position, transform.rotation);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null ||
                other.GetComponentInParent<ParkourPlayerBridge>() == null)
                return;

            TriggerCheckpoint();
        }

        public void TriggerCheckpoint()
        {
            if (onlyOnce && triggered)
                return;

            CheckpointService service = CheckpointService.Instance;
            if (service != null && service.ActivateCheckpoint(
                    gameObject.scene.name,
                    checkpointId))
            {
                triggered = true;
            }
        }

        public void ResetCheckpoint()
        {
            triggered = false;
        }
    }
}
