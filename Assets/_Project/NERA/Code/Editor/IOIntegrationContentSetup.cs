using System;
using System.Collections.Generic;
using NERA.Combat;
using NERA.Items;
using NERA.Library;
using NERA.Localization;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;
using Object = UnityEngine.Object;

namespace NERA.EditorTools
{
    public static class IOIntegrationContentSetup
    {
        private const string CombatRoot =
            "Assets/_Project/NERA/Configs/Combat";
        private const string ItemRoot =
            "Assets/_Project/NERA/Configs/Items";
        private const string LibraryRoot =
            "Assets/_Project/NERA/Configs/Library";

        private sealed class IntegrationSpec
        {
            public string ItemId;
            public string IntegrationId;
            public string AssetFile;
            public string EnglishName;
            public string RussianName;
            public Color Color;
            public AnomalyIntegrationEffect Effect;
            public float Radius;
            public float Damage;
            public float Duration;
            public float SynthesisDuration;
            public string LibraryTitleEnglish;
            public string LibraryTitleRussian;
            public string LibraryDescriptionEnglish;
            public string LibraryDescriptionRussian;
        }

        [MenuItem("NERA/Setup/Configure IO Weapon Integrations")]
        public static void Build()
        {
            BuildContent(true);
        }

        public static void BuildContent(bool selectLast)
        {
            EnsureFolder(CombatRoot);

            List<IntegrationSpec> specs = CreateSpecs();
            AnomalyIntegrationDefinition blue =
                AssetDatabase.LoadAssetAtPath<AnomalyIntegrationDefinition>(
                    CombatRoot + "/Integration_IOBlue_Discharge.asset");
            if (blue == null)
            {
                blue = ScriptableObject
                    .CreateInstance<AnomalyIntegrationDefinition>();
                AssetDatabase.CreateAsset(
                    blue,
                    CombatRoot + "/Integration_IOBlue_Discharge.asset");
            }

            List<Object> compatibleEquipment =
                ReadObjectArray(blue, "compatibleEquipment");
            AnomalyIntegrationDefinition last = null;

            foreach (IntegrationSpec spec in specs)
            {
                string path = CombatRoot + "/" + spec.AssetFile + ".asset";
                AnomalyIntegrationDefinition definition =
                    AssetDatabase.LoadAssetAtPath<
                        AnomalyIntegrationDefinition>(path);
                if (definition == null)
                {
                    definition = ScriptableObject
                        .CreateInstance<AnomalyIntegrationDefinition>();
                    AssetDatabase.CreateAsset(definition, path);
                }

                ConfigureDefinition(
                    definition,
                    spec,
                    compatibleEquipment);

                ItemData item = FindById<ItemData>(
                    ItemRoot,
                    "itemId",
                    spec.ItemId);
                if (item == null)
                {
                    Debug.LogError(
                        "Missing anomaly item for integration: " +
                        spec.ItemId);
                    continue;
                }

                SetObject(
                    item,
                    "anomalyIntegrationDefinition",
                    definition);

                LibraryEntryData library =
                    FindById<LibraryEntryData>(
                        LibraryRoot,
                        "entryId",
                        spec.ItemId);
                if (library != null)
                {
                    SetString(
                        library,
                        "title",
                        spec.LibraryTitleEnglish);
                    SetString(
                        library,
                        "description",
                        spec.LibraryDescriptionEnglish);
                }

                SetLocalized(
                    "integration." + spec.IntegrationId + ".name",
                    spec.EnglishName,
                    spec.RussianName);
                SetLocalized(
                    "library." + spec.ItemId + ".title",
                    spec.LibraryTitleEnglish,
                    spec.LibraryTitleRussian);
                SetLocalized(
                    "library." + spec.ItemId + ".description",
                    spec.LibraryDescriptionEnglish,
                    spec.LibraryDescriptionRussian);
                last = definition;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (selectLast && last != null)
                Selection.activeObject = last;

            Debug.Log(
                "Configured Blue, Green, Yellow, Red, and Violet IO " +
                "weapon integrations.");
        }

        private static List<IntegrationSpec> CreateSpecs()
        {
            return new List<IntegrationSpec>
            {
                new IntegrationSpec
                {
                    ItemId = "io_blue_shard_01",
                    IntegrationId = "io_blue_discharge",
                    AssetFile = "Integration_IOBlue_Discharge",
                    EnglishName = "Blue IO Charge",
                    RussianName = "Заряд Blue IO",
                    Color = new Color(0.1f, 0.55f, 1f),
                    Effect = AnomalyIntegrationEffect.EnableElectronics,
                    Radius = 8f,
                    Damage = 0f,
                    Duration = 8f,
                    SynthesisDuration = 5f,
                    LibraryTitleEnglish =
                        "BLUE IO // POWER SHARD",
                    LibraryTitleRussian =
                        "BLUE IO // ЭНЕРГЕТИЧЕСКИЙ ОСКОЛОК",
                    LibraryDescriptionEnglish =
                        "The Blue IO shard stores a stable power pulse. " +
                        "Integrated into a weapon, it temporarily powers " +
                        "nearby non-station devices within 8 m. Player " +
                        "station systems are intentionally ignored.",
                    LibraryDescriptionRussian =
                        "Осколок Blue IO хранит стабильный импульс питания. " +
                        "После интеграции в оружие он временно включает " +
                        "обычные приборы в радиусе 8 м, но намеренно " +
                        "игнорирует все системы станции игрока."
                },
                new IntegrationSpec
                {
                    ItemId = "io_green_node_02",
                    IntegrationId = "io_green_restoration",
                    AssetFile = "Integration_IOGreen_Restoration",
                    EnglishName = "Green IO Restoration",
                    RussianName = "Восстановление Green IO",
                    Color = new Color(0.16f, 0.95f, 0.35f),
                    Effect = AnomalyIntegrationEffect.RestoreFullHealth,
                    Radius = 8f,
                    Damage = 0f,
                    Duration = 0f,
                    SynthesisDuration = 8f,
                    LibraryTitleEnglish =
                        "GREEN IO // REPAIR NODE",
                    LibraryTitleRussian =
                        "ЗЕЛЁНЫЙ IO // РЕМОНТНЫЙ УЗЕЛ",
                    LibraryDescriptionEnglish =
                        "The integrated repair node reconstructs the " +
                        "operator's biometrics. Activating it restores the " +
                        "player's health to 100%.",
                    LibraryDescriptionRussian =
                        "Интегрированный ремонтный узел восстанавливает " +
                        "биометрию оператора. При активации здоровье игрока " +
                        "возвращается к 100%."
                },
                new IntegrationSpec
                {
                    ItemId = "io_yellow_lens_03",
                    IntegrationId = "io_yellow_scan",
                    AssetFile = "Integration_IOYellow_Scan",
                    EnglishName = "Yellow IO Scan",
                    RussianName = "Сканирование Yellow IO",
                    Color = new Color(1f, 0.76f, 0.08f),
                    Effect = AnomalyIntegrationEffect.RevealThroughWalls,
                    Radius = 10f,
                    Damage = 0f,
                    Duration = 6f,
                    SynthesisDuration = 12f,
                    LibraryTitleEnglish =
                        "YELLOW IO // HUNTER LENS",
                    LibraryTitleRussian =
                        "ЖЁЛТЫЙ IO // ЛИНЗА ОХОТНИКА",
                    LibraryDescriptionEnglish =
                        "The integrated hunter lens scans a 10 m area and " +
                        "marks enemies, devices, interactables, and items " +
                        "through walls for 6 seconds.",
                    LibraryDescriptionRussian =
                        "Интегрированная линза сканирует область 10 м и на " +
                        "6 секунд отмечает сквозь стены врагов, приборы, " +
                        "интерактивные объекты и предметы."
                },
                new IntegrationSpec
                {
                    ItemId = "io_red_core_04",
                    IntegrationId = "io_red_blast",
                    AssetFile = "Integration_IORed_Blast",
                    EnglishName = "Red IO Blast",
                    RussianName = "Взрыв Red IO",
                    Color = new Color(1f, 0.12f, 0.08f),
                    Effect = AnomalyIntegrationEffect.DamageAnomalies,
                    Radius = 8f,
                    Damage = 40f,
                    Duration = 0f,
                    SynthesisDuration = 18f,
                    LibraryTitleEnglish =
                        "RED IO // IMPACT CORE",
                    LibraryTitleRussian =
                        "КРАСНЫЙ IO // УДАРНОЕ ЯДРО",
                    LibraryDescriptionEnglish =
                        "The integrated impact core releases the former " +
                        "Blue-shard combat pulse: 40 damage to every IO " +
                        "enemy within 8 m. Red IO also casts a periodic " +
                        "power-disruption pulse against nearby devices.",
                    LibraryDescriptionRussian =
                        "Интегрированное ударное ядро создаёт прежний боевой " +
                        "импульс синего осколка: 40 урона всем врагам IO в " +
                        "радиусе 8 м. Сам Red IO периодически полностью " +
                        "обесточивает ближайшие приборы."
                },
                new IntegrationSpec
                {
                    ItemId = "io_violet_core_05",
                    IntegrationId = "io_violet_overload",
                    AssetFile = "Integration_IOViolet_Overload",
                    EnglishName = "Violet IO Overload",
                    RussianName = "Перегрузка Violet IO",
                    Color = new Color(0.72f, 0.22f, 1f),
                    Effect = AnomalyIntegrationEffect
                        .DisableElectronicsPermanently,
                    Radius = 12.5f,
                    Damage = 400f,
                    Duration = 0f,
                    SynthesisDuration = 25f,
                    LibraryTitleEnglish =
                        "VIOLET IO // COMMAND CORE",
                    LibraryTitleRussian =
                        "ФИОЛЕТОВЫЙ IO // КОМАНДНОЕ ЯДРО",
                    LibraryDescriptionEnglish =
                        "The integrated command core permanently deactivates " +
                        "electronics and player-station objects within " +
                        "12.5 m and deals 400 damage to every IO enemy. " +
                        "Violet IO also casts this power disruption periodically.",
                    LibraryDescriptionRussian =
                        "Интегрированное командное ядро полностью отключает " +
                        "электронику и объекты станции игрока в радиусе " +
                        "12,5 м и наносит 400 урона всем врагам IO. Сам Violet " +
                        "IO также периодически применяет такое обесточивание."
                }
            };
        }

        private static void ConfigureDefinition(
            AnomalyIntegrationDefinition definition,
            IntegrationSpec spec,
            IReadOnlyList<Object> compatibleEquipment)
        {
            SetString(definition, "integrationId", spec.IntegrationId);
            SetString(definition, "displayName", spec.EnglishName);
            SetColor(definition, "displayColor", spec.Color);
            SetFloat(
                definition,
                "synthesisDuration",
                spec.SynthesisDuration);
            SetInt(definition, "effect", (int)spec.Effect);
            SetFloat(definition, "radius", spec.Radius);
            SetFloat(definition, "anomalyDamage", spec.Damage);
            SetFloat(
                definition,
                "electronicDuration",
                spec.Duration);
            SetInt(definition, "affectedLayers", ~0);
            SetObjectArray(
                definition,
                "compatibleEquipment",
                compatibleEquipment);
        }

        private static T FindById<T>(
            string root,
            string propertyName,
            string expectedId) where T : Object
        {
            foreach (string guid in AssetDatabase.FindAssets(
                         "t:" + typeof(T).Name,
                         new[] { root }))
            {
                T asset = AssetDatabase.LoadAssetAtPath<T>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null)
                    continue;

                SerializedObject serialized =
                    new SerializedObject(asset);
                SerializedProperty id =
                    serialized.FindProperty(propertyName);
                if (id != null && id.stringValue == expectedId)
                    return asset;
            }

            return null;
        }

