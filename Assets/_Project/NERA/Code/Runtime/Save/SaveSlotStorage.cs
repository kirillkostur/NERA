using System;
using System.IO;
using UnityEngine;

namespace NERA.Save
{
    /// <summary>
    /// Owns the three production save-slot paths and the one-time migration
    /// from the pre-slot single save file into slot 1.
    /// </summary>
    public static class SaveSlotStorage
    {
        public const int SlotCount = 3;
        public const int DefaultSlot = 1;
        public const int MaxBackupGenerations = 5;
        public const string LegacySingleFileName = "nera_save.json";
        public const string SaveRootEnvironmentVariable = "NERA_SAVE_ROOT";

        private const string SlotFilePrefix = "nera_save_";
        private const string MigrationMarkerName = ".save_slots_v1.migrated";

        public static string StorageRoot
        {
            get
            {
                string overriddenRoot = Environment.GetEnvironmentVariable(
                    SaveRootEnvironmentVariable);
                return string.IsNullOrWhiteSpace(overriddenRoot)
                    ? Application.persistentDataPath
                    : Path.GetFullPath(overriddenRoot.Trim());
            }
        }

        public static int NormalizeSlot(int slot)
        {
            return Mathf.Clamp(slot, 1, SlotCount);
        }

        public static string GetSlotFileName(int slot)
        {
            return $"{SlotFilePrefix}{NormalizeSlot(slot)}.json";
        }

        public static string GetSlotPath(int slot)
        {
            return GetSlotPath(StorageRoot, slot);
        }

        public static string GetSlotPath(string persistentRoot, int slot)
        {
            return Path.Combine(persistentRoot, GetSlotFileName(slot));
        }

        public static string GetBackupPath(int slot, int generation)
        {
            return GetBackupPath(
                StorageRoot,
                slot,
                generation);
        }

        public static string GetBackupPath(
            string persistentRoot,
            int slot,
            int generation)
        {
            int normalizedGeneration = Mathf.Clamp(
                generation,
                1,
                MaxBackupGenerations);
            return Path.Combine(
                persistentRoot,
                $"{SlotFilePrefix}{NormalizeSlot(slot)}.backup_" +
                $"{normalizedGeneration}.json");
        }

        public static string GetCheckpointPath(int slot)
        {
            return GetCheckpointPath(StorageRoot, slot);
        }

        public static string GetCheckpointPath(string persistentRoot, int slot)
        {
            return Path.Combine(
                persistentRoot,
                $"{SlotFilePrefix}{NormalizeSlot(slot)}.checkpoint.json");
        }

        public static string GetCheckpointBackupPath(int slot)
        {
            return GetCheckpointBackupPath(
                StorageRoot,
                slot);
        }

        public static string GetCheckpointBackupPath(
            string persistentRoot,
            int slot)
        {
            return Path.Combine(
                persistentRoot,
                $"{SlotFilePrefix}{NormalizeSlot(slot)}.checkpoint.backup.json");
        }

        public static bool HasSave(int slot)
        {
            foreach (string path in GetLoadCandidates(
                         slot,
                         MaxBackupGenerations))
            {
                if (File.Exists(path))
                    return true;
            }

            return File.Exists(GetCheckpointPath(slot)) ||
                File.Exists(GetCheckpointBackupPath(slot));
        }

        public static bool HasAnySave()
        {
            for (int slot = 1; slot <= SlotCount; slot++)
            {
                if (HasSave(slot))
                    return true;
            }

            return false;
        }

        public static DateTime GetLastWriteTime(int slot)
        {
            string path = FindFirstExistingPath(slot);
            return File.Exists(path)
                ? File.GetLastWriteTime(path)
                : DateTime.MinValue;
        }

        public static float GetCompletionPercent(int slot)
        {
            string path = FindFirstExistingPath(slot);
            if (!File.Exists(path))
                return 0f;

            try
            {
                SaveGameData data = JsonUtility.FromJson<SaveGameData>(
                    File.ReadAllText(path));
                return data != null
                    ? Mathf.Clamp(data.completionPercent, 0f, 100f)
                    : 0f;
            }
            catch (Exception)
            {
                return 0f;
            }
        }

        public static bool TryMigrateLegacySingleSaveToSlotOne()
        {
            string persistentRoot = StorageRoot;
            string currentSinglePath = Path.Combine(
                persistentRoot,
                LegacySingleFileName);
            return TryMigrateLegacySingleSaveToSlotOne(
                persistentRoot,
                SavePathMigration.GetLegacySavePath(currentSinglePath));
        }

