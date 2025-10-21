using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ContextMenuEdit.Core;
using ManagedCommon;

namespace Microsoft.PowerToys.Settings.UI.ContextMenuEdit
{
    public class ContextMenuEditModule : ISettingsModule, IDisposable
    {
        private const string ModuleName = "ContextMenuEdit";
        private ContextMenuEditSettings? _settings;
        private ShellConfigGenerator? _configGenerator;
        private FileSystemWatcher? _settingsWatcher;
        private bool _disposed;

        public string Name => ModuleName;
        public string Version => "1.0.0";

        public bool IsEnabled => _settings?.Enabled ?? false;

        public void Initialize(ISettingsUtils settingsUtils)
        {
            try
            {
                Logger.LogInfo("Initializing Context Menu Edit module");
                
                _configGenerator = new ShellConfigGenerator();
                LoadSettings(settingsUtils);
                
                if (_settings?.Enabled == true)
                {
                    Enable();
                }
                
                StartSettingsWatcher(settingsUtils);
                
                Logger.LogInfo("Context Menu Edit module initialized successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to initialize Context Menu Edit module", ex);
            }
        }

        public void Enable()
        {
            try
            {
                Logger.LogInfo("Enabling Context Menu Edit");

                if (!ShellManager.IsShellInstalled())
                {
                    if (_settings?.AutoInstallShell == true)
                    {
                        Task.Run(async () =>
                        {
                            var installed = await ShellManager.DownloadAndInstallShellAsync();
                            if (!installed)
                            {
                                ShowShellInstallationPrompt();
                            }
                            else
                            {
                                await GenerateAndApplyConfig();
                            }
                        });
                    }
                    else
                    {
                        ShowShellInstallationPrompt();
                    }
                    return;
                }

                Task.Run(GenerateAndApplyConfig);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to enable Context Menu Edit", ex);
            }
        }

        public void Disable()
        {
            try
            {
                Logger.LogInfo("Disabling Context Menu Edit");
                
                // Restore backup or remove PowerToys config
                RestoreOriginalConfig();
                
                // Reload Shell
                Task.Run(() => ShellManager.ReloadShellConfigAsync());
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to disable Context Menu Edit", ex);
            }
        }

        private void LoadSettings(ISettingsUtils settingsUtils)
        {
            try
            {
                var settingsJson = settingsUtils.GetSettings<ContextMenuEditSettings>(ModuleName);
                _settings = settingsJson ?? new ContextMenuEditSettings();
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to load settings, using defaults", ex);
                _settings = new ContextMenuEditSettings();
            }
        }

        private async Task GenerateAndApplyConfig()
        {
            try
            {
                if (_settings == null || _configGenerator == null)
                {
                    Logger.LogError("Settings or config generator not initialized");
                    return;
                }

                Logger.LogInfo("Generating Shell configuration");

                // Backup existing config if enabled
                if (_settings.AutoBackupConfigs)
                {
                    BackupExistingConfig();
                }

                // Generate new config
                var config = _configGenerator.GenerateConfig(_settings);
                var configPath = ShellManager.GetShellConfigPath();

                // Ensure directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);

                // Write config file
                await File.WriteAllTextAsync(configPath, config);
                
                Logger.LogInfo($"Shell configuration written to: {configPath}");

                // Reload Shell
                var reloaded = await ShellManager.ReloadShellConfigAsync();
                if (!reloaded)
                {
                    Logger.LogWarning("Failed to reload Shell configuration");
                }

                if (_settings.ShowNotifications)
                {
                    // Show success notification (implement based on PowerToys notification system)
                    ShowNotification("Context menu updated successfully", "Your changes are now active in File Explorer");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to generate and apply Shell config", ex);
                ShowNotification("Context menu update failed", "Check PowerToys logs for details");
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
                Logger.LogInfo($"Config backed up to: {backupPath}");

                // Keep only last 10 backups
                CleanupOldBackups(backupDir);
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Failed to backup Shell config", ex);
            }
        }

        private void CleanupOldBackups(string backupDir)
        {
            try
            {
                var backupFiles = Directory.GetFiles(backupDir, "powertoys_backup_*.nss")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.CreationTime)
                    .Skip(10);

                foreach (var file in backupFiles)
                {
                    file.Delete();
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Failed to cleanup old backups", ex);
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
                    Logger.LogInfo("PowerToys Shell config removed");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to restore original config", ex);
            }
        }

        private void StartSettingsWatcher(ISettingsUtils settingsUtils)
        {
            try
            {
                var settingsPath = settingsUtils.GetSettingsFilePath(ModuleName);
                var settingsDir = Path.GetDirectoryName(settingsPath);
                var settingsFile = Path.GetFileName(settingsPath);

                _settingsWatcher = new FileSystemWatcher(settingsDir!, settingsFile!)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };

                _settingsWatcher.Changed += async (sender, e) =>
                {
                    // Debounce multiple change events
                    await Task.Delay(500);
                    
                    Logger.LogInfo("Settings changed, regenerating config");
                    LoadSettings(settingsUtils);
                    
                    if (_settings?.Enabled == true)
                    {
                        await GenerateAndApplyConfig();
                    }
                };
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to start settings watcher", ex);
            }
        }

        private void ShowShellInstallationPrompt()
        {
            // This would integrate with PowerToys notification system
            Logger.LogInfo("Nilesoft Shell installation required - showing prompt to user");
            ShowNotification("Nilesoft Shell Required", 
                "Context Menu Edit requires Nilesoft Shell. Please install from nilesoft.org or enable auto-installation in settings.");
        }

        private void ShowNotification(string title, string message)
        {
            // Integrate with PowerToys notification system
            // For now, just log
            Logger.LogInfo($"Notification: {title} - {message}");
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _settingsWatcher?.Dispose();
            _disposed = true;
        }
    }
}
