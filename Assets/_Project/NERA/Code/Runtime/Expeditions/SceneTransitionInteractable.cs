using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SceneTransitionInteractable : BaseInteractable
{
    [Header("Scene Transition")]
    [SerializeField] private string targetSceneName;
    [SerializeField] private string targetSpawnPointId;

    [Header("Transition Safety")]
    [SerializeField] private bool disableAfterUse = true;

    private bool isLoading;

    private void Reset()
    {
        Collider transitionCollider = GetComponent<Collider>();
        transitionCollider.isTrigger = false;
    }

    protected override void OnInteractCompleted()
    {
        if (isLoading)
            return;

        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            Debug.LogError($"{name}: Target scene name is empty.");
            return;
        }

        if (string.IsNullOrWhiteSpace(targetSpawnPointId))
        {
            Debug.LogError($"{name}: Target spawn point id is empty.");
            return;
        }

        if (SceneLoader.Instance == null)
        {
            Debug.LogError($"{name}: SceneLoader not found.");
            return;
        }

        isLoading = true;

        if (disableAfterUse)
            SetCanInteract(false);

        Debug.Log($"{name}: Scene transition requested. Target Scene='{targetSceneName}', Target Spawn='{targetSpawnPointId}'");

        SceneLoader.Instance.LoadScene(targetSceneName, targetSpawnPointId);
    }
}