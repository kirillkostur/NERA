using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace NERA.Localization
{
    public static class NERALocalization
    {
        public const string CommonTable = "Common";
        public const string MainMenuTable = "MainMenu";
        public const string HudTable = "HUD";
        public const string TerminalTable = "Terminal";
        public const string InventoryLaboratoryTable = "InventoryLaboratory";
        public const string ContentTable = "Content";
        public const string QuestsTable = "Quests";

        public const string EnglishCode = "en";
        public const string RussianCode = "ru";
        public const string LocalePreferenceKey = "nera.locale";

        public static event Action LocaleChanged;

        private static bool subscribed;

        public static string CurrentLocaleCode
        {
            get
            {
                if (!LocalizationSettings.HasSettings)
                    return EnglishCode;

                return LocalizationSettings.SelectedLocale?.Identifier.Code ??
                    EnglishCode;
            }
        }

        public static void EnsureInitialized()
        {
            if (subscribed || !LocalizationSettings.HasSettings)
                return;

            subscribed = true;
            LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
        }

        public static bool SetLocale(string localeCode)
        {
            EnsureInitialized();
            if (!LocalizationSettings.HasSettings ||
                string.IsNullOrWhiteSpace(localeCode))
            {
                return false;
            }

            Locale locale = LocalizationSettings.AvailableLocales.GetLocale(
                new LocaleIdentifier(localeCode.Trim().ToLowerInvariant()));
            if (locale == null)
                return false;

            LocalizationSettings.SelectedLocale = locale;
            PlayerPrefs.SetString(LocalePreferenceKey, locale.Identifier.Code);
            PlayerPrefs.Save();
            return true;
        }

        public static void ToggleEnglishRussian()
        {
            SetLocale(CurrentLocaleCode.StartsWith(
                RussianCode,
                StringComparison.OrdinalIgnoreCase)
                    ? EnglishCode
                    : RussianCode);
        }

        public static string Get(
            string table,
            string key,
            string fallback = null,
            params object[] arguments)
        {
            EnsureInitialized();
            if (!LocalizationSettings.HasSettings ||
                string.IsNullOrWhiteSpace(table) ||
                string.IsNullOrWhiteSpace(key))
            {
                return fallback ?? string.Empty;
            }

            try
            {
                LocalizedStringDatabase.TableEntryResult entry =
                    LocalizationSettings.StringDatabase.GetTableEntry(
                        table,
                        key,
                        null,
                        FallbackBehavior.UseProjectSettings);
                if (entry.Entry == null)
                    return fallback ?? string.Empty;

                string value = LocalizationSettings.StringDatabase
                    .GetLocalizedString(
                        table,
                        key,
                        null,
                        FallbackBehavior.UseProjectSettings,
                        arguments ?? Array.Empty<object>());

                return string.IsNullOrEmpty(value) ? fallback ?? string.Empty : value;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"Localization entry '{table}/{key}' could not be read: " +
                    exception.Message);
                return fallback ?? string.Empty;
            }
        }

        public static string Content(
            string category,
            string id,
            string field,
            string fallback)
        {
            string normalizedId = NormalizeKeyPart(id);
            if (string.IsNullOrEmpty(normalizedId))
                return fallback ?? string.Empty;

            return Get(
                ContentTable,
                $"{NormalizeKeyPart(category)}.{normalizedId}." +
                NormalizeKeyPart(field),
                fallback);
        }

        public static string Quest(
            string questId,
            string field,
            string fallback,
            params object[] arguments)
        {
            return Get(
                QuestsTable,
                $"quest.{NormalizeKeyPart(questId)}.{field}",
                fallback,
                arguments);
        }

        public static string NormalizeKeyPart(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string normalized = Regex.Replace(
                value.Trim().ToLowerInvariant(),
                @"[^a-z0-9._]+",
                "_");
            return Regex.Replace(normalized, @"_+", "_").Trim('_');
        }

        private static void OnSelectedLocaleChanged(Locale locale)
        {
            if (locale != null)
            {
                PlayerPrefs.SetString(
                    LocalePreferenceKey,
                    locale.Identifier.Code);
                PlayerPrefs.Save();
            }

            Action callbacks = LocaleChanged;
            if (callbacks == null)
                return;

            foreach (Delegate subscribed in callbacks.GetInvocationList())
            {
                Action callback = (Action)subscribed;
                if (callback.Target is UnityEngine.Object unityTarget &&
                    unityTarget == null)
                {
                    LocaleChanged -= callback;
                    continue;
                }

                callback.Invoke();
            }
        }
    }
}
