using System;
using System.Collections.Generic;
using NERA.Energy;
using NERA.Maintenance;
using NERA.Station;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NERA.Editor
{
    /// <summary>
    /// Rebuilds the deliberately simple cube prototypes from the central
    /// StationSystems_Default configuration. Slots come from config, so adding
    /// or removing a slot never requires another hardcoded gameplay branch.
    /// </summary>
    public static class StationPhysicalUpgradeSetup
    {
        private const string PlayerScenePath =
            "Assets/_Project/NERA/Scenes/Player_Station.unity";
        private const string MainScenePath =
            "Assets/_Project/NERA/Scenes/MainScene.unity";
        private const string TemplatePrefabPath =
            "Assets/_Project/NERA/Prefabs/Station/" +
            "P_StationUpgradeableCube.prefab";

        private readonly struct WorldObjectSpec
        {
            public WorldObjectSpec(
                StationSystemType type,
                string objectId,
                string name,
                Vector3 position,
                Vector3 bodyPosition,
                Vector3 bodyScale)
            {
                Type = type;
                ObjectId = objectId;
                Name = name;
                Position = position;
                BodyPosition = bodyPosition;
                BodyScale = bodyScale;
            }

            public StationSystemType Type { get; }
            public string ObjectId { get; }
            public string Name { get; }
            public Vector3 Position { get; }
            public Vector3 BodyPosition { get; }
            public Vector3 BodyScale { get; }
        }

        private static readonly WorldObjectSpec[] WorldObjects =
        {
            new WorldObjectSpec(
                StationSystemType.Turret,
                "station_turret_01",
                "Station_Turret_01",
                new Vector3(-12.53f, 0f, 33.8f),
                new Vector3(0f, 0.45f, 0f),
                new Vector3(0.9f, 0.9f, 0.9f)),
            new WorldObjectSpec(
                StationSystemType.Turret,
                "station_turret_02",
                "Station_Turret_02",
                new Vector3(12.33f, 0f, 33.02f),
                new Vector3(0f, 0.45f, 0f),
                new Vector3(0.9f, 0.9f, 0.9f)),
            new WorldObjectSpec(
                StationSystemType.Antenna,
                "station_antenna",
                "Station_Antenna",
                new Vector3(-8.22f, 0f, 30.71f),
                new Vector3(0f, 1.2f, 0f),
                new Vector3(0.7f, 2.4f, 0.7f)),
            new WorldObjectSpec(
                StationSystemType.Drone,
                "station_drone",
                "Station_Drone",
                new Vector3(7.56f, 0f, 24.36f),
                new Vector3(0f, 0.9f, 0f),
                new Vector3(1.4f, 0.45f, 1f)),
            new WorldObjectSpec(
                StationSystemType.Battery,
                "station_battery",
                "Station_Battery",
                new Vector3(-9.567f, 0f, 14.349f),
                new Vector3(0f, 0.5f, 0f),
                new Vector3(1.5f, 1f, 1.3f))
        };

        [MenuItem("NERA/Station/Rebuild Physical Upgrade Prototypes")]
        public static void Rebuild()
        {
            StationSystemsConfig config = StationSystemsConfig.LoadDefault();
            if (config == null)
                throw new InvalidOperationException(
                    "StationSystems_Default could not be loaded.");

            Scene playerScene = OpenOrGetScene(PlayerScenePath);
            GameObject templatePrefab = EnsureTemplatePrefab();
            RebuildWorld(playerScene, templatePrefab, config);

            Scene mainScene = OpenOrGetScene(MainScenePath);
            RebuildPreview(mainScene, config);

            EditorSceneManager.MarkSceneDirty(playerScene);
            EditorSceneManager.MarkSceneDirty(mainScene);
            EditorSceneManager.SaveScene(playerScene);
            EditorSceneManager.SaveScene(mainScene);
            AssetDatabase.SaveAssets();
            Debug.Log(
                "Rebuilt 2 turrets, antenna, drone and battery with " +
                "config-driven slots in Player_Station and StationUIPreview.");
        }

        private static GameObject EnsureTemplatePrefab()
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(TemplatePrefabPath);
            if (prefab == null)
                throw new InvalidOperationException(
                    "P_StationUpgradeableCube prefab is missing.");
            return prefab;
        }

        private static void RebuildWorld(
            Scene scene,
            GameObject templatePrefab,
            StationSystemsConfig config)
        {
            foreach (WorldObjectSpec spec in WorldObjects)
                DestroyRoot(scene, spec.Name);

            foreach (WorldObjectSpec spec in WorldObjects)
            {
                StationSystemDefinition definition =
                    config.Find(spec.Type, spec.ObjectId);
                if (definition == null)
                {
                    throw new InvalidOperationException(
                        $"Missing station config for {spec.ObjectId}.");
                }

                GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(
                    templatePrefab,
                    scene);
                root.name = spec.Name;
                root.transform.SetPositionAndRotation(
                    spec.Position,
                    Quaternion.identity);

                StationObjectIdentity identity =
                    GetOrAdd<StationObjectIdentity>(root);
                identity.Configure(spec.Type, spec.ObjectId);
                TrimAndConfigureSlots(root, definition);

                Transform body = root.transform.Find("Base");
                if (body == null)
                    throw new InvalidOperationException("Template has no Base.");
                body.localPosition = spec.BodyPosition;
                body.localScale = spec.BodyScale;
                int interactableLayer = LayerMask.NameToLayer("Interactable");
                if (interactableLayer >= 0)
                    SetLayerRecursively(body.gameObject, interactableLayer);

                StationObjectVisual visual =
                    GetOrAdd<StationObjectVisual>(root);
                visual.Configure(true);
                ConfigureUpgradeable(root, identity, visual);
                AddRuntimeBehaviour(root, body, spec.Type);
            }
        }

        private static void RebuildPreview(
            Scene scene,
            StationSystemsConfig config)
        {
            GameObject previewRoot = FindInScene(scene, "SM_UI_3D");
            if (previewRoot == null)
                throw new InvalidOperationException("StationUIPreview/SM_UI_3D is missing.");

            var positions = new Dictionary<string, Vector3>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["station_turret_01"] = new Vector3(-5f, 0f, 3.8f),
                ["station_turret_02"] = new Vector3(5f, 0f, 3.8f),
                ["station_antenna"] = new Vector3(-2.5f, 0f, 2f),
                ["station_drone"] = new Vector3(2.5f, 0.65f, 0f),
                ["station_battery"] = new Vector3(-2.6f, 0f, -2.2f)
            };

            foreach (WorldObjectSpec spec in WorldObjects)
            {
                string previewName = spec.Type == StationSystemType.Turret
                    ? $"SM_Turret_{spec.ObjectId[^1]}"
                    : spec.Type switch
                    {
                        StationSystemType.Antenna => "SM_Antenna",
                        StationSystemType.Drone => "SM_Drone",
                        _ => "SM_Battery"
                    };
                Transform existing = previewRoot.transform.Find(previewName);
                GameObject root = existing != null
                    ? existing.gameObject
                    : new GameObject(previewName);
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);
                root.transform.SetParent(previewRoot.transform, false);
                root.transform.localPosition = positions[spec.ObjectId];
                root.transform.localRotation = Quaternion.identity;
                root.transform.localScale = Vector3.one;

                DestroyChildren(root.transform);
                StationObjectIdentity identity =
                    GetOrAdd<StationObjectIdentity>(root);
                identity.Configure(spec.Type, spec.ObjectId);

                GameObject body = GameObject.CreatePrimitive(
                    PrimitiveType.Cube);
                body.name = "Base";
                body.transform.SetParent(root.transform, false);
                body.transform.localPosition = spec.BodyPosition * 0.55f;
                body.transform.localScale = spec.BodyScale * 0.55f;

                StationSystemDefinition definition =
                    config.Find(spec.Type, spec.ObjectId);
                CreatePreviewSlots(body.transform, definition);

                StationObjectVisual visual =
                    GetOrAdd<StationObjectVisual>(root);
                visual.Configure(false);
                SetLayerRecursively(root, 13);
            }
        }

        private static void TrimAndConfigureSlots(
            GameObject root,
            StationSystemDefinition definition)
        {
            StationUpgradeSlot[] slots =
                root.GetComponentsInChildren<StationUpgradeSlot>(true);
            foreach (StationUpgradeSlot slot in slots)
            {
                StationObjectSlotDefinition declared =
                    definition.FindSlot(slot.SlotId);
                if (declared == null)
                {
                    UnityEngine.Object.DestroyImmediate(slot.gameObject);
                    continue;
                }
                slot.Configure(declared.SlotId, slot.FakeVisual);
                if (slot.FakeVisual != null)
                    slot.FakeVisual.SetActive(false);
            }
        }

        private static void CreatePreviewSlots(
            Transform body,
            StationSystemDefinition definition)
        {
            if (definition == null)
                return;
            Vector3[] positions =
            {
                new Vector3(0f, 0.45f, 0.52f),
                new Vector3(-0.52f, 0f, 0.2f),
                new Vector3(0.52f, 0f, -0.2f),
                new Vector3(0f, -0.35f, -0.52f),
                new Vector3(0f, 0.52f, 0f)
            };

            for (int index = 0; index < definition.Slots.Count; index++)
            {
                StationObjectSlotDefinition declared = definition.Slots[index];
                GameObject slotRoot = new GameObject(declared.SlotId);
                slotRoot.transform.SetParent(body, false);
                slotRoot.transform.localPosition =
                    positions[Mathf.Min(index, positions.Length - 1)];
                StationUpgradeSlot slot =
                    slotRoot.AddComponent<StationUpgradeSlot>();

                GameObject fake = GameObject.CreatePrimitive(PrimitiveType.Cube);
                fake.name = "Fake";
                fake.transform.SetParent(slotRoot.transform, false);
                fake.transform.localScale = Vector3.one * 0.24f;
                fake.SetActive(false);
                slot.Configure(declared.SlotId, fake);
                slot.ConfigureMount(
                    Vector3.zero,
                    Vector3.zero,
                    Vector3.one * 0.4f);
            }
        }

        private static void ConfigureUpgradeable(
            GameObject root,
            StationObjectIdentity identity,
            StationObjectVisual visual)
        {
            StationUpgradeableObject upgradeable =
                GetOrAdd<StationUpgradeableObject>(root);
            SerializedObject serialized = new SerializedObject(upgradeable);
            serialized.FindProperty("identity").objectReferenceValue = identity;
            serialized.FindProperty("visual").objectReferenceValue = visual;
            serialized.FindProperty("upgradeCamera").objectReferenceValue =
                root.GetComponentInChildren<CinemachineVirtualCameraBase>(true);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddRuntimeBehaviour(
            GameObject root,
            Transform body,
            StationSystemType type)
        {
            if (type == StationSystemType.Battery)
            {
                GetOrAdd<StationBattery>(root);
                return;
            }

            if (type == StationSystemType.Antenna)
            {
                MaintainableObject maintainable =
                    GetOrAdd<MaintainableObject>(root);
                SetOutdoorMaintenanceRole(
                    maintainable,
                    MaintenanceRole.Antenna);
                return;
            }

            if (type == StationSystemType.Drone)
            {
                MaintainableObject maintainable =
                    GetOrAdd<MaintainableObject>(root);
                SetOutdoorMaintenanceRole(
                    maintainable,
                    MaintenanceRole.Drone);
                return;
            }

            if (type != StationSystemType.Turret)
                return;

            MaintainableObject turretMaintenance =
                GetOrAdd<MaintainableObject>(root);
            SetOutdoorMaintenanceRole(
                turretMaintenance,
                MaintenanceRole.Turret);
            StationTurretController turret =
                GetOrAdd<StationTurretController>(root);
            SerializedObject serialized = new SerializedObject(turret);
            serialized.FindProperty("yawPivot").objectReferenceValue = body;
            serialized.FindProperty("muzzle").objectReferenceValue =
                body.Find("Focus_Point");
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetOutdoorMaintenanceRole(
            MaintainableObject target,
            MaintenanceRole role)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty("role").enumValueIndex = (int)role;
            serialized.FindProperty("exposedToWeather").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Scene OpenOrGetScene(string path)
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            return scene.IsValid() && scene.isLoaded
                ? scene
                : EditorSceneManager.OpenScene(
                    path,
                    OpenSceneMode.Additive);
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name)
                    return root;
            }
            return null;
        }

        private static GameObject FindInScene(Scene scene, string name)
        {
            foreach (Transform transform in UnityEngine.Object
                         .FindObjectsByType<Transform>(
                             FindObjectsInactive.Include,
                             FindObjectsSortMode.None))
            {
                if (transform.gameObject.scene == scene &&
                    transform.name == name)
                {
                    return transform.gameObject;
                }
            }
            return null;
        }

        private static void DestroyRoot(Scene scene, string name)
        {
            GameObject root = FindRoot(scene, name);
            if (root != null)
                UnityEngine.Object.DestroyImmediate(root);
        }

        private static void DestroyChildren(Transform parent)
        {
            for (int index = parent.childCount - 1; index >= 0; index--)
                UnityEngine.Object.DestroyImmediate(
                    parent.GetChild(index).gameObject);
        }

        private static T GetOrAdd<T>(GameObject target)
            where T : Component
        {
            return target.TryGetComponent(out T component)
                ? component
                : target.AddComponent<T>();
        }

        private static void SetLayerRecursively(GameObject target, int layer)
        {
            target.layer = layer;
            foreach (Transform child in target.transform)
                SetLayerRecursively(child.gameObject, layer);
        }
    }
}
