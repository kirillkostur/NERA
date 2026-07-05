using UnityEngine;

[CreateAssetMenu(
    fileName = "ExpeditionData_NewExpedition",
    menuName = "NERA/Expeditions/Expedition Data"
)]
public class ExpeditionData : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string expeditionId = "new_expedition";
    [SerializeField] private LocalizedText displayName;

    [Header("Scene")]
    [SerializeField] private string sceneName;
    [SerializeField] private string spawnPointId;

    [Header("Content")]
    [SerializeField] private LocalizedText locationType;
    [SerializeField] private LocalizedText description;
    [SerializeField] private LocalizedText objectiveText;
    [SerializeField] private LocalizedText completedObjectiveText;

    [Header("Completion")]
    [SerializeField] private string completionObjectiveId = "expedition_01_find_object";

    [Header("State")]
    [SerializeField] private ExpeditionStatus defaultStatus = ExpeditionStatus.Available;
    [SerializeField] private bool requiresStationPower = true;

    public string ExpeditionId => expeditionId;
    public LocalizedText DisplayName => displayName;
    public string SceneName => sceneName;
    public string SpawnPointId => spawnPointId;
    public LocalizedText LocationType => locationType;
    public LocalizedText Description => description;
    public LocalizedText ObjectiveText => objectiveText;
    public LocalizedText CompletedObjectiveText => completedObjectiveText;
    public string CompletionObjectiveId => completionObjectiveId;
    public ExpeditionStatus DefaultStatus => defaultStatus;
    public bool RequiresStationPower => requiresStationPower;

    public string GetObjectivePreviewText()
    {
        ExpeditionStatus runtimeStatus = GetRuntimeStatus();

        if (runtimeStatus == ExpeditionStatus.Completed)
            return completedObjectiveText.GetText("Objective completed.");

        return objectiveText.GetText("Find the expedition objective.");
    }

    public ExpeditionStatus GetRuntimeStatus()
    {
        ExpeditionStatus status = defaultStatus;

        if (status == ExpeditionStatus.Locked)
            return ExpeditionStatus.Locked;

        if (GameSessionState.Instance == null)
            return status;

        if (!string.IsNullOrWhiteSpace(completionObjectiveId) &&
            GameSessionState.Instance.IsObjectiveCompleted(completionObjectiveId))
        {
            return ExpeditionStatus.Completed;
        }

        return status;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(expeditionId))
            expeditionId = name.ToLowerInvariant().Replace(" ", "_");
    }
#endif
}