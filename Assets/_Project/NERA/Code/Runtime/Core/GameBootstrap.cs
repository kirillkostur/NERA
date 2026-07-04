using System.Collections;
using UnityEngine;

public class GameBootstrap : MonoBehaviour
{
    [Header("Start Scene")]
    [SerializeField] private string startSceneName = "Player_Station";
    [SerializeField] private string startSpawnPointId = "Station_Start";

    private IEnumerator Start()
    {
        yield return null;

        if (SceneLoader.Instance == null)
        {
            Debug.LogError("GameBootstrap: SceneLoader not found in Boot scene.");
            yield break;
        }

        SceneLoader.Instance.LoadScene(startSceneName, startSpawnPointId);
    }
}