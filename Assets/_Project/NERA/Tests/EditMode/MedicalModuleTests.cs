using System;
using System.Linq;
using NERA.Energy;
using NERA.Interaction;
using NERA.Navigation;
using NERA.Station;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NERA.Tests.EditMode
{
    public sealed class MedicalModuleTests
    {
        private const string ObjectId = "station_medical_module";
        private const string MedicalPrefabPath =
            "Assets/_Project/NERA/Prefabs/Station/" +
            "P_StationMedicalModule.prefab";
        private const string PlayerScenePath =
            "Assets/_Project/NERA/Scenes/Player_Station.unity";

        [Test]
        public void CentralConfigDefinesFixedTreatmentBalance()
        {
            StationSystemsConfig config =
                StationSystemsConfig.LoadDefault();
            StationSystemDefinition definition = config.Find(
                StationSystemType.MedicalModule,
                ObjectId);

            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.Controllable, Is.True);
            Assert.That(definition.InitiallyActive, Is.False);
            Assert.That(definition.SupportsPhysicalUpgrades, Is.False);
            Assert.That(
                definition.GetBaseStat(
                    StationObjectStat.IdleEnergyConsumption,
                    -1f),
                Is.EqualTo(0f));
            Assert.That(
                definition.GetBaseStat(
                    StationObjectStat.TreatmentEnergyCost,
                    -1f),
                Is.EqualTo(30f));
            Assert.That(
                definition.GetBaseStat(
                    StationObjectStat.TreatmentDuration,
                    -1f),
                Is.EqualTo(10f));
            Assert.That(
                EnergyBalanceConfig.LoadDefault().GetMinimumChargePercent(
                    StationSystemType.MedicalModule,
                    ObjectId),
                Is.EqualTo(0f));
        }

        [Test]
        public void MedicalPrefabHasHoldPointQuestIdsAndExactCubeSize()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                MedicalPrefabPath);
            Assert.That(prefab, Is.Not.Null);

            StationObjectIdentity identity =
                prefab.GetComponent<StationObjectIdentity>();
            MedicalModuleController medical =
                prefab.GetComponent<MedicalModuleController>();
            Transform body = prefab.transform.Find("MedicalPlatform");
            Transform interactionPoint =
                prefab.transform.Find("InteractionPoint");
            Transform treatmentPoint = prefab.transform.Find("TreatmentPoint");
            Transform questMarker = prefab.transform.Find("Quest Marker");

            Assert.That(identity.SystemType,
                Is.EqualTo(StationSystemType.MedicalModule));
            Assert.That(identity.ObjectId, Is.EqualTo(ObjectId));
            Assert.That(medical, Is.Not.Null);
            Assert.That(
                medical.InteractionTransform,
                Is.EqualTo(interactionPoint));
            Assert.That(medical.TreatmentPoint, Is.EqualTo(treatmentPoint));
            Assert.That(medical.TreatmentDuration, Is.EqualTo(10f));
            Assert.That(medical.TreatmentEnergyCost, Is.EqualTo(30f));
            Assert.That(medical.IdleEnergyConsumption, Is.EqualTo(0f));
            Assert.That(body, Is.Not.Null);
            Assert.That(body.localScale, Is.EqualTo(new Vector3(1f, 0.2f, 1f)));
            Assert.That(body.gameObject.layer,
                Is.EqualTo(LayerMask.NameToLayer("Default")));

            Assert.That(interactionPoint, Is.Not.Null);
            Assert.That(
                interactionPoint.gameObject.layer,
                Is.EqualTo(LayerMask.NameToLayer("Interactable")));
            SphereCollider interactionCollider =
                interactionPoint.GetComponent<SphereCollider>();
            Assert.That(interactionCollider, Is.Not.Null);
            Assert.That(interactionCollider.isTrigger, Is.True);
            Assert.That(interactionCollider.radius, Is.EqualTo(0.2f));

            Assert.That(treatmentPoint, Is.Not.Null);
            Assert.That(
                treatmentPoint.GetComponents<Component>(),
                Has.Length.EqualTo(1),
                "TreatmentPoint must remain an empty destination transform.");

            QuestMarkerAnchor anchor =
                questMarker.GetComponent<QuestMarkerAnchor>();
            Assert.That(anchor, Is.Not.Null);
            Assert.That(
                anchor.MarkerId,
                Is.EqualTo(MedicalModuleController.QuestMarkerId));

            var serializedMedical = new SerializedObject(medical);
            Assert.That(
                serializedMedical.FindProperty("mode").enumValueIndex,
                Is.EqualTo((int)NERA.Interaction.InteractionMode.Hold));
            Assert.That(
                serializedMedical.FindProperty("holdDuration").floatValue,
                Is.EqualTo(1f));
            Assert.That(
                serializedMedical.FindProperty("actionText").stringValue,
                Is.EqualTo("Interact"));
            Assert.That(
                serializedMedical.FindProperty(
                    "unavailableReason").stringValue,
                Is.EqualTo("Unavailable"));
            Assert.That(
                serializedMedical.FindProperty(
                    "requiredActionHoldDuration").floatValue,
                Is.EqualTo(1f));
            Assert.That(
                serializedMedical.FindProperty("startActionLocalizationKey"),
                Is.Null);
            Assert.That(
                serializedMedical.FindProperty("energyConsumerId"),
                Is.Null);
            Assert.That(
                serializedMedical.FindProperty("walkAnimationState").stringValue,
                Is.EqualTo("Walk"));
        }

        [Test]
        public void TerminalPreviewContainsClickableMedicalCube()
        {
            const string prefabPath =
                "Assets/_Project/NERA/Prefabs/UI/Screens/" +
                "P_Screen_Terminal.prefab";
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            StationObjectIdentity[] medicalIdentities = prefab
                .GetComponentsInChildren<StationObjectIdentity>(true)
                .Where(item => item.SystemType ==
                        StationSystemType.MedicalModule &&
                    string.Equals(
                        item.ObjectId,
                        ObjectId,
                        StringComparison.OrdinalIgnoreCase))
                .ToArray();

            Assert.That(medicalIdentities, Has.Length.EqualTo(1));
            Collider collider = medicalIdentities[0]
                .GetComponentInChildren<Collider>(true);
            Assert.That(collider, Is.Not.Null);
            Assert.That(collider.gameObject.layer, Is.EqualTo(13));
        }

        [Test]
        public void TerminalWorldDecorationContainsVisualOnlyMedicalCube()
        {
            const string prefabPath =
                "Assets/_Project/NERA/Prefabs/Station/Station_Terminal.prefab";
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            StationObjectIdentity medicalIdentity = prefab
                .GetComponentsInChildren<StationObjectIdentity>(true)
                .Single(item => item.SystemType ==
                        StationSystemType.MedicalModule &&
                    string.Equals(
                        item.ObjectId,
                        ObjectId,
                        StringComparison.OrdinalIgnoreCase));

            Assert.That(
                medicalIdentity.GetComponentInChildren<Collider>(true),
                Is.Null);
            Assert.That(
                medicalIdentity.GetComponentsInChildren<Transform>(true)
                    .All(item => item.gameObject.layer ==
                        LayerMask.NameToLayer("Default")),
                Is.True);
        }

        [Test]
        public void PlayerStationContainsMedicalModuleInstance()
        {
            Scene scene = SceneManager.GetSceneByPath(PlayerScenePath);
            bool alreadyLoaded = scene.IsValid() && scene.isLoaded;
            if (!alreadyLoaded)
            {
                scene = EditorSceneManager.OpenScene(
                    PlayerScenePath,
                    OpenSceneMode.Additive);
            }

            try
            {
                GameObject medical = scene.GetRootGameObjects()
                    .FirstOrDefault(item =>
                        item.name == "Station_MedicalModule");
                Assert.That(medical, Is.Not.Null);
                StationObjectIdentity identity =
                    medical.GetComponent<StationObjectIdentity>();
                Assert.That(identity, Is.Not.Null);
                Assert.That(identity.ObjectId, Is.EqualTo(ObjectId));
            }
            finally
            {
                if (!alreadyLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
