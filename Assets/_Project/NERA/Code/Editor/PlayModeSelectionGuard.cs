using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace NERA.EditorTools
{
    /// <summary>
    /// Prevents Unity inspectors from retaining references to objects that only
    /// exist in Play Mode. Those stale references otherwise produce
    /// MissingReferenceException and SerializedObjectNotCreatableException
    /// messages when returning to Edit Mode.
    /// </summary>
    [InitializeOnLoad]
    internal static class PlayModeSelectionGuard
    {
        static PlayModeSelectionGuard()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorSceneManager.sceneClosing -= OnSceneClosing;
            EditorSceneManager.sceneClosing += OnSceneClosing;
        }

        private static void OnPlayModeStateChanged(
            PlayModeStateChange state
        )
        {
            if (state != PlayModeStateChange.ExitingPlayMode)
                return;

            ClearSelection();
        }

        private static void OnSceneClosing(
            Scene scene,
            bool removingScene
        )
        {
            ClearSelection();
        }

        private static void ClearSelection()
        {
            Selection.objects = Array.Empty<UnityEngine.Object>();
            Selection.activeObject = null;
        }
    }
}
