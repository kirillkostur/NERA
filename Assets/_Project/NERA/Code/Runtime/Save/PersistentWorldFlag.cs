using UnityEngine;
using UnityEngine.Events;

namespace NERA.Save
{
    /// <summary>
    /// Persists one authored boolean world state, such as a solved puzzle or
    /// an opened story door, and reapplies it after load or death rollback.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PersistentWorldFlag : MonoBehaviour
    {
        [Tooltip(
            "Stable ID. Do not rename it after saves are shipped. If empty, " +
            "the hierarchy path is used only as a prototype fallback.")]
        [SerializeField] private string persistentId;
        [Tooltip(
            "After Complete(), save the full rollback snapshot at the " +
            "player's current position.")]
        [SerializeField] private bool checkpointOnComplete = true;
        [Header("Apply saved state")]
        [SerializeField] private UnityEvent onCompleted;
        [SerializeField] private UnityEvent onIncomplete;

        private WorldStateController worldState;
        private string persistentKey;
        private bool hasAppliedState;
        private bool lastAppliedState;

        public string PersistentKey
        {
            get
            {
                CachePersistentKey();
                return persistentKey;
            }
        }

        public bool IsCompleted => worldState != null &&
            worldState.IsWorldFlagCompleted(PersistentKey);

        private void Awake()
        {
            CachePersistentKey();
        }

        private void OnEnable()
        {
            SubscribeToWorldState();
            ApplySavedState();
        }

        private void Start()
        {
            SubscribeToWorldState();
            ApplySavedState();
        }

        public void Complete()
        {
            SetCompleted(true);
        }

        public void Clear()
        {
            SetCompleted(false);
        }

        public void SetCompleted(bool completed)
        {
            SubscribeToWorldState();
            if (worldState == null)
            {
                Debug.LogWarning(
                    "Persistent world flag ignored: WorldStateController " +
                    "is not ready.",
                    this);
                return;
            }

            bool changed = worldState.SetWorldFlagCompleted(
                PersistentKey,
                completed);
            ApplySavedState();
            if (changed && completed && checkpointOnComplete)
            {
                CheckpointService.Instance?.ActivateCheckpointAtPlayer(
                    $"world/{PersistentKey}");
            }
        }

        public void ApplySavedState()
        {
            SubscribeToWorldState();
            if (worldState == null)
                return;

            bool completed = worldState.IsWorldFlagCompleted(PersistentKey);
            if (hasAppliedState && completed == lastAppliedState)
                return;

            hasAppliedState = true;
            lastAppliedState = completed;
            if (completed)
                onCompleted?.Invoke();
            else
                onIncomplete?.Invoke();
        }

        private void SubscribeToWorldState()
        {
            WorldStateController current = WorldStateController.Instance;
            if (current == worldState)
                return;

            if (worldState != null)
                worldState.StateRestored -= HandleStateRestored;
            worldState = current;
            if (worldState != null)
                worldState.StateRestored += HandleStateRestored;
        }

        private void HandleStateRestored()
        {
            hasAppliedState = false;
            ApplySavedState();
        }

        private void CachePersistentKey()
        {
            if (string.IsNullOrEmpty(persistentKey))
            {
                persistentKey = PersistentSceneIdentity.CreateKey(
                    transform,
                    persistentId);
            }
        }

        private void OnDisable()
        {
            if (worldState != null)
                worldState.StateRestored -= HandleStateRestored;
            worldState = null;
        }

        private void OnValidate()
        {
            persistentId = persistentId?.Trim();
        }
    }
}
