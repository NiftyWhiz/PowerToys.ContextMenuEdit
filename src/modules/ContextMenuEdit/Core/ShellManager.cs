using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading.Tasks;
using Microsoft.PowerToys.Settings.UI.Library;
using ManagedCommon;

namespace Microsoft.PowerToys.Settings.UI.ContextMenuEdit.Core
{
    public static class ShellManager
    {
        private static readonly string[] CommonInstallPaths = {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Nilesoft Shell"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Nilesoft Shell"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nilesoft Shell"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Nilesoft Shell")
        };

        public static string? DetectShellInstallation()
        {
            foreach (var path in CommonInstallPaths)
            {
                var exePath = Path.Combine(path, "shell.exe");
                if (File.Exists(exePath))
                {
                    Logger.LogInfo($"ShellManager: Found Shell installation at {path}");
                    return path;
                }
            }

            // Check PATH environment variable
            var pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(pathEnv))
            {
                foreach (var pathDir in pathEnv.Split(Path.PathSeparator))
                {
                    try
                    {
                        var exePath = Path.Combine(pathDir, "shell.exe");
                        if (File.Exists(exePath))
                        {
                            Logger.LogInfo($"ShellManager: Found Shell in PATH at {pathDir}");
                            return pathDir;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"ShellManager: Error checking PATH directory {pathDir}: {ex.Message}");
                    }
                }
            }

            Logger.LogInfo("ShellManager: Shell installation not found");
            return null;
        }

        public static bool IsShellInstalled()
        {
            return DetectShellInstallation() != null;
        }

        public static string GetShellConfigPath()
        {
            var installPath = DetectShellInstallation();
            if (string.IsNullOrEmpty(installPath))
            {
                throw new InvalidOperationException("Nilesoft Shell is not installed");
            }

            // Try per-user location first to avoid permission issues
            var userConfigPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Nilesoft Shell", "imports", "powertoys.nss");

            var userImportsDir = Path.GetDirectoryName(userConfigPath)!;
            if (!Directory.Exists(userImportsDir))
            {
                try
                {
                    Directory.CreateDirectory(userImportsDir);
                    Logger.LogInfo($"ShellManager: Created user imports directory: {userImportsDir}");
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"ShellManager: Could not create user imports directory: {ex.Message}");
                }
            }

            // Test write permission
            if (CanWriteToDirectory(Path.GetDirectoryName(userConfigPath)!))
            {
                return userConfigPath;
            }

            // Fallback to install directory (may require elevation)
            var installConfigPath = Path.Combine(installPath, "imports", "powertoys.nss");
            if (CanWriteToDirectory(Path.GetDirectoryName(installConfigPath)!))
            {
                return installConfigPath;
            }

            Logger.LogWarning($"ShellManager: No writable config path found, using: {userConfigPath}");
            return userConfigPath;
        }

        public static string GetShellBackupPath()
        {
            var powerToysData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft", "PowerToys", "ContextMenuEdit", "Backups");
            
            Directory.CreateDirectory(powerToysData);
            return powerToysData;
        }

        private static bool CanWriteToDirectory(string directory)
        {
            try
            {
                var testFile = Path.Combine(directory, $"test_write_{Guid.NewGuid():N}.tmp");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                Logger.LogInfo($"ShellManager: No write access to directory: {directory}");
                return false;
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"ShellManager: Error testing write access to {directory}: {ex.Message}");
                return false;
            }
        }

        public static async Task<bool> ReloadShellConfigAsync()
        {
            try
            {
                var installPath = DetectShellInstallation();
                if (string.IsNullOrEmpty(installPath))
                {
                    Logger.LogError("ShellManager: Cannot reload - Shell installation not found");
                    return false;
                }

                var shellExe = Path.Combine(installPath, "shell.exe");
                if (!File.Exists(shellExe))
                {
                    Logger.LogError($"ShellManager: Shell executable not found at {shellExe}");
                    return false;
                }

                Logger.LogInfo("ShellManager: Reloading Shell configuration");

                // Use Shell's reload command
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = shellExe,
                    Arguments = "reload",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                });

                if (process != null)
                {
                    var stdout = await process.StandardOutput.ReadToEndAsync();
                    var stderr = await process.StandardError.ReadToEndAsync();
                    
                    await process.WaitForExitAsync();
                    
                    if (process.ExitCode == 0)
                    {
                        Logger.LogInfo("ShellManager: Shell configuration reloaded successfully");
                        if (!string.IsNullOrEmpty(stdout))
                        {
                            Logger.LogInfo($"ShellManager: Shell stdout: {stdout}");
                        }
                        return true;
                    }
                    else
                    {
                        Logger.LogError($"ShellManager: Shell reload failed with exit code {process.ExitCode}");
                        if (!string.IsNullOrEmpty(stderr))
                        {
                            Logger.LogError($"ShellManager: Shell stderr: {stderr}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("ShellManager: Failed to reload Shell config", ex);
            }

            return false;
        }

        public static string GetShellVersion()
        {
            try
            {
                var installPath = DetectShellInstallation();
                if (string.IsNullOrEmpty(installPath))
                {
                    return "Not installed";
                }

                var shellExe = Path.Combine(installPath, "shell.exe");
                if (!File.Exists(shellExe))
                {
                    return "Invalid installation";
                }

                var versionInfo = FileVersionInfo.GetVersionInfo(shellExe);
                return versionInfo.FileVersion ?? "Unknown";
            }
            catch (Exception ex)
            {
                Logger.LogError("ShellManager: Failed to get Shell version", ex);
                return "Error reading version";
            }
        }

        public static async Task<bool> DownloadAndInstallShellAsync()
        {
            try
            {
                Logger.LogInfo("ShellManager: Starting Nilesoft Shell download");
                
                // For now, this is a placeholder that returns false
                // In a full implementation, this would:
                // 1. Download from nilesoft.org/download
                // 2. Extract to user's local directory
                // 3. Run any required registration
                
                Logger.LogInfo("ShellManager: Auto-install not yet implemented - user must install manually");
                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError("ShellManager: Failed to download/install Shell", ex);
                return false;
            }
        }
    }
}
