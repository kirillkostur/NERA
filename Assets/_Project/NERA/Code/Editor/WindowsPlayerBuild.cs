using System;
using System.IO;
using System.Linq;
using NERA.Graphics;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace NERA.Editor
{
    public static class WindowsPlayerBuild
    {
        private const string OutputPath = "Builds/Windows/NERA.exe";
        private const string FirstPlayableDevelopmentOutputPath =
            "Builds/WindowsDevelopment/NERA_FirstPlayable.exe";
        private const string PlayerDefaultsDefine =
            "NERA_WINDOWS_HIGH_100_FPS";
        private static readonly string[] FirstPlayableScenes =
        {
            "Assets/_Project/NERA/Scenes/Boot.unity",
            "Assets/_Project/NERA/Scenes/MainScene.unity",
            "Assets/_Project/NERA/Scenes/Player_Station.unity",
            "Assets/_Project/NERA/Scenes/Expedition_01.unity"
        };

        [MenuItem("NERA/Build/Windows x64")]
        public static void Build()
        {
            ProjectValidator.ValidateOrThrow();

            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            BuildPlayer(
                scenes,
                OutputPath,
                BuildOptions.None,
                "Windows");
        }

        [MenuItem("NERA/Build/First Playable Development x64")]
        public static void BuildFirstPlayableDevelopment()
        {
            ProjectValidator.ValidateOrThrow();

            foreach (string scenePath in FirstPlayableScenes)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) ==
                    null)
                {
                    throw new InvalidOperationException(
                        $"First Playable scene is missing: {scenePath}");
                }
            }

            BuildPlayer(
                FirstPlayableScenes,
                FirstPlayableDevelopmentOutputPath,
                BuildOptions.Development |
                BuildOptions.AllowDebugging |
                BuildOptions.ConnectWithProfiler,
                "First Playable Development");
        }

        private static void BuildPlayer(
            string[] scenes,
            string outputPath,
            BuildOptions options,
            string buildLabel)
        {

            if (scenes.Length == 0)
                throw new InvalidOperationException("No enabled scenes in Build Settings.");

            if (!UnityEngine.QualitySettings.names.Contains(
                    PCQualityRuntimeController.WindowsBuildQualityPreset))
            {
                throw new InvalidOperationException(
                    $"Required quality preset " +
                    $"'{PCQualityRuntimeController.WindowsBuildQualityPreset}' " +
                    "is missing.");
            }

            string outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                extraScriptingDefines = new[] { PlayerDefaultsDefine },
                options = options
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Windows build failed: {report.summary.result}, " +
                    $"{report.summary.totalErrors} errors."
                );
            }

            UnityEngine.Debug.Log(
                $"{buildLabel} build complete: {outputPath} " +
                $"({report.summary.totalSize / (1024f * 1024f):0.0} MB), " +
                $"scenes={scenes.Length}, " +
                $"preset={PCQualityRuntimeController.WindowsBuildQualityPreset}, " +
                $"FPS cap={PCQualityRuntimeController.WindowsBuildTargetFrameRate}."
            );
        }
    }
}
