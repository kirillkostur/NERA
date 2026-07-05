using System;
using UnityEngine;

[DisallowMultipleComponent]
public class LocalizationManager : MonoBehaviour
{
    public static LocalizationManager Instance { get; private set; }

    private const string LanguagePrefsKey = "NERA_LANGUAGE";

    [Header("Language")]
    [SerializeField] private GameLanguage defaultLanguage = GameLanguage.RU;
    [SerializeField] private GameLanguage currentLanguage = GameLanguage.RU;
    [SerializeField] private bool loadSavedLanguage = true;
    [SerializeField] private bool saveLanguageChanges = true;

    public GameLanguage CurrentLanguage => currentLanguage;

    public event Action<GameLanguage> LanguageChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"LocalizationManager duplicate destroyed: {name}");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (loadSavedLanguage)
            currentLanguage = LoadLanguage();
        else
            currentLanguage = defaultLanguage;

        Debug.Log($"LocalizationManager initialized. Language: {currentLanguage}");
    }

    public void SetLanguage(GameLanguage language)
    {
        if (currentLanguage == language)
            return;

        currentLanguage = language;

        if (saveLanguageChanges)
            SaveLanguage(language);

        Debug.Log($"LocalizationManager: Language changed to {currentLanguage}");

        LanguageChanged?.Invoke(currentLanguage);
    }

    public void SetRussian()
    {
        SetLanguage(GameLanguage.RU);
    }

    public void SetEnglish()
    {
        SetLanguage(GameLanguage.EN);
    }

    private GameLanguage LoadLanguage()
    {
        int savedValue = PlayerPrefs.GetInt(LanguagePrefsKey, (int)defaultLanguage);

        if (!Enum.IsDefined(typeof(GameLanguage), savedValue))
            return defaultLanguage;

        return (GameLanguage)savedValue;
    }

    private void SaveLanguage(GameLanguage language)
    {
        PlayerPrefs.SetInt(LanguagePrefsKey, (int)language);
        PlayerPrefs.Save();
    }
}