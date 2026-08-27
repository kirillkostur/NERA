using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Climbing;
using NERA.Combat;
using NERA.Core;
using NERA.Drone;
using NERA.Energy;
using NERA.Enemies;
using NERA.Expeditions;
using NERA.Graphics;
using NERA.Interaction;
using NERA.Inventory;
using NERA.Items;
using NERA.Localization;
using NERA.Maintenance;
using NERA.Player;
using NERA.Research;
using NERA.Quests;
using NERA.Save;
using NERA.Station;
using NERA.UI;
using NERA.World;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NERA.Tests
{
    public sealed class StationEnergySceneLifecycleTests
    {
        private string previousSaveRoot;
        private string isolatedSaveRoot;
        private bool hadLocalePreference;
        private string previousLocalePreference;
        private LoadingScreenConfig loadingScreenConfig;
        private float previousLoadingScreenDuration;

        [OneTimeSetUp]
        public void RedirectSavesToTemporaryStorage()
        {
            GameObject loadingPrefab = Resources.Load<GameObject>(
                LoadingScreenController.PrefabResourcePath);
            loadingScreenConfig = loadingPrefab != null
                ? loadingPrefab.GetComponent<LoadingScreenController>()?.Config
                : null;
            if (loadingScreenConfig != null)
            {
                previousLoadingScreenDuration =
                    loadingScreenConfig.MinimumDisplaySeconds;
            }

            hadLocalePreference = PlayerPrefs.HasKey(
                NERALocalization.LocalePreferenceKey);
            previousLocalePreference = PlayerPrefs.GetString(
                NERALocalization.LocalePreferenceKey,
                NERALocalization.EnglishCode);
            PlayerPrefs.SetString(
                NERALocalization.LocalePreferenceKey,
                NERALocalization.EnglishCode);
            PlayerPrefs.Save();
            NERALocalization.SetLocale(NERALocalization.EnglishCode);

            previousSaveRoot = Environment.GetEnvironmentVariable(
                SaveSlotStorage.SaveRootEnvironmentVariable);
            isolatedSaveRoot = Path.Combine(
                Path.GetTempPath(),
                "NERA_PlayModeTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(isolatedSaveRoot);
            Environment.SetEnvironmentVariable(
                SaveSlotStorage.SaveRootEnvironmentVariable,
                isolatedSaveRoot);
        }

        [OneTimeTearDown]
        public void RestoreSaveStorage()
        {
            if (loadingScreenConfig != null)
            {
                SetPrivateField(
                    loadingScreenConfig,
                    "minimumDisplaySeconds",
                    previousLoadingScreenDuration);
            }

            if (hadLocalePreference)
            {
                PlayerPrefs.SetString(
                    NERALocalization.LocalePreferenceKey,
                    previousLocalePreference);
            }
            else
            {
                PlayerPrefs.DeleteKey(NERALocalization.LocalePreferenceKey);
            }
            PlayerPrefs.Save();
            if (hadLocalePreference)
                NERALocalization.SetLocale(previousLocalePreference);

            Environment.SetEnvironmentVariable(
                SaveSlotStorage.SaveRootEnvironmentVariable,
                previousSaveRoot);
            if (!string.IsNullOrWhiteSpace(isolatedSaveRoot) &&
                Directory.Exists(isolatedSaveRoot))
            {
                Directory.Delete(isolatedSaveRoot, true);
            }
        }

        [UnitySetUp]
        public IEnumerator UseEnglishLocaleForEveryTest()
        {
            yield return ResetSceneState();
            if (loadingScreenConfig != null)
            {
                SetPrivateField(
                    loadingScreenConfig,
                    "minimumDisplaySeconds",
                    0f);
            }
            PlayerPrefs.SetString(
                NERALocalization.LocalePreferenceKey,
                NERALocalization.EnglishCode);
            PlayerPrefs.Save();
            NERALocalization.SetLocale(NERALocalization.EnglishCode);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDownPersistentBootRoot()
        {
            yield return ResetSceneState();
        }

        [UnityTest]
        public IEnumerator LoadingScreenCoversRuntimeStartupAndUsesPools()
        {
            Assert.That(loadingScreenConfig, Is.Not.Null);
            SetPrivateField(
                loadingScreenConfig,
                "minimumDisplaySeconds",
                0.15f);

            SceneManager.LoadScene("MainScene");
            yield return null;

            LoadingScreenController loading =
                LoadingScreenController.Instance;
            Assert.That(loading, Is.Not.Null);
            Assert.That(loading.IsVisible, Is.True);
            Assert.That(loading.LoadingCamera, Is.Not.Null);
            Assert.That(loading.LoadingCamera.enabled, Is.True);
            Assert.That(loading.CurrentImage, Is.Not.Null);
            Assert.That(loading.CurrentTipText, Is.Not.Empty);

            float visibleAt = Time.realtimeSinceStartup;
            yield return WaitForScene("Player_Station");

            Assert.That(
                Time.realtimeSinceStartup - visibleAt,
                Is.GreaterThanOrEqualTo(0.1f));
            Assert.That(loading.IsVisible, Is.False);
            Assert.That(loading.LoadingCamera.enabled, Is.False);
        }

        [UnityTest]
        public IEnumerator LoadingScreenCoversNewGameFromMainMenuWithoutBlinking()
        {
            SetPrivateField(
                loadingScreenConfig,
                "minimumDisplaySeconds",
                0.15f);
            SceneManager.LoadScene("Boot");
            yield return null;

            MainMenuController mainMenu =
                Object.FindFirstObjectByType<MainMenuController>();
            Assert.That(mainMenu, Is.Not.Null);
            Transform menuPanel = GameObject.Find("Canvas")
                .transform.Find("Panel");
            mainMenu.StartNewGame();
            Transform slotScreen = menuPanel.Find(
                "ContinueScreen/background_Screen_station");
            slotScreen.Find("Panel_Save_1")
                .GetComponent<Button>().onClick.Invoke();
            slotScreen.Find("ContinueButton")
                .GetComponent<Button>().onClick.Invoke();
            yield return null;

            LoadingScreenController loading =
                LoadingScreenController.Instance;
            Assert.That(loading, Is.Not.Null);
            Assert.That(loading.IsVisible, Is.True);
            Assert.That(loading.ActiveRequestCount, Is.GreaterThanOrEqualTo(1));

            yield return WaitForScene("Player_Station");
            Assert.That(loading.IsVisible, Is.False);
            Assert.That(loading.ActiveRequestCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator LoadingScreenCoversGameplaySceneTransition()
        {
            SceneManager.LoadScene("MainScene");
            yield return WaitForScene("Player_Station");

            SetPrivateField(
                loadingScreenConfig,
                "minimumDisplaySeconds",
                0.15f);
            BootInitializer runtime = BootInitializer.Instance;
            LoadingScreenController loading =
                LoadingScreenController.Instance;
            Assert.That(runtime, Is.Not.Null);
            Assert.That(loading, Is.Not.Null);

            Assert.That(
                runtime.LoadGameplayScene("Expedition_01", string.Empty),
                Is.True);
            yield return null;

            Assert.That(loading.IsVisible, Is.True);
            Assert.That(loading.ActiveRequestCount, Is.EqualTo(1));
            yield return WaitForScene("Expedition_01");
            Assert.That(loading.IsVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator LoadingScreenCoversDeathUntilPlayerIsRevived()
        {
            SceneManager.LoadScene("MainScene");
            yield return WaitForScene("Player_Station");

            SetPrivateField(
                loadingScreenConfig,
                "minimumDisplaySeconds",
                0.15f);
            PlayerHealth health =
                Object.FindFirstObjectByType<PlayerHealth>();
            CheckpointService checkpoints = CheckpointService.Instance;
            LoadingScreenController loading =
                LoadingScreenController.Instance;
            Assert.That(health, Is.Not.Null);
            Assert.That(checkpoints, Is.Not.Null);
            Assert.That(loading, Is.Not.Null);

            LogAssert.Expect(
                LogType.Warning,
                "Player died and ragdoll was enabled.");
            health.Kill();
            yield return null;

            Assert.That(health.IsAlive, Is.False);
            Assert.That(checkpoints.IsRestoring, Is.True);
            Assert.That(loading.IsVisible, Is.True);

            float deadline = Time.realtimeSinceStartup + 15f;
            while (checkpoints.IsRestoring &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(checkpoints.IsRestoring, Is.False);
            Assert.That(health.IsAlive, Is.True);
            Assert.That(BootInitializer.Instance.IsLoading, Is.False);
            Assert.That(loading.IsVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator BootRemainsInMenuUntilAStartActionIsRequested()
        {
            SceneManager.LoadScene("Boot");
            yield return null;

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Boot"));
            Assert.That(
                Object.FindFirstObjectByType<MainMenuController>(),
                Is.Not.Null);
            Assert.That(
                Object.FindFirstObjectByType<BootInitializer>(),
                Is.Null);
        }

        [UnityTest]
        public IEnumerator BootMenuWindowsFollowAuthoredFlow()
        {
            SceneManager.LoadScene("Boot");
            yield return null;

            Transform canvas = GameObject.Find("Canvas").transform;
            Transform panel = canvas.Find("Panel");
            Transform root = panel.Find("RootButton");
            Transform continueScreen = panel.Find("ContinueScreen");
            Transform optionsScreen = panel.Find("OptionsScreen");
            Transform exitScreen = panel.Find("ExitScreen");
            Transform rootButtonContainer = root.Find("background_button");
            Transform slotBackground = continueScreen.Find(
                "background_Screen_station");
            CinemachineVirtualCameraBase rootMenuCamera = GameObject
                .Find("VirtualCam_01")
                .GetComponent<CinemachineVirtualCameraBase>();
            CinemachineVirtualCameraBase saveSlotCamera = GameObject
                .Find("VirtualCam_02")
                .GetComponent<CinemachineVirtualCameraBase>();

            Assert.That(root.gameObject.activeSelf, Is.True);
            Assert.That(continueScreen.gameObject.activeSelf, Is.False);
            Assert.That(optionsScreen.gameObject.activeSelf, Is.False);
            Assert.That(exitScreen.gameObject.activeSelf, Is.False);
            AssertActiveMenuCamera(rootMenuCamera, saveSlotCamera);

            for (int slot = 1; slot <= SaveSlotStorage.SlotCount; slot++)
            {
                Assert.That(
                    slotBackground.Find($"Panel_Save_{slot}")
                        .GetComponent<Button>(),
                    Is.Not.Null);
            }

            rootButtonContainer.Find("ContinueButton").GetComponent<Button>()
                .onClick.Invoke();
            Assert.That(root.gameObject.activeSelf, Is.False);
            Assert.That(continueScreen.gameObject.activeSelf, Is.True);
            AssertActiveMenuCamera(saveSlotCamera, rootMenuCamera);

            slotBackground.Find("CloseButton").GetComponent<Button>()
                .onClick.Invoke();
            Assert.That(root.gameObject.activeSelf, Is.True);
            Assert.That(continueScreen.gameObject.activeSelf, Is.False);
            AssertActiveMenuCamera(rootMenuCamera, saveSlotCamera);

            rootButtonContainer.Find("NewGameButton").GetComponent<Button>()
                .onClick.Invoke();
            Assert.That(root.gameObject.activeSelf, Is.False);
            Assert.That(continueScreen.gameObject.activeSelf, Is.True);
            AssertActiveMenuCamera(saveSlotCamera, rootMenuCamera);
            slotBackground.Find("CloseButton").GetComponent<Button>()
                .onClick.Invoke();
            AssertActiveMenuCamera(rootMenuCamera, saveSlotCamera);

            rootButtonContainer.Find("OptionsButton").GetComponent<Button>()
                .onClick.Invoke();
            Assert.That(root.gameObject.activeSelf, Is.False);
            Assert.That(optionsScreen.gameObject.activeSelf, Is.True);
            AssertActiveMenuCamera(rootMenuCamera, saveSlotCamera);
            Transform languageButton = optionsScreen.Find(
                "background_Screen_station/LanguageButton");
            Assert.That(languageButton, Is.Not.Null);
            Assert.That(
                languageButton.GetComponent<LanguageToggleButton>(),
                Is.Not.Null);
            languageButton.GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(
                NERALocalization.CurrentLocaleCode,
                Is.EqualTo(NERALocalization.RussianCode));
            languageButton.GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(
                NERALocalization.CurrentLocaleCode,
                Is.EqualTo(NERALocalization.EnglishCode));
            optionsScreen.Find("background_Screen_station/CloseButton")
                .GetComponent<Button>().onClick.Invoke();

            rootButtonContainer.Find("ExitButton").GetComponent<Button>()
                .onClick.Invoke();
            Assert.That(root.gameObject.activeSelf, Is.True);
            Assert.That(exitScreen.gameObject.activeSelf, Is.True);
            AssertActiveMenuCamera(rootMenuCamera, saveSlotCamera);
            exitScreen.Find("background_exit/NOButton")
                .GetComponent<Button>().onClick.Invoke();
            Assert.That(root.gameObject.activeSelf, Is.True);
            Assert.That(exitScreen.gameObject.activeSelf, Is.False);
            AssertActiveMenuCamera(rootMenuCamera, saveSlotCamera);
        }

        private static void AssertActiveMenuCamera(
            CinemachineVirtualCameraBase activeCamera,
            CinemachineVirtualCameraBase inactiveCamera)
        {
            Assert.That(activeCamera, Is.Not.Null);
            Assert.That(inactiveCamera, Is.Not.Null);
            Assert.That(
                activeCamera.Priority.Value,
                Is.GreaterThan(inactiveCamera.Priority.Value));
        }

        [UnityTest]
        public IEnumerator BootSaveSlotsShowEmptyOrSaveDateAndTime()
        {
            SaveSlotStorage.DeleteAllSlots();
            string occupiedPath = SaveSlotStorage.GetSlotPath(2);
            File.WriteAllText(
                occupiedPath,
                JsonUtility.ToJson(new SaveGameData
                {
                    completionPercent = 37f
                }));
            File.SetLastWriteTime(
                occupiedPath,
                new DateTime(2030, 4, 5, 18, 7, 0));

            SceneManager.LoadScene("Boot");
            yield return null;

            MainMenuController menu =
                Object.FindFirstObjectByType<MainMenuController>();
            Assert.That(menu, Is.Not.Null);
            menu.ContinueGame();
            yield return null;

            Transform slots = GameObject.Find("Canvas").transform.Find(
                "Panel/ContinueScreen/background_Screen_station");
            Transform emptySlot = slots.Find("Panel_Save_1");
            Component emptyDate = emptySlot.Find("Data_Text")
                .GetComponent("TextMeshProUGUI");
            Component emptyCompletion = emptySlot.Find("Complete_Text")
                .GetComponent("TextMeshProUGUI");
            Assert.That(
                emptyDate.GetType().GetProperty("text")?.GetValue(emptyDate),
                Is.EqualTo("EMPTY"));
            Assert.That(
                emptyCompletion.GetType().GetProperty("text")
                    ?.GetValue(emptyCompletion),
                Is.EqualTo("0% COMPLETE"));

            DateTime writeTime = SaveSlotStorage.GetLastWriteTime(2);
            string expectedDate = writeTime.ToString(
                "MM.dd.yyyy - HH:mm",
                CultureInfo.InvariantCulture);
            Transform occupiedSlot = slots.Find("Panel_Save_2");
            Component occupiedDate = occupiedSlot.Find("Data_Text")
                .GetComponent("TextMeshProUGUI");
            Component occupiedCompletion = occupiedSlot.Find("Complete_Text")
                .GetComponent("TextMeshProUGUI");
            Assert.That(
                occupiedDate.GetType().GetProperty("text")
                    ?.GetValue(occupiedDate),
                Is.EqualTo(expectedDate));
            Assert.That(
                occupiedCompletion.GetType().GetProperty("text")
                    ?.GetValue(occupiedCompletion),
                Is.EqualTo("37% COMPLETE"));

            NERALocalization.SetLocale(NERALocalization.RussianCode);
            yield return null;
            string expectedRussianDate = writeTime.ToString(
                "dd.MM.yyyy - HH:mm",
                CultureInfo.InvariantCulture);
            Assert.That(
                occupiedDate.GetType().GetProperty("text")
                    ?.GetValue(occupiedDate),
                Is.EqualTo(expectedRussianDate));
            Assert.That(
                emptyDate.GetType().GetProperty("text")?.GetValue(emptyDate),
                Is.EqualTo("ПУСТО"));
            NERALocalization.SetLocale(NERALocalization.EnglishCode);

            SaveSlotStorage.DeleteAllSlots();
        }

        [UnityTest]
        public IEnumerator CheatConsoleLanguageButtonTogglesLocale()
        {
            SceneManager.LoadScene("MainScene");
            yield return null;

            NERA.Development.DeveloperCheatConsoleController cheats =
                Object.FindFirstObjectByType<
                    NERA.Development.DeveloperCheatConsoleController>();
            Assert.That(cheats, Is.Not.Null);
            Transform languageButton = cheats.transform.Find(
                "CheatWindow/LanguageButton");
            Assert.That(languageButton, Is.Not.Null);
            Button button = languageButton.GetComponent<Button>();
            Assert.That(button, Is.Not.Null);

            NERALocalization.SetLocale(NERALocalization.EnglishCode);
            yield return null;
            button.onClick.Invoke();
            yield return null;
            Assert.That(
                NERALocalization.CurrentLocaleCode,
                Is.EqualTo(NERALocalization.RussianCode));

            button.onClick.Invoke();
            yield return null;
            Assert.That(
                NERALocalization.CurrentLocaleCode,
                Is.EqualTo(NERALocalization.EnglishCode));
        }

        [UnityTest]
        public IEnumerator CheatConsoleEquipmentButtonsAddConfiguredItems()
        {
            SceneManager.LoadScene("MainScene");
            yield return WaitForScene("Player_Station");
            yield return null;
            yield return DisablePersistenceForTest();

            NERA.Development.DeveloperCheatConsoleController cheats =
                Object.FindFirstObjectByType<
                    NERA.Development.DeveloperCheatConsoleController>();
            PlayerInventory inventory =
                Object.FindFirstObjectByType<PlayerInventory>();
            Assert.That(cheats, Is.Not.Null);
            Assert.That(inventory, Is.Not.Null);

            inventory.RestoreItems(Array.Empty<ItemData>());
            Button pistolButton = cheats.transform.Find(
                    "CheatWindow/GiveItemButton_25")
                ?.GetComponent<Button>();
            Button integratorButton = cheats.transform.Find(
                    "CheatWindow/GiveItemButton_26")
                ?.GetComponent<Button>();
            Assert.That(pistolButton, Is.Not.Null);
            Assert.That(integratorButton, Is.Not.Null);

            pistolButton.onClick.Invoke();
            integratorButton.onClick.Invoke();
            yield return null;

            Assert.That(inventory.CountItem("energy_pistol_01"), Is.EqualTo(1));
            Assert.That(inventory.CountItem("io_integrator_01"), Is.EqualTo(1));
            inventory.RestoreItems(Array.Empty<ItemData>());
        }

        [UnityTest]
        public IEnumerator LaboratoryIsUnavailableUntilGridStarts()
        {
            SceneManager.LoadScene("MainScene");
            yield return WaitForScene("Player_Station");
            yield return null;
            yield return DisablePersistenceForTest();

            EnergySystemController energy = EnergySystemController.Instance;
            LaboratoryTableInteractable laboratory =
                UnityEngine.Object.FindFirstObjectByType<LaboratoryTableInteractable>();

            Assert.That(energy, Is.Not.Null);
            Assert.That(laboratory, Is.Not.Null);

            energy.RestoreState(energy.TotalCapacity, false);
            Assert.That(laboratory.GetPrompt().IsAvailable, Is.False);

            energy.SetGridEnabled(true);
            Assert.That(laboratory.GetPrompt().IsAvailable, Is.True);
        }

        [UnityTest]
        public IEnumerator StationBakedLightingFollowsRealPowerAndWeather()
        {
            SceneManager.LoadScene("Boot");
            yield return null;

            MainMenuController mainMenu =
                Object.FindFirstObjectByType<MainMenuController>();
            Assert.That(mainMenu, Is.Not.Null);
            Transform menuPanel = GameObject.Find("Canvas")
                .transform.Find("Panel");
            mainMenu.StartNewGame();
            yield return null;
            Transform slotScreen = menuPanel.Find(
                "ContinueScreen/background_Screen_station");
            slotScreen.Find("Panel_Save_1")
                .GetComponent<Button>().onClick.Invoke();
            slotScreen.Find("ContinueButton")
                .GetComponent<Button>().onClick.Invoke();

            yield return WaitForScene("Player_Station");
            yield return null;
            yield return DisablePersistenceForTest();

            EnergySystemController energy = EnergySystemController.Instance;
            StationEnvironmentController environment =
                StationEnvironmentController.Instance;
            SwitchBakedLights lighting =
                Object.FindFirstObjectByType<SwitchBakedLights>();

            Assert.That(energy, Is.Not.Null);
            Assert.That(environment, Is.Not.Null);
            Assert.That(lighting, Is.Not.Null);

            energy.RestoreState(
                energy.TotalCapacity,
                energy.TotalBackupReserve,
                false);
            yield return null;
            AssertLightingPreset(
                lighting,
                SwitchBakedLights.StationLightingMode.BackupPowerEmergency,
                "backupPowerEmergency");

            energy.SetGridEnabled(true);
            yield return null;
            AssertLightingPreset(
                lighting,
                SwitchBakedLights.StationLightingMode.Normal,
                "normalOperation");

            environment.SetWeather(StationWeather.Sandstorm);
            yield return null;
            AssertLightingPreset(
                lighting,
                SwitchBakedLights.StationLightingMode.LowEnergyWarning,
                "lowEnergyWarning");
        }

        [UnityTest]
        public IEnumerator TerminalWorldDecorationFollowsPowerStateAndLastTab()
        {
            SceneManager.LoadScene("MainScene");
            yield return WaitForScene("Player_Station");
            yield return null;
            yield return DisablePersistenceForTest();

            EnergySystemController energy = EnergySystemController.Instance;
            StationSystemsController systems = StationSystemsController.Instance;
            StationWeatherController weather =
                StationWeatherController.Instance;
            Terminal.TerminalUIScreen terminal =
                Terminal.TerminalUIScreen.Instance;
            Terminal.TerminalAccessInteractable access =
                Object.FindFirstObjectByType<
                    Terminal.TerminalAccessInteractable>(
                    FindObjectsInactive.Include);
            ParkourPlayerBridge player =
                Object.FindFirstObjectByType<ParkourPlayerBridge>();

            Assert.That(energy, Is.Not.Null);
            Assert.That(systems, Is.Not.Null);
            Assert.That(weather, Is.Not.Null);
            Assert.That(terminal, Is.Not.Null);
            Assert.That(access, Is.Not.Null);
            Assert.That(player, Is.Not.Null);

            Camera gameplayCamera = player.GameplayCamera;
            Camera stationPreviewCamera = Array.Find(
                terminal.GetComponentsInChildren<Camera>(true),
                candidate => candidate.name == "StationUICamera");
            Camera mapPreviewCamera = Array.Find(
                terminal.GetComponentsInChildren<Camera>(true),
                candidate => candidate.name == "MapUICamera");
            Assert.That(gameplayCamera, Is.Not.Null);
            Assert.That(gameplayCamera.enabled, Is.True);
            int gameplayCullingMask = gameplayCamera.cullingMask;
            CameraClearFlags gameplayClearFlags = gameplayCamera.clearFlags;
            bool gameplayAllowHdr = gameplayCamera.allowHDR;
            bool gameplayAllowMsaa = gameplayCamera.allowMSAA;
            bool gameplayUseOcclusionCulling =
                gameplayCamera.useOcclusionCulling;
            Assert.That(stationPreviewCamera, Is.Not.Null);
            Assert.That(mapPreviewCamera, Is.Not.Null);
            Assert.That(stationPreviewCamera.targetTexture.width, Is.EqualTo(1024));
            Assert.That(stationPreviewCamera.targetTexture.height, Is.EqualTo(1024));
            Assert.That(mapPreviewCamera.targetTexture.width, Is.EqualTo(1024));
            Assert.That(mapPreviewCamera.targetTexture.height, Is.EqualTo(1024));

            Transform visualRoot = access.transform.Find("Visual_3D");
            Transform stationVisual =
                visualRoot?.Find("SM_Station_Mini_3D");
            Transform mapVisual = visualRoot?.Find("SM_Map_Mini_3D");
            Transform sandstormVisual = stationVisual?.Find(
                "VFX_Sandstorm_Mini");
            ParticleEffectController sandstormEffect =
                sandstormVisual?.GetComponent<ParticleEffectController>();
            Transform interactionPoint =
                access.transform.Find("InteractionPoint");
            Assert.That(visualRoot, Is.Not.Null);
            Assert.That(stationVisual, Is.Not.Null);
            Assert.That(mapVisual, Is.Not.Null);
            Assert.That(sandstormVisual, Is.Not.Null);
            Assert.That(sandstormEffect, Is.Not.Null);
            Assert.That(interactionPoint, Is.Not.Null);
            weather.StopSandstorm();
            yield return null;
            Assert.That(sandstormEffect.IsPlayingRequested, Is.False);
            Assert.That(weather.StartSandstorm(10f), Is.True);
            yield return null;
            Assert.That(sandstormEffect.IsPlayingRequested, Is.True);
            Assert.That(weather.StopSandstorm(), Is.True);
            yield return null;
            Assert.That(sandstormEffect.IsPlayingRequested, Is.False);
            Assert.That(access.InteractionTransform, Is.EqualTo(interactionPoint));
            Assert.That(
                access.gameObject.layer,
                Is.EqualTo(LayerMask.NameToLayer("Environment")));
            Assert.That(
                interactionPoint.gameObject.layer,
                Is.EqualTo(LayerMask.NameToLayer("Interactable")));
            SphereCollider interactionCollider =
                interactionPoint.GetComponent<SphereCollider>();
            Assert.That(interactionCollider, Is.Not.Null);
            Assert.That(interactionCollider.isTrigger, Is.True);

            int environmentMask =
                1 << LayerMask.NameToLayer("Environment");
            Vector3 frontOrigin = interactionPoint.position +
                access.transform.forward * 1.2f;
            Vector3 frontTarget =
                interactionCollider.ClosestPoint(frontOrigin);
            Vector3 frontOffset = frontTarget - frontOrigin;
            Assert.That(
                Physics.Raycast(
                    frontOrigin,
                    frontOffset.normalized,
                    frontOffset.magnitude,
                    environmentMask,
                    QueryTriggerInteraction.Ignore),
                Is.False,
                "The terminal must be approachable from its screen side.");

            Vector3 backOrigin = interactionPoint.position -
                access.transform.forward * 2f;
            Vector3 backTarget = interactionCollider.ClosestPoint(backOrigin);
            Vector3 backOffset = backTarget - backOrigin;
            Assert.That(
                Physics.Raycast(
                    backOrigin,
                    backOffset.normalized,
                    backOffset.magnitude,
                    environmentMask,
                    QueryTriggerInteraction.Ignore),
                Is.True,
                "The terminal body must block interaction from behind.");

            energy.RestoreState(energy.TotalCapacity, false);
            yield return null;
            Assert.That(visualRoot.gameObject.activeSelf, Is.False);
            Assert.That(stationVisual.gameObject.activeSelf, Is.False);
            Assert.That(mapVisual.gameObject.activeSelf, Is.False);

            energy.RestoreState(energy.TotalCapacity, true);
            systems.SetCriticalSystemActive(StationSystemType.Terminal, true);
            yield return null;
            Assert.That(visualRoot.gameObject.activeSelf, Is.True);
            Assert.That(stationVisual.gameObject.activeSelf, Is.True);
            Assert.That(mapVisual.gameObject.activeSelf, Is.False);
            Assert.That(
                stationVisual.GetComponentsInChildren<StationObjectVisual>(true),
                Has.Length.GreaterThan(0),
                "World terminal station must mirror installed upgrade parts.");
            Assert.That(
                stationVisual.GetComponentsInChildren<Collider>(true),
                Is.Empty,
                "The decorative mini station must not contain colliders.");
            Assert.That(
                stationVisual.GetComponentsInChildren<Transform>(true)
                    .All(item => item.gameObject.layer ==
                        LayerMask.NameToLayer("Default")),
                Is.True,
                "The decorative mini station must stay on the Default layer.");
            Assert.That(
                mapVisual.GetComponentsInChildren<Collider>(true),
                Is.Empty,
                "The decorative mini map must not contain colliders.");
            Assert.That(
                mapVisual.GetComponentsInChildren<
                    Terminal.MapLocationSlotRegistry>(true),
                Is.Empty,
                "The decorative mini map must not contain a slot registry.");
            Assert.That(
                mapVisual.GetComponentsInChildren<
                    Terminal.MapLocationSlot>(true),
                Is.Empty,
                "The decorative mini map must not contain interactive slots.");
            Assert.That(
                mapVisual.GetComponentsInChildren<Transform>(true)
                    .All(item => item.gameObject.layer ==
                        LayerMask.NameToLayer("Default")),
                Is.True,
                "The decorative mini map must stay on the Default layer.");

            systems.SetCriticalSystemActive(
                StationSystemType.Terminal,
                false);
            yield return null;
            Assert.That(visualRoot.gameObject.activeSelf, Is.False);
            Assert.That(stationVisual.gameObject.activeSelf, Is.False);
            Assert.That(mapVisual.gameObject.activeSelf, Is.False);

            systems.SetCriticalSystemActive(
                StationSystemType.Terminal,
                true);
            yield return null;
            Assert.That(visualRoot.gameObject.activeSelf, Is.True);
            Assert.That(stationVisual.gameObject.activeSelf, Is.True);
            Assert.That(mapVisual.gameObject.activeSelf, Is.False);

            access.CompleteInteraction(player.gameObject);
            Assert.That(terminal.IsOpening, Is.True);
            Assert.That(terminal.IsOpen, Is.False);
            Assert.That(terminal.GetComponent<CanvasGroup>().alpha, Is.Zero);

            float openingDeadline = Time.realtimeSinceStartup + 6f;
            while (!terminal.IsOpen &&
                   Time.realtimeSinceStartup < openingDeadline)
            {
                yield return null;
            }

            Assert.That(terminal.IsOpen, Is.True);
            Assert.That(terminal.ActiveScreenIndex, Is.EqualTo(1));
            Assert.That(gameplayCamera.enabled, Is.True);
            Assert.That(Camera.allCameras, Does.Contain(gameplayCamera));
            Assert.That(gameplayCamera.cullingMask, Is.Zero);
            Assert.That(gameplayCamera.allowHDR, Is.False);
            Assert.That(gameplayCamera.allowMSAA, Is.False);
            Assert.That(gameplayCamera.useOcclusionCulling, Is.False);
            Assert.That(stationPreviewCamera.enabled, Is.False);
            Assert.That(mapPreviewCamera.enabled, Is.False);
            Assert.That(
                stationPreviewCamera.GetComponent<
                    Terminal.TerminalPreviewRenderer>(),
                Is.Not.Null);
            Assert.That(
                mapPreviewCamera.GetComponent<
                    Terminal.TerminalPreviewRenderer>(),
                Is.Not.Null);
            terminal.ShowMap();
            Assert.That(stationPreviewCamera.enabled, Is.False);
            Assert.That(mapPreviewCamera.enabled, Is.False);
            Assert.That(stationVisual.gameObject.activeSelf, Is.False);
            Assert.That(mapVisual.gameObject.activeSelf, Is.True);
            terminal.Close();
            Assert.That(gameplayCamera.enabled, Is.True);
            Assert.That(gameplayCamera.cullingMask, Is.EqualTo(gameplayCullingMask));
            Assert.That(gameplayCamera.clearFlags, Is.EqualTo(gameplayClearFlags));
            Assert.That(gameplayCamera.allowHDR, Is.EqualTo(gameplayAllowHdr));
            Assert.That(gameplayCamera.allowMSAA, Is.EqualTo(gameplayAllowMsaa));
            Assert.That(
                gameplayCamera.useOcclusionCulling,
                Is.EqualTo(gameplayUseOcclusionCulling));
            Assert.That(mapVisual.gameObject.activeSelf, Is.True);

            access.CompleteInteraction(player.gameObject);
            Assert.That(stationVisual.gameObject.activeSelf, Is.False);
            Assert.That(mapVisual.gameObject.activeSelf, Is.True);
            openingDeadline = Time.realtimeSinceStartup + 6f;
            while (!terminal.IsOpen &&
                   Time.realtimeSinceStartup < openingDeadline)
            {
                yield return null;
            }

            Assert.That(terminal.IsOpen, Is.True);
            Assert.That(terminal.ActiveScreenIndex, Is.EqualTo(0));
            Assert.That(gameplayCamera.enabled, Is.True);
            Assert.That(gameplayCamera.cullingMask, Is.Zero);
            terminal.ShowLibrary();
            terminal.Close();
            Assert.That(gameplayCamera.enabled, Is.True);
            Assert.That(stationVisual.gameObject.activeSelf, Is.True);
            Assert.That(mapVisual.gameObject.activeSelf, Is.False);

            access.CompleteInteraction(player.gameObject);
            openingDeadline = Time.realtimeSinceStartup + 6f;
            while (!terminal.IsOpen &&
                   Time.realtimeSinceStartup < openingDeadline)
            {
                yield return null;
            }

            Assert.That(terminal.IsOpen, Is.True);
            Assert.That(terminal.ActiveScreenIndex, Is.EqualTo(2));
            Assert.That(gameplayCamera.enabled, Is.True);
            Assert.That(gameplayCamera.cullingMask, Is.Zero);
            terminal.Close();
            Assert.That(gameplayCamera.enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator SavedChargeSurvivesStartupBeforeBatterySceneLoads()
        {
            SaveSlotStorage.DeleteAllSlots();
            string savePath = SaveSlotStorage.GetSlotPath(
                SaveSlotStorage.DefaultSlot);
            Directory.CreateDirectory(Path.GetDirectoryName(savePath));
            File.WriteAllText(
                savePath,
                JsonUtility.ToJson(new SaveGameData
                {
                    energyStateInitialized = true,
                    stationEnergy = 1000f,
                    energyGridEnabled = true
                }));
            GameSessionLaunchState.Request(
                GameLaunchMode.Continue,
                SaveSlotStorage.DefaultSlot);

            SceneManager.LoadScene("MainScene");
            yield return WaitForScene("Player_Station");
            yield return null;

            EnergySystemController energy = EnergySystemController.Instance;
            Assert.That(energy, Is.Not.Null);
            Assert.That(energy.TotalCapacity, Is.GreaterThanOrEqualTo(1000f));
            Assert.That(
                energy.CurrentEnergy,
                Is.EqualTo(1000f).Within(0.01f),
                "Startup must preserve charge loaded before station batteries register.");
        }

        [UnityTest]
        public IEnumerator ContinueLoadsAutoSaveCheckpointAsSpawnPoint()
        {
            SaveSlotStorage.DeleteAllSlots();
            string savePath = SaveSlotStorage.GetSlotPath(
                SaveSlotStorage.DefaultSlot);
            Directory.CreateDirectory(Path.GetDirectoryName(savePath));
            File.WriteAllText(
                savePath,
                JsonUtility.ToJson(new SaveGameData
                {
                    checkpointSceneName = "Expedition_01",
                    checkpointSpawnPointId = "checkpoint",
                    checkpointUsesWorldPose = false
                }));
            GameSessionLaunchState.Request(
                GameLaunchMode.Continue,
                SaveSlotStorage.DefaultSlot);

            SceneManager.LoadScene("MainScene");
            float deadline = Time.realtimeSinceStartup + 10f;
            while ((BootInitializer.Instance == null ||
                    BootInitializer.Instance.IsLoading) &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            BootInitializer runtime = BootInitializer.Instance;
            Assert.That(runtime, Is.Not.Null);
            Assert.That(runtime.IsLoading, Is.False, "Continue timed out.");
            Assert.That(
                runtime.LastTransitionResult,
                Is.EqualTo(SceneTransitionResult.Success));
            Assert.That(
                runtime.CurrentGameplaySceneName,
                Is.EqualTo("Expedition_01"));
            Assert.That(
                SceneManager.GetSceneByName("Expedition_01").isLoaded,
                Is.True);

            AutoSaveCheckpoint checkpoint =
                Object.FindObjectsByType<AutoSaveCheckpoint>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None)
                    .FirstOrDefault(candidate =>
                        candidate.gameObject.scene.name == "Expedition_01" &&
                        candidate.CheckpointId == "checkpoint");
            ParkourPlayerBridge player =
                Object.FindFirstObjectByType<ParkourPlayerBridge>();
            Assert.That(checkpoint, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            Assert.That(
                Vector3.Distance(
                    player.transform.position,
                    checkpoint.transform.position),
                Is.LessThan(0.1f));
            Assert.That(SceneTransitionState.HasPendingSpawnPoint, Is.False);
        }

        [UnityTest]
        public IEnumerator ReturningToStationDoesNotDuplicateEnergySources()
        {
            SceneManager.LoadScene("MainScene");
            yield return WaitForScene("Player_Station");
            yield return null;
            yield return DisablePersistenceForTest();

            EnergySystemController energy = EnergySystemController.Instance;
            StationEnvironmentController environment =
                StationEnvironmentController.Instance;
            BootInitializer runtime = BootInitializer.Instance;

            Assert.That(energy, Is.Not.Null);
            Assert.That(environment, Is.Not.Null);
            Assert.That(runtime, Is.Not.Null);
            Assert.That(
                SceneManager.GetSceneByName("MainScene").isLoaded,
                Is.True,
                "MainScene must remain loaded while gameplay content is active.");
            Assert.That(
                runtime.gameObject.scene.name,
                Is.EqualTo("MainScene"),
                "RuntimeRoot must stay in MainScene, not DontDestroyOnLoad.");

            environment.SetTime(12f);
            environment.SetWeather(StationWeather.Clear);
            energy.AdvanceSimulation(0.1f);

            StationBattery[] batteries = Object.FindObjectsByType<StationBattery>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            SolarPowerSource[] solarPanels =
                Object.FindObjectsByType<SolarPowerSource>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
            int initialBatteryCount = batteries.Length;
            int initialSolarPanelCount = solarPanels.Length;
            float initialCapacity = energy.TotalCapacity;
            Assert.That(initialBatteryCount, Is.GreaterThan(0));
            Assert.That(initialCapacity, Is.GreaterThan(0f));
            Assert.That(
                energy.CurrentGeneration,
                Is.EqualTo(
                    energy.Config.ClearDayGeneration * initialSolarPanelCount)
                    .Within(0.01f)
            );

            Assert.That(
                runtime.LoadGameplayScene("Expedition_01", string.Empty),
                Is.True);
            yield return WaitForScene("Expedition_01");

            Assert.That(
                runtime.LoadGameplayScene("Player_Station", "Station_Start"),
                Is.True);
            yield return WaitForScene("Player_Station");
            yield return null;

            Assert.That(SceneManager.GetSceneByName("MainScene").isLoaded, Is.True);
            Assert.That(
                SceneManager.GetSceneByName("Expedition_01").isLoaded,
                Is.False);

            environment.SetTime(12f);
            environment.SetWeather(StationWeather.Clear);
            energy.AdvanceSimulation(0.1f);

            solarPanels = Object.FindObjectsByType<SolarPowerSource>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            batteries = Object.FindObjectsByType<StationBattery>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            Assert.That(
                batteries.Length,
                Is.EqualTo(initialBatteryCount),
                "Returning to the station duplicated or lost battery sources.");
            Assert.That(
                solarPanels.Length,
                Is.EqualTo(initialSolarPanelCount),
                "Returning to the station duplicated or lost solar sources.");
            Assert.That(energy.TotalCapacity, Is.EqualTo(initialCapacity));
            Assert.That(
                energy.CurrentGeneration,
                Is.EqualTo(
                    energy.Config.ClearDayGeneration * initialSolarPanelCount)
                    .Within(0.01f)
            );
        }

        [UnityTest]
        public IEnumerator TimeOfDayVisualsAndVolumetricFogOnlyRunAtPlayerStation()
        {
            SceneManager.LoadScene("MainScene");
            yield return WaitForScene("Player_Station");
            yield return null;
            yield return DisablePersistenceForTest();

            BootInitializer runtime = BootInitializer.Instance;
            StationEnvironmentController environment =
                StationEnvironmentController.Instance;
            StationWeatherController weather = StationWeatherController.Instance;
            Assert.That(runtime, Is.Not.Null);
            Assert.That(environment, Is.Not.Null);
            Assert.That(weather, Is.Not.Null);
            Assert.That(
                SceneManager.GetActiveScene().name,
                Is.EqualTo(StationEnvironmentController.PlayerStationSceneName));

            environment.SetTime(12f);
            weather.StopSandstorm();
            Assert.That(weather.StartSandstorm(120f), Is.True);
            weather.AdvanceSimulation(
                weather.Config.SandstormFogFadeDurationSeconds * 2f);
            Assert.That(
                weather.IsSandstormRendererFeatureActive,
                Is.True);

            Assert.That(
                runtime.LoadGameplayScene("Expedition_01", string.Empty),
                Is.True);
            yield return WaitForScene("Expedition_01");
            yield return null;

            float expeditionHour = environment.CurrentHour;
            yield return new WaitForSeconds(0.2f);
            Assert.That(
                environment.CurrentHour,
                Is.GreaterThan(expeditionHour));
            Assert.That(
                weather.IsSandstormRendererFeatureActive,
                Is.False);
            Assert.That(
                weather.CurrentFogDensity,
                Is.EqualTo(weather.Config.ClearFogDensity).Within(0.001f));

            Assert.That(
                runtime.LoadGameplayScene(
                    StationEnvironmentController.PlayerStationSceneName,
                    "Station_Start"),
                Is.True);
            yield return WaitForScene(
                StationEnvironmentController.PlayerStationSceneName);
            yield return null;

            Assert.That(weather.IsSandstormActive, Is.True);
            Assert.That(
                weather.IsSandstormRendererFeatureActive,
                Is.True);

            weather.StopSandstorm();
            weather.AdvanceSimulation(
                weather.Config.SandstormFogFadeDurationSeconds * 2f);
        }

        [UnityTest]
        public IEnumerator FailedSceneTransitionKeepsCurrentSceneAndCheckpoint()
        {
            SceneManager.LoadScene("MainScene");
            yield return WaitForScene("Player_Station");
            yield return null;

            BootInitializer runtime = BootInitializer.Instance;
            SaveGameController save =
                Object.FindFirstObjectByType<SaveGameController>();
            Assert.That(runtime, Is.Not.Null);
            Assert.That(save, Is.Not.Null);
            string checkpointSceneBefore = save.CheckpointSceneName;
            string checkpointIdBefore = save.CheckpointSpawnPointId;

            const string missingSpawn = "missing_transition_test_spawn";
            LogAssert.Expect(
                LogType.Error,
                $"BootInitializer: Scene 'Expedition_01' has no spawn point " +
                $"'{missingSpawn}'.");
            Assert.That(
                runtime.LoadGameplayScene("Expedition_01", missingSpawn),
                Is.True,
                "A valid scene request should start asynchronously.");
            while (runtime.IsLoading)
                yield return null;

            Assert.That(
                runtime.LastTransitionResult,
                Is.EqualTo(SceneTransitionResult.Failure));
            Assert.That(
                runtime.CurrentGameplaySceneName,
                Is.EqualTo("Player_Station"));
            Assert.That(
                SceneManager.GetActiveScene().name,
                Is.EqualTo("Player_Station"));
            Assert.That(
                SceneManager.GetSceneByName("Expedition_01").isLoaded,
                Is.False,
                "A scene loaded by a failed transition must be rolled back.");
            Assert.That(SceneTransitionState.HasPendingSpawnPoint, Is.False);
            Assert.That(save.CheckpointSceneName, Is.EqualTo(checkpointSceneBefore));
            Assert.That(save.CheckpointSpawnPointId, Is.EqualTo(checkpointIdBefore));
        }

        [UnityTest]
        public IEnumerator TurretEnergyCostIsAtomicPerShot()
        {
            SceneManager.LoadScene("MainScene");
            yield return WaitForScene("Player_Station");
            yield return null;
            yield return DisablePersistenceForTest();

            EnergySystemController energy = EnergySystemController.Instance;
            StationTurretController turret =
                StationTurretController.FindById("station_turret_01");
            Assert.That(energy, Is.Not.Null);
            Assert.That(turret, Is.Not.Null);
            energy.RestoreState(energy.TotalCapacity, true);

            float energyBefore = energy.CurrentEnergy;
            float consumerRateBefore = energy.CurrentConsumption;
            float shotCost = turret.EffectiveEnergyPerShot;
            Assert.That(shotCost, Is.GreaterThan(0f));

            Assert.That(turret.TrySpendFiringEnergy(), Is.True);
            Assert.That(turret.TrySpendFiringEnergy(), Is.True);

            Assert.That(
                energy.CurrentEnergy,
                Is.EqualTo(energyBefore - shotCost * 2f).Within(0.001f));
            Assert.That(
                energy.CurrentConsumption,
                Is.EqualTo(consumerRateBefore).Within(0.001f),
                "A shot must not replace the idle consumer rate for one frame.");
        }

        [UnityTest]
        public IEnumerator StationTerminalIsStatusOnlyAndReflectsPhysicalParts()
        {
            SceneManager.LoadScene("MainScene");
            yield return WaitForScene("Player_Station");
            yield return null;
            yield return DisablePersistenceForTest();

            EnergySystemController energy = EnergySystemController.Instance;
            StationSystemsController systems = StationSystemsController.Instance;
            Terminal.TerminalUIScreen terminal =
                Terminal.TerminalUIScreen.Instance;
            Terminal.TerminalStationScreenController stationScreen =
                Object.FindFirstObjectByType<
                    Terminal.TerminalStationScreenController>(
                    FindObjectsInactive.Include);
            Assert.That(energy, Is.Not.Null);
            Assert.That(systems, Is.Not.Null);
            Assert.That(terminal, Is.Not.Null);
            Assert.That(stationScreen, Is.Not.Null);

            systems.ResetSystems();
            energy.RestoreState(energy.TotalCapacity, true);
            systems.SetCriticalSystemActive(StationSystemType.Terminal, true);
            terminal.Open();
            terminal.ShowStation();
            yield return null;

            Assert.That(
                stationScreen.transform.Find("background_Status"),
                Is.Not.Null);
            Assert.That(
                stationScreen.transform.Find("background_Upgrade"),
                Is.Null);
            Assert.That(
                stationScreen.transform.Find("UpgradesMapButton"),
                Is.Null);
            Assert.That(
                stationScreen.GetComponentsInChildren<StationObjectVisual>(true),
                Has.Length.EqualTo(5));

            Transform turretPreview = Array.Find(
                stationScreen.GetComponentsInChildren<Transform>(true),
                candidate => candidate.name == "SM_Turret_1");
            Assert.That(turretPreview, Is.Not.Null);
            Assert.That(
                stationScreen.SelectPreviewObject(turretPreview),
                Is.True);
            Assert.That(
                stationScreen.SelectedObjectId,
                Is.EqualTo("station_turret_01"));

            StationSystemDefinition turretDefinition = systems.Config.Find(
                StationSystemType.Turret,
                "station_turret_01");
            StationObjectStatDefinition damageDefinition =
                turretDefinition?.FindStat(StationObjectStat.Damage);
            Assert.That(turretDefinition, Is.Not.Null);
            Assert.That(damageDefinition, Is.Not.Null);

            Transform statusPanel =
                stationScreen.transform.Find("background_Status");
            Transform textTransform = Array.Find(
                statusPanel.GetComponentsInChildren<Transform>(true),
                candidate => candidate.name == "Text_description");
            Component statusText =
                textTransform.GetComponent("TextMeshProUGUI");
            string initialText = statusText.GetType().GetProperty("text")
                ?.GetValue(statusText)?.ToString();
            float initialDamage = systems.GetStat(
                StationSystemType.Turret,
                "station_turret_01",
                StationObjectStat.Damage);
            Assert.That(
                initialText,
                Does.Contain(
                    $"Damage - {damageDefinition.Format(initialDamage)}"));
            Assert.That(
                initialText,
                Does.Contain(
                    $"Installed parts - 0/{turretDefinition.Slots.Count}"));
            Assert.That(initialText, Does.Contain("Condition -"));
            Assert.That(initialText, Does.Not.Contain("Status -"));
            Assert.That(initialText, Does.Not.Contain("Aim tolerance"));

            energy.RestoreState(750f, true);
            stationScreen.SelectSystem(StationSystemType.Battery);
            string batteryText = statusText.GetType().GetProperty("text")
                ?.GetValue(statusText)?.ToString();
            Assert.That(
                batteryText,
                Does.Contain(
                    $"Capacity - 750/{energy.TotalCapacity:F0} kWh"));
            Assert.That(batteryText, Does.Not.Contain("Condition -"));
            Assert.That(
                batteryText,
                Does.Contain(
                    "Backup Reserve - " +
                    $"{energy.CurrentBackupReserve:F0}/" +
                    $"{energy.TotalBackupReserve:F0} kWh"));

            stationScreen.SelectSystem(StationSystemType.Drone);
            string droneText = statusText.GetType().GetProperty("text")
                ?.GetValue(statusText)?.ToString();
            Assert.That(droneText, Does.Contain("Condition -"));

            Assert.That(
                stationScreen.SelectPreviewObject(turretPreview),
                Is.True);

            ItemData emitter = Resources.Load<ItemCatalogData>(
                "ItemCatalog_Default").Find("item_emitter_damage_01");
            Assert.That(emitter, Is.Not.Null);
            EngineeringPartCompatibility compatibility = turretDefinition
                .Slots
                .Select(slot => emitter.FindEngineeringCompatibility(
                    StationSystemType.Turret,
                    "station_turret_01",
                    slot.SlotId))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(compatibility, Is.Not.Null);
            Assert.That(
                systems.TryInstallParts(
                    StationSystemType.Turret,
                    "station_turret_01",
                    new[]
                    {
                        new StationPartInstallRequest(
                            compatibility.SlotId,
                            emitter)
                    },
                    out string reason),
                Is.True,
                reason);
            yield return new WaitForSecondsRealtime(0.15f);

            string upgradedText = statusText.GetType().GetProperty("text")
                ?.GetValue(statusText)?.ToString();
            float upgradedDamage = systems.GetStat(
                StationSystemType.Turret,
                "station_turret_01",
                StationObjectStat.Damage);
            Assert.That(
                upgradedText,
                Does.Contain(
                    $"Damage - {damageDefinition.Format(upgradedDamage)}"));
            Assert.That(
                upgradedText,
                Does.Contain(
                    $"Installed parts - 1/{turretDefinition.Slots.Count}"));
            Transform installedPreview = Array.Find(
                turretPreview.GetComponentsInChildren<Transform>(true),
                candidate => candidate.name ==
                    "Installed_item_emitter_damage_01");
            Assert.That(
                installedPreview,
                Is.Not.Null,
                "StationUIPreview must spawn the same installed part visual.");
        }

        [UnityTest]
        public IEnumerator UpgradeCameraAlignsOnEntryAndPlayerCameraOnExit()
        {
            SceneManager.LoadScene("MainScene");
            yield return WaitForScene("Player_Station");
            yield return null;
            yield return DisablePersistenceForTest();

            StationSystemsController systems =
                StationSystemsController.Instance;
            PlayerInventory inventory =
                Object.FindFirstObjectByType<PlayerInventory>();
            ParkourPlayerBridge player =
                inventory?.GetComponent<ParkourPlayerBridge>();
            StationUpgradeableObject target = Object
                .FindObjectsByType<StationUpgradeableObject>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .FirstOrDefault(candidate =>
                    candidate.SystemType == StationSystemType.Turret &&
                    candidate.ObjectId == "station_turret_01");

            Assert.That(systems, Is.Not.Null);
            Assert.That(inventory, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            Assert.That(player.GameplayCamera, Is.Not.Null);
            Assert.That(target, Is.Not.Null);
            Assert.That(target.UpgradeCamera, Is.Not.Null);
            systems.ResetSystems();

            CinemachineOrbitalTransposer orbit = target.UpgradeCamera
                .GetComponentInChildren<CinemachineOrbitalTransposer>(true);
            Assert.That(orbit, Is.Not.Null);
            float expectedEntryAxis = orbit.GetAxisClosestValue(
                player.GameplayCamera.transform.position,
                Vector3.up);
            CinemachineBrain brain =
                player.GameplayCamera.GetComponent<CinemachineBrain>();
            Assert.That(brain, Is.Not.Null);
            CinemachineFreeLook gameplayFreeLook =
                brain.ActiveVirtualCamera as CinemachineFreeLook;
            Assert.That(
                gameplayFreeLook,
                Is.Not.Null,
                "The active gameplay camera must be the player FreeLookCam.");
            CinemachineOrbitalTransposer gameplayOrbit = gameplayFreeLook
                .GetRig(1)
                .GetCinemachineComponent<CinemachineOrbitalTransposer>();
            Assert.That(gameplayOrbit, Is.Not.Null);

            StationUpgradeModeController controller =
                StationUpgradeModeController.GetOrCreate();
            Assert.That(
                controller.Open(target, inventory.gameObject),
                Is.True);
            Assert.That(
                Mathf.Abs(Mathf.DeltaAngle(
                    orbit.m_XAxis.Value,
                    expectedEntryAxis)),
                Is.LessThan(0.01f));
            Assert.That(player.IsInputEnabled, Is.False);

            float entryTimeoutAt = Time.realtimeSinceStartup + 6f;
            while ((brain.IsBlending ||
                    !ReferenceEquals(
                        brain.ActiveVirtualCamera,
                        target.UpgradeCamera)) &&
                   Time.realtimeSinceStartup < entryTimeoutAt)
            {
                yield return null;
            }
            Assert.That(
                ReferenceEquals(
                    brain.ActiveVirtualCamera,
                    target.UpgradeCamera),
                Is.True,
                "Upgrade camera did not finish its entry blend.");

            float exitObjectAxis = expectedEntryAxis + 60f;
            orbit.m_XAxis.Value = exitObjectAxis;
            yield return null;
            Vector3 exitCameraPosition =
                player.GameplayCamera.transform.position;
            float expectedGameplayAxis = gameplayOrbit.GetAxisClosestValue(
                exitCameraPosition,
                gameplayFreeLook.State.ReferenceUp);

            controller.Close();
            Assert.That(controller.IsOpen, Is.True);
            Assert.That(controller.IsClosing, Is.True);
            Assert.That(controller.IsBlendingToGameplay, Is.True);
            Assert.That(player.IsInputEnabled, Is.False);
            Assert.That(
                Mathf.Abs(Mathf.DeltaAngle(
                    gameplayFreeLook.m_XAxis.Value,
                    expectedGameplayAxis)),
                Is.LessThan(0.1f),
                "Player FreeLookCam must align to the object exit view.");
            Assert.That(
                Mathf.Abs(Mathf.DeltaAngle(
                    orbit.m_XAxis.Value,
                    exitObjectAxis)),
                Is.LessThan(0.1f),
                "Object camera must not orbit back during exit.");

            yield return null;
            Assert.That(
                player.IsInputEnabled,
                Is.False,
                "Input must remain locked throughout camera blending.");

            float timeoutAt = Time.realtimeSinceStartup + 8f;
            while (controller.IsOpen &&
                   Time.realtimeSinceStartup < timeoutAt)
            {
                yield return null;
            }

            Assert.That(controller.IsOpen, Is.False);
            Assert.That(player.IsInputEnabled, Is.True);
            Assert.That(
                Mathf.Abs(Mathf.DeltaAngle(
                    orbit.m_XAxis.Value,
                    exitObjectAxis)),
                Is.LessThan(0.01f),
                "The object camera must keep its last upgrade orbit angle.");
        }

        [UnityTest]
        public IEnumerator CriticalSystemTogglesCloseTerminalAndCutPower()
        {
            SceneManager.LoadScene("MainScene");
            yield return WaitForScene("Player_Station");
            yield return null;
            yield return DisablePersistenceForTest();

            EnergySystemController energy = EnergySystemController.Instance;
            StationSystemsController systems = StationSystemsController.Instance;
            Terminal.TerminalUIScreen terminal = Terminal.TerminalUIScreen.Instance;
            Terminal.TerminalStationScreenController stationScreen =
                Object.FindFirstObjectByType<
                    Terminal.TerminalStationScreenController>(
                    FindObjectsInactive.Include);
            Transform powerSwitch = Array.Find(
                stationScreen.GetComponentsInChildren<Transform>(true),
                candidate => candidate.name == "Toggle");
            Button onButton = powerSwitch.Find("OnButton").GetComponent<Button>();

            energy.RestoreState(energy.TotalCapacity, true);
            systems.SetCriticalSystemActive(StationSystemType.Terminal, true);
            terminal.Open();
            terminal.ShowStation();

            stationScreen.SelectSystem(StationSystemType.Terminal);
            onButton.onClick.Invoke();
            Assert.That(
                terminal.IsOpen,
                Is.False,
                "Terminal shutdown must close the terminal UI.");
            Assert.That(
                systems.IsRequestedActive(StationSystemType.Terminal),
                Is.False,
                "Terminal must be inactive after pressing its active toggle.");
            Assert.That(energy.GridEnabled, Is.True);

            systems.SetCriticalSystemActive(StationSystemType.Terminal, true);
            terminal.Open();
            terminal.ShowStation();
            stationScreen.SelectSystem(StationSystemType.Battery);
            onButton.onClick.Invoke();

            Assert.That(
                terminal.IsOpen,
                Is.False,
                "Battery shutdown must close the terminal.");
            Assert.That(
                energy.GridEnabled,
                Is.False,
                "Battery shutdown must disable the energy grid.");
            Assert.That(
                systems.IsRequestedActive(StationSystemType.Battery),
                Is.False,
                "Battery must be inactive after pressing its active toggle.");
        }

        [UnityTest]
        public IEnumerator BatteryPartAppliesConfiguredCapacityImmediately()
        {
            SceneManager.LoadScene("MainScene");
            yield return WaitForScene("Player_Station");
            yield return null;
            yield return DisablePersistenceForTest();

            EnergySystemController energy = EnergySystemController.Instance;
            StationSystemsController systems = StationSystemsController.Instance;
            Assert.That(energy, Is.Not.Null);
            Assert.That(systems, Is.Not.Null);

            systems.ResetSystems();
            yield return null;
            StationSystemDefinition battery =
                systems.GetDefinition(StationSystemType.Battery);
            Assert.That(battery, Is.Not.Null);
            float baseCapacity = systems.GetStat(
                StationSystemType.Battery,
                battery.ObjectId,
                StationObjectStat.Capacity);
            ItemData capacityPart = CreateEngineeringPart(
                "test_battery_capacity",
                StationSystemType.Battery,
                battery.ObjectId,
                "Slot_1",
                StationObjectStat.Capacity,
                250f);
            ItemCatalogData testCatalog = CreateCatalog(capacityPart);
            try
            {
                SetPrivateField(systems, "itemCatalog", testCatalog);
                Assert.That(
                    systems.TryInstallParts(
                        StationSystemType.Battery,
                        battery.ObjectId,
                        new[]
                        {
                            new StationPartInstallRequest(
                                "Slot_1",
                                capacityPart)
                        },
                        out string reason),
                    Is.True,
                    reason);
                yield return null;

                Assert.That(
                    systems.GetStat(
                        StationSystemType.Battery,
                        battery.ObjectId,
                        StationObjectStat.Capacity),
                    Is.EqualTo(baseCapacity + 250f));
                Assert.That(
                    energy.TotalCapacity,
                    Is.GreaterThanOrEqualTo(baseCapacity + 250f),
                    "The live battery must re-register after the part is installed.");
            }
            finally
            {
                Object.Destroy(testCatalog);
                Object.Destroy(capacityPart);
            }
        }

        [UnityTest]
        public IEnumerator MainExpeditionQuestRunsFromRuntimeSignals()
        {
            SceneManager.LoadScene("MainScene");
            yield return WaitForScene("Player_Station");
            yield return null;
            yield return DisablePersistenceForTest();

            QuestController quests = QuestController.Instance;
            ExpeditionDiscoveryController discovery =
                ExpeditionDiscoveryController.Instance;
            QuestHUDController questHud =
                Object.FindFirstObjectByType<QuestHUDController>(
                    FindObjectsInactive.Include);
            Assert.That(quests, Is.Not.Null);
            Assert.That(discovery, Is.Not.Null);
            Assert.That(questHud, Is.Not.Null);
            Assert.That(questHud.MaxDisplayedMainQuests, Is.EqualTo(3));
            Assert.That(questHud.MaxDisplayedSideQuests, Is.EqualTo(4));

            quests.ResetProgress();
            discovery.RestoreDiscovered(Array.Empty<string>());
            Assert.That(questHud.IsVisible, Is.False);

            Assert.That(
                quests.Report(
                    QuestSignalType.LocationEntered,
                    "Player_Station"),
                Is.True);
            Assert.That(
                quests.FindActive("main.restore_battery"),
                Is.Not.Null,
                "Restoring station power is the first one-time main quest.");
            Assert.That(
                quests.Report(
                    QuestSignalType.StationSystemActivated,
                    "station_battery",
                    "BATTERY"),
                Is.True);
            Assert.That(
                quests.IsCompleted("main.restore_battery"),
                Is.True);
            Assert.That(
                quests.FindActive("main.first_terminal"),
                Is.Not.Null,
                "The terminal introduction follows the battery quest.");
            Assert.That(questHud.IsVisible, Is.True);

            quests.Report(
                QuestSignalType.ObjectInteractionCompleted,
                "station_terminal",
                "Station Terminal");
            Assert.That(
                quests.IsCompleted("main.first_terminal"),
                Is.True);
            Assert.That(
                quests.FindActive("main.launch_drone_expedition_01"),
                Is.Not.Null);

            Assert.That(discovery.Discover("Expedition_01"), Is.True);
            quests.Report(
                QuestSignalType.DroneScanCompleted,
                "Expedition_01",
                "Ancient Outpost",
                cause: "new_location");
            Assert.That(
                quests.FindActive("main.expedition_01")?.CurrentStageIndex,
                Is.Zero);
            yield return new WaitForSecondsRealtime(
                questHud.CompletedDisplayDuration + 0.1f);
            Assert.That(
                questHud.DisplayedMainText,
                Does.Not.Contain("MAIN QUEST"));
            Assert.That(
                questHud.DisplayedMainText,
                Does.Contain("Travel to the Ancient Outpost"));
            Assert.That(
                questHud.DisplayedMainText,
                Does.Contain("• Travel to the Ancient Outpost"));
            Assert.That(questHud.DisplayedSideText, Is.Empty);

            quests.ReportDeviceCondition(
                "test_solar_panel",
                "Test Solar Panel",
                0.3f);
            Assert.That(
                questHud.DisplayedSideText,
                Does.Contain("Start cleaning"));
            Assert.That(
                questHud.DisplayedSideText,
                Does.Contain("• Clean Test Solar Panel"));
            Assert.That(
                questHud.DisplayedSideText,
                Does.Not.Contain("SIDE QUEST"));

            quests.ReportStationFault(
                "test_turret",
                "Test Turret",
                "EnemySabotage");
            Assert.That(
                questHud.DisplayedSideText,
                Does.Contain("Restart malfunctioning objects"),
                "The higher-priority side quest must be displayed.");
            Assert.That(
                questHud.DisplayedSideText,
                Does.Contain("Start cleaning"),
                "Multiple side quests must be displayed at the same time.");
            Assert.That(
                questHud.DisplayedSideText.IndexOf(
                    "Restart malfunctioning objects",
                    StringComparison.Ordinal),
                Is.LessThan(questHud.DisplayedSideText.IndexOf(
                    "Start cleaning",
                    StringComparison.Ordinal)),
                "Higher-priority quests must be listed first.");

            quests.Report(
                QuestSignalType.StationSystemActivated,
                "test_turret",
                "Test Turret");
            Assert.That(
                questHud.DisplayedSideText,
                Does.Contain("Start cleaning"),
                "HUD must fall back to the next active side quest.");
            Assert.That(
                questHud.DisplayedSideText,
                Does.Contain("<s>• Restart Test Turret</s>"),
                "A completed visible quest must be struck through first.");

            quests.Report(QuestSignalType.LocationEntered, "Expedition_01");
            quests.Report(QuestSignalType.EnemyEncountered, "io_blue_weak");
            quests.Report(QuestSignalType.ItemCollected, "io_blue_shard_01");
            quests.Report(QuestSignalType.LocationEntered, "Player_Station");
            quests.Report(
                QuestSignalType.ResearchAnalyzed,
                "research_io_blue_shard_01");

            Assert.That(quests.IsCompleted("main.expedition_01"), Is.True);
            Assert.That(questHud.DisplayedMainText, Does.Contain("<s>"));
            Assert.That(questHud.IsVisible, Is.True);

            quests.ReportDeviceCondition(
                "test_solar_panel",
                "Test Solar Panel",
                1f);
            Assert.That(quests.ActiveQuests.Count, Is.Zero);
            Assert.That(questHud.DisplayedSideText, Does.Contain("<s>"));
            Assert.That(questHud.IsVisible, Is.True);

            yield return new WaitForSecondsRealtime(
                questHud.CompletedDisplayDuration + 0.1f);
            Assert.That(questHud.DisplayedMainText, Is.Empty);
            Assert.That(questHud.DisplayedSideText, Is.Empty);
            Assert.That(questHud.IsVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator FirstBatteryInteractionCompletesPowerRestoreQuest()
        {
            SceneManager.LoadScene("MainScene");
            yield return WaitForScene("Player_Station");
            yield return null;
            yield return DisablePersistenceForTest();

            QuestController quests = QuestController.Instance;
            StationSystemsController systems =
                StationSystemsController.Instance;
            StationPowerController power = StationPowerController.Instance;
            StationUpgradeableObject battery = Object
                .FindObjectsByType<StationUpgradeableObject>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .FirstOrDefault(candidate =>
                    candidate.SystemType == StationSystemType.Battery);

            Assert.That(quests, Is.Not.Null);
            Assert.That(systems, Is.Not.Null);
            Assert.That(power, Is.Not.Null);
            Assert.That(battery, Is.Not.Null);

            quests.ResetProgress();
            systems.ResetSystems();
            power.SetState(StationPowerState.Offline);

            Assert.That(
                systems.IsRequestedActive(StationSystemType.Battery),
                Is.False,
                "A new game must start with the station battery disabled.");

            Assert.That(
                systems.SetCriticalSystemActive(
                    StationSystemType.Battery,
                    true),
                Is.True);
            quests.ResetProgress();
            Assert.That(
                quests.Report(
                    QuestSignalType.LocationEntered,
                    "Player_Station"),
                Is.True);
            Assert.That(
                quests.FindActive("main.restore_battery"),
                Is.Not.Null);
            Assert.That(
                systems.IsRequestedActive(StationSystemType.Battery),
                Is.True,
                "The test reproduces an old save with RequestedActive=true.");
            Assert.That(power.IsPowered, Is.False);

            battery.CompleteInteraction(null);

            Assert.That(power.IsPowered, Is.True);
            Assert.That(
                quests.IsCompleted("main.restore_battery"),
                Is.True,
                "The first physical power restore must complete the quest " +
                "even when RequestedActive was already true.");
        }

        [UnityTest]
        public IEnumerator NewGameRestoresAuthoredMaintenanceConditions()
        {
            SceneManager.LoadScene("MainScene");
            yield return WaitForScene("Player_Station");
            yield return null;

            SaveGameController save =
                Object.FindFirstObjectByType<SaveGameController>();
            Assert.That(save, Is.Not.Null);

            GameObject firstRoot = new GameObject("Test_InitialTurret_01");
            GameObject secondRoot = new GameObject("Test_InitialTurret_02");
            try
            {
                StationObjectIdentity firstIdentity =
                    firstRoot.AddComponent<StationObjectIdentity>();
                firstIdentity.Configure(
                    StationSystemType.Turret,
                    "test_initial_turret_01");
                MaintainableObject first =
                    firstRoot.AddComponent<MaintainableObject>();
                SetPrivateField(first, "initialCondition", 0.25f);

                StationObjectIdentity secondIdentity =
                    secondRoot.AddComponent<StationObjectIdentity>();
                secondIdentity.Configure(
                    StationSystemType.Turret,
                    "test_initial_turret_02");
                MaintainableObject second =
                    secondRoot.AddComponent<MaintainableObject>();
                SetPrivateField(second, "initialCondition", 0.75f);

                first.SetCondition(1f);
                second.SetCondition(0f);
                save.ClearSave(resetProgress: true);

                Assert.That(first.Condition, Is.EqualTo(0.25f));
                Assert.That(second.Condition, Is.EqualTo(0.75f));
            }
            finally
            {
                Object.Destroy(firstRoot);
                Object.Destroy(secondRoot);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator DirtyDroneCannotLaunchUntilItIsFullyCleaned()
        {
            SceneManager.LoadScene("MainScene");
            yield return WaitForScene("Player_Station");
            yield return null;
            yield return DisablePersistenceForTest();

            EnergySystemController energy = EnergySystemController.Instance;
            ExpeditionDiscoveryController discovery =
                ExpeditionDiscoveryController.Instance;
            DroneScanController drone = DroneScanController.Instance;
            StationSystemsController systems = StationSystemsController.Instance;
            Assert.That(energy, Is.Not.Null);
            Assert.That(discovery, Is.Not.Null);
            Assert.That(drone, Is.Not.Null);
            Assert.That(systems, Is.Not.Null);
            Assert.That(
                MaintainableObject.TryFind(
                    "station_drone",
                    out MaintainableObject maintenance),
                Is.True);

            systems.ResetSystems();
            energy.RestoreState(energy.TotalCapacity, true);
            Assert.That(
                systems.SetRequestedActive(StationSystemType.Drone, true),
                Is.True);
            discovery.RestoreDiscovered(Array.Empty<string>());
            StationSystemDefinition definition =
                systems.GetDefinition(StationSystemType.Drone);
            float range = systems.GetStat(
                StationSystemType.Drone,
                definition.ObjectId,
                StationObjectStat.TravelRange);
            ExpeditionLocationData location = discovery.KnownLocations
                .FirstOrDefault(candidate =>
                    candidate != null &&
                    candidate.DiscoverySource ==
                        NERA.Locations.DiscoverySource.Drone &&
                    candidate.RequiredDroneTravelRange <= range);
            Assert.That(location, Is.Not.Null);

            maintenance.SetCondition(0.5f);
            Assert.That(drone.IsFlightReady, Is.False);
            Assert.That(drone.State, Is.EqualTo(DroneState.Locked));
            Assert.That(drone.CanLaunchScan(location), Is.False);
            Assert.That(drone.LaunchScan(location), Is.False);

            maintenance.SetCondition(1f);
            Assert.That(drone.IsFlightReady, Is.True);
            Assert.That(drone.State, Is.EqualTo(DroneState.Ready));
            Assert.That(drone.CanLaunchScan(location), Is.True);
        }

        [UnityTest]
        public IEnumerator DroneWeatherExposureTracksPhysicalStationPresence()
        {
            SceneManager.LoadScene("MainScene");
            yield return WaitForScene("Player_Station");
            yield return null;
            yield return DisablePersistenceForTest();

            EnergySystemController energy = EnergySystemController.Instance;
            ExpeditionDiscoveryController discovery =
                ExpeditionDiscoveryController.Instance;
            DroneScanController drone = DroneScanController.Instance;
            StationSystemsController systems = StationSystemsController.Instance;
            StationWeatherController weather = StationWeatherController.Instance;
            Assert.That(energy, Is.Not.Null);
            Assert.That(discovery, Is.Not.Null);
            Assert.That(drone, Is.Not.Null);
            Assert.That(systems, Is.Not.Null);
            Assert.That(weather, Is.Not.Null);
            Assert.That(
                MaintainableObject.TryFind(
                    "station_drone",
                    out MaintainableObject maintenance),
                Is.True);

            weather.StopSandstorm();
            systems.ResetSystems();
            energy.RestoreState(energy.TotalCapacity, true);
            Assert.That(
                systems.SetRequestedActive(StationSystemType.Drone, true),
                Is.True);
            maintenance.SetCondition(1f);
            discovery.RestoreDiscovered(Array.Empty<string>());

            StationSystemDefinition definition =
                systems.GetDefinition(StationSystemType.Drone);
            float range = systems.GetStat(
                StationSystemType.Drone,
                definition.ObjectId,
                StationObjectStat.TravelRange);
            ExpeditionLocationData location = discovery.KnownLocations
                .FirstOrDefault(candidate =>
                    candidate != null &&
                    candidate.DiscoverySource ==
                        NERA.Locations.DiscoverySource.Drone &&
                    candidate.RequiredDroneTravelRange <= range);
            Assert.That(location, Is.Not.Null);

            drone.RefreshAvailability();
            Assert.That(weather.StartSandstorm(10f), Is.True);
            Assert.That(drone.State, Is.EqualTo(DroneState.Locked));
            Assert.That(drone.CanLaunchScan(location), Is.False);
            Assert.That(drone.LaunchScan(location), Is.False);

            Assert.That(weather.StopSandstorm(), Is.True);
            maintenance.SetCondition(1f);
            drone.RefreshAvailability();
            yield return null;
            Assert.That(drone.State, Is.EqualTo(DroneState.Ready));
            Assert.That(drone.LaunchScan(location), Is.True);
            Assert.That(
                drone.IsAtStation,
                Is.True,
                "The drone remains present until the Start_Scan event.");

            DroneAnimationView mainAnimation =
                Object.FindObjectsByType<DroneAnimationView>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None)
                    .Single(view =>
                        view.GetComponent<Animator>()
                            .runtimeAnimatorController.name ==
                        DroneAnimationView.MainControllerName);
            mainAnimation.Start_Scan();
            Assert.That(drone.IsAtStation, Is.False);

            NERA.Development.DeveloperCheatConsoleController cheats =
                NERA.Development.DeveloperCheatConsoleController.Instance;
            Assert.That(cheats, Is.Not.Null);
            cheats.ContaminateAllObjects();
            Assert.That(
                maintenance.Condition,
                Is.EqualTo(1f),
                "The contamination cheat must skip an absent drone.");

            Assert.That(weather.StartSandstorm(10f), Is.True);
            maintenance.AdvanceSandExposure(6f, 10f);
            weather.AdvanceSimulation(6f);
            Assert.That(maintenance.Condition, Is.EqualTo(1f));

            drone.AdvanceScan(drone.CurrentScanDuration);
            mainAnimation.End_Scan();
            Assert.That(drone.IsAtStation, Is.True);

            maintenance.AdvanceSandExposure(4f, 10f);
            weather.AdvanceSimulation(4f);
            Assert.That(
                maintenance.Condition,
                Is.EqualTo(0.6f).Within(0.001f));
            Assert.That(maintenance.IsSandClogged, Is.False);

            cheats.ContaminateAllObjects();
            Assert.That(
                maintenance.Condition,
                Is.Zero,
                "The contamination cheat must affect the returned drone.");
        }

        [UnityTest]
        public IEnumerator DroneAnimationsMirrorLaunchAndReturnInEveryView()
        {
            SceneManager.LoadScene("MainScene");
            yield return WaitForScene("Player_Station");
            yield return null;
            yield return DisablePersistenceForTest();

            DroneScanController drone = DroneScanController.Instance;
            ExpeditionDiscoveryController discovery =
                ExpeditionDiscoveryController.Instance;
            EnergySystemController energy = EnergySystemController.Instance;
            StationSystemsController systems = StationSystemsController.Instance;
            Assert.That(drone, Is.Not.Null);
            Assert.That(discovery, Is.Not.Null);
            Assert.That(energy, Is.Not.Null);
            Assert.That(systems, Is.Not.Null);

            DroneAnimationView[] views =
                Object.FindObjectsByType<DroneAnimationView>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            Assert.That(views, Has.Length.EqualTo(3));
            Assert.That(
                views.Count(view =>
                    view.GetComponent<Animator>()
                        .runtimeAnimatorController.name ==
                    DroneAnimationView.MainControllerName),
                Is.EqualTo(1));
            Assert.That(
                views.Count(view =>
                    view.GetComponent<Animator>()
                        .runtimeAnimatorController.name ==
                    DroneAnimationView.MiniControllerName),
                Is.EqualTo(2));

            systems.ResetSystems();
            energy.RestoreState(energy.TotalCapacity, true);
            Assert.That(
                systems.SetRequestedActive(StationSystemType.Drone, true),
                Is.True);
            Assert.That(
                MaintainableObject.TryFind(
                    "station_drone",
                    out MaintainableObject maintenance),
                Is.True);
            maintenance.SetCondition(1f);
            discovery.RestoreDiscovered(Array.Empty<string>());

            StationSystemDefinition definition =
                systems.GetDefinition(StationSystemType.Drone);
            float range = systems.GetStat(
                StationSystemType.Drone,
                definition.ObjectId,
                StationObjectStat.TravelRange);
            ExpeditionLocationData location = discovery.KnownLocations
                .FirstOrDefault(candidate =>
                    candidate != null &&
                    candidate.DiscoverySource ==
                        NERA.Locations.DiscoverySource.Drone &&
                    candidate.RequiredDroneTravelRange <= range);
            Assert.That(location, Is.Not.Null);

            drone.RefreshAvailability();
            float chargeBeforeLaunch = drone.CurrentBatteryCharge;
            Assert.That(drone.LaunchScan(location), Is.True);
            yield return null;
            AssertActiveDroneAnimation(
                views,
                DroneAnimationView.MainLaunchStateName,
                DroneAnimationView.MiniLaunchStateName);

            drone.AdvanceScan(location.DroneFlightDuration);
            Assert.That(drone.ScanProgress, Is.EqualTo(0f));
            Assert.That(
                drone.CurrentBatteryCharge,
                Is.EqualTo(chargeBeforeLaunch));
            Assert.That(discovery.IsDiscovered(location), Is.False);

            yield return new WaitForSeconds(
                GetDroneAnimationEventTime(
                    views,
                    DroneAnimationView.MainLaunchStateName,
                    "Start_Scan") + 0.1f);
            Assert.That(
                drone.CurrentBatteryCharge,
                Is.LessThan(chargeBeforeLaunch),
                "Start_Scan must begin the expedition and consume its charge.");

            DroneAnimationView mainAnimationView = views.Single(view =>
                view.GetComponent<Animator>()
                    .runtimeAnimatorController.name ==
                DroneAnimationView.MainControllerName);
            DroneAnimationView miniAnimationView = views.First(view =>
                view.gameObject.activeInHierarchy &&
                view.GetComponent<Animator>()
                    .runtimeAnimatorController.name ==
                DroneAnimationView.MiniControllerName);
            yield return new WaitForSeconds(1f);
            miniAnimationView.gameObject.SetActive(false);
            yield return null;
            miniAnimationView.gameObject.SetActive(true);
            yield return null;
            AssertDroneViewsSynchronized(
                mainAnimationView,
                miniAnimationView,
                DroneAnimationView.MainLaunchStateName,
                DroneAnimationView.MiniLaunchStateName);

            drone.AdvanceScan(location.DroneFlightDuration);
            yield return null;
            AssertActiveDroneAnimation(
                views,
                DroneAnimationView.MainReturnStateName,
                DroneAnimationView.MiniReturnStateName);
            Assert.That(discovery.IsDiscovered(location), Is.False);

            yield return new WaitForSeconds(1f);
            miniAnimationView.gameObject.SetActive(false);
            yield return null;
            miniAnimationView.gameObject.SetActive(true);
            yield return null;
            AssertDroneViewsSynchronized(
                mainAnimationView,
                miniAnimationView,
                DroneAnimationView.MainReturnStateName,
                DroneAnimationView.MiniReturnStateName);

            yield return new WaitForSeconds(
                GetDroneAnimationEventTime(
                    views,
                    DroneAnimationView.MainReturnStateName,
                    "End_Scan") + 0.1f);
            Assert.That(
                discovery.IsDiscovered(location),
                Is.True,
                "End_Scan must commit the expedition result.");
        }

        [UnityTest]
        public IEnumerator DroneUpgradeDoesNotPlayFlightAnimations()
        {
            SceneManager.LoadScene("MainScene");
            yield return WaitForScene("Player_Station");
            yield return null;
            yield return DisablePersistenceForTest();

            DroneScanController drone = DroneScanController.Instance;
            EnergySystemController energy = EnergySystemController.Instance;
            StationSystemsController systems = StationSystemsController.Instance;
            Assert.That(drone, Is.Not.Null);
            Assert.That(energy, Is.Not.Null);
            Assert.That(systems, Is.Not.Null);

            systems.ResetSystems();
            energy.RestoreState(energy.TotalCapacity, true);
            Assert.That(
                systems.SetRequestedActive(StationSystemType.Drone, true),
                Is.True);
            Assert.That(
                MaintainableObject.TryFind(
                    "station_drone",
                    out MaintainableObject maintenance),
                Is.True);
            maintenance.SetCondition(1f);
            drone.ResetBatteryCharge();
            drone.RefreshAvailability();
            yield return null;

            DroneAnimationView[] views =
                Object.FindObjectsByType<DroneAnimationView>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            Assert.That(views, Has.Length.EqualTo(3));
            AssertDroneViewsAtHome(views);

            StationSystemDefinition definition =
                systems.GetDefinition(StationSystemType.Drone);
            ItemData powerCore = Resources.Load<ItemCatalogData>(
                "ItemCatalog_Default").Find("item_power_core_01");
            EngineeringPartCompatibility compatibility = powerCore?
                .EngineeringPartDefinition?.CompatibleInstallations
                .FirstOrDefault(candidate =>
                    candidate != null &&
                    candidate.Matches(
                        StationSystemType.Drone,
                        definition.ObjectId,
                        candidate.SlotId) &&
                    definition.FindSlot(candidate.SlotId) != null &&
                    candidate.Modifiers.Any(modifier =>
                        modifier != null &&
                        modifier.Stat == StationObjectStat.BatteryCharge &&
                        modifier.Value > 0f));
            Assert.That(powerCore, Is.Not.Null);
            Assert.That(compatibility, Is.Not.Null);

            float capacityBefore = drone.BatteryCapacity;
            Assert.That(
                systems.TryInstallParts(
                    StationSystemType.Drone,
                    definition.ObjectId,
                    new[]
                    {
                        new StationPartInstallRequest(
                            compatibility.SlotId,
                            powerCore)
                    },
                    out string reason),
                Is.True,
                reason);
            yield return null;

            Assert.That(drone.BatteryCapacity, Is.GreaterThan(capacityBefore));
            Assert.That(drone.IsExpeditionInProgress, Is.False);
            AssertDroneViewsAtHome(views);
        }

        [UnityTest]
        public IEnumerator DroneCanSurveySecondLocationAfterRecharge()
        {
            SceneManager.LoadScene("MainScene");
            yield return WaitForScene("Player_Station");
            yield return null;
            yield return DisablePersistenceForTest();

            EnergySystemController energy = EnergySystemController.Instance;
            ExpeditionDiscoveryController discovery =
                ExpeditionDiscoveryController.Instance;
            DroneScanController drone = DroneScanController.Instance;
            StationSystemsController systems = StationSystemsController.Instance;
            StationEnvironmentController environment =
                StationEnvironmentController.Instance;

            Assert.That(energy, Is.Not.Null);
            Assert.That(discovery, Is.Not.Null);
            Assert.That(drone, Is.Not.Null);
            Assert.That(systems, Is.Not.Null);
            Assert.That(environment, Is.Not.Null);
            systems.ResetSystems();
            StationSystemDefinition droneDefinition =
                systems.GetDefinition(StationSystemType.Drone);
            Assert.That(droneDefinition, Is.Not.Null);
            Assert.That(
                MaintainableObject.TryFind(
                    droneDefinition.ObjectId,
                    out MaintainableObject droneMaintenance),
                Is.True);
            droneMaintenance.SetCondition(1f);
            float droneRange = systems.GetStat(
                StationSystemType.Drone,
                droneDefinition.ObjectId,
                StationObjectStat.TravelRange);
            ExpeditionLocationData first = discovery.KnownLocations
                .FirstOrDefault(
                    location =>
                        location != null &&
                        location.DiscoverySource ==
                            NERA.Locations.DiscoverySource.Drone &&
                        location.RequiredDroneTravelRange <= droneRange);
            ExpeditionLocationData second = discovery.KnownLocations
                .FirstOrDefault(
                    location =>
                        location != null &&
                        location != first &&
                        location.DiscoverySource ==
                            NERA.Locations.DiscoverySource.Drone &&
                        location.RequiredDroneTravelRange > droneRange);
            if (first == null || second == null)
            {
                Assert.Ignore(
                    "This upgrade scenario needs one currently reachable " +
                    "location and one location unlocked by a Drone part.");
            }

            discovery.RestoreDiscovered(Array.Empty<string>());
            energy.RestoreState(energy.TotalCapacity, true);
            Assert.That(
                systems.SetRequestedActive(StationSystemType.Drone, true),
                Is.True);
            drone.RefreshAvailability();
            yield return null;

            DroneAnimationView mainAnimation =
                Object.FindObjectsByType<DroneAnimationView>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None)
                    .Single(view =>
                        view.GetComponent<Animator>()
                            .runtimeAnimatorController.name ==
                        DroneAnimationView.MainControllerName);

            Assert.That(drone.LaunchScan(first), Is.True);
            mainAnimation.Start_Scan();
            drone.AdvanceScan(first.DroneFlightDuration);
            mainAnimation.End_Scan();
            Assert.That(discovery.IsDiscovered(first), Is.True);
            Assert.That(drone.IsCharging, Is.True);
            Assert.That(drone.CanLaunchScan(second), Is.False);

            // The first story discovery intentionally starts a sandstorm.
            // This test isolates recharge and upgrade range from that quest
            // weather gate; storm locking is covered by dedicated tests.
            environment.SetWeather(StationWeather.Clear);

            drone.AdvanceRecharge(drone.RechargeRemaining + 0.01f);

            Assert.That(drone.State, Is.EqualTo(DroneState.Ready));
            Assert.That(drone.IsCharging, Is.False);
            Assert.That(drone.LaunchScan(second), Is.False,
                "A distant expedition must remain locked before the drone upgrade.");

            ItemCatalogData catalog = Resources.Load<ItemCatalogData>("ItemCatalog_Default");
            ItemData propulsion = catalog != null ? catalog.Find("item_propulsion_01") : null;
            Assert.That(systems, Is.Not.Null);
            Assert.That(propulsion, Is.Not.Null);
            EngineeringPartCompatibility propulsionCompatibility =
                propulsion.EngineeringPartDefinition?.CompatibleInstallations
                    .FirstOrDefault(candidate =>
                        candidate != null &&
                        candidate.Matches(
                            StationSystemType.Drone,
                            droneDefinition.ObjectId,
                            candidate.SlotId) &&
                        droneDefinition.FindSlot(candidate.SlotId) != null &&
                        candidate.Modifiers.Any(modifier =>
                            modifier != null &&
                            modifier.Stat == StationObjectStat.TravelRange &&
                            modifier.Value > 0f));
            Assert.That(
                propulsionCompatibility,
                Is.Not.Null,
                "Propulsion must declare a valid travel-range slot for the drone.");
            Assert.That(
                systems.TryInstallParts(
                    StationSystemType.Drone,
                    droneDefinition.ObjectId,
                    new[]
                    {
                        new StationPartInstallRequest(
                            propulsionCompatibility.SlotId,
                            propulsion)
                    },
                    out string installReason),
                Is.True,
                installReason);

            Assert.That(systems.CanDroneReach(second), Is.True);
            Assert.That(drone.LaunchScan(second), Is.True);
            Assert.That(drone.ScanLocation, Is.EqualTo(second));
        }

        [UnityTest]
        public IEnumerator DroneCannotBeStoppedWhileScanningAndStopButtonIsHidden()
        {
            SceneManager.LoadScene("MainScene");
            yield return WaitForScene("Player_Station");
            yield return null;
            yield return DisablePersistenceForTest();

            EnergySystemController energy = EnergySystemController.Instance;
            ExpeditionDiscoveryController discovery =
                ExpeditionDiscoveryController.Instance;
            DroneScanController drone = DroneScanController.Instance;
            StationSystemsController systems = StationSystemsController.Instance;
            StationWeatherController weather = StationWeatherController.Instance;
            ParkourPlayerBridge player =
                Object.FindFirstObjectByType<ParkourPlayerBridge>();
            StationUpgradeableObject physicalDrone = Object
                .FindObjectsByType<StationUpgradeableObject>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .Single(candidate =>
                    candidate.SystemType == StationSystemType.Drone &&
                    candidate.ObjectId == "station_drone");
            Terminal.TerminalStationScreenController stationScreen =
                Object.FindFirstObjectByType<Terminal.TerminalStationScreenController>(
                    FindObjectsInactive.Include);

            Assert.That(energy, Is.Not.Null);
            Assert.That(discovery, Is.Not.Null);
            Assert.That(drone, Is.Not.Null);
            Assert.That(systems, Is.Not.Null);
            Assert.That(weather, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            Assert.That(physicalDrone, Is.Not.Null);
            Assert.That(stationScreen, Is.Not.Null);
            Assert.That(
                MaintainableObject.TryFind(
                    "station_drone",
                    out MaintainableObject maintenance),
                Is.True);
            Assert.That(
                stationScreen.transform.Find("background_Upgrade"),
                Is.Null,
                "The station terminal must remain status-only.");
            Assert.That(discovery.KnownLocations.Count, Is.GreaterThan(0));

            Assert.That(
                energy.GetConsumerRate("drone_charger"),
                Is.EqualTo(systems.GetStat(
                    StationSystemType.Drone,
                    "station_drone",
                    StationObjectStat.EnergyConsumption)));

            ExpeditionLocationData location = discovery.KnownLocations[0];
            weather.StopSandstorm();
            discovery.RestoreDiscovered(Array.Empty<string>());
            energy.RestoreState(energy.TotalCapacity, true);
            Assert.That(
                systems.SetRequestedActive(StationSystemType.Drone, true),
                Is.True);
            int connectedConsumerCount = energy.ConnectedConsumerCount;
            Assert.That(connectedConsumerCount, Is.GreaterThan(0));
            drone.RefreshAvailability();
            Assert.That(drone.LaunchScan(location), Is.True);
            Assert.That(drone.IsAtStation, Is.True);

            DroneAnimationView mainAnimation =
                Object.FindObjectsByType<DroneAnimationView>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None)
                    .Single(view =>
                        view.GetComponent<Animator>()
                            .runtimeAnimatorController.name ==
                        DroneAnimationView.MainControllerName);
            mainAnimation.Start_Scan();
            Assert.That(drone.IsAtStation, Is.False);
            Assert.That(physicalDrone.GetPrompt().IsVisible, Is.False);
            Assert.That(
                StationUpgradeModeController.GetOrCreate().Open(
                    physicalDrone,
                    player.gameObject),
                Is.False,
                "Upgrade mode must not open while the drone is away.");

            maintenance.SetCondition(0.5f);
            Assert.That(maintenance.Condition, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(
                maintenance.CanService,
                Is.False,
                "An absent drone must not be serviceable.");

            stationScreen.SelectSystem(StationSystemType.Drone);
            Transform powerSwitch = Array.Find(
                stationScreen.GetComponentsInChildren<Transform>(true),
                candidate => candidate.name == "Toggle");
            Assert.That(powerSwitch, Is.Not.Null, "Drone power switch not found.");
            Button onButton = powerSwitch.Find("OnButton").GetComponent<Button>();
            Button offButton = powerSwitch.Find("OffButton").GetComponent<Button>();
            Assert.That(onButton.interactable, Is.False);
            Assert.That(offButton.interactable, Is.False);
            Assert.That(
                systems.SetRequestedActive(StationSystemType.Drone, false),
                Is.False);
            Assert.That(
                systems.SetRequestedActive(StationSystemType.Drone, true),
                Is.False,
                "The drone state must be immutable while it is away.");
            Assert.That(
                systems.IsRequestedActive(StationSystemType.Drone),
                Is.True);
            Assert.That(drone.State, Is.EqualTo(DroneState.Scanning));

            drone.AdvanceScan(drone.CurrentScanDuration);
            mainAnimation.End_Scan();
            weather.StopSandstorm();
            Assert.That(drone.IsAtStation, Is.True);
            Assert.That(physicalDrone.GetPrompt().IsVisible, Is.True);
            Assert.That(maintenance.Condition, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(maintenance.NeedsService, Is.True);
            Assert.That(maintenance.IsCleaning, Is.False);
            Assert.That(weather.IsSandstormActive, Is.False);
            Assert.That(
                maintenance.CanService,
                Is.True,
                "Interaction and service must return with the drone.");
            Assert.That(drone.IsCharging, Is.True);
            Assert.That(
                energy.ConnectedConsumerCount,
                Is.EqualTo(connectedConsumerCount),
                "Drone charging must activate an existing connection, not add one.");
            drone.AdvanceRecharge(drone.RechargeRemaining + 0.01f);
            Assert.That(drone.IsCharging, Is.False);
            Assert.That(
                energy.ConnectedConsumerCount,
                Is.EqualTo(connectedConsumerCount),
                "The drone must remain connected after charging completes.");
            Assert.That(
                systems.SetRequestedActive(StationSystemType.Drone, false),
                Is.True);
            Assert.That(
                energy.ConnectedConsumerCount,
                Is.EqualTo(connectedConsumerCount - 1),
                "Only switching the drone off must remove its connection.");
        }

        [UnityTest]
        public IEnumerator BackpackUsesConfiguredAuthoredSlotPoints()
        {
            SceneManager.LoadScene("MainScene");
            yield return WaitForScene("Player_Station");
            yield return null;
            yield return DisablePersistenceForTest();

            InventoryLabHUDController hud = InventoryLabHUDController.Instance;
            PlayerInventory inventory =
                Object.FindFirstObjectByType<PlayerInventory>();

            Assert.That(hud, Is.Not.Null);
            Assert.That(inventory, Is.Not.Null);
            Assert.That(inventory.Config, Is.Not.Null);

            Transform content = FindDescendant(
                hud.transform.Find("InventoryScreen"),
                "background_Screen_Storage_Slot_Invent");
            Assert.That(content, Is.Not.Null);

            Assert.That(content.childCount, Is.EqualTo(inventory.BackpackCapacity));
            for (int i = 0; i < inventory.BackpackCapacity; i++)
            {
                Transform spawnPoint = content.Find($"Slot_{i + 1}");
                Assert.That(spawnPoint, Is.Not.Null);
                Assert.That(spawnPoint.gameObject.activeSelf, Is.True);
                Assert.That(
                    spawnPoint.GetComponent<InventorySlotView>(),
                    Is.Null,
                    "Slot_N must remain a spawn point, not an inventory slot.");
                Assert.That(
                    GetSpawnedInventorySlot(spawnPoint),
                    Is.Not.Null,
                    "P_InventorySlot was not spawned inside Slot_N.");
            }
        }

        [UnityTest]
        public IEnumerator InventoryScreenSupportsAllSlotsAndDropsSelectedItem()
        {
            SceneManager.LoadScene("MainScene");
            yield return WaitForScene("Player_Station");
            yield return null;
            yield return DisablePersistenceForTest();

            InventoryLabHUDController hud = InventoryLabHUDController.Instance;
            PlayerInventory inventory =
                Object.FindFirstObjectByType<PlayerInventory>();
            ItemCatalogData catalog =
                Resources.Load<ItemCatalogData>("ItemCatalog_Default");
            ItemData item = catalog != null
                ? catalog.Find("item_servo_drive_01")
                : null;

            Assert.That(hud, Is.Not.Null);
            Assert.That(inventory, Is.Not.Null);
            Assert.That(item, Is.Not.Null);
            Assert.That(item.WorldPrefab, Is.Not.Null);
            Assert.That(inventory.BackpackCapacity, Is.EqualTo(8));
            Assert.That(PlayerInventory.AnomalyCapacity, Is.EqualTo(4));
            Assert.That(PlayerInventory.QuickAccessCapacity, Is.EqualTo(4));
            Assert.That(PlayerInventory.ActiveQuickAccessCapacity, Is.EqualTo(4));

            Transform inventoryScreen = hud.transform.Find("InventoryScreen");
            Transform backpackRoot = FindDescendant(
                inventoryScreen,
                "background_Screen_Storage_Slot_Invent");
            Transform anomalyRoot = FindDescendant(
                inventoryScreen,
                "background_Screen_Storage_Slot_Invent_Anomaly");
            Transform quickRoot = hud.transform.Find("Slot_Invent_Equipment");
            Button dropButton = FindDescendant(
                inventoryScreen,
                "DropButton").GetComponent<Button>();

            Assert.That(backpackRoot.childCount, Is.EqualTo(8));
            Assert.That(anomalyRoot.childCount, Is.EqualTo(4));
            Assert.That(quickRoot.childCount, Is.EqualTo(4));

            foreach (Transform root in new[]
                     {
                         backpackRoot,
                         anomalyRoot,
                         quickRoot
                     })
            {
                for (int index = 0; index < root.childCount; index++)
                {
                    InventorySlotView view =
                        GetSpawnedInventorySlot(root.GetChild(index));
                    Assert.That(
                        view,
                        Is.Not.Null,
                        $"{root.name} Slot_{index + 1} has no P_InventorySlot.");
                    Assert.That(
                        view.GetComponent<InventorySlotDropTarget>(),
                        Is.Not.Null,
                        $"{root.name} slot {index + 1} has no drop target.");
                }
            }

            inventory.RestoreItemInstances(Array.Empty<ItemInstance>());
            Assert.That(inventory.AddItem(item), Is.True);
            hud.OpenInventory();
            Assert.That(inventoryScreen.gameObject.activeSelf, Is.True);

            Transform sourceSlot =
                GetSpawnedInventorySlot(backpackRoot.GetChild(0)).transform;
            Transform destinationSlot =
                GetSpawnedInventorySlot(backpackRoot.GetChild(7)).transform;
            LaboratoryInventoryItemDrag sourceDrag =
                sourceSlot.GetComponent<LaboratoryInventoryItemDrag>();
            InventorySlotDropTarget destination =
                destinationSlot.GetComponent<InventorySlotDropTarget>();
            Assert.That(sourceDrag, Is.Not.Null);
            Assert.That(sourceDrag.Item, Is.EqualTo(item));
            Assert.That(EventSystem.current, Is.Not.Null);

            PointerEventData dropEvent =
                new PointerEventData(EventSystem.current)
                {
                    pointerDrag = sourceSlot.gameObject
                };
            destination.OnDrop(dropEvent);
            Assert.That(
                inventory.GetItem(InventorySlotGroup.Backpack, 0),
                Is.Null);
            Assert.That(
                inventory.GetItem(InventorySlotGroup.Backpack, 7),
                Is.EqualTo(item));

            destinationSlot.GetComponent<Button>().onClick.Invoke();
            Assert.That(dropButton.interactable, Is.True);
            dropButton.onClick.Invoke();
            Assert.That(
                inventory.GetItem(InventorySlotGroup.Backpack, 7),
                Is.Null);
            Assert.That(inventory.Count, Is.Zero);

            WorldItem[] worldItems = Object.FindObjectsByType<WorldItem>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            WorldItem droppedItem = Array.Find(
                worldItems,
                worldItem => worldItem != null &&
                             worldItem.ItemData == item &&
                             worldItem.name.StartsWith("Dropped_"));
            Assert.That(
                droppedItem,
                Is.Not.Null,
                "DropButton did not create the selected world item.");

            Object.Destroy(droppedItem.gameObject);
            hud.CloseAll();
            yield return null;
        }

        [UnityTest]
        public IEnumerator RuntimeIoDropsIgnorePersistentWorldState()
        {
            SceneManager.LoadScene("MainScene");
            yield return WaitForScene("Player_Station");
            yield return null;
            yield return DisablePersistenceForTest();

            WorldStateController worldState = WorldStateController.Instance;
            ItemCatalogData catalog =
                Resources.Load<ItemCatalogData>("ItemCatalog_Default");
            ItemData anomaly = catalog?.Find("io_blue_shard_01");
            Assert.That(worldState, Is.Not.Null);
            Assert.That(anomaly, Is.Not.Null);
            Assert.That(anomaly.WorldPrefab, Is.Not.Null);

            worldState.ResetState();
            worldState.MarkConsumed("/drop");

            var existingDropIds = new HashSet<int>(
                Object.FindObjectsByType<WorldItem>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None)
                    .Select(item => item.GetInstanceID()));

            IOEnemyConfig config =
                ScriptableObject.CreateInstance<IOEnemyConfig>();
            SetPrivateField(
                config,
                "deathDropPrefab",
                anomaly.WorldPrefab.gameObject);

            GameObject firstEnemyObject = new GameObject("Test_RuntimeIO_1");
            IOEnemyController firstEnemy =
                firstEnemyObject.AddComponent<IOEnemyController>();
            SetPrivateField(firstEnemy, "config", config);
            firstEnemy.TakeDamage(float.MaxValue, null);
            yield return null;
            yield return null;

            WorldItem[] firstDrops = Object.FindObjectsByType<WorldItem>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .Where(item =>
                    item.ItemData == anomaly &&
                    !existingDropIds.Contains(item.GetInstanceID()))
                .ToArray();
            Assert.That(
                firstDrops,
                Has.Length.EqualTo(1),
                "The first runtime IO drop was suppressed by stale '/drop' state.");
            Assert.That(firstDrops[0].TracksWorldState, Is.False);
            Assert.That(firstDrops[0].PersistentKey, Is.Empty);

            GameObject secondEnemyObject = new GameObject("Test_RuntimeIO_2");
            IOEnemyController secondEnemy =
                secondEnemyObject.AddComponent<IOEnemyController>();
            SetPrivateField(secondEnemy, "config", config);
            secondEnemy.TakeDamage(float.MaxValue, null);
            yield return null;
            yield return null;

            WorldItem[] runtimeDrops = Object.FindObjectsByType<WorldItem>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .Where(item =>
                    item.ItemData == anomaly &&
                    !existingDropIds.Contains(item.GetInstanceID()))
                .ToArray();
            Assert.That(
                runtimeDrops,
                Has.Length.EqualTo(2),
                "Sequential runtime IO kills must produce independent drops.");
            Assert.That(
                runtimeDrops.All(item => !item.TracksWorldState),
                Is.True);

            const string authoredEnemyKey =
                "player_station/test_authored_io";
            worldState.MarkConsumed(authoredEnemyKey + "/drop");
            GameObject authoredEnemyObject =
                new GameObject("Test_AuthoredIO");
            IOEnemyController authoredEnemy =
                authoredEnemyObject.AddComponent<IOEnemyController>();
            SetPrivateField(authoredEnemy, "config", config);
            SetPrivateField(
                authoredEnemy,
                "persistentKey",
                authoredEnemyKey);
            authoredEnemy.TakeDamage(float.MaxValue, null);
            yield return null;
            yield return null;

            WorldItem[] dropsAfterAuthoredKill =
                Object.FindObjectsByType<WorldItem>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None)
                    .Where(item =>
                        item.ItemData == anomaly &&
                        !existingDropIds.Contains(item.GetInstanceID()))
                    .ToArray();
            Assert.That(
                dropsAfterAuthoredKill,
                Has.Length.EqualTo(2),
                "A consumed authored IO drop must remain suppressed.");

            foreach (WorldItem drop in runtimeDrops)
                Object.Destroy(drop.gameObject);
            Object.Destroy(config);
            worldState.ResetState();
            yield return null;
        }

        [UnityTest]
        public IEnumerator LaboratoryScreenUsesUnifiedInventoryAndWorkflows()
        {
            SceneManager.LoadScene("MainScene");
            yield return WaitForScene("Player_Station");
            yield return null;
            yield return DisablePersistenceForTest();

            InventoryLabHUDController hud = InventoryLabHUDController.Instance;
            PlayerInventory inventory =
                Object.FindFirstObjectByType<PlayerInventory>();
            ResearchController research = ResearchController.Instance;
            LaboratoryWorkstationController workstation =
                LaboratoryWorkstationController.Instance;
            LaboratoryTableItemVisuals tableVisuals =
                Object.FindFirstObjectByType<LaboratoryTableItemVisuals>();
            ItemCatalogData catalog =
                Resources.Load<ItemCatalogData>("ItemCatalog_Default");
            ItemData pistol = catalog != null
                ? catalog.Find("energy_pistol_01")
                : null;
            ItemData integrator = catalog != null
                ? catalog.Find("io_integrator_01")
                : null;
            ItemData anomaly = catalog != null
                ? catalog.Find("io_blue_shard_01")
                : null;
            ItemData record = catalog != null
                ? catalog.Find("ancient_record_02")
                : null;

            Assert.That(hud, Is.Not.Null);
            Assert.That(inventory, Is.Not.Null);
            Assert.That(research, Is.Not.Null);
            Assert.That(workstation, Is.Not.Null);
            Assert.That(tableVisuals, Is.Not.Null);
            Assert.That(pistol, Is.Not.Null);
            Assert.That(integrator, Is.Not.Null);
            Assert.That(anomaly, Is.Not.Null);
            Assert.That(record, Is.Not.Null);

            inventory.RestoreItemInstances(Array.Empty<ItemInstance>());
            research.RestoreAnalyzed(Array.Empty<string>());
            research.RestoreLoadedItem(null, null);
            workstation.RestoreItems(
                Array.Empty<ItemInstance>(),
                Array.Empty<ItemInstance>());
            Assert.That(inventory.AddItem(pistol), Is.True, "Pistol was not added.");
            Assert.That(
                inventory.AddItem(integrator),
                Is.True,
                "IO Integrator was not added.");
            Assert.That(inventory.AddItem(anomaly), Is.True, "Anomaly was not added.");
            Assert.That(inventory.AddItem(record), Is.True, "Record was not added.");

            EnergySystemController energy = EnergySystemController.Instance;
            Assert.That(energy, Is.Not.Null);
            energy.RestoreState(energy.TotalCapacity, true);
            StationSystemsController.Instance.SetRequestedActive(
                StationSystemType.Laboratory,
                true);

            hud.OpenLaboratory(inventory.gameObject);
            yield return null;

            Transform laboratory = hud.transform.Find("LaboratoryScreen");
            LaboratoryScreenController screen =
                laboratory.GetComponent<LaboratoryScreenController>();
            Transform sharedInventory =
                laboratory.Find("Inventory_and_info_Screen");
            Transform powerScreen = laboratory.Find("PowerScreen");
            Transform scanScreen = laboratory.Find("ScanScreen");
            Transform upgradeScreen = laboratory.Find("UpgradeScreen");

            Assert.That(laboratory.gameObject.activeSelf, Is.True, "LaboratoryScreen is closed.");
            Assert.That(screen, Is.Not.Null);
            Assert.That(sharedInventory.gameObject.activeSelf, Is.True, "Shared inventory is hidden.");
            Assert.That(scanScreen.gameObject.activeSelf, Is.True, "Scan screen is not the default.");
            Assert.That(powerScreen.gameObject.activeSelf, Is.False);
            Assert.That(upgradeScreen.gameObject.activeSelf, Is.False);

            AssertLaboratoryInventoryGroup(
                sharedInventory,
                "background_Screen_Storage_Slot_Invent",
                8);
            AssertLaboratoryInventoryGroup(
                sharedInventory,
                "background_Screen_Storage_Slot_Invent_Anomaly",
                4);
            AssertLaboratoryInventoryGroup(
                sharedInventory,
                "background_Screen_Storage_Slot_Invent_Equipment",
                4);

            laboratory.Find("PowerMapButton").GetComponent<Button>()
                .onClick.Invoke();
            Assert.That(powerScreen.gameObject.activeSelf, Is.True, "Power tab did not open.");
            Assert.That(scanScreen.gameObject.activeSelf, Is.False);
            Assert.That(upgradeScreen.gameObject.activeSelf, Is.False);
            Assert.That(sharedInventory.gameObject.activeSelf, Is.True, "Shared inventory hid after tab switch.");

            laboratory.Find("NextButton").GetComponent<Button>()
                .onClick.Invoke();
            Assert.That(screen.ActiveModeIndex, Is.EqualTo(1));
            Assert.That(scanScreen.gameObject.activeSelf, Is.True);
            laboratory.Find("BackButton").GetComponent<Button>()
                .onClick.Invoke();
            Assert.That(screen.ActiveModeIndex, Is.EqualTo(0));
            yield return null;
            Canvas.ForceUpdateCanvases();

            LaboratoryInventoryItemDrag pistolDrag =
                FindPlayerInventoryDrag(laboratory, pistol);
            Transform powerSlotRoot = FindDescendant(
                powerScreen,
                "Slot_01");
            InventorySlotView powerSlot =
                GetSpawnedInventorySlot(powerSlotRoot);
            Assert.That(pistolDrag, Is.Not.Null);
            Assert.That(powerSlot, Is.Not.Null);
            Button inventorySlotButton =
                pistolDrag.GetComponent<Button>();
            Assert.That(inventorySlotButton, Is.Not.Null);
            ClickThroughUi(inventorySlotButton);
            Component laboratoryInfoName = FindDescendant(
                    sharedInventory,
                    "Text_Name")
                .GetComponent("TextMeshProUGUI");
            Assert.That(
                laboratoryInfoName.GetType().GetProperty("text")
                    ?.GetValue(laboratoryInfoName)?.ToString(),
                Is.EqualTo(pistol.DisplayName),
                "Laboratory inventory slot did not select its item.");

            Canvas.ForceUpdateCanvases();
            DropThroughUi(
                pistolDrag,
                powerSlot.GetComponent<LaboratoryItemDropSlot>());

            Assert.That(
                workstation.GetChargingItem(0)?.ItemData,
                Is.SameAs(pistol));
            AssertLaboratoryTableVisual(
                tableVisuals.GetChargingVisual(0),
                tableVisuals.transform.Find("Slot_Power/Slot_1"));
            Transform progressTransform = FindDescendant(
                powerScreen,
                "Text_progress_01");
            Component progress = progressTransform.GetComponent(
                "TextMeshProUGUI");
            Assert.That(progressTransform.gameObject.activeSelf, Is.True, "Charge progress is hidden.");
            Assert.That(
                progress.GetType().GetProperty("text")?.GetValue(progress)
                    ?.ToString(),
                Does.EndWith("%"));

            FindDescendant(powerScreen, "DropButton")
                .GetComponent<Button>().onClick.Invoke();
            Assert.That(workstation.GetChargingItem(0), Is.Null);
            Assert.That(tableVisuals.GetChargingVisual(0), Is.Null);
            Assert.That(inventory.Contains(pistol.ItemId), Is.True, "Power Drop did not return pistol.");

            laboratory.Find("UpgradeMapButton").GetComponent<Button>()
                .onClick.Invoke();
            LaboratoryInventoryItemDrag rejectedPistol =
                FindPlayerInventoryDrag(laboratory, pistol);
            LaboratoryInventoryItemDrag upgradeIntegrator =
                FindPlayerInventoryDrag(laboratory, integrator);
            LaboratoryInventoryItemDrag upgradeAnomaly =
                FindPlayerInventoryDrag(laboratory, anomaly);
            Transform upgradeSlot01 = upgradeScreen.transform.Find(
                "background_Screen_Storage_Slot/Slot_01");
            Transform upgradeSlot02 = upgradeScreen.transform.Find(
                "background_Screen_Storage_Slot/Slot_02");
            GetSpawnedInventorySlot(upgradeSlot01)
                .GetComponent<LaboratoryItemDropSlot>()
                .ItemDropped.Invoke(rejectedPistol);
            Assert.That(
                workstation.GetUpgradeItem(0),
                Is.Null,
                "Ordinary weapons must not enter the integration slot.");
            GetSpawnedInventorySlot(upgradeSlot01)
                .GetComponent<LaboratoryItemDropSlot>()
                .ItemDropped.Invoke(upgradeIntegrator);
            GetSpawnedInventorySlot(upgradeSlot02)
                .GetComponent<LaboratoryItemDropSlot>()
                .ItemDropped.Invoke(upgradeAnomaly);

            Assert.That(
                workstation.GetUpgradeItem(0)?.ItemData,
                Is.SameAs(integrator));
            Assert.That(
                workstation.GetUpgradeItem(1)?.ItemData,
                Is.SameAs(anomaly));
            AssertLaboratoryTableVisual(
                tableVisuals.GetUpgradeVisual(0),
                tableVisuals.transform.Find("Slot_Upgrade/Slot_1"));
            AssertLaboratoryTableVisual(
                tableVisuals.GetUpgradeVisual(1),
                tableVisuals.transform.Find("Slot_Upgrade/Slot_2"));

            LaboratoryInventoryItemDrag blockedScanRecord =
                FindPlayerInventoryDrag(laboratory, record);
            Assert.That(blockedScanRecord, Is.Not.Null);
            Assert.That(
                research.LoadItem(
                    blockedScanRecord.Item,
                    inventory,
                    blockedScanRecord.SourceGroup,
                    blockedScanRecord.SourceIndex),
                Is.False,
                "An occupied upgrade anomaly slot must reserve the laboratory scanner.");
            Assert.That(research.LoadedItem, Is.Null);
            Assert.That(
                FindDescendant(upgradeScreen, "UpgradeButton")
                    .GetComponent<Button>().interactable,
                Is.False,
                "Synthesis is intentionally reserved for the next mechanic.");

            FindDescendant(upgradeScreen, "DropButton")
                .GetComponent<Button>().onClick.Invoke();
            Assert.That(workstation.GetUpgradeItem(0), Is.Null);
            Assert.That(workstation.GetUpgradeItem(1), Is.Null);
            Assert.That(tableVisuals.GetUpgradeVisual(0), Is.Null);
            Assert.That(tableVisuals.GetUpgradeVisual(1), Is.Null);

            laboratory.Find("ScanMapButton").GetComponent<Button>()
                .onClick.Invoke();
            LaboratoryInventoryItemDrag scanAnomaly =
                FindPlayerInventoryDrag(laboratory, anomaly);
            InventorySlotView scanSlot = GetSpawnedInventorySlot(
                scanScreen.transform.Find(
                    "background_Screen_Storage_Slot/Slot"));
            scanSlot.GetComponent<LaboratoryItemDropSlot>()
                .ItemDropped.Invoke(scanAnomaly);
            Assert.That(research.LoadedItem, Is.SameAs(anomaly));
            AssertLaboratoryTableVisual(
                tableVisuals.ScanVisual,
                tableVisuals.transform.Find("Slot_Scan/Slot_1"));

            LaboratoryInventoryItemDrag scanRecord =
                FindPlayerInventoryDrag(laboratory, record);
            Assert.That(scanRecord, Is.Not.Null);
            scanRecord.OnPointerDown(
                new PointerEventData(EventSystem.current));
            Assert.That(
                laboratoryInfoName.GetType().GetProperty("text")
                    ?.GetValue(laboratoryInfoName)?.ToString(),
                Is.EqualTo(record.DisplayName),
                "Starting a drag did not update laboratory item info.");
            scanSlot.GetComponent<LaboratoryItemDropSlot>()
                .ItemDropped.Invoke(scanRecord);
            Assert.That(
                research.LoadedItem,
                Is.SameAs(record),
                "A different item type did not replace the loaded sample.");
            GameObject scanRecordVisual = tableVisuals.ScanVisual;
            AssertLaboratoryTableVisual(
                scanRecordVisual,
                tableVisuals.transform.Find("Slot_Scan/Slot_1"));
            Assert.That(
                inventory.GetItem(
                    InventorySlotGroup.Anomaly,
                    0),
                Is.SameAs(anomaly),
                "The replaced anomaly did not return to its nearest typed slot.");

            LaboratoryInventoryItemDrag blockedUpgradeAnomaly =
                FindPlayerInventoryDrag(laboratory, anomaly);
            Assert.That(blockedUpgradeAnomaly, Is.Not.Null);
            Assert.That(
                workstation.LoadUpgradeItem(
                    1,
                    inventory,
                    blockedUpgradeAnomaly.SourceGroup,
                    blockedUpgradeAnomaly.SourceIndex),
                Is.False,
                "An occupied scan slot must reserve the anomaly upgrade slot.");
            Assert.That(workstation.GetUpgradeItem(1), Is.Null);

            Button scanButton = FindDescendant(
                scanScreen,
                "ScanButton").GetComponent<Button>();
            Button scanDrop = FindDescendant(
                scanScreen,
                "DropButton").GetComponent<Button>();
            Transform scanProgressTransform = FindDescendant(
                scanScreen,
                "Text_progress");
            Component scanProgressText =
                scanProgressTransform.GetComponent("TextMeshProUGUI");
            Assert.That(scanProgressTransform, Is.Not.Null);
            Assert.That(scanProgressText, Is.Not.Null);
            Assert.That(
                scanProgressTransform.gameObject.activeSelf,
                Is.False,
                "Scan progress must stay hidden before scanning.");
            Assert.That(scanButton.interactable, Is.True, "Scan button stayed disabled.");
            scanButton.onClick.Invoke();
            yield return null;

            Assert.That(
                research.State,
                Is.EqualTo(ResearchController.ResearchState.Analyzing));
            Assert.That(scanDrop.interactable, Is.False);
            Assert.That(
                scanSlot.LaboratoryDrag.enabled,
                Is.False,
                "The sample must not be draggable while scanning.");
            Assert.That(
                scanProgressTransform.gameObject.activeSelf,
                Is.True,
                "Scan progress did not appear.");
            Assert.That(
                scanProgressText.GetType().GetProperty("text")
                    ?.GetValue(scanProgressText)?.ToString(),
                Does.Match(@"^Progress - \d+%$"));

            research.AdvanceAnalysis(
                record.ResearchDefinition.AnalysisDuration * 0.5f);
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(
                scanProgressTransform.gameObject.activeSelf,
                Is.True);
            Assert.That(
                scanProgressText.GetType().GetProperty("text")
                    ?.GetValue(scanProgressText)?.ToString(),
                Is.Not.EqualTo("Progress - 0%"),
                "Scan percentage did not change.");

            research.AdvanceAnalysis(999f);
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(
                tableVisuals.ScanVisual,
                Is.SameAs(scanRecordVisual),
                "The scan visual must remain after analysis until retrieval.");
            Assert.That(scanDrop.interactable, Is.True, "Scan Drop stayed disabled.");
            Assert.That(scanSlot.LaboratoryDrag.enabled, Is.True, "Scanned sample stayed locked.");
            Assert.That(
                scanProgressTransform.gameObject.activeSelf,
                Is.False,
                "Scan progress stayed visible after completion.");
            scanDrop.onClick.Invoke();
            Assert.That(research.LoadedItem, Is.Null);
            Assert.That(tableVisuals.ScanVisual, Is.Null);
            Assert.That(inventory.Contains(anomaly.ItemId), Is.True);
            Assert.That(
                inventory.Contains(record.ItemId),
                Is.True,
                "Scan Drop did not return the replacement sample.");

            LaboratoryInventoryItemDrag firstAnomalyScan =
                FindPlayerInventoryDrag(laboratory, anomaly);
            scanSlot.GetComponent<LaboratoryItemDropSlot>()
                .ItemDropped.Invoke(firstAnomalyScan);
            Assert.That(research.LoadedItem, Is.SameAs(anomaly));
            Assert.That(research.LoadedItemInstance.IsScanned, Is.False);
            Assert.That(scanButton.interactable, Is.True);
            scanButton.onClick.Invoke();
            research.AdvanceAnalysis(999f);
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(research.LoadedItemInstance.IsScanned, Is.True);
            scanDrop.onClick.Invoke();

            laboratory.Find("UpgradeMapButton").GetComponent<Button>()
                .onClick.Invoke();
            LaboratoryInventoryItemDrag synthesizedIntegrator =
                FindPlayerInventoryDrag(laboratory, integrator);
            LaboratoryInventoryItemDrag synthesizedAnomaly =
                FindPlayerInventoryDrag(laboratory, anomaly);
            Assert.That(synthesizedIntegrator, Is.Not.Null);
            Assert.That(synthesizedAnomaly, Is.Not.Null);
            GetSpawnedInventorySlot(upgradeSlot01)
                .GetComponent<LaboratoryItemDropSlot>()
                .ItemDropped.Invoke(synthesizedIntegrator);
            GetSpawnedInventorySlot(upgradeSlot02)
                .GetComponent<LaboratoryItemDropSlot>()
                .ItemDropped.Invoke(synthesizedAnomaly);
            AssertLaboratoryTableVisual(
                tableVisuals.GetUpgradeVisual(0),
                tableVisuals.transform.Find("Slot_Upgrade/Slot_1"));
            GameObject synthesisAnomalyVisual =
                tableVisuals.GetUpgradeVisual(1);
            AssertLaboratoryTableVisual(
                synthesisAnomalyVisual,
                tableVisuals.transform.Find("Slot_Upgrade/Slot_2"));

            Button synthesisButton =
                FindDescendant(upgradeScreen, "UpgradeButton")
                    .GetComponent<Button>();
            Transform synthesisProgressTransform = FindDescendant(
                upgradeScreen,
                "Text_progress");
            Component synthesisProgressText =
                synthesisProgressTransform.GetComponent("TextMeshProUGUI");
            Assert.That(synthesisProgressTransform.gameObject.activeSelf, Is.False);
            Assert.That(
                synthesisButton.interactable,
                Is.True,
                "UpgradeButton stayed disabled for an analyzed IO shard.");
            synthesisButton.onClick.Invoke();

            Assert.That(workstation.IsUpgradeProcessing, Is.True);
            Assert.That(
                workstation.GetUpgradeItem(1),
                Is.Not.Null,
                "The anomaly must remain in its slot until synthesis completes.");
            Assert.That(
                tableVisuals.GetUpgradeVisual(1),
                Is.SameAs(synthesisAnomalyVisual));
            Assert.That(synthesisProgressTransform.gameObject.activeSelf, Is.True);
            Assert.That(
                synthesisProgressText.GetType().GetProperty("text")
                    ?.GetValue(synthesisProgressText)?.ToString(),
                Does.Match(@"^Progress - \d+%$"));
            Assert.That(
                FindDescendant(upgradeScreen, "DropButton")
                    .GetComponent<Button>().interactable,
                Is.False,
                "Synthesis items must stay locked while processing.");

            float synthesisDuration = workstation.CurrentSynthesisDuration;
            workstation.AdvanceSynthesis(synthesisDuration * 0.5f);
            Assert.That(
                synthesisProgressText.GetType().GetProperty("text")
                    ?.GetValue(synthesisProgressText)?.ToString(),
                Is.Not.EqualTo("Progress - 0%"),
                "Synthesis percentage did not change.");

            workstation.AdvanceSynthesis(synthesisDuration);
            Assert.That(workstation.IsUpgradeProcessing, Is.False);
            Assert.That(synthesisProgressTransform.gameObject.activeSelf, Is.False);

            ItemInstance synthesizedTool =
                workstation.GetUpgradeItem(0);
            Assert.That(synthesizedTool, Is.Not.Null);
            Assert.That(
                synthesizedTool.IntegratedAnomaly,
                Is.SameAs(anomaly));
            Assert.That(synthesizedTool.AnomalyCharges, Is.EqualTo(1));
            Assert.That(synthesizedTool.IsFullyCharged, Is.True);
            Assert.That(
                workstation.GetUpgradeItem(1),
                Is.Null,
                "Synthesis did not consume the IO shard.");
            Assert.That(
                tableVisuals.GetUpgradeVisual(1),
                Is.Null,
                "The consumed anomaly visual remained on the laboratory table.");
            Assert.That(
                tableVisuals.GetUpgradeVisual(0),
                Is.Not.Null,
                "The upgraded tool visual disappeared before retrieval.");

            FindDescendant(upgradeScreen, "DropButton")
                .GetComponent<Button>().onClick.Invoke();
            Assert.That(tableVisuals.GetUpgradeVisual(0), Is.Null);
            Assert.That(inventory.Contains(integrator.ItemId), Is.True);
            Assert.That(inventory.Contains(anomaly.ItemId), Is.False);

            ItemInstance equippedIntegrator =
                inventory.QuickAccessItemInstances.FirstOrDefault(
                    instance => instance?.ItemData == integrator);
            PlayerEquipmentController equipmentController =
                inventory.GetComponent<PlayerEquipmentController>();
            Assert.That(equippedIntegrator, Is.Not.Null);
            Assert.That(equipmentController, Is.Not.Null);
            Assert.That(
                equipmentController.TryUseIntegratedAnomaly(
                    equippedIntegrator),
                Is.True,
                "R activation failed for the IO Integrator.");
            Assert.That(equippedIntegrator.Charge, Is.Zero);
            Assert.That(equippedIntegrator.IntegratedAnomaly, Is.Null);

            laboratory.Find("PowerMapButton").GetComponent<Button>()
                .onClick.Invoke();
            LaboratoryInventoryItemDrag dischargedIntegrator =
                FindPlayerInventoryDrag(laboratory, integrator);
            powerSlot.GetComponent<LaboratoryItemDropSlot>()
                .ItemDropped.Invoke(dischargedIntegrator);
            Assert.That(
                workstation.GetChargingItem(0)?.ItemData,
                Is.SameAs(integrator));
            workstation.AdvanceCharging(
                integrator.EnergyDefinition.Capacity /
                integrator.EnergyDefinition.RechargePerSecond + 0.1f);
            Assert.That(
                workstation.GetChargingItem(0)?.IsFullyCharged,
                Is.True,
                "The IO Integrator did not recharge.");
            FindDescendant(powerScreen, "DropButton")
                .GetComponent<Button>().onClick.Invoke();

            Assert.That(
                inventory.AddItem(anomaly),
                Is.True,
                "A second shard was not added.");
            ItemInstance secondAnomalyInstance =
                inventory.AnomalyItemInstances.First(
                    instance => instance?.ItemData == anomaly);
            Assert.That(
                secondAnomalyInstance.IsScanned,
                Is.False,
                "A new instance must not inherit scan state from its type.");

            laboratory.Find("UpgradeMapButton").GetComponent<Button>()
                .onClick.Invoke();
            LaboratoryInventoryItemDrag rechargedIntegrator =
                FindPlayerInventoryDrag(laboratory, integrator);
            LaboratoryInventoryItemDrag secondAnalyzedAnomaly =
                FindPlayerInventoryDrag(laboratory, anomaly);
            GetSpawnedInventorySlot(upgradeSlot01)
                .GetComponent<LaboratoryItemDropSlot>()
                .ItemDropped.Invoke(rechargedIntegrator);
            GetSpawnedInventorySlot(upgradeSlot02)
                .GetComponent<LaboratoryItemDropSlot>()
                .ItemDropped.Invoke(secondAnalyzedAnomaly);
            Assert.That(
                synthesisButton.interactable,
                Is.False,
                "An unscanned second shard incorrectly inherited access.");

            FindDescendant(upgradeScreen, "DropButton")
                .GetComponent<Button>().onClick.Invoke();
            laboratory.Find("ScanMapButton").GetComponent<Button>()
                .onClick.Invoke();
            LaboratoryInventoryItemDrag secondAnomalyScan =
                FindPlayerInventoryDrag(laboratory, anomaly);
            scanSlot.GetComponent<LaboratoryItemDropSlot>()
                .ItemDropped.Invoke(secondAnomalyScan);
            Assert.That(
                research.LoadedItemInstance,
                Is.SameAs(secondAnomalyInstance));
            Assert.That(scanButton.interactable, Is.True);
            scanButton.onClick.Invoke();
            research.AdvanceAnalysis(999f);
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(secondAnomalyInstance.IsScanned, Is.True);
            Assert.That(
                research.AnalyzedResearchIds.Count,
                Is.EqualTo(2),
                "The known anomaly type must not create a duplicate research id.");
            scanDrop.onClick.Invoke();

            laboratory.Find("UpgradeMapButton").GetComponent<Button>()
                .onClick.Invoke();
            rechargedIntegrator =
                FindPlayerInventoryDrag(laboratory, integrator);
            secondAnalyzedAnomaly =
                FindPlayerInventoryDrag(laboratory, anomaly);
            GetSpawnedInventorySlot(upgradeSlot01)
                .GetComponent<LaboratoryItemDropSlot>()
                .ItemDropped.Invoke(rechargedIntegrator);
            GetSpawnedInventorySlot(upgradeSlot02)
                .GetComponent<LaboratoryItemDropSlot>()
                .ItemDropped.Invoke(secondAnalyzedAnomaly);
            Assert.That(
                synthesisButton.interactable,
                Is.True,
                "The second shard stayed locked after its own scan.");
            synthesisButton.onClick.Invoke();
            Assert.That(workstation.IsUpgradeProcessing, Is.True);
            workstation.AdvanceSynthesis(
                workstation.CurrentSynthesisDuration);
            Assert.That(
                workstation.GetUpgradeItem(0)?.IntegratedAnomaly,
                Is.SameAs(anomaly));
            Assert.That(workstation.GetUpgradeItem(1), Is.Null);

            hud.CloseAll();
            yield return null;
        }

        [UnityTest]
        public IEnumerator DevicesPanelMatchesTypedStationStorageCapacities()
        {
            SceneManager.LoadScene("MainScene");
            yield return WaitForScene("Player_Station");
            yield return null;
            yield return DisablePersistenceForTest();

            StationStorageController storage = StationStorageController.Instance;
            Terminal.TerminalStorageScreenController storageScreen =
                Object.FindFirstObjectByType<Terminal.TerminalStorageScreenController>(
                    FindObjectsInactive.Include);

            Assert.That(storage, Is.Not.Null);
            Assert.That(storageScreen, Is.Not.Null);
            Assert.That(storage.BackpackSlots.Count, Is.EqualTo(16));
            Assert.That(storage.QuickAccessSlots.Count, Is.EqualTo(16));
            Assert.That(storage.AnomalySlots.Count, Is.EqualTo(16));

            Assert.That(
                CountDirectSlotButtons(storageScreen.transform.Find(
                    "background_Screen_Storage_Slot")),
                Is.EqualTo(storage.BackpackSlots.Count));
            Assert.That(
                CountDirectSlotButtons(storageScreen.transform.Find(
                    "background_Screen_Storage_Slot_Equipment")),
                Is.EqualTo(storage.QuickAccessSlots.Count));
            Assert.That(
                CountDirectSlotButtons(storageScreen.transform.Find(
                    "background_Screen_Storage_Slot_Anomaly")),
                Is.EqualTo(storage.AnomalySlots.Count));
        }

        [UnityTest]
        public IEnumerator DevicesTabShowsInventoryAndOtherTabsHideIt()
        {
            SceneManager.LoadScene("MainScene");
            yield return WaitForScene("Player_Station");
            yield return null;
            yield return DisablePersistenceForTest();

            Terminal.TerminalUIScreen terminal =
                Object.FindFirstObjectByType<Terminal.TerminalUIScreen>(
                    FindObjectsInactive.Include);
            InventoryLabHUDController inventoryHud = InventoryLabHUDController.Instance;
            Assert.That(terminal, Is.Not.Null);
            Assert.That(inventoryHud, Is.Not.Null);

            EnergySystemController.Instance.RestoreState(
                EnergySystemController.Instance.TotalCapacity,
                true);
            StationSystemsController.Instance.SetCriticalSystemActive(
                StationSystemType.Terminal,
                true);
            terminal.Open();

            Button devicesButton = terminal.transform.Find("StorageButton")
                .GetComponent<Button>();
            Button statusButton = terminal.transform.Find("StationButton")
                .GetComponent<Button>();
            Transform storageScreen = terminal.transform.Find("StorageScreen");

            devicesButton.onClick.Invoke();
            Assert.That(storageScreen.gameObject.activeSelf, Is.True);
            Assert.That(
                storageScreen.Find("background_Screen_Storage_Slot_Invent")
                    .gameObject.activeSelf,
                Is.True);
            Assert.That(
                storageScreen.Find("background_Screen_Storage_Slot_Invent_Anomaly")
                    .gameObject.activeSelf,
                Is.True);
            Assert.That(
                storageScreen.Find("background_Screen_Storage_Slot_Invent_Equipment")
                    .gameObject.activeSelf,
                Is.True);

            statusButton.onClick.Invoke();
            Assert.That(storageScreen.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator InventoryItemCanBeDraggedIntoStationStorageThroughUi()
        {
            SceneManager.LoadScene("MainScene");
            yield return WaitForScene("Player_Station");
            yield return null;
            yield return DisablePersistenceForTest();

            PlayerInventory inventory = Object.FindFirstObjectByType<PlayerInventory>();
            StationStorageController storage = StationStorageController.Instance;
            Terminal.TerminalUIScreen terminal =
                Object.FindFirstObjectByType<Terminal.TerminalUIScreen>(
                    FindObjectsInactive.Include);
            ItemCatalogData catalog = Resources.Load<ItemCatalogData>("ItemCatalog_Default");
            ItemData item = catalog != null ? catalog.Find("item_servo_drive_01") : null;

            Assert.That(inventory, Is.Not.Null);
            Assert.That(storage, Is.Not.Null);
            Assert.That(terminal, Is.Not.Null);
            Assert.That(item, Is.Not.Null);
            inventory.RestoreItemInstances(Array.Empty<ItemInstance>());
            storage.ResetStorage();
            Assert.That(inventory.AddItem(item), Is.True);

            EnergySystemController.Instance.RestoreState(
                EnergySystemController.Instance.TotalCapacity,
                true);
            StationSystemsController.Instance.SetCriticalSystemActive(
                StationSystemType.Terminal,
                true);
            terminal.Open();
            terminal.transform.Find("StorageButton").GetComponent<Button>()
                .onClick.Invoke();
            yield return null;
            Canvas.ForceUpdateCanvases();

            Transform storageScreenRoot = terminal.transform.Find("StorageScreen");
            Assert.That(storageScreenRoot, Is.Not.Null, "StorageScreen not found.");
            LaboratoryInventoryItemDrag source = null;
            foreach (LaboratoryInventoryItemDrag drag in
                     storageScreenRoot.GetComponentsInChildren<
                         LaboratoryInventoryItemDrag>(true))
            {
                if (drag.Item == item && !drag.IsStationStorageSource)
                {
                    source = drag;
                    break;
                }
            }

            Transform destinationRoot = terminal.transform.Find(
                "StorageScreen/background_Screen_Storage_Slot/Slot_1");
            Assert.That(destinationRoot, Is.Not.Null, "Storage Slot_1 not found.");
            InventorySlotView destinationView =
                GetSpawnedInventorySlot(destinationRoot);
            Assert.That(
                destinationView,
                Is.Not.Null,
                "Storage Slot_1 did not spawn P_InventorySlot.");
            LaboratoryItemDropSlot destination =
                destinationView.GetComponent<LaboratoryItemDropSlot>();
            Assert.That(source, Is.Not.Null, "Occupied inventory slot has no drag source.");
            Assert.That(destination, Is.Not.Null, "Storage slot has no drop target.");
            Transform destinationIconRoot = destination.transform.Find("Icon");
            Assert.That(destinationIconRoot, Is.Not.Null, "Storage icon not created.");
            Image destinationIcon = destinationIconRoot.GetComponent<Image>();
            Assert.That(destinationIcon, Is.Not.Null);
            Assert.That(source.SourceGroup, Is.EqualTo(InventorySlotGroup.Backpack));
            Assert.That(source.SourceIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(source.IsStationStorageSource, Is.False);
            Assert.That(source.IsLaboratorySource, Is.False);
            Assert.That(source.IsChargingSource, Is.False);
            Assert.That(storage.BackpackSlots.Count, Is.EqualTo(16));
            Assert.That(
                PlayerInventory.GetSlotGroup(item.ItemType),
                Is.EqualTo(InventorySlotGroup.Backpack));
            Assert.That(
                inventory.GetItemInstance(source.SourceGroup, source.SourceIndex)?.ItemData,
                Is.SameAs(item));
            Assert.That(
                destination.ItemDropped,
                Is.Not.Null,
                "Storage slot was created without terminal storage callback.");

            RectTransform destinationRect = (RectTransform)destination.transform;
            Canvas canvas = destination.GetComponentInParent<Canvas>();
            Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(
                eventCamera,
                destinationRect.TransformPoint(destinationRect.rect.center));
            PointerEventData pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = screenPoint,
                pointerDrag = source.gameObject
            };

            source.OnPointerDown(pointer);
            Component storageInfoName = FindDescendant(
                    storageScreenRoot,
                    "Text_Name")
                .GetComponent("TextMeshProUGUI");
            Assert.That(
                storageInfoName.GetType().GetProperty("text")
                    ?.GetValue(storageInfoName)?.ToString(),
                Is.EqualTo(item.DisplayName),
                "Starting a storage drag did not update item info.");
            source.OnBeginDrag(pointer);
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            RaycastResult dropHit = hits.Find(hit =>
                hit.gameObject.GetComponentInParent<LaboratoryItemDropSlot>() != null);
            string raycastStack = string.Join(
                "\n",
                hits.ConvertAll(hit => GetHierarchyPath(hit.gameObject.transform)));
            GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
            string raycastContext =
                $"point={screenPoint}, screen={Screen.width}x{Screen.height}, " +
                $"destinationActive={destination.gameObject.activeInHierarchy}, " +
                $"canvasActive={canvas.gameObject.activeInHierarchy}, " +
                $"canvasEnabled={canvas.enabled}, " +
                $"raycasterEnabled={raycaster != null && raycaster.enabled}";
            Assert.That(
                dropHit.gameObject,
                Is.Not.Null,
                "No storage drop target is reachable by the UI raycaster. " +
                raycastContext + " Hits:\n" + raycastStack);
            Assert.That(
                dropHit.gameObject.GetComponentInParent<LaboratoryItemDropSlot>(),
                Is.SameAs(destination));

            bool dropEventReached = false;
            destination.ItemDropped += _ => dropEventReached = true;
            ExecuteEvents.ExecuteHierarchy(
                dropHit.gameObject,
                pointer,
                ExecuteEvents.dropHandler);
            source.OnEndDrag(pointer);

            Assert.That(dropEventReached, Is.True, "Storage drop callback was not invoked.");
            Assert.That(storage.Count, Is.EqualTo(1));
            Assert.That(destinationIcon.enabled, Is.True);
            Assert.That(destinationIcon.sprite, Is.SameAs(item.Icon));
            Assert.That(
                inventory.GetItemInstance(source.SourceGroup, source.SourceIndex),
                Is.Null);
        }

        [UnityTest]
        public IEnumerator ReturningToStationDoesNotMovePlayerItemsIntoStorage()
        {
            SceneManager.LoadScene("MainScene");
            yield return WaitForScene("Player_Station");
            yield return null;
            yield return DisablePersistenceForTest();

            PlayerInventory inventory = Object.FindFirstObjectByType<PlayerInventory>();
            StationStorageController storage = StationStorageController.Instance;
            BootInitializer runtime = BootInitializer.Instance;
            ItemCatalogData catalog = Resources.Load<ItemCatalogData>("ItemCatalog_Default");
            ItemData servoDrive = catalog != null ? catalog.Find("item_servo_drive_01") : null;
            Assert.That(inventory, Is.Not.Null);
            Assert.That(storage, Is.Not.Null);
            Assert.That(runtime, Is.Not.Null);
            Assert.That(servoDrive, Is.Not.Null);

            int storedBefore = storage.Count;
            Assert.That(inventory.AddItem(servoDrive), Is.True);

            Assert.That(
                runtime.LoadGameplayScene("Expedition_01", string.Empty),
                Is.True);
            yield return WaitForScene("Expedition_01");
            Assert.That(
                runtime.LoadGameplayScene("Player_Station", "Station_Start"),
                Is.True);
            yield return WaitForScene("Player_Station");
            yield return null;

            Assert.That(inventory.Contains("item_servo_drive_01"), Is.True);
            Assert.That(storage.Count, Is.EqualTo(storedBefore));
        }

        [UnityTest]
        public IEnumerator InteractionTargetUsesProximityFromAnySide()
        {
            GameObject player = new GameObject("ProximityPlayer");
            PlayerInteractionController interaction =
                player.AddComponent<PlayerInteractionController>();
            GameObject target = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            target.name = "ProximityTarget";
            target.layer = 6;
            ProximityTestInteractable interactable =
                target.AddComponent<ProximityTestInteractable>();

            Vector3[] approachDirections =
            {
                Vector3.forward,
                Vector3.back,
                Vector3.left,
                Vector3.right,
            };

            foreach (Vector3 direction in approachDirections)
            {
                player.transform.SetPositionAndRotation(
                    target.transform.position + direction * 1.25f,
                    Quaternion.LookRotation(direction));
                Physics.SyncTransforms();
                yield return null;

                Assert.That(
                    interaction.CurrentInteractable,
                    Is.SameAs(interactable),
                    $"Interaction was not detected from {direction}.");
            }

            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "InteractionObstruction";
            wall.transform.SetPositionAndRotation(
                target.transform.position + Vector3.forward * 0.65f,
                Quaternion.identity);
            wall.transform.localScale = new Vector3(2f, 2f, 0.1f);
            player.transform.position = target.transform.position +
                                        Vector3.forward * 1.25f;
            Physics.SyncTransforms();
            yield return null;

            Assert.That(
                interaction.CurrentInteractable,
                Is.Null,
                "A wall must block interaction without reintroducing aim/facing.");
            Object.Destroy(wall);
            yield return null;

            interactable.IsAvailable = false;
            yield return null;

            Assert.That(
                interaction.CurrentInteractable,
                Is.SameAs(interactable),
                "Unavailable targets must remain visible so HUD can show " +
                "the reason, while input remains blocked.");

            player.transform.position = target.transform.position +
                                        Vector3.forward * 4f;
            Physics.SyncTransforms();
            yield return null;

            Assert.That(interaction.CurrentInteractable, Is.Null);
            Object.Destroy(player);
            Object.Destroy(target);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayerDeathSwitchesMotorToSeparateRagdoll()
        {
            SceneManager.LoadScene("MainScene");
            yield return WaitForScene("Player_Station");
            yield return null;
            yield return DisablePersistenceForTest();

            PlayerHealth health =
                Object.FindFirstObjectByType<PlayerHealth>();
            ParkourPlayerBridge bridge =
                Object.FindFirstObjectByType<ParkourPlayerBridge>();
            AnimationCharacterController parkourAnimation =
                Object.FindFirstObjectByType<AnimationCharacterController>();
            Assert.That(health, Is.Not.Null);
            Assert.That(bridge, Is.Not.Null);
            Assert.That(parkourAnimation, Is.Not.Null);
            Assert.That(parkourAnimation.switchCameras, Is.Not.Null);
            Assert.That(
                parkourAnimation.switchCameras.transform.IsChildOf(
                    bridge.transform.parent),
                Is.True,
                "Parkour camera switcher must belong to the Player rig.");

            ThirdPersonController parkour =
                bridge.GetComponent<ThirdPersonController>();
            parkour.characterAnimation.switchCameras.SlideCam();
            parkour.SetSlidingCollider(true);
            parkour.characterMovement.enableFeetIK = false;
            parkour.characterAnimation.animator.SetFloat("AnimSpeed", 2f);
            parkour.characterAnimation.animator.SetBool(
                "PredictedJump",
                true);
            parkour.characterAnimation.animator.SetBool("Crouch", true);
            parkour.characterAnimation.animator.Play(
                "Running Slide",
                0,
                0f);
            parkour.characterAnimation.animator.Update(0f);
            parkour.cameraController.newOffset(true);
            yield return null;

            parkour.isVaulting = true;
            bridge.LocomotionBody.isKinematic = true;
            bridge.Teleport(
                bridge.transform.position + Vector3.right * 0.25f,
                bridge.transform.rotation);
            Assert.That(
                bridge.LocomotionBody.isKinematic,
                Is.False,
                "A scene teleport during vault must restore the live motor.");
            Assert.That(parkour.isVaulting, Is.False);
            Assert.That(bridge.LocomotionBody.useGravity, Is.True);
            Assert.That(bridge.LocomotionBody.detectCollisions, Is.True);
            Assert.That(parkour.characterMovement.enableFeetIK, Is.True);
            Assert.That(
                bridge.GetComponents<CapsuleCollider>()
                    .Single(collider => collider.enabled),
                Is.SameAs(parkour.normalCapsuleCollider));
            Assert.That(
                parkour.characterAnimation.switchCameras.IsFreeLookActive,
                Is.True);
            Assert.That(parkour.cameraController.IsAtDefaultOffset, Is.True);
            Assert.That(
                parkour.characterAnimation.animator.GetFloat("AnimSpeed"),
                Is.EqualTo(1f).Within(0.001f));
            Assert.That(
                parkour.characterAnimation.animator.GetBool("PredictedJump"),
                Is.False);
            Assert.That(
                parkour.characterAnimation.animator.GetBool("Crouch"),
                Is.False);
            Assert.That(
                parkour.characterAnimation.animator.GetBool("Released"),
                Is.True);
            Assert.That(
                parkour.characterAnimation.animator.applyRootMotion,
                Is.False);
            Assert.That(
                parkour.characterAnimation.animator
                    .GetCurrentAnimatorStateInfo(0)
                    .IsName("Idle"),
                Is.True,
                "Teleport must cancel root motion from an interrupted action.");
            Assert.That(health.RagdollBodies.Count, Is.GreaterThanOrEqualTo(12));
            Assert.That(
                health.RagdollBodies.All(body => body.isKinematic),
                Is.True);
            Assert.That(
                health.RagdollBodies.All(body => !body.detectCollisions),
                Is.True);

            bridge.LocomotionBody.linearVelocity =
                new Vector3(3f, 0f, 0f);
            Transform hips = health.GetComponent<Animator>()
                .GetBoneTransform(HumanBodyBones.Hips);

            LogAssert.Expect(
                LogType.Warning,
                "Player died and ragdoll was enabled.");
            health.Kill();
            yield return new WaitForFixedUpdate();

            Assert.That(bridge.IsDead, Is.True);
            Assert.That(bridge.LocomotionBody.isKinematic, Is.True);
            Assert.That(bridge.LocomotionBody.detectCollisions, Is.False);
            Assert.That(
                bridge.GetComponents<CapsuleCollider>()
                    .All(collider => !collider.enabled),
                Is.True);
            Assert.That(
                health.GetComponent<Animator>().enabled,
                Is.False);
            Assert.That(
                health.RagdollBodies.All(body => !body.isKinematic),
                Is.True);
            Assert.That(
                health.RagdollBodies.All(body => body.detectCollisions),
                Is.True);
            Assert.That(
                health.RagdollBodies.All(body => body.useGravity),
                Is.True);
            Assert.That(
                health.RagdollBodies.Average(body => body.linearVelocity.x),
                Is.GreaterThan(1f),
                "Ragdoll must inherit the moving player's momentum.");

            var cameraFollowTargets = bridge.transform.parent
                .GetComponentsInChildren<MonoBehaviour>(true)
                .Select(behaviour => new
                {
                    Behaviour = behaviour,
                    Follow = behaviour.GetType().GetProperty(
                        "Follow",
                        BindingFlags.Instance | BindingFlags.Public),
                })
                .Where(entry => entry.Follow != null &&
                                entry.Follow.PropertyType == typeof(Transform))
                .Select(entry =>
                    entry.Follow.GetValue(entry.Behaviour) as Transform)
                .Where(targetTransform => targetTransform != null)
                .ToArray();
            Assert.That(cameraFollowTargets.Length, Is.GreaterThanOrEqualTo(2));
            Assert.That(
                cameraFollowTargets.All(targetTransform =>
                    targetTransform == hips),
                Is.True,
                "Gameplay cameras must follow the moving ragdoll hips.");
        }

        private static int CountDirectSlotButtons(Transform root)
        {
            Assert.That(root, Is.Not.Null);
            int count = 0;
            for (int index = 0; index < root.childCount; index++)
            {
                Transform spawnPoint = root.GetChild(index);
                if (spawnPoint.name.StartsWith("Slot_", StringComparison.Ordinal) &&
                    GetSpawnedInventorySlot(spawnPoint)?.Button != null)
                    count++;
            }
            return count;
        }

        private static InventorySlotView GetSpawnedInventorySlot(
            Transform spawnPoint)
        {
            if (spawnPoint == null)
                return null;

            for (int index = 0; index < spawnPoint.childCount; index++)
            {
                InventorySlotView view =
                    spawnPoint.GetChild(index).GetComponent<InventorySlotView>();
                if (view != null)
                    return view;
            }

            return null;
        }

        private static void AssertLaboratoryInventoryGroup(
            Transform sharedInventory,
            string groupName,
            int expectedCount)
        {
            Transform root = FindDescendant(sharedInventory, groupName);
            Assert.That(root, Is.Not.Null);
            Assert.That(root.childCount, Is.EqualTo(expectedCount));
            for (int index = 0; index < expectedCount; index++)
            {
                Transform spawnPoint = root.GetChild(index);
                Assert.That(
                    GetSpawnedInventorySlot(spawnPoint),
                    Is.Not.Null,
                    $"{groupName}/{spawnPoint.name} has no P_InventorySlot.");
            }
        }

        private static LaboratoryInventoryItemDrag FindPlayerInventoryDrag(
            Transform root,
            ItemData item)
        {
            foreach (LaboratoryInventoryItemDrag drag in
                     root.GetComponentsInChildren<
                         LaboratoryInventoryItemDrag>(true))
            {
                if (drag.Item == item &&
                    drag.SourceIndex >= 0 &&
                    !drag.IsLaboratorySource &&
                    !drag.IsChargingSource &&
                    !drag.IsUpgradeSource &&
                    !drag.IsStationStorageSource)
                {
                    return drag;
                }
            }

            return null;
        }

        private static void AssertLaboratoryTableVisual(
            GameObject visual,
            Transform expectedSlot)
        {
            Assert.That(visual, Is.Not.Null);
            Assert.That(expectedSlot, Is.Not.Null);
            Assert.That(visual.transform.parent, Is.SameAs(expectedSlot));
            Assert.That(visual.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(visual.transform.localRotation, Is.EqualTo(Quaternion.identity));

            foreach (MonoBehaviour behaviour in
                     visual.GetComponentsInChildren<MonoBehaviour>(true))
            {
                Assert.That(
                    behaviour.enabled,
                    Is.False,
                    $"{behaviour.GetType().Name} remained active on a table visual.");
            }

            foreach (Collider collider in
                     visual.GetComponentsInChildren<Collider>(true))
            {
                Assert.That(
                    collider.enabled,
                    Is.False,
                    $"{collider.name} remained interactive on a table visual.");
            }

            foreach (Rigidbody body in
                     visual.GetComponentsInChildren<Rigidbody>(true))
            {
                Assert.That(body.isKinematic, Is.True);
                Assert.That(body.detectCollisions, Is.False);
            }
        }

        private static void DropThroughUi(
            LaboratoryInventoryItemDrag source,
            LaboratoryItemDropSlot destination)
        {
            Assert.That(source, Is.Not.Null);
            Assert.That(destination, Is.Not.Null);
            Assert.That(EventSystem.current, Is.Not.Null);

            RectTransform destinationRect =
                (RectTransform)destination.transform;
            Canvas canvas = destination.GetComponentInParent<Canvas>();
            Camera eventCamera = canvas.renderMode ==
                RenderMode.ScreenSpaceOverlay
                    ? null
                    : canvas.worldCamera;
            Vector2 screenPoint =
                RectTransformUtility.WorldToScreenPoint(
                    eventCamera,
                    destinationRect.TransformPoint(
                        destinationRect.rect.center));
            PointerEventData pointer =
                new PointerEventData(EventSystem.current)
                {
                    button = PointerEventData.InputButton.Left,
                    position = screenPoint,
                    pointerDrag = source.gameObject
                };

            source.OnBeginDrag(pointer);
            var hits =
                new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            RaycastResult dropHit = hits.Find(hit =>
                hit.gameObject.GetComponentInParent<
                    LaboratoryItemDropSlot>() == destination);
            string raycastStack = string.Join(
                "\n",
                hits.ConvertAll(hit =>
                    GetHierarchyPath(hit.gameObject.transform)));
            Graphic destinationGraphic =
                destination.GetComponent<Graphic>();
            CanvasGroup destinationGroup =
                destination.GetComponent<CanvasGroup>();
            string destinationState =
                $"path={GetHierarchyPath(destination.transform)}, " +
                $"active={destination.gameObject.activeInHierarchy}, " +
                $"rect={destinationRect.rect}, " +
                $"world={destinationRect.position}, " +
                $"screen={screenPoint}, " +
                $"graphicEnabled={destinationGraphic != null && destinationGraphic.enabled}, " +
                $"raycastTarget={destinationGraphic != null && destinationGraphic.raycastTarget}, " +
                $"depth={(destinationGraphic != null ? destinationGraphic.depth : -999)}, " +
                $"groupBlocks={destinationGroup != null && destinationGroup.blocksRaycasts}, " +
                $"groupInteractable={destinationGroup != null && destinationGroup.interactable}";
            Assert.That(
                dropHit.gameObject,
                Is.Not.Null,
                "Laboratory slot is blocked from UI raycasts. " +
                destinationState + "\nHits:\n" + raycastStack);

            ExecuteEvents.ExecuteHierarchy(
                dropHit.gameObject,
                pointer,
                ExecuteEvents.dropHandler);
            source.OnEndDrag(pointer);
        }

        private static void ClickThroughUi(Button button)
        {
            Assert.That(button, Is.Not.Null);
            Assert.That(EventSystem.current, Is.Not.Null);

            RectTransform rect = (RectTransform)button.transform;
            Canvas canvas = button.GetComponentInParent<Canvas>();
            Camera eventCamera = canvas.renderMode ==
                RenderMode.ScreenSpaceOverlay
                    ? null
                    : canvas.worldCamera;
            Vector2 screenPoint =
                RectTransformUtility.WorldToScreenPoint(
                    eventCamera,
                    rect.TransformPoint(rect.rect.center));
            PointerEventData pointer =
                new PointerEventData(EventSystem.current)
                {
                    button = PointerEventData.InputButton.Left,
                    position = screenPoint
                };

            var hits =
                new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            RaycastResult clickHit = hits.Find(hit =>
                hit.gameObject.GetComponentInParent<Button>() == button);
            string raycastStack = string.Join(
                "\n",
                hits.ConvertAll(hit =>
                    GetHierarchyPath(hit.gameObject.transform)));
            Assert.That(
                clickHit.gameObject,
                Is.Not.Null,
                "Laboratory inventory slot cannot receive clicks. Hits:\n" +
                raycastStack);
            ExecuteEvents.ExecuteHierarchy(
                clickHit.gameObject,
                pointer,
                ExecuteEvents.pointerClickHandler);
        }

        private static Transform FindDescendant(
            Transform root,
            string objectName)
        {
            if (root == null)
                return null;
            if (root.name == objectName)
                return root;

            for (int index = 0; index < root.childCount; index++)
            {
                Transform found = FindDescendant(
                    root.GetChild(index),
                    objectName);
                if (found != null)
                    return found;
            }

            return null;
        }

        private static string GetHierarchyPath(Transform target)
        {
            string path = target.name;
            while (target.parent != null)
            {
                target = target.parent;
                path = target.name + "/" + path;
            }

            return path;
        }

        private static void AssertActiveDroneAnimation(
            IEnumerable<DroneAnimationView> views,
            string mainStateName,
            string miniStateName)
        {
            int checkedViews = 0;
            foreach (DroneAnimationView view in views)
            {
                if (view == null || !view.gameObject.activeInHierarchy)
                    continue;

                Animator animator = view.GetComponent<Animator>();
                string expectedState =
                    animator.runtimeAnimatorController.name ==
                    DroneAnimationView.MiniControllerName
                        ? miniStateName
                        : mainStateName;
                int expectedHash = Animator.StringToHash(
                    "Base Layer." + expectedState);
                Assert.That(
                    animator.GetCurrentAnimatorStateInfo(0).fullPathHash,
                    Is.EqualTo(expectedHash),
                    GetHierarchyPath(view.transform));
                Assert.That(
                    animator.speed,
                    Is.EqualTo(1f),
                    GetHierarchyPath(view.transform));
                checkedViews++;
            }

            Assert.That(
                checkedViews,
                Is.GreaterThan(0),
                "At least one loaded drone view must be active.");
        }

        private static void AssertDroneViewsAtHome(
            IEnumerable<DroneAnimationView> views)
        {
            int checkedViews = 0;
            foreach (DroneAnimationView view in views)
            {
                if (view == null || !view.gameObject.activeInHierarchy)
                    continue;

                Animator animator = view.GetComponent<Animator>();
                string returnStateName =
                    animator.runtimeAnimatorController.name ==
                    DroneAnimationView.MiniControllerName
                        ? DroneAnimationView.MiniReturnStateName
                        : DroneAnimationView.MainReturnStateName;
                int expectedHash = Animator.StringToHash(
                    "Base Layer." + returnStateName);
                AnimatorStateInfo state =
                    animator.GetCurrentAnimatorStateInfo(0);
                Assert.That(
                    state.fullPathHash,
                    Is.EqualTo(expectedHash),
                    GetHierarchyPath(view.transform));
                Assert.That(
                    state.normalizedTime,
                    Is.GreaterThanOrEqualTo(1f),
                    GetHierarchyPath(view.transform));
                Assert.That(
                    animator.speed,
                    Is.EqualTo(0f),
                    GetHierarchyPath(view.transform));
                checkedViews++;
            }

            Assert.That(
                checkedViews,
                Is.GreaterThan(0),
                "At least one loaded drone view must be active.");
        }

        private static void AssertDroneViewsSynchronized(
            DroneAnimationView mainView,
            DroneAnimationView miniView,
            string mainStateName,
            string miniStateName)
        {
            Animator mainAnimator = mainView.GetComponent<Animator>();
            Animator miniAnimator = miniView.GetComponent<Animator>();
            AnimatorStateInfo mainState =
                mainAnimator.GetCurrentAnimatorStateInfo(0);
            AnimatorStateInfo miniState =
                miniAnimator.GetCurrentAnimatorStateInfo(0);

            Assert.That(
                mainState.fullPathHash,
                Is.EqualTo(Animator.StringToHash(
                    "Base Layer." + mainStateName)));
            Assert.That(
                miniState.fullPathHash,
                Is.EqualTo(Animator.StringToHash(
                    "Base Layer." + miniStateName)));
            Assert.That(
                miniState.normalizedTime,
                Is.EqualTo(mainState.normalizedTime).Within(0.05f));
        }

        private static float GetDroneAnimationEventTime(
            IEnumerable<DroneAnimationView> views,
            string clipName,
            string functionName)
        {
            Animator mainAnimator = views
                .Where(view => view != null)
                .Select(view => view.GetComponent<Animator>())
                .Single(animator =>
                    animator.runtimeAnimatorController.name ==
                    DroneAnimationView.MainControllerName);
            AnimationClip clip = mainAnimator.runtimeAnimatorController
                .animationClips
                .Single(candidate => candidate.name == clipName);
            return clip.events.Single(animationEvent =>
                animationEvent.functionName == functionName).time;
        }

        private static IEnumerator WaitForScene(string sceneName)
        {
            while (SceneManager.GetActiveScene().name != sceneName ||
                   (BootInitializer.Instance != null &&
                    BootInitializer.Instance.IsLoading))
            {
                yield return null;
            }
        }

        private static IEnumerator DisablePersistenceForTest()
        {
            SaveGameController save =
                Object.FindFirstObjectByType<SaveGameController>();
            if (save != null)
                Object.Destroy(save);

            yield return null;
        }

        private static IEnumerator ResetSceneState()
        {
            BootInitializer[] bootRoots =
                Object.FindObjectsByType<BootInitializer>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            foreach (BootInitializer boot in bootRoots)
            {
                if (boot != null)
                    Object.Destroy(boot.gameObject);
            }

            LoadingScreenController[] loadingScreens =
                Object.FindObjectsByType<LoadingScreenController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            foreach (LoadingScreenController loadingScreen in loadingScreens)
            {
                if (loadingScreen != null)
                    Object.Destroy(loadingScreen.gameObject);
            }

            yield return null;
            GameSessionLaunchState.Clear();
            SaveSlotStorage.DeleteAllSlots();
            SceneManager.LoadScene("Boot", LoadSceneMode.Single);
            yield return null;
        }

        private static ItemData CreateEngineeringPart(
            string itemId,
            StationSystemType systemType,
            string objectId,
            string slotId,
            StationObjectStat stat,
            float value)
        {
            var modifier = new StationObjectStatModifierDefinition();
            SetPrivateField(modifier, "stat", stat);
            SetPrivateField(modifier, "mode", StationStatModifierMode.Add);
            SetPrivateField(modifier, "value", value);

            var compatibility = new EngineeringPartCompatibility();
            SetPrivateField(compatibility, "systemType", systemType);
            SetPrivateField(compatibility, "objectId", objectId);
            SetPrivateField(compatibility, "slotId", slotId);
            SetPrivateField(
                compatibility,
                "modifiers",
                new List<StationObjectStatModifierDefinition> { modifier });

            var definition = new EngineeringPartDefinition();
            SetPrivateField(
                definition,
                "compatibleInstallations",
                new List<EngineeringPartCompatibility> { compatibility });

            ItemData item = ScriptableObject.CreateInstance<ItemData>();
            item.name = $"Test_{itemId}";
            SetPrivateField(item, "itemId", itemId);
            SetPrivateField(item, "displayName", itemId);
            SetPrivateField(item, "itemType", ItemType.EngineeringPart);
            SetPrivateField(item, "engineeringPartDefinition", definition);
            return item;
        }

        private static ItemCatalogData CreateCatalog(params ItemData[] items)
        {
            ItemCatalogData catalog =
                ScriptableObject.CreateInstance<ItemCatalogData>();
            catalog.name = "Test_ItemCatalog";
            SetPrivateField(catalog, "items", new List<ItemData>(items));
            return catalog;
        }

        private static void SetPrivateField(
            object target,
            string fieldName,
            object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(
                field,
                Is.Not.Null,
                $"Missing field {target.GetType().Name}.{fieldName}");
            field.SetValue(target, value);
        }

        private static void AssertLightingPreset(
            SwitchBakedLights lighting,
            SwitchBakedLights.StationLightingMode expectedMode,
            string presetFieldName)
        {
            FieldInfo presetField = typeof(SwitchBakedLights).GetField(
                presetFieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(presetField, Is.Not.Null);
            object preset = presetField.GetValue(lighting);
            Assert.That(preset, Is.Not.Null);

            FieldInfo colorsField = preset.GetType().GetField(
                "lightmapColors",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(colorsField, Is.Not.Null);
            Texture2D[] expectedColors =
                (Texture2D[])colorsField.GetValue(preset);

            Assert.That(lighting.CurrentMode, Is.EqualTo(expectedMode));
            Assert.That(
                LightmapSettings.lightmaps,
                Has.Length.GreaterThanOrEqualTo(expectedColors.Length));

            Scene stationScene = SceneManager.GetSceneByName("Player_Station");
            Assert.That(stationScene.IsValid() && stationScene.isLoaded, Is.True);

            Renderer[] stationRenderers = stationScene.GetRootGameObjects()
                .SelectMany(root =>
                    root.GetComponentsInChildren<Renderer>(true))
                .Where(renderer =>
                    renderer.name == "TestStation" ||
                    renderer.name == "Station_Terminal")
                .ToArray();
            Assert.That(stationRenderers, Has.Length.EqualTo(2));

            int firstStationLightmapIndex = stationRenderers.Min(
                renderer => renderer.lightmapIndex);

            foreach (Renderer stationRenderer in stationRenderers)
            {
                int localLightmapIndex =
                    stationRenderer.lightmapIndex - firstStationLightmapIndex;
                Assert.That(
                    stationRenderer.lightmapIndex,
                    Is.GreaterThanOrEqualTo(0).And.LessThan(
                        LightmapSettings.lightmaps.Length),
                    $"{stationRenderer.name} is not bound to a station lightmap");
                Assert.That(
                    localLightmapIndex,
                    Is.GreaterThanOrEqualTo(0).And.LessThan(expectedColors.Length),
                    $"{stationRenderer.name} uses an unexpected station lightmap index");
                Assert.That(
                    LightmapSettings.lightmaps[stationRenderer.lightmapIndex]
                        .lightmapColor,
                    Is.SameAs(expectedColors[localLightmapIndex]),
                    $"{stationRenderer.name} uses a lightmap from another preset");
            }
        }

    }

    public sealed class ProximityTestInteractable : MonoBehaviour, IInteractable
    {
        public bool IsAvailable { get; set; } = true;

        public Transform InteractionTransform => transform;

        public InteractionPrompt GetPrompt()
        {
            return new InteractionPrompt(
                "Test",
                InteractionMode.Press,
                0f,
                IsAvailable,
                IsAvailable ? string.Empty : "Unavailable for test");
        }

        public void BeginInteraction(GameObject interactor)
        {
        }

        public void CancelInteraction(GameObject interactor)
        {
        }

        public void CompleteInteraction(GameObject interactor)
        {
        }
    }
}
