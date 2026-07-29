using System.Collections;
using System.IO;
using NERA.Core;
using NERA.Graphics;
using NERA.Save;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NERA.UI
{
    /// <summary>
    /// Controller for the authored Boot scene menu. Assign buttons in the
    /// Inspector; visual hierarchy and animation remain fully authored.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        [Header("Scene Flow")]
        [SerializeField] private string runtimeSceneName = "MainScene";

        [Header("Optional Authored Buttons")]
        [SerializeField] private Button newGameButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button exitButton;

        private bool isLoading;

        public bool HasSave => File.Exists(SaveGameController.DefaultSavePath);

        private void Awake()
        {
            newGameButton?.onClick.AddListener(StartNewGame);
            continueButton?.onClick.AddListener(ContinueGame);
            exitButton?.onClick.AddListener(ExitGame);
            Refresh();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void Refresh()
        {
            if (newGameButton != null)
                newGameButton.interactable = !isLoading;
            if (continueButton != null)
                continueButton.interactable = !isLoading && HasSave;
            if (exitButton != null)
                exitButton.interactable = !isLoading;
        }

        public void StartNewGame()
        {
            StartRuntime(GameLaunchMode.NewGame);
        }

        public void ContinueGame()
        {
            if (!HasSave)
            {
                Debug.LogWarning(
                    "Main menu: Continue requested, but no save file exists.",
                    this);
                Refresh();
                return;
            }

            StartRuntime(GameLaunchMode.Continue);
        }

        public void SetLowQuality()
        {
            PCQualityRuntimeController.SetQualityLevel("Low");
        }

        public void SetMediumQuality()
        {
            PCQualityRuntimeController.SetQualityLevel("Medium");
        }

        public void SetHighQuality()
        {
            PCQualityRuntimeController.SetQualityLevel("High");
        }

        public void ExitGame()
        {
            Application.Quit();
#if UNITY_EDITOR
            Debug.Log("Main menu: Exit requested. Application.Quit is ignored in the Editor.");
#endif
        }

        private void StartRuntime(GameLaunchMode mode)
        {
            if (isLoading)
                return;

            if (string.IsNullOrWhiteSpace(runtimeSceneName) ||
                !Application.CanStreamedLevelBeLoaded(runtimeSceneName))
            {
                Debug.LogError(
                    $"Main menu: Runtime scene '{runtimeSceneName}' is not " +
                    "available in Build Settings.",
                    this);
                return;
            }

            isLoading = true;
            Refresh();
            GameSessionLaunchState.Request(mode);
            StartCoroutine(LoadRuntimeScene());
        }

        private IEnumerator LoadRuntimeScene()
        {
            AsyncOperation operation = SceneManager.LoadSceneAsync(
                runtimeSceneName,
                LoadSceneMode.Additive);
            if (operation == null)
            {
                isLoading = false;
                Refresh();
                yield break;
            }

            yield return operation;
        }

        private void OnDestroy()
        {
            newGameButton?.onClick.RemoveListener(StartNewGame);
            continueButton?.onClick.RemoveListener(ContinueGame);
            exitButton?.onClick.RemoveListener(ExitGame);
        }
    }
}
