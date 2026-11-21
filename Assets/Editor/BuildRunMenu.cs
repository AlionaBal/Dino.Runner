using System.IO;
using UnityEditor;
using UnityEngine;

namespace App.Editor
{
    /// "Build/Run (<Platform>)" items: enabled only when that platform's build exists (and can run on this machine).
    internal static class BuildRunMenu
    {
        private const string Menu = "Build/Run ";
        private const int Priority = 100; // own section below the Build items

        private static bool IsMacEditor => Application.platform == RuntimePlatform.OSXEditor;
        private static bool IsWindowsEditor => Application.platform == RuntimePlatform.WindowsEditor;

        [MenuItem(Menu + "(macOS)", false, Priority)]
        private static void RunMacOS() => ShellCommand.StartDetached("open", "-n " + ShellCommand.Quote(Path.GetFullPath(BuildScript.MacOSPath)));

        [MenuItem(Menu + "(macOS)", true)]
        private static bool CanRunMacOS() => IsMacEditor && Directory.Exists(BuildScript.MacOSPath);

        [MenuItem(Menu + "(Windows)", false, Priority + 1)]
        private static void RunWindows() => ShellCommand.StartDetached(Path.GetFullPath(BuildScript.WindowsPath), "");

        [MenuItem(Menu + "(Windows)", true)]
        private static bool CanRunWindows() => IsWindowsEditor && File.Exists(BuildScript.WindowsPath);

        [MenuItem(Menu + "(WebGL)", false, Priority + 2)]
        private static void RunWebGL() => Application.OpenURL(WebGLServer.Serve(BuildScript.WebGLPath));

        [MenuItem(Menu + "(WebGL)", true)]
        private static bool CanRunWebGL() => File.Exists(Path.Combine(BuildScript.WebGLPath, "index.html"));

        [MenuItem(Menu + "(Android)", false, Priority + 3)]
        private static void RunAndroid() => AndroidLauncher.Run(BuildScript.AndroidPath);

        [MenuItem(Menu + "(Android)", true)]
        private static bool CanRunAndroid() => File.Exists(BuildScript.AndroidPath);

        [MenuItem(Menu + "(iOS)", false, Priority + 4)]
        private static void RunIOS() => IOSSimulatorLauncher.Run(BuildScript.IOSSimulatorPath);

        [MenuItem(Menu + "(iOS)", true)]
        private static bool CanRunIOS() =>
            IsMacEditor && Directory.Exists(Path.Combine(BuildScript.IOSSimulatorPath, "Unity-iPhone.xcodeproj"));
    }
}
