using System.Collections.Generic;
using System.Linq;

public class UpgradeEntryData
{
    public UpgradeConfig.Entry config;
    public List<UpgradeSlotData> slots = new();
    public bool IsReady => slots.All(s => s.IsFull);
    public void ResetProgress() { foreach (var s in slots) s.currentCount = 0; }
}
