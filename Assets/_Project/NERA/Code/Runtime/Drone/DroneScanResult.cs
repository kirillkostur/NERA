using NERA.Expeditions;

namespace NERA.Drone
{
    public readonly struct DroneScanResult
    {
        public DroneScanResult(ExpeditionLocationData location, bool newlyDiscovered)
        {
            Location = location;
            NewlyDiscovered = newlyDiscovered;
        }

        public ExpeditionLocationData Location { get; }
        public bool NewlyDiscovered { get; }
    }
}
