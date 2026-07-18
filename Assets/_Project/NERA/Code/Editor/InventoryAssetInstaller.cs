using NERA.Inventory;
using NERA.Research;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace NERA.EditorTools
{
    public static class InventoryAssetInstaller
    {
        private const string PrefabFolder =
            "Assets/_Project/NERA/Prefabs/UI";
        private const string PrefabPath =
            PrefabFolder + "/P_InventorySlot.prefab";
        private const string ConfigFolder =
            "Assets/_Project/NERA/Resources/Inventory";
        private const string ConfigPath =
            ConfigFolder + "/DefaultInventoryConfig.asset";

        private static readonly Color SlotColor =
            new Color(0.10f, 0.18f, 0.21f, 1f);

        [MenuItem("NERA/Setup/Create Inventory Config And Slot Prefab")]
        public static void CreateOrUpdateAssets()
        {
            EnsureFolder(PrefabFolder);
            EnsureFolder(ConfigFolder);

            InventorySlotView slotPrefab = CreateSlotPrefab();
            InventoryConfig config =
                AssetDatabase.LoadAssetAtPath<InventoryConfig>(ConfigPath);

            if (config == null)
            {
                config = ScriptableObject.CreateInstance<InventoryConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            SerializedObject serialized = new SerializedObject(config);
            serialized.FindProperty("slotPrefab").objectReferenceValue = slotPrefab;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = config;
            Debug.Log(
                "NERA: InventoryConfig and reusable TMP inventory-slot prefab are ready."
            );
        }

        private static InventorySlotView CreateSlotPrefab()
        {
            InventorySlotView existing =
                AssetDatabase.LoadAssetAtPath<InventorySlotView>(PrefabPath);
            if (existing != null)
                return existing;

            GameObject root = new GameObject(
                "P_InventorySlot",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(CanvasGroup),
                typeof(LaboratoryInventoryItemDrag),
                typeof(InventorySlotView)
            );

            try
            {
                RectTransform rect = root.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(92f, 68f);

                Image background = root.GetComponent<Image>();
                background.color = SlotColor;
                root.GetComponent<Button>().targetGraphic = background;

                GameObject iconObject = new GameObject(
                    "Icon",
                    typeof(RectTransform),
                    typeof(Image)
                );
                iconObject.transform.SetParent(root.transform, false);
                RectTransform iconRect = iconObject.GetComponent<RectTransform>();
                iconRect.anchorMin = Vector2.zero;
                iconRect.anchorMax = Vector2.one;
                iconRect.offsetMin = new Vector2(8f, 8f);
                iconRect.offsetMax = new Vector2(-8f, -8f);
                Image icon = iconObject.GetComponent<Image>();
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                icon.enabled = false;

                GameObject numberObject = new GameObject(
                    "Number",
                    typeof(RectTransform),
                    typeof(TextMeshProUGUI)
                );
                numberObject.transform.SetParent(root.transform, false);
                RectTransform numberRect =
                    numberObject.GetComponent<RectTransform>();
                numberRect.anchorMin = Vector2.zero;
                numberRect.anchorMax = Vector2.zero;
                numberRect.pivot = Vector2.zero;
                numberRect.anchoredPosition = new Vector2(5f, 4f);
                numberRect.sizeDelta = new Vector2(24f, 22f);

                TextMeshProUGUI number =
                    numberObject.GetComponent<TextMeshProUGUI>();
                if (TMP_Settings.defaultFontAsset != null)
                    number.font = TMP_Settings.defaultFontAsset;
                number.text = "1";
                number.fontSize = 16f;
                number.fontStyle = FontStyles.Bold;
                number.alignment = TextAlignmentOptions.BottomLeft;
                number.color = Color.white;
                number.raycastTarget = false;

                GameObject prefab =
                    PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                return prefab.GetComponent<InventorySlotView>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
