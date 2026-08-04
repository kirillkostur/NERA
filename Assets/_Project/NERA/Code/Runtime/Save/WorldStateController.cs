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
            data.consumedWorldObjectIds.AddRange(consumedObjects);
            data.defeatedEnemyObjectIds.AddRange(defeatedEnemies);
            data.completedWorldFlagIds.AddRange(completedWorldFlags);
            data.consumedWorldObjectIds.Sort(StringComparer.Ordinal);
            data.defeatedEnemyObjectIds.Sort(StringComparer.Ordinal);
            data.completedWorldFlagIds.Sort(StringComparer.Ordinal);
        }

        public void Restore(SaveGameData data)
        {
            consumedObjects.Clear();
            defeatedEnemies.Clear();
            completedWorldFlags.Clear();
            AddNormalized(consumedObjects, data?.consumedWorldObjectIds);
            AddNormalized(defeatedEnemies, data?.defeatedEnemyObjectIds);
            AddNormalized(completedWorldFlags, data?.completedWorldFlagIds);
            StateRestored?.Invoke();
        }

        public void ResetState()
        {
            consumedObjects.Clear();
            defeatedEnemies.Clear();
            completedWorldFlags.Clear();
            StateRestored?.Invoke();
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
