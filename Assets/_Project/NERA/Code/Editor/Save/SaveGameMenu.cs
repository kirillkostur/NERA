using System.IO;
using NERA.Save;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NERA.EditorTools
{
    public static class SaveGameMenu
    {
        private const string BootScenePath =
            "Assets/_Project/NERA/Scenes/Boot/Boot.unity";

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

            if (!File.Exists(GetSavePath()))
            {
                EditorUtility.DisplayDialog(
                    "NERA Save",
                    "Save file does not exist yet.",
                    "OK"
                );
                return;
            }

            SceneAsset bootScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(
                BootScenePath
            );

            if (bootScene == null)
            {
                Debug.LogError($"Save menu: Boot scene not found at '{BootScenePath}'.");
                return;
            }

            EditorSceneManager.playModeStartScene = bootScene;
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

        [MenuItem("Project/Save/Clear", priority = 102)]
        private static void Clear()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Clear NERA Save",
                "Delete the current save and reset runtime progress?",
                "Clear",
                "Cancel"
            );

            if (!confirmed)
                return;

            if (EditorApplication.isPlaying && SaveGameController.Instance != null)
            {
                SaveGameController.Instance.ClearSave(true);
            }
            else
            {
                DeleteIfExists(GetSavePath());
                DeleteIfExists(GetSavePath() + ".tmp");
                Debug.Log($"Save menu: Save file cleared at '{GetSavePath()}'.");
            }
        }

        [MenuItem("Project/Save/Open Save Folder", priority = 120)]
        private static void OpenSaveFolder()
        {
            Directory.CreateDirectory(Application.persistentDataPath);
            EditorUtility.RevealInFinder(Application.persistentDataPath);
        }

        private static string GetSavePath()
        {
            SaveGameController controller =
                Object.FindFirstObjectByType<SaveGameController>();

            return controller != null
                ? controller.SavePath
                : SaveGameController.DefaultSavePath;
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
