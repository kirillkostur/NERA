using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace NERA.Editor
{
    public static class WindowsPlayerBuild
    {
        private const string OutputPath = "Builds/Windows/NERA.exe";

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

            string outputDirectory = Path.GetDirectoryName(OutputPath);
            if (!string.IsNullOrEmpty(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputPath,
                target = BuildTarget.StandaloneWindows64,
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
                $"({report.summary.totalSize / (1024f * 1024f):0.0} MB)."
            );
        }
    }
}
