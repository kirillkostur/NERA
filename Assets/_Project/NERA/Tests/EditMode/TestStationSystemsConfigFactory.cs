using System;
using System.Collections.Generic;
using System.Reflection;
using NERA.Items;
using NERA.Station;
using UnityEngine;

namespace NERA.Tests
{
    internal static class TestStationSystemsConfigFactory
    {
        public static StationSystemsConfig CreateControllerConfig()
        {
            StationSystemsConfig config =
                ScriptableObject.CreateInstance<StationSystemsConfig>();
            config.name = "Test_StationSystemsConfig";

            SetField(
                config,
                "stationObjects",
                new List<StationSystemDefinition>
                {
                    CreateDefinition(
                        StationSystemType.Drone,
                        "station_drone",
                        new[]
                        {
                            CreateStat(StationObjectStat.TravelRange, 1f),
                            CreateStat(StationObjectStat.BatteryCharge, 100f),
                            CreateStat(StationObjectStat.EnergyConsumption, 4f),
                            CreateStat(
                                StationObjectStat.FlightEnergyConsumption,
                                4f)
                        }),
                    CreateDefinition(
                        StationSystemType.Antenna,
                        "station_antenna",
                        new[]
                        {
                            CreateStat(StationObjectStat.ScanRange, 1f),
                            CreateStat(
                                StationObjectStat.CalibrationEnergyConsumption,
                                5f),
                            CreateStat(
                                StationObjectStat.CalibrationDuration,
                                8f)
                        },
                        "Slot_1")
                });
            return config;
        }

        public static ItemData CreateEngineeringPart(
            string itemId,
            StationSystemType systemType,
            string objectId,
            string slotId,
            StationObjectStat stat,
            float value)
        {
            var modifier = new StationObjectStatModifierDefinition();
            SetField(modifier, "stat", stat);
            SetField(modifier, "mode", StationStatModifierMode.Add);
            SetField(modifier, "value", value);

            var compatibility = new EngineeringPartCompatibility();
            SetField(compatibility, "systemType", systemType);
            SetField(compatibility, "objectId", objectId);
            SetField(compatibility, "slotId", slotId);
            SetField(
                compatibility,
                "modifiers",
                new List<StationObjectStatModifierDefinition> { modifier });

            var definition = new EngineeringPartDefinition();
            SetField(
                definition,
                "compatibleInstallations",
                new List<EngineeringPartCompatibility> { compatibility });

            ItemData item = ScriptableObject.CreateInstance<ItemData>();
            item.name = $"Test_{itemId}";
            SetField(item, "itemId", itemId);
            SetField(item, "displayName", itemId);
            SetField(item, "itemType", ItemType.EngineeringPart);
            SetField(item, "engineeringPartDefinition", definition);
            return item;
        }

        public static ItemCatalogData CreateCatalog(params ItemData[] items)
        {
            ItemCatalogData catalog =
                ScriptableObject.CreateInstance<ItemCatalogData>();
            catalog.name = "Test_ItemCatalog";
            SetField(catalog, "items", new List<ItemData>(items));
            return catalog;
        }

        public static void AssignConfig(
            StationSystemsController systems,
            StationSystemsConfig config)
        {
            SetField(systems, "config", config);
            systems.ResetSystems();
        }

        public static void AssignCatalog(
            StationSystemsController systems,
            ItemCatalogData catalog)
        {
            SetField(systems, "itemCatalog", catalog);
        }

        public static void SetSingleton(Type controllerType, object value)
        {
            PropertyInfo instanceProperty = controllerType.GetProperty(
                "Instance",
                BindingFlags.Static | BindingFlags.Public);
            instanceProperty?.GetSetMethod(true)?.Invoke(null, new[] { value });
        }

        private static StationSystemDefinition CreateDefinition(
            StationSystemType type,
            string objectId,
            IEnumerable<StationObjectStatDefinition> stats,
            params string[] slotIds)
        {
            var definition = new StationSystemDefinition();
            SetField(definition, "systemType", type);
            SetField(definition, "objectId", objectId);
            SetField(definition, "displayName", $"Test {type}");
            SetField(definition, "controllable", true);
            SetField(definition, "initiallyActive", true);
            SetField(
                definition,
                "baseStats",
                new List<StationObjectStatDefinition>(stats));

            var slots = new List<StationObjectSlotDefinition>();
            foreach (string slotId in slotIds)
            {
                var slot = new StationObjectSlotDefinition();
                SetField(slot, "slotId", slotId);
                SetField(slot, "displayName", slotId);
                slots.Add(slot);
            }
            SetField(definition, "slots", slots);
            return definition;
        }

        private static StationObjectStatDefinition CreateStat(
            StationObjectStat stat,
            float value)
        {
            var definition = new StationObjectStatDefinition();
            SetField(definition, "stat", stat);
            SetField(definition, "displayName", stat.ToString());
            SetField(definition, "baseValue", value);
            return definition;
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
                throw new MissingFieldException(target.GetType().Name, name);
            field.SetValue(target, value);
        }
    }
}
