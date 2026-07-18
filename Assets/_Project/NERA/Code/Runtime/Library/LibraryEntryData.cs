using UnityEngine;

namespace NERA.Library
{
    [CreateAssetMenu(
        fileName = "LibraryEntry",
        menuName = "NERA/Library/Entry"
    )]
    public sealed class LibraryEntryData : ScriptableObject
    {
        [SerializeField] private string entryId;
        [SerializeField] private string title;
        [SerializeField, TextArea(4, 12)] private string body;
        [SerializeField] private Sprite illustration;

        public string EntryId => entryId;
        public string Title => title;
        public string Body => body;
        public Sprite Illustration => illustration;
    }
}
