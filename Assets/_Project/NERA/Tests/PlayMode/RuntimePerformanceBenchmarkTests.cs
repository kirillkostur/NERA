using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NERA.Core;
using NERA.Save;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace NERA.Tests
{
    public sealed class RuntimePerformanceBenchmarkTests
    {
        private const int WarmupFrames = 180;
        private const int MeasurementFrames = 600;

        private string previousSaveRoot;
        private string isolatedSaveRoot;
        private int previousVSyncCount;
        private int previousTargetFrameRate;

        [OneTimeSetUp]
        public void ConfigureBenchmarkEnvironment()
        {
            previousSaveRoot = Environment.GetEnvironmentVariable(
                SaveSlotStorage.SaveRootEnvironmentVariable);
            isolatedSaveRoot = Path.Combine(
                Path.GetTempPath(),
                "NERA_PerformanceBenchmark",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(isolatedSaveRoot);
            Environment.SetEnvironmentVariable(
                SaveSlotStorage.SaveRootEnvironmentVariable,
                isolatedSaveRoot);

            previousVSyncCount = QualitySettings.vSyncCount;
            previousTargetFrameRate = Application.targetFrameRate;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
        }

        [OneTimeTearDown]
        public void RestoreBenchmarkEnvironment()
        {
            QualitySettings.vSyncCount = previousVSyncCount;
            Application.targetFrameRate = previousTargetFrameRate;
            Environment.SetEnvironmentVariable(
                SaveSlotStorage.SaveRootEnvironmentVariable,
                previousSaveRoot);

            if (!string.IsNullOrWhiteSpace(isolatedSaveRoot) &&
                Directory.Exists(isolatedSaveRoot))
            {
                Directory.Delete(isolatedSaveRoot, true);
            }
        }

        [UnityTest]
        public IEnumerator CaptureStationAndExpeditionPerformance()
        {
            SaveSlotStorage.DeleteAllSlots();
            GameSessionLaunchState.Request(
                GameLaunchMode.NewGame,
                SaveSlotStorage.DefaultSlot);
            SceneManager.LoadScene("MainScene", LoadSceneMode.Single);
            yield return WaitForScene("Player_Station");

            var report = new BenchmarkReport
            {
                unityVersion = Application.unityVersion,
                platform = Application.platform.ToString(),
                warmupFrames = WarmupFrames,
                measurementFrames = MeasurementFrames,
                scenarios = new List<ScenarioReport>()
            };

            yield return CaptureScenario(
                "Player_Station_Idle",
                scenario => report.scenarios.Add(scenario));

            BootInitializer runtime = BootInitializer.Instance;
            Assert.That(runtime, Is.Not.Null);
            Assert.That(
                runtime.LoadGameplayScene("Expedition_01", string.Empty),
                Is.True);
            yield return WaitForScene("Expedition_01");

            yield return CaptureScenario(
                "Expedition_01_Idle",
                scenario => report.scenarios.Add(scenario));

            string outputDirectory = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                "Library",
                "NERAProfiling"));
            Directory.CreateDirectory(outputDirectory);
            string outputPath = Path.Combine(outputDirectory, "latest.json");
            File.WriteAllText(outputPath, JsonUtility.ToJson(report, true));
            Debug.Log($"NERA performance benchmark written to {outputPath}");
        }

        private static IEnumerator CaptureScenario(
            string scenarioName,
            Action<ScenarioReport> completed)
        {
            for (int frame = 0; frame < WarmupFrames; frame++)
                yield return null;

            using var mainThread = ProfilerRecorder.StartNew(
                ProfilerCategory.Render,
                "CPU Main Thread Frame Time");
            using var totalCpu = ProfilerRecorder.StartNew(
                ProfilerCategory.Render,
                "CPU Total Frame Time");
            using var renderThread = ProfilerRecorder.StartNew(
                ProfilerCategory.Render,
                "CPU Render Thread Frame Time");
            using var gcAllocated = ProfilerRecorder.StartNew(
                ProfilerCategory.Memory,
                "GC Allocated In Frame");
            using var behaviourUpdate = ProfilerRecorder.StartNew(
                ProfilerCategory.Scripts,
                "BehaviourUpdate");
            using var physicsSimulate = ProfilerRecorder.StartNew(
                ProfilerCategory.Physics,
                "Physics.Simulate");
            using var drawCalls = ProfilerRecorder.StartNew(
                ProfilerCategory.Render,
                "Draw Calls Count");
            using var batches = ProfilerRecorder.StartNew(
                ProfilerCategory.Render,
                "Batches Count");
            using var triangles = ProfilerRecorder.StartNew(
                ProfilerCategory.Render,
                "Triangles Count");
            using var vertices = ProfilerRecorder.StartNew(
                ProfilerCategory.Render,
                "Vertices Count");

            var frameDeltaSamples = new List<double>(MeasurementFrames);
            var mainThreadSamples = new List<double>(MeasurementFrames);
            var totalCpuSamples = new List<double>(MeasurementFrames);
            var renderThreadSamples = new List<double>(MeasurementFrames);
            var gcSamples = new List<double>(MeasurementFrames);
            var behaviourSamples = new List<double>(MeasurementFrames);
            var physicsSamples = new List<double>(MeasurementFrames);
            var drawCallSamples = new List<double>(MeasurementFrames);
            var batchSamples = new List<double>(MeasurementFrames);
            var triangleSamples = new List<double>(MeasurementFrames);
            var vertexSamples = new List<double>(MeasurementFrames);

            for (int frame = 0; frame < MeasurementFrames; frame++)
            {
                yield return null;

                frameDeltaSamples.Add(Time.unscaledDeltaTime * 1000.0);
                AddNanoseconds(mainThread, mainThreadSamples);
                AddNanoseconds(totalCpu, totalCpuSamples);
                AddNanoseconds(renderThread, renderThreadSamples);
                AddRaw(gcAllocated, gcSamples);
                AddNanoseconds(behaviourUpdate, behaviourSamples);
                AddNanoseconds(physicsSimulate, physicsSamples);
                AddRaw(drawCalls, drawCallSamples);
                AddRaw(batches, batchSamples);
                AddRaw(triangles, triangleSamples);
                AddRaw(vertices, vertexSamples);
            }

            completed(new ScenarioReport
            {
                name = scenarioName,
                metrics = new List<MetricReport>
                {
                    BuildMetric("Frame Delta", "ms", frameDeltaSamples),
                    BuildMetric("CPU Main Thread", "ms", mainThreadSamples),
                    BuildMetric("CPU Total", "ms", totalCpuSamples),
                    BuildMetric("CPU Render Thread", "ms", renderThreadSamples),
                    BuildMetric("GC Allocated", "bytes/frame", gcSamples),
                    BuildMetric("BehaviourUpdate", "ms", behaviourSamples),
                    BuildMetric("Physics.Simulate", "ms", physicsSamples),
                    BuildMetric("Draw Calls", "count", drawCallSamples),
                    BuildMetric("Batches", "count", batchSamples),
                    BuildMetric("Triangles", "count", triangleSamples),
                    BuildMetric("Vertices", "count", vertexSamples)
                }
            });
        }

        private static void AddNanoseconds(
            ProfilerRecorder recorder,
            ICollection<double> samples)
        {
            samples.Add(recorder.Valid ? recorder.LastValue / 1_000_000.0 : 0.0);
        }

        private static void AddRaw(
            ProfilerRecorder recorder,
            ICollection<double> samples)
        {
            samples.Add(recorder.Valid ? recorder.LastValue : 0.0);
        }

        private static MetricReport BuildMetric(
            string name,
            string unit,
            List<double> samples)
        {
            double[] ordered = samples.OrderBy(value => value).ToArray();
            return new MetricReport
            {
                name = name,
                unit = unit,
                sampleCount = ordered.Length,
                mean = ordered.Average(),
                median = Percentile(ordered, 0.5),
                p95 = Percentile(ordered, 0.95),
                minimum = ordered[0],
                maximum = ordered[ordered.Length - 1]
            };
        }

        private static double Percentile(double[] ordered, double percentile)
        {
            if (ordered.Length == 0)
                return 0.0;

            double position = (ordered.Length - 1) * percentile;
            int lower = Mathf.FloorToInt((float)position);
            int upper = Mathf.CeilToInt((float)position);
            if (lower == upper)
                return ordered[lower];

            double fraction = position - lower;
            return ordered[lower] + (ordered[upper] - ordered[lower]) * fraction;
        }

        private static IEnumerator WaitForScene(string sceneName)
        {
            while (SceneManager.GetActiveScene().name != sceneName ||
                   (BootInitializer.Instance != null &&
                    BootInitializer.Instance.IsLoading))
            {
                yield return null;
            }

            yield return null;
        }

        [Serializable]
        private sealed class BenchmarkReport
        {
            public string unityVersion;
            public string platform;
            public int warmupFrames;
            public int measurementFrames;
            public List<ScenarioReport> scenarios;
        }

        [Serializable]
        private sealed class ScenarioReport
        {
            public string name;
            public List<MetricReport> metrics;
        }

        [Serializable]
        private sealed class MetricReport
        {
            public string name;
            public string unit;
            public int sampleCount;
            public double mean;
            public double median;
            public double p95;
            public double minimum;
            public double maximum;
        }
    }
}
