using UnityEngine;
using NERA.Player;

namespace NERA.Core
{
    public sealed class SceneSpawnPoint : MonoBehaviour
    {
        [Tooltip("Stable ID referenced by travel configs and scene exits.")]
        [SerializeField] private string spawnPointId;

        public string SpawnPointId =>
            spawnPointId?.Trim() ?? string.Empty;

        public bool TryTeleport(ParkourPlayerBridge player)
        {
            if (player == null)
            {
                Debug.LogError(
                    $"SceneSpawnPoint '{SpawnPointId}': parkour player not found.",
                    this
                );
                return false;
            }

            player.Teleport(transform.position, transform.rotation);
            return true;
        }
    }
}
