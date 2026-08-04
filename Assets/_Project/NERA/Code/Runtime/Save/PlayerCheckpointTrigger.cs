using UnityEngine;

namespace NERA.Save
{
    /// <summary>
    /// Creates a full checkpoint at the player's current position when called
    /// by a UnityEvent, interaction, timeline or another gameplay system.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerCheckpointTrigger : MonoBehaviour
    {
        [Tooltip("Stable label used to identify this authored checkpoint.")]
        [SerializeField] private string checkpointId = "story_checkpoint";
        [SerializeField] private bool onlyOnce = true;

        private bool triggered;

        public string CheckpointId => checkpointId?.Trim() ?? string.Empty;
        public bool HasTriggered => triggered;

        public void TriggerCheckpoint()
        {
            TryTriggerCheckpoint();
        }

        public bool TryTriggerCheckpoint()
        {
            if (onlyOnce && triggered)
                return false;

            CheckpointService service = CheckpointService.Instance;
            if (service == null ||
                !service.ActivateCheckpointAtPlayer(CheckpointId))
            {
                return false;
            }

            triggered = true;
            return true;
        }

        public void ResetTrigger()
        {
            triggered = false;
        }

        private void OnValidate()
        {
            checkpointId = checkpointId?.Trim();
        }
    }
}
