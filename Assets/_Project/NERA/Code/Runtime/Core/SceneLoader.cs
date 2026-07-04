using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    private const string PlayerTag = "Player";

    private string targetSpawnPointId;
    private bool hasPendingSpawn;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"SceneLoader duplicate destroyed: {name}");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += OnSceneLoaded;

        Debug.Log($"SceneLoader initialized on object: {name}");
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    public void LoadScene(string sceneName, string spawnPointId)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("SceneLoader: sceneName is empty.");
            return;
        }

        if (string.IsNullOrWhiteSpace(spawnPointId))
        {
            Debug.LogError("SceneLoader: spawnPointId is empty.");
            return;
        }

        targetSpawnPointId = spawnPointId;
        hasPendingSpawn = true;

        Debug.Log($"SceneLoader: LoadScene requested. Scene='{sceneName}', Spawn='{targetSpawnPointId}'");

        SceneManager.LoadScene(sceneName);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"SceneLoader: OnSceneLoaded. Scene='{scene.name}', HasPendingSpawn={hasPendingSpawn}, TargetSpawn='{targetSpawnPointId}'");

        if (!hasPendingSpawn)
            return;

        StartCoroutine(PlacePlayerNextFrame());
    }

    private IEnumerator PlacePlayerNextFrame()
    {
        yield return null;

        MovePersistentPlayerToSpawnPoint();
        ReconnectCameraToPlayer();

        hasPendingSpawn = false;
        targetSpawnPointId = null;
    }

    private void MovePersistentPlayerToSpawnPoint()
    {
        GameObject player = GetPersistentPlayerObject();

        if (player == null)
        {
            Debug.LogError("SceneLoader: Persistent Player not found.");
            return;
        }

        SceneSpawnPoint spawnPoint = FindSpawnPoint(targetSpawnPointId);

        if (spawnPoint == null)
        {
            Debug.LogError($"SceneLoader: Spawn point '{targetSpawnPointId}' not found in scene '{SceneManager.GetActiveScene().name}'.");
            LogAvailableSpawnPoints();
            return;
        }

        CharacterController characterController = player.GetComponent<CharacterController>();

        if (characterController != null)
            characterController.enabled = false;

        player.transform.SetPositionAndRotation(
            spawnPoint.transform.position,
            spawnPoint.transform.rotation
        );

        if (characterController != null)
            characterController.enabled = true;

        Debug.Log($"SceneLoader: Persistent Player moved to spawn point '{spawnPoint.SpawnPointId}' at {spawnPoint.transform.position}.");
    }

    private GameObject GetPersistentPlayerObject()
    {
        if (PersistentPlayer.Instance != null)
            return PersistentPlayer.Instance.gameObject;

        GameObject playerByTag = GameObject.FindGameObjectWithTag(PlayerTag);

        if (playerByTag != null)
            return playerByTag;

        PlayerController playerController = FindFirstObjectByType<PlayerController>();

        if (playerController != null)
            return playerController.gameObject;

        return null;
    }

    private SceneSpawnPoint FindSpawnPoint(string spawnPointId)
    {
        SceneSpawnPoint[] spawnPoints = FindObjectsByType<SceneSpawnPoint>(FindObjectsSortMode.None);

        foreach (SceneSpawnPoint spawnPoint in spawnPoints)
        {
            if (spawnPoint.SpawnPointId == spawnPointId)
                return spawnPoint;
        }

        return null;
    }

    private void LogAvailableSpawnPoints()
    {
        SceneSpawnPoint[] spawnPoints = FindObjectsByType<SceneSpawnPoint>(FindObjectsSortMode.None);

        if (spawnPoints.Length == 0)
        {
            Debug.LogWarning("SceneLoader: No SceneSpawnPoint objects found in current scene.");
            return;
        }

        Debug.Log("SceneLoader: Available spawn points:");

        foreach (SceneSpawnPoint spawnPoint in spawnPoints)
        {
            Debug.Log($"- ID='{spawnPoint.SpawnPointId}' Object='{spawnPoint.name}' Position={spawnPoint.transform.position}");
        }
    }

    private void ReconnectCameraToPlayer()
    {
        GameObject player = GetPersistentPlayerObject();

        if (player == null)
        {
            Debug.LogWarning("SceneLoader: Cannot reconnect camera. Player not found.");
            return;
        }

        PlayerFollowCamera followCamera = FindFirstObjectByType<PlayerFollowCamera>();

        if (followCamera != null)
        {
            followCamera.SetTarget(player.transform);
            Debug.Log("SceneLoader: Camera target reconnected.");
        }
        else
        {
            Debug.LogWarning("SceneLoader: PlayerFollowCamera not found.");
        }

        PlayerController playerController = player.GetComponent<PlayerController>();

        if (playerController != null && Camera.main != null)
        {
            playerController.SetCameraTransform(Camera.main.transform);
            Debug.Log("SceneLoader: Player camera transform reconnected.");
        }
    }
}