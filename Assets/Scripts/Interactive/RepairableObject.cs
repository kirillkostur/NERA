using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Collider))]
public class RepairableObject : MonoBehaviour, ITargetable, IInteractable
{
    [Header("Основные настройки")]
    public string objectName = "Генератор";
    [Tooltip("Время ремонта (сек.)")]
    public float repairTime = 3f;
    [Tooltip("Починен ли объект в начале игры")]
    public bool isRepaired = false;

    [Header("Режим взаимодействия")]
    [Tooltip("Если включено — ремонт происходит мгновенно без анимации и прогресс-бара")]
    public bool instantInteract = false;

    [Header("Дистанция взаимодействия")]
    [Tooltip("Макс. дистанция, на которой ремонт не прервётся")]
    public float maxInteractDistance = 4f;

    [Header("UI и эффекты")]
    public GameObject icon;     // Красная иконка проблем
    public GameObject targetHighlightEffect; // Зелёный таргет
    public Slider progressBar;

    public delegate void RepairEvent(RepairableObject obj);
    public event RepairEvent OnRepaired;

    private GameObject interactor;
    private Animator interactorAnimator;
    private float progress = 0f;
    private bool repairing = false;

    private AlertManager alerts;

    private void Start()
    {
        UpdateRepairEffect();
        HideHighlight();
        alerts = FindFirstObjectByType<AlertManager>();

        if (progressBar != null)
        {
            progressBar.minValue = 0f;
            progressBar.maxValue = 1f;
            progressBar.value = 0f;
            progressBar.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        UpdateRepairEffect();

        if (repairing && !isRepaired && !instantInteract)
        {
            if (interactor == null || Vector3.Distance(interactor.transform.position, transform.position) > maxInteractDistance)
            {
                CancelInteract();
                return;
            }

            progress += Time.deltaTime / repairTime;
            if (progressBar != null)
            {
                if (!progressBar.gameObject.activeSelf) progressBar.gameObject.SetActive(true);
                progressBar.value = progress;
            }

            if (progress >= 1f)
            {
                CompleteRepair();
            }
        }
    }

    private void CompleteRepair()
    {
        // 🚫 Если идёт буря и это солнечная панель — блокируем ремонт
        var panel = GetComponent<SolarPanelSystem>();
        if (panel != null && SandStormController.StormActive)
        {
            alerts?.ShowAlert("Система очистки панелей не работает во время бури!");
            Logger.Log($"🚫 {objectName} нельзя отремонтировать во время бури!");
            repairing = false;
            progress = 0f;

            if (progressBar != null) progressBar.gameObject.SetActive(false);
            if (interactorAnimator != null) interactorAnimator.SetBool("Repair", false);

            return;
        }

        isRepaired = true;
        repairing = false;

        if (progressBar != null) progressBar.gameObject.SetActive(false);
        if (interactorAnimator != null) interactorAnimator.SetBool("Repair", false);

        HideHighlight();
        Logger.Log($"✅ {objectName} отремонтирован!");
        OnRepaired?.Invoke(this);

        // 👇 ДОБАВЛЕНО: уведомим систему квестов
        GameEvents.RaiseObjectRepaired(this);
    }

    private void UpdateRepairEffect()
    {
        if (icon != null)
            icon.SetActive(!isRepaired);
    }

    public void ShowHighlight()
    {
        if (targetHighlightEffect != null)
            targetHighlightEffect.SetActive(true);
    }

    public void HideHighlight()
    {
        if (targetHighlightEffect != null)
            targetHighlightEffect.SetActive(false);
    }

    // === Взаимодействие ===
    public void StartInteract(GameObject player)
    {
        if (isRepaired) return;

        interactor = player;
        interactorAnimator = player.GetComponent<Animator>();

        // ❌ Эффекты солнечной панели больше не запускаем здесь

        if (instantInteract)
        {
            CompleteRepair();
            return;
        }

        repairing = true;
        Logger.Log($"🔧 Начат ремонт: {objectName}");

        if (interactorAnimator != null)
            interactorAnimator.SetBool("Repair", true);
    }

    public void HoldInteract() { /* Ремонт продолжается в Update */ }

    public void CancelInteract()
    {
        if (!repairing || instantInteract) return;

        repairing = false;
        progress = 0f;

        if (progressBar != null)
        {
            progressBar.value = 0f;
            progressBar.gameObject.SetActive(false);
        }

        if (interactorAnimator != null)
            interactorAnimator.SetBool("Repair", false);

        Logger.Log($"⛔ Ремонт {objectName} отменён");
    }

    public void BreakObject()
    {
        if (!isRepaired) return;

        isRepaired = false;
        progress = 0f;
        UpdateRepairEffect();
        HideHighlight();
        alerts?.ShowAlert("Буря повредила панель, включите систему очистки!");
        Logger.Log($"❗ {objectName} снова сломан.");
    }

    // === ITargetable ===
    public Transform GetTransform() => transform;
    public bool IsAlive() => !isRepaired;

    public void ToggleHighlight(bool on)
    {
        if (on) ShowHighlight(); else HideHighlight();
    }
}
