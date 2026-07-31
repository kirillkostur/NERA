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
        private const string PlayerDefaultsDefine =
            "NERA_WINDOWS_HIGH_100_FPS";

        [MenuItem("NERA/Build/Windows x64")]
        public static void Build()
        {
            ProjectValidator.ValidateOrThrow();

            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

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

            string outputDirectory = Path.GetDirectoryName(OutputPath);
            if (!string.IsNullOrEmpty(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputPath,
                target = BuildTarget.StandaloneWindows64,
                extraScriptingDefines = new[] { PlayerDefaultsDefine },
                options = BuildOptions.None
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Windows build failed: {report.summary.result}, " +
                    $"{report.summary.totalErrors} errors."
                );
            }

            UnityEngine.Debug.Log(
                $"Windows build complete: {OutputPath} " +
                $"({report.summary.totalSize / (1024f * 1024f):0.0} MB), " +
                $"preset={PCQualityRuntimeController.WindowsBuildQualityPreset}, " +
                $"FPS cap={PCQualityRuntimeController.WindowsBuildTargetFrameRate}."
            );
        }
    }
}
