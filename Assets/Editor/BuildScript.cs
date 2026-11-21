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

        internal static readonly string MacOSPath = Path.Combine(BuildsRoot, "macOS", "DinoRunner.app");
        internal static readonly string WindowsPath = Path.Combine(BuildsRoot, "Windows", "DinoRunner.exe");
        internal static readonly string WebGLPath = Path.Combine(BuildsRoot, "WebGL");
        internal static readonly string AndroidPath = Path.Combine(BuildsRoot, "Android", "dino-runner.apk");
        internal static readonly string IOSSimulatorPath = Path.Combine(BuildsRoot, "iOS");

        [MenuItem("Build/macOS", priority = 1)]
        public static void BuildMacOS() => Build(BuildTarget.StandaloneOSX, MacOSPath);

        [MenuItem("Build/Windows", priority = 2)]
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, WindowsPath);

        [MenuItem("Build/WebGL", priority = 3)]
        public static void BuildWebGL() => Build(BuildTarget.WebGL, WebGLPath);

        [MenuItem("Build/Android", priority = 4)]
        public static void BuildAndroid() => Build(BuildTarget.Android, AndroidPath);

        // Xcode project built against the Simulator SDK, so "Build/Run (iOS)" can launch it in the iOS Simulator
        [MenuItem("Build/iOS Simulator", priority = 5)]
        public static void BuildIOSSimulator()
        {
            var sdkVersion = PlayerSettings.iOS.sdkVersion;
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.SimulatorSDK;

            try
            {
                Build(BuildTarget.iOS, IOSSimulatorPath);
            }
            finally
            {
                PlayerSettings.iOS.sdkVersion = sdkVersion;
            }
        }

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
