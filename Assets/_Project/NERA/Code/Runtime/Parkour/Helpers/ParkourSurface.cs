using System;
using UnityEngine;

namespace Climbing
{
    [Flags]
    public enum ParkourSurfaceType
    {
        None = 0,
        Vault = 1 << 0,
        Slide = 1 << 2,
        Reach = 1 << 3,
        Ledge = 1 << 4,
        Pole = 1 << 5,
        VaultOver = 1 << 6,
        Climb = 1 << 7,
    }

    /// <summary>
    /// Describes which parkour actions a collider supports. This replaces
    /// package-specific Unity tags and allows one surface to support several
    /// actions without changing the global tag list.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ParkourSurface : MonoBehaviour
    {
        [SerializeField] private ParkourSurfaceType surfaceTypes;

        public ParkourSurfaceType SurfaceTypes => surfaceTypes;

        public bool Supports(ParkourSurfaceType requiredType)
        {
            return requiredType != ParkourSurfaceType.None &&
                   (surfaceTypes & requiredType) == requiredType;
        }

        public void Configure(ParkourSurfaceType types)
        {
            surfaceTypes = types;
        }

        public static bool Supports(
            Component component,
            ParkourSurfaceType requiredType)
        {
            if (component == null || requiredType == ParkourSurfaceType.None)
                return false;

            ParkourSurface surface =
                component.GetComponentInParent<ParkourSurface>();
            return surface != null && surface.Supports(requiredType);
        }
    }
}
