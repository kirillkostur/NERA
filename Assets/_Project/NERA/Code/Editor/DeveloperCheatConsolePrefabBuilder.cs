using System;
using System.Collections.Generic;
using System.Linq;
using NERA.Development;
using NERA.Items;
using NERA.Station;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NERA.EditorTools
{
    public static class DeveloperCheatConsolePrefabBuilder
    {
        public const string PrefabPath =
            "Assets/_Project/NERA/Prefabs/Developer/" +
            "P_DeveloperCheatConsole.prefab";
        public const string MainScenePath =
            "Assets/_Project/NERA/Scenes/MainScene.unity";

        private const string EngineeringPartsFolder =
            "Assets/_Project/NERA/Configs/Items/Item_EngineeringPart";
        private const string EquipmentFolder =
            "Assets/_Project/NERA/Configs/Items/Item_Equipment";
        private static readonly string[] IoPrefabPaths =
        {
            "Assets/_Project/NERA/Prefabs/IO/IO_Blue_Weak.prefab",
            "Assets/_Project/NERA/Prefabs/IO/IO_Green_Regenerator.prefab",
            "Assets/_Project/NERA/Prefabs/IO/IO_Yellow_Hunter.prefab",
            "Assets/_Project/NERA/Prefabs/IO/IO_Red_Enforcer.prefab",
            "Assets/_Project/NERA/Prefabs/IO/IO_Violet_Overseer.prefab"
        };
        private static readonly string[] IoButtonLabels =
        {
            "B",
            "G",
            "Y",
            "R",
            "V"
        };
        private static readonly Color[] IoButtonColors =
        {
            new Color(0.08f, 0.42f, 1f, 1f),
            new Color(0.12f, 0.82f, 0.30f, 1f),
            new Color(1f, 0.78f, 0.08f, 1f),
            new Color(0.95f, 0.10f, 0.10f, 1f),
            new Color(0.62f, 0.18f, 0.92f, 1f)
        };

        private static readonly string[] CheatEquipmentItemIds =
        {
            "energy_pistol_01",
            "io_integrator_01",
            "anomaly_container_01"
        };

        private static readonly Color PanelColor =
            new Color(0.025f, 0.035f, 0.055f, 0.96f);
        private static readonly Color ButtonColor =
            new Color(0.10f, 0.14f, 0.20f, 1f);
        private static readonly Color ButtonHighlightColor =
            new Color(0.18f, 0.30f, 0.42f, 1f);
        private static readonly Color AccentColor =
            new Color(0.26f, 0.75f, 0.92f, 1f);
        private static readonly Color EnableColor =
            new Color(0.10f, 0.95f, 0.24f, 1f);
        private static readonly Color DisableColor =
            new Color(1f, 0.12f, 0.12f, 1f);
        private static readonly Color TextColor =
            new Color(0.93f, 0.96f, 1f, 1f);

        private static readonly ItemGroupLayout[] ItemGroups =
        {
            new ItemGroupLayout("ДРОН:", StationSystemType.Drone),
            new ItemGroupLayout("АНТЕННА:", StationSystemType.Antenna),
            new ItemGroupLayout("ПАНЕЛЬ:", StationSystemType.SolarPanel),
            new ItemGroupLayout("БАТАРЕЯ:", StationSystemType.Battery),
            new ItemGroupLayout("ТУРЕЛИ:", StationSystemType.Turret)
        };

        [InitializeOnLoadMethod]
        private static void RebuildMissingPrefabAfterCompilation()
        {
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode &&
                    AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
                {
                    Rebuild();
                }
            };
        }

        [MenuItem("NERA/Developer/Rebuild Cheat Console")]
        public static void Rebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError(
                    "Developer cheat console cannot be rebuilt in Play Mode.");
                return;
            }

            EnsureFolder("Assets/_Project/NERA/Prefabs/Developer");
            ItemData[] items = LoadInventoryItems();
            GameObject root = BuildPrefabContents(items);
            try
            {
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                    root,
                    PrefabPath);
                if (prefab == null)
                    throw new InvalidOperationException("Prefab save failed.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            InstallInMainScene();
            Debug.Log(
                $"Developer cheat console rebuilt: {PrefabPath}. " +
                $"Static item buttons: {items.Length}.");
        }

        [MenuItem("NERA/Developer/Open Cheat Console (Play Mode)")]
        private static void OpenInPlayMode()
        {
            DeveloperCheatConsoleController controller =
                UnityEngine.Object.FindFirstObjectByType<
                    DeveloperCheatConsoleController>();
            if (controller == null)
            {
                Debug.LogWarning(
                    "Developer cheat console is unavailable in the current " +
                    "Play Mode session.");
                return;
            }

            controller.SetOpen(true);
        }

        [MenuItem("NERA/Developer/Open Cheat Console (Play Mode)", true)]
        private static bool CanOpenInPlayMode()
        {
            return EditorApplication.isPlaying;
        }

        private static GameObject BuildPrefabContents(ItemData[] items)
        {
            var root = new GameObject(
                "DeveloperCheatConsole",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(DeveloperCheatConsoleController));
            RectTransform rootRect = root.GetComponent<RectTransform>();
            Stretch(rootRect);

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode =
                CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            GameObject window = CreatePanel(root.transform, "CheatWindow");
            CreateLabel(
                window.transform,
                "Header",
                "NERA — ЧИТЫ",
                30f,
                18f,
                250f,
                44f,
                28,
                TextAnchor.MiddleLeft,
                AccentColor);
            CreateLabel(
                window.transform,
                "ShortcutHint",
                "` — ОТКРЫТЬ  •  ESC — ЗАКРЫТЬ",
                1420f,
                18f,
                460f,
                44f,
                18,
                TextAnchor.MiddleRight,
                new Color(0.65f, 0.72f, 0.82f, 1f));

            Button home = CreateButton(
                window.transform,
                "HomeButton",
                "ДОМОЙ",
                30f,
                72f,
                220f,
                48f);
            Button clean = CreateButton(
                window.transform,
                "CleanButton",
                "ОЧИСТИТЬ",
                300f,
                72f,
                220f,
                48f);
            Button clear = CreateButton(
                window.transform,
                "ClearWeatherButton",
                "ЯСНО",
                540f,
                72f,
                220f,
                48f);
            Button sandstorm = CreateButton(
                window.transform,
                "SandstormButton",
                "ПЕСОК",
                780f,
                72f,
                220f,
                48f);
            Button contaminate = CreateButton(
                window.transform,
                "ContaminateButton",
                "ЗАГРЯЗНИТЬ",
                1020f,
                72f,
                220f,
                48f);
            Button timer = CreateButton(
                window.transform,
                "CompleteTimersButton",
                "ТАЙМЕР+",
                1260f,
                72f,
                170f,
                48f);

            CreateBoxLabel(
                window.transform,
                "TerminalPowerLabel",
                "ТЕРМИНАЛ",
                1450f,
                72f,
                160f,
                48f,
                18);
            Button terminalEnable = CreateButton(
                window.transform,
                "TerminalEnableButton",
                "ВКЛ",
                1620f,
                72f,
                84f,
                48f,
                15,
                EnableColor,
                EnableColor);
            Button terminalDisable = CreateButton(
                window.transform,
                "TerminalDisableButton",
                "ВЫКЛ",
                1714f,
                72f,
                94f,
                48f,
                15,
                DisableColor,
                DisableColor);

            var batteryChargeButtons = new List<Button>(5);
            for (int index = 0; index < 5; index++)
            {
                batteryChargeButtons.Add(CreateButton(
                    window.transform,
                    $"BatteryChargeButton_{index * 25:000}",
                    $"{index * 25}%",
                    1820f,
                    150f + index * 56f,
                    70f,
                    44f,
                    16,
                    DisableColor,
                    DisableColor));
            }

            Button expeditionDropdownButton = CreateButton(
                window.transform,
                "ExpeditionDropdownButton",
                "ЭКСПЕДИЦИИ",
                30f,
                150f,
                220f,
                44f);
            Button signalDropdownButton = CreateButton(
                window.transform,
                "SignalDropdownButton",
                "СИГНАЛЫ",
                275f,
                150f,
                220f,
                44f);

            Button turretOne = CreateButton(
                window.transform,
                "UpgradeTurretOneButton",
                "ТУРЕЛЬ 1",
                540f,
                150f,
                160f,
                44f);
            Button turretTwo = CreateButton(
                window.transform,
                "UpgradeTurretTwoButton",
                "ТУРЕЛЬ 2",
                540f,
                206f,
                160f,
                44f);
            Button drone = CreateButton(
                window.transform,
                "UpgradeDroneButton",
                "ДРОН",
                540f,
                262f,
                160f,
                44f);
            Button antenna = CreateButton(
                window.transform,
                "UpgradeAntennaButton",
                "АНТЕННА",
                540f,
                318f,
                160f,
                44f);
            Button battery = CreateButton(
                window.transform,
                "UpgradeBatteryButton",
                "БАТАРЕЯ",
                540f,
                374f,
                160f,
                44f);
            Button solar = CreateButton(
                window.transform,
                "UpgradeSolarPanelButton",
                "ПАНЕЛЬ",
                540f,
                430f,
                160f,
                44f);

            var stationEnableButtons = new List<Button>(7);
            var stationDisableButtons = new List<Button>(7);
            string[] stationControlNames =
            {
                "TurretOne",
                "TurretTwo",
                "Drone",
                "Antenna",
                "Battery",
                "SolarPanel"
            };
            for (int index = 0; index < stationControlNames.Length; index++)
            {
                float y = 150f + index * 56f;
                stationEnableButtons.Add(CreateButton(
                    window.transform,
                    $"{stationControlNames[index]}EnableButton",
                    "ВКЛ",
                    710f,
                    y,
                    72f,
                    44f,
                    14,
                    EnableColor,
                    EnableColor));
                stationDisableButtons.Add(CreateButton(
                    window.transform,
                    $"{stationControlNames[index]}DisableButton",
                    "ВЫКЛ",
                    792f,
                    y,
                    82f,
                    44f,
                    14,
                    DisableColor,
                    DisableColor));
            }
            stationEnableButtons.Add(terminalEnable);
            stationDisableButtons.Add(terminalDisable);

            Button inventoryDropdownButton = CreateButton(
                window.transform,
                "InventoryDropdownButton",
                "ДЕТАЛИ В ИНВЕНТАРЬ",
                1040f,
                150f,
                400f,
                44f);

            Button language = CreateButton(
                window.transform,
                "LanguageButton",
                "ЯЗЫК",
                30f,
                1002f,
                220f,
                48f);

            GameObject[] ioPrefabs = IoPrefabPaths
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .ToArray();
            if (ioPrefabs.Any(prefab => prefab == null))
            {
                throw new InvalidOperationException(
                    "One or more IO cheat prefabs are missing.");
            }

            var spawnIoButtons = new List<Button>(ioPrefabs.Length);
            const float ioStartX = 540f;
            const float ioY = 992f;
            const float ioSize = 52f;
            const float ioStep = 62f;
            for (int index = 0; index < ioPrefabs.Length; index++)
            {
                Color labelColor = index == 2 ? Color.black : Color.white;
                spawnIoButtons.Add(CreateButton(
                    window.transform,
                    $"SpawnIoButton_{index:00}",
                    IoButtonLabels[index],
                    ioStartX + index * ioStep,
                    ioY,
                    ioSize,
                    ioSize,
                    19,
                    IoButtonColors[index],
                    labelColor,
                    IoButtonColors[index]));
            }

            Button killIo = CreateButton(
                window.transform,
                "KillIoButton",
                "УБИТЬ IO",
                ioStartX + ioPrefabs.Length * ioStep,
                ioY,
                132f,
                ioSize,
                16,
                DisableColor,
                DisableColor);

            GameObject expeditionDropdown = CreateDropdownPanel(
                window.transform,
                "ExpeditionDropdown",
                30f,
                202f,
                220f,
                364f);
            var expeditionButtons = new List<Button>(8);
            for (int index = 0; index < 8; index++)
            {
                int ordinal = index + 1;
                expeditionButtons.Add(CreateButton(
                    expeditionDropdown.transform,
                    $"ExpeditionButton_{ordinal:00}",
                    $"ЭКСПЕДИЦИЯ {ordinal}",
                    6f,
                    6f + index * 44f,
                    208f,
                    38f,
                    16));
            }

            GameObject signalDropdown = CreateDropdownPanel(
                window.transform,
                "SignalDropdown",
                275f,
                202f,
                220f,
                492f);
            var signalButtons = new List<Button>(12);
            for (int index = 0; index < 12; index++)
            {
                int ordinal = index + 1;
                signalButtons.Add(CreateButton(
                    signalDropdown.transform,
                    $"SignalButton_{ordinal:00}",
                    $"СИГНАЛ {ordinal}",
                    6f,
                    6f + index * 40f,
                    208f,
                    34f,
                    16));
            }

            GameObject inventoryDropdown = CreateDropdownPanel(
                window.transform,
                "InventoryDropdown",
                1040f,
                202f,
                820f,
                430f);
            var inventoryGroupButtons = new List<Button>(6);
            var inventoryGroupRoots = new List<GameObject>(6);
            var itemButtons = new List<Button>(items.Length);
            var orderedItems = new List<ItemData>(items.Length);
            var assignedItems = new HashSet<ItemData>();
            int itemIndex = 0;

            for (int groupIndex = 0;
                 groupIndex < ItemGroups.Length;
                 groupIndex++)
            {
                ItemGroupLayout group = ItemGroups[groupIndex];
                int column = groupIndex % 3;
                int row = groupIndex / 3;
                inventoryGroupButtons.Add(CreateButton(
                    inventoryDropdown.transform,
                    $"InventoryGroupButton_{group.SystemType}",
                    group.Label,
                    12f + column * 264f,
                    12f + row * 48f,
                    250f,
                    40f,
                    17));

                GameObject groupRoot = CreateDropdownPanel(
                    inventoryDropdown.transform,
                    $"InventoryGroupRoot_{group.SystemType}",
                    12f,
                    112f,
                    796f,
                    306f);
                inventoryGroupRoots.Add(groupRoot);

                ItemData[] groupItems = items
                    .Where(item =>
                        item != null &&
                        BelongsToSystem(item, group.SystemType) &&
                        !assignedItems.Contains(item))
                    .OrderBy(item => GetFirstSlotOrder(
                        item,
                        group.SystemType))
                    .ThenBy(item => item.ItemId, StringComparer.Ordinal)
                    .ToArray();
                for (int rowIndex = 0;
                     rowIndex < groupItems.Length;
                     rowIndex++)
                {
                    ItemData item = groupItems[rowIndex];
                    assignedItems.Add(item);
                    orderedItems.Add(item);
                    float itemY = 10f + rowIndex * 40f;
                    CreateLabel(
                        groupRoot.transform,
                        $"ItemLabel_{itemIndex:00}",
                        item.DisplayName.ToUpperInvariant(),
                        12f,
                        itemY,
                        708f,
                        34f,
                        16,
                        TextAnchor.MiddleRight,
                        TextColor);
                    itemButtons.Add(CreateButton(
                        groupRoot.transform,
                        $"GiveItemButton_{itemIndex:00}",
                        "+1",
                        728f,
                        itemY,
                        50f,
                        34f,
                        15));
                    itemIndex++;
                }
                groupRoot.SetActive(false);
            }

            const int equipmentGroupIndex = 5;
            inventoryGroupButtons.Add(CreateButton(
                inventoryDropdown.transform,
                "InventoryGroupButton_Equipment",
                "СНАРЯЖЕНИЕ:",
                12f + (equipmentGroupIndex % 3) * 264f,
                12f + (equipmentGroupIndex / 3) * 48f,
                250f,
                40f,
                17));
            GameObject equipmentRoot = CreateDropdownPanel(
                inventoryDropdown.transform,
                "InventoryGroupRoot_Equipment",
                12f,
                112f,
                796f,
                306f);
            inventoryGroupRoots.Add(equipmentRoot);
            for (int rowIndex = 0;
                 rowIndex < CheatEquipmentItemIds.Length;
                 rowIndex++)
            {
                string itemId = CheatEquipmentItemIds[rowIndex];
                ItemData item = items.Single(candidate =>
                    candidate != null && candidate.ItemId == itemId);
                assignedItems.Add(item);
                orderedItems.Add(item);
                float itemY = 10f + rowIndex * 40f;
                CreateLabel(
                    equipmentRoot.transform,
                    $"ItemLabel_{itemIndex:00}",
                    item.DisplayName.ToUpperInvariant(),
                    12f,
                    itemY,
                    708f,
                    34f,
                    16,
                    TextAnchor.MiddleRight,
                    TextColor);
                itemButtons.Add(CreateButton(
                    equipmentRoot.transform,
                    $"GiveItemButton_{itemIndex:00}",
                    "+1",
                    728f,
                    itemY,
                    50f,
                    34f,
                    15));
                itemIndex++;
            }
            equipmentRoot.SetActive(false);

            if (assignedItems.Count != items.Length)
            {
                string missingItemIds = string.Join(
                    ", ",
                    items
                        .Where(item => item != null &&
                            !assignedItems.Contains(item))
                        .Select(item => item.ItemId));
                throw new InvalidOperationException(
                    "Inventory items are missing a supported cheat-console " +
                    $"group: {missingItemIds}.");
            }

            ConfigureController(
                root.GetComponent<DeveloperCheatConsoleController>(),
                window,
                home,
                clean,
                clear,
                sandstorm,
                contaminate,
                timer,
                batteryChargeButtons,
                expeditionDropdownButton,
                expeditionDropdown,
                expeditionButtons,
                signalDropdownButton,
                signalDropdown,
                signalButtons,
                turretOne,
                turretTwo,
                drone,
                antenna,
                battery,
                solar,
                stationEnableButtons,
                stationDisableButtons,
                inventoryDropdownButton,
                inventoryDropdown,
                inventoryGroupButtons,
                inventoryGroupRoots,
                itemButtons,
                orderedItems,
                spawnIoButtons,
                killIo,
                ioPrefabs,
                language);

            expeditionDropdown.SetActive(false);
            signalDropdown.SetActive(false);
            inventoryDropdown.SetActive(false);
            window.SetActive(false);
            return root;
        }

        private static void ConfigureController(
            DeveloperCheatConsoleController controller,
            GameObject window,
            Button home,
            Button clean,
            Button clear,
            Button sandstorm,
            Button contaminate,
            Button timer,
            IReadOnlyList<Button> batteryChargeButtons,
            Button expeditionDropdownButton,
            GameObject expeditionDropdown,
            IReadOnlyList<Button> expeditionButtons,
            Button signalDropdownButton,
            GameObject signalDropdown,
            IReadOnlyList<Button> signalButtons,
            Button turretOne,
            Button turretTwo,
            Button drone,
            Button antenna,
            Button battery,
            Button solar,
            IReadOnlyList<Button> stationEnableButtons,
            IReadOnlyList<Button> stationDisableButtons,
            Button inventoryDropdownButton,
            GameObject inventoryDropdown,
            IReadOnlyList<Button> inventoryGroupButtons,
            IReadOnlyList<GameObject> inventoryGroupRoots,
            IReadOnlyList<Button> itemButtons,
            IReadOnlyList<ItemData> items,
            IReadOnlyList<Button> spawnIoButtons,
            Button killIo,
            IReadOnlyList<GameObject> ioPrefabs,
            Button language)
        {
            var serialized = new SerializedObject(controller);
            SetReference(serialized, "windowRoot", window);
            SetReference(serialized, "homeButton", home);
            SetReference(serialized, "cleanButton", clean);
            SetReference(serialized, "clearWeatherButton", clear);
            SetReference(serialized, "sandstormButton", sandstorm);
            SetReference(serialized, "contaminateButton", contaminate);
            SetReference(serialized, "timerButton", timer);
            SetReferences(
                serialized,
                "batteryChargeButtons",
                batteryChargeButtons);

            SetReference(serialized, "languageButton", language);
            SetReference(
                serialized,
                "expeditionDropdownButton",
                expeditionDropdownButton);
            SetReference(
                serialized,
                "expeditionDropdownRoot",
                expeditionDropdown);
            SetReferences(serialized, "expeditionButtons", expeditionButtons);
            SetReference(
                serialized,
                "signalDropdownButton",
                signalDropdownButton);
            SetReference(serialized, "signalDropdownRoot", signalDropdown);
            SetReferences(serialized, "signalButtons", signalButtons);
            SetReference(serialized, "turretOneButton", turretOne);
            SetReference(serialized, "turretTwoButton", turretTwo);
            SetReference(serialized, "droneButton", drone);
            SetReference(serialized, "antennaButton", antenna);
            SetReference(serialized, "batteryButton", battery);
            SetReference(serialized, "solarPanelButton", solar);
            SetReferences(
                serialized,
                "stationEnableButtons",
                stationEnableButtons);
            SetReferences(
                serialized,
                "stationDisableButtons",
                stationDisableButtons);
            SetReference(
                serialized,
                "inventoryDropdownButton",
                inventoryDropdownButton);
            SetReference(
                serialized,
                "inventoryDropdownRoot",
                inventoryDropdown);
            SetReferences(
                serialized,
                "inventoryGroupButtons",
                inventoryGroupButtons);
            SetReferences(
                serialized,
                "inventoryGroupRoots",
                inventoryGroupRoots);
            SetReferences(serialized, "itemButtons", itemButtons);
            SetReferences(serialized, "inventoryItems", items);
            SetReferences(serialized, "spawnIoButtons", spawnIoButtons);
            SetReference(serialized, "killIoButton", killIo);
            SetReferences(serialized, "ioEnemyPrefabs", ioPrefabs);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject CreatePanel(Transform parent, string name)
        {
            var panel = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            panel.transform.SetParent(parent, false);
            Stretch(panel.GetComponent<RectTransform>());
            Image image = panel.GetComponent<Image>();
            image.color = PanelColor;
            image.raycastTarget = true;
            return panel;
        }

        private static GameObject CreateDropdownPanel(
            Transform parent,
            string name,
            float x,
            float y,
            float width,
            float height)
        {
            var panel = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Outline));
            panel.transform.SetParent(parent, false);
            SetTopLeftRect(
                panel.GetComponent<RectTransform>(),
                x,
                y,
                width,
                height);
            Image image = panel.GetComponent<Image>();
            image.color = new Color(0.035f, 0.05f, 0.075f, 0.995f);
            image.raycastTarget = true;
            Outline outline = panel.GetComponent<Outline>();
            outline.effectColor = AccentColor;
            outline.effectDistance = new Vector2(1f, -1f);
            return panel;
        }

        private static void CreateBoxLabel(
            Transform parent,
            string name,
            string label,
            float x,
            float y,
            float width,
            float height,
            int textScale)
        {
            var box = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Outline));
            box.transform.SetParent(parent, false);
            SetTopLeftRect(
                box.GetComponent<RectTransform>(),
                x,
                y,
                width,
                height);
            box.GetComponent<Image>().color = ButtonColor;
            Outline outline = box.GetComponent<Outline>();
            outline.effectColor = AccentColor;
            outline.effectDistance = new Vector2(1f, -1f);

            Text text = CreateTextObject(
                box.transform,
                "Label",
                label,
                textScale,
                TextAnchor.MiddleCenter,
                TextColor);
            Stretch(text.rectTransform);
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            string label,
            float x,
            float y,
            float width,
            float height,
            int textScale = 19,
            Color? outlineColor = null,
            Color? labelColor = null,
            Color? backgroundColor = null)
        {
            var buttonObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Outline),
                typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            SetTopLeftRect(
                buttonObject.GetComponent<RectTransform>(),
                x,
                y,
                width,
                height);

            Color background = backgroundColor ?? ButtonColor;
            Image image = buttonObject.GetComponent<Image>();
            image.color = background;
            Outline outline = buttonObject.GetComponent<Outline>();
            Color accent = outlineColor ?? AccentColor;
            outline.effectColor = accent;
            outline.effectDistance = new Vector2(1f, -1f);

            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = background;
            colors.highlightedColor = Color.Lerp(
                background,
                Color.white,
                0.18f);
            colors.pressedColor = Color.Lerp(
                background,
                Color.black,
                0.25f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.08f, 0.09f, 0.11f, 0.6f);
            colors.colorMultiplier = 1f;
            button.colors = colors;

            Text text = CreateTextObject(
                buttonObject.transform,
                "Label",
                label,
                textScale,
                TextAnchor.MiddleCenter,
                labelColor ?? TextColor);
            Stretch(text.rectTransform);
            text.raycastTarget = false;
            return button;
        }

        private static Text CreateLabel(
            Transform parent,
            string name,
            string value,
            float x,
            float y,
            float width,
            float height,
            int textScale,
            TextAnchor alignment,
            Color color)
        {
            Text text = CreateTextObject(
                parent,
                name,
                value,
                textScale,
                alignment,
                color);
            SetTopLeftRect(text.rectTransform, x, y, width, height);
            return text;
        }

        private static Text CreateTextObject(
            Transform parent,
            string name,
            string value,
            int textScale,
            TextAnchor alignment,
            Color color)
        {
            var textObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));
            textObject.transform.SetParent(parent, false);
            Text text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;

            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static void SetTopLeftRect(
            RectTransform rect,
            float x,
            float y,
            float width,
            float height)
        {
            rect.anchorMin = Vector2.up;
            rect.anchorMax = Vector2.up;
            rect.pivot = Vector2.up;
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static ItemData[] LoadInventoryItems()
        {
            ItemData[] engineeringParts = AssetDatabase.FindAssets(
                    "t:ItemData",
                    new[] { EngineeringPartsFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ItemData>)
                .Where(item => item != null &&
                    item.ItemType == ItemType.EngineeringPart)
                .OrderBy(item => item.ItemId, StringComparer.Ordinal)
                .ToArray();

            Dictionary<string, ItemData> equipmentById = AssetDatabase.FindAssets(
                    "t:ItemData",
                    new[] { EquipmentFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ItemData>)
                .Where(item => item != null &&
                    item.ItemType == ItemType.Equipment &&
                    CheatEquipmentItemIds.Contains(item.ItemId))
                .ToDictionary(item => item.ItemId, StringComparer.Ordinal);

            string[] missingEquipmentIds = CheatEquipmentItemIds
                .Where(itemId => !equipmentById.ContainsKey(itemId))
                .ToArray();
            if (missingEquipmentIds.Length > 0)
            {
                throw new InvalidOperationException(
                    "Cheat-console equipment items are missing: " +
                    string.Join(", ", missingEquipmentIds));
            }

            return engineeringParts
                .Concat(CheatEquipmentItemIds.Select(itemId =>
                    equipmentById[itemId]))
                .ToArray();
        }

        private static bool BelongsToSystem(
            ItemData item,
            StationSystemType systemType)
        {
            return item?.EngineeringPartDefinition?.CompatibleInstallations
                .Any(compatibility =>
                    compatibility != null &&
                    compatibility.SystemType == systemType) == true;
        }

        private static int GetFirstSlotOrder(
            ItemData item,
            StationSystemType systemType)
        {
            int bestOrder = int.MaxValue;
            IReadOnlyList<EngineeringPartCompatibility> installations =
                item?.EngineeringPartDefinition?.CompatibleInstallations;
            if (installations == null)
                return bestOrder;

            foreach (EngineeringPartCompatibility compatibility in installations)
            {
                if (compatibility == null ||
                    compatibility.SystemType != systemType)
                {
                    continue;
                }

                string slotId = compatibility.SlotId;
                int separator = slotId.LastIndexOf('_');
                if (separator >= 0 &&
                    int.TryParse(
                        slotId.Substring(separator + 1),
                        out int slotOrder))
                {
                    bestOrder = Math.Min(bestOrder, slotOrder);
                }
            }

            return bestOrder;
        }

        private static void SetReference(
            SerializedObject serialized,
            string propertyName,
            UnityEngine.Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
                throw new MissingFieldException(propertyName);
            property.objectReferenceValue = value;
        }

        private static void SetReferences<T>(
            SerializedObject serialized,
            string propertyName,
            IReadOnlyList<T> values)
            where T : UnityEngine.Object
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
                throw new MissingFieldException(propertyName);
            property.arraySize = values.Count;
            for (int index = 0; index < values.Count; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue =
                    values[index];
            }
        }

        private static void InstallInMainScene()
        {
            Scene mainScene = SceneManager.GetSceneByPath(MainScenePath);
            bool openedForInstall = !mainScene.IsValid() || !mainScene.isLoaded;
            if (openedForInstall)
            {
                mainScene = EditorSceneManager.OpenScene(
                    MainScenePath,
                    OpenSceneMode.Additive);
            }

            foreach (GameObject root in mainScene.GetRootGameObjects())
            {
                if (root != null &&
                    (root.name == "DeveloperCheatConsole" ||
                     root.GetComponent<DeveloperCheatConsoleController>() != null))
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                PrefabPath);
            GameObject instance = PrefabUtility.InstantiatePrefab(
                prefab,
                mainScene) as GameObject;
            if (instance == null)
                throw new InvalidOperationException("Prefab install failed.");
            instance.name = "DeveloperCheatConsole";
            EditorSceneManager.MarkSceneDirty(mainScene);
            EditorSceneManager.SaveScene(mainScene);

            if (openedForInstall)
                EditorSceneManager.CloseScene(mainScene, true);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = System.IO.Path.GetDirectoryName(path)
                ?.Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);
            if (string.IsNullOrWhiteSpace(parent) ||
                string.IsNullOrWhiteSpace(leaf))
            {
                throw new InvalidOperationException(
                    $"Invalid asset folder path: {path}");
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private sealed class ItemGroupLayout
        {
            public ItemGroupLayout(
                string label,
                StationSystemType systemType)
            {
                Label = label;
                SystemType = systemType;
            }

            public string Label { get; }
            public StationSystemType SystemType { get; }
        }
    }
}
