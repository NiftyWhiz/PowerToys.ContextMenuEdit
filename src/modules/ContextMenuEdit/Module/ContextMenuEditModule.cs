using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ContextMenuEdit.Core;
using ManagedCommon;

namespace Microsoft.PowerToys.Settings.UI.ContextMenuEdit
{
    public class ContextMenuEditModule : ISettingsModule, IDisposable
    {
        private const string ModuleName = "ContextMenuEdit";
        private readonly SemaphoreSlim _configSemaphore = new(1, 1);
        private ContextMenuEditSettings? _settings;
        private FileSystemWatcher? _settingsWatcher;
        private bool _disposed;
        private bool _installPromptShown;

        public string Name => ModuleName;
        public string Version => "1.0.0";

        public bool IsEnabled => _settings?.Enabled ?? false;

        public void Initialize(ISettingsUtils settingsUtils)
        {
            try
            {
                Logger.LogInfo("ContextMenuEditModule: Initializing Context Menu Edit module");
                
                LoadSettings(settingsUtils);
                
                if (_settings?.Enabled == true)
                {
                    Enable();
                }
                
                StartSettingsWatcher(settingsUtils);
                
                Logger.LogInfo("ContextMenuEditModule: Context Menu Edit module initialized successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError("ContextMenuEditModule: Failed to initialize Context Menu Edit module", ex);
            }
        }

        public void Enable()
        {
            try
            {
                Logger.LogInfo("ContextMenuEditModule: Enabling Context Menu Edit");

                if (!ShellManager.IsShellInstalled())
                {
                    if (_settings?.AutoInstallShell == true && !_installPromptShown)
                    {
                        _installPromptShown = true;
                        Task.Run(async () =>
                        {
                            var installed = await ShellManager.DownloadAndInstallShellAsync();
                            if (!installed)
                            {
                                ShowShellInstallationPrompt();
                            }
                            else
                            {
                                await GenerateAndApplyConfigSafe();
                            }
                        });
                    }
                    else
                    {
                        ShowShellInstallationPrompt();
                    }
                    return;
                }

                Task.Run(GenerateAndApplyConfigSafe);
            }
            catch (Exception ex)
            {
                Logger.LogError("ContextMenuEditModule: Failed to enable Context Menu Edit", ex);
            }
        }

        public void Disable()
        {
            try
            {
                Logger.LogInfo("ContextMenuEditModule: Disabling Context Menu Edit");
                
                // Restore backup or remove PowerToys config
                RestoreOriginalConfig();
                
                // Reload Shell
                Task.Run(() => ShellManager.ReloadShellConfigAsync());
            }
            catch (Exception ex)
            {
                Logger.LogError("ContextMenuEditModule: Failed to disable Context Menu Edit", ex);
            }
        }

        private void LoadSettings(ISettingsUtils settingsUtils)
        {
            try
            {
                var settingsJson = settingsUtils.GetSettings<ContextMenuEditSettings>(ModuleName);
                _settings = settingsJson ?? new ContextMenuEditSettings();
                Logger.LogInfo($"ContextMenuEditModule: Settings loaded - {_settings.NewActions.Count} actions, {_settings.Modifications.Count} modifications, {_settings.Removals.Count} removals");
            }
            catch (Exception ex)
            {
                Logger.LogError("ContextMenuEditModule: Failed to load settings, using defaults", ex);
                _settings = new ContextMenuEditSettings();
            }
        }

        private async Task GenerateAndApplyConfigSafe()
        {
            // Serialize config generation to prevent race conditions
            await _configSemaphore.WaitAsync();
            try
            {
                await GenerateAndApplyConfig();
            }
            finally
            {
                _configSemaphore.Release();
            }
        }

        private async Task GenerateAndApplyConfig()
        {
            const int maxRetries = 3;
            var delay = TimeSpan.FromMilliseconds(250);

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    if (_settings == null)
                    {
                        Logger.LogError("ContextMenuEditModule: Settings not initialized");
                        return;
                    }

                    Logger.LogInfo($"ContextMenuEditModule: Generating Shell configuration (attempt {attempt}/{maxRetries})");

                    // Backup existing config if enabled
                    if (_settings.AutoBackupConfigs)
                    {
                        BackupExistingConfig();
                    }

                    // Generate new config
                    var config = ShellConfigGenerator.GenerateConfig(_settings);
                    var configPath = ShellManager.GetShellConfigPath();

                    // Ensure directory exists
                    var configDir = Path.GetDirectoryName(configPath)!;
                    if (!Directory.Exists(configDir))
                    {
                        Directory.CreateDirectory(configDir);
                        Logger.LogInfo($"ContextMenuEditModule: Created config directory: {configDir}");
                    }

                    // Write config file
                    await File.WriteAllTextAsync(configPath, config);
                    
                    Logger.LogInfo($"ContextMenuEditModule: Shell configuration written to: {configPath}");

                    // Reload Shell
                    var reloaded = await ShellManager.ReloadShellConfigAsync();
                    if (!reloaded)
                    {
                        Logger.LogWarning("ContextMenuEditModule: Failed to reload Shell configuration");
                    }

                    if (_settings.ShowNotifications)
                    {
                        ShowNotification("Context menu updated successfully", "Your changes are now active in File Explorer");
                    }

                    return; // Success - exit retry loop
                }
                catch (UnauthorizedAccessException ex)
                {
                    Logger.LogError($"ContextMenuEditModule: Access denied writing config (attempt {attempt}): {ex.Message}");
                    ShowNotification("Permission Error", "Context menu update requires administrator privileges or Shell is installed in a protected location");
                    return; // Don't retry permission errors
                }
                catch (IOException ex) when (attempt < maxRetries)
                {
                    Logger.LogWarning($"ContextMenuEditModule: IO error on attempt {attempt}, retrying: {ex.Message}");
                    await Task.Delay(delay * attempt); // Exponential backoff
                }
                catch (Exception ex)
                {
                    Logger.LogError($"ContextMenuEditModule: Failed to generate and apply Shell config (attempt {attempt})", ex);
                    if (attempt == maxRetries)
                    {
                        ShowNotification("Context menu update failed", "Check PowerToys logs for details");
                    }
                }
            }
        }