        private static List<Object> ReadObjectArray(
            Object target,
            string propertyName)
        {
            List<Object> result = new List<Object>();
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property =
                serialized.FindProperty(propertyName);
            if (property == null || !property.isArray)
                return result;

            for (int index = 0; index < property.arraySize; index++)
            {
                Object value = property.GetArrayElementAtIndex(index)
                    .objectReferenceValue;
                if (value != null)
                    result.Add(value);
            }

            return result;
        }

        private static void SetLocalized(
            string key,
            string english,
            string russian)
        {
            StringTableCollection collection =
                LocalizationEditorSettings.GetStringTableCollection(
                    NERALocalization.ContentTable);
            if (collection == null)
                throw new InvalidOperationException(
                    "Content localization table is missing.");

            SetTableEntry(
                collection.GetTable("en") as StringTable,
                key,
                english);
            SetTableEntry(
                collection.GetTable("ru") as StringTable,
                key,
                russian);
        }

        private static void SetTableEntry(
            StringTable table,
            string key,
            string value)
        {
            if (table == null)
                throw new InvalidOperationException(
                    "Locale table is missing for " + key);

            StringTableEntry entry = table.GetEntry(key);
            if (entry == null)
                table.AddEntry(key, value);
            else
                entry.Value = value;
            EditorUtility.SetDirty(table);
            EditorUtility.SetDirty(table.SharedData);
        }

