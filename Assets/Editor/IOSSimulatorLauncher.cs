using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace App.Editor
{
    /// Compiles the Unity-generated Xcode project for the Simulator, then installs and launches it on an iPhone simulator.
    internal static class IOSSimulatorLauncher
    {
        private const string Title = "Run (iOS)";
        private const int XcodeBuildTimeoutMs = 1_800_000;

        // "    iPhone 16 Pro (0A1B2C3D-...) (Shutdown)" rows of `xcrun simctl list devices available`
        private static readonly Regex SimulatorRow =
            new Regex(@"^\s+(?<name>iPhone.*?)\s+\((?<udid>[0-9A-F-]{36})\)\s+\((?<state>\w+)\)", RegexOptions.Multiline);

        public static void Run(string xcodeProjectFolder)
        {
            if (!ShellCommand.Run("xcrun", "--find xcodebuild").Succeeded)
            {
                EditorUtility.DisplayDialog(Title, "Xcode is required to run iOS builds in the Simulator.\n\n" +
                    "Install Xcode from the App Store, then select it: sudo xcode-select -s /Applications/Xcode.app", "OK");
                return;
            }

            var simulator = SimulatorRow.Matches(ShellCommand.Run("xcrun", "simctl list devices available").Output)
                .Cast<Match>()
                .OrderByDescending(match => match.Groups["state"].Value == "Booted")
                .FirstOrDefault();

            if (simulator == null)
            {
                EditorUtility.DisplayDialog(Title, "No iPhone simulator is available. Install an iOS Simulator runtime " +
                    "in Xcode > Settings > Components.", "OK");
                return;
            }

            string udid = simulator.Groups["udid"].Value;
            string name = simulator.Groups["name"].Value;
            string projectFolder = Path.GetFullPath(xcodeProjectFolder);
            string bundleId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS);

            Task.Run(() =>
            {
                try
                {
                    string app = BuildApp(projectFolder, udid);

                    Debug.Log($"[Run iOS] Booting {name}...");
                    ShellCommand.Run("xcrun", $"simctl boot {udid}"); // fails harmlessly when already booted
                    ShellCommand.Run("open", "-a Simulator");

                    Check(ShellCommand.Run("xcrun", $"simctl install {udid} {ShellCommand.Quote(app)}", 300_000), "install");
                    Check(ShellCommand.Run("xcrun", $"simctl launch {udid} {bundleId}"), "launch");

                    Debug.Log($"[Run iOS] Launched {bundleId} on {name}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Run iOS] {e.Message}");
                }
            });
        }

        private static string BuildApp(string projectFolder, string udid)
        {
            string derivedData = Path.Combine(projectFolder, "DerivedData");
            string project = Path.Combine(projectFolder, "Unity-iPhone.xcodeproj");

            Debug.Log("[Run iOS] Compiling the Xcode project for the Simulator (this can take a few minutes)...");
            Check(ShellCommand.Run("xcodebuild",
                $"-project {ShellCommand.Quote(project)} -scheme Unity-iPhone -configuration Debug " +
                $"-destination id={udid} -derivedDataPath {ShellCommand.Quote(derivedData)} " +
                "CODE_SIGNING_ALLOWED=NO build", XcodeBuildTimeoutMs), "xcodebuild");

            string products = Path.Combine(derivedData, "Build", "Products", "Debug-iphonesimulator");
            string app = Directory.Exists(products) ? Directory.GetDirectories(products, "*.app").FirstOrDefault() : null;

            return app ?? throw new Exception($"xcodebuild produced no .app in {products}");
        }

        private static void Check(ShellCommand.Result result, string step)
        {
            if (!result.Succeeded)
                throw new Exception($"{step} failed (exit {result.ExitCode}): {Tail(result.Output)}");
        }

        // xcodebuild output is huge; the error is at the end
        private static string Tail(string output) =>
            output.Length > 3000 ? "..." + output.Substring(output.Length - 3000) : output.Trim();
    }
}
