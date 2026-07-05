using UnityEngine;

[CreateAssetMenu(
    fileName = "TerminalTextData_Default",
    menuName = "NERA/UI/Terminal Text Data"
)]
public class TerminalTextData : ScriptableObject
{
    [Header("Tabs")]
    [SerializeField] private LocalizedText stationTabLabel;
    [SerializeField] private LocalizedText locationsTabLabel;
    [SerializeField] private LocalizedText libraryTabLabel;
    [SerializeField] private LocalizedText droneTabLabel;

    [Header("Buttons")]
    [SerializeField] private LocalizedText launchExpeditionButtonLabel;
    [SerializeField] private LocalizedText closeButtonLabel;

    [Header("Station Panel")]
    [SerializeField] private LocalizedText stationTitle;
    [SerializeField] private LocalizedText powerLabel;
    [SerializeField] private LocalizedText terminalLabel;
    [SerializeField] private LocalizedText neraProgramLinkLabel;
    [SerializeField] private LocalizedText neraProgramLinkValue;
    [SerializeField] private LocalizedText expeditionModuleLabel;
    [SerializeField] private LocalizedText expeditionModuleAvailableValue;
    [SerializeField] private LocalizedText currentTaskTitle;
    [SerializeField] private LocalizedText taskCheckLocations;
    [SerializeField] private LocalizedText taskLaunchFirstExpedition;

    [Header("Station Follow-up")]
    [SerializeField] private LocalizedText stationFollowUpTitle;
    [SerializeField] private LocalizedText stationAncientObjectFoundText;
    [SerializeField] private LocalizedText stationOpenLibraryText;

    [Header("Power State")]
    [SerializeField] private LocalizedText powerOnline;
    [SerializeField] private LocalizedText powerOffline;
    [SerializeField] private LocalizedText powerUnknown;

    [Header("Locations Panel")]
    [SerializeField] private LocalizedText locationsTitle;
    [SerializeField] private LocalizedText typeLabel;
    [SerializeField] private LocalizedText statusLabel;
    [SerializeField] private LocalizedText objectiveLabel;
    [SerializeField] private LocalizedText contentDatabaseMissingText;
    [SerializeField] private LocalizedText expeditionNotFoundText;

    [Header("Expedition Status")]
    [SerializeField] private LocalizedText expeditionStatusLocked;
    [SerializeField] private LocalizedText expeditionStatusAvailable;
    [SerializeField] private LocalizedText expeditionStatusInProgress;
    [SerializeField] private LocalizedText expeditionStatusCompleted;

    [Header("Library Panel")]
    [SerializeField] private LocalizedText libraryTitle;
    [SerializeField] private LocalizedText foundEntriesLabel;
    [SerializeField] private LocalizedText noLibraryEntriesConfiguredText;
    [SerializeField] private LocalizedText noLibraryEntriesText;

    [Header("Library Translation State")]
    [SerializeField] private LocalizedText translationStateLabel;
    [SerializeField] private LocalizedText translationStateUnknown;
    [SerializeField] private LocalizedText translationStateUntranslated;
    [SerializeField] private LocalizedText translationStatePartiallyTranslated;
    [SerializeField] private LocalizedText translationStateTranslated;

    [Header("Drone Panel")]
    [SerializeField] private LocalizedText droneTitle;
    [SerializeField] private LocalizedText droneStatusLabel;
    [SerializeField] private LocalizedText droneChargeLabel;
    [SerializeField] private LocalizedText droneReconModuleLabel;
    [SerializeField] private LocalizedText droneFutureFeaturesLabel;
    [SerializeField] private LocalizedText droneStatusPlaceholder;
    [SerializeField] private LocalizedText droneChargeUnknown;
    [SerializeField] private LocalizedText droneReconNotConnected;
    [SerializeField] private LocalizedText droneFeatureLaunch;
    [SerializeField] private LocalizedText droneFeatureScan;
    [SerializeField] private LocalizedText droneFeatureScout;
    [SerializeField] private LocalizedText droneFeatureDetectIO;

    public string StationTabLabel => stationTabLabel.GetText("Station");
    public string LocationsTabLabel => locationsTabLabel.GetText("Locations");
    public string LibraryTabLabel => libraryTabLabel.GetText("Library");
    public string DroneTabLabel => droneTabLabel.GetText("Drone");

