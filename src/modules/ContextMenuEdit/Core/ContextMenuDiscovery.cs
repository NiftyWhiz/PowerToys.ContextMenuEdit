using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using ManagedCommon;

namespace Microsoft.PowerToys.Settings.UI.ContextMenuEdit.Core
{
    public class ExistingContextMenuItem
    {
        public string Title { get; set; } = "";
        public string Command { get; set; } = "";
        public string Arguments { get; set; } = "";
        public string Icon { get; set; } = "";
        public string SourceApp { get; set; } = "";
        public string RegistryLocation { get; set; } = "";
        public bool IsLegacyItem { get; set; } = true;
        public bool IsModified { get; set; } = false;
        public bool IsHidden { get; set; } = false;
        public ContextScope DetectedScope { get; set; } = ContextScope.All;
    }

    public static class ContextMenuDiscovery
    {
        private static readonly string[] RegistryRoots = {
            @"HKEY_CLASSES_ROOT\*\shell",           // All files
            @"HKEY_CLASSES_ROOT\Directory\shell",   // Folders
            @"HKEY_CLASSES_ROOT\Directory\Background\shell", // Background
            @"HKEY_CLASSES_ROOT\AllFilesystemObjects\shell", // Files and folders
        };

        public static List<ExistingContextMenuItem> DiscoverExistingItems()
        {
            var items = new List<ExistingContextMenuItem>();

            try
            {
                // Scan registry for legacy context menu items
                items.AddRange(ScanRegistryContextItems());
                
                // Parse existing Shell config for modifications
                items.AddRange(ParseExistingShellConfig());
                
                // Add known problematic items that users commonly want to remove
                items.AddRange(GetCommonClutterItems());
            }
            catch (Exception ex)
            {
                Logger.LogError("Error discovering context menu items", ex);
            }

            return items.DistinctBy(i => $"{i.Title}|{i.Command}").ToList();
        }

        private static List<ExistingContextMenuItem> ScanRegistryContextItems()
        {
            var items = new List<ExistingContextMenuItem>();

            var registryMappings = new Dictionary<string, ContextScope>
            {
                { @"HKEY_CLASSES_ROOT\*\shell", ContextScope.File },
                { @"HKEY_CLASSES_ROOT\Directory\shell", ContextScope.Folder },
                { @"HKEY_CLASSES_ROOT\Directory\Background\shell", ContextScope.Background },
                { @"HKEY_CLASSES_ROOT\AllFilesystemObjects\shell", ContextScope.All }
            };

            foreach (var (registryPath, scope) in registryMappings)
            {
                try
                {
                    items.AddRange(ScanRegistryPath(registryPath, scope));
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Could not scan registry path {registryPath}: {ex.Message}");
                }
            }

            return items;
        }

