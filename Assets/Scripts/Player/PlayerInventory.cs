// PlayerInventory.cs  — ПОЛНАЯ ВЕРСИЯ С ДОБАВЛЕННЫМ RemoveFromSlot
using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    public static PlayerInventory Instance { get; private set; }

    [Header("Конфиг (можно не назначать)")]
    public InventoryConfig config;

    [Header("Слоты (если конфиг не задан)")]
    [Min(1)] public int slotCount = 15;

    public IReadOnlyList<InventorySlot> Slots => slots;
    public event Action OnChanged;

    private List<InventorySlot> slots = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        int count = config != null ? Mathf.Max(1, config.slotCount) : Mathf.Max(1, slotCount);
        slots.Clear();
        for (int i = 0; i < count; i++) slots.Add(new InventorySlot());

        if (config != null && config.startItems != null)
        {
            foreach (var e in config.startItems)
                if (e?.item != null) AddItem(e.item, e.count, false);
        }

        OnChanged?.Invoke();
    }

    // --- helpers ---
    private static bool SameItem(InventoryItem a, InventoryItem b)
        => a != null && b != null && a.ID == b.ID;

    private static int StackLimit(InventorySlot a, InventorySlot b, InventoryItem incoming = null)
    {
        int ma = a?.item != null ? a.item.maxStack : (incoming != null ? incoming.maxStack : 0);
        int mb = b?.item != null ? b.item.maxStack : (incoming != null ? incoming.maxStack : 0);
        return Mathf.Max(ma, mb);
    }

    // Добавление предметов (стак по ID)
    public int AddItem(InventoryItem item, int amount, bool raiseEvent = true)
    {
        if (item == null || amount <= 0) return 0;
        int remaining = amount;

        // 1) доливаем в уже существующие стеки
        for (int i = 0; i < slots.Count && remaining > 0; i++)
        {
            var s = slots[i];
            if (!s.IsEmpty && SameItem(s.item, item))
            {
                int maxStack = StackLimit(s, null, item);
                if (s.count < maxStack)
                {
                    int add = Mathf.Min(remaining, maxStack - s.count);
                    s.count += add;
                    remaining -= add;
                }
            }
        }

        // 2) кладём в пустые слоты
        for (int i = 0; i < slots.Count && remaining > 0; i++)
        {
            var s = slots[i];
            if (s.IsEmpty)
            {
                int maxStack = item.maxStack;
                s.item = item;
                s.count = Mathf.Min(maxStack, remaining);
                remaining -= s.count;
            }
        }

        if (raiseEvent && amount != remaining)
        {
            OnChanged?.Invoke();

            // 👉 Сообщаем квестовой системе, что предмет добавлен
            int added = amount - remaining;
            if (item != null && added > 0)
            {
                GameEvents.RaiseQuestEvent(item.ID.ToString(), added);
            }
        }

        return amount - remaining;
    }

    public void ClearSlot(int index)
    {
        if (!IsValidIndex(index)) return;
        if (slots[index].IsEmpty) return;
        slots[index].Clear();
        OnChanged?.Invoke();
    }

    // НОВОЕ: частично списать из КОНКРЕТНОГО слота (для Drag&Drop в окно апгрейда)
    // Возвращает, сколько реально списано.
    public int RemoveFromSlot(int index, int amount)
    {
        if (!IsValidIndex(index) || amount <= 0) return 0;
        var s = slots[index];
        if (s.IsEmpty) return 0;

        int take = Mathf.Min(amount, s.count);
        s.count -= take;
        if (s.count <= 0) s.Clear();
        if (take > 0) OnChanged?.Invoke();
        return take;
    }


    // Переместить/слить/свап (стак по ID)
    public void MoveOrSwap(int from, int to)
    {
        if (from == to) return;
        if (!IsValidIndex(from) || !IsValidIndex(to)) return;

        var a = slots[from];
        var b = slots[to];
        if (a.IsEmpty) return;

        if (b.IsEmpty)
        {
            slots[to] = a;
            slots[from] = new InventorySlot();
            OnChanged?.Invoke();
            return;
        }

        if (SameItem(a.item, b.item))
        {
            int maxStack = StackLimit(a, b);
            int total = a.count + b.count;

            if (total <= maxStack)
            {
                b.count = total;
                a.Clear();
            }
            else
            {
                b.count = maxStack;
                a.count = total - maxStack;
            }

            OnChanged?.Invoke();
            return;
        }

        (slots[to], slots[from]) = (slots[from], slots[to]);
        OnChanged?.Invoke();
    }

    public void ResizeSlots(int newCount)
    {
        newCount = Mathf.Max(1, newCount);
        if (newCount == slots.Count) return;

        if (newCount < slots.Count)
            slots.RemoveRange(newCount, slots.Count - newCount);
        else
            while (slots.Count < newCount) slots.Add(new InventorySlot());

        OnChanged?.Invoke();
    }

    public bool IsValidIndex(int i) => i >= 0 && i < slots.Count;
}
