using UnityEngine;

[RequireComponent(typeof(PoweredDevice))]
public class TurretController : MonoBehaviour
{
    [Header("Настройки турели")]
    public Transform rotatingPart;       // Верхняя часть турели
    public float rotationSpeed = 5f;     // Скорость поворота
    public float attackRange = 20f;      // Радиус поиска целей
    public float fireRate = 1f;          // Выстрелов в секунду
    public int damage = 10;              // Урон по цели
    public GameObject shotEffectPrefab;  // Эффект выстрела
    public Transform firePoint;          // Точка выстрела
    public float attackConsumption = 2f; // Доп. потребление батареи при атаке
    public LayerMask spiderMask;         // Слой пауков (задать в инспекторе)

    private PoweredDevice poweredDevice;
    private Transform currentTarget;
    private float nextFireTime;
    private float baseConsumption;

    // Буфер для поиска целей
    private const int MaxHits = 32;
    private readonly Collider[] hits = new Collider[MaxHits];

    private void Awake()
    {
        poweredDevice = GetComponent<PoweredDevice>();
        baseConsumption = poweredDevice.consumptionPerSecond;
    }

    private void Update()
    {
        if (!IsTurretOperational())
        {
            currentTarget = null;
            return;
        }

        // Ищем новую цель, если текущая недействительна
        if (currentTarget == null || !IsTargetValid(currentTarget))
        {
            FindTarget();
        }

        if (currentTarget != null)
        {
            RotateTowardsTarget();
            AttackTarget();
        }
        else
        {
            poweredDevice.consumptionPerSecond = baseConsumption;
        }
    }

    private bool IsTurretOperational()
    {
        return poweredDevice != null && poweredDevice.IsConsuming();
    }

    private void FindTarget()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, attackRange, hits, spiderMask);
        float closestDist = Mathf.Infinity;
        Transform closestTarget = null;

        for (int i = 0; i < count; i++)
        {
            var t = hits[i].transform;
            if (t == null) continue;

            var health = t.GetComponent<SpiderHealth>();
            if (health == null || !t.gameObject.activeInHierarchy) continue;

            float dist = Vector3.Distance(transform.position, t.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closestTarget = t;
            }
        }

        currentTarget = closestTarget;
    }

    private bool IsTargetValid(Transform target)
    {
        if (target == null) return false;
        var health = target.GetComponent<SpiderHealth>();
        if (health == null || !target.gameObject.activeInHierarchy) return false;

        return Vector3.Distance(transform.position, target.position) <= attackRange * 1.2f;
    }

    private void RotateTowardsTarget()
    {
        if (rotatingPart == null || currentTarget == null) return;

        Vector3 dir = currentTarget.position - rotatingPart.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir);
        rotatingPart.rotation = Quaternion.Slerp(rotatingPart.rotation, targetRot, rotationSpeed * Time.deltaTime);
    }

    private void AttackTarget()
    {
        if (Time.time >= nextFireTime && currentTarget != null)
        {
            nextFireTime = Time.time + (1f / fireRate);

            if (shotEffectPrefab != null && firePoint != null)
            {
                GameObject fx = Instantiate(shotEffectPrefab, firePoint.position, firePoint.rotation);
                Destroy(fx, 2f);
            }

            poweredDevice.consumptionPerSecond = baseConsumption + attackConsumption;

            var health = currentTarget.GetComponent<SpiderHealth>();
            if (health != null)
            {
                health.TakeDamage(damage);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
