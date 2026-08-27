using System;
using System.Collections;
using System.Globalization;
using NERA.Core;
using NERA.Graphics;
using NERA.Localization;
using NERA.Save;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NERA.UI
{
    /// <summary>
    /// Controls the authored Boot menu without owning its presentation. The
    /// UI is resolved from the stable Canvas/Panel hierarchy at runtime.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        [Header("Scene Flow")]
        [SerializeField] private string runtimeSceneName = "MainScene";

        [Header("Menu Cameras")]
        [SerializeField] private CinemachineVirtualCameraBase rootMenuCamera;
        [SerializeField] private CinemachineVirtualCameraBase saveSlotCamera;

        private const int ActiveMenuCameraPriority = 10;
        private const int InactiveMenuCameraPriority = 0;

        [Header("Optional Authored Root Buttons")]
        [SerializeField] private Button newGameButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button optionsButton;
        [SerializeField] private Button exitButton;

        private readonly SaveSlotView[] saveSlots =
            new SaveSlotView[SaveSlotStorage.SlotCount];

        private GameObject rootButtons;
        private GameObject continueScreen;
        private GameObject optionsScreen;
        private GameObject exitScreen;
        private GameObject overwriteDialog;
        private Button slotContinueButton;
        private Button slotCloseButton;
        private Button overwriteYesButton;
        private Button overwriteNoButton;
        private Button optionsConfirmButton;
        private Button optionsCloseButton;
        private Button exitYesButton;
        private Button exitNoButton;
        private TMP_Text slotScreenDescription;
        private GameLaunchMode slotScreenMode;
        private int selectedSlot;
        private bool isLoading;

        public bool HasSave => SaveSlotStorage.HasAnySave();

        private void Awake()
        {
            SaveSlotStorage.TryMigrateLegacySingleSaveToSlotOne();
            if (!TryResolveAuthoredUi())
            {
                enabled = false;
                return;
            }

            BindButtons();
            ShowRootMenu();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnEnable()
        {
            NERALocalization.LocaleChanged += RefreshLocalizedText;
        }

        private void OnDisable()
        {
            NERALocalization.LocaleChanged -= RefreshLocalizedText;
        }

        public void Refresh()
        {
            if (newGameButton != null)
                newGameButton.interactable = !isLoading;
            if (continueButton != null)
                continueButton.interactable = !isLoading;
            if (optionsButton != null)
                optionsButton.interactable = !isLoading;
            if (exitButton != null)
                exitButton.interactable = !isLoading;

            RefreshSaveSlots();
        }

        public void StartNewGame()
        {
            OpenSaveSlotScreen(GameLaunchMode.NewGame);
        }

        public void ContinueGame()
        {
            OpenSaveSlotScreen(GameLaunchMode.Continue);
        }

        public void ShowOptions()
        {
            if (isLoading)
                return;

            ShowRootMenuCamera();
            rootButtons.SetActive(false);
            continueScreen.SetActive(false);
            exitScreen.SetActive(false);
            optionsScreen.SetActive(true);
            overwriteDialog.SetActive(false);
        }

        public void ShowExitConfirmation()
        {
            if (isLoading)
                return;

            ShowRootMenuCamera();
            continueScreen.SetActive(false);
            optionsScreen.SetActive(false);
            overwriteDialog.SetActive(false);
            rootButtons.SetActive(true);
            exitScreen.SetActive(true);
        }

        public void ShowRootMenu()
        {
            selectedSlot = 0;
            ShowRootMenuCamera();
            rootButtons.SetActive(true);
            continueScreen.SetActive(false);
            optionsScreen.SetActive(false);
            exitScreen.SetActive(false);
            overwriteDialog.SetActive(false);
            Refresh();
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
            Debug.Log(
                "Main menu: Exit requested. Application.Quit is ignored " +
                "in the Editor.");
#endif
        }

        private void OpenSaveSlotScreen(GameLaunchMode mode)
        {
            if (isLoading)
                return;

            slotScreenMode = mode;
            selectedSlot = 0;
            ShowSaveSlotCamera();
            rootButtons.SetActive(false);
            continueScreen.SetActive(true);
            optionsScreen.SetActive(false);
            exitScreen.SetActive(false);
            overwriteDialog.SetActive(false);

            if (slotScreenDescription != null)
            {
                slotScreenDescription.text = mode == GameLaunchMode.NewGame
                    ? NERALocalization.Get(
                        NERALocalization.MainMenuTable,
                        "save.select_new_slot",
                        "SELECT A SAVE SLOT")
                    : NERALocalization.Get(
                        NERALocalization.MainMenuTable,
                        "save.select_existing_slot",
                        "SELECT A GAME SAVE");
            }

            RefreshSaveSlots();
        }

        private void SelectSlot1()
        {
            SelectSlot(1);
        }

        private void SelectSlot2()
        {
            SelectSlot(2);
        }

        private void SelectSlot3()
        {
            SelectSlot(3);
        }

        private void SelectSlot(int slot)
        {
            if (isLoading || overwriteDialog.activeSelf)
                return;

            if (slotScreenMode == GameLaunchMode.Continue &&
                !SaveSlotStorage.HasSave(slot))
            {
                return;
            }

            selectedSlot = SaveSlotStorage.NormalizeSlot(slot);
            RefreshSaveSlots();
        }

        private void ConfirmSlotSelection()
        {
            if (selectedSlot < 1 || selectedSlot > SaveSlotStorage.SlotCount)
                return;

            bool occupied = SaveSlotStorage.HasSave(selectedSlot);
            if (slotScreenMode == GameLaunchMode.Continue)
            {
                if (occupied)
                    StartRuntime(GameLaunchMode.Continue, selectedSlot);
                return;
            }

            if (occupied)
            {
                overwriteDialog.SetActive(true);
                return;
            }

            StartRuntime(GameLaunchMode.NewGame, selectedSlot);
        }

        private void ConfirmOverwrite()
        {
            if (selectedSlot < 1 || selectedSlot > SaveSlotStorage.SlotCount)
                return;

            overwriteDialog.SetActive(false);
            StartRuntime(GameLaunchMode.NewGame, selectedSlot);
        }

        private void CancelOverwrite()
        {
            overwriteDialog.SetActive(false);
            selectedSlot = 0;
            RefreshSaveSlots();
        }

        private void ConfirmOptions()
        {
            ShowRootMenu();
        }

        private void RefreshLocalizedText()
        {
            if (continueScreen != null && continueScreen.activeSelf &&
                slotScreenDescription != null)
            {
                slotScreenDescription.text = slotScreenMode ==
                    GameLaunchMode.NewGame
                        ? NERALocalization.Get(
                            NERALocalization.MainMenuTable,
                            "save.select_new_slot",
                            "SELECT A SAVE SLOT")
                        : NERALocalization.Get(
                            NERALocalization.MainMenuTable,
                            "save.select_existing_slot",
                            "SELECT A GAME SAVE");
            }

            RefreshSaveSlots();
        }

        private void StartRuntime(GameLaunchMode mode, int saveSlot)
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
            GameSessionLaunchState.Request(mode, saveSlot);
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

        private bool TryResolveAuthoredUi()
        {
            Transform panel = null;
            Transform root = null;
            Transform slotScreen = null;
            Transform options = null;
            Transform exit = null;
            Canvas[] canvases = FindObjectsByType<Canvas>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (Canvas candidate in canvases)
            {
                Transform candidatePanel = candidate.transform.Find("Panel");
                Transform candidateRoot = candidatePanel != null
                    ? candidatePanel.Find("RootButton")
                    : null;
                Transform candidateSlotScreen = candidatePanel != null
                    ? candidatePanel.Find("ContinueScreen")
                    : null;
                Transform candidateOptions = candidatePanel != null
                    ? candidatePanel.Find("OptionsScreen")
                    : null;
                Transform candidateExit = candidatePanel != null
                    ? candidatePanel.Find("ExitScreen")
                    : null;
                if (candidateRoot == null || candidateSlotScreen == null ||
                    candidateOptions == null || candidateExit == null)
                {
                    continue;
                }

                panel = candidatePanel;
                root = candidateRoot;
                slotScreen = candidateSlotScreen;
                options = candidateOptions;
                exit = candidateExit;
                break;
            }

            if (root == null || slotScreen == null || options == null ||
                exit == null)
            {
                Debug.LogError(
                    "Main menu: Expected Canvas/Panel menu hierarchy was " +
                    "not found.",
                    this);
                return false;
            }

            rootButtons = root.gameObject;
            continueScreen = slotScreen.gameObject;
            optionsScreen = options.gameObject;
            exitScreen = exit.gameObject;

            ResolveMenuCameras();

            Transform rootButtonContainer =
                root.Find("background_button") ?? root;
            newGameButton ??= FindButton(
                rootButtonContainer,
                "NewGameButton");
            continueButton ??= FindButton(
                rootButtonContainer,
                "ContinueButton");
            optionsButton ??= FindButton(
                rootButtonContainer,
                "OptionsButton");
            exitButton ??= FindButton(rootButtonContainer, "ExitButton");

            Transform slotBackground =
                slotScreen.Find("background_Screen_station");
            Transform optionsBackground =
                options.Find("background_Screen_station");
            Transform exitBackground = exit.Find("background_exit");
            if (slotBackground == null || optionsBackground == null ||
                exitBackground == null)
            {
                Debug.LogError(
                    "Main menu: One or more authored menu backgrounds are " +
                    "missing.",
                    this);
                return false;
            }

            slotScreenDescription = FindText(
                slotBackground,
                "Description_Text");
            slotContinueButton = FindButton(
                slotBackground,
                "ContinueButton");
            slotCloseButton = FindButton(slotBackground, "CloseButton");

            Transform overwrite =
                slotBackground.Find("background_overwrite_slot");
            overwriteDialog = overwrite != null ? overwrite.gameObject : null;
            overwriteYesButton = FindButton(overwrite, "YESButton");
            overwriteNoButton = FindButton(overwrite, "NOButton");

            optionsConfirmButton = FindButton(
                optionsBackground,
                "ContinueButton");
            optionsCloseButton = FindButton(optionsBackground, "CloseButton");
            exitYesButton = FindButton(exitBackground, "YESButton");
            exitNoButton = FindButton(exitBackground, "NOButton");

            for (int index = 0; index < saveSlots.Length; index++)
            {
                int slot = index + 1;
                Transform slotRoot =
                    slotBackground.Find($"Panel_Save_{slot}");
                saveSlots[index] = SaveSlotView.Create(slot, slotRoot);
            }

            bool resolved = newGameButton != null &&
                            continueButton != null &&
                            optionsButton != null &&
                            exitButton != null &&
                            slotContinueButton != null &&
                            slotCloseButton != null &&
                            overwriteDialog != null &&
                            overwriteYesButton != null &&
                            overwriteNoButton != null &&
                            optionsConfirmButton != null &&
                            optionsCloseButton != null &&
                            exitYesButton != null &&
                            exitNoButton != null;

            for (int index = 0; index < saveSlots.Length; index++)
                resolved &= saveSlots[index] != null;

            if (!resolved)
            {
                Debug.LogError(
                    "Main menu: One or more authored controls could not be " +
                    "resolved. Check the Boot hierarchy and slot Buttons.",
                    this);
            }

            return resolved;
        }

        private void ResolveMenuCameras()
        {
            Transform camerasRoot = GameObject.Find("VirtualCam")?.transform;
            rootMenuCamera ??= camerasRoot
                ?.Find("VirtualCam_01")
                ?.GetComponent<CinemachineVirtualCameraBase>();
            saveSlotCamera ??= camerasRoot
                ?.Find("VirtualCam_02")
                ?.GetComponent<CinemachineVirtualCameraBase>();

            if (rootMenuCamera == null || saveSlotCamera == null)
            {
                Debug.LogWarning(
                    "Main menu: VirtualCam_01 or VirtualCam_02 was not " +
                    "found under VirtualCam. Menu camera " +
                    "switching is disabled.",
                    this);
            }
        }

        private void ShowRootMenuCamera()
        {
            SetActiveMenuCamera(rootMenuCamera);
        }

        private void ShowSaveSlotCamera()
        {
            SetActiveMenuCamera(saveSlotCamera);
        }

        private void SetActiveMenuCamera(
            CinemachineVirtualCameraBase activeCamera)
        {
            if (rootMenuCamera == null || saveSlotCamera == null)
                return;

            rootMenuCamera.Priority = activeCamera == rootMenuCamera
                ? ActiveMenuCameraPriority
                : InactiveMenuCameraPriority;
            saveSlotCamera.Priority = activeCamera == saveSlotCamera
                ? ActiveMenuCameraPriority
                : InactiveMenuCameraPriority;
        }

        private void BindButtons()
        {
            newGameButton.onClick.AddListener(StartNewGame);
            continueButton.onClick.AddListener(ContinueGame);
            optionsButton.onClick.AddListener(ShowOptions);
            exitButton.onClick.AddListener(ShowExitConfirmation);
            slotContinueButton.onClick.AddListener(ConfirmSlotSelection);
            slotCloseButton.onClick.AddListener(ShowRootMenu);
            overwriteYesButton.onClick.AddListener(ConfirmOverwrite);
            overwriteNoButton.onClick.AddListener(CancelOverwrite);
            optionsConfirmButton.onClick.AddListener(ConfirmOptions);
            optionsCloseButton.onClick.AddListener(ShowRootMenu);
            exitYesButton.onClick.AddListener(ExitGame);
            exitNoButton.onClick.AddListener(ShowRootMenu);
            saveSlots[0].Button.onClick.AddListener(SelectSlot1);
            saveSlots[1].Button.onClick.AddListener(SelectSlot2);
            saveSlots[2].Button.onClick.AddListener(SelectSlot3);
        }

        private void RefreshSaveSlots()
        {
            if (saveSlots[0] == null)
                return;

            bool isNewGame = slotScreenMode == GameLaunchMode.NewGame;
            for (int index = 0; index < saveSlots.Length; index++)
            {
                SaveSlotView slot = saveSlots[index];
                bool occupied = SaveSlotStorage.HasSave(slot.Slot);
                bool canSelect = !isLoading && (isNewGame || occupied);
                slot.Refresh(
                    occupied,
                    canSelect,
                    selectedSlot == slot.Slot);
            }

            if (slotContinueButton != null)
            {
                bool selectedSaveExists = selectedSlot > 0 &&
                    SaveSlotStorage.HasSave(selectedSlot);
                slotContinueButton.interactable = !isLoading &&
                    selectedSlot > 0 &&
                    (isNewGame || selectedSaveExists);
            }
        }

        private static Button FindButton(Transform parent, string childName)
        {
            Transform child = parent != null ? parent.Find(childName) : null;
            return child != null ? child.GetComponent<Button>() : null;
        }

        private static TMP_Text FindText(Transform parent, string childName)
        {
            Transform child = parent != null ? parent.Find(childName) : null;
            return child != null ? child.GetComponent<TMP_Text>() : null;
        }

        private void OnDestroy()
        {
            newGameButton?.onClick.RemoveListener(StartNewGame);
            continueButton?.onClick.RemoveListener(ContinueGame);
            optionsButton?.onClick.RemoveListener(ShowOptions);
            exitButton?.onClick.RemoveListener(ShowExitConfirmation);
            slotContinueButton?.onClick.RemoveListener(ConfirmSlotSelection);
            slotCloseButton?.onClick.RemoveListener(ShowRootMenu);
            overwriteYesButton?.onClick.RemoveListener(ConfirmOverwrite);
            overwriteNoButton?.onClick.RemoveListener(CancelOverwrite);
            optionsConfirmButton?.onClick.RemoveListener(ConfirmOptions);
            optionsCloseButton?.onClick.RemoveListener(ShowRootMenu);
            exitYesButton?.onClick.RemoveListener(ExitGame);
            exitNoButton?.onClick.RemoveListener(ShowRootMenu);

            if (saveSlots[0] != null)
                saveSlots[0].Button.onClick.RemoveListener(SelectSlot1);
            if (saveSlots[1] != null)
                saveSlots[1].Button.onClick.RemoveListener(SelectSlot2);
            if (saveSlots[2] != null)
                saveSlots[2].Button.onClick.RemoveListener(SelectSlot3);
        }

        private sealed class SaveSlotView
        {
            private readonly Image background;
            private readonly TMP_Text dateText;
            private readonly TMP_Text completionText;
            private readonly Color baseColor;

            private SaveSlotView(
                int slot,
                Button button,
                Image background,
                TMP_Text dateText,
                TMP_Text completionText)
            {
                Slot = slot;
                Button = button;
                this.background = background;
                this.dateText = dateText;
                this.completionText = completionText;
                baseColor = background.color;
                Button.transition = Selectable.Transition.None;
            }

            public int Slot { get; }
            public Button Button { get; }

            public static SaveSlotView Create(int slot, Transform root)
            {
                if (root == null)
                    return null;

                Button button = root.GetComponent<Button>();
                Image image = root.GetComponent<Image>();
                TMP_Text date = FindText(root, "Data_Text");
                TMP_Text completion = FindText(root, "Complete_Text");
                return button != null && image != null && date != null &&
                       completion != null
                    ? new SaveSlotView(
                        slot,
                        button,
                        image,
                        date,
                        completion)
                    : null;
            }

            public void Refresh(
                bool occupied,
                bool canSelect,
                bool selected)
            {
                Button.interactable = canSelect;

                Color color = baseColor;
                if (selected)
                    color.a = Mathf.Max(baseColor.a, 0.75f);
                else if (!canSelect)
                    color.a = Mathf.Min(baseColor.a, 0.18f);
                background.color = color;

                if (!occupied)
                {
                    dateText.text = NERALocalization.Get(
                        NERALocalization.MainMenuTable,
                        "save.empty",
                        "EMPTY");
                    completionText.text = NERALocalization.Get(
                        NERALocalization.MainMenuTable,
                        "save.completion",
                        "{0}% COMPLETE",
                        0);
                    return;
                }

                DateTime writeTime = SaveSlotStorage.GetLastWriteTime(Slot);
                dateText.text = writeTime.ToString(
                    NERALocalization.Get(
                        NERALocalization.MainMenuTable,
                        "save.date_format",
                        "MM.dd.yyyy - HH:mm"),
                    CultureInfo.InvariantCulture);
                int completion = Mathf.RoundToInt(
                    SaveSlotStorage.GetCompletionPercent(Slot));
                completionText.text = NERALocalization.Get(
                    NERALocalization.MainMenuTable,
                    "save.completion",
                    "{0}% COMPLETE",
                    completion);
            }
        }
    }
}
