using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HUDUpgradeUI : MonoBehaviour
{
    public static HUDUpgradeUI Instance { get; private set; }

    [Header("Панель окна апгрейда")]
    public GameObject panel;

    [Header("Контейнер и префаб секции")]
    public Transform entriesParent;
    public UpgradeEntryUI entryPrefab;

    [Header("Кнопка 'Прокачать'")]
    public Button upgradeButton;

    [Header("Подсказка/хинт")]
    public TMP_Text hintText;

    private UpgradeConfig currentConfig;
    private readonly List<UpgradeEntryData> entriesData = new();
    private readonly List<UpgradeEntryUI> entriesUI = new();

    public bool IsOpen => panel != null && panel.activeSelf;

    private void Awake()
    {
        Instance = this;
        if (panel) panel.SetActive(false);

        if (upgradeButton != null)
        {
            upgradeButton.onClick.RemoveAllListeners();
            upgradeButton.onClick.AddListener(OnUpgradeClick);
            upgradeButton.interactable = false;
        }

        if (hintText != null) hintText.gameObject.SetActive(false);
    }

    public void Toggle(UpgradeConfig cfg)
    {
        if (!IsOpen) Show(cfg);
        else if (currentConfig == cfg) Hide();
        else Show(cfg);
    }

    public void Show(UpgradeConfig cfg)
    {
        panel.SetActive(true);

        if (currentConfig != cfg)
        {
            ClearAll();
            Build(cfg);
        }
        else
        {
            RefreshAll();
        }

        RefreshButtonState();
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
    }

    private void Build(UpgradeConfig cfg)
    {
        currentConfig = cfg;
        if (currentConfig == null || currentConfig.entries == null) return;

        foreach (var e in currentConfig.entries)
        {
            var targets = FindObjectsByType<ObjectLevelSwitch>(FindObjectsSortMode.None);
            var target = System.Array.Find(targets, t => t.upgradeID == e.targetID);

            if (target != null && target.IsMaxLevel)
                continue;

            int currentLevel = target != null ? target.currentLevel : 0;
            int nextIndex = currentLevel < e.levelRequirements.Length ? currentLevel : -1;
            if (nextIndex == -1) continue;

            var reqSlots = e.levelRequirements[nextIndex].slots;
            if (reqSlots == null || reqSlots.Length == 0) continue;

            var entryData = new UpgradeEntryData { config = e, levelIndex = nextIndex };
            foreach (var req in reqSlots)
                entryData.slots.Add(new UpgradeSlotData { config = req, currentCount = 0 });

            entriesData.Add(entryData);

            var ui = Instantiate(entryPrefab, entriesParent);
            ui.Setup(entryData, this);
            entriesUI.Add(ui);
        }
    }

    private void ClearAll(bool keepConfig = false)
    {
        foreach (Transform c in entriesParent) Destroy(c.gameObject);
        entriesData.Clear();
        entriesUI.Clear();
        if (!keepConfig) currentConfig = null;
    }

    private void RefreshAll()
    {
        foreach (var ui in entriesUI) ui.Refresh();
    }

    public void RefreshButtonState()
    {
        bool anyReady = false;
        foreach (var e in entriesData)
            if (e.IsReady) { anyReady = true; break; }

        if (upgradeButton != null) upgradeButton.interactable = anyReady;
        RefreshAll();
    }

    public void ShowHint(string msg)
    {
        if (hintText == null) return;
        hintText.text = msg;
        hintText.gameObject.SetActive(true);
        CancelInvoke(nameof(HideHint));
        Invoke(nameof(HideHint), 2f);
    }
    private void HideHint() => hintText?.gameObject.SetActive(false);

    private void OnUpgradeClick()
    {
        UpgradeEntryData ready = null;
        foreach (var e in entriesData)
            if (e.IsReady) { ready = e; break; }

        if (ready == null)
        {
            ShowHint("Недостаточно материалов");
            return;
        }

        var targets = FindObjectsByType<ObjectLevelSwitch>(FindObjectsSortMode.None);
        ObjectLevelSwitch target = null;
        foreach (var t in targets)
            if (t.upgradeID == ready.config.targetID) { target = t; break; }

        if (target != null)
        {
            if (target.IsMaxLevel)
            {
                ShowHint("Максимальный уровень достигнут");
                return;
            }

            target.UpgradeToNext();
            ShowHint($"Апгрейд: {ready.config.displayName}");

            // Пересобираем список для нового уровня, не сбрасывая currentConfig
            ClearAll(true);
            Build(currentConfig);
            RefreshButtonState();
        }
        else
        {
            ShowHint($"Цель {ready.config.targetID} не найдена");
        }
    }
}
