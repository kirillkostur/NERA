using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HUDUpgradeUI : MonoBehaviour
{
    public static HUDUpgradeUI Instance { get; private set; }

    [Header("Панель окна апгрейда")]
    public GameObject panel;            // корневая панель (ON/OFF)

    [Header("Контейнер секций и префаб секции")]
    public Transform entriesParent;     // сюда будут добавляться секции (VerticalLayoutGroup)
    public UpgradeEntryUI entryPrefab;  // префаб одного блока: имя + иконка + его слоты

    [Header("Кнопка 'Прокачать' (общая на окно)")]
    public Button upgradeButton;

    [Header("Хинт/подсказка")]
    public TMP_Text hintText;

    // текущее отображаемое описание
    private UpgradeConfig currentConfig;

    // рантайм состояние по всем секциям
    private readonly List<UpgradeEntryData> entriesData = new();
    private readonly List<UpgradeEntryUI> entriesUI = new();

    public bool IsOpen => panel != null && panel.activeSelf;

    private void Awake()
    {
        Instance = this;
        if (panel != null) panel.SetActive(false);

        if (upgradeButton != null)
        {
            upgradeButton.onClick.RemoveAllListeners();
            upgradeButton.onClick.AddListener(OnUpgradeClick);
            upgradeButton.interactable = false;
        }

        if (hintText != null) hintText.gameObject.SetActive(false);
    }

    /// <summary>Кнопка HUD вызывает Toggle с нужным конфигом</summary>
    public void Toggle(UpgradeConfig cfg)
    {
        if (!IsOpen) Show(cfg);
        else if (currentConfig == cfg) Hide();
        else Show(cfg);
    }

    public void Show(UpgradeConfig cfg)
    {
        panel.SetActive(true);

        // если другой конфиг — перестроим
        if (currentConfig != cfg) { ClearAll(); Build(cfg); }
        else RefreshAll();

        RefreshButtonState();
    }

    public void Hide()
    {
        // прогресс НЕ очищаем, чтобы не пропадали вложенные вещи
        if (panel != null) panel.SetActive(false);
    }

    private void Build(UpgradeConfig cfg)
    {
        currentConfig = cfg;
        if (currentConfig == null || currentConfig.entries == null) return;

        foreach (var e in currentConfig.entries)
        {
            // собрать рантайм-данные секции
            var ed = new UpgradeEntryData { config = e };
            if (e.slots != null)
                foreach (var req in e.slots) ed.slots.Add(new UpgradeSlotData { config = req, currentCount = 0 });

            entriesData.Add(ed);

            // создать UI секции
            var ui = Instantiate(entryPrefab, entriesParent);
            ui.Setup(ed, this);
            entriesUI.Add(ui);
        }
    }

    private void ClearAll()
    {
        foreach (Transform c in entriesParent) Destroy(c.gameObject);
        entriesData.Clear();
        entriesUI.Clear();
        currentConfig = null;
    }

    private void RefreshAll()
    {
        foreach (var ui in entriesUI) ui.Refresh();
    }

    /// <summary>Кнопка включается, если ХОТЯ БЫ ОДНА секция готова</summary>
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

        // Новый метод поиска объектов на сцене
        var allTargets = FindObjectsByType<ObjectLevelSwitch>(FindObjectsSortMode.None);
        ObjectLevelSwitch target = null;
        foreach (var t in allTargets)
        {
            if (t.upgradeID == ready.config.targetID)
            {
                target = t;
                break;
            }
        }

        if (target != null)
        {
            target.UpgradeToNext();
            ShowHint($"Апгрейд: {ready.config.displayName}");
            ready.ResetProgress();
            RefreshButtonState();
        }
        else
        {
            ShowHint($"Цель с ID {ready.config.targetID} не найдена");
        }
    }


}
