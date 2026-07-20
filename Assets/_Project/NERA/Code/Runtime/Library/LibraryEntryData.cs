using UnityEngine;
using UnityEngine.Serialization;

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
        public string Title => title;
        public LibraryCategory Category => category;
        public string Description => description;
        public Sprite Illustration => illustration;
    }
}
