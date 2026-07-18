using System;
using System.Collections.Generic;

namespace NERA.Save
{
    [Serializable]
    public sealed class SaveGameData
    {
        public int version = 1;
        public int stationPowerState;
        public List<string> discoveredLocationIds = new List<string>();
        public List<string> inventoryItemIds = new List<string>();
    }
}
