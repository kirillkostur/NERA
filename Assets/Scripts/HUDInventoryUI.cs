using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HUDInventoryUI : MonoBehaviour
{
    public static HUDInventoryUI Instance { get; private set; }

    [Header("Ссылки")]
    public GameObject inventoryPanel;   // панель окна инвентаря
    public Button backpackButton;       // кнопка рюкзака на HUD
    public Transform slotsParent;       // контейнер (GridLayoutGroup)
    public GameObject slotPrefab;       // префаб слота (см. SlotUI)

    private readonly List<SlotUI> spawnedSlots = new();

    private void Awake()
    {
        Instance = this;
        if (inventoryPanel != null) inventoryPanel.SetActive(false);
        if (backpackButton != null) backpackButton.onClick.AddListener(Toggle);
    }

    private void OnEnable()
    {
        if (PlayerInventory.Instance != null)
            PlayerInventory.Instance.OnChanged += Refresh;
    }

    private void OnDisable()
    {
        if (PlayerInventory.Instance != null)
            PlayerInventory.Instance.OnChanged -= Refresh;
    }

    private void Start()
    {
        BuildSlots();   // создаём UI слоты по количеству в инвентаре
        Refresh();      // и отображаем содержимое
    }

    private void BuildSlots()
    {
        // очистим старые
        foreach (Transform c in slotsParent) Destroy(c.gameObject);
        spawnedSlots.Clear();

        var inv = PlayerInventory.Instance;
        int count = inv != null ? inv.Slots.Count : 0;

        for (int i = 0; i < count; i++)
        {
            var go = Instantiate(slotPrefab, slotsParent);
            var ui = go.GetComponent<SlotUI>();
            ui.Bind(i);
            spawnedSlots.Add(ui);
        }
    }

    public void Refresh()
    {
        var inv = PlayerInventory.Instance;
        if (inv == null) return;

        // если число слотов изменилось — перестроим сетку
        if (spawnedSlots.Count != inv.Slots.Count)
            BuildSlots();

        for (int i = 0; i < spawnedSlots.Count; i++)
            spawnedSlots[i].SetSlot(inv.Slots[i]);
    }

    private void Toggle()
    {
        bool active = !inventoryPanel.activeSelf;
        inventoryPanel.SetActive(active);
        if (active) Refresh();
    }
}
