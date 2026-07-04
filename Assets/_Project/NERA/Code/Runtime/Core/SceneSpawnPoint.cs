using UnityEngine;

public class SceneSpawnPoint : MonoBehaviour
{
    [Header("Spawn Point")]
    [SerializeField] private string spawnPointId = "Default";

    public string SpawnPointId => spawnPointId;

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(transform.position, 0.25f);

        Gizmos.color = Color.white;
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1.5f);
    }
#endif
}