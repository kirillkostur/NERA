using NERA.Library;
using UnityEngine;

namespace NERA.Research
{
    public enum ResearchItemFate
    {
        Destroy,
        Return
    }

    [CreateAssetMenu(fileName = "ResearchDefinition", menuName = "NERA/Research/Definition")]
    public sealed class ResearchDefinition : ScriptableObject
    {
        [SerializeField] private string researchId;
        [SerializeField] private string displayName;
        [SerializeField, Min(0.1f)] private float analysisDuration = 5f;
        [SerializeField] private ResearchItemFate itemFate = ResearchItemFate.Return;
        [SerializeField] private LibraryEntryData unlockedEntry;

        public string ResearchId => researchId;
        public string DisplayName => displayName;
        public float AnalysisDuration => analysisDuration;
        public ResearchItemFate ItemFate => itemFate;
        public LibraryEntryData UnlockedEntry => unlockedEntry;
    }
}
