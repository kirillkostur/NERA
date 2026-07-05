using System;
using UnityEngine;

[DisallowMultipleComponent]
public class ExpeditionObjectiveController : MonoBehaviour
{
    public static ExpeditionObjectiveController Instance { get; private set; }

    [Header("Objective")]
    [SerializeField] private string objectiveId = "expedition_01_find_object";
    [SerializeField] private LocalizedText activeText;
    [SerializeField] private LocalizedText completedText;

    [Header("State")]
    [SerializeField] private bool activateOnStart = true;
    [SerializeField] private bool respectCompletedSessionState = true;
    [SerializeField] private bool showCompletedTextIfAlreadyCompleted = true;
    [SerializeField] private ObjectiveState state = ObjectiveState.Inactive;

    public string ObjectiveId => objectiveId;
    public ObjectiveState State => state;

    public bool IsActive => state == ObjectiveState.Active;
    public bool IsCompleted => state == ObjectiveState.Completed;

    public event Action ObjectiveChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"ExpeditionObjectiveController duplicate destroyed: {name}");
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (respectCompletedSessionState && IsCompletedInSessionState())
        {
            state = ObjectiveState.Completed;

            Debug.Log($"Objective already completed in session: {objectiveId}");

            ObjectiveChanged?.Invoke();
            return;
        }

        if (activateOnStart)
            ActivateObjective();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void ActivateObjective()
    {
        if (state == ObjectiveState.Completed)
            return;

        state = ObjectiveState.Active;

        Debug.Log($"Objective activated: {objectiveId}");

        ObjectiveChanged?.Invoke();
    }

    public void CompleteObjective()
    {
        if (state == ObjectiveState.Completed)
            return;

        state = ObjectiveState.Completed;

        if (GameSessionState.Instance != null)
            GameSessionState.Instance.MarkObjectiveCompleted(objectiveId);

        Debug.Log($"Objective completed: {objectiveId}");

        ObjectiveChanged?.Invoke();
    }

    public string GetCurrentText()
    {
        switch (state)
        {
            case ObjectiveState.Completed:
                if (!showCompletedTextIfAlreadyCompleted)
                    return string.Empty;

                return completedText.GetText("Object found. Return to the station.");

            case ObjectiveState.Active:
                return activeText.GetText("Find the first research object.");

            case ObjectiveState.Inactive:
            default:
                return string.Empty;
        }
    }

    private bool IsCompletedInSessionState()
    {
        if (GameSessionState.Instance == null)
            return false;

        return GameSessionState.Instance.IsObjectiveCompleted(objectiveId);
    }
}