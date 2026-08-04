using System;
using System.IO;
using UnityEngine;

namespace NERA.Save
{
    /// <summary>
    /// Moves saves created before the production Company/Product identity was
    /// fixed. The legacy file is removed only after the new file exists.
    /// </summary>
    public static class SavePathMigration
    {
        public const string LegacyCompanyName = "DefaultCompany";
        public const string LegacyProductName = "My project";

        public static bool HasCurrentOrLegacySave(string currentSavePath)
        {
            return File.Exists(currentSavePath) ||
                   File.Exists(GetLegacySavePath(currentSavePath));
        }

        public static bool TryMigrateLegacySave(string currentSavePath)
        {
            string legacySavePath = GetLegacySavePath(currentSavePath);
            return TryMigrateSave(legacySavePath, currentSavePath);
        }

        public static bool TryMigrateSave(
            string sourceSavePath,
            string currentSavePath)
        {
            if (string.IsNullOrWhiteSpace(sourceSavePath) ||
                string.IsNullOrWhiteSpace(currentSavePath) ||
                PathsEqual(sourceSavePath, currentSavePath) ||
                File.Exists(currentSavePath) ||
                !File.Exists(sourceSavePath))
            {
                return false;
            }

            string temporaryPath = currentSavePath + ".migration";
            try
            {
                string destinationDirectory =
                    Path.GetDirectoryName(currentSavePath);
                if (string.IsNullOrWhiteSpace(destinationDirectory))
                    return false;

                Directory.CreateDirectory(destinationDirectory);
                DeleteIfExists(temporaryPath);
                File.Copy(sourceSavePath, temporaryPath, true);

                long sourceLength = new FileInfo(sourceSavePath).Length;
                long copiedLength = new FileInfo(temporaryPath).Length;
                if (sourceLength != copiedLength)
                {
                    throw new IOException(
                        "Legacy save copy size does not match the source.");
                }

                File.Move(temporaryPath, currentSavePath);

                try
                {
                    File.Delete(sourceSavePath);
                    DeleteIfExists(sourceSavePath + ".tmp");
                }
                catch (Exception cleanupException)
                {
                    Debug.LogWarning(
                        "Save migration completed, but the legacy copy could " +
                        $"not be removed: {cleanupException.Message}");
                }

                Debug.Log(
                    $"SaveGame: Migrated legacy save from " +
                    $"'{sourceSavePath}' to '{currentSavePath}'.");
                return true;
            }
            catch (Exception exception)
            {
                DeleteIfExists(temporaryPath);
                Debug.LogError(
                    $"SaveGame: Could not migrate legacy save from " +
                    $"'{sourceSavePath}' to '{currentSavePath}'.\n" +
                    exception);
                return false;
            }
        }

        public static void DeleteLegacySave(string currentSavePath)
        {
            string legacySavePath = GetLegacySavePath(currentSavePath);
            if (string.IsNullOrWhiteSpace(legacySavePath) ||
                PathsEqual(currentSavePath, legacySavePath))
            {
                return;
            }

            DeleteIfExists(legacySavePath);
            DeleteIfExists(legacySavePath + ".tmp");
        }

        public static string GetLegacySavePath(string currentSavePath)
        {
            if (string.IsNullOrWhiteSpace(currentSavePath))
                return string.Empty;

            string productDirectory = Path.GetDirectoryName(currentSavePath);
            if (string.IsNullOrWhiteSpace(productDirectory))
                return string.Empty;

            DirectoryInfo companyDirectory =
                Directory.GetParent(productDirectory);
            DirectoryInfo persistentDataRoot = companyDirectory?.Parent;
            string fileName = Path.GetFileName(currentSavePath);
            if (persistentDataRoot == null || string.IsNullOrWhiteSpace(fileName))
                return string.Empty;

            return Path.Combine(
                persistentDataRoot.FullName,
                LegacyCompanyName,
                LegacyProductName,
                fileName);
        }

        private static bool PathsEqual(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) ||
                string.IsNullOrWhiteSpace(right))
            {
                return false;
            }

            return string.Equals(
                Path.GetFullPath(left).TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar),
                Path.GetFullPath(right).TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar),
                Application.platform == RuntimePlatform.WindowsEditor ||
                Application.platform == RuntimePlatform.WindowsPlayer
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal);
        }

        private static void DeleteIfExists(string path)
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                File.Delete(path);
        }
    }
}
