using System;
using System.Collections.Generic;
using UnityEngine;

namespace NERA.Save
{
    [DisallowMultipleComponent]
    public sealed class WorldStateController : MonoBehaviour
    {
        private readonly HashSet<string> consumedObjects =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> defeatedEnemies =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> completedWorldFlags =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> enemySpawnerWaves =
            new Dictionary<string, List<string>>(StringComparer.Ordinal);

        public static WorldStateController Instance { get; private set; }
        public IReadOnlyCollection<string> ConsumedObjects => consumedObjects;
        public IReadOnlyCollection<string> DefeatedEnemies => defeatedEnemies;
        public IReadOnlyCollection<string> CompletedWorldFlags =>
            completedWorldFlags;

        public event Action StateChanged;
        public event Action StateRestored;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
        }

        public bool IsConsumed(string persistentKey)
        {
            return consumedObjects.Contains(
                PersistentSceneIdentity.Normalize(persistentKey));
        }

        public bool IsEnemyDefeated(string persistentKey)
        {
            return defeatedEnemies.Contains(
                PersistentSceneIdentity.Normalize(persistentKey));
        }

        public bool IsWorldFlagCompleted(string persistentKey)
        {
            return completedWorldFlags.Contains(
                PersistentSceneIdentity.Normalize(persistentKey));
        }

        public IReadOnlyList<string> GetEnemySpawnerWaveIds(
            string spawnerId)
        {
            string normalizedSpawnerId =
                PersistentSceneIdentity.Normalize(spawnerId);
            if (string.IsNullOrEmpty(normalizedSpawnerId) ||
                !enemySpawnerWaves.TryGetValue(
                    normalizedSpawnerId,
                    out List<string> waveIds))
            {
                return Array.Empty<string>();
            }

            List<string> result = new List<string>(waveIds);
            return result;
        }

        public bool RememberEnemySpawnerWave(
            string spawnerId,
            string waveId)
        {
            string normalizedSpawnerId =
                PersistentSceneIdentity.Normalize(spawnerId);
            string normalizedWaveId =
                PersistentSceneIdentity.Normalize(waveId);
            if (string.IsNullOrEmpty(normalizedSpawnerId) ||
                string.IsNullOrEmpty(normalizedWaveId))
            {
                return false;
            }

            if (!enemySpawnerWaves.TryGetValue(
                    normalizedSpawnerId,
                    out List<string> waveIds))
            {
                waveIds = new List<string>();
                enemySpawnerWaves.Add(normalizedSpawnerId, waveIds);
            }

            if (waveIds.Contains(normalizedWaveId))
                return false;

            waveIds.Add(normalizedWaveId);
            StateChanged?.Invoke();
            return true;
        }

        public void MarkConsumed(string persistentKey)
        {
            string key = PersistentSceneIdentity.Normalize(persistentKey);
            if (!string.IsNullOrEmpty(key) && consumedObjects.Add(key))
                StateChanged?.Invoke();
        }

        public void MarkEnemyDefeated(string persistentKey)
        {
            string key = PersistentSceneIdentity.Normalize(persistentKey);
            if (!string.IsNullOrEmpty(key) && defeatedEnemies.Add(key))
                StateChanged?.Invoke();
        }

        public bool SetWorldFlagCompleted(
            string persistentKey,
            bool completed = true)
        {
            string key = PersistentSceneIdentity.Normalize(persistentKey);
            if (string.IsNullOrEmpty(key))
                return false;

            bool changed = completed
                ? completedWorldFlags.Add(key)
                : completedWorldFlags.Remove(key);
            if (changed)
                StateChanged?.Invoke();
            return changed;
        }

        public void Capture(SaveGameData data)
        {
            if (data == null)
                return;

            data.consumedWorldObjectIds.Clear();
            data.defeatedEnemyObjectIds.Clear();
            data.completedWorldFlagIds ??= new List<string>();
            data.completedWorldFlagIds.Clear();
            data.enemySpawnerWaves ??=
                new List<EnemySpawnerWaveSaveData>();
            data.enemySpawnerWaves.Clear();
            data.consumedWorldObjectIds.AddRange(consumedObjects);
            data.defeatedEnemyObjectIds.AddRange(defeatedEnemies);
            data.completedWorldFlagIds.AddRange(completedWorldFlags);
            foreach (KeyValuePair<string, List<string>> pair in
                     enemySpawnerWaves)
            {
                for (int index = 0; index < pair.Value.Count; index++)
                {
                    data.enemySpawnerWaves.Add(
                        new EnemySpawnerWaveSaveData
                        {
                            spawnerId = pair.Key,
                            waveId = pair.Value[index],
                            order = index
                        });
                }
            }
            data.consumedWorldObjectIds.Sort(StringComparer.Ordinal);
            data.defeatedEnemyObjectIds.Sort(StringComparer.Ordinal);
            data.completedWorldFlagIds.Sort(StringComparer.Ordinal);
            data.enemySpawnerWaves.Sort((left, right) =>
            {
                int spawnerComparison = string.CompareOrdinal(
                    left.spawnerId,
                    right.spawnerId);
                return spawnerComparison != 0
                    ? spawnerComparison
                    : left.order.CompareTo(right.order);
            });
        }

        public void Restore(SaveGameData data)
        {
            consumedObjects.Clear();
            defeatedEnemies.Clear();
            completedWorldFlags.Clear();
            enemySpawnerWaves.Clear();
            AddNormalized(consumedObjects, data?.consumedWorldObjectIds);
            AddNormalized(defeatedEnemies, data?.defeatedEnemyObjectIds);
            AddNormalized(completedWorldFlags, data?.completedWorldFlagIds);
            if (data?.enemySpawnerWaves != null)
            {
                foreach (EnemySpawnerWaveSaveData saved in
                         data.enemySpawnerWaves)
                {
                    if (saved != null)
                    {
                        AddEnemySpawnerWave(
                            saved.spawnerId,
                            saved.waveId);
                    }
                }
            }
            StateRestored?.Invoke();
        }

        public void ResetState()
        {
            consumedObjects.Clear();
            defeatedEnemies.Clear();
            completedWorldFlags.Clear();
            enemySpawnerWaves.Clear();
            StateRestored?.Invoke();
        }

        private void AddEnemySpawnerWave(
            string spawnerId,
            string waveId)
        {
            string normalizedSpawnerId =
                PersistentSceneIdentity.Normalize(spawnerId);
            string normalizedWaveId =
                PersistentSceneIdentity.Normalize(waveId);
            if (string.IsNullOrEmpty(normalizedSpawnerId) ||
                string.IsNullOrEmpty(normalizedWaveId))
            {
                return;
            }

            if (!enemySpawnerWaves.TryGetValue(
                    normalizedSpawnerId,
                    out List<string> waveIds))
            {
                waveIds = new List<string>();
                enemySpawnerWaves.Add(normalizedSpawnerId, waveIds);
            }

            if (!waveIds.Contains(normalizedWaveId))
                waveIds.Add(normalizedWaveId);
        }

        private static void AddNormalized(
            HashSet<string> destination,
            IEnumerable<string> source)
        {
            if (source == null)
                return;

            foreach (string value in source)
            {
                string key = PersistentSceneIdentity.Normalize(value);
                if (!string.IsNullOrEmpty(key))
                    destination.Add(key);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
