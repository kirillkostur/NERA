using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class GameSessionState : MonoBehaviour
{
    public static GameSessionState Instance { get; private set; }

    [Header("Debug")]
    [SerializeField] private bool logStateChanges = true;

    private readonly HashSet<string> foundItemIds = new HashSet<string>();
    private readonly HashSet<string> completedObjectiveIds = new HashSet<string>();
    private readonly Dictionary<string, TranslationState> libraryTranslationStates = new Dictionary<string, TranslationState>();

    public event Action StateChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"GameSessionState duplicate destroyed: {name}");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void MarkItemFound(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return;

        bool added = foundItemIds.Add(itemId);

        if (!added)
            return;

        if (logStateChanges)
            Debug.Log($"GameSessionState: Item found '{itemId}'.");

        StateChanged?.Invoke();
    }

    public bool IsItemFound(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return false;

        return foundItemIds.Contains(itemId);
    }

    public void MarkObjectiveCompleted(string objectiveId)
    {
        if (string.IsNullOrWhiteSpace(objectiveId))
            return;

        bool added = completedObjectiveIds.Add(objectiveId);

        if (!added)
            return;

        if (logStateChanges)
            Debug.Log($"GameSessionState: Objective completed '{objectiveId}'.");

        StateChanged?.Invoke();
    }

    public bool IsObjectiveCompleted(string objectiveId)
    {
        if (string.IsNullOrWhiteSpace(objectiveId))
            return false;

        return completedObjectiveIds.Contains(objectiveId);
    }

    public void SetLibraryEntryTranslationState(string entryId, TranslationState state)
    {
        if (string.IsNullOrWhiteSpace(entryId))
            return;

        bool hasCurrentState = libraryTranslationStates.TryGetValue(entryId, out TranslationState currentState);

        if (hasCurrentState && currentState == state)
            return;

        libraryTranslationStates[entryId] = state;

        if (logStateChanges)
            Debug.Log($"GameSessionState: Library entry '{entryId}' translation state set to '{state}'.");

        StateChanged?.Invoke();
    }

    public bool HasLibraryEntryTranslationState(string entryId)
    {
        if (string.IsNullOrWhiteSpace(entryId))
            return false;

        return libraryTranslationStates.ContainsKey(entryId);
    }

    public TranslationState GetLibraryEntryTranslationState(string entryId, TranslationState fallbackState)
    {
        if (string.IsNullOrWhiteSpace(entryId))
            return fallbackState;

        if (libraryTranslationStates.TryGetValue(entryId, out TranslationState state))
            return state;

        return fallbackState;
    }
}