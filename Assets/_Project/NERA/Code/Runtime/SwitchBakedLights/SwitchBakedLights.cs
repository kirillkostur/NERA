using System;
using System.Collections;
using NERA.Energy;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public sealed class SwitchBakedLights : MonoBehaviour
{
    public enum StationLightingMode
    {
        Normal,
        LowEnergyWarning,
        BackupPowerEmergency
    }

    [Serializable]
    private sealed class LightingPreset
    {
        [SerializeField] private Texture2D[] lightmapDirections =
            Array.Empty<Texture2D>();
        [SerializeField] private Texture2D[] lightmapColors =
            Array.Empty<Texture2D>();
        [SerializeField] private Light[] lightSources = Array.Empty<Light>();

        [NonSerialized] private LightmapData[] runtimeLightmaps =
            Array.Empty<LightmapData>();
        [NonSerialized] private bool hasRuntimeLightmaps;
        [NonSerialized] private bool hasDirectionalMaps;

        public bool HasLightmaps =>
            lightmapColors != null && lightmapColors.Length > 0;
        public bool HasLightSources =>
            lightSources != null && lightSources.Length > 0;

        public bool AdoptLegacyData(
            Texture2D[] colors,
            Texture2D[] directions,
            Light[] sources = null)
        {
            bool changed = false;

            if (!HasLightmaps && colors != null && colors.Length > 0)
            {
                lightmapColors = colors;
                lightmapDirections = directions ?? Array.Empty<Texture2D>();
                changed = true;
            }

            if (!HasLightSources && sources != null && sources.Length > 0)
            {
                lightSources = sources;
                changed = true;
            }

            return changed;
        }

        public void Build(SwitchBakedLights owner, string displayName)
        {
            runtimeLightmaps = Array.Empty<LightmapData>();
            hasRuntimeLightmaps = false;

            if (lightmapColors == null || lightmapColors.Length == 0)
                return;

            for (int i = 0; i < lightmapColors.Length; i++)
            {
                if (lightmapColors[i] != null)
                    continue;

                Debug.LogWarning(
                    $"The '{displayName}' lighting preset has an empty " +
                    $"color lightmap at index {i}. Its light sources will " +
                    "still switch, but baked lightmaps will be left unchanged.",
                    owner);
                return;
            }

            hasDirectionalMaps = HasCompleteDirectionalSet();
            runtimeLightmaps = new LightmapData[lightmapColors.Length];
            for (int i = 0; i < lightmapColors.Length; i++)
            {
                runtimeLightmaps[i] = new LightmapData
                {
                    lightmapColor = lightmapColors[i],
                    lightmapDir = hasDirectionalMaps
                        ? lightmapDirections[i]
                        : null
                };
            }

            hasRuntimeLightmaps = true;
        }

        public void ApplyLightmaps(int firstLightmapIndex)
        {
            if (!hasRuntimeLightmaps)
                return;

            LightmapData[] currentLightmaps = LightmapSettings.lightmaps ??
                Array.Empty<LightmapData>();
            int safeFirstIndex = Mathf.Max(0, firstLightmapIndex);
            int requiredLength = safeFirstIndex + runtimeLightmaps.Length;
            int resultLength = Mathf.Max(currentLightmaps.Length, requiredLength);
            LightmapData[] combinedLightmaps = new LightmapData[resultLength];

            Array.Copy(
                currentLightmaps,
                combinedLightmaps,
                currentLightmaps.Length);
            Array.Copy(
                runtimeLightmaps,
                0,
                combinedLightmaps,
                safeFirstIndex,
                runtimeLightmaps.Length);

            LightmapSettings.lightmapsMode = hasDirectionalMaps
                ? LightmapsMode.CombinedDirectional
                : LightmapsMode.NonDirectional;
            LightmapSettings.lightmaps = combinedLightmaps;
        }

        private bool HasCompleteDirectionalSet()
        {
            if (lightmapDirections == null ||
                lightmapDirections.Length != lightmapColors.Length)
            {
                return false;
            }

            for (int i = 0; i < lightmapDirections.Length; i++)
            {
                if (lightmapDirections[i] == null)
                    return false;
            }

            return lightmapDirections.Length > 0;
        }

        public void SetLightSourcesEnabled(bool value)
        {
            if (lightSources == null)
                return;

            foreach (Light lightSource in lightSources)
            {
                if (lightSource != null)
                    lightSource.enabled = value;
            }
        }
    }

    private const float EnergyEpsilon = 0.001f;

    [Header("Lighting Presets")]
    [SerializeField] private LightingPreset normalOperation = new();
    [SerializeField] private LightingPreset lowEnergyWarning = new();
    [SerializeField] private LightingPreset backupPowerEmergency = new();

    [Header("Station Binding")]
    [Tooltip(
        "Automatically follows the station's main battery charge and backup reserve.")]
    [SerializeField] private bool followStationEnergy = true;

    [Header("Debug")]
    [Tooltip(
        "Ctrl+1/2/3 select Normal/Warning/Emergency. Ctrl+0 resumes automatic control.")]
    [SerializeField] private bool enableKeyboardShortcuts = true;

    [FormerlySerializedAs("darkLightmapDir")]
    [SerializeField, HideInInspector] private Texture2D[] legacyNormalDirections;
    [FormerlySerializedAs("darkLightmapColor")]
    [SerializeField, HideInInspector] private Texture2D[] legacyNormalColors;
    [FormerlySerializedAs("brightLightmapDir")]
    [SerializeField, HideInInspector] private Texture2D[] legacyWarningDirections;
    [FormerlySerializedAs("brightLightmapColor")]
    [SerializeField, HideInInspector] private Texture2D[] legacyWarningColors;
    [FormerlySerializedAs("lights")]
    [SerializeField, HideInInspector] private Light[] legacyNormalLights;

    private EnergySystemController subscribedEnergy;
    private StationEnvironmentController subscribedEnvironment;
    private bool hasAppliedMode;
    private bool activePresetLightsEnabled;
    private bool debugOverrideActive;
    private bool missingControllerWarningLogged;

    public StationLightingMode CurrentMode { get; private set; }

    private void Awake()
    {
        EnsurePresetsExist();
        MigrateLegacyData();
        BuildPresets();
    }

    private void OnEnable()
    {
        EnsurePresetsExist();
        MigrateLegacyData();
        BuildPresets();
        hasAppliedMode = false;
        missingControllerWarningLogged = false;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;

        if (followStationEnergy)
        {
            BindEnergy(EnergySystemController.Instance, false);
            BindEnvironment(StationEnvironmentController.Instance, false);
            RefreshFromStationEnergy();
        }
        else
            ApplyLightingMode(StationLightingMode.Normal, true);

        StartCoroutine(ReapplyAfterSceneLoad());
    }

    private IEnumerator Start()
    {
        yield return ReapplyAfterSceneLoad();
    }

    private void Update()
    {
        if (followStationEnergy &&
            subscribedEnergy != EnergySystemController.Instance)
        {
            BindEnergy(EnergySystemController.Instance, false);
        }

        if (followStationEnergy &&
            subscribedEnvironment != StationEnvironmentController.Instance)
        {
            BindEnvironment(StationEnvironmentController.Instance, false);
        }

        if (followStationEnergy && !debugOverrideActive)
            RefreshFromStationEnergy();

        HandleKeyboardShortcuts();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        BindEnergy(null, false);
        BindEnvironment(null, false);
    }

    private void OnValidate()
    {
        EnsurePresetsExist();
        MigrateLegacyData();
    }

    public void SetNormalLighting()
    {
        debugOverrideActive = true;
        ApplyLightingMode(StationLightingMode.Normal, true);
    }

    public void SetWarningLighting()
    {
        debugOverrideActive = true;
        ApplyLightingMode(StationLightingMode.LowEnergyWarning, true);
    }

    public void SetEmergencyLighting()
    {
        debugOverrideActive = true;
        ApplyLightingMode(StationLightingMode.BackupPowerEmergency, true);
    }

    public void ResumeAutomaticStationControl()
    {
        debugOverrideActive = false;
        RefreshFromStationEnergy();
    }

    public void RefreshFromStationEnergy()
    {
        RefreshFromStationEnergy(false);
    }

    private void RefreshFromStationEnergy(bool forceApply)
    {
        if (!followStationEnergy)
        {
            ApplyLightingMode(
                StationLightingMode.Normal,
                true,
                forceApply);
            return;
        }

        EnergySystemController energy = subscribedEnergy;
        if (energy == null)
        {
            ApplyLightingMode(
                StationLightingMode.Normal,
                true,
                forceApply);
            return;
        }

        if (!energy.GridEnabled)
        {
            ApplyLightingMode(
                StationLightingMode.BackupPowerEmergency,
                energy.CurrentBackupReserve > EnergyEpsilon,
                forceApply);
            return;
        }

        if (energy.TotalCapacity <= EnergyEpsilon)
        {
            ApplyLightingMode(
                StationLightingMode.Normal,
                false,
                forceApply);
            return;
        }

        bool stationHasPower = energy.GridEnabled &&
            (energy.CurrentEnergy > EnergyEpsilon ||
             energy.CurrentBackupReserve > EnergyEpsilon);

        if (energy.CurrentEnergy <= EnergyEpsilon)
        {
            ApplyLightingMode(
                StationLightingMode.BackupPowerEmergency,
                stationHasPower,
                forceApply);
            return;
        }

        if (subscribedEnvironment != null &&
            subscribedEnvironment.Weather == StationWeather.Sandstorm)
        {
            ApplyLightingMode(
                StationLightingMode.LowEnergyWarning,
                stationHasPower,
                forceApply);
            return;
        }

        float warningThreshold = energy.Config.DefaultConsumerMinimumCharge01;
        if (energy.Charge01 <= warningThreshold + EnergyEpsilon)
        {
            ApplyLightingMode(
                StationLightingMode.LowEnergyWarning,
                stationHasPower,
                forceApply);
            return;
        }

        ApplyLightingMode(
            StationLightingMode.Normal,
            stationHasPower,
            forceApply);
    }

    private void HandleSceneLoaded(Scene _, LoadSceneMode __)
    {
        if (isActiveAndEnabled)
            StartCoroutine(ReapplyAfterSceneLoad());
    }

    private IEnumerator ReapplyAfterSceneLoad()
    {
        yield return null;

        if (!isActiveAndEnabled)
            yield break;

        WarnAboutMissingStationControllers();
        if (!debugOverrideActive)
            RefreshFromStationEnergy(true);
    }

    private void WarnAboutMissingStationControllers()
    {
        if (missingControllerWarningLogged || !followStationEnergy ||
            (EnergySystemController.Instance != null &&
             StationEnvironmentController.Instance != null))
        {
            return;
        }

        missingControllerWarningLogged = true;
        Debug.LogWarning(
            "SwitchBakedLights: automatic station lighting requires " +
            "EnergySystemController and StationEnvironmentController from " +
            "MainScene. Start the game through Boot/MainScene; playing " +
            "Player_Station directly supports only manual lighting modes.",
            this);
    }

    private void BindEnergy(
        EnergySystemController energy,
        bool refreshLighting = true)
    {
        if (subscribedEnergy == energy)
        {
            if (refreshLighting && !debugOverrideActive)
                RefreshFromStationEnergy();
            return;
        }

        if (subscribedEnergy != null)
            subscribedEnergy.EnergyChanged -= HandleEnergyChanged;

        subscribedEnergy = energy;

        if (subscribedEnergy != null)
            subscribedEnergy.EnergyChanged += HandleEnergyChanged;

        if (refreshLighting && !debugOverrideActive)
            RefreshFromStationEnergy();
    }

    private void HandleEnergyChanged()
    {
        if (!debugOverrideActive)
            RefreshFromStationEnergy();
    }

    private void BindEnvironment(
        StationEnvironmentController environment,
        bool refreshLighting = true)
    {
        if (subscribedEnvironment == environment)
        {
            if (refreshLighting && !debugOverrideActive)
                RefreshFromStationEnergy();
            return;
        }

        if (subscribedEnvironment != null)
        {
            subscribedEnvironment.EnvironmentChanged -=
                HandleEnvironmentChanged;
        }

        subscribedEnvironment = environment;

        if (subscribedEnvironment != null)
        {
            subscribedEnvironment.EnvironmentChanged +=
                HandleEnvironmentChanged;
        }

        if (refreshLighting && !debugOverrideActive)
            RefreshFromStationEnergy();
    }

    private void HandleEnvironmentChanged()
    {
        if (!debugOverrideActive)
            RefreshFromStationEnergy();
    }

    private void HandleKeyboardShortcuts()
    {
        if (!enableKeyboardShortcuts || Keyboard.current == null)
            return;

        bool controlPressed = Keyboard.current.leftCtrlKey.isPressed ||
            Keyboard.current.rightCtrlKey.isPressed;
        if (!controlPressed)
            return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            SetNormalLighting();
        }
        else if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            SetWarningLighting();
        }
        else if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            SetEmergencyLighting();
        }
        else if (Keyboard.current.digit0Key.wasPressedThisFrame)
        {
            ResumeAutomaticStationControl();
        }
    }

    private void ApplyLightingMode(
        StationLightingMode mode,
        bool enablePresetLights,
        bool forceApply = false)
    {
        if (!forceApply && hasAppliedMode && CurrentMode == mode &&
            activePresetLightsEnabled == enablePresetLights)
        {
            return;
        }

        normalOperation.SetLightSourcesEnabled(false);
        lowEnergyWarning.SetLightSourcesEnabled(false);
        backupPowerEmergency.SetLightSourcesEnabled(false);

        LightingPreset activePreset = GetPreset(mode);
        activePreset.ApplyLightmaps(GetSceneLightmapStartIndex());
        activePreset.SetLightSourcesEnabled(enablePresetLights);

        CurrentMode = mode;
        activePresetLightsEnabled = enablePresetLights;
        hasAppliedMode = true;
    }

    private int GetSceneLightmapStartIndex()
    {
        Scene scene = gameObject.scene;
        if (!scene.IsValid() || !scene.isLoaded)
            return 0;

        int firstLightmapIndex = int.MaxValue;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                int index = renderer.lightmapIndex;
                if (index >= 0 && index < 65534)
                    firstLightmapIndex = Mathf.Min(firstLightmapIndex, index);
            }
        }

        return firstLightmapIndex == int.MaxValue
            ? 0
            : firstLightmapIndex;
    }

    private LightingPreset GetPreset(StationLightingMode mode)
    {
        switch (mode)
        {
            case StationLightingMode.LowEnergyWarning:
                return lowEnergyWarning;
            case StationLightingMode.BackupPowerEmergency:
                return backupPowerEmergency;
            default:
                return normalOperation;
        }
    }

    private void BuildPresets()
    {
        normalOperation.Build(this, "Normal Operation");
        lowEnergyWarning.Build(this, "Low Energy Warning");
        backupPowerEmergency.Build(this, "Backup Power Emergency");
    }

    private void EnsurePresetsExist()
    {
        normalOperation ??= new LightingPreset();
        lowEnergyWarning ??= new LightingPreset();
        backupPowerEmergency ??= new LightingPreset();
    }

    private void MigrateLegacyData()
    {
        bool migratedNormal = normalOperation.AdoptLegacyData(
            legacyNormalColors,
            legacyNormalDirections,
            legacyNormalLights);
        bool migratedWarning = lowEnergyWarning.AdoptLegacyData(
            legacyWarningColors,
            legacyWarningDirections);

        if (migratedNormal)
        {
            legacyNormalColors = Array.Empty<Texture2D>();
            legacyNormalDirections = Array.Empty<Texture2D>();
            legacyNormalLights = Array.Empty<Light>();
        }

        if (migratedWarning)
        {
            legacyWarningColors = Array.Empty<Texture2D>();
            legacyWarningDirections = Array.Empty<Texture2D>();
        }
    }

}
