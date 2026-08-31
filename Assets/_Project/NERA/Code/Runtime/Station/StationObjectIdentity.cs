using UnityEngine;

namespace NERA.Station
{
    /// <summary>
    /// Data-only link between a station object in a gameplay scene or UI
    /// preview and its shared StationSystemsConfig definition.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StationObjectIdentity : MonoBehaviour
    {
        [SerializeField] private StationSystemType systemType;
        [Tooltip("Stable StationSystems_Default object ID. Every physical " +
                 "station object must have a non-empty unique ID.")]
        [SerializeField] private string objectId;

        public StationSystemType SystemType => systemType;
        public string ObjectId => objectId?.Trim() ?? string.Empty;
        public StationSystemDefinition Definition => ResolveDefinition();
        public string DisplayName =>
            ResolveDefinition()?.DisplayName ?? gameObject.name;

        public StationSystemDefinition ResolveDefinition(
            StationSystemsConfig config = null)
        {
            config ??= StationSystemsController.Instance?.Config ??
                StationSystemsConfig.LoadDefault();
            if (config == null)
                return null;

            if (!string.IsNullOrWhiteSpace(ObjectId))
                return config.FindByObjectId(ObjectId);

            return config.Find(systemType);
        }

        public void Configure(StationSystemType type, string stableObjectId)
        {
            systemType = type;
            objectId = stableObjectId?.Trim() ?? string.Empty;
        }

        private void OnValidate()
        {
            objectId = objectId?.Trim();
        }
    }
}
