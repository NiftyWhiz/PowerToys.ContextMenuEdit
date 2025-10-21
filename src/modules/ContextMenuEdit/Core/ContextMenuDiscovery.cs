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
        private static readonly Dictionary<string, ContextScope> RegistryMappings = new()
        {
            { @"HKEY_CLASSES_ROOT\*\shell", ContextScope.File },
            { @"HKEY_CLASSES_ROOT\Directory\shell", ContextScope.Folder },
            { @"HKEY_CLASSES_ROOT\Directory\Background\shell", ContextScope.Background },
            { @"HKEY_CLASSES_ROOT\AllFilesystemObjects\shell", ContextScope.All },
            // Additional ShellEx context menu handlers
            { @"HKEY_CLASSES_ROOT\*\ShellEx\ContextMenuHandlers", ContextScope.File },
            { @"HKEY_CLASSES_ROOT\Directory\ShellEx\ContextMenuHandlers", ContextScope.Folder },
            { @"HKEY_CLASSES_ROOT\Directory\Background\ShellEx\ContextMenuHandlers", ContextScope.Background },
        };

        public static List<ExistingContextMenuItem> DiscoverExistingItems()
        {
            var items = new List<ExistingContextMenuItem>();

            try
            {
                // Scan registry for legacy context menu items (both 32-bit and 64-bit views)
                items.AddRange(ScanRegistryContextItems(RegistryView.Default));
                items.AddRange(ScanRegistryContextItems(RegistryView.Registry32));
                items.AddRange(ScanRegistryContextItems(RegistryView.Registry64));
                
                // Parse existing Shell config for modifications
                items.AddRange(ParseExistingShellConfig());
                
                // Add known problematic items that users commonly want to remove
                items.AddRange(GetCommonClutterItems());
            }
            catch (Exception ex)
            {
                Logger.LogError("Error discovering context menu items", ex);
            }

            return items.DistinctBy(i => $"{i.Title}|{i.Command}|{i.RegistryLocation}").ToList();
        }

        private static List<ExistingContextMenuItem> ScanRegistryContextItems(RegistryView view)
        {
            var items = new List<ExistingContextMenuItem>();

            foreach (var (registryPath, scope) in RegistryMappings)
            {
                try
                {
                    items.AddRange(ScanRegistryPath(registryPath, scope, view));
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"ContextMenuDiscovery: Could not scan registry path {registryPath} (view: {view}): {ex.Message}");
                }
            }

            return items;
        }

        private static List<ExistingContextMenuItem> ScanRegistryPath(string registryPath, ContextScope scope, RegistryView view)
        {
            var items = new List<ExistingContextMenuItem>();
            
            try
            {
                var pathParts = registryPath.Split('\\');
                var hiveKey = pathParts[0] switch
                {
                    "HKEY_CLASSES_ROOT" => RegistryKey.OpenBaseKey(RegistryHive.ClassesRoot, view),
                    "HKEY_CURRENT_USER" => RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, view),
                    "HKEY_LOCAL_MACHINE" => RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view),
                    _ => null
                };

                if (hiveKey == null) return items;

                using (hiveKey)
                {
                    var subKeyPath = string.Join("\\", pathParts.Skip(1));
                    
                    using var key = hiveKey.OpenSubKey(subKeyPath, false);
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
                                RegistryLocation = $"{registryPath}\\{subKeyName} ({view})",
                                DetectedScope = scope,
                                IsLegacyItem = true
                            };

                            // Handle ShellEx handlers differently from shell commands
                            if (registryPath.Contains("ShellEx"))
                            {
                                // This is a COM handler, get the CLSID and try to resolve friendly name
                                var clsid = subKey.GetValue("")?.ToString();
                                if (!string.IsNullOrEmpty(clsid))
                                {
                                    item.Command = clsid;
                                    item.SourceApp = ResolveComHandlerName(clsid);
                                }
                            }
                            else
                            {
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
                            }

                            // Get icon if available
                            var icon = subKey.GetValue("Icon")?.ToString();
                            if (!string.IsNullOrEmpty(icon))
                            {
                                item.Icon = icon;
                            }

                            // Only add items with actual commands or CLSID
                            if (!string.IsNullOrEmpty(item.Command))
                            {
                                items.Add(item);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogWarning($"ContextMenuDiscovery: Error reading registry subkey {subKeyName}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"ContextMenuDiscovery: Error scanning registry path {registryPath}: {ex.Message}");
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
            catch (Exception ex)
            {
                Logger.LogWarning($"ContextMenuDiscovery: Error detecting source app from command '{command}': {ex.Message}");
                return "Unknown";
            }
        }

        private static string ResolveComHandlerName(string clsid)
        {
            try
            {
                // Try to resolve CLSID to friendly name
                using var clsidKey = Registry.ClassesRoot.OpenSubKey($"CLSID\\{clsid}");
                if (clsidKey != null)
                {
                    var friendlyName = clsidKey.GetValue("")?.ToString();
                    if (!string.IsNullOrEmpty(friendlyName))
                    {
                        return friendlyName;
                    }
                }

                return $"COM Handler ({clsid[..8]}...)";
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"ContextMenuDiscovery: Error resolving COM handler {clsid}: {ex.Message}");
                return "Unknown COM Handler";
            }
        }

        private static List<ExistingContextMenuItem> ParseExistingShellConfig()
        {
            var items = new List<ExistingContextMenuItem>();

            try
            {
                if (!ShellManager.IsShellInstalled())
                {
                    return items;
                }

                var configPath = ShellManager.GetShellConfigPath();
                if (!File.Exists(configPath))
                {
                    return items;
                }

                // Parse existing Shell config to see what's already modified
                var configContent = File.ReadAllText(configPath);
                // Simple parsing - look for PowerToys-generated content
                if (configContent.Contains("// Generated by PowerToys ContextMenuEdit"))
                {
                    Logger.LogInfo("ContextMenuDiscovery: Found existing PowerToys Shell configuration");
                    // For now, just log that we have existing config
                    // Full NSS parser would be implemented in a future version
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"ContextMenuDiscovery: Could not parse existing Shell config: {ex.Message}");
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
                new() { Title = "Share", SourceApp = "Windows", DetectedScope = ContextScope.File },
                new() { Title = "Cast to Device", SourceApp = "Windows", DetectedScope = ContextScope.File },
            };
        }
    }
}
