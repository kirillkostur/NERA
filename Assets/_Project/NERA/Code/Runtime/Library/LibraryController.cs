using System;
using System.Collections.Generic;
using UnityEngine;

namespace NERA.Library
{
    public sealed class LibraryController : MonoBehaviour
    {
        public static LibraryController Instance { get; private set; }

        public event Action<string> EntryUnlocked;

        private readonly HashSet<string> unlockedEntryIds = new HashSet<string>();

        public IReadOnlyCollection<string> UnlockedEntryIds => unlockedEntryIds;
        public string LastUnlockedEntryId { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            Unlock("station_primer");
        }

        public bool IsUnlocked(LibraryEntryData entry)
        {
            return entry != null && IsUnlocked(entry.EntryId);
        }

        public bool IsUnlocked(string entryId)
        {
            return !string.IsNullOrWhiteSpace(entryId) &&
                   unlockedEntryIds.Contains(entryId);
        }

        public bool Unlock(string entryId)
        {
            if (string.IsNullOrWhiteSpace(entryId) || !unlockedEntryIds.Add(entryId))
                return false;

            EntryUnlocked?.Invoke(entryId);
            LastUnlockedEntryId = entryId;
            Debug.Log($"Library: entry unlocked '{entryId}'.", this);
            return true;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
