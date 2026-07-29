using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace NERA.Enemies
{
    /// <summary>
    /// Shared pool for IO projectiles. Pools are separated by authored prefab,
    /// while color and scale are reapplied for every shot.
    /// </summary>
    public sealed class IOProjectilePool : MonoBehaviour
    {
        private const int FallbackPoolKey = 0;
        private const int DefaultCapacity = 8;
        private const int MaximumPoolSize = 64;

        private static IOProjectilePool instance;

        private readonly Dictionary<int, ObjectPool<IOEnergyProjectile>> pools =
            new Dictionary<int, ObjectPool<IOEnergyProjectile>>();

        public static IOEnergyProjectile Spawn(
            GameObject prefab,
            Vector3 position,
            Quaternion rotation,
            float scale,
            Color color,
            float emissionIntensity)
        {
            IOProjectilePool owner = GetOrCreate();
            ObjectPool<IOEnergyProjectile> pool = owner.GetPool(prefab);
            IOEnergyProjectile projectile = pool.Get();
            projectile.transform.SetPositionAndRotation(position, rotation);
            projectile.transform.localScale =
                Vector3.one * Mathf.Max(0.01f, scale);
            projectile.ConfigureVisual(color, emissionIntensity);
            projectile.SetReleaseAction(pool.Release);
            return projectile;
        }

        private static IOProjectilePool GetOrCreate()
        {
            if (instance != null)
                return instance;

            GameObject root = new GameObject("IO_ProjectilePool");
            instance = root.AddComponent<IOProjectilePool>();
            return instance;
        }

        private ObjectPool<IOEnergyProjectile> GetPool(GameObject prefab)
        {
            int key = prefab != null
                ? prefab.GetInstanceID()
                : FallbackPoolKey;
            if (pools.TryGetValue(
                    key,
                    out ObjectPool<IOEnergyProjectile> existing))
            {
                return existing;
            }

            ObjectPool<IOEnergyProjectile> created =
                new ObjectPool<IOEnergyProjectile>(
                    () => CreateProjectile(prefab),
                    projectile => projectile.gameObject.SetActive(true),
                    projectile =>
                    {
                        projectile.transform.SetParent(transform, false);
                        projectile.gameObject.SetActive(false);
                    },
                    projectile =>
                    {
                        if (projectile != null)
                            Destroy(projectile.gameObject);
                    },
                    collectionCheck: true,
                    defaultCapacity: DefaultCapacity,
                    maxSize: MaximumPoolSize);
            pools.Add(key, created);
            return created;
        }

        private IOEnergyProjectile CreateProjectile(GameObject prefab)
        {
            GameObject projectile = prefab != null
                ? Instantiate(prefab, transform)
                : GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectile.name = "IO_Energy_Projectile";
            projectile.transform.SetParent(transform, false);

            Collider projectileCollider = projectile.GetComponent<Collider>();
            if (projectileCollider != null)
            {
                projectileCollider.enabled = false;
                Destroy(projectileCollider);
            }

            IOEnergyProjectile controller =
                projectile.GetComponent<IOEnergyProjectile>();
            if (controller == null)
                controller = projectile.AddComponent<IOEnergyProjectile>();
            projectile.SetActive(false);
            return controller;
        }

        private void OnDestroy()
        {
            foreach (ObjectPool<IOEnergyProjectile> pool in pools.Values)
                pool.Dispose();
            pools.Clear();

            if (instance == this)
                instance = null;
        }
    }
}
