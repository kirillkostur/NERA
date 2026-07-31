using System;
using System.Collections.Generic;
using UnityEngine;

namespace NERA.Quests
{
    [CreateAssetMenu(
        fileName = "QuestCatalog",
        menuName = "NERA/Quests/Quest Catalog")]
    public sealed class QuestCatalog : ScriptableObject
    {
        [SerializeField] private List<QuestDefinition> definitions =
            new List<QuestDefinition>();

        private readonly Dictionary<string, QuestDefinition> definitionsById =
            new Dictionary<string, QuestDefinition>(StringComparer.Ordinal);

        public IReadOnlyList<QuestDefinition> Definitions =>
            definitions ??
            (IReadOnlyList<QuestDefinition>)Array.Empty<QuestDefinition>();

        public static QuestCatalog LoadDefault()
        {
            return Resources.Load<QuestCatalog>(
                "Quests/QuestCatalog_Default");
        }

        public QuestDefinition Find(string questId)
        {
            RebuildIndex();
            definitionsById.TryGetValue(
                QuestIdUtility.Normalize(questId),
                out QuestDefinition definition);
            return definition;
        }

        public bool TryValidate(out string error)
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            for (int definitionIndex = 0;
                 definitionIndex < Definitions.Count;
                 definitionIndex++)
            {
                QuestDefinition definition = Definitions[definitionIndex];
                if (definition == null)
                {
                    error = $"Quest catalog entry {definitionIndex} is null.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(definition.QuestId))
                {
                    error = $"Quest '{definition.name}' has no stable Quest ID.";
                    return false;
                }

                if (!ids.Add(definition.QuestId))
                {
                    error = $"Duplicate Quest ID '{definition.QuestId}'.";
                    return false;
                }

                if (!definition.TryValidate(out error))
                    return false;
            }

            error = string.Empty;
            return true;
        }

        private void OnEnable()
        {
            RebuildIndex();
        }

        private void OnValidate()
        {
            RebuildIndex();
        }

        private void RebuildIndex()
        {
            definitionsById.Clear();
            foreach (QuestDefinition definition in Definitions)
            {
                if (definition == null ||
                    string.IsNullOrWhiteSpace(definition.QuestId) ||
                    definitionsById.ContainsKey(definition.QuestId))
                {
                    continue;
                }

                definitionsById.Add(definition.QuestId, definition);
            }
        }
    }
}
