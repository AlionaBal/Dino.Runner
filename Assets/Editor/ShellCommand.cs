using System.Diagnostics;

namespace App.Editor
{
    internal static class ShellCommand
    {
        public readonly struct Result
        {
            public readonly int ExitCode;
            public readonly string Output;

            public Result(int exitCode, string output)
            {
                ExitCode = exitCode;
                Output = output;
            }

            public bool Succeeded => ExitCode == 0;
        }

        /// Runs a process to completion and returns its exit code with stdout + stderr. ExitCode is -1 on timeout.
        public static Result Run(string fileName, string arguments, int timeoutMs = 60_000)
        {
            var startInfo = new ProcessStartInfo(fileName, arguments)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            try
            {
                using var process = Process.Start(startInfo);
                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();

                if (!process.WaitForExit(timeoutMs))
                {
                    try { process.Kill(); } catch { /* already exited */ }
                    return new Result(-1, $"'{fileName} {arguments}' timed out after {timeoutMs / 1000}s");
                }

                return new Result(process.ExitCode, stdout.Result + stderr.Result);
            }
            catch (System.ComponentModel.Win32Exception e)
            {
                return new Result(-1, $"Cannot start '{fileName}': {e.Message}");
            }
        }

        /// Starts a long-lived process (emulator, player) without waiting for it.
        public static void StartDetached(string fileName, string arguments)
        {
            using var _ = Process.Start(new ProcessStartInfo(fileName, arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            });
        }

        public static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
    }
}
