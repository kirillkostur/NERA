using UnityEngine;

namespace NERA.Core
{
    public sealed class SceneSpawnPoint : MonoBehaviour
    {
        [SerializeField] private string spawnPointId;

        private void Start()
        {
            if (!SceneTransitionState.TryConsumeSpawnPoint(spawnPointId))
                return;

            PlayerController player = FindFirstObjectByType<PlayerController>();

            if (player == null)
            {
                Debug.LogError(
                    $"SceneSpawnPoint '{spawnPointId}': PlayerController not found.",
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
