using System;
using System.Collections.Generic;
using NERA.Locations;
using UnityEngine;

namespace NERA.Expeditions
{
    public sealed class ExpeditionDiscoveryController : MonoBehaviour
    {
        private readonly HashSet<string> discoveredLocationIds = new HashSet<string>();
        [SerializeField] private List<ExpeditionLocationData> knownLocations =
            new List<ExpeditionLocationData>();

        public static ExpeditionDiscoveryController Instance { get; private set; }

        public event Action<string> LocationDiscovered;
        public List<ExpeditionLocationData> KnownLocations => knownLocations;
        public IEnumerable<string> DiscoveredLocationIds => discoveredLocationIds;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
        }

        public bool Discover(string locationId)
        {
            if (string.IsNullOrWhiteSpace(locationId) ||
                !discoveredLocationIds.Add(locationId))
            {
                return false;
            }

            LocationDiscovered?.Invoke(locationId);
            Debug.Log($"ExpeditionDiscovery: Location '{locationId}' discovered.", this);
            return true;
        }

        public bool Discover(ExpeditionLocationData location)
        {
            return location != null && Discover(location.LocationId);
        }

        public bool IsDiscovered(string locationId)
        {
            return !string.IsNullOrWhiteSpace(locationId) &&
                discoveredLocationIds.Contains(locationId);
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

        public void RestoreDiscovered(IEnumerable<string> locationIds)
        {
            discoveredLocationIds.Clear();

            if (locationIds == null)
                return;

            foreach (string locationId in locationIds)
            {
                if (!string.IsNullOrWhiteSpace(locationId))
                    discoveredLocationIds.Add(locationId);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
