using System;
using System.Collections.Generic;
using NERA.Quests;
using NERA.Save;
using UnityEngine;
using UnityEngine.Serialization;

namespace NERA.Enemies
{
    public enum EnemySpawnerActivationMode
    {
        [InspectorName("Вручную")]
        Manual,
        [InspectorName("При запуске сцены")]
        OnStart
    }

    [DisallowMultipleComponent]
    public sealed class EnemySpawner : MonoBehaviour
    {
        private const float WorldStateBindRetryInterval = 0.5f;
        private static readonly Dictionary<string, EnemySpawner> Registry =
            new Dictionary<string, EnemySpawner>(StringComparer.Ordinal);
        private static readonly Dictionary<string, List<string>>
            PendingWaveRequests =
                new Dictionary<string, List<string>>(
                    StringComparer.Ordinal);

        [Header("Идентификация")]
        [Tooltip(
            "Стабильный ID спавнера для квестовых условий, например " +
            "expedition_01/wave_01.")]
        [SerializeField] private string spawnerId;
        [SerializeField] private string displayName;

        [Header("Враги")]
        [SerializeField] private IOEnemyController[] enemyPrefabs =
            Array.Empty<IOEnemyController>();
        [SerializeField, Min(1)] private int spawnCount = 1;
        [SerializeField] private bool randomizePrefab = true;
        [SerializeField] private bool randomizeYaw = true;
        [SerializeField] private Transform spawnedEnemiesRoot;

        [Header("Область спавна")]
        [SerializeField, Min(0f)] private float spawnRadius = 5f;
        [Tooltip(
            "Проецирует случайную точку радиуса на поверхность. " +
            "Если выключено, используется весь объём сферы.")]
        [SerializeField] private bool snapToGround = true;
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField, Min(0.1f)] private float groundProbeHeight = 20f;
        [SerializeField, Min(0.1f)] private float groundProbeDistance = 50f;
        [SerializeField] private float groundOffset = 1.6f;

        [Header("Запуск")]
        [SerializeField] private EnemySpawnerActivationMode activationMode =
            EnemySpawnerActivationMode.Manual;
        [Tooltip(
            "Не позволяет запускать новую волну после первой. Для " +
            "переиспользуемых квестовых спавнеров выключить.")]
        [SerializeField] private bool spawnOnlyOnce;

        [Header("Сохранение волны")]
        [Tooltip(
            "Сохраняет все созданные волны, убитые позиции и состояние их " +
            "дропа. Работает и для переиспользуемого спавнера.")]
        [FormerlySerializedAs("persistOneShotWave")]
        [SerializeField] private bool persistWaveState = true;

        [Header("Диагностика")]
        [SerializeField] private bool logEvents;

        private readonly List<IOEnemyController> aliveEnemies =
            new List<IOEnemyController>();
        private readonly HashSet<string> restoredWaveIds =
            new HashSet<string>(StringComparer.Ordinal);
        private WorldStateController boundWorldState;
        private bool hasSpawned;
        private bool waveActive;
        private int spawnedInCurrentWave;
        private int killedInCurrentWave;
        private float nextWorldStateBindAttemptAt;
        private string currentWaveId;

        public event Action<EnemySpawner, int> WaveSpawned;
        public event Action<EnemySpawner> WaveCleared;

        public string SpawnerId => Normalize(spawnerId);
        public string DisplayName => string.IsNullOrWhiteSpace(displayName)
            ? name
            : displayName.Trim();
        public IReadOnlyList<IOEnemyController> EnemyPrefabs =>
            enemyPrefabs ??
            (IReadOnlyList<IOEnemyController>)
            Array.Empty<IOEnemyController>();
        public EnemySpawnerActivationMode ActivationMode => activationMode;
        public float SpawnRadius => Mathf.Max(0f, spawnRadius);
        public bool PersistsWaveState => persistWaveState;
        public bool HasSpawned => hasSpawned;
        public bool IsWaveActive => waveActive;
        public int AliveCount
        {
            get
            {
                PruneDestroyedEnemies();
                return aliveEnemies.Count;
            }
        }

        private void Reset()
        {
            EnsureSpawnerId();
        }

