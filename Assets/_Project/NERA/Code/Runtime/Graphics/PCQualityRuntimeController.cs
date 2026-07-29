using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace NERA.Graphics
{
    /// <summary>
    /// Applies visual-only parts of the PC presets that are not represented by
    /// QualitySettings or a URP asset: authored camera post-processing and
    /// particle density. It never changes simulation, AI, timers, or physics.
    /// </summary>
    public static class PCQualityRuntimeController
    {
        private sealed class ParticleBaseline
        {
            public ParticleSystem System;
            public float RateOverTime;
            public float RateOverDistance;
            public int MaxParticles;
        }

        private static readonly Dictionary<Camera, bool> CameraPostBaselines =
            new Dictionary<Camera, bool>();
        private static readonly Dictionary<ParticleSystem, ParticleBaseline>
            ParticleBaselines =
                new Dictionary<ParticleSystem, ParticleBaseline>();

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            CameraPostBaselines.Clear();
            ParticleBaselines.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        public static bool SetQualityLevel(
            string presetName,
            bool applyExpensiveChanges = true)
        {
            int index = Array.FindIndex(
                QualitySettings.names,
                name => string.Equals(
                    name,
                    presetName,
                    StringComparison.OrdinalIgnoreCase));
            if (index < 0)
                return false;

            QualitySettings.SetQualityLevel(index, applyExpensiveChanges);
            ApplyCurrentQuality();
            return true;
        }

        public static void ApplyCurrentQuality()
        {
            ApplyPresetToLoadedScenes();
        }

        private static void HandleSceneLoaded(Scene _, LoadSceneMode __)
        {
            ApplyPresetToLoadedScenes();
        }

        private static void ApplyPresetToLoadedScenes()
        {
            string preset = QualitySettings.names[QualitySettings.GetQualityLevel()];
            if (!TryGetVisualSettings(
                    preset,
                    out bool postProcessingEnabled,
                    out float particleDensity))
            {
                return;
            }

            ApplyPostProcessing(postProcessingEnabled);
            ApplyParticleDensity(particleDensity);
        }

        private static bool TryGetVisualSettings(
            string preset,
            out bool postProcessingEnabled,
            out float particleDensity)
        {
            switch (preset)
            {
                case "Low":
                    postProcessingEnabled = false;
                    particleDensity = 0.5f;
                    return true;
                case "Medium":
                    postProcessingEnabled = true;
                    particleDensity = 0.75f;
                    return true;
                case "High":
                    postProcessingEnabled = true;
                    particleDensity = 1f;
                    return true;
                default:
                    postProcessingEnabled = true;
                    particleDensity = 1f;
                    return false;
            }
        }

        private static void ApplyPostProcessing(bool presetAllowsPostProcessing)
        {
            RemoveDestroyedCameraBaselines();
            Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (Camera camera in cameras)
            {
                UniversalAdditionalCameraData data =
                    camera.GetComponent<UniversalAdditionalCameraData>();
                if (data == null)
                    continue;

                if (!CameraPostBaselines.TryGetValue(camera, out bool authored))
                {
                    authored = data.renderPostProcessing;
                    CameraPostBaselines.Add(camera, authored);
                }

                data.renderPostProcessing =
                    presetAllowsPostProcessing && authored;
            }
        }

        private static void ApplyParticleDensity(float density)
        {
            RemoveDestroyedParticleBaselines();
            ParticleSystem[] systems =
                UnityEngine.Object.FindObjectsByType<ParticleSystem>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (ParticleSystem system in systems)
            {
                if (!ParticleBaselines.TryGetValue(
                        system,
                        out ParticleBaseline baseline))
                {
                    ParticleSystem.EmissionModule authoredEmission =
                        system.emission;
                    ParticleSystem.MainModule authoredMain = system.main;
                    baseline = new ParticleBaseline
                    {
                        System = system,
                        RateOverTime =
                            authoredEmission.rateOverTimeMultiplier,
                        RateOverDistance =
                            authoredEmission.rateOverDistanceMultiplier,
                        MaxParticles = authoredMain.maxParticles
                    };
                    ParticleBaselines.Add(system, baseline);
                }

                ParticleSystem.EmissionModule emission = system.emission;
                emission.rateOverTimeMultiplier =
                    baseline.RateOverTime * density;
                emission.rateOverDistanceMultiplier =
                    baseline.RateOverDistance * density;

                ParticleSystem.MainModule main = system.main;
                main.maxParticles = Mathf.Max(
                    1,
                    Mathf.RoundToInt(baseline.MaxParticles * density));
            }
        }

        private static void RemoveDestroyedCameraBaselines()
        {
            List<Camera> destroyed = null;
            foreach (Camera camera in CameraPostBaselines.Keys)
            {
                if (camera != null)
                    continue;
                destroyed ??= new List<Camera>();
                destroyed.Add(camera);
            }

            if (destroyed == null)
                return;
            foreach (Camera camera in destroyed)
                CameraPostBaselines.Remove(camera);
        }

        private static void RemoveDestroyedParticleBaselines()
        {
            List<ParticleSystem> destroyed = null;
            foreach (KeyValuePair<ParticleSystem, ParticleBaseline> pair in
                     ParticleBaselines)
            {
                if (pair.Value.System != null)
                    continue;
                destroyed ??= new List<ParticleSystem>();
                destroyed.Add(pair.Key);
            }

            if (destroyed == null)
                return;
            foreach (ParticleSystem system in destroyed)
                ParticleBaselines.Remove(system);
        }

    }
}
