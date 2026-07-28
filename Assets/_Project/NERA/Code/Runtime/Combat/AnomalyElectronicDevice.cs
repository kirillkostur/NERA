using System.Collections;
using UnityEngine;

namespace NERA.Combat
{
    [DisallowMultipleComponent]
    public sealed class AnomalyElectronicDevice :
        MonoBehaviour,
        IAnomalyElectronic
    {
        [SerializeField] private bool initiallyPowered = true;
        [SerializeField] private GameObject[] poweredObjects;
        [SerializeField] private Behaviour[] poweredBehaviours;

        private Coroutine restoreRoutine;
        private bool restoreState;

        public bool IsPowered { get; private set; }

        private void Awake()
        {
            SetPowered(initiallyPowered);
        }

        public void ApplyAnomalyPowerState(
            bool powered,
            float duration,
            GameObject _)
        {
            if (restoreRoutine == null)
                restoreState = IsPowered;
            else
                StopCoroutine(restoreRoutine);

            SetPowered(powered);
            if (duration > 0f)
                restoreRoutine = StartCoroutine(RestoreAfter(duration));
        }

        public void SetPowered(bool powered)
        {
            IsPowered = powered;
            if (poweredObjects != null)
            {
                foreach (GameObject target in poweredObjects)
                {
                    if (target != null)
                        target.SetActive(powered);
                }
            }

            if (poweredBehaviours == null)
                return;

            foreach (Behaviour target in poweredBehaviours)
            {
                if (target != null)
                    target.enabled = powered;
            }
        }

        private IEnumerator RestoreAfter(float duration)
        {
            yield return new WaitForSeconds(duration);
            SetPowered(restoreState);
            restoreRoutine = null;
        }

        private void OnDisable()
        {
            if (restoreRoutine == null)
                return;

            StopCoroutine(restoreRoutine);
            restoreRoutine = null;
        }
    }
}
