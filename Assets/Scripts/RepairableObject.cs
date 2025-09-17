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
    [Tooltip("Слайдер прогресса ремонта (опционально)")]
    public Slider progressBar;
    [Tooltip("Квад/меш или Particle System для подсветки объекта")]
    public GameObject repairEffectMesh;

    public delegate void RepairEvent(RepairableObject obj);
    public event RepairEvent OnRepaired;

    private GameObject interactor;
    private Animator interactorAnimator;
    private float progress = 0f;
    private bool repairing = false;
    private bool isTargeted = false;

    private void Start()
    {
        UpdateRepairEffect();

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
        if (repairing && !isRepaired)
        {
            // Проверяем дистанцию
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

                Debug.Log($"✅ {objectName} отремонтирован!");
                UpdateRepairEffect();
                OnRepaired?.Invoke(this);
            }
        }
    }

    /// <summary>Обновляет видимость подсветки/меша в зависимости от статуса ремонта.</summary>
    private void UpdateRepairEffect()
    {
        if (repairEffectMesh != null)
            repairEffectMesh.SetActive(isTargeted && !isRepaired);
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
        Debug.Log($"❗ {objectName} снова сломан.");
    }

    // === ITargetable ===
    public Transform GetTransform() => transform;
    public bool IsAlive() => !isRepaired;
    public void SetTargeted(bool active)
    {
        if (isTargeted == active) return;

        isTargeted = active;
        UpdateRepairEffect();
    }
}
