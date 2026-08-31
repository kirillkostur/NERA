using System.Collections.Generic;
using NERA.Enemies;
using NERA.Items;
using NERA.Library;
using NERA.Localization;
using NERA.Research;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace NERA.EditorTools
{
    public static class IOArchetypeContentSetup
    {
        private const string ConfigRoot =
            "Assets/_Project/NERA/Configs/IO";
        private const string ItemRoot =
            "Assets/_Project/NERA/Configs/Items/Item_Anomaly";
        private const string ResearchRoot =
            "Assets/_Project/NERA/Configs/Research";
        private const string LibraryRoot =
            "Assets/_Project/NERA/Configs/Library";
        private const string EnemyPrefabRoot =
            "Assets/_Project/NERA/Prefabs/IO";
        private const string ItemPrefabRoot =
            "Assets/_Project/NERA/Prefabs/Items/Item_Anomaly";
        private const string MaterialRoot =
            "Assets/_Project/NERA/Materials/IO";
        private const string BlueConfigPath =
            ConfigRoot + "/CFG_IO_Blue_Weak.asset";
        private const string BluePrefabPath =
            EnemyPrefabRoot + "/IO_Blue_Weak.prefab";

        private sealed class ArchetypeSpec
        {
            public string ColorName;
            public string RoleName;
            public string EnemyId;
            public string EnemyEnglish;
            public string EnemyRussian;
            public string ConfigFile;
            public string EnemyPrefabFile;
            public string ItemId;
            public string ItemEnglish;
            public string ItemRussian;
            public string ItemDescriptionEnglish;
            public string ItemDescriptionRussian;
            public string ItemFile;
            public string ItemPrefabFile;
            public string ResearchId;
            public string ResearchFile;
            public string LibraryFile;
            public string LibraryTitleEnglish;
            public string LibraryTitleRussian;
            public string LibraryDescriptionEnglish;
            public string LibraryDescriptionRussian;
            public Color Color;
            public float MaxHealth;
            public float DetectionRadius;
            public float AttackRange;
            public float MoveSpeed;
            public float AttackCooldown;
            public float ProjectileSpeed;
            public float ProjectileDamage;
            public float ProjectileScale;
            public float AnalysisDuration;
            public float ColliderRadius;
            public PrimitiveType DropPrimitive;

            public IOEnemyConfig Config;
            public ItemData Item;
            public ResearchDefinition Research;
            public LibraryEntryData Library;
            public GameObject DropPrefab;
            public GameObject EnemyPrefab;
            public Material Material;
        }

        [MenuItem("NERA/Setup/Create IO Archetypes")]
        public static void Build()
        {
            EnsureFolder(ConfigRoot);
            EnsureFolder(ItemRoot);
            EnsureFolder(ResearchRoot);
            EnsureFolder(LibraryRoot);
            EnsureFolder(EnemyPrefabRoot);
            EnsureFolder(ItemPrefabRoot);
            EnsureFolder(MaterialRoot);

            IOEnemyConfig blueConfig =
                AssetDatabase.LoadAssetAtPath<IOEnemyConfig>(BlueConfigPath);
            GameObject bluePrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(BluePrefabPath);
            if (blueConfig == null || bluePrefab == null)
            {
                Debug.LogError(
                    "IO archetype setup requires the existing Blue Weak config and prefab.");
                return;
            }

            SerializedObject blueSerialized = new SerializedObject(blueConfig);
            GameObject projectilePrefab = blueSerialized
                .FindProperty("projectilePrefab")
                ?.objectReferenceValue as GameObject;
            Shader shader = bluePrefab.GetComponentInChildren<Renderer>(true)
                ?.sharedMaterial?.shader;
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            if (shader == null)
            {
                Debug.LogError("No compatible shader was found for IO materials.");
                return;
            }

            List<ArchetypeSpec> specs = CreateSpecs();
            foreach (ArchetypeSpec spec in specs)
            {
                spec.Material = CreateOrUpdateMaterial(spec, shader);
                BuildDataAssets(spec);
                spec.DropPrefab = BuildWorldItemPrefab(spec);
                ConfigureItemWorldPrefab(spec);
                spec.Config = BuildEnemyConfig(
                    spec,
                    projectilePrefab,
                    spec.DropPrefab);
            }

            GameObject yellowPrefab = null;
            foreach (ArchetypeSpec spec in specs)
            {
                GameObject[] reinforcements = spec.EnemyId ==
                    "io_violet_overseer"
                    ? new[] { bluePrefab, yellowPrefab }
                    : null;
                spec.EnemyPrefab = BuildEnemyPrefab(
                    spec,
                    reinforcements);
                if (spec.EnemyId == "io_yellow_hunter")
                    yellowPrefab = spec.EnemyPrefab;
            }

            WriteLocalization(specs);
            IOIntegrationContentSetup.BuildContent(false);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    EnemyPrefabRoot + "/IO_Violet_Overseer.prefab");
            Debug.Log(
                "Created four IO archetypes with drops, research, library entries, localization, and unique ability prefabs.");
        }

        private static List<ArchetypeSpec> CreateSpecs()
        {
            return new List<ArchetypeSpec>
            {
                new ArchetypeSpec
                {
                    ColorName = "Green",
                    RoleName = "Regenerator",
                    EnemyId = "io_green_regenerator",
                    EnemyEnglish = "Green IO Regenerator",
                    EnemyRussian = "Зелёный IO-регенератор",
                    ConfigFile = "CFG_IO_Green_Regenerator",
                    EnemyPrefabFile = "IO_Green_Regenerator",
                    ItemId = "io_green_node_02",
                    ItemEnglish = "Green Repair Node",
                    ItemRussian = "Зелёный ремонтный узел",
                    ItemDescriptionEnglish =
                        "A self-repair node recovered from a Green IO.",
                    ItemDescriptionRussian =
                        "Узел саморемонта, извлечённый из зелёного IO.",
                    ItemFile = "Item_IOGreenRepairNode_02",
                    ItemPrefabFile = "P_WorldItem_IOGreenRepairNode",
                    ResearchId = "research_io_green_node_02",
                    ResearchFile = "Research_IOGreenNode_02",
                    LibraryFile = "Library_IOGreenNode_02",
                    LibraryTitleEnglish =
                        "GREEN IO // REPAIR NODE",
                    LibraryTitleRussian =
                        "ЗЕЛЁНЫЙ IO // РЕМОНТНЫЙ УЗЕЛ",
                    LibraryDescriptionEnglish =
                        "The Green IO node stores a repeating repair pattern. It restores nearby IO structures after a short energy pulse.",
                    LibraryDescriptionRussian =
                        "Узел зелёного IO хранит повторяющийся шаблон ремонта. После короткого энергетического импульса он восстанавливает ближайшие структуры IO.",
                    Color = new Color(0.16f, 0.95f, 0.35f),
                    MaxHealth = 80f,
                    DetectionRadius = 12f,
                    AttackRange = 7f,
                    MoveSpeed = 1.8f,
                    AttackCooldown = 2.4f,
                    ProjectileSpeed = 8.5f,
                    ProjectileDamage = 8f,
                    ProjectileScale = 0.24f,
                    AnalysisDuration = 8f,
                    ColliderRadius = 0.7f,
                    DropPrimitive = PrimitiveType.Cube
                },
                new ArchetypeSpec
                {
                    ColorName = "Yellow",
                    RoleName = "Hunter",
                    EnemyId = "io_yellow_hunter",
                    EnemyEnglish = "Yellow IO Hunter",
                    EnemyRussian = "Жёлтый IO-охотник",
                    ConfigFile = "CFG_IO_Yellow_Hunter",
                    EnemyPrefabFile = "IO_Yellow_Hunter",
                    ItemId = "io_yellow_lens_03",
                    ItemEnglish = "Yellow Hunter Lens",
                    ItemRussian = "Жёлтая линза охотника",
                    ItemDescriptionEnglish =
                        "A tracking lens recovered from a Yellow IO.",
                    ItemDescriptionRussian =
                        "Линза наведения, извлечённая из жёлтого IO.",
                    ItemFile = "Item_IOYellowHunterLens_03",
                    ItemPrefabFile = "P_WorldItem_IOYellowHunterLens",
                    ResearchId = "research_io_yellow_lens_03",
                    ResearchFile = "Research_IOYellowLens_03",
                    LibraryFile = "Library_IOYellowLens_03",
                    LibraryTitleEnglish =
                        "YELLOW IO // HUNTER LENS",
                    LibraryTitleRussian =
                        "ЖЁЛТЫЙ IO // ЛИНЗА ОХОТНИКА",
                    LibraryDescriptionEnglish =
                        "The lens predicts lateral movement and coordinates rapid projectile bursts. Yellow IO uses it to dash before firing.",
                    LibraryDescriptionRussian =
                        "Линза прогнозирует боковое движение и координирует быстрые очереди. Жёлтый IO использует её для рывка перед стрельбой.",
                    Color = new Color(1f, 0.76f, 0.08f),
                    MaxHealth = 130f,
                    DetectionRadius = 16f,
                    AttackRange = 9f,
                    MoveSpeed = 3.8f,
                    AttackCooldown = 2.5f,
                    ProjectileSpeed = 13f,
                    ProjectileDamage = 7f,
                    ProjectileScale = 0.18f,
                    AnalysisDuration = 12f,
                    ColliderRadius = 0.65f,
                    DropPrimitive = PrimitiveType.Cylinder
                },
                new ArchetypeSpec
                {
                    ColorName = "Red",
                    RoleName = "Enforcer",
                    EnemyId = "io_red_enforcer",
                    EnemyEnglish = "Red IO Enforcer",
                    EnemyRussian = "Красный IO-силовик",
                    ConfigFile = "CFG_IO_Red_Enforcer",
                    EnemyPrefabFile = "IO_Red_Enforcer",
                    ItemId = "io_red_core_04",
                    ItemEnglish = "Red Impact Core",
                    ItemRussian = "Красное ударное ядро",
                    ItemDescriptionEnglish =
                        "A dense impact core recovered from a Red IO.",
                    ItemDescriptionRussian =
                        "Плотное ударное ядро, извлечённое из красного IO.",
                    ItemFile = "Item_IORedImpactCore_04",
                    ItemPrefabFile = "P_WorldItem_IORedImpactCore",
                    ResearchId = "research_io_red_core_04",
                    ResearchFile = "Research_IORedCore_04",
                    LibraryFile = "Library_IORedCore_04",
                    LibraryTitleEnglish =
                        "RED IO // IMPACT CORE",
                    LibraryTitleRussian =
                        "КРАСНЫЙ IO // УДАРНОЕ ЯДРО",
                    LibraryDescriptionEnglish =
                        "The core compresses energy into a slow projectile that releases a destructive blast on impact.",
                    LibraryDescriptionRussian =
                        "Ядро сжимает энергию в медленный снаряд, который при столкновении создаёт разрушительный взрыв.",
                    Color = new Color(1f, 0.12f, 0.08f),
                    MaxHealth = 220f,
                    DetectionRadius = 14f,
                    AttackRange = 10f,
                    MoveSpeed = 1.4f,
                    AttackCooldown = 3.2f,
                    ProjectileSpeed = 6.5f,
                    ProjectileDamage = 25f,
                    ProjectileScale = 0.42f,
                    AnalysisDuration = 18f,
                    ColliderRadius = 0.95f,
                    DropPrimitive = PrimitiveType.Sphere
                },
                new ArchetypeSpec
                {
                    ColorName = "Violet",
                    RoleName = "Overseer",
                    EnemyId = "io_violet_overseer",
                    EnemyEnglish = "Violet IO Overseer",
                    EnemyRussian = "Фиолетовый IO-надзиратель",
                    ConfigFile = "CFG_IO_Violet_Overseer",
                    EnemyPrefabFile = "IO_Violet_Overseer",
                    ItemId = "io_violet_core_05",
                    ItemEnglish = "Violet Command Core",
                    ItemRussian = "Фиолетовое командное ядро",
                    ItemDescriptionEnglish =
                        "A command core recovered from a Violet IO.",
                    ItemDescriptionRussian =
                        "Командное ядро, извлечённое из фиолетового IO.",
                    ItemFile = "Item_IOVioletCommandCore_05",
                    ItemPrefabFile = "P_WorldItem_IOVioletCommandCore",
                    ResearchId = "research_io_violet_core_05",
                    ResearchFile = "Research_IOVioletCore_05",
                    LibraryFile = "Library_IOVioletCore_05",
                    LibraryTitleEnglish =
                        "VIOLET IO // COMMAND CORE",
                    LibraryTitleRussian =
                        "ФИОЛЕТОВЫЙ IO // КОМАНДНОЕ ЯДРО",
                    LibraryDescriptionEnglish =
                        "The command core broadcasts reinforcement patterns at critical integrity thresholds. Summoned IO do not retain stable anomaly cores.",
                    LibraryDescriptionRussian =
                        "Командное ядро передаёт шаблоны подкрепления при критических порогах целостности. Призванные IO не сохраняют стабильные ядра аномалии.",
                    Color = new Color(0.72f, 0.22f, 1f),
                    MaxHealth = 400f,
                    DetectionRadius = 18f,
                    AttackRange = 12f,
                    MoveSpeed = 1.2f,
                    AttackCooldown = 1.7f,
                    ProjectileSpeed = 10f,
                    ProjectileDamage = 18f,
                    ProjectileScale = 0.32f,
                    AnalysisDuration = 25f,
                    ColliderRadius = 1.15f,
                    DropPrimitive = PrimitiveType.Sphere
                }
            };
        }

        private static void BuildDataAssets(ArchetypeSpec spec)
        {
            string libraryPath =
                LibraryRoot + "/" + spec.LibraryFile + ".asset";
            spec.Library = CreateOrLoad<LibraryEntryData>(libraryPath);
            SetString(spec.Library, "entryId", spec.ItemId);
            SetString(spec.Library, "title", spec.LibraryTitleEnglish);
            SetInt(spec.Library, "category", (int)LibraryCategory.Anomaly);
            SetString(
                spec.Library,
                "description",
                spec.LibraryDescriptionEnglish);

            string researchPath =
                ResearchRoot + "/" + spec.ResearchFile + ".asset";
            spec.Research = CreateOrLoad<ResearchDefinition>(researchPath);
            SetString(spec.Research, "researchId", spec.ResearchId);
            SetString(spec.Research, "displayName", spec.ItemEnglish);
            SetFloat(
                spec.Research,
                "analysisDuration",
                spec.AnalysisDuration);
            SetInt(
                spec.Research,
                "itemFate",
                (int)ResearchItemFate.Return);
            SetObject(spec.Research, "unlockedEntry", spec.Library);

            string itemPath =
                ItemRoot + "/" + spec.ItemFile + ".asset";
            spec.Item = CreateOrLoad<ItemData>(itemPath);
            SetString(spec.Item, "itemId", spec.ItemId);
            SetString(spec.Item, "displayName", spec.ItemEnglish);
            SetString(
                spec.Item,
                "description",
                spec.ItemDescriptionEnglish);
            SetInt(spec.Item, "itemType", (int)ItemType.Anomaly);
            SetObject(
                spec.Item,
                "researchDefinition",
                spec.Research);
            SetBool(spec.Item, "acceptsAnomalyIntegration", false);
        }

        private static IOEnemyConfig BuildEnemyConfig(
            ArchetypeSpec spec,
            GameObject projectilePrefab,
            GameObject dropPrefab)
        {
            string path =
                ConfigRoot + "/" + spec.ConfigFile + ".asset";
            IOEnemyConfig config = CreateOrLoad<IOEnemyConfig>(path);
            SetString(config, "enemyId", spec.EnemyId);
            SetString(config, "displayName", spec.EnemyEnglish);
            SetFloat(config, "maxHealth", spec.MaxHealth);
            SetFloat(
                config,
                "detectionRadius",
                spec.DetectionRadius);
            SetFloat(config, "attackRange", spec.AttackRange);
            SetFloat(config, "moveSpeed", spec.MoveSpeed);
            SetFloat(config, "hoverHeight", 1.6f);
            SetFloat(config, "hoverAmplitude", 0.15f);
            SetFloat(config, "hoverFrequency", 2f);
            SetFloat(
                config,
                "attackCooldown",
                spec.AttackCooldown);
            SetFloat(
                config,
                "projectileSpeed",
                spec.ProjectileSpeed);
            SetFloat(config, "projectileLifetime", 5f);
            SetFloat(
                config,
                "projectileDamage",
                spec.ProjectileDamage);
            SetFloat(
                config,
                "projectileScale",
                spec.ProjectileScale);
            SetObject(config, "projectilePrefab", projectilePrefab);
            SetColor(config, "energyColor", spec.Color);
            SetFloat(config, "emissionIntensity", 3.2f);
            SetFloat(
                config,
                "projectileEmissionIntensity",
                5f);
            SetObject(config, "deathDropPrefab", dropPrefab);
            SetVector3(
                config,
                "deathDropOffset",
                new Vector3(0f, 0.35f, 0f));
            return config;
        }

        private static GameObject BuildWorldItemPrefab(
            ArchetypeSpec spec)
        {
            GameObject root =
                GameObject.CreatePrimitive(spec.DropPrimitive);
            root.name = spec.ItemPrefabFile;
            root.transform.localScale =
                spec.DropPrimitive == PrimitiveType.Cylinder
                    ? new Vector3(0.45f, 0.15f, 0.45f)
                    : Vector3.one * 0.42f;

            Renderer renderer = root.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = spec.Material;

            WorldItem worldItem = root.AddComponent<WorldItem>();
            SetObject(worldItem, "itemData", spec.Item);
            SetBool(worldItem, "destroyAfterPickup", true);
            SetString(worldItem, "persistentId", string.Empty);
            SetBool(worldItem, "trackWorldState", false);
            SetFloat(worldItem, "settleDelay", 2f);

            Rigidbody body = root.GetComponent<Rigidbody>();
            if (body == null)
                body = root.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.isKinematic = true;
            body.mass = 0.35f;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            string path =
                ItemPrefabRoot + "/" + spec.ItemPrefabFile + ".prefab";
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
        }

        private static void ConfigureItemWorldPrefab(
            ArchetypeSpec spec)
        {
            if (spec.DropPrefab == null)
                return;

            SetObject(
                spec.Item,
                "worldPrefab",
                spec.DropPrefab.GetComponent<WorldItem>());
        }

        private static GameObject BuildEnemyPrefab(
            ArchetypeSpec spec,
            GameObject[] reinforcements)
        {
            GameObject root = new GameObject(spec.EnemyPrefabFile);
            SphereCollider collider = root.AddComponent<SphereCollider>();
            collider.radius = spec.ColliderRadius;

            IOEnemyController controller =
                root.AddComponent<IOEnemyController>();
            SetObject(controller, "config", spec.Config);
            SetString(controller, "persistentId", string.Empty);

            switch (spec.EnemyId)
            {
                case "io_green_regenerator":
                    BuildGreenVisual(root, spec.Material);
                    break;
                case "io_yellow_hunter":
                    BuildYellowVisual(root, spec.Material);
                    root.AddComponent<IOHunterBurstAbility>();
                    break;
                case "io_red_enforcer":
                    BuildRedVisual(root, spec.Material);
                    root.AddComponent<IOExplosiveShotAbility>();
                    ConfigurePowerDisruption(
                        root.AddComponent<IOPowerDisruptionAbility>(),
                        4f,
                        14f,
                        8f,
                        spec.Color);
                    break;
                case "io_violet_overseer":
                    BuildVioletVisual(root, spec.Material);
                    IOOverseerSummonAbility summon =
                        root.AddComponent<IOOverseerSummonAbility>();
                    SetObjectArray(
                        summon,
                        "reinforcementPrefabs",
                        reinforcements);
                    SetFloatArray(
                        summon,
                        "healthThresholds",
                        new[] { 0.66f, 0.33f });
                    ConfigurePowerDisruption(
                        root.AddComponent<IOPowerDisruptionAbility>(),
                        3f,
                        10f,
                        12.5f,
                        spec.Color);
                    break;
            }

            string path =
                EnemyPrefabRoot + "/" + spec.EnemyPrefabFile + ".prefab";
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
        }

        private static void BuildGreenVisual(
            GameObject root,
            Material material)
        {
            CreatePrimitiveChild(
                root.transform,
                "Core",
                PrimitiveType.Sphere,
                Vector3.zero,
                Vector3.one * 1.05f,
                Vector3.zero,
                material);

            GameObject ringA = CreateRing(
                root.transform,
                "RepairRing_A",
                0.85f,
                10,
                material);
            ringA.transform.localEulerAngles =
                new Vector3(28f, 0f, 0f);
            ConfigureRotation(
                ringA.AddComponent<IORotatingVisual>(),
                Vector3.up,
                55f);

            GameObject ringB = CreateRing(
                root.transform,
                "RepairRing_B",
                0.62f,
                8,
                material);
            ringB.transform.localEulerAngles =
                new Vector3(0f, 0f, 72f);
            ConfigureRotation(
                ringB.AddComponent<IORotatingVisual>(),
                Vector3.forward,
                -80f);

            GameObject pulse = CreateRing(
                root.transform,
                "RepairPulse",
                1.15f,
                14,
                material);
            pulse.transform.localEulerAngles =
                new Vector3(90f, 0f, 0f);
            pulse.SetActive(false);

            IORegenerationPulseAbility ability =
                root.AddComponent<IORegenerationPulseAbility>();
            SetObject(ability, "pulseVisual", pulse.transform);
        }

        private static void BuildYellowVisual(
            GameObject root,
            Material material)
        {
            CreatePrimitiveChild(
                root.transform,
                "Core",
                PrimitiveType.Sphere,
                Vector3.zero,
                Vector3.one * 0.82f,
                Vector3.zero,
                material);
            CreatePrimitiveChild(
                root.transform,
                "LeftBlade",
                PrimitiveType.Cube,
                new Vector3(-0.68f, 0f, 0f),
                new Vector3(0.75f, 0.12f, 0.28f),
                new Vector3(0f, 25f, 35f),
                material);
            CreatePrimitiveChild(
                root.transform,
                "RightBlade",
                PrimitiveType.Cube,
                new Vector3(0.68f, 0f, 0f),
                new Vector3(0.75f, 0.12f, 0.28f),
                new Vector3(0f, -25f, -35f),
                material);
            for (int index = 0; index < 3; index++)
            {
                CreatePrimitiveChild(
                    root.transform,
                    "Trail_" + index,
                    PrimitiveType.Cube,
                    new Vector3(0f, 0f, -0.55f - index * 0.28f),
                    new Vector3(
                        0.12f,
                        0.12f,
                        0.35f - index * 0.07f),
                    Vector3.zero,
                    material);
            }
        }

        private static void BuildRedVisual(
            GameObject root,
            Material material)
        {
            CreatePrimitiveChild(
                root.transform,
                "Core",
                PrimitiveType.Sphere,
                Vector3.zero,
                Vector3.one * 1.2f,
                Vector3.zero,
                material);
            Vector3[] armorPositions =
            {
                Vector3.right * 0.9f,
                Vector3.left * 0.9f,
                Vector3.forward * 0.9f,
                Vector3.back * 0.9f
            };
            for (int index = 0; index < armorPositions.Length; index++)
            {
                Vector3 position = armorPositions[index];
                CreatePrimitiveChild(
                    root.transform,
                    "Armor_" + index,
                    PrimitiveType.Cube,
                    position,
                    new Vector3(0.65f, 0.75f, 0.25f),
                    new Vector3(
                        0f,
                        Mathf.Atan2(position.x, position.z) *
                        Mathf.Rad2Deg,
                        0f),
                    material);
            }
        }

        private static void BuildVioletVisual(
            GameObject root,
            Material material)
        {
            CreatePrimitiveChild(
                root.transform,
                "Core",
                PrimitiveType.Sphere,
                Vector3.zero,
                Vector3.one * 1.42f,
                Vector3.zero,
                material);

            GameObject orbit = new GameObject("CommandOrbit");
            orbit.transform.SetParent(root.transform, false);
            for (int index = 0; index < 3; index++)
            {
                float angle = index * Mathf.PI * 2f / 3f;
                CreatePrimitiveChild(
                    orbit.transform,
                    "CommandNode_" + index,
                    PrimitiveType.Sphere,
                    new Vector3(
                        Mathf.Cos(angle) * 1.2f,
                        index == 1 ? 0.25f : -0.1f,
                        Mathf.Sin(angle) * 1.2f),
                    Vector3.one * 0.32f,
                    Vector3.zero,
                    material);
            }

            ConfigureRotation(
                orbit.AddComponent<IORotatingVisual>(),
                Vector3.up,
                42f);
        }

        private static GameObject CreateRing(
            Transform parent,
            string name,
            float radius,
            int segmentCount,
            Material material)
        {
            GameObject ring = new GameObject(name);
            ring.transform.SetParent(parent, false);
            for (int index = 0; index < segmentCount; index++)
            {
                float angle = index * Mathf.PI * 2f / segmentCount;
                Vector3 position = new Vector3(
                    Mathf.Cos(angle) * radius,
                    0f,
                    Mathf.Sin(angle) * radius);
                CreatePrimitiveChild(
                    ring.transform,
                    "Segment_" + index,
                    PrimitiveType.Cube,
                    position,
                    new Vector3(0.2f, 0.08f, 0.08f),
                    new Vector3(
                        0f,
                        -angle * Mathf.Rad2Deg,
                        0f),
                    material);
            }

            return ring;
        }

        private static GameObject CreatePrimitiveChild(
            Transform parent,
            string name,
            PrimitiveType primitive,
            Vector3 localPosition,
            Vector3 localScale,
            Vector3 localEulerAngles,
            Material material)
        {
            GameObject child = GameObject.CreatePrimitive(primitive);
            child.name = name;
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            child.transform.localScale = localScale;
            child.transform.localEulerAngles = localEulerAngles;
            Collider childCollider = child.GetComponent<Collider>();
            if (childCollider != null)
                Object.DestroyImmediate(childCollider);
            Renderer renderer = child.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;
            return child;
        }

        private static void ConfigureRotation(
            IORotatingVisual visual,
            Vector3 axis,
            float speed)
        {
            SetVector3(visual, "rotationAxis", axis);
            SetFloat(visual, "degreesPerSecond", speed);
        }

private static void ConfigurePowerDisruption(
            IOPowerDisruptionAbility ability,
            float initialDelay,
            float cooldown,
            float radius,
            Color color)
        {
            SetFloat(ability, "initialDelay", initialDelay);
            SetFloat(ability, "cooldown", cooldown);
            SetFloat(ability, "radius", radius);
            SetInt(ability, "affectedLayers", ~0);
            SetColor(ability, "pulseColor", color);
        }


        private static Material CreateOrUpdateMaterial(
            ArchetypeSpec spec,
            Shader shader)
        {
            string path =
                MaterialRoot + "/M_IO_" + spec.ColorName + ".mat";
            Material material =
                AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            material.color = spec.Color;
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", spec.Color);
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor(
                    "_EmissionColor",
                    spec.Color * 3f);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void WriteLocalization(
            IEnumerable<ArchetypeSpec> specs)
        {
            foreach (ArchetypeSpec spec in specs)
            {
                SetLocalized(
                    "enemy." + spec.EnemyId + ".name",
                    spec.EnemyEnglish,
                    spec.EnemyRussian);
                SetLocalized(
                    "item." + spec.ItemId + ".name",
                    spec.ItemEnglish,
                    spec.ItemRussian);
                SetLocalized(
                    "item." + spec.ItemId + ".description",
                    spec.ItemDescriptionEnglish,
                    spec.ItemDescriptionRussian);
                SetLocalized(
                    "research." + spec.ResearchId + ".name",
                    spec.ItemEnglish,
                    spec.ItemRussian);
                SetLocalized(
                    "library." + spec.ItemId + ".title",
                    spec.LibraryTitleEnglish,
                    spec.LibraryTitleRussian);
                SetLocalized(
                    "library." + spec.ItemId + ".description",
                    spec.LibraryDescriptionEnglish,
                    spec.LibraryDescriptionRussian);
            }
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
                throw new System.InvalidOperationException(
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
                throw new System.InvalidOperationException(
                    "Locale table is missing for " + key);

            StringTableEntry entry = table.GetEntry(key);
            if (entry == null)
                table.AddEntry(key, value);
            else
                entry.Value = value;
            EditorUtility.SetDirty(table);
            EditorUtility.SetDirty(table.SharedData);
        }

        private static T CreateOrLoad<T>(string path)
            where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void SetString(
            Object target,
            string property,
            string value)
        {
            SerializedProperty serialized =
                GetProperty(target, property);
            serialized.stringValue = value ?? string.Empty;
            Apply(serialized);
        }

        private static void SetFloat(
            Object target,
            string property,
            float value)
        {
            SerializedProperty serialized =
                GetProperty(target, property);
            serialized.floatValue = value;
            Apply(serialized);
        }

        private static void SetInt(
            Object target,
            string property,
            int value)
        {
            SerializedProperty serialized =
                GetProperty(target, property);
            serialized.intValue = value;
            Apply(serialized);
        }

        private static void SetBool(
            Object target,
            string property,
            bool value)
        {
            SerializedProperty serialized =
                GetProperty(target, property);
            serialized.boolValue = value;
            Apply(serialized);
        }

        private static void SetColor(
            Object target,
            string property,
            Color value)
        {
            SerializedProperty serialized =
                GetProperty(target, property);
            serialized.colorValue = value;
            Apply(serialized);
        }

        private static void SetVector3(
            Object target,
            string property,
            Vector3 value)
        {
            SerializedProperty serialized =
                GetProperty(target, property);
            serialized.vector3Value = value;
            Apply(serialized);
        }

        private static void SetObject(
            Object target,
            string property,
            Object value)
        {
            SerializedProperty serialized =
                GetProperty(target, property);
            serialized.objectReferenceValue = value;
            Apply(serialized);
        }

        private static void SetObjectArray(
            Object target,
            string property,
            Object[] values)
        {
            SerializedProperty serialized =
                GetProperty(target, property);
            int count = values != null ? values.Length : 0;
            serialized.arraySize = count;
            for (int index = 0; index < count; index++)
            {
                serialized.GetArrayElementAtIndex(index)
                    .objectReferenceValue = values[index];
            }
            Apply(serialized);
        }

        private static void SetFloatArray(
            Object target,
            string property,
            float[] values)
        {
            SerializedProperty serialized =
                GetProperty(target, property);
            int count = values != null ? values.Length : 0;
            serialized.arraySize = count;
            for (int index = 0; index < count; index++)
            {
                serialized.GetArrayElementAtIndex(index)
                    .floatValue = values[index];
            }
            Apply(serialized);
        }

        private static SerializedProperty GetProperty(
            Object target,
            string property)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.Update();
            SerializedProperty result =
                serialized.FindProperty(property);
            if (result == null)
            {
                throw new System.InvalidOperationException(
                    target.name + " has no serialized property " + property);
            }
            return result;
        }

        private static void Apply(SerializedProperty property)
        {
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(property.serializedObject.targetObject);
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
