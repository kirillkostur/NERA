using NERA.Inventory;
using NERA.Research;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NERA.EditorTools
{
    public static class InventoryLabHUDInstaller
    {
        private static readonly Color Dark = new Color(0.015f, 0.06f, 0.08f, 0.96f);
        private static readonly Color Panel = new Color(0.025f, 0.10f, 0.13f, 0.97f);
        private static readonly Color Slot = new Color(0.10f, 0.18f, 0.21f, 1f);
        private static readonly Color Accent = new Color(0.04f, 0.42f, 0.50f, 1f);

        [MenuItem("NERA/Setup/Rebuild Inventory and Laboratory HUD")]
        public static void Rebuild()
        {
            InventoryAssetInstaller.CreateOrUpdateAssets();

            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != "Assets/_Project/NERA/Scenes/Boot/Boot.unity")
            {
                Debug.LogError("Open the Boot scene before rebuilding the Inventory/Laboratory HUD.");
                return;
            }

            GameObject canvasObject = GameObject.Find("RuntimeRoot/HUD_Canvas");
            if (canvasObject == null)
            {
                Debug.LogError("RuntimeRoot/HUD_Canvas was not found in Boot.");
                return;
            }

            InventoryLabHUDController existing =
                canvasObject.GetComponent<InventoryLabHUDController>();
            if (existing != null)
                Object.DestroyImmediate(existing);

            DeleteChild(canvasObject.transform, "QuickAccessHUD");
            DeleteChild(canvasObject.transform, "InventoryHint");
            DeleteChild(canvasObject.transform, "InventoryPanel");
            DeleteChild(canvasObject.transform, "LaboratoryPanel");

            BuildQuickAccess(canvasObject.transform);
            BuildInventory(canvasObject.transform);
            BuildLaboratory(canvasObject.transform);
            canvasObject.AddComponent<InventoryLabHUDController>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = canvasObject;
            Debug.Log("NERA: Inventory and Laboratory HUD rebuilt on Boot/HUD_Canvas.");
        }

        private static void BuildQuickAccess(Transform canvas)
        {
            GameObject root = CreatePanel(
                "QuickAccessHUD",
                canvas,
                Dark,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 52f),
                new Vector2(300f, 86f)
            );

            for (int i = 0; i < PlayerInventory.QuickAccessCapacity; i++)
            {
                GameObject slot = CreateButton(
                    $"Slot_{i + 1}",
                    root.transform,
                    string.Empty,
                    new Vector2(-86f + i * 86f, -5f),
                    new Vector2(74f, 66f),
                    Slot,
                    13
                );
                CreateItemIcon(slot.transform, new Vector2(58f, 50f));
                TextMeshProUGUI number = CreateText(
                    "Number",
                    slot.transform,
                    (i + 1).ToString(),
                    16,
                    new Vector2(-25f, -21f),
                    new Vector2(20f, 20f)
                );
                number.fontStyle = FontStyles.Bold;
            }

            TextMeshProUGUI inventoryHint = CreateText(
                "InventoryHint",
                canvas,
                "INVENTORY [I]",
                13,
                new Vector2(-90f, 28f),
                new Vector2(160f, 24f)
            );
            ConfigureRect(
                inventoryHint.rectTransform,
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-90f, 28f),
                new Vector2(160f, 24f)
            );
            inventoryHint.alignment = TextAlignmentOptions.MidlineRight;
        }

        private static void BuildInventory(Transform canvas)
        {
            GameObject root = CreatePanel(
                "InventoryPanel",
                canvas,
                Panel,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-230f, 0f),
                new Vector2(420f, 620f)
            );

            CreateText("Title", root.transform, "INVENTORY [I]", 24, new Vector2(0f, 275f), new Vector2(360f, 42f));

            GameObject selectionPanel = CreatePanel(
                "SelectionPanel",
                root.transform,
                Dark,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 190f),
                new Vector2(360f, 120f)
            );
            TextMeshProUGUI selectionName = CreateText(
                "Name",
                selectionPanel.transform,
                "SELECT AN ITEM",
                17,
                new Vector2(0f, 38f),
                new Vector2(330f, 28f)
            );
            selectionName.fontStyle = FontStyles.Bold;
            TextMeshProUGUI selectionDescription = CreateText(
                "Description",
                selectionPanel.transform,
                string.Empty,
                14,
                new Vector2(0f, -17f),
                new Vector2(330f, 70f)
            );
            selectionDescription.alignment = TextAlignmentOptions.TopLeft;

            GameObject backpack = CreateContainer(
                "Backpack",
                root.transform,
                new Vector2(0f, 20f),
                new Vector2(360f, 200f)
            );
            CreateText("Title", backpack.transform, "BACKPACK  5 SLOTS", 15, new Vector2(0f, 84f), new Vector2(330f, 26f));

            GameObject anomalies = CreateContainer(
                "Anomalies",
                root.transform,
                new Vector2(0f, -145f),
                new Vector2(360f, 115f)
            );
            CreateText("Title", anomalies.transform, "ANOMALIES  3 SLOTS", 15, new Vector2(0f, 43f), new Vector2(330f, 26f));

            for (int i = 0; i < PlayerInventory.AnomalyCapacity; i++)
            {
                GameObject slot = CreateButton(
                    $"Slot_{i + 1}",
                    anomalies.transform,
                    string.Empty,
                    new Vector2(-105f + i * 105f, -13f),
                    new Vector2(92f, 62f),
                    Slot,
                    12
                );
                CreateItemIcon(slot.transform, new Vector2(76f, 48f));
            }

            CreateButton(
                "DropButton",
                root.transform,
                "DROP SELECTED",
                new Vector2(0f, -274f),
                new Vector2(250f, 42f),
                Accent,
                13
            );
            root.SetActive(false);
        }

        private static void BuildLaboratory(Transform canvas)
        {
            GameObject root = CreatePanel(
                "LaboratoryPanel",
                canvas,
                Panel,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(-240f, 25f),
                new Vector2(460f, 430f)
            );

            CreateText("Title", root.transform, "LABORATORY", 27, new Vector2(0f, 170f), new Vector2(390f, 44f));
            CreateText(
                "Status",
                root.transform,
                "Laboratory ready.",
                15,
                new Vector2(0f, 112f),
                new Vector2(390f, 46f)
            );

            GameObject sampleSlot = CreatePanel(
                "SampleSlot",
                root.transform,
                Slot,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 25f),
                new Vector2(240f, 130f)
            );
            sampleSlot.AddComponent<LaboratoryItemDropSlot>();
            CreateItemIcon(sampleSlot.transform, new Vector2(105f, 105f));

            CreateButton(
                "ScanButton",
                root.transform,
                "START SCAN",
                new Vector2(0f, -82f),
                new Vector2(250f, 50f),
                Accent,
                15
            );
            CreateButton(
                "TakeButton",
                root.transform,
                "TAKE SAMPLE",
                new Vector2(0f, -140f),
                new Vector2(250f, 42f),
                Slot,
                14
            );
            CreateButton(
                "CloseButton",
                root.transform,
                "CLOSE [ESC]",
                new Vector2(0f, -185f),
                new Vector2(250f, 38f),
                Slot,
                13
            );
            root.SetActive(false);
        }

        private static GameObject CreateContainer(
            string name,
            Transform parent,
            Vector2 position,
            Vector2 size
        )
        {
            GameObject container = new GameObject(name, typeof(RectTransform));
            container.transform.SetParent(parent, false);
            ConfigureRect(container.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);
            return container;
        }

        private static GameObject CreatePanel(
            string name,
            Transform parent,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 position,
            Vector2 size
        )
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            ConfigureRect(panel.GetComponent<RectTransform>(), anchorMin, anchorMax, position, size);
            panel.GetComponent<Image>().color = color;
            return panel;
        }

        private static GameObject CreateButton(
            string name,
            Transform parent,
            string label,
            Vector2 position,
            Vector2 size,
            Color color,
            int fontSize
        )
        {
            GameObject buttonObject = CreatePanel(
                name,
                parent,
                color,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                position,
                size
            );
            buttonObject.AddComponent<Button>();
            if (!string.IsNullOrEmpty(label))
            {
                CreateText(
                    "Label",
                    buttonObject.transform,
                    label,
                    fontSize,
                    Vector2.zero,
                    size - new Vector2(10f, 6f)
                );
            }
            return buttonObject;
        }

        private static Image CreateItemIcon(Transform parent, Vector2 size)
        {
            GameObject iconObject = new GameObject(
                "Icon",
                typeof(RectTransform),
                typeof(Image)
            );
            iconObject.transform.SetParent(parent, false);
            ConfigureRect(
                iconObject.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                size
            );

            Image image = iconObject.GetComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.enabled = false;
            return image;
        }

        private static TextMeshProUGUI CreateText(
            string name,
            Transform parent,
            string value,
            int fontSize,
            Vector2 position,
            Vector2 size
        )
        {
            GameObject textObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(TextMeshProUGUI)
            );
            textObject.transform.SetParent(parent, false);
            ConfigureRect(
                textObject.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                position,
                size
            );

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            text.color = Color.white;
            text.text = value;
            return text;
        }

        private static void ConfigureRect(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 position,
            Vector2 size
        )
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void DeleteChild(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            if (child != null)
                Object.DestroyImmediate(child.gameObject);
        }
    }
}
