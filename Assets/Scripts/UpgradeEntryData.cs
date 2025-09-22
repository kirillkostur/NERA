using System.Collections.Generic;
using System.Linq;

public class UpgradeEntryData
{
    public UpgradeConfig.Entry config;
    public int levelIndex; // текущий уровень для которого собраны требования
    public List<UpgradeSlotData> slots = new();

    public bool IsReady => slots.All(s => s.IsFull);

    public void ResetProgress()
    {
        foreach (var s in slots) s.Reset();
    }
}
