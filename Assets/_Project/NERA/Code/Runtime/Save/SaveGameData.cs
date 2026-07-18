using System;
using System.Collections.Generic;

namespace NERA.Save
{
    [Serializable]
    public sealed class SaveGameData
    {
        public int version = 3;
        public int stationPowerState;
        public bool energyStateInitialized;
        public float stationEnergy;
        public bool energyGridEnabled;
        public List<string> discoveredLocationIds = new List<string>();
        public List<string> inventoryItemIds = new List<string>();
        public List<string> backpackSlotItemIds = new List<string>();
        public List<string> anomalySlotItemIds = new List<string>();
        public List<string> quickAccessSlotItemIds = new List<string>();
    }
}
