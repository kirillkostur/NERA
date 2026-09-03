using Climbing;
using NERA.Inventory;
using NERA.Research;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NERA.Editor
{
    public static class PermanentEquipmentFlowSetup
    {
        private const string PlayerPrefab =
            "Assets/_Project/NERA/Prefabs/Player/Player.prefab";
        private const string HudPrefab =
            "Assets/_Project/NERA/Prefabs/UI/P_HUD_Canvas.prefab";
        private const string InventoryScreenPrefab =
            "Assets/_Project/NERA/Prefabs/UI/Screens/P_Screen_Inventory.prefab";
        private const string LaboratoryScreenPrefab =
            "Assets/_Project/NERA/Prefabs/UI/Screens/P_Screen_Laboratory.prefab";
        private const string TerminalScreenPrefab =
            "Assets/_Project/NERA/Prefabs/UI/Screens/P_Screen_Terminal.prefab";
        private const string LaboratoryTablePrefab =
            "Assets/_Project/NERA/Prefabs/Station/Station_LaboratoryTable.prefab";
        private const string GameplayCameraName = "FreeLookCam";
        private const string InventoryCameraName = "InventoryCamera";

        [MenuItem("NERA/Setup/Configure Permanent Equipment Flow")]
        public static void Configure()
        {
            ConfigurePlayer();
            ConfigureInventoryScreen();
            RemoveNamedObjects(
                HudPrefab,
                "Slot_Invent_Equipment");
            RemoveNamedObjects(
                LaboratoryScreenPrefab,
                "PowerScreen",
                "PowerMapButton",
                "background_Screen_Storage_Slot_Equipment",
                "background_Screen_Storage_Slot_Invent_Equipment");
            RemoveNamedObjects(
                TerminalScreenPrefab,
                "EquipmentMapButton",
                "background_Screen_Storage_Slot_Equipment",
                "background_Screen_Storage_Slot_Invent_Equipment");
            RemoveNamedObjects(
                LaboratoryTablePrefab,
                "Slot_Power");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void ConfigurePlayer()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
            try
            {
                PlayerInventory inventory =
                    root.GetComponentInChildren<PlayerInventory>(true);
                if (inventory == null)
                    return;

                if (inventory.GetComponent<PlayerStationEquipmentCharger>() == null)
                {
                    inventory.gameObject
                        .AddComponent<PlayerStationEquipmentCharger>();
                }
                ConfigureInventoryCamera(root);

                Save(root, PlayerPrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureInventoryCamera(GameObject playerRoot)
        {
            SwitchCameras cameraSwitch = playerRoot
                .GetComponentInChildren<SwitchCameras>(true);
            if (cameraSwitch == null)
                return;

            CinemachineVirtualCameraBase gameplayCamera = null;
            CinemachineVirtualCameraBase slideCamera = null;
            CinemachineVirtualCameraBase inventoryCamera = null;
            CinemachineVirtualCameraBase[] cameras = playerRoot
                .GetComponentsInChildren<CinemachineVirtualCameraBase>(true);
            foreach (CinemachineVirtualCameraBase camera in cameras)
            {
                if (camera.gameObject.name == GameplayCameraName)
                    gameplayCamera = camera;
                else if (camera.gameObject.name == "SlideCam")
                    slideCamera = camera;
                else if (camera.gameObject.name == InventoryCameraName)
                    inventoryCamera = camera;
            }

            if (gameplayCamera == null || inventoryCamera == null)
                return;

            SerializedObject serializedSwitch =
                new SerializedObject(cameraSwitch);
            serializedSwitch.FindProperty("FreeLook")
                .objectReferenceValue = gameplayCamera;
            serializedSwitch.FindProperty("Slide")
                .objectReferenceValue = slideCamera;
            serializedSwitch.FindProperty("Inventory")
                .objectReferenceValue = inventoryCamera;
            serializedSwitch.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureInventoryScreen()
        {
            GameObject root =
                PrefabUtility.LoadPrefabContents(InventoryScreenPrefab);
            try
            {
                Transform existing =
                    Find(root.transform, "BackContainerMountDropTarget");
                GameObject target = existing != null
                    ? existing.gameObject
                    : new GameObject(
                        "BackContainerMountDropTarget",
                        typeof(RectTransform),
                        typeof(CanvasRenderer),
                        typeof(Image),
                        typeof(LaboratoryItemDropSlot));
                if (target.transform.parent != root.transform)
                    target.transform.SetParent(root.transform, false);

                target.layer = root.layer;
                RectTransform rect = (RectTransform)target.transform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(360f, 420f);
                rect.anchoredPosition = new Vector2(0f, -70f);
                rect.localRotation = Quaternion.identity;
                rect.localScale = Vector3.one;

                Image image = target.GetComponent<Image>();
                image.color = Color.clear;
                image.raycastTarget = true;
                ConfigureMountedContainerDrag(target);
                target.transform.SetAsLastSibling();
                Save(root, InventoryScreenPrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureMountedContainerDrag(GameObject target)
        {
            Transform existing = target.transform.Find(
                "MountedAnomalyContainerDrag");
            GameObject dragTarget = existing != null
                ? existing.gameObject
                : new GameObject(
                    "MountedAnomalyContainerDrag",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(CanvasGroup),
                    typeof(LaboratoryInventoryItemDrag));
            if (dragTarget.transform.parent != target.transform)
                dragTarget.transform.SetParent(target.transform, false);

            dragTarget.layer = target.layer;
            RectTransform rect = (RectTransform)dragTarget.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;

            Image image = dragTarget.GetComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = true;
            dragTarget.SetActive(false);
            dragTarget.transform.SetAsLastSibling();
        }

        private static void RemoveNamedObjects(
            string prefabPath,
            params string[] names)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                foreach (string objectName in names)
                {
                    Transform found;
                    while ((found = Find(root.transform, objectName)) != null)
                        Object.DestroyImmediate(found.gameObject);
                }
                Save(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void Save(GameObject root, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }

        private static Transform Find(Transform root, string objectName)
        {
            if (root == null)
                return null;
            if (root.name == objectName)
                return root;

            for (int index = 0; index < root.childCount; index++)
            {
                Transform result = Find(root.GetChild(index), objectName);
                if (result != null)
                    return result;
            }
            return null;
        }
    }
}
