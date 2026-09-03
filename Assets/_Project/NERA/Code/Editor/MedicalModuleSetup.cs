using System;
using System.Collections.Generic;
using System.Linq;
using NERA.Interaction;
using NERA.Navigation;
using NERA.Station;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NERA.Editor
{
    /// <summary>
    /// Deterministically builds the medical-module prototype, its world
    /// instance, and both terminal preview representations.
    /// </summary>
    public static class MedicalModuleSetup
    {
        private const string PlayerScenePath =
            "Assets/_Project/NERA/Scenes/Player_Station.unity";
        private const string MedicalPrefabPath =
            "Assets/_Project/NERA/Prefabs/Station/" +
            "P_StationMedicalModule.prefab";
        private const string TerminalPrefabPath =
            "Assets/_Project/NERA/Prefabs/Station/Station_Terminal.prefab";
        private const string TerminalScreenPrefabPath =
            "Assets/_Project/NERA/Prefabs/UI/Screens/" +
            "P_Screen_Terminal.prefab";
        private const string WorldObjectName = "Station_MedicalModule";
        private const string PreviewObjectName = "SM_MedicalModule";

        private static readonly Vector3 WorldPosition =
            new Vector3(2.75f, 0f, -2.25f);
        private static readonly Vector3 PreviewPosition =
            new Vector3(2.5f, 0f, -2.2f);
        private static readonly Vector3 InteractionPointPosition =
            new Vector3(-0.423f, 0.868f, 0.723f);
        private static readonly Vector3 TreatmentPointPosition =
            new Vector3(0f, 0.2f, 0f);

        [MenuItem("NERA/Station/Rebuild Medical Module")]
        public static void Rebuild()
        {
            StationSystemDefinition definition =
                StationSystemsConfig.LoadDefault()?.Find(
                    StationSystemType.MedicalModule,
                    MedicalModuleController.DefaultObjectId);
            if (definition == null)
            {
                throw new InvalidOperationException(
                    "Medical-module station config is missing.");
            }

            GameObject prefab = BuildMedicalPrefab();
            InstallWorldInstance(prefab);
            InstallTerminalMiniature();
            InstallTerminalScreenPreview();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "Medical module rebuilt: station object, quest point, " +
                "terminal miniature and clickable terminal preview are ready.");
        }

        private static GameObject BuildMedicalPrefab()
        {
            int interactableLayer = LayerMask.NameToLayer("Interactable");
            var root = new GameObject("P_StationMedicalModule");
            try
            {
                StationObjectIdentity identity =
                    root.AddComponent<StationObjectIdentity>();
                identity.Configure(
                    StationSystemType.MedicalModule,
                    MedicalModuleController.DefaultObjectId);

                GameObject body = GameObject.CreatePrimitive(
                    PrimitiveType.Cube);
                body.name = "MedicalPlatform";
                body.transform.SetParent(root.transform, false);
                body.transform.localPosition = new Vector3(0f, 0.1f, 0f);
                body.transform.localScale = new Vector3(1f, 0.2f, 1f);

                var interactionPoint = new GameObject("InteractionPoint");
                interactionPoint.transform.SetParent(root.transform, false);
                interactionPoint.transform.localPosition =
                    InteractionPointPosition;
                if (interactableLayer >= 0)
                    interactionPoint.layer = interactableLayer;
                SphereCollider interactionTrigger =
                    interactionPoint.AddComponent<SphereCollider>();
                interactionTrigger.isTrigger = true;
                interactionTrigger.radius = 0.2f;

                var treatmentPoint = new GameObject("TreatmentPoint");
                treatmentPoint.transform.SetParent(root.transform, false);
                treatmentPoint.transform.localPosition =
                    TreatmentPointPosition;

                var markerObject = new GameObject("Quest Marker");
                markerObject.transform.SetParent(root.transform, false);
                QuestMarkerAnchor marker =
                    markerObject.AddComponent<QuestMarkerAnchor>();
                ConfigureQuestMarker(marker);

                MedicalModuleController medical =
                    root.AddComponent<MedicalModuleController>();
                ConfigureMedicalController(
                    medical,
                    identity,
                    interactionPoint.transform,
                    treatmentPoint.transform);

                PrefabUtility.SaveAsPrefabAsset(root, MedicalPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(
                MedicalPrefabPath);
        }

        private static void ConfigureMedicalController(
            MedicalModuleController medical,
            StationObjectIdentity identity,
            Transform interactionPoint,
            Transform treatmentPoint)
        {
            var serialized = new SerializedObject(medical);
            serialized.FindProperty("interactionPoint").objectReferenceValue =
                interactionPoint;
            serialized.FindProperty("mode").enumValueIndex =
                (int)NERA.Interaction.InteractionMode.Hold;
            serialized.FindProperty("holdDuration").floatValue = 1f;
            serialized.FindProperty("isAvailable").boolValue = true;
            serialized.FindProperty("questInteractionId").stringValue =
                MedicalModuleController.DefaultObjectId;
            serialized.FindProperty("questInteractionName").stringValue =
                string.Empty;
            serialized.FindProperty("identity").objectReferenceValue =
                identity;
            serialized.FindProperty("treatmentPoint").objectReferenceValue =
                treatmentPoint;
            serialized.FindProperty("requiredActionHoldDuration").floatValue = 1f;
            serialized.FindProperty("entryDuration").floatValue = 1.25f;
            serialized.FindProperty("walkAnimationState").stringValue =
                "Walk";
            serialized.FindProperty("idleAnimationState").stringValue =
                "Idle";
            serialized.FindProperty("hideAllCanvases").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureQuestMarker(QuestMarkerAnchor marker)
        {
            var serialized = new SerializedObject(marker);
            serialized.FindProperty("markerId").stringValue =
                MedicalModuleController.QuestMarkerId;
            serialized.FindProperty("positionSource").objectReferenceValue =
                null;
            serialized.FindProperty("localOffset").vector3Value =
                new Vector3(0f, 1.5f, 0f);
            serialized.FindProperty("showDistance").boolValue = true;
            serialized.FindProperty("worldMarkerFadeDistance").floatValue = 3f;
            serialized.FindProperty("worldMarkerMaxDistance").floatValue = 50f;
            serialized.FindProperty("availableWithoutQuest").boolValue = false;
            serialized.FindProperty("available").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void InstallWorldInstance(GameObject prefab)
        {
            if (prefab == null)
                throw new InvalidOperationException(
                    "Medical-module prefab was not created.");

            Scene scene = SceneManager.GetSceneByPath(PlayerScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(
                    PlayerScenePath,
                    OpenSceneMode.Additive);
            }

            GameObject existing = scene.GetRootGameObjects()
                .FirstOrDefault(item => item.name == WorldObjectName);
            if (existing != null)
                UnityEngine.Object.DestroyImmediate(existing);

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(
                prefab,
                scene);
            instance.name = WorldObjectName;
            instance.transform.SetPositionAndRotation(
                WorldPosition,
                Quaternion.identity);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void InstallTerminalMiniature()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(
                TerminalPrefabPath);
            try
            {
                Transform container = root.transform.Find(
                    "Visual_3D/SM_Station_Mini_3D");
                if (container == null)
                {
                    throw new InvalidOperationException(
                        "Station terminal miniature container is missing.");
                }
                InstallPreviewObject(container, false);
                PrefabUtility.SaveAsPrefabAsset(root, TerminalPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void InstallTerminalScreenPreview()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(
                TerminalScreenPrefabPath);
            try
            {
                List<Transform> containers = root
                    .GetComponentsInChildren<Transform>(true)
                    .Where(item => item.name == "SM_UI_3D" &&
                        item.GetComponentsInChildren<StationObjectIdentity>(
                            true).Length >= 2)
                    .ToList();
                if (containers.Count == 0)
                {
                    throw new InvalidOperationException(
                        "Terminal screen 3D preview container is missing.");
                }

                foreach (Transform container in containers)
                    InstallPreviewObject(container, true);
                PrefabUtility.SaveAsPrefabAsset(
                    root,
                    TerminalScreenPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void InstallPreviewObject(
            Transform container,
            bool clickable)
        {
            StationObjectIdentity[] existing = container
                .GetComponentsInChildren<StationObjectIdentity>(true);
            foreach (StationObjectIdentity identity in existing)
            {
                if (identity != null && string.Equals(
                        identity.ObjectId,
                        MedicalModuleController.DefaultObjectId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    UnityEngine.Object.DestroyImmediate(identity.gameObject);
                }
            }

            var preview = new GameObject(PreviewObjectName);
            preview.transform.SetParent(container, false);
            preview.transform.localPosition = PreviewPosition;
            preview.transform.localRotation = Quaternion.identity;
            preview.transform.localScale = Vector3.one;
            StationObjectIdentity previewIdentity =
                preview.AddComponent<StationObjectIdentity>();
            previewIdentity.Configure(
                StationSystemType.MedicalModule,
                MedicalModuleController.DefaultObjectId);

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "MedicalPlatform";
            body.transform.SetParent(preview.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            body.transform.localScale = new Vector3(1f, 0.2f, 1f);
            if (!clickable)
            {
                Collider collider = body.GetComponent<Collider>();
                if (collider != null)
                    UnityEngine.Object.DestroyImmediate(collider);
            }

            StationObjectVisual visual =
                preview.AddComponent<StationObjectVisual>();
            visual.Configure(false);
            SetLayerRecursively(
                preview,
                clickable ? 13 : LayerMask.NameToLayer("Default"));
        }

        private static void SetLayerRecursively(GameObject target, int layer)
        {
            target.layer = layer;
            foreach (Transform child in target.transform)
                SetLayerRecursively(child.gameObject, layer);
        }
    }
}