        private void OnValidate()
        {
            EnsureSpawnerId();
            spawnerId = spawnerId?.Trim();
            displayName = displayName?.Trim();
            spawnCount = Mathf.Max(1, spawnCount);
            spawnRadius = Mathf.Max(0f, spawnRadius);
            groundProbeHeight = Mathf.Max(0.1f, groundProbeHeight);
            groundProbeDistance = Mathf.Max(0.1f, groundProbeDistance);
        }

        private void OnEnable()
        {
            Register();
            if (TryBindWorldState())
                TryRestoreRememberedWaves();
            TrySpawnPendingRequests();
        }

        private void Start()
        {
            if (TryBindWorldState())
                TryRestoreRememberedWaves();
            TrySpawnPendingRequests();

            if (activationMode == EnemySpawnerActivationMode.OnStart)
                SpawnWave();
        }

        private void Update()
        {
            if (boundWorldState != null ||
                Time.unscaledTime < nextWorldStateBindAttemptAt)
            {
                return;
            }

            nextWorldStateBindAttemptAt =
                Time.unscaledTime + WorldStateBindRetryInterval;
            if (TryBindWorldState())
                TryRestoreRememberedWaves();
        }

        private void OnDisable()
        {
            Unregister();
            UnbindWorldState();
        }

        private void OnDestroy()
        {
            foreach (IOEnemyController enemy in aliveEnemies)
            {
                if (enemy != null)
                    enemy.Died -= HandleEnemyDied;
            }
        }

        [ContextMenu("Spawn Wave")]
        public int SpawnWave()
        {
            string waveId = $"runtime/{Guid.NewGuid():N}";
            return SpawnWave(waveId);
        }

        public int SpawnWave(string waveId)
        {
            return SpawnWaveInternal(
                Normalize(waveId),
                reportSpawnSignal: true,
                trackActiveWave: true,
                bypassReuseLimit: false);
        }

        public static bool TrySpawnWave(
            string targetSpawnerId,
            string waveId,
            out int spawnedCount)
        {
            spawnedCount = 0;
            string normalizedId = Normalize(targetSpawnerId);
            if (string.IsNullOrEmpty(normalizedId) ||
                !Registry.TryGetValue(
                    normalizedId,
                    out EnemySpawner spawner) ||
                spawner == null ||
                !spawner.isActiveAndEnabled)
            {
                return false;
            }

            spawnedCount = spawner.SpawnWave(waveId);
            return spawnedCount > 0;
        }

        public static bool RequestWave(
            string targetSpawnerId,
            string waveId,
            out int spawnedCount)
        {
            spawnedCount = 0;
            string normalizedSpawnerId = Normalize(targetSpawnerId);
            string normalizedWaveId = EnsureWaveId(waveId);
            if (string.IsNullOrEmpty(normalizedSpawnerId))
                return false;

            if (Registry.TryGetValue(
                    normalizedSpawnerId,
                    out EnemySpawner spawner) &&
                spawner != null &&
                spawner.isActiveAndEnabled)
            {
                spawnedCount = spawner.SpawnWave(normalizedWaveId);
                if (spawnedCount > 0)
                    return true;
                if (!spawner.IsWaveActive)
                    return false;
            }

            if (!PendingWaveRequests.TryGetValue(
                    normalizedSpawnerId,
                    out List<string> waveIds))
            {
                waveIds = new List<string>();
                PendingWaveRequests.Add(normalizedSpawnerId, waveIds);
            }

            if (!waveIds.Contains(normalizedWaveId))
                waveIds.Add(normalizedWaveId);
            WorldStateController.Instance?.RememberEnemySpawnerWave(
                normalizedSpawnerId,
                normalizedWaveId);
            return true;
        }

