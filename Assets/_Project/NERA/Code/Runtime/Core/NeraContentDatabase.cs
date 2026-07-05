using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "NERA_ContentDatabase",
    menuName = "NERA/Core/Content Database"
)]
public class NeraContentDatabase : ScriptableObject
{
    [Header("UI Text")]
    [SerializeField] private TerminalTextData terminalTextData;
    [SerializeField] private InteractionTextData interactionTextData;

    [Header("Expeditions")]
    [SerializeField] private List<ExpeditionData> expeditions = new List<ExpeditionData>();

    [Header("Library")]
    [SerializeField] private List<LibraryEntryData> libraryEntries = new List<LibraryEntryData>();

    public TerminalTextData TerminalTextData => terminalTextData;
    public InteractionTextData InteractionTextData => interactionTextData;

    public IReadOnlyList<ExpeditionData> Expeditions => expeditions;
    public IReadOnlyList<LibraryEntryData> LibraryEntries => libraryEntries;

    public ExpeditionData GetExpeditionById(string expeditionId)
    {
        if (string.IsNullOrWhiteSpace(expeditionId))
            return null;

        for (int i = 0; i < expeditions.Count; i++)
        {
            ExpeditionData expedition = expeditions[i];

            if (expedition == null)
                continue;

            if (expedition.ExpeditionId == expeditionId)
                return expedition;
        }

        return null;
    }

    public LibraryEntryData GetLibraryEntryById(string entryId)
    {
        if (string.IsNullOrWhiteSpace(entryId))
            return null;

        for (int i = 0; i < libraryEntries.Count; i++)
        {
            LibraryEntryData entry = libraryEntries[i];

            if (entry == null)
                continue;

            if (entry.EntryId == entryId)
                return entry;
        }

        return null;
    }
}