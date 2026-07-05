using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TerminalUI : MonoBehaviour
{
    public static TerminalUI Instance { get; private set; }

    [Header("Window")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Tab Buttons")]
    [SerializeField] private Button stationButton;
    [SerializeField] private Button locationsButton;
    [SerializeField] private Button libraryButton;
    [SerializeField] private Button droneButton;

    [Header("Tab Button Labels")]
    [SerializeField] private TMP_Text stationButtonText;
    [SerializeField] private TMP_Text locationsButtonText;
    [SerializeField] private TMP_Text libraryButtonText;
    [SerializeField] private TMP_Text droneButtonText;

    [Header("Panels")]
    [SerializeField] private GameObject stationPanel;
    [SerializeField] private GameObject locationsPanel;
    [SerializeField] private GameObject libraryPanel;
    [SerializeField] private GameObject dronePanel;

    [Header("Station Panel")]
    [SerializeField] private TMP_Text stationStatusText;

    [Header("Locations Panel")]
    [SerializeField] private TMP_Text locationsInfoText;
    [SerializeField] private Button launchExpedition01Button;
    [SerializeField] private TMP_Text launchExpedition01ButtonText;

    [Header("Library Panel")]
    [SerializeField] private TMP_Text libraryInfoText;

    [Header("Drone Panel")]
    [SerializeField] private TMP_Text droneInfoText;

    [Header("Action Buttons")]
    [SerializeField] private Button closeButton;
    [SerializeField] private TMP_Text closeButtonText;

    [Header("Input")]
    [SerializeField] private KeyCode closeKey = KeyCode.Escape;

    [Header("Cursor")]
    [SerializeField] private bool showCursorWhileOpen = true;
    [SerializeField] private CursorLockMode cursorModeWhenClosed = CursorLockMode.Locked;
    [SerializeField] private bool cursorVisibleWhenClosed = false;

    [Header("Auto Close")]
    [SerializeField] private bool closeOnSceneLoaded = true;
    [SerializeField] private bool closeIfPlayerLeavesTerminal = true;
    [SerializeField] private float maxTerminalUseDistance = 3.5f;

    [Header("Expedition Launch")]
    [SerializeField] private string defaultExpeditionId = "expedition_01";

    private bool isOpen;
    private Transform openedFromTerminal;
    private Transform playerTransform;
    private string currentPanelId = "station";

    public bool IsOpen => isOpen;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"TerminalUI duplicate destroyed: {name}");
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        BindButtons();
        HideInstant();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;

        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.LanguageChanged += OnLanguageChanged;

        if (GameSessionState.Instance != null)
            GameSessionState.Instance.StateChanged += OnGameSessionStateChanged;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.LanguageChanged -= OnLanguageChanged;

        if (GameSessionState.Instance != null)
            GameSessionState.Instance.StateChanged -= OnGameSessionStateChanged;
    }

    private void Update()
    {
        if (!isOpen)
            return;

        if (Input.GetKeyDown(closeKey))
        {
            Close();
            return;
        }

        if (closeIfPlayerLeavesTerminal)
            CheckDistanceToTerminal();
    }

    private void OnGameSessionStateChanged()
{
    RefreshCurrentPanel();
}

    private void BindButtons()
    {
        if (stationButton != null)
            stationButton.onClick.AddListener(ShowStationPanel);

        if (locationsButton != null)
            locationsButton.onClick.AddListener(ShowLocationsPanel);

        if (libraryButton != null)
            libraryButton.onClick.AddListener(ShowLibraryPanel);

        if (droneButton != null)
            droneButton.onClick.AddListener(ShowDronePanel);

        if (launchExpedition01Button != null)
            launchExpedition01Button.onClick.AddListener(LaunchDefaultExpedition);

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);
    }

    public void Open(Transform terminalTransform)
    {
        openedFromTerminal = terminalTransform;
        playerTransform = FindPlayerTransform();

        isOpen = true;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        ApplyOpenCursorState();

        UpdateStaticTexts();
        ShowStationPanel();

        Debug.Log("TerminalUI: Opened.");
    }

    public void Close()
    {
        if (!isOpen)
            return;

        isOpen = false;

        openedFromTerminal = null;
        playerTransform = null;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        ApplyClosedCursorState();

        Debug.Log("TerminalUI: Closed.");
    }

    public void HideInstant()
    {
        isOpen = false;

        openedFromTerminal = null;
        playerTransform = null;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        HideAllPanels();
        ApplyClosedCursorState();
    }

    public void ShowStationPanel()
    {
        currentPanelId = "station";

        HideAllPanels();

        if (stationPanel != null)
            stationPanel.SetActive(true);

        UpdateStationPanel();

        Debug.Log("TerminalUI: Station panel opened.");
    }

    public void ShowLocationsPanel()
    {
        currentPanelId = "locations";

        HideAllPanels();

        if (locationsPanel != null)
            locationsPanel.SetActive(true);

        UpdateLocationsPanel();

        Debug.Log("TerminalUI: Locations panel opened.");
    }

    public void ShowLibraryPanel()
    {
        currentPanelId = "library";

        HideAllPanels();

        if (libraryPanel != null)
            libraryPanel.SetActive(true);

        UpdateLibraryPanel();

        Debug.Log("TerminalUI: Library panel opened.");
    }

    public void ShowDronePanel()
    {
        currentPanelId = "drone";

        HideAllPanels();

        if (dronePanel != null)
            dronePanel.SetActive(true);

        UpdateDronePanel();

        Debug.Log("TerminalUI: Drone panel opened.");
    }

    private void HideAllPanels()
    {
        if (stationPanel != null)
            stationPanel.SetActive(false);

        if (locationsPanel != null)
            locationsPanel.SetActive(false);

        if (libraryPanel != null)
            libraryPanel.SetActive(false);

        if (dronePanel != null)
            dronePanel.SetActive(false);
    }

    private void RefreshCurrentPanel()
    {
        if (!isOpen)
            return;

        UpdateStaticTexts();

        switch (currentPanelId)
        {
            case "locations":
                ShowLocationsPanel();
                break;

            case "library":
                ShowLibraryPanel();
                break;

            case "drone":
                ShowDronePanel();
                break;

            case "station":
            default:
                ShowStationPanel();
                break;
        }
    }

    private void UpdateStaticTexts()
    {
        TerminalTextData textData = GetTerminalTextData();

        if (textData == null)
            return;

        if (stationButtonText != null)
            stationButtonText.text = textData.StationTabLabel;

        if (locationsButtonText != null)
            locationsButtonText.text = textData.LocationsTabLabel;

        if (libraryButtonText != null)
            libraryButtonText.text = textData.LibraryTabLabel;

        if (droneButtonText != null)
            droneButtonText.text = textData.DroneTabLabel;

        if (launchExpedition01ButtonText != null)
            launchExpedition01ButtonText.text = textData.LaunchExpeditionButtonLabel;

        if (closeButtonText != null)
            closeButtonText.text = textData.CloseButtonLabel;
    }

    private void UpdateStationPanel()
    {
        if (stationStatusText == null)
            return;

        TerminalTextData textData = GetTerminalTextData();

        if (textData == null)
        {
            stationStatusText.text = "Terminal text data is missing.";
            return;
        }

        string powerState = GetLocalizedPowerState();

        string text =
            $"{textData.StationTitle}\n\n" +
            $"{textData.PowerLabel}: {powerState}\n" +
            $"{textData.TerminalLabel}: {textData.PowerOnline}\n" +
            $"{textData.NeraProgramLinkLabel}: {textData.NeraProgramLinkValue}\n" +
            $"{textData.ExpeditionModuleLabel}: {textData.ExpeditionModuleAvailableValue}\n\n" +
            $"{textData.CurrentTaskTitle}:\n" +
            $"- {textData.TaskCheckLocations}\n" +
            $"- {textData.TaskLaunchFirstExpedition}";

        if (GameSessionState.Instance != null &&
            GameSessionState.Instance.IsItemFound("ancient_object_01"))
        {
            text +=
                $"\n\n{textData.StationFollowUpTitle}:\n" +
                $"- {textData.StationAncientObjectFoundText}\n" +
                $"- {textData.StationOpenLibraryText}";
        }

        stationStatusText.text = text;
    }

    private void UpdateLocationsPanel()
    {
        if (locationsInfoText == null)
            return;

        TerminalTextData textData = GetTerminalTextData();

        if (textData == null)
        {
            locationsInfoText.text = "Terminal text data is missing.";
            SetLaunchButtonVisible(false);
            return;
        }

        NeraContentDatabase database = GetContentDatabase();

        if (database == null)
        {
            locationsInfoText.text =
                $"{textData.LocationsTitle}\n\n" +
                textData.ContentDatabaseMissingText;

            SetLaunchButtonVisible(false);
            return;
        }

        ExpeditionData expedition = database.GetExpeditionById(defaultExpeditionId);

        if (expedition == null)
        {
            locationsInfoText.text =
                $"{textData.LocationsTitle}\n\n" +
                $"{textData.ExpeditionNotFoundText}: {defaultExpeditionId}";

            SetLaunchButtonVisible(false);
            return;
        }

        locationsInfoText.text =
            $"{textData.LocationsTitle}\n\n" +
            $"{expedition.DisplayName.GetText()}\n" +
            $"{textData.TypeLabel}: {expedition.LocationType.GetText()}\n" +
            $"{textData.StatusLabel}: {GetLocalizedExpeditionStatus(GetRuntimeExpeditionStatus(expedition))}\n\n" +
            $"{expedition.Description.GetText()}\n\n" +
            $"{textData.ObjectiveLabel}:\n" +
            $"- {expedition.GetObjectivePreviewText()}";

        UpdateLaunchButtonState(expedition);
    }

    private void UpdateLibraryPanel()
    {
        if (libraryInfoText == null)
            return;

        TerminalTextData textData = GetTerminalTextData();

        if (textData == null)
        {
            libraryInfoText.text = "Terminal text data is missing.";
            return;
        }

        NeraContentDatabase database = GetContentDatabase();

        if (database == null)
        {
            libraryInfoText.text =
                $"{textData.LibraryTitle}\n\n" +
                textData.ContentDatabaseMissingText;

            return;
        }

        string entriesText = BuildLibraryEntriesText(database, textData);

        libraryInfoText.text =
            $"{textData.LibraryTitle}\n\n" +
            $"{textData.FoundEntriesLabel}:\n" +
            entriesText;
    }

    private string BuildLibraryEntriesText(NeraContentDatabase database, TerminalTextData textData)
    {
        if (database.LibraryEntries == null || database.LibraryEntries.Count <= 0)
            return "- " + textData.NoLibraryEntriesConfiguredText;

        PlayerInventory inventory = FindPlayerInventory();

        string result = string.Empty;

        for (int i = 0; i < database.LibraryEntries.Count; i++)
        {
            LibraryEntryData entry = database.LibraryEntries[i];

            if (entry == null)
                continue;

            bool isUnlocked = IsLibraryEntryUnlocked(entry, inventory);

            string title = entry.Title != null
                ? entry.Title.GetText()
                : entry.EntryId;

            if (!isUnlocked)
            {
                result += $"- {title}\n";
                result += $"{textData.TranslationStateLabel}: {GetLocalizedTranslationState(TranslationState.Unknown, textData)}\n";
                result += $"{entry.GetTextByState(TranslationState.Unknown)}\n\n";
                continue;
            }

            TranslationState runtimeState = GetRuntimeLibraryEntryTranslationState(entry);

            result += $"- {title}\n";
            result += $"{textData.TranslationStateLabel}: {GetLocalizedTranslationState(runtimeState, textData)}\n";
            result += $"{entry.GetTextByState(runtimeState)}\n\n";
        }

        if (string.IsNullOrWhiteSpace(result))
            return "- " + textData.NoLibraryEntriesText;

        return result.TrimEnd();
    }

    private void UpdateDronePanel()
    {
        if (droneInfoText == null)
            return;

        TerminalTextData textData = GetTerminalTextData();

        if (textData == null)
        {
            droneInfoText.text = "Terminal text data is missing.";
            return;
        }

        droneInfoText.text =
            $"{textData.DroneTitle}\n\n" +
            $"{textData.DroneStatusLabel}: {textData.DroneStatusPlaceholder}\n" +
            $"{textData.DroneChargeLabel}: {textData.DroneChargeUnknown}\n" +
            $"{textData.DroneReconModuleLabel}: {textData.DroneReconNotConnected}\n\n" +
            $"{textData.DroneFutureFeaturesLabel}:\n" +
            $"- {textData.DroneFeatureLaunch}\n" +
            $"- {textData.DroneFeatureScan}\n" +
            $"- {textData.DroneFeatureScout}\n" +
            $"- {textData.DroneFeatureDetectIO}";
    }

    private void UpdateLaunchButtonState(ExpeditionData expedition)
    {
        if (launchExpedition01Button == null)
            return;

        bool canLaunch = expedition != null;

        if (expedition != null && expedition.RequiresStationPower)
        {
            canLaunch =
                StationPowerController.Instance != null &&
                StationPowerController.Instance.IsOnline;
        }

        launchExpedition01Button.gameObject.SetActive(true);
        launchExpedition01Button.interactable = canLaunch;
    }

    private void SetLaunchButtonVisible(bool visible)
    {
        if (launchExpedition01Button == null)
            return;

        launchExpedition01Button.gameObject.SetActive(visible);
    }

    public void LaunchDefaultExpedition()
    {
        NeraContentDatabase database = GetContentDatabase();

        if (database == null)
        {
            Debug.LogError("TerminalUI: Content database not found.");
            return;
        }

        ExpeditionData expedition = database.GetExpeditionById(defaultExpeditionId);

        if (expedition == null)
        {
            Debug.LogError($"TerminalUI: Expedition not found: {defaultExpeditionId}");
            return;
        }

        if (expedition.RequiresStationPower)
        {
            if (StationPowerController.Instance == null || !StationPowerController.Instance.IsOnline)
            {
                Debug.LogWarning("TerminalUI: Cannot launch expedition. Station power is offline.");
                return;
            }
        }

        if (SceneLoader.Instance == null)
        {
            Debug.LogError("TerminalUI: SceneLoader not found.");
            return;
        }

        Debug.Log(
            $"TerminalUI: Launching expedition. " +
            $"Id='{expedition.ExpeditionId}', Scene='{expedition.SceneName}', Spawn='{expedition.SpawnPointId}'"
        );

        Close();

        SceneLoader.Instance.LoadScene(expedition.SceneName, expedition.SpawnPointId);
    }

    private bool IsLibraryEntryUnlocked(LibraryEntryData entry, PlayerInventory inventory)
    {
        if (entry == null)
            return false;

        if (entry.RelatedItem == null)
            return true;

        if (inventory == null || inventory.Backpack == null)
            return false;

        return inventory.Backpack.ContainsItem(entry.RelatedItem.ItemId);
    }

    private string GetLocalizedPowerState()
    {
        TerminalTextData textData = GetTerminalTextData();

        if (textData == null)
            return "Unknown";

        if (StationPowerController.Instance == null)
            return textData.PowerUnknown;

        switch (StationPowerController.Instance.CurrentState)
        {
            case StationPowerState.Online:
                return textData.PowerOnline;

            case StationPowerState.Offline:
                return textData.PowerOffline;

            default:
                return textData.PowerUnknown;
        }
    }

    private string GetLocalizedTranslationState(TranslationState state, TerminalTextData textData)
    {
        if (textData == null)
            return state.ToString();

        switch (state)
        {
            case TranslationState.Translated:
                return textData.TranslationStateTranslated;

            case TranslationState.PartiallyTranslated:
                return textData.TranslationStatePartiallyTranslated;

            case TranslationState.Untranslated:
                return textData.TranslationStateUntranslated;

            case TranslationState.Unknown:
            default:
                return textData.TranslationStateUnknown;
        }
    }

    private string GetLocalizedExpeditionStatus(ExpeditionStatus status)
    {
        TerminalTextData textData = GetTerminalTextData();

        if (textData == null)
            return status.ToString();

        switch (status)
        {
            case ExpeditionStatus.Locked:
                return textData.ExpeditionStatusLocked;

            case ExpeditionStatus.InProgress:
                return textData.ExpeditionStatusInProgress;

            case ExpeditionStatus.Completed:
                return textData.ExpeditionStatusCompleted;

            case ExpeditionStatus.Available:
            default:
                return textData.ExpeditionStatusAvailable;
        }
    }

    private TranslationState GetRuntimeLibraryEntryTranslationState(LibraryEntryData entry)
    {
        if (entry == null)
            return TranslationState.Unknown;

        if (GameSessionState.Instance == null)
            return entry.DefaultTranslationState;

        return GameSessionState.Instance.GetLibraryEntryTranslationState(
            entry.EntryId,
            entry.DefaultTranslationState
        );
    }

    private ExpeditionStatus GetRuntimeExpeditionStatus(ExpeditionData expedition)
{
    if (expedition == null)
        return ExpeditionStatus.Locked;

    return expedition.GetRuntimeStatus();
}

    private TerminalTextData GetTerminalTextData()
    {
        NeraContentDatabase database = GetContentDatabase();

        if (database == null)
            return null;

        return database.TerminalTextData;
    }

    private NeraContentDatabase GetContentDatabase()
    {
        if (NeraContentProvider.Instance == null)
            return null;

        return NeraContentProvider.Instance.ContentDatabase;
    }

    private PlayerInventory FindPlayerInventory()
    {
        if (PersistentPlayer.Instance != null)
            return PersistentPlayer.Instance.GetComponent<PlayerInventory>();

        return FindFirstObjectByType<PlayerInventory>();
    }

    private void CheckDistanceToTerminal()
    {
        if (openedFromTerminal == null)
        {
            Close();
            return;
        }

        if (playerTransform == null)
            playerTransform = FindPlayerTransform();

        if (playerTransform == null)
        {
            Close();
            return;
        }

        float distance = Vector3.Distance(playerTransform.position, openedFromTerminal.position);

        if (distance > maxTerminalUseDistance)
        {
            Debug.Log("TerminalUI: Player left terminal range. Closing terminal.");
            Close();
        }
    }

    private Transform FindPlayerTransform()
    {
        if (PersistentPlayer.Instance != null)
            return PersistentPlayer.Instance.transform;

        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player != null)
            return player.transform;

        return null;
    }

    private void ApplyOpenCursorState()
    {
        if (!showCursorWhileOpen)
            return;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void ApplyClosedCursorState()
    {
        Cursor.visible = cursorVisibleWhenClosed;
        Cursor.lockState = cursorModeWhenClosed;
    }

    private void OnLanguageChanged(GameLanguage language)
    {
        RefreshCurrentPanel();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!closeOnSceneLoaded)
            return;

        if (!isOpen)
            return;

        Debug.Log($"TerminalUI: Scene loaded '{scene.name}'. Closing terminal.");
        Close();
    }
}