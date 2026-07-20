using System;
using System.Collections.Generic;

namespace NERA.Save
{
    [Serializable]
    public sealed class SaveGameData
    {
        public int version = 4;
        public int stationPowerState;
        public bool energyStateInitialized;
        public float stationEnergy;
        public bool energyGridEnabled;
        public float antennaCondition = 1f;
        public string activeAntennaSignalLocationId;
        public int activeAntennaSignalSectorIndex = -1;
        public List<string> consumedAntennaSignalLocationIds = new List<string>();
        public List<string> discoveredLocationIds = new List<string>();
        public List<string> inventoryItemIds = new List<string>();
        public List<string> backpackSlotItemIds = new List<string>();
        public List<string> anomalySlotItemIds = new List<string>();
        public List<string> quickAccessSlotItemIds = new List<string>();
        public List<string> analyzedResearchIds = new List<string>();
        public List<string> unlockedLibraryEntryIds = new List<string>();
        public List<string> knownLibraryItemIds = new List<string>();
    }
}
