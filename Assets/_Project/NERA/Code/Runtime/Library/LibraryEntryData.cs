using UnityEngine;
using UnityEngine.Serialization;
using NERA.Localization;

namespace NERA.Library
{
    public enum LibraryCategory
    {
        Station,
        Anomaly,
        Records
    }
    [CreateAssetMenu(
        fileName = "LibraryEntry",
        menuName = "NERA/Library/Entry"
    )]
    public sealed class LibraryEntryData : ScriptableObject
    {
        [SerializeField] private string entryId;
        [SerializeField] private string title;
        [SerializeField] private LibraryCategory category = LibraryCategory.Anomaly;
        [FormerlySerializedAs("body")]
        [SerializeField, TextArea(4, 12)] private string description;
        [SerializeField] private Sprite illustration;

        public string EntryId => entryId;
        public string Title => NERALocalization.Content(
            "library", entryId, "title", title);
        public LibraryCategory Category => category;
        public string Description => NERALocalization.Content(
            "library", entryId, "description", description);
        public Sprite Illustration => illustration;
    }
}