        private int SpawnWaveInternal(
            string waveId,
            bool reportSpawnSignal,
            bool trackActiveWave,
            bool bypassReuseLimit)
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning(
                    $"EnemySpawner '{name}' can only spawn in Play Mode.",
                    this);
                return 0;
            }

            PruneDestroyedEnemies();
            if (trackActiveWave &&
                (waveActive ||
                 (!bypassReuseLimit && spawnOnlyOnce && hasSpawned)))
                return 0;

            if (!HasValidEnemyPrefab())
            {
                Debug.LogWarning(
                    $"EnemySpawner '{name}' has no IO enemy prefab.",
                    this);
                return 0;
            }

            if (trackActiveWave)
            {
                aliveEnemies.Clear();
                spawnedInCurrentWave = 0;
                killedInCurrentWave = 0;
                currentWaveId = EnsureWaveId(waveId);
            }

            string resolvedWaveId = trackActiveWave
                ? currentWaveId
                : EnsureWaveId(waveId);
            int createdCount = 0;

            for (int index = 0; index < spawnCount; index++)
            {
                System.Random slotRandom = persistWaveState
                    ? CreateSlotRandom(resolvedWaveId, index)
                    : null;
                IOEnemyController prefab = SelectPrefab(index, slotRandom);
                if (prefab == null)
                    continue;

                Vector3 position = GetSpawnPosition(slotRandom);
                Quaternion rotation = randomizeYaw
                    ? Quaternion.Euler(
                        0f,
                        GetRandomRange(slotRandom, 0f, 360f),
                        0f)
                    : transform.rotation;
                IOEnemyController enemy = Instantiate(
                    prefab,
                    position,
                    rotation,
                    spawnedEnemiesRoot);
                if (persistWaveState)
                {
                    enemy.ConfigureAsSpawnedInstance(
                        GetEnemyPersistentKey(resolvedWaveId, index));
                }
                else
                {
                    enemy.ConfigureAsRuntimeSpawn();
                }
                if (!enemy.gameObject.activeSelf)
                    enemy.gameObject.SetActive(true);
                enemy.ActivateWaveCombat();

                enemy.name =
                    $"{prefab.name}_Spawned_{index + 1:00}";
                if (trackActiveWave)
                {
                    enemy.Died += HandleEnemyDied;
                    aliveEnemies.Add(enemy);
                    spawnedInCurrentWave++;
                }

                createdCount++;
            }

            if (createdCount == 0)
                return 0;

            restoredWaveIds.Add(resolvedWaveId);
            if (persistWaveState && TryBindWorldState())
            {
                boundWorldState.RememberEnemySpawnerWave(
                    SpawnerId,
                    resolvedWaveId);
            }

            if (!trackActiveWave)
                return createdCount;

            hasSpawned = true;
            waveActive = true;
            if (reportSpawnSignal)
            {
                ReportWaveSignal(
                    QuestSignalType.EnemyWaveSpawned,
                    spawnedInCurrentWave);
                WaveSpawned?.Invoke(this, spawnedInCurrentWave);
            }
            Log(
                $"spawned {spawnedInCurrentWave} enemies inside radius " +
                $"{SpawnRadius:0.##} (wave '{currentWaveId}')");
            return spawnedInCurrentWave;
        }

        private void Register()
        {
            string id = SpawnerId;
            if (string.IsNullOrEmpty(id))
                return;

            if (Registry.TryGetValue(id, out EnemySpawner existing) &&
                existing != null && existing != this)
            {
                Debug.LogError(
                    $"Duplicate active EnemySpawner ID '{id}' on " +
                    $"'{existing.name}' and '{name}'.",
                    this);
                return;
            }

            Registry[id] = this;
        }

        private void Unregister()
        {
            string id = SpawnerId;
            if (!string.IsNullOrEmpty(id) &&
                Registry.TryGetValue(id, out EnemySpawner registered) &&
                registered == this)
            {
                Registry.Remove(id);
            }
        }

        private bool TryBindWorldState()
        {
            WorldStateController worldState = WorldStateController.Instance;
            if (worldState == null)
                return false;
            if (boundWorldState == worldState)
                return true;

            UnbindWorldState();
            boundWorldState = worldState;
            boundWorldState.StateRestored += HandleWorldStateRestored;
            foreach (string waveId in restoredWaveIds)
            {
                boundWorldState.RememberEnemySpawnerWave(
                    SpawnerId,
                    waveId);
            }
            return true;
        }

        private void UnbindWorldState()
        {
            if (boundWorldState == null)
                return;

            boundWorldState.StateRestored -= HandleWorldStateRestored;
            boundWorldState = null;
        }

        private void HandleWorldStateRestored()
        {
            if (waveActive || AliveCount > 0)
                return;

            restoredWaveIds.Clear();
            hasSpawned = false;
            TryRestoreRememberedWaves();
            TrySpawnPendingRequests();
        }

        private void TryRestoreRememberedWaves()
        {
            if (!persistWaveState ||
                boundWorldState == null ||
                string.IsNullOrEmpty(SpawnerId))
            {
                return;
            }

            IReadOnlyList<string> waveIds =
                boundWorldState.GetEnemySpawnerWaveIds(SpawnerId);
            foreach (string waveId in waveIds)
            {
                if (restoredWaveIds.Contains(waveId))
                    continue;

                bool defeated = IsWaveCompletelyDefeated(waveId);
                if (!defeated && waveActive)
                {
                    Debug.LogWarning(
                        $"EnemySpawner '{SpawnerId}' has more than one " +
                        "unfinished saved wave. Only one unfinished wave " +
                        "can be restored at a time.",
                        this);
                    continue;
                }

                int restored = SpawnWaveInternal(
                    waveId,
                    reportSpawnSignal: false,
                    trackActiveWave: !defeated,
                    bypassReuseLimit: true);
                if (restored > 0)
                    hasSpawned = true;
            }
        }

        private bool IsWaveCompletelyDefeated(string waveId)
        {
            if (boundWorldState == null)
                return false;

            for (int index = 0; index < spawnCount; index++)
            {
                if (!boundWorldState.IsEnemyDefeated(
                        GetEnemyPersistentKey(waveId, index)))
                {
                    return false;
                }
            }

            return true;
        }

        private void TrySpawnPendingRequests()
        {
            if (!PendingWaveRequests.TryGetValue(
                    SpawnerId,
                    out List<string> pendingIds))
            {
                return;
            }

            List<string> snapshot = new List<string>(pendingIds);
            foreach (string waveId in snapshot)
            {
                if (restoredWaveIds.Contains(waveId))
                {
                    pendingIds.Remove(waveId);
                    continue;
                }

                if (waveActive)
                    break;

                if (SpawnWave(waveId) <= 0)
                    break;

                pendingIds.Remove(waveId);
            }

            if (pendingIds.Count == 0)
                PendingWaveRequests.Remove(SpawnerId);
        }

        private void HandleEnemyDied(IOEnemyController enemy)
        {
            if (enemy != null)
                enemy.Died -= HandleEnemyDied;
            aliveEnemies.Remove(enemy);

            if (!waveActive)
                return;

            killedInCurrentWave++;
            if (killedInCurrentWave < spawnedInCurrentWave)
                return;

            waveActive = false;
            ReportWaveSignal(
                QuestSignalType.EnemyWaveCleared,
                spawnedInCurrentWave);
            WaveCleared?.Invoke(this);
            Log("wave cleared");
            TryRestoreRememberedWaves();
            TrySpawnPendingRequests();
        }

        private void ReportWaveSignal(QuestSignalType type, float enemyCount)
        {
            if (string.IsNullOrEmpty(SpawnerId))
            {
                Debug.LogWarning(
                    $"EnemySpawner '{name}' cannot report '{type}' " +
                    "because Spawner ID is empty.",
                    this);
                return;
            }

            QuestController.Instance?.Report(
                type,
                SpawnerId,
                DisplayName,
                value: enemyCount);
        }

        private IOEnemyController SelectPrefab(
            int spawnIndex,
            System.Random random)
        {
            int length = enemyPrefabs?.Length ?? 0;
            if (length == 0)
                return null;

            int startIndex = randomizePrefab
                ? GetRandomIndex(random, length)
                : spawnIndex % length;
            for (int offset = 0; offset < length; offset++)
            {
                IOEnemyController candidate =
                    enemyPrefabs[(startIndex + offset) % length];
                if (candidate != null)
                    return candidate;
            }

            return null;
        }

        private bool HasValidEnemyPrefab()
        {
            if (enemyPrefabs == null)
                return false;
            foreach (IOEnemyController prefab in enemyPrefabs)
            {
                if (prefab != null)
                    return true;
            }

            return false;
        }

        private Vector3 GetSpawnPosition(System.Random random)
        {
            if (!snapToGround)
            {
                return transform.position +
                    GetInsideUnitSphere(random) * SpawnRadius;
            }

            Vector2 offset = GetInsideUnitCircle(random) * SpawnRadius;
            Vector3 basePosition = transform.position +
                new Vector3(offset.x, 0f, offset.y);
            Vector3 rayOrigin = basePosition + Vector3.up * groundProbeHeight;
            if (Physics.Raycast(
                    rayOrigin,
                    Vector3.down,
                    out RaycastHit hit,
                    groundProbeDistance,
                    groundMask,
                    QueryTriggerInteraction.Ignore))
            {
                return hit.point + Vector3.up * groundOffset;
            }

            return basePosition + Vector3.up * groundOffset;
        }

        private string GetEnemyPersistentKey(
            string waveId,
            int spawnIndex)
        {
            return $"enemy_spawner/{SpawnerId}/{EnsureWaveId(waveId)}/" +
                $"enemy_{spawnIndex + 1:00}";
        }

        private System.Random CreateSlotRandom(
            string waveId,
            int spawnIndex)
        {
            string input =
                $"{SpawnerId}:{EnsureWaveId(waveId)}:{spawnIndex + 1}";
            unchecked
            {
                uint hash = 2166136261;
                foreach (char character in input)
                {
                    hash ^= character;
                    hash *= 16777619;
                }

                return new System.Random((int)hash);
            }
        }

        private static int GetRandomIndex(
            System.Random random,
            int length)
        {
            return random != null
                ? random.Next(0, length)
                : UnityEngine.Random.Range(0, length);
        }

        private static float GetRandomRange(
            System.Random random,
            float minimum,
            float maximum)
        {
            return random != null
                ? Mathf.Lerp(minimum, maximum, (float)random.NextDouble())
                : UnityEngine.Random.Range(minimum, maximum);
        }

        private static Vector2 GetInsideUnitCircle(System.Random random)
        {
            if (random == null)
                return UnityEngine.Random.insideUnitCircle;

            Vector2 point;
            do
            {
                point = new Vector2(
                    GetRandomRange(random, -1f, 1f),
                    GetRandomRange(random, -1f, 1f));
            }
            while (point.sqrMagnitude > 1f);

            return point;
        }

        private static Vector3 GetInsideUnitSphere(System.Random random)
        {
            if (random == null)
                return UnityEngine.Random.insideUnitSphere;

            Vector3 point;
            do
            {
                point = new Vector3(
                    GetRandomRange(random, -1f, 1f),
                    GetRandomRange(random, -1f, 1f),
                    GetRandomRange(random, -1f, 1f));
            }
            while (point.sqrMagnitude > 1f);

            return point;
        }

        private void PruneDestroyedEnemies()
        {
            for (int index = aliveEnemies.Count - 1; index >= 0; index--)
            {
                if (aliveEnemies[index] == null)
                    aliveEnemies.RemoveAt(index);
            }
        }

        private void Log(string message)
        {
            if (logEvents)
                Debug.Log($"EnemySpawner '{SpawnerId}': {message}.", this);
        }

        private void OnDrawGizmosSelected()
        {
            Color previous = Gizmos.color;
            Gizmos.color = new Color(0.1f, 0.75f, 1f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, SpawnRadius);
            Gizmos.DrawLine(
                transform.position - Vector3.right * 0.35f,
                transform.position + Vector3.right * 0.35f);
            Gizmos.DrawLine(
                transform.position - Vector3.forward * 0.35f,
                transform.position + Vector3.forward * 0.35f);
            Gizmos.color = previous;
        }

        private static string Normalize(string value)
        {
            return value?.Trim().ToLowerInvariant() ?? string.Empty;
        }

        private static string EnsureWaveId(string value)
        {
            string normalized = Normalize(value);
            return string.IsNullOrEmpty(normalized)
                ? $"runtime/{Guid.NewGuid():N}"
                : normalized;
        }

        private void EnsureSpawnerId()
        {
            if (string.IsNullOrWhiteSpace(spawnerId))
                GenerateNewSpawnerId();
        }

        [ContextMenu("Generate New Spawner ID")]
        private void GenerateNewSpawnerId()
        {
            spawnerId = "enemy_spawner_" +
                Guid.NewGuid().ToString("N");
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry()
        {
            Registry.Clear();
            PendingWaveRequests.Clear();
        }
    }
}
