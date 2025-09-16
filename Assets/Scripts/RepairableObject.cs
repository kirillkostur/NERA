using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Collider))]
public class RepairableObject : MonoBehaviour, ITargetable
{
    [Header("Основные настройки")]
    public string objectName = "Объект";
    public float repairTime = 3f;
    public bool isRepaired = false;

    private float currentProgress = 0f;
    private bool isPlayerNearby = false;
    private Animator playerAnimator;

    [Header("UI и эффекты")]
    public Slider progressBar;
    public GameObject repairEffectMesh;

    public delegate void RepairEvent(RepairableObject obj);
    public event RepairEvent OnRepaired;

    void Start()
    {
        if (repairEffectMesh != null)
            repairEffectMesh.SetActive(!isRepaired);
    }

    void Update()
    {
        if (repairEffectMesh != null)
            repairEffectMesh.SetActive(!isRepaired);

        if (!isPlayerNearby || isRepaired) return;

        if (Input.GetKey(KeyCode.E))
        {
            currentProgress += Time.deltaTime;

            if (progressBar != null && !progressBar.gameObject.activeSelf)
                progressBar.gameObject.SetActive(true);

            if (progressBar != null)
                progressBar.value = currentProgress / repairTime;

            if (playerAnimator != null)
                playerAnimator.SetBool("Repair", true);

            if (currentProgress >= repairTime)
            {
                isRepaired = true;
                Debug.Log($"✅ {objectName} починен!");

                if (progressBar != null) progressBar.gameObject.SetActive(false);
                if (playerAnimator != null) playerAnimator.SetBool("Repair", false);

                OnRepaired?.Invoke(this);
            }
        }
        else
        {
            if (currentProgress > 0f)
            {
                currentProgress = 0f;
                if (progressBar != null) progressBar.value = 0f;
            }

            if (progressBar != null && progressBar.gameObject.activeSelf)
                progressBar.gameObject.SetActive(false);

            if (playerAnimator != null)
                playerAnimator.SetBool("Repair", false);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = true;
            playerAnimator = other.GetComponent<Animator>();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = false;
            currentProgress = 0f;

            if (progressBar != null) progressBar.gameObject.SetActive(false);

            if (playerAnimator != null)
                playerAnimator.SetBool("Repair", false);

            playerAnimator = null;
        }
    }

    // === Реализация ITargetable ===
    public Transform GetTransform() => transform;
    public bool IsAlive() => !isRepaired;
}
