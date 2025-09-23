using UnityEngine;

[System.Serializable]
public class UpgradeSlotData
{
    public UpgradeConfig.RequirementSlot config;
    public int currentCount = 0;

    public bool IsFull => currentCount >= (config?.requiredCount ?? 0);
    public int Need => Mathf.Max(0, (config?.requiredCount ?? 0) - currentCount);

    public void Add(int amount)
    {
        if (config == null || amount <= 0) return;
        currentCount = Mathf.Min(config.requiredCount, currentCount + amount);
    }

    public void Reset()
    {
        currentCount = 0;
    }
}
