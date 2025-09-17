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

    [Header("Дистанция взаимодействия")]
    [Tooltip("Макс. дистанция, на которой ремонт не прервётся")]
    public float maxInteractDistance = 4f;

    [Header("UI и эффекты")]
    [Tooltip("Индикатор состояния (горит, когда объект сломан)")]
    public GameObject repairEffectMesh;
    [Tooltip("Эффект подсветки при выборе цели игроком (Particle/Plane и т.д.)")]
    public GameObject targetHighlightEffect;
    [Tooltip("Слайдер прогресса ремонта (опционально)")]
    public Slider progressBar;

    public delegate void RepairEvent(RepairableObject obj);
    public event RepairEvent OnRepaired;

    private GameObject interactor;
    private Animator interactorAnimator;
    private float progress = 0f;
    private bool repairing = false;

    private void Start()
    {
        UpdateRepairEffect();
        HideHighlight();

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

        if (repairing && !isRepaired)
        {
            // Проверка дистанции: если игрок отошёл — отменяем ремонт
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
                isRepaired = true;
                repairing = false;

                if (progressBar != null) progressBar.gameObject.SetActive(false);
                if (interactorAnimator != null) interactorAnimator.SetBool("Repair", false);

                HideHighlight(); // при ремонте сразу убираем подсветку
                Debug.Log($"✅ {objectName} отремонтирован!");
                OnRepaired?.Invoke(this);
            }
        }
    }

    /// <summary>Обновляет визуал состояния: горит ли постоянный индикатор поломки.</summary>
    private void UpdateRepairEffect()
    {
        if (repairEffectMesh != null)
            repairEffectMesh.SetActive(!isRepaired);
    }

    /// <summary>Включить подсветку при выборе игроком.</summary>
    public void ShowHighlight()
    {
        if (!isRepaired && targetHighlightEffect != null)
            targetHighlightEffect.SetActive(true);
    }

    /// <summary>Выключить подсветку выбора.</summary>
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

        repairing = true;
        Debug.Log($"🔧 Начат ремонт: {objectName}");

        if (interactorAnimator != null)
            interactorAnimator.SetBool("Repair", true);
    }

    public void HoldInteract() { /* Ремонт продолжается в Update */ }

    public void CancelInteract()
    {
        if (!repairing) return;

        repairing = false;
        progress = 0f;

        if (progressBar != null)
        {
            progressBar.value = 0f;
            progressBar.gameObject.SetActive(false);
        }

        if (interactorAnimator != null)
            interactorAnimator.SetBool("Repair", false);

        Debug.Log($"⛔ Ремонт {objectName} отменён");
    }

    // === Поломка ===
    public void BreakObject()
    {
        isRepaired = false;
        progress = 0f;
        UpdateRepairEffect();
        HideHighlight(); // сбрасываем подсветку, если объект снова сломан
        Debug.Log($"❗ {objectName} снова сломан.");
    }

    // === ITargetable ===
    public Transform GetTransform() => transform;
    public bool IsAlive() => !isRepaired;
}
