using UnityEngine;

namespace NERA.Quests
{
    [DisallowMultipleComponent]
    public sealed class QuestSignalEmitter : MonoBehaviour
    {
        [SerializeField] private QuestSignalType signalType =
            QuestSignalType.AreaExplored;
        [SerializeField] private string targetId;
        [SerializeField] private string targetName;
        [SerializeField] private string cause;
        [SerializeField, Min(1)] private int amount = 1;
        [SerializeField, Min(0f)] private float value;
        [SerializeField] private bool emitOnStart;
        [SerializeField] private bool emitOnPlayerTrigger = true;
        [SerializeField] private bool emitOnPlayerExit;
        [SerializeField] private bool oneShot = true;

        private bool emitted;

        private void Start()
        {
            if (emitOnStart)
                Emit();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (emitOnPlayerTrigger &&
                other != null &&
                other.CompareTag("Player"))
            {
                Emit();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (emitOnPlayerExit &&
                other != null &&
                other.CompareTag("Player"))
            {
                Emit();
            }
        }

        public bool Emit()
        {
            if ((oneShot && emitted) || QuestController.Instance == null)
                return false;

            bool changed = QuestController.Instance.Report(
                signalType,
                targetId,
                targetName,
                amount,
                value,
                cause);
            emitted = true;
            return changed;
        }
    }
}
