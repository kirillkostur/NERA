using UnityEngine;

[CreateAssetMenu(
    fileName = "LibraryEntry_NewEntry",
    menuName = "NERA/Library/Library Entry Data"
)]
public class LibraryEntryData : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string entryId = "new_library_entry";
    [SerializeField] private LocalizedText title;

    [Header("Unlock")]
    [SerializeField] private ItemData relatedItem;

    [Header("Translation")]
    [SerializeField] private TranslationState defaultTranslationState = TranslationState.Untranslated;

    [Header("Texts")]
    [SerializeField] private LocalizedText lockedText;
    [SerializeField] private LocalizedText untranslatedText;
    [SerializeField] private LocalizedText partiallyTranslatedText;
    [SerializeField] private LocalizedText translatedText;

    public string EntryId => entryId;
    public LocalizedText Title => title;
    public ItemData RelatedItem => relatedItem;
    public TranslationState DefaultTranslationState => defaultTranslationState;

    public string GetTextByState(TranslationState state)
    {
        switch (state)
        {
            case TranslationState.Translated:
                return translatedText.GetText();

            case TranslationState.PartiallyTranslated:
                return partiallyTranslatedText.GetText();

            case TranslationState.Untranslated:
                return untranslatedText.GetText();

            case TranslationState.Unknown:
            default:
                return lockedText.GetText();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(entryId))
            entryId = name.ToLowerInvariant().Replace(" ", "_");
    }
#endif
}