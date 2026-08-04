using System.IO;
using NERA.Save;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NERA.EditorTools
{
    public static class SaveGameMenu
    {
        private const string RuntimeScenePath =
            "Assets/_Project/NERA/Scenes/MainScene.unity";

        [MenuItem("Project/Save/Load", priority = 100)]
        private static void Load()
        {
            if (EditorApplication.isPlaying)
            {
                if (SaveGameController.Instance != null)
                    SaveGameController.Instance.Load();
                else
                    Debug.LogWarning("Save menu: SaveGameController is not available.");

                return;
            }

            SaveSlotStorage.TryMigrateLegacySingleSaveToSlotOne();
            if (!File.Exists(GetSavePath()))
            {
                EditorUtility.DisplayDialog(
                    "NERA Save",
                    "Save file does not exist yet.",
                    "OK"
                );
                return;
            }

            SceneAsset runtimeScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(
                RuntimeScenePath
            );

            if (runtimeScene == null)
            {
                Debug.LogError(
                    $"Save menu: Runtime scene not found at " +
                    $"'{RuntimeScenePath}'.");
                return;
            }

            EditorSceneManager.playModeStartScene = runtimeScene;
            EditorApplication.EnterPlaymode();
        }

        [MenuItem("Project/Save/Save Now", priority = 101)]
        private static void SaveNow()
        {
            if (SaveGameController.Instance != null)
                SaveGameController.Instance.Save();
            else
                Debug.LogWarning("Save menu: Enter Play Mode before saving.");
        }

        [MenuItem("Project/Save/Save Now", true)]
        private static bool ValidateSaveNow()
        {
            return EditorApplication.isPlaying;
        }

        [MenuItem("Project/Save/Clear/Slot 1", priority = 102)]
        private static void ClearSlot1()
        {
            ClearSlot(1);
        }

        [MenuItem("Project/Save/Clear/Slot 2", priority = 103)]
        private static void ClearSlot2()
        {
            ClearSlot(2);
        }

        [MenuItem("Project/Save/Clear/Slot 3", priority = 104)]
        private static void ClearSlot3()
        {
            ClearSlot(3);
        }

        [MenuItem("Project/Save/Clear/All Slots", priority = 120)]
        private static void ClearAllSlots()
        {
            SaveGameController controller = SaveGameController.Instance;
            bool hasActiveRuntime =
                EditorApplication.isPlaying && controller != null;
            if (!SaveSlotStorage.HasAnySave() && !hasActiveRuntime)
            {
                EditorUtility.DisplayDialog(
                    "Clear NERA Saves",
                    "All save slots are already empty.",
                    "OK");
                return;
            }

            bool confirmed = EditorUtility.DisplayDialog(
                "Clear All NERA Saves",
                "Delete save slots 1, 2 and 3? This cannot be undone." +
                (hasActiveRuntime
                    ? " The current runtime progress will also be reset."
                    : string.Empty),
                "Clear All",
                "Cancel");

            if (!confirmed)
                return;

            SaveSlotStorage.DeleteAllSlots();
            if (hasActiveRuntime)
                controller.ClearSave(true);

            Debug.Log("Save menu: All three save slots were cleared.");
        }

        [MenuItem("Project/Save/Open Save Folder", priority = 120)]
        private static void OpenSaveFolder()
        {
            Directory.CreateDirectory(SaveSlotStorage.StorageRoot);
            EditorUtility.RevealInFinder(SaveSlotStorage.StorageRoot);
        }

        private static string GetSavePath()
        {
            SaveGameController controller =
                Object.FindFirstObjectByType<SaveGameController>();

            return controller != null
                ? controller.SavePath
                : SaveGameController.DefaultSavePath;
        }

        private static void ClearSlot(int slot)
        {
            int normalizedSlot = SaveSlotStorage.NormalizeSlot(slot);
            SaveGameController controller = SaveGameController.Instance;
            bool isActiveRuntimeSlot =
                EditorApplication.isPlaying &&
                controller != null &&
                controller.ActiveSaveSlot == normalizedSlot;
            if (!SaveSlotStorage.HasSave(normalizedSlot) &&
                !isActiveRuntimeSlot)
            {
                EditorUtility.DisplayDialog(
                    $"Clear NERA Save Slot {normalizedSlot}",
                    $"Save slot {normalizedSlot} is already empty.",
                    "OK");
                return;
            }

            bool confirmed = EditorUtility.DisplayDialog(
                $"Clear NERA Save Slot {normalizedSlot}",
                $"Delete save slot {normalizedSlot}? This cannot be undone." +
                (isActiveRuntimeSlot
                    ? " The current runtime progress will also be reset."
                    : string.Empty),
                "Clear",
                "Cancel");
            if (!confirmed)
                return;

            if (isActiveRuntimeSlot)
                controller.ClearSave(true);
            else
                SaveSlotStorage.DeleteSlot(normalizedSlot);

            Debug.Log(
                $"Save menu: Save slot {normalizedSlot} was cleared at " +
                $"'{SaveSlotStorage.GetSlotPath(normalizedSlot)}'.");
        }
    }
}
