using System;
using UnityEngine;

[Serializable]
public class LocalizedText
{
    [TextArea(1, 6)]
    [SerializeField] private string ru;

    [TextArea(1, 6)]
    [SerializeField] private string en;

    public string Ru => ru;
    public string En => en;

    public string GetText()
    {
        return GetText(string.Empty);
    }

    public string GetText(string fallback)
    {
        if (LocalizationManager.Instance == null)
            return GetText(GameLanguage.RU, fallback);

        return GetText(LocalizationManager.Instance.CurrentLanguage, fallback);
    }

    public string GetText(GameLanguage language)
    {
        return GetText(language, string.Empty);
    }

    public string GetText(GameLanguage language, string fallback)
    {
        string result;

        switch (language)
        {
            case GameLanguage.EN:
                result = string.IsNullOrWhiteSpace(en) ? ru : en;
                break;

            case GameLanguage.RU:
            default:
                result = string.IsNullOrWhiteSpace(ru) ? en : ru;
                break;
        }

        if (string.IsNullOrWhiteSpace(result))
            return fallback;

        return result;
    }
}