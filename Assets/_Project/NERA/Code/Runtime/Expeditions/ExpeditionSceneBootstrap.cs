using NERA.Core;
using NERA.Quests;
using UnityEngine;

namespace NERA.Expeditions
{
    public sealed class ExpeditionSceneBootstrap : MonoBehaviour
    {
        private void Start()
        {
            // BootInitializer reports normal additive scene transitions. This
            // fallback keeps direct scene play useful during authoring.
            if (BootInitializer.Instance != null)
                return;

            QuestController quests = QuestController.Instance;
            if (quests == null)
            {
                Debug.LogError(
                    "ExpeditionSceneBootstrap: QuestController is missing. Start gameplay through MainScene.",
                    this
                );
                return;
            }

            string targetId = gameObject.scene.name;
            string targetName = gameObject.scene.name;
            ExpeditionDiscoveryController discovery =
                ExpeditionDiscoveryController.Instance;
            if (discovery != null &&
                discovery.TryGetKnownLocationBySceneName(
                    gameObject.scene.name,
                    out ExpeditionLocationData location))
            {
                targetId = location.LocationId;
                targetName = location.DisplayName;
            }

            quests.Report(
                QuestSignalType.LocationEntered,
                targetId,
                targetName);
        }
    }
}