        private static void SetString(
            Object target,
            string propertyName,
            string value)
        {
            SetProperty(
                target,
                propertyName,
                property => property.stringValue = value ?? string.Empty);
        }

        private static void SetFloat(
            Object target,
            string propertyName,
            float value)
        {
            SetProperty(
                target,
                propertyName,
                property => property.floatValue = value);
        }

        private static void SetInt(
            Object target,
            string propertyName,
            int value)
        {
            SetProperty(
                target,
                propertyName,
                property => property.intValue = value);
        }

        private static void SetColor(
            Object target,
            string propertyName,
            Color value)
        {
            SetProperty(
                target,
                propertyName,
                property => property.colorValue = value);
        }

        private static void SetObject(
            Object target,
            string propertyName,
            Object value)
        {
            SetProperty(
                target,
                propertyName,
                property => property.objectReferenceValue = value);
        }

        private static void SetObjectArray(
            Object target,
            string propertyName,
            IReadOnlyList<Object> values)
        {
            SetProperty(
                target,
                propertyName,
                property =>
                {
                    property.arraySize = values?.Count ?? 0;
                    for (int index = 0; index < property.arraySize; index++)
                    {
                        property.GetArrayElementAtIndex(index)
                            .objectReferenceValue = values[index];
                    }
                });
        }

        private static void SetProperty(
            Object target,
            string propertyName,
            Action<SerializedProperty> setter)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.Update();
            SerializedProperty property =
                serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    target.name + " has no serialized property " +
                    propertyName);
            }

            setter(property);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }
    }
}
