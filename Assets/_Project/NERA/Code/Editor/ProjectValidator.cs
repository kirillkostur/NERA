using System;
using System.Collections.Generic;
using System.Linq;
using NERA.Core;
using NERA.Expeditions;
using NERA.Locations;
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
        private const string LocationConfigRoot =
            "Assets/_Project/NERA/Configs";

        private static readonly string[] RequiredBuildScenePrefix =
        {
            "Assets/_Project/NERA/Scenes/Boot.unity",
            MainScenePath,
            "Assets/_Project/NERA/Scenes/Player_Station.unity"
        };

        private static readonly UpgradePrefabExpectation[]
            UpgradePrefabExpectations =
            {
                new UpgradePrefabExpectation(
                    "Assets/_Project/NERA/Prefabs/StationUpgrade/" +
                    "P_StationTurret_Stages.prefab",
                    StationSystemType.Turret,
                    0,
                    3),
                new UpgradePrefabExpectation(
                    "Assets/_Project/NERA/Prefabs/StationUpgrade/" +
                    "P_StationBattery_Stages.prefab",
                    StationSystemType.Battery,
                    1,
                    2),
                new UpgradePrefabExpectation(
                    "Assets/_Project/NERA/Prefabs/StationUpgrade/" +
                    "P_StationDrone_Stages.prefab",
                    StationSystemType.Drone,
                    1,
                    3),
                new UpgradePrefabExpectation(
                    "Assets/_Project/NERA/Prefabs/StationUpgrade/" +
                    "P_StationAntenna_Stages.prefab",
                    StationSystemType.Antenna,
                    0,
                    3)
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
                $"{UpgradePrefabExpectations.Length} upgrade prefabs, " +
                $"{PCQualityPresetExpectations.Length} PC quality presets.");
        }

        public static void ValidateOrThrow()
        {
            List<string> errors = new List<string>();
            ValidateBuildScenes(errors);
            ValidateExpeditionLocations(errors);
            ValidateUpgradePrefabs(errors);
            ValidatePCQualityPresets(errors);

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "NERA project validation failed:\n- " +
                    string.Join("\n- ", errors));
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

            try
            {
                ExpeditionDiscoveryController discovery = null;
                MapLocationSlotRegistry mapSlotRegistry = null;
                foreach (GameObject root in mainScene.GetRootGameObjects())
                {
                    discovery ??= root.GetComponentInChildren<
                        ExpeditionDiscoveryController>(true);
                    mapSlotRegistry ??=
                        root.GetComponentInChildren<
                            MapLocationSlotRegistry>(true);
                    if (discovery != null && mapSlotRegistry != null)
                        break;
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
                        $"{MainScenePath} has no " +
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
                            $"{MainScenePath}: map object " +
                            $"'{authoredSlot.name}' has no Map Slot asset.");
                        continue;
                    }

                    if (!authoredSlots.Add(authoredSlot.Slot))
                    {
                        errors.Add(
                            $"{MainScenePath}: Map Slot " +
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
                            $"{MainScenePath}: duplicate stable Map Slot ID " +
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
                            $"{MainScenePath}: location " +
                            $"'{location.LocationId}' references Map Slot " +
                            $"'{location.MapSlot.DisplayName}', but no 3D " +
                            "MapLocationSlot uses it.");
                    }
                }
            }
            finally
            {
                if (openedByValidator && mainScene.IsValid())
                    EditorSceneManager.CloseScene(mainScene, true);
            }
        }

        private static void ValidateUpgradePrefabs(List<string> errors)
        {
            foreach (UpgradePrefabExpectation expectation in
                     UpgradePrefabExpectations)
            {
                GameObject prefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        expectation.Path);
                if (prefab == null)
                {
                    errors.Add(
                        $"Station upgrade prefab is missing: " +
                        expectation.Path);
                    continue;
                }

                StationUpgradeStageController controller =
                    prefab.GetComponent<StationUpgradeStageController>();
                if (controller == null)
                {
                    errors.Add(
                        $"{expectation.Path} has no " +
                        nameof(StationUpgradeStageController));
                    continue;
                }

                if (controller.SystemType != expectation.SystemType)
                {
                    errors.Add(
                        $"{expectation.Path} targets " +
                        $"{controller.SystemType}, expected " +
                        expectation.SystemType);
                }

                if (controller.MaxStage != expectation.MaximumStage)
                {
                    errors.Add(
                        $"{expectation.Path} has max stage " +
                        $"{controller.MaxStage}, expected " +
                        expectation.MaximumStage);
                }

                for (int stage = 0;
                     stage <= expectation.MaximumStage;
                     stage++)
                {
                    Transform stageRoot =
                        prefab.transform.Find($"Stage_{stage}");
                    if (stageRoot == null)
                    {
                        errors.Add(
                            $"{expectation.Path} is missing Stage_{stage}");
                        continue;
                    }

                    bool shouldBeActive =
                        stage == expectation.InitialStage;
                    if (stageRoot.gameObject.activeSelf != shouldBeActive)
                    {
                        errors.Add(
                            $"{expectation.Path}/Stage_{stage} active state " +
                            $"is {stageRoot.gameObject.activeSelf}, expected " +
                            shouldBeActive);
                    }
                }
            }
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

        private readonly struct UpgradePrefabExpectation
        {
            public UpgradePrefabExpectation(
                string path,
                StationSystemType systemType,
                int initialStage,
                int maximumStage)
            {
                Path = path;
                SystemType = systemType;
                InitialStage = initialStage;
                MaximumStage = maximumStage;
            }

            public string Path { get; }
            public StationSystemType SystemType { get; }
            public int InitialStage { get; }
            public int MaximumStage { get; }
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
