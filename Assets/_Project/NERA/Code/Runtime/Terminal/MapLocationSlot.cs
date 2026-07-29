using UnityEngine;

namespace NERA.Terminal
{
    [DisallowMultipleComponent]
    [AddComponentMenu("NERA/Terminal Map/Location Slot")]
    public sealed class MapLocationSlot : MonoBehaviour
    {
        [Tooltip("Assign the same MapSlotData asset in the location config.")]
        [SerializeField] private MapSlotData slot;
        [Tooltip("Optional anchor for runtime markers. Uses this transform when empty.")]
        [SerializeField] private Transform signalAnchor;

        public MapSlotData Slot => slot;
        public Transform SignalAnchor =>
            signalAnchor != null ? signalAnchor : transform;
    }
}
