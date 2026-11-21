using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace App.Editor
{
    // Usage (CI): Unity -batchmode -quit -projectPath . -executeMethod App.Editor.BuildScript.BuildMacOS -logFile -
    public static class BuildScript
    {
        private const string BuildsRoot = "Builds";

        [MenuItem("Build/macOS")]
        public static void BuildMacOS() =>
            Build(BuildTarget.StandaloneOSX, Path.Combine(BuildsRoot, "macOS", "DinoRunner.app"));

        [MenuItem("Build/Windows")]
        public static void BuildWindows() =>
            Build(BuildTarget.StandaloneWindows64, Path.Combine(BuildsRoot, "Windows", "DinoRunner.exe"));

        [MenuItem("Build/WebGL")]
        public static void BuildWebGL() =>
            Build(BuildTarget.WebGL, Path.Combine(BuildsRoot, "WebGL"));

        [MenuItem("Build/Android")]
        public static void BuildAndroid() =>
            Build(BuildTarget.Android, Path.Combine(BuildsRoot, "Android", "dino-runner.apk"));

        private static void Build(BuildTarget target, string locationPathName)
        {
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = locationPathName,
                target = target,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            Debug.Log($"[BuildScript] {target}: {summary.result}, errors: {summary.totalErrors}, " +
                      $"size: {summary.totalSize} bytes, time: {summary.totalTime}, output: {summary.outputPath}");

            if (Application.isBatchMode)
                EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
            else if (summary.result != BuildResult.Succeeded)
                throw new Exception($"[BuildScript] {target} build failed: {summary.result}");
        }
    }
}
