using System;
using System.Collections;
using NERA.Combat;
using NERA.Core;
using NERA.Player;
using NERA.Quests;
using UnityEngine;

namespace NERA.Save
{
    public enum CheckpointActivity
    {
        Saving,
        Saved,
        SaveFailed,
        Restoring,
        Restored,
        RestoreFailed
    }

    [DisallowMultipleComponent]
    public sealed class CheckpointService : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float deathRestoreDelay = 2f;

        private SaveGameController saveController;
        private AutoSaveService autoSave;
        private BootInitializer boot;
        private PlayerHealth playerHealth;
        private ParkourPlayerBridge player;
        private QuestController quests;
        private bool initialized;
        private bool restoring;
        private string suppressedScene;
        private string suppressedCheckpoint;
        private string queuedQuestCheckpointId;

        public static CheckpointService Instance { get; private set; }
        public static event Action<CheckpointActivity> ActivityChanged;
        public bool IsRestoring => restoring;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            saveController = GetComponent<SaveGameController>();
            autoSave = GetComponent<AutoSaveService>();
            boot = GetComponent<BootInitializer>();
            playerHealth = GetComponentInChildren<PlayerHealth>(true);
            player = GetComponentInChildren<ParkourPlayerBridge>(true);
            quests = GetComponent<QuestController>();
        }

        public void InitializeSession()
        {
            if (initialized)
                return;

            initialized = true;
            if (playerHealth != null)
                playerHealth.Died += HandlePlayerDied;
            if (quests != null)
            {
                quests.QuestStageChanged += HandleQuestStageChanged;
                quests.QuestCompleted += HandleQuestCompleted;
            }
        }

        public bool ActivateCheckpoint(
            string sceneName,
            string checkpointId)
        {
            if (!initialized || restoring ||
                string.IsNullOrWhiteSpace(sceneName) ||
                string.IsNullOrWhiteSpace(checkpointId))
            {
                return false;
            }

            if (IsSuppressed(sceneName, checkpointId))
                return false;

            ActivityChanged?.Invoke(CheckpointActivity.Saving);
            autoSave?.CancelPending();
            bool saved = saveController != null &&
                saveController.SaveCheckpoint(sceneName, checkpointId);
            ActivityChanged?.Invoke(saved
                ? CheckpointActivity.Saved
                : CheckpointActivity.SaveFailed);
            return saved;
        }

        public bool ActivateCheckpointAtPlayer(string checkpointId)
        {
            if (!initialized || restoring ||
                string.IsNullOrWhiteSpace(checkpointId))
            {
                return false;
            }

            if (player == null)
                player = FindFirstObjectByType<ParkourPlayerBridge>();
            string sceneName = boot != null
                ? boot.CurrentGameplaySceneName
                : string.Empty;
            if (player == null || string.IsNullOrWhiteSpace(sceneName))
                return false;

            ActivityChanged?.Invoke(CheckpointActivity.Saving);
            autoSave?.CancelPending();
            bool saved = saveController != null &&
                saveController.SaveCheckpointAtPosition(
                    sceneName,
                    checkpointId,
                    player.transform.position,
                    player.transform.rotation);
            ActivityChanged?.Invoke(saved
                ? CheckpointActivity.Saved
                : CheckpointActivity.SaveFailed);
            return saved;
        }

        public void SuppressNextActivation(
            string sceneName,
            string checkpointId)
        {
            suppressedScene = sceneName?.Trim() ?? string.Empty;
            suppressedCheckpoint = checkpointId?.Trim() ?? string.Empty;
        }

        private bool IsSuppressed(string sceneName, string checkpointId)
        {
            if (!string.Equals(
                    suppressedScene,
                    sceneName?.Trim(),
                    StringComparison.Ordinal) ||
                !string.Equals(
                    suppressedCheckpoint,
                    checkpointId?.Trim(),
                    StringComparison.Ordinal))
            {
                return false;
            }

            suppressedScene = string.Empty;
            suppressedCheckpoint = string.Empty;
            return true;
        }

        private void HandlePlayerDied()
        {
            if (!restoring)
                StartCoroutine(RestoreAfterDeath());
        }

        private void HandleQuestStageChanged(QuestRuntimeState state)
        {
            int completedStageIndex = state != null
                ? state.CurrentStageIndex - 1
                : -1;
            if (!StageCreatesCheckpoint(state, completedStageIndex))
                return;

            QueueQuestCheckpoint(state, completedStageIndex);
        }

        private void HandleQuestCompleted(QuestRuntimeState state)
        {
            int completedStageIndex = state?.Definition != null
                ? state.CurrentStageIndex
                : -1;
            if (!StageCreatesCheckpoint(state, completedStageIndex))
                return;

            QueueQuestCheckpoint(state, completedStageIndex);
        }

        private void QueueQuestCheckpoint(
            QuestRuntimeState state,
            int completedStageIndex)
        {
            queuedQuestCheckpointId =
                $"quest/{state.InstanceId}/stage/{completedStageIndex + 1}";
        }

        private static bool StageCreatesCheckpoint(
            QuestRuntimeState state,
            int stageIndex)
        {
            return state?.Definition != null &&
                stageIndex >= 0 &&
                stageIndex < state.Definition.Stages.Count &&
                state.Definition.Stages[stageIndex]
                    .CreateCheckpointOnCompletion;
        }

        private void LateUpdate()
        {
            if (string.IsNullOrEmpty(queuedQuestCheckpointId))
                return;

            string checkpointId = queuedQuestCheckpointId;
            queuedQuestCheckpointId = string.Empty;
            ActivateCheckpointAtPlayer(checkpointId);
        }

        private IEnumerator RestoreAfterDeath()
        {
            restoring = true;
            autoSave?.CancelPending();
            autoSave?.SetSuspended(true);
            if (deathRestoreDelay > 0f)
                yield return new WaitForSecondsRealtime(deathRestoreDelay);

            ActivityChanged?.Invoke(CheckpointActivity.Restoring);
            bool loaded = saveController != null &&
                saveController.LoadCheckpoint();
            if (!loaded ||
                string.IsNullOrWhiteSpace(saveController.CheckpointSceneName) ||
                string.IsNullOrWhiteSpace(saveController.CheckpointSpawnPointId))
            {
                ActivityChanged?.Invoke(CheckpointActivity.RestoreFailed);
                autoSave?.SetSuspended(false);
                restoring = false;
                yield break;
            }

            string sceneName = saveController.CheckpointSceneName;
            string checkpointId = saveController.CheckpointSpawnPointId;
            if (!saveController.CheckpointUsesWorldPose)
                SuppressNextActivation(sceneName, checkpointId);
            playerHealth?.Revive();

            if (boot != null)
            {
                yield return boot.ReloadGameplayFromCheckpoint(
                    sceneName,
                    checkpointId);
            }

            ActivityChanged?.Invoke(CheckpointActivity.Restored);
            autoSave?.SetSuspended(false);
            restoring = false;
        }

        private void OnDestroy()
        {
            if (playerHealth != null)
                playerHealth.Died -= HandlePlayerDied;
            if (quests != null)
            {
                quests.QuestStageChanged -= HandleQuestStageChanged;
                quests.QuestCompleted -= HandleQuestCompleted;
            }
            if (Instance == this)
                Instance = null;
        }
    }
}
