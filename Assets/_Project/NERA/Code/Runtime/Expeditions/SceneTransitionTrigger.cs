using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SceneTransitionTrigger : MonoBehaviour
{
    [Header("Transition")]
    [SerializeField] private string targetSceneName;
    [SerializeField] private string targetSpawnPointId = "Default";

    [Header("Trigger Settings")]
    [SerializeField] private bool loadOnTriggerEnter = true;
    [SerializeField] private bool requirePlayerTag = true;

    private bool isLoading;

    private void Reset()
    {
        Collider triggerCollider = GetComponent<Collider>();
        triggerCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!loadOnTriggerEnter)
            return;

        if (isLoading)
            return;

        if (requirePlayerTag && !other.CompareTag("Player"))
            return;

        LoadTargetScene();
    }

    public void LoadTargetScene()
    {
        if (isLoading)
            return;

        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            Debug.LogError($"{name}: targetSceneName is empty.");
            return;
        }

        isLoading = true;

        if (SceneLoader.Instance == null)
        {
            Debug.LogError($"{name}: SceneLoader not found in scene.");
            return;
        }

        SceneLoader.Instance.LoadScene(targetSceneName, targetSpawnPointId);
    }
}