    public string LaunchExpeditionButtonLabel => launchExpeditionButtonLabel.GetText("Launch Expedition");
    public string CloseButtonLabel => closeButtonLabel.GetText("Close");

    public string StationTitle => stationTitle.GetText("Station");
    public string PowerLabel => powerLabel.GetText("Power");
    public string TerminalLabel => terminalLabel.GetText("Terminal");
    public string NeraProgramLinkLabel => neraProgramLinkLabel.GetText("NERA Program Link");
    public string NeraProgramLinkValue => neraProgramLinkValue.GetText("Limited");
    public string ExpeditionModuleLabel => expeditionModuleLabel.GetText("Expedition Module");
    public string ExpeditionModuleAvailableValue => expeditionModuleAvailableValue.GetText("Available");
    public string CurrentTaskTitle => currentTaskTitle.GetText("Current task");
    public string TaskCheckLocations => taskCheckLocations.GetText("Check available locations");
    public string TaskLaunchFirstExpedition => taskLaunchFirstExpedition.GetText("Launch the first expedition");

    public string PowerOnline => powerOnline.GetText("Online");
    public string PowerOffline => powerOffline.GetText("Offline");
    public string PowerUnknown => powerUnknown.GetText("Unknown");

    public string LocationsTitle => locationsTitle.GetText("Locations");
    public string TypeLabel => typeLabel.GetText("Type");
    public string StatusLabel => statusLabel.GetText("Status");
    public string ObjectiveLabel => objectiveLabel.GetText("Objective");
    public string ContentDatabaseMissingText => contentDatabaseMissingText.GetText("Content database is not assigned.");
    public string ExpeditionNotFoundText => expeditionNotFoundText.GetText("Expedition not found");

    public string ExpeditionStatusLocked => expeditionStatusLocked.GetText("Locked");
    public string ExpeditionStatusAvailable => expeditionStatusAvailable.GetText("Available");
    public string ExpeditionStatusInProgress => expeditionStatusInProgress.GetText("In Progress");
    public string ExpeditionStatusCompleted => expeditionStatusCompleted.GetText("Completed");

    public string LibraryTitle => libraryTitle.GetText("Library");
    public string FoundEntriesLabel => foundEntriesLabel.GetText("Found entries");
    public string NoLibraryEntriesConfiguredText => noLibraryEntriesConfiguredText.GetText("No library entries configured.");
    public string NoLibraryEntriesText => noLibraryEntriesText.GetText("No entries.");

    public string TranslationStateLabel => translationStateLabel.GetText("Translation State");
    public string TranslationStateUnknown => translationStateUnknown.GetText("Unknown");
    public string TranslationStateUntranslated => translationStateUntranslated.GetText("Untranslated");
    public string TranslationStatePartiallyTranslated => translationStatePartiallyTranslated.GetText("Partially Translated");
    public string TranslationStateTranslated => translationStateTranslated.GetText("Translated");

    public string DroneTitle => droneTitle.GetText("Drone");
    public string DroneStatusLabel => droneStatusLabel.GetText("Status");
    public string DroneChargeLabel => droneChargeLabel.GetText("Charge");
    public string DroneReconModuleLabel => droneReconModuleLabel.GetText("Recon Module");
    public string DroneFutureFeaturesLabel => droneFutureFeaturesLabel.GetText("Future features");
    public string DroneStatusPlaceholder => droneStatusPlaceholder.GetText("Placeholder");
    public string DroneChargeUnknown => droneChargeUnknown.GetText("Unknown");
    public string DroneReconNotConnected => droneReconNotConnected.GetText("Not connected");
    public string DroneFeatureLaunch => droneFeatureLaunch.GetText("Launch drone");
    public string DroneFeatureScan => droneFeatureScan.GetText("Scan zones");
    public string DroneFeatureScout => droneFeatureScout.GetText("Scout route");
    public string DroneFeatureDetectIO => droneFeatureDetectIO.GetText("Detect IO signals");

    public string StationFollowUpTitle => stationFollowUpTitle.GetText("Follow-up");
    public string StationAncientObjectFoundText => stationAncientObjectFoundText.GetText("Research object received.");
    public string StationOpenLibraryText => stationOpenLibraryText.GetText("Open the Library tab to inspect the new entry.");
}