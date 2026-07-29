using UnityEngine;

namespace NERA.Core
{
    public sealed class SceneSpawnPoint : MonoBehaviour
    {
        [Tooltip("Stable ID referenced by travel configs and scene exits.")]
        [SerializeField] private string spawnPointId;

        public string SpawnPointId =>
            spawnPointId?.Trim() ?? string.Empty;

        private void Start()
        {
            if (!SceneTransitionState.TryConsumeSpawnPoint(SpawnPointId))
                return;

            PlayerController player = FindFirstObjectByType<PlayerController>();

            if (player == null)
            {
                Debug.LogError(
                    $"SceneSpawnPoint '{SpawnPointId}': PlayerController not found.",
                    this
                );
                return;
            }

            CharacterController controller = player.GetComponent<CharacterController>();

            if (controller != null)
                controller.enabled = false;

            player.transform.SetPositionAndRotation(transform.position, transform.rotation);

            if (controller != null)
                controller.enabled = true;
        }
    }
}
