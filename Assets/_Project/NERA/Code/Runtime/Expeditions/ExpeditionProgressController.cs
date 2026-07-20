using System;
using UnityEngine;

namespace NERA.Expeditions
{
    public sealed class ExpeditionProgressController : MonoBehaviour
    {
        public static ExpeditionProgressController Instance { get; private set; }

        public event Action ProgressChanged;

        public bool Expedition01Visited { get; private set; }
        public bool IOTrace01Seen { get; private set; }
        public bool ResearchSampleCollected { get; private set; }
        public bool Expedition01Returned { get; private set; }

        public string CurrentObjective
        {
            get
            {
                if (!IOTrace01Seen)
                    return "Find the weak Blue IO anomaly.";
                if (!ResearchSampleCollected)
                    return "Collect an anomaly sample.";
                if (!Expedition01Returned)
                    return "Return to the station.";
                return "Analyze the sample in the laboratory.";
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
        }

        public void MarkVisited()
        {
            if (Expedition01Visited)
                return;
            Expedition01Visited = true;
            NotifyChanged("Expedition 01 entered.");
        }

        public void MarkIOTraceSeen()
        {
            if (IOTrace01Seen)
                return;
            IOTrace01Seen = true;
            NotifyChanged("Weak Blue IO encountered.");
        }

        public void MarkResearchSampleCollected(string sampleName)
        {
            if (ResearchSampleCollected)
                return;
            ResearchSampleCollected = true;
            NotifyChanged($"{sampleName} collected.");
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
