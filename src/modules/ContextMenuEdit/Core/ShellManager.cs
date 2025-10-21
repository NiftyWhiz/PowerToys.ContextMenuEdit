using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
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
                            return pathDir;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Error checking PATH directory", ex);
                    }
                }
            }

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

            return Path.Combine(installPath, "imports", "powertoys.nss");
        }

        public static string GetShellBackupPath()
        {
            var powerToysData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft", "PowerToys", "ContextMenuEdit", "Backups");
            
            Directory.CreateDirectory(powerToysData);
            return powerToysData;
        }

        public static async Task<bool> ReloadShellConfigAsync()
        {
            try
            {
                var installPath = DetectShellInstallation();
                if (string.IsNullOrEmpty(installPath))
                {
                    return false;
                }

                var shellExe = Path.Combine(installPath, "shell.exe");
                if (!File.Exists(shellExe))
                {
                    return false;
                }

                // Use Shell's reload command
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = shellExe,
                    Arguments = "reload",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });

                if (process != null)
                {
                    await process.WaitForExitAsync();
                    return process.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to reload Shell config", ex);
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
                Logger.LogError("Failed to get Shell version", ex);
                return "Error reading version";
            }
        }

        public static async Task<bool> DownloadAndInstallShellAsync()
        {
            try
            {
                Logger.LogInfo("Starting Nilesoft Shell download");
                
                // This would need to be implemented based on actual Shell distribution
                // For now, direct user to manual installation
                var installPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nilesoft Shell");
                Directory.CreateDirectory(installPath);
                
                // In a real implementation, you'd:
                // 1. Download shell.zip from nilesoft.org
                // 2. Extract to LocalApplicationData
                // 3. Run installation/registration
                
                // For MVP, we'll just create the directory and prompt user
                return false; // Return false to prompt manual installation
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to download/install Shell", ex);
                return false;
            }
        }
    }
}
