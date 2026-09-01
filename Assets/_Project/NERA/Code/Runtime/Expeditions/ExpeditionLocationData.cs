using NERA.Core;
using NERA.Localization;
using NERA.Locations;
using NERA.Terminal;
using UnityEngine;

namespace NERA.Expeditions
{
    [CreateAssetMenu(
        fileName = "ExpeditionLocation",
        menuName = "NERA/Expeditions/Location"
    )]
    public sealed class ExpeditionLocationData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable unique save-game ID. Do not change after release.")]
        [SerializeField] private string locationId;
        [SerializeField] private LocationType locationType;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;

        [Header("Travel")]
        [SerializeField] private SceneReference scene = new SceneReference();
        [Tooltip("Must match a SceneSpawnPoint ID inside the selected scene.")]
        [SerializeField] private string spawnPointId;

        [Header("Discovery")]
        [SerializeField] private DiscoverySource discoverySource;
        [SerializeField, Min(0.1f)]
        [InspectorName("Drone Flight Duration")]
        [Tooltip(
            "Drone flight/scan time in seconds. Flight battery cost equals " +
            "this duration multiplied by the drone Flight Energy Consumption.")]
        private float droneScanDuration = 3f;
        [SerializeField, Min(0f)]
        [Tooltip("Required configured Travel Range of the station drone.")]
        private float requiredDroneTravelRange;
        [SerializeField, Min(0f)]
        [Tooltip("Required configured Scan Range of the station antenna.")]
        private float requiredAntennaScanRange;
        [SerializeField, Min(1f)]
        [InspectorName("Close Delay After Collection")]
        [Tooltip(
            "How many seconds an antenna-discovered Unknown Signal remains " +
            "available after all of its persistent world items have been " +
            "collected.")]
        private float postCollectionLifetime = 60f;
        [SerializeField] private LocationState initialState;

        [Header("Map")]
        [SerializeField] private MapSymbol mapSymbol;
        [Tooltip(
            "3D terminal-map slot used by this location. " +
            "Assign the same asset to a MapLocationSlot in MainScene.")]
        [SerializeField] private MapSlotData mapSlot;
        [SerializeField] private Sprite mapPreview;

        public string LocationId => locationId?.Trim() ?? string.Empty;
        public LocationType LocationType => locationType;
        public string DisplayName => NERALocalization.Content(
            "location",
            LocationId,
            "name",
            string.IsNullOrWhiteSpace(displayName)
                ? LocationId
                : displayName.Trim());
        public string Description => NERALocalization.Content(
            "location",
            LocationId,
            "description",
            description?.Trim() ?? string.Empty);
        public SceneReference Scene => scene;
        public string ScenePath => scene?.ScenePath ?? string.Empty;
        public string SceneName => scene?.SceneName ?? string.Empty;
        public string SpawnPointId => spawnPointId?.Trim() ?? string.Empty;
        public DiscoverySource DiscoverySource => discoverySource;
        public float DroneFlightDuration =>
            Mathf.Max(0.1f, droneScanDuration);
        public float RequiredDroneTravelRange =>
            Mathf.Max(0f, requiredDroneTravelRange);
        public float RequiredAntennaScanRange =>
            Mathf.Max(0f, requiredAntennaScanRange);
        public bool UsesPostCollectionLifetime =>
            locationType == LocationType.UnknownSignal &&
            discoverySource == DiscoverySource.Antenna;
        public float PostCollectionLifetime =>
            Mathf.Max(1f, postCollectionLifetime);
        public LocationState InitialState => initialState;
        public MapSymbol MapSymbol => mapSymbol;
        public MapSlotData MapSlot => mapSlot;
        public Sprite MapPreview => mapPreview;
    }
}
