using System;
using System.Collections.Generic;
using System.Linq;
using Climbing;
using NERA.Combat;
using NERA.Core;
using NERA.Expeditions;
using NERA.Interaction;
using NERA.Inventory;
using NERA.Locations;
using NERA.Player;
using NERA.Quests;
using NERA.Station;
using NERA.Terminal;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace NERA.Editor
{
    /// <summary>
    /// Promotes the project-structure checks that started as EditMode tests
    /// into an editor command that can be run before content hand-off or build.
    /// </summary>
    public static class ProjectValidator
    {
        private const string MainScenePath =
            "Assets/_Project/NERA/Scenes/MainScene.unity";
        private const string PlayerStationScenePath =
            "Assets/_Project/NERA/Scenes/Player_Station.unity";
        private const string PlayerPrefabPath =
            "Assets/_Project/NERA/Prefabs/Player/Player.prefab";
        private const string LocationConfigRoot =
            "Assets/_Project/NERA/Configs";
        private const string ExpectedCompanyName = "Measured Field";
        private const string ExpectedProductName = "Nera";

        private static readonly string[] RequiredBuildScenePrefix =
        {
            "Assets/_Project/NERA/Scenes/Boot.unity",
            MainScenePath,
            PlayerStationScenePath
        };

        private static readonly QualityPresetExpectation[]
            PCQualityPresetExpectations =
            {
                new QualityPresetExpectation(
                    "Low",
                    "Assets/Settings/PC_Low_RPAsset.asset"),
                new QualityPresetExpectation(
                    "Medium",
                    "Assets/Settings/PC_Medium_RPAsset.asset"),
                new QualityPresetExpectation(
                    "High",
                    "Assets/Settings/PC_High_RPAsset.asset")
            };

        [MenuItem("NERA/Validate Project")]
        public static void ValidateFromMenu()
        {
            ValidateOrThrow();
            Debug.Log(
                $"NERA project validation passed: " +
                $"{EditorBuildSettings.scenes.Count(scene => scene.enabled)} " +
                $"enabled build scenes, " +
                $"{FindLocationAssets().Length} location configs and " +
                $"{PCQualityPresetExpectations.Length} PC quality presets.");
        }

        public static void ValidateOrThrow()
        {
            List<string> errors = new List<string>();
            ValidatePlayerSettings(errors);
            ValidateBuildScenes(errors);
            ValidatePlayerPrefab(errors);
            ValidateExpeditionLocations(errors);
            ValidateQuestCatalog(errors);
            ValidatePCQualityPresets(errors);

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "NERA project validation failed:\n- " +
                    string.Join("\n- ", errors));
            }
        }

        private static void ValidatePlayerSettings(List<string> errors)
        {
            if (!string.Equals(
                    PlayerSettings.companyName,
                    ExpectedCompanyName,
                    StringComparison.Ordinal))
            {
                errors.Add(
                    $"Company Name must be '{ExpectedCompanyName}', but is " +
                    $"'{PlayerSettings.companyName}'.");
            }

            if (!string.Equals(
                    PlayerSettings.productName,
                    ExpectedProductName,
                    StringComparison.Ordinal))
            {
                errors.Add(
                    $"Product Name must be '{ExpectedProductName}', but is " +
                    $"'{PlayerSettings.productName}'.");
            }
        }

        private static void ValidateBuildScenes(List<string> errors)
        {
            string[] enabledScenePaths = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            HashSet<string> enabledScenes = new HashSet<string>(
                enabledScenePaths,
                StringComparer.Ordinal);

            for (int index = 0;
                 index < RequiredBuildScenePrefix.Length;
                 index++)
            {
                string scenePath = RequiredBuildScenePrefix[index];
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
                    errors.Add($"Required scene asset is missing: {scenePath}");
                else if (!enabledScenes.Contains(scenePath))
                    errors.Add(
                        $"Required scene is disabled in Build Settings: " +
                        scenePath);

                if (index >= enabledScenePaths.Length ||
                    !string.Equals(
                        enabledScenePaths[index],
                        scenePath,
                        StringComparison.Ordinal))
                {
                    errors.Add(
                        $"Build scene index {index} must be {scenePath}.");
                }
            }

            foreach (string scenePath in enabledScenePaths)
                ValidateSceneHasNoMissingScripts(scenePath, errors);
        }

        private static void ValidatePlayerPrefab(List<string> errors)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                PlayerPrefabPath);
            if (prefab == null)
            {
                errors.Add($"Player prefab is missing: {PlayerPrefabPath}");
                return;
            }

            ParkourPlayerBridge bridge =
                prefab.GetComponentInChildren<ParkourPlayerBridge>(true);
            if (bridge == null)
            {
                errors.Add(
                    $"{PlayerPrefabPath} has no {nameof(ParkourPlayerBridge)}.");
                return;
            }

            GameObject model = bridge.gameObject;
            if (!model.CompareTag("Player") || model.layer != 3)
            {
                errors.Add(
                    $"{PlayerPrefabPath}/PlayerModel must use tag Player " +
                    "and layer Player (3).");
            }

            RequirePlayerComponent<InputCharacterController>(model, errors);
            RequirePlayerComponent<ThirdPersonController>(model, errors);
            RequirePlayerComponent<PlayerInteractionController>(model, errors);
            RequirePlayerComponent<PlayerInventory>(model, errors);
            RequirePlayerComponent<PlayerEquipmentController>(model, errors);
            RequirePlayerComponent<PlayerEnergyWeaponController>(model, errors);
            RequirePlayerComponent<PlayerHealth>(model, errors);

            Camera[] cameras = prefab.GetComponentsInChildren<Camera>(true);
            if (cameras.Length != 1 || !cameras[0].CompareTag("MainCamera"))
            {
                errors.Add(
                    $"{PlayerPrefabPath} must contain exactly one MainCamera.");
            }

            Animator animator = model.GetComponent<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                errors.Add(
                    $"{PlayerPrefabPath}/PlayerModel has no configured Animator.");
            }

            CapsuleCollider[] motorColliders =
                model.GetComponents<CapsuleCollider>();
            if (motorColliders.Length != 2 ||
                motorColliders.Count(collider => collider.enabled) != 1)
            {
                errors.Add(
                    $"{PlayerPrefabPath}/PlayerModel must have two motor " +
                    "capsules with exactly one enabled.");
            }

            Rigidbody motor = model.GetComponent<Rigidbody>();
            Rigidbody[] ragdollBodies = prefab
                .GetComponentsInChildren<Rigidbody>(true)
                .Where(body => body != motor)
                .ToArray();
            if (ragdollBodies.Length < 12 ||
                prefab.GetComponentsInChildren<CharacterJoint>(true).Length < 11)
            {
                errors.Add(
                    $"{PlayerPrefabPath} ragdoll is incomplete: expected at " +
                    "least 12 bodies and 11 joints.");
            }

            foreach (Transform child in
                     prefab.GetComponentsInChildren<Transform>(true))
            {
                int missingCount =
                    GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(
                        child.gameObject);
                if (missingCount > 0)
                {
                    errors.Add(
                        $"{PlayerPrefabPath}/{child.name} has {missingCount} " +
                        "missing script component(s).");
                }
            }
        }

        private static void RequirePlayerComponent<T>(
            GameObject model,
            List<string> errors) where T : Component
        {
            if (model.GetComponent<T>() == null)
            {
                errors.Add(
                    $"{PlayerPrefabPath}/PlayerModel has no {typeof(T).Name}.");
            }
        }

        private static void ValidateSceneHasNoMissingScripts(
            string scenePath,
            List<string> errors)
        {
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool openedByValidator = !scene.IsValid() || !scene.isLoaded;
            if (openedByValidator)
            {
                scene = EditorSceneManager.OpenScene(
                    scenePath,
                    OpenSceneMode.Additive);
            }

            try
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (Transform child in
                             root.GetComponentsInChildren<Transform>(true))
                    {
                        int missingCount =
                            GameObjectUtility
                                .GetMonoBehavioursWithMissingScriptCount(
                                    child.gameObject);
                        if (missingCount > 0)
                        {
                            errors.Add(
                                $"{scenePath}: {child.name} has " +
                                $"{missingCount} missing script component(s).");
                        }
                    }
                }
            }
            finally
            {
                if (openedByValidator && scene.IsValid())
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void ValidateExpeditionLocations(List<string> errors)
        {
            string[] locationAssetPaths = FindLocationAssets();
            if (locationAssetPaths.Length == 0)
            {
                errors.Add(
                    $"No ExpeditionLocationData assets found under " +
                    LocationConfigRoot);
                return;
            }

            HashSet<string> enabledScenePaths =
                EditorBuildSettings.scenes
                    .Where(scene => scene.enabled)
                    .Select(scene => scene.path)
                    .ToHashSet(StringComparer.Ordinal);
            HashSet<string> locationIds =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> referencedScenes =
                new HashSet<string>(StringComparer.Ordinal);
            HashSet<MapSlotData> expeditionMapSlots =
                new HashSet<MapSlotData>();
            List<ExpeditionLocationData> locations =
                new List<ExpeditionLocationData>();

            foreach (string assetPath in locationAssetPaths)
            {
                ExpeditionLocationData location =
                    AssetDatabase.LoadAssetAtPath<ExpeditionLocationData>(
                        assetPath);
                if (location == null)
                {
                    errors.Add(
                        $"Location config could not be loaded: {assetPath}");
                    continue;
                }

                locations.Add(location);
                string prefix = $"{assetPath}: ";

                if (string.IsNullOrWhiteSpace(location.LocationId))
                    errors.Add(prefix + "Location Id is empty.");
                else if (!locationIds.Add(location.LocationId))
                {
                    errors.Add(
                        prefix + $"duplicate Location Id " +
                        $"'{location.LocationId}'.");
                }

                if (location.Scene == null ||
                    !location.Scene.IsConfigured)
                {
                    errors.Add(prefix + "Scene reference is not configured.");
                }
                else
                {
                    bool sceneExists =
                        AssetDatabase.LoadAssetAtPath<SceneAsset>(
                            location.ScenePath) != null;
                    if (!sceneExists)
                    {
                        errors.Add(
                            prefix + $"scene asset is missing: " +
                            location.ScenePath);
                    }

                    string actualGuid =
                        AssetDatabase.AssetPathToGUID(location.ScenePath);
                    if (!string.Equals(
                            actualGuid,
                            location.Scene.AssetGuid,
                            StringComparison.Ordinal))
                    {
                        errors.Add(
                            prefix + "scene GUID and path do not match.");
                    }

                    if (!enabledScenePaths.Contains(location.ScenePath))
                    {
                        errors.Add(
                            prefix + $"scene is disabled in Build Settings: " +
                            location.ScenePath);
                    }

                    if (!referencedScenes.Add(location.ScenePath))
                    {
                        errors.Add(
                            prefix + $"scene is referenced by more than one " +
                            $"location: {location.ScenePath}");
                    }

                    if (sceneExists &&
                        !string.IsNullOrWhiteSpace(location.SpawnPointId))
                    {
                        ValidateSpawnPoint(location, prefix, errors);
                    }
                }

                if (string.IsNullOrWhiteSpace(location.SpawnPointId))
                    errors.Add(prefix + "Spawn Point Id is empty.");

                if (location.LocationType == LocationType.Expedition &&
                    location.DiscoverySource != DiscoverySource.Antenna)
                {
                    if (location.MapSlot == null)
                    {
                        errors.Add(
                            prefix + "Map Slot is not assigned.");
                    }
                    else if (!expeditionMapSlots.Add(location.MapSlot))
                    {
                        errors.Add(
                            prefix + $"duplicate Map Slot " +
                            $"'{location.MapSlot.DisplayName}'.");
                    }
                }
            }

            ValidateLocationRegistration(locations, errors);
        }

        private static void ValidateSpawnPoint(
            ExpeditionLocationData location,
            string errorPrefix,
            List<string> errors)
        {
            Scene scene = SceneManager.GetSceneByPath(location.ScenePath);
            bool openedByValidator = !scene.IsValid() || !scene.isLoaded;
            if (openedByValidator)
            {
                scene = EditorSceneManager.OpenScene(
                    location.ScenePath,
                    OpenSceneMode.Additive);
            }

            try
            {
                int matchingSpawnPoints = 0;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    SceneSpawnPoint[] spawnPoints =
                        root.GetComponentsInChildren<SceneSpawnPoint>(true);
                    matchingSpawnPoints += spawnPoints.Count(
                        spawnPoint => string.Equals(
                            spawnPoint.SpawnPointId,
                            location.SpawnPointId,
                            StringComparison.Ordinal));
                }

                if (matchingSpawnPoints == 0)
                {
                    errors.Add(
                        errorPrefix + $"scene has no SceneSpawnPoint with ID " +
                        $"'{location.SpawnPointId}'.");
                }
                else if (matchingSpawnPoints > 1)
                {
                    errors.Add(
                        errorPrefix + $"scene has {matchingSpawnPoints} " +
                        $"SceneSpawnPoints with duplicate ID " +
                        $"'{location.SpawnPointId}'.");
                }
            }
            finally
            {
                if (openedByValidator && scene.IsValid())
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static string[] FindLocationAssets()
        {
            return AssetDatabase.FindAssets(
                    $"t:{nameof(ExpeditionLocationData)}",
                    new[] { LocationConfigRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }

        private static void ValidateLocationRegistration(
            IReadOnlyCollection<ExpeditionLocationData> configuredLocations,
            List<string> errors)
        {
            Scene mainScene = SceneManager.GetSceneByPath(MainScenePath);
            bool openedByValidator = !mainScene.IsValid() ||
                !mainScene.isLoaded;
            if (openedByValidator)
            {
                mainScene = EditorSceneManager.OpenScene(
                    MainScenePath,
                    OpenSceneMode.Additive);
            }

            Scene stationScene =
                SceneManager.GetSceneByPath(PlayerStationScenePath);
            bool stationOpenedByValidator = !stationScene.IsValid() ||
                !stationScene.isLoaded;
            if (stationOpenedByValidator)
            {
                stationScene = EditorSceneManager.OpenScene(
                    PlayerStationScenePath,
                    OpenSceneMode.Additive);
            }

            try
            {
                ExpeditionDiscoveryController discovery = null;
                QuestController questController = null;
                foreach (GameObject root in mainScene.GetRootGameObjects())
                {
                    discovery ??= root.GetComponentInChildren<
                        ExpeditionDiscoveryController>(true);
                    questController ??=
                        root.GetComponentInChildren<QuestController>(true);
                    if (discovery != null && questController != null)
                        break;
                }

                MapLocationSlotRegistry mapSlotRegistry = null;
                foreach (GameObject root in stationScene.GetRootGameObjects())
                {
                    mapSlotRegistry = root.GetComponentInChildren<
                        MapLocationSlotRegistry>(true);
                    if (mapSlotRegistry != null)
                        break;
                }

                if (questController == null)
                {
                    errors.Add(
                        $"{MainScenePath} has no {nameof(QuestController)}.");
                }

                if (discovery == null)
                {
                    errors.Add(
                        $"{MainScenePath} has no " +
                        nameof(ExpeditionDiscoveryController));
                }
                else
                {
                    HashSet<ExpeditionLocationData> registered =
                        new HashSet<ExpeditionLocationData>();
                    foreach (ExpeditionLocationData location in
                             discovery.KnownLocations)
                    {
                        if (location == null)
                        {
                            errors.Add(
                                $"{MainScenePath}: Known Locations contains null.");
                        }
                        else if (!registered.Add(location))
                        {
                            errors.Add(
                                $"{MainScenePath}: location " +
                                $"'{location.LocationId}' is registered twice.");
                        }
                    }

                    foreach (ExpeditionLocationData location in
                             configuredLocations)
                    {
                        if (!registered.Contains(location))
                        {
                            errors.Add(
                                $"{MainScenePath}: location config " +
                                $"'{location.LocationId}' is not registered in " +
                                "Known Locations.");
                        }
                    }
                }

                if (mapSlotRegistry == null)
                {
                    errors.Add(
                        $"{PlayerStationScenePath} has no " +
                        nameof(MapLocationSlotRegistry));
                    return;
                }

                HashSet<MapSlotData> authoredSlots =
                    new HashSet<MapSlotData>();
                HashSet<string> authoredSlotIds =
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (MapLocationSlot authoredSlot in
                         mapSlotRegistry.GetComponentsInChildren<
                             MapLocationSlot>(true))
                {
                    if (authoredSlot.Slot == null)
                    {
                        errors.Add(
                            $"{PlayerStationScenePath}: map object " +
                            $"'{authoredSlot.name}' has no Map Slot asset.");
                        continue;
                    }

                    if (!authoredSlots.Add(authoredSlot.Slot))
                    {
                        errors.Add(
                            $"{PlayerStationScenePath}: Map Slot " +
                            $"'{authoredSlot.Slot.DisplayName}' is assigned " +
                            "to more than one 3D object.");
                    }

                    if (string.IsNullOrWhiteSpace(authoredSlot.Slot.SlotId))
                    {
                        errors.Add(
                            $"{AssetDatabase.GetAssetPath(authoredSlot.Slot)}: " +
                            "Slot Id is empty.");
                    }
                    else if (!authoredSlotIds.Add(authoredSlot.Slot.SlotId))
                    {
                        errors.Add(
                            $"{PlayerStationScenePath}: duplicate stable " +
                            "Map Slot ID " +
                            $"'{authoredSlot.Slot.SlotId}'.");
                    }
                }

                foreach (ExpeditionLocationData location in configuredLocations)
                {
                    if (location.LocationType == LocationType.Expedition &&
                        location.DiscoverySource != DiscoverySource.Antenna &&
                        location.MapSlot != null &&
                        !authoredSlots.Contains(location.MapSlot))
                    {
                        errors.Add(
                            $"{PlayerStationScenePath}: location " +
                            $"'{location.LocationId}' references Map Slot " +
                            $"'{location.MapSlot.DisplayName}', but no 3D " +
                            "MapLocationSlot uses it.");
                    }
                }
            }
            finally
            {
                if (stationOpenedByValidator && stationScene.IsValid())
                    EditorSceneManager.CloseScene(stationScene, true);
                if (openedByValidator && mainScene.IsValid())
                    EditorSceneManager.CloseScene(mainScene, true);
            }
        }

        private static void ValidateQuestCatalog(List<string> errors)
        {
            QuestCatalog catalog = QuestCatalog.LoadDefault();
            if (catalog == null)
            {
                errors.Add(
                    "Resources/Quests/QuestCatalog_Default is missing.");
                return;
            }

            if (!catalog.TryValidate(out string error))
                errors.Add($"Quest catalog: {error}");
        }

        private static void ValidatePCQualityPresets(List<string> errors)
        {
            HashSet<string> qualityNames = new HashSet<string>(
                QualitySettings.names,
                StringComparer.Ordinal);
            foreach (QualityPresetExpectation expectation in
                     PCQualityPresetExpectations)
            {
                if (!qualityNames.Contains(expectation.Name))
                {
                    errors.Add(
                        $"Quality Settings is missing PC preset " +
                        expectation.Name);
                }

                if (AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(
                        expectation.PipelineAssetPath) == null)
                {
                    errors.Add(
                        $"PC render pipeline asset is missing: " +
                        expectation.PipelineAssetPath);
                }
            }
        }

        private readonly struct QualityPresetExpectation
        {
            public QualityPresetExpectation(
                string name,
                string pipelineAssetPath)
            {
                Name = name;
                PipelineAssetPath = pipelineAssetPath;
            }

            public string Name { get; }
            public string PipelineAssetPath { get; }
        }
    }
}
