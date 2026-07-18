using UnityEngine;
using NERA.Locations;

namespace NERA.Expeditions
{
    [CreateAssetMenu(
        fileName = "ExpeditionLocation",
        menuName = "NERA/Expeditions/Location"
    )]
    public sealed class ExpeditionLocationData : ScriptableObject
    {
        [SerializeField] private string locationId;
        [SerializeField] private LocationId id;
        [SerializeField] private LocationType locationType;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private string sceneName;
        [SerializeField] private string spawnPointId;
        [SerializeField] private DiscoverySource discoverySource;
        [Header("Drone Survey")]
        [SerializeField, Min(0.1f)]
        [Tooltip("Time in seconds required for the drone to discover this location.")]
        private float droneScanDuration = 3f;
        [SerializeField] private LocationState initialState;
        [SerializeField] private MapSymbol mapSymbol;
        [SerializeField, Range(0, 8)] private int mapSectorIndex;
        [SerializeField] private Sprite mapPreview;

        public string LocationId => locationId;
        public LocationId Id => id;
        public LocationType LocationType => locationType;
        public string DisplayName => displayName;
        public string Description => description;
        public string SceneName => sceneName;
        public string SpawnPointId => spawnPointId;
        public DiscoverySource DiscoverySource => discoverySource;
        public float DroneScanDuration => Mathf.Max(0.1f, droneScanDuration);
        public LocationState InitialState => initialState;
        public MapSymbol MapSymbol => mapSymbol;
        public int MapSectorIndex => mapSectorIndex;
        public Sprite MapPreview => mapPreview;
    }
}
