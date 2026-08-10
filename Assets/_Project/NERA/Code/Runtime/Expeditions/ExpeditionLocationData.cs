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
        [Tooltip("Time in seconds required for the drone to discover this location.")]
        private float droneScanDuration = 3f;
        [SerializeField, Min(0)]
        [Tooltip("Drone system upgrade level required to survey this location.")]
        private int requiredDroneUpgradeLevel;
        [SerializeField, Min(0.1f)]
        [Tooltip("Time in seconds required for the antenna to detect this location.")]
        private float antennaScanDuration = 3f;
        [SerializeField, Min(0)]
        [Tooltip("Antenna system upgrade level required to detect this location.")]
        private int requiredAntennaUpgradeLevel;
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
        public float DroneScanDuration => Mathf.Max(0.1f, droneScanDuration);
        public int RequiredDroneUpgradeLevel => Mathf.Max(0, requiredDroneUpgradeLevel);
        public float AntennaScanDuration => Mathf.Max(0.1f, antennaScanDuration);
        public int RequiredAntennaUpgradeLevel =>
            Mathf.Max(0, requiredAntennaUpgradeLevel);
        public LocationState InitialState => initialState;
        public MapSymbol MapSymbol => mapSymbol;
        public MapSlotData MapSlot => mapSlot;
        public Sprite MapPreview => mapPreview;
    }
}
