using System;
using System.Collections.Generic;
using UnityEngine;

namespace NERA.Items
{
    [CreateAssetMenu(
        fileName = "ItemCatalog_Default",
        menuName = "NERA/Items/Item Catalog"
    )]
    public sealed class ItemCatalogData : ScriptableObject
    {
        [SerializeField] private List<ItemData> items = new List<ItemData>();

        public IReadOnlyList<ItemData> Items => items;

        public ItemData Find(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return null;

            string canonicalItemId = ResolveLegacyEngineeringPartId(itemId);

            foreach (ItemData item in items)
            {
                if (item != null && string.Equals(
                        item.ItemId,
                        canonicalItemId,
                        StringComparison.Ordinal))
                {
                    return item;
                }
            }

            return null;
        }

        private static string ResolveLegacyEngineeringPartId(string itemId)
        {
            string trimmedItemId = itemId.Trim();
            switch (trimmedItemId)
            {
                case "advanced_stabilizer_01":
                case "antenna_array_01":
                case "calibration_module_01":
                case "capacitor_01":
                case "chassis_01":
                case "cooling_01":
                case "cooling_system_01":
                case "emitter_damage_01":
                case "energy_cells_01":
                case "power_bus_01":
                case "power_controller_01":
                case "power_converter_01":
                case "power_core_01":
                case "propulsion_01":
                case "sensor_01":
                case "sensor_array_01":
                case "servo_01":
                case "servo_drive_01":
                case "signal_amplifier_01":
                case "signal_processor_01":
                case "solar_cells_01":
                case "solar_dust_repeller_01":
                case "solar_mppt_controller_01":
                case "solar_tracker_01":
                case "voltage_regulator_01":
                    return $"item_{trimmedItemId}";
                default:
                    return trimmedItemId;
            }
        }
    }
}