        private void BackupExistingConfig()
        {
            try
            {
                var configPath = ShellManager.GetShellConfigPath();
                if (!File.Exists(configPath))
                {
                    return;
                }

                var backupDir = ShellManager.GetShellBackupPath();
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var backupPath = Path.Combine(backupDir, $"powertoys_backup_{timestamp}.nss");

                File.Copy(configPath, backupPath);
                Logger.LogInfo($"ContextMenuEditModule: Config backed up to: {backupPath}");

                // Keep only last 10 backups by last write time
                CleanupOldBackups(backupDir);
            }
            catch (Exception ex)
            {
                Logger.LogWarning("ContextMenuEditModule: Failed to backup Shell config", ex);
            }
        }

        private static void CleanupOldBackups(string backupDir)
        {
            try
            {
                var backupFiles = Directory.GetFiles(backupDir, "powertoys_backup_*.nss")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.LastWriteTimeUtc) // Use LastWriteTimeUtc instead of CreationTime
                    .Skip(10);

                foreach (var file in backupFiles)
                {
                    file.Delete();
                    Logger.LogInfo($"ContextMenuEditModule: Deleted old backup: {file.Name}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning("ContextMenuEditModule: Failed to cleanup old backups", ex);
            }
        }

        private void RestoreOriginalConfig()
        {
            try
            {
                var configPath = ShellManager.GetShellConfigPath();
                if (File.Exists(configPath))
                {
                    File.Delete(configPath);
                    Logger.LogInfo("ContextMenuEditModule: PowerToys Shell config removed");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("ContextMenuEditModule: Failed to restore original config", ex);
            }
        }

        private void StartSettingsWatcher(ISettingsUtils settingsUtils)
        {
            try
            {
                var settingsPath = settingsUtils.GetSettingsFilePath(ModuleName);
                var settingsDir = Path.GetDirectoryName(settingsPath);
                var settingsFile = Path.GetFileName(settingsPath);

                if (settingsDir == null || settingsFile == null)
                {
                    Logger.LogWarning("ContextMenuEditModule: Could not determine settings path for watcher");
                    return;
                }

                _settingsWatcher = new FileSystemWatcher(settingsDir, settingsFile)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };

                _settingsWatcher.Changed += async (sender, e) =>
                {
                    // Debounce multiple change events
                    await Task.Delay(500);
                    
                    Logger.LogInfo("ContextMenuEditModule: Settings changed, regenerating config");
                    LoadSettings(settingsUtils);
                    
                    if (_settings?.Enabled == true)
                    {
                        await GenerateAndApplyConfigSafe();
                    }
                };

                Logger.LogInfo($"ContextMenuEditModule: Settings watcher started for: {settingsPath}");
            }
            catch (Exception ex)
            {
                Logger.LogError("ContextMenuEditModule: Failed to start settings watcher", ex);
            }
        }

        private void ShowShellInstallationPrompt()
        {
            // This would integrate with PowerToys notification system
            Logger.LogInfo("ContextMenuEditModule: Nilesoft Shell installation required - showing prompt to user");
            ShowNotification("Nilesoft Shell Required", 
                "Context Menu Edit requires Nilesoft Shell. Please download and install from nilesoft.org or enable auto-installation in settings.");
        }

        private void ShowNotification(string title, string message)
        {
            // Integrate with PowerToys notification system when available
            // For now, just log with context
            Logger.LogInfo($"ContextMenuEditModule: Notification - {title}: {message}");
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _settingsWatcher?.Dispose();
            _configSemaphore?.Dispose();
            _disposed = true;
        }
    }
}
