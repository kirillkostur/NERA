using System;
using System.Collections.Generic;
using NERA.Locations;
using NERA.Quests;
using UnityEngine;

namespace NERA.Expeditions
{
    public sealed class ExpeditionDiscoveryController : MonoBehaviour
    {
        private readonly HashSet<string> discoveredLocationIds = new HashSet<string>();
        private readonly Dictionary<string, ExpeditionLocationData>
            locationsById =
                new Dictionary<string, ExpeditionLocationData>(
                    StringComparer.Ordinal);
        [SerializeField] private List<ExpeditionLocationData> knownLocations =
            new List<ExpeditionLocationData>();

        public static ExpeditionDiscoveryController Instance { get; private set; }

        public event Action<string> LocationDiscovered;
        public IReadOnlyList<ExpeditionLocationData> KnownLocations =>
            knownLocations;
        public IEnumerable<string> DiscoveredLocationIds => discoveredLocationIds;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            RebuildLocationIndex();
        }

        public bool Discover(string locationId)
        {
            string normalizedId = NormalizeId(locationId);
            if (string.IsNullOrEmpty(normalizedId) ||
                !discoveredLocationIds.Add(normalizedId))
            {
                return false;
            }

            LocationDiscovered?.Invoke(normalizedId);
            string targetName = locationsById.TryGetValue(
                    normalizedId,
                    out ExpeditionLocationData location)
                ? location.DisplayName
                : normalizedId;
            QuestController.Instance?.Report(
                QuestSignalType.LocationDiscovered,
                normalizedId,
                targetName);
            Debug.Log(
                $"ExpeditionDiscovery: Location '{normalizedId}' discovered.",
                this);
            return true;
        }

        public bool Discover(ExpeditionLocationData location)
        {
            return location != null && Discover(location.LocationId);
        }

        public bool IsDiscovered(string locationId)
        {
            string normalizedId = NormalizeId(locationId);
            return !string.IsNullOrEmpty(normalizedId) &&
                discoveredLocationIds.Contains(normalizedId);
        }

        public bool IsDiscovered(ExpeditionLocationData location)
        {
            return location != null && IsDiscovered(location.LocationId);
        }

        public List<ExpeditionLocationData> GetDiscoveredLocations()
        {
            List<ExpeditionLocationData> result = new List<ExpeditionLocationData>();

            foreach (ExpeditionLocationData location in knownLocations)
            {
                if (IsDiscovered(location))
                    result.Add(location);
            }

            return result;
        }

        public List<ExpeditionLocationData> GetKnownLocations(
            LocationType locationType
        )
        {
            List<ExpeditionLocationData> result = new List<ExpeditionLocationData>();

            foreach (ExpeditionLocationData location in knownLocations)
            {
                if (location != null && location.LocationType == locationType)
                    result.Add(location);
            }

            return result;
        }

        public List<ExpeditionLocationData> GetKnownLocations(
            DiscoverySource discoverySource
        )
        {
            List<ExpeditionLocationData> result = new List<ExpeditionLocationData>();

            foreach (ExpeditionLocationData location in knownLocations)
            {
                if (location != null && location.DiscoverySource == discoverySource)
                    result.Add(location);
            }

            return result;
        }

        public List<ExpeditionLocationData> GetUndiscoveredLocations(
            DiscoverySource discoverySource
        )
        {
            List<ExpeditionLocationData> result = new List<ExpeditionLocationData>();

            foreach (ExpeditionLocationData location in knownLocations)
            {
                if (location != null &&
                    location.DiscoverySource == discoverySource &&
                    !IsDiscovered(location))
                {
                    result.Add(location);
                }
            }

            return result;
        }

        public bool TryGetNextUndiscovered(
            DiscoverySource discoverySource,
            out ExpeditionLocationData location
        )
        {
            foreach (ExpeditionLocationData candidate in knownLocations)
            {
                if (candidate == null ||
                    candidate.DiscoverySource != discoverySource ||
                    IsDiscovered(candidate))
                {
                    continue;
                }

                location = candidate;
                return true;
            }

            location = null;
            return false;
        }

        public bool TryGetKnownLocation(
            string locationId,
            out ExpeditionLocationData location)
        {
            location = null;
            string normalizedId = NormalizeId(locationId);
            return !string.IsNullOrEmpty(normalizedId) &&
                locationsById.TryGetValue(normalizedId, out location);
        }

        public bool TryGetKnownLocationBySceneName(
            string sceneName,
            out ExpeditionLocationData location)
        {
            location = null;
            if (string.IsNullOrWhiteSpace(sceneName))
                return false;

            foreach (ExpeditionLocationData candidate in knownLocations)
            {
                if (candidate != null &&
                    string.Equals(
                        candidate.SceneName,
                        sceneName.Trim(),
                        StringComparison.OrdinalIgnoreCase))
                {
                    location = candidate;
                    return true;
                }
            }

            return false;
        }

        public void RestoreDiscovered(IEnumerable<string> locationIds)
        {
            discoveredLocationIds.Clear();

            if (locationIds == null)
                return;

            foreach (string locationId in locationIds)
            {
                string normalizedId = NormalizeId(locationId);
                if (!string.IsNullOrEmpty(normalizedId))
                    discoveredLocationIds.Add(normalizedId);
            }
        }

        private void RebuildLocationIndex()
        {
            locationsById.Clear();
            foreach (ExpeditionLocationData location in knownLocations)
            {
                if (location == null ||
                    string.IsNullOrWhiteSpace(location.LocationId))
                {
                    continue;
                }

                if (!locationsById.TryAdd(location.LocationId, location))
                {
                    Debug.LogError(
                        $"ExpeditionDiscovery: Duplicate location ID " +
                        $"'{location.LocationId}'.",
                        this);
                }
            }
        }

        private static string NormalizeId(string locationId)
        {
            return locationId?.Trim() ?? string.Empty;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
