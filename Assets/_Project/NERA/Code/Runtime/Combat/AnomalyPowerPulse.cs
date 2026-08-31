using System.Collections.Generic;
using NERA.Station;
using UnityEngine;

namespace NERA.Combat
{
    public static class AnomalyPowerPulse
    {
public static int ApplyTemporaryState(
            Vector3 center,
            float radius,
            LayerMask affectedLayers,
            bool powered,
            float duration,
            GameObject source)
        {
            Collider[] hits = Physics.OverlapSphere(
                center,
                Mathf.Max(0.1f, radius),
                affectedLayers,
                QueryTriggerInteraction.Collide);
            HashSet<IAnomalyElectronic> affected =
                new HashSet<IAnomalyElectronic>();

            foreach (Collider hit in hits)
            {
                if (hit == null)
                    continue;

                // Blue may power expedition electronics, but must never
                // activate an object belonging to the player's station.
                if (powered &&
                    hit.GetComponentInParent<StationObjectIdentity>() != null)
                {
                    continue;
                }

                MonoBehaviour[] behaviours =
                    hit.GetComponentsInParent<MonoBehaviour>(true);
                foreach (MonoBehaviour behaviour in behaviours)
                {
                    if (behaviour is IAnomalyElectronic electronic &&
                        affected.Add(electronic))
                    {
                        electronic.ApplyAnomalyPowerState(
                            powered,
                            Mathf.Max(0f, duration),
                            source);
                    }
                }
            }

            return affected.Count;
        }

public static int DisablePermanently(
            Vector3 center,
            float radius,
            LayerMask affectedLayers,
            GameObject source,
            string cause = "IO power disruption")
        {
            Collider[] hits = Physics.OverlapSphere(
                center,
                Mathf.Max(0.1f, radius),
                affectedLayers,
                QueryTriggerInteraction.Collide);
            HashSet<IAnomalyElectronic> electronics =
                new HashSet<IAnomalyElectronic>();
            HashSet<StationObjectIdentity> stationObjects =
                new HashSet<StationObjectIdentity>();

            foreach (Collider hit in hits)
            {
                if (hit == null)
                    continue;

                MonoBehaviour[] behaviours =
                    hit.GetComponentsInParent<MonoBehaviour>(true);
                foreach (MonoBehaviour behaviour in behaviours)
                {
                    if (behaviour is IAnomalyElectronic electronic)
                        electronics.Add(electronic);
                }

                StationObjectIdentity identity =
                    hit.GetComponentInParent<StationObjectIdentity>();
                if (identity != null)
                    stationObjects.Add(identity);
            }

            foreach (IAnomalyElectronic electronic in electronics)
            {
                electronic.ApplyAnomalyPowerState(
                    false,
                    0f,
                    source);
            }

            int disabledStationCount = 0;
            StationSystemsController systems =
                StationSystemsController.Instance;
            if (systems != null)
            {
                foreach (StationObjectIdentity identity in stationObjects)
                {
                    if (!systems.IsRequestedActive(
                            identity.SystemType,
                            identity.ObjectId))
                    {
                        continue;
                    }

                    if (systems.DisableFromFault(
                            identity.SystemType,
                            identity.ObjectId,
                            cause,
                            source))
                    {
                        disabledStationCount++;
                    }
                }
            }

            return electronics.Count + disabledStationCount;
        }
    }
}
