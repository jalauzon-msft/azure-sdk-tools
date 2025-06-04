using Azure.Sdk.Tools.PerfAutomation.Models;
using Microsoft.Crank.Agent;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PerfAutomation
{
    static class Util
    {
        public static bool IsWindows { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        public static string GetUniquePath(string path)
        {
            var directoryName = Path.GetDirectoryName(path);
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
            var extension = Path.GetExtension(path);

            return GetUniquePaths(Path.Join(directoryName, fileNameWithoutExtension), extension)[0];
        }

        public static string[] GetUniquePaths(string prefix, params string[] extensions)
        {
            var uniquePaths = extensions.Select(e => $"{prefix}{e}");

            int index = 0;
            while (uniquePaths.Any(p => File.Exists(p)))
            {
                index++;
                uniquePaths = extensions.Select(e => $"{prefix}.{index}{e}");
            }

            return uniquePaths.ToArray();
        }

        // These commands must be run using "cmd /c" on Windows, since they are shell scripts rather than executables.
        private static readonly string[] _requiresShellOnWindows = new string[]
        {
            "mvn",
            "npm",
        };

        // TODO: We should usually not throw on error, since it prevents extracting StandardOutput and StandardError
        public static async Task<ProcessResult> RunAsync(
            string filename,
            string arguments,
            string workingDirectory,
            IDictionary<string, string> environmentVariables = null,
            StringBuilder outputBuilder = null,
            StringBuilder errorBuilder = null,
            bool throwOnError = true
            )
        {
            if (IsWindows && _requiresShellOnWindows.Contains(filename))
            {
                arguments = $"/c {filename} {arguments}";
                filename = "cmd";
            }

            if (environmentVariables != null)
            {
                Log.WriteLine($"[{workingDirectory}] Env: {JsonSerializer.Serialize(environmentVariables)}");
            }

            var result = await ProcessUtil.RunAsync(
                filename,
                arguments,
                workingDirectory: workingDirectory,
                environmentVariables: environmentVariables,
                throwOnError: false,
                log: true,
                captureOutput: true,
                captureError: true);

            outputBuilder?.Append(result.StandardOutput);
            errorBuilder?.Append(result.StandardError);

            if (throwOnError && result.ExitCode != 0)
            {
                throw new ProcessResultException(command: $"{filename} {arguments}", result: result);
            }
            else
            {
                return result;
            }
        }

        public static async Task<ProcessResultWithStats> RunWithStatsAsync(
            string filename,
            string arguments,
            string workingDirectory)
        {
            if (IsWindows && _requiresShellOnWindows.Contains(filename))
            {
                arguments = $"/c {filename} {arguments}";
                filename = "cmd";
            }

            using var process = new Process()
            {
                StartInfo =
                {
                    FileName = filename,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = workingDirectory,
                },
                EnableRaisingEvents = true
            };

            var outputBuilder = new StringBuilder();
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    outputBuilder.AppendLine(e.Data);
                    Log.WriteLine(e.Data);
                }
            };

            var errorBuilder = new StringBuilder();
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    errorBuilder.AppendLine(e.Data);
                    Log.WriteLine("[STDERR] " + e.Data);
                }
            };
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var stopwatch = Stopwatch.StartNew();
            TimeSpan lastCpuTime = TimeSpan.Zero;
            List<double> cpuSamples = new();
            List<long> memorySamples = new();

            while (!process.HasExited)
            {
                await Task.Delay(1000);

                try
                {
                    // CPU usage
                    TimeSpan currentCpuTime = process.TotalProcessorTime;
                    double cpuUsage = (currentCpuTime - lastCpuTime).TotalMilliseconds / (1000.0 * Environment.ProcessorCount) * 100;
                    lastCpuTime = currentCpuTime;
                    cpuSamples.Add(cpuUsage);

                    // Memory usage
                    process.Refresh(); // Refresh to get updated memory info
                    long memoryUsage = process.WorkingSet64;
                    memorySamples.Add(memoryUsage);
                }
                catch (InvalidOperationException)
                {
                    // Process may have exited while checking stats, ignore
                }
            }

            stopwatch.Stop();

            double avgCpu = cpuSamples.Count > 0 ? cpuSamples.Average() : 0;
            double avgMemoryInMB = memorySamples.Count > 0 ? memorySamples.Average() / 1024.0 / 1024.0 : 0;

            Console.WriteLine($"Average CPU Usage: {avgCpu:F2}%");
            Console.WriteLine($"Average Memory Usage: {avgMemoryInMB:F2} MB");

            return new ProcessResultWithStats()
            {
                ExitCode = process.ExitCode,
                StandardOutput = outputBuilder.ToString(),
                StandardError = errorBuilder.ToString(),
                AvgCpu = avgCpu,
                AvgMemoryInMB = avgMemoryInMB,
            };
        }

        public static void DeleteIfExists(string path)
        {
            if (Directory.Exists(path))
            {
                try
                {
                    Directory.Delete(path, recursive: true);
                }
                catch (UnauthorizedAccessException)
                {
                    // Allow deleting read-only files
                    foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                    }

                    Directory.Delete(path, recursive: true);
                }
            }
        }

        /// <summary>
        /// Gets the directory where profiles should be written to during performance testing.
        /// 
        /// The directory is based on the repository root folder.
        /// </summary>
        /// <param name="repoRoot">The repository root folder path.</param>
        /// <returns>The directory where profiles should be written.</returns>
        public static string GetProfileDirectory(string repoRoot)
        {
            return Path.Combine(repoRoot, "profile");
        }
    }
}
