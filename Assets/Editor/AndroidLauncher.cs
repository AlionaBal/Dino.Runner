using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace App.Editor
{
    /// Installs and launches an APK on a connected device / running emulator, booting the first AVD if none is online.
    internal static class AndroidLauncher
    {
        private const string Title = "Run (Android)";
        private const int BootTimeoutMs = 300_000;

        public static void Run(string apkPath)
        {
            string adb = FindSdkTool("platform-tools", "adb");

            if (adb == null)
            {
                EditorUtility.DisplayDialog(Title, "adb was not found. Install the Android Build Support module " +
                    "(with Android SDK & NDK Tools) in Unity Hub, or set ANDROID_HOME.", "OK");
                return;
            }

            string serial = GetOnlineDevice(adb);
            string emulator = null;
            string avd = null;

            if (serial == null)
            {
                emulator = FindSdkTool("emulator", "emulator");
                avd = emulator != null ? GetFirstAvd(emulator) : null;

                if (avd == null)
                {
                    EditorUtility.DisplayDialog(Title, "No Android device or emulator is available.\n\n" +
                        "Connect a device with USB debugging enabled, or install the Android Emulator and create " +
                        "a virtual device (Android Studio > Device Manager).", "OK");
                    return;
                }
            }

            string apk = Path.GetFullPath(apkPath);
            string package = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);

            Task.Run(() =>
            {
                try
                {
                    if (serial == null)
                        serial = BootEmulator(adb, emulator, avd);

                    Debug.Log($"[Run Android] Installing {Path.GetFileName(apk)} on {serial}...");
                    Check(ShellCommand.Run(adb, $"-s {serial} install -r {ShellCommand.Quote(apk)}", 300_000), "install");

                    Check(ShellCommand.Run(adb,
                        $"-s {serial} shell monkey -p {package} -c android.intent.category.LAUNCHER 1"), "launch");

                    Debug.Log($"[Run Android] Launched {package} on {serial}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Run Android] {e.Message}");
                }
            });
        }

        private static string BootEmulator(string adb, string emulator, string avd)
        {
            Debug.Log($"[Run Android] Starting emulator '{avd}'...");
            ShellCommand.StartDetached(emulator, $"-avd {ShellCommand.Quote(avd)}");

            var deadline = DateTime.UtcNow.AddMilliseconds(BootTimeoutMs);

            while (DateTime.UtcNow < deadline)
            {
                string serial = GetOnlineDevice(adb);

                if (serial != null &&
                    ShellCommand.Run(adb, $"-s {serial} shell getprop sys.boot_completed").Output.Trim() == "1")
                {
                    Debug.Log($"[Run Android] Emulator {serial} booted");
                    return serial;
                }

                Thread.Sleep(2000);
            }

            throw new TimeoutException($"Emulator '{avd}' did not finish booting in {BootTimeoutMs / 1000}s");
        }

        private static string GetOnlineDevice(string adb)
        {
            // "List of devices attached" followed by "<serial>\tdevice" rows
            return ShellCommand.Run(adb, "devices").Output
                .Split('\n')
                .Skip(1)
                .Select(line => line.Trim().Split('\t'))
                .Where(columns => columns.Length == 2 && columns[1] == "device")
                .Select(columns => columns[0])
                .FirstOrDefault();
        }

        private static string GetFirstAvd(string emulator)
        {
            return ShellCommand.Run(emulator, "-list-avds").Output
                .Split('\n')
                .Select(line => line.Trim())
                .FirstOrDefault(line => line.Length > 0 && !line.StartsWith("INFO") && !line.Contains(" "));
        }

        private static string FindSdkTool(string folder, string tool)
        {
            string executable = Application.platform == RuntimePlatform.WindowsEditor ? tool + ".exe" : tool;

            return GetSdkRoots()
                .Where(root => !string.IsNullOrEmpty(root))
                .Select(root => Path.Combine(root, folder, executable))
                .FirstOrDefault(File.Exists);
        }

        private static IEnumerable<string> GetSdkRoots()
        {
            // Custom SDK from Preferences > External Tools, then the SDK bundled with the Android module
            yield return EditorPrefs.GetString("AndroidSdkRoot");

            string bundled = null;
            try
            {
                bundled = Path.Combine(BuildPipeline.GetPlaybackEngineDirectory(BuildTarget.Android, BuildOptions.None), "SDK");
            }
            catch (Exception)
            {
                // Android module not installed
            }
            yield return bundled;

            yield return Environment.GetEnvironmentVariable("ANDROID_HOME");
            yield return Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT");

            // Android Studio's default SDK location (has the emulator, which Unity's bundled SDK does not)
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            yield return Application.platform == RuntimePlatform.WindowsEditor
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Android", "Sdk")
                : Path.Combine(home, "Library", "Android", "sdk");
        }

        private static void Check(ShellCommand.Result result, string step)
        {
            // adb can exit 0 and still report "Failure [...]" (install) or "monkey aborted" (no launcher activity)
            if (!result.Succeeded || result.Output.Contains("Failure") || result.Output.Contains("Error") ||
                result.Output.Contains("aborted"))
                throw new Exception($"{step} failed: {result.Output.Trim()}");
        }
    }
}