        private static List<ExistingContextMenuItem> ScanRegistryPath(string registryPath, ContextScope scope)
        {
            var items = new List<ExistingContextMenuItem>();
            
            try
            {
                var pathParts = registryPath.Split('\\');
                var hive = pathParts[0] switch
                {
                    "HKEY_CLASSES_ROOT" => Registry.ClassesRoot,
                    "HKEY_CURRENT_USER" => Registry.CurrentUser,
                    "HKEY_LOCAL_MACHINE" => Registry.LocalMachine,
                    _ => null
                };

                if (hive == null) return items;

                var subKeyPath = string.Join("\\", pathParts.Skip(1));
                
                using var key = hive.OpenSubKey(subKeyPath, false);
                if (key == null) return items;

                foreach (var subKeyName in key.GetSubKeyNames())
                {
                    try
                    {
                        using var subKey = key.OpenSubKey(subKeyName);
                        if (subKey == null) continue;

                        var item = new ExistingContextMenuItem
                        {
                            Title = subKey.GetValue("", subKeyName)?.ToString() ?? subKeyName,
                            RegistryLocation = $"{registryPath}\\{subKeyName}",
                            DetectedScope = scope,
                            IsLegacyItem = true
                        };

                        // Try to get the command
                        using var commandKey = subKey.OpenSubKey("command");
                        if (commandKey != null)
                        {
                            var command = commandKey.GetValue("")?.ToString();
                            if (!string.IsNullOrEmpty(command))
                            {
                                item.Command = command;
                                item.SourceApp = DetectSourceApp(command);
                            }
                        }

                        // Get icon if available
                        var icon = subKey.GetValue("Icon")?.ToString();
                        if (!string.IsNullOrEmpty(icon))
                        {
                            item.Icon = icon;
                        }

                        // Only add items with actual commands
                        if (!string.IsNullOrEmpty(item.Command))
                        {
                            items.Add(item);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"Error reading registry subkey {subKeyName}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Error scanning registry path {registryPath}: {ex.Message}");
            }

            return items;
        }

        private static string DetectSourceApp(string command)
        {
            try
            {
                // Extract executable name from command line
                var cleanCommand = command.Trim('"', ' ');
                var exePath = cleanCommand.Split(' ')[0];
                
                if (File.Exists(exePath))
                {
                    var fileName = Path.GetFileNameWithoutExtension(exePath);
                    return char.ToUpper(fileName[0]) + fileName[1..].ToLower();
                }

                // Try to parse common patterns
                if (command.Contains("adobe", StringComparison.OrdinalIgnoreCase))
                    return "Adobe";
                if (command.Contains("winrar", StringComparison.OrdinalIgnoreCase))
                    return "WinRAR";
                if (command.Contains("7-zip", StringComparison.OrdinalIgnoreCase) || command.Contains("7z", StringComparison.OrdinalIgnoreCase))
                    return "7-Zip";
                if (command.Contains("notepad", StringComparison.OrdinalIgnoreCase))
                    return "Notepad++";

                return "Unknown";
            }
            catch (Exception)
            {
                return "Unknown";
            }
        }

        private static List<ExistingContextMenuItem> ParseExistingShellConfig()
        {
            var items = new List<ExistingContextMenuItem>();

            try
            {
                var configPath = ShellManager.GetShellConfigPath();
                if (!File.Exists(configPath))
                {
                    return items;
                }

                // Parse existing Shell config to see what's already modified
                var configContent = File.ReadAllText(configPath);
                // This would require a simple NSS parser
                // For MVP, we can skip this and just track in our own settings
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Could not parse existing Shell config: {ex.Message}");
            }

            return items;
        }

        private static List<ExistingContextMenuItem> GetCommonClutterItems()
        {
            // Pre-defined list of commonly unwanted context menu items
            return new List<ExistingContextMenuItem>
            {
                new() { Title = "Edit with Adobe Photoshop", SourceApp = "Adobe", DetectedScope = ContextScope.File },
                new() { Title = "Edit with Paint 3D", SourceApp = "Microsoft", DetectedScope = ContextScope.File },
                new() { Title = "Add to archive", SourceApp = "WinRAR", DetectedScope = ContextScope.All },
                new() { Title = "Extract files", SourceApp = "WinRAR", DetectedScope = ContextScope.File },
                new() { Title = "Extract Here", SourceApp = "WinRAR", DetectedScope = ContextScope.File },
                new() { Title = "Extract to folder", SourceApp = "WinRAR", DetectedScope = ContextScope.File },
                new() { Title = "Send to Mail Recipient", SourceApp = "Windows", DetectedScope = ContextScope.File },
                new() { Title = "Fax Recipient", SourceApp = "Windows", DetectedScope = ContextScope.File },
            };
        }

        private string EscapeString(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "''";

            // Escape quotes and backslashes for Shell config
            return "'" + input.Replace("\\", "\\\\").Replace("'", "\\'") + "'";
        }

        private string EscapeComment(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "";

            return input.Replace("*/", "").Replace("/*", "");
        }

        private string ExpandEnvironmentPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            try
            {
                return Environment.ExpandEnvironmentVariables(path);
            }
            catch
            {
                return path;
            }
        }
    }
}
