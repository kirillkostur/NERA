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

    private PoweredDevice poweredDevice;
    private Transform currentTarget;
    private float nextFireTime;
    private float baseConsumption;

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

        FindTarget();

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
        GameObject[] spiders = GameObject.FindGameObjectsWithTag("Spider");
        float closestDist = Mathf.Infinity;
        Transform closestTarget = null;

        foreach (var spider in spiders)
        {
            float dist = Vector3.Distance(transform.position, spider.transform.position);
            if (dist < attackRange && dist < closestDist)
            {
                closestDist = dist;
                closestTarget = spider.transform;
            }
        }

        currentTarget = closestTarget;
    }

    private void RotateTowardsTarget()
    {
        if (rotatingPart == null || currentTarget == null) return;

        Vector3 dir = currentTarget.position - rotatingPart.position;
        dir.y = 0f;
        if (dir == Vector3.zero) return;

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
                Destroy(fx, 2f); // удаляем эффект выстрела
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
