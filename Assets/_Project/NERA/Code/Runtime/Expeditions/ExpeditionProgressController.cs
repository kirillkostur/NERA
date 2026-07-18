using System;
using UnityEngine;

namespace NERA.Expeditions
{
    public sealed class ExpeditionProgressController : MonoBehaviour
    {
        public static ExpeditionProgressController Instance { get; private set; }

        public event Action ProgressChanged;

        public bool Expedition01Visited { get; private set; }
        public bool AncientRecord01Found { get; private set; }
        public bool ResearchObject01Collected { get; private set; }
        public bool IOTrace01Seen { get; private set; }
        public bool Expedition01Returned { get; private set; }

        public string CurrentObjective
        {
            get
            {
                if (!AncientRecord01Found)
                    return "Find and read the ancient record.";
                if (!ResearchObject01Collected)
                    return "Inspect the NERA Memory Core.";
                if (!IOTrace01Seen)
                    return "Approach the weak Blue IO carefully.";
                if (!Expedition01Returned)
                    return "Return to the station.";
                return "Return to the terminal for analysis.";
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void MarkVisited()
        {
            if (Expedition01Visited)
                return;
            Expedition01Visited = true;
            NotifyChanged("Expedition 01 entered.");
        }

        public void MarkAncientRecordFound()
        {
            if (AncientRecord01Found)
                return;
            AncientRecord01Found = true;
            NotifyChanged("Ancient record recovered.");
        }

        public void MarkResearchObjectCollected()
        {
            if (ResearchObject01Collected)
                return;
            ResearchObject01Collected = true;
            NotifyChanged("NERA Memory Core secured.");
        }

        public void MarkIOTraceSeen()
        {
            if (IOTrace01Seen)
                return;
            IOTrace01Seen = true;
            NotifyChanged("Weak Blue IO encountered.");
        }

        public void MarkReturned()
        {
            if (Expedition01Returned)
                return;
            Expedition01Returned = true;
            NotifyChanged("Returned to station.");
        }

        private void NotifyChanged(string message)
        {
            ProgressChanged?.Invoke();
            Debug.Log($"ExpeditionProgress: {message} Objective: {CurrentObjective}", this);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
