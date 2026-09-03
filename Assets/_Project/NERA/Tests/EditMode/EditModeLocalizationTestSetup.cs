using NERA.Localization;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace NERA.Tests
{
    [SetUpFixture]
    public sealed class EditModeLocalizationTestSetup
    {
        private bool hadPreference;
        private string previousPreference;
        private Locale previousLocale;

        [OneTimeSetUp]
        public void SelectEnglish()
        {
            hadPreference = PlayerPrefs.HasKey(
                NERALocalization.LocalePreferenceKey);
            previousPreference = PlayerPrefs.GetString(
                NERALocalization.LocalePreferenceKey,
                NERALocalization.EnglishCode);
            previousLocale = LocalizationSettings.HasSettings
                ? LocalizationSettings.SelectedLocale
                : null;

            PlayerPrefs.SetString(
                NERALocalization.LocalePreferenceKey,
                NERALocalization.EnglishCode);
            PlayerPrefs.Save();
            NERALocalization.SetLocale(NERALocalization.EnglishCode);
        }

        [OneTimeTearDown]
        public void RestoreLocale()
        {
            if (LocalizationSettings.HasSettings)
                LocalizationSettings.SelectedLocale = previousLocale;

            if (hadPreference)
            {
                PlayerPrefs.SetString(
                    NERALocalization.LocalePreferenceKey,
                    previousPreference);
            }
            else
            {
                PlayerPrefs.DeleteKey(
                    NERALocalization.LocalePreferenceKey);
            }
            PlayerPrefs.Save();
        }
    }
}
