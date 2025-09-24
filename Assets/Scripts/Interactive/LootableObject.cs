using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Identifiable))]
public class LootableObject : MonoBehaviour, ITargetable, IInteractable, ILootPreviewProvider
{
    public LootConfig lootConfig;
    public GameObject icon;               // 🔹 Подсказка для игрока
    public GameObject targetHighlight;
    public Slider progressBar;
    public float lootTime = 2f;
    public float maxInteractDistance = 3.5f;

    private bool lootedCompletely = false;
    private bool looting = false;
    private bool wasSearchedOnce = false;  // 🔹 Флаг первого обыска
    private bool lootPanelOpen = false;
    private float progress = 0f;
    private GameObject interactor;
    private Animator animator;
    private AlertManager alerts;

    [System.Serializable]
    private class RuntimeEntry { public InventoryItem item; public int count; }
    private readonly List<RuntimeEntry> contents = new List<RuntimeEntry>();

    private void Start()
    {
        contents.Clear();
        if (lootConfig != null && lootConfig.lootItems != null)
        {
            foreach (var e in lootConfig.lootItems)
            {
                if (e != null && e.item != null && e.count > 0)
                    contents.Add(new RuntimeEntry { item = e.item, count = e.count });
            }
        }

        alerts = FindFirstObjectByType<AlertManager>();

        if (progressBar != null)
        {
            progressBar.gameObject.SetActive(false);
            progressBar.minValue = 0f;
            progressBar.maxValue = 1f;
            progressBar.value = 0f;
        }

        // 🔹 Иконка горит, если есть предметы и объект ещё не обыскивали
        icon?.SetActive(HasItems() && !wasSearchedOnce);
        targetHighlight?.SetActive(false);
    }

    private void Update()
    {
        if (!looting) return;

        if (interactor == null || Vector3.Distance(interactor.transform.position, transform.position) > maxInteractDistance)
        {
            CancelInteract();
            return;
        }

        progress += Time.deltaTime / lootTime;
        if (progressBar != null)
        {
            if (!progressBar.gameObject.activeSelf) progressBar.gameObject.SetActive(true);
            progressBar.value = progress;
        }

        if (progress >= 1f)
            CompleteLoot();
    }

    private bool HasItems()
    {
        foreach (var c in contents)
            if (c.count > 0) return true;
        return false;
    }

    private void CompleteLoot()
    {
        looting = false;
        wasSearchedOnce = true;
        lootPanelOpen = true;

        progressBar?.gameObject.SetActive(false);
        if (animator != null) animator.SetBool("Repair", false);

        // 🔹 После успешного обыска скрываем иконку навсегда
        icon?.SetActive(false);
        targetHighlight?.SetActive(true);

        HUDLootUI.Instance?.ShowLoot(this, true);
    }

    public void TakeAll()
    {
        if (contents.Count == 0) return;

        bool anyLeft = false;

        foreach (var c in contents)
        {
            if (c.count <= 0 || c.item == null) continue;

            int added = PlayerInventory.Instance.AddItem(c.item, c.count);
            if (added < c.count)
            {
                c.count -= added;
                anyLeft = true;
            }
            else
            {
                c.count = 0;
            }
        }

        HUDLootUI.Instance?.RefreshIfCurrent(this);
        HUDInventoryUI.Instance?.Refresh();

        if (!anyLeft)
        {
            lootedCompletely = true;
            lootPanelOpen = false;
            HUDLootUI.Instance?.HideLootIfCurrent(this);
            icon?.SetActive(false);
            targetHighlight?.SetActive(false);

            var ident = GetComponent<Identifiable>();
            string id = ident != null ? ident.Id : gameObject.name;
            GameEvents.RaiseQuestEvent(new QuestEventData(QuestEventType.LootItemFromContainer, id, 1));
        }
        else
        {
            alerts?.ShowAlert("Недостаточно места в инвентаре!");
            // 🔹 Даже если остались предметы, иконку повторно не включаем
        }
    }

    public void RemoveItem(InventoryItem item, int amount)
    {
        foreach (var c in contents)
        {
            if (c.item == item && c.count > 0)
            {
                c.count -= amount;
                if (c.count < 0) c.count = 0;
                break;

            }
        }

        HUDLootUI.Instance?.RefreshIfCurrent(this);

        if (!HasItems())
        {
            lootedCompletely = true;
            lootPanelOpen = false;
            icon?.SetActive(false);
            targetHighlight?.SetActive(false);
            HUDLootUI.Instance?.HideLootIfCurrent(this);

            var ident = GetComponent<Identifiable>();
            string id = ident != null ? ident.Id : gameObject.name;
            GameEvents.RaiseQuestEvent(new QuestEventData(QuestEventType.LootItemFromContainer, id, 1));
        }
    }

    public void ReturnItem(InventoryItem item, int amount)
    {
        foreach (var c in contents)
        {
            if (c.item == item)
            {
                c.count += amount;
                HUDLootUI.Instance?.RefreshIfCurrent(this);
                return;
            }
        }

        contents.Add(new RuntimeEntry { item = item, count = amount });
        HUDLootUI.Instance?.RefreshIfCurrent(this);
    }

    public Transform GetTransform() => transform;
    public bool IsAlive() => !lootedCompletely;

    public void ToggleHighlight(bool on)
    {
        // 🔹 Управляем только подсветкой таргета
        targetHighlight?.SetActive(on);

        if (on && wasSearchedOnce && HasItems())
        {
            HUDLootUI.Instance?.ShowLoot(this, true);
            lootPanelOpen = true;
        }
        else if (!on)
        {
            HUDLootUI.Instance?.HideLootIfCurrent(this);
            lootPanelOpen = false;
        }
    }

    public void StartInteract(GameObject player)
    {
        // 🔹 Если уже обыскан или панель открыта, блокируем повторный прогресс
        if (lootPanelOpen || lootedCompletely || !HasItems()) return;

        interactor = player;
        animator = player.GetComponent<Animator>();
        if (animator != null) animator.SetBool("Repair", true);

        looting = true;
        progress = 0f;

        targetHighlight?.SetActive(true);
        // 🔹 Icon уже горит до обыска, тут не управляем
    }

    public void HoldInteract() { }

    public void CancelInteract()
    {
        looting = false;
        progress = 0f;

        if (progressBar != null)
        {
            progressBar.value = 0f;
            progressBar.gameObject.SetActive(false);
        }

        if (animator != null) animator.SetBool("Repair", false);
    }

    public List<(InventoryItem item, int count)> GetPreview()
    {
        var list = new List<(InventoryItem, int)>();
        foreach (var c in contents)
        {
            if (c.item != null && c.count > 0)
                list.Add((c.item, c.count));
        }
        return list;
    }

    private void OnDestroy()
    {
        HUDLootUI.Instance?.HideLootIfCurrent(this);
        lootPanelOpen = false;
    }
}