        public static bool TryMigrateLegacySingleSaveToSlotOne(
            string persistentRoot,
            string previousIdentitySavePath)
        {
            if (string.IsNullOrWhiteSpace(persistentRoot))
                return false;

            string markerPath = Path.Combine(
                persistentRoot,
                MigrationMarkerName);
            if (File.Exists(markerPath))
                return false;

            string slotOnePath = Path.Combine(
                persistentRoot,
                GetSlotFileName(DefaultSlot));
            if (File.Exists(slotOnePath))
            {
                WriteMigrationMarker(markerPath);
                return false;
            }

            string currentSinglePath = Path.Combine(
                persistentRoot,
                LegacySingleFileName);
            bool migrated = SavePathMigration.TryMigrateSave(
                currentSinglePath,
                slotOnePath);
            if (!migrated)
            {
                migrated = SavePathMigration.TryMigrateSave(
                    previousIdentitySavePath,
                    slotOnePath);
            }

            if (migrated)
                WriteMigrationMarker(markerPath);

            return migrated;
        }

        public static void DeleteSlot(int slot)
        {
            int normalizedSlot = NormalizeSlot(slot);
            string path = GetSlotPath(normalizedSlot);
            DeleteIfExists(path);
            DeleteIfExists(path + ".tmp");
            DeleteIfExists(path + ".migration");
            DeleteIfExists(GetCheckpointPath(normalizedSlot));
            DeleteIfExists(GetCheckpointPath(normalizedSlot) + ".tmp");
            DeleteIfExists(GetCheckpointBackupPath(normalizedSlot));
            for (int generation = 1;
                 generation <= MaxBackupGenerations;
                 generation++)
            {
                DeleteIfExists(GetBackupPath(normalizedSlot, generation));
            }

            if (normalizedSlot != DefaultSlot)
                return;

            string currentSinglePath = Path.Combine(
                StorageRoot,
                LegacySingleFileName);
            DeleteIfExists(currentSinglePath);
            DeleteIfExists(currentSinglePath + ".tmp");
            SavePathMigration.DeleteLegacySave(currentSinglePath);
        }

        public static void DeleteAllSlots()
        {
            for (int slot = 1; slot <= SlotCount; slot++)
                DeleteSlot(slot);
        }

        public static string[] GetLoadCandidates(
            int slot,
            int backupGenerations)
        {
            return GetLoadCandidates(
                StorageRoot,
                slot,
                backupGenerations);
        }

        public static string[] GetLoadCandidates(
            string persistentRoot,
            int slot,
            int backupGenerations)
        {
            int generations = Mathf.Clamp(
                backupGenerations,
                0,
                MaxBackupGenerations);
            string[] paths = new string[generations + 1];
            paths[0] = GetSlotPath(persistentRoot, slot);
            for (int generation = 1;
                 generation <= generations;
                 generation++)
            {
                paths[generation] = GetBackupPath(
                    persistentRoot,
                    slot,
                    generation);
            }

            return paths;
        }

        public static void RotateBackups(int slot, int backupGenerations)
        {
            RotateBackups(
                StorageRoot,
                slot,
                backupGenerations);
        }

        public static void RotateBackups(
            string persistentRoot,
            int slot,
            int backupGenerations)
        {
            int generations = Mathf.Clamp(
                backupGenerations,
                0,
                MaxBackupGenerations);
            if (generations == 0)
                return;

            for (int generation = generations;
                 generation >= 2;
                 generation--)
            {
                string source = GetBackupPath(
                    persistentRoot,
                    slot,
                    generation - 1);
                if (File.Exists(source))
                    File.Copy(
                        source,
                        GetBackupPath(persistentRoot, slot, generation),
                        true);
            }

            string primary = GetSlotPath(persistentRoot, slot);
            if (File.Exists(primary))
            {
                File.Copy(
                    primary,
                    GetBackupPath(persistentRoot, slot, 1),
                    true);
            }
        }

        private static string FindFirstExistingPath(int slot)
        {
            foreach (string path in GetLoadCandidates(
                         slot,
                         MaxBackupGenerations))
            {
                if (File.Exists(path))
                    return path;
            }

            string checkpointPath = GetCheckpointPath(slot);
            if (File.Exists(checkpointPath))
                return checkpointPath;

            string checkpointBackupPath = GetCheckpointBackupPath(slot);
            if (File.Exists(checkpointBackupPath))
                return checkpointBackupPath;

            return GetSlotPath(slot);
        }

        private static void WriteMigrationMarker(string markerPath)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(markerPath));
                File.WriteAllText(markerPath, "slot-save-layout-v1");
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "SaveGame: Could not write the save-slot migration " +
                    $"marker. {exception.Message}");
            }
        }

        private static void DeleteIfExists(string path)
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                File.Delete(path);
        }
    }
}
