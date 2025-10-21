using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.PowerToys.Settings.UI.Library;
using ManagedCommon;

namespace Microsoft.PowerToys.Settings.UI.ContextMenuEdit.Core
{
    public static class ShellConfigGenerator
    {
        private const string ConfigHeader = @"// PowerToys Context Menu Edit Configuration
// Generated automatically - do not edit manually
// Changes will be overwritten when PowerToys settings are updated
// Visit https://nilesoft.org/docs for advanced Shell configuration

";

        public static string GenerateConfig(ContextMenuEditSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            var config = new StringBuilder(ConfigHeader);
            
            // Import user's existing config if it exists (conditional import)
            config.AppendLine("// Import user's custom configuration (if exists)");
            config.AppendLine("import(file='user-custom.nss' exists)");
            config.AppendLine();

            // Generate removals first
            var enabledRemovals = settings.Removals.Where(r => r.Enabled).ToList();
            if (enabledRemovals.Any())
            {
                GenerateRemovals(config, enabledRemovals);
            }

            // Generate modifications
            var enabledModifications = settings.Modifications.Where(m => m.Enabled).ToList();
            if (enabledModifications.Any())
            {
                GenerateModifications(config, enabledModifications);
            }

            // Generate new items
            var enabledActions = settings.NewActions.Where(a => a.Enabled && !string.IsNullOrEmpty(a.Title) && !string.IsNullOrEmpty(a.Command)).ToList();
            if (enabledActions.Any())
            {
                GenerateNewItems(config, enabledActions);
            }

            return config.ToString();
        }

        private static void GenerateRemovals(StringBuilder config, List<ContextMenuRemoval> removals)
        {
            config.AppendLine("// Remove unwanted context menu items");
            config.AppendLine("modify");
            config.AppendLine("{");

            foreach (var removal in removals)
            {
                config.AppendLine($"    // Remove: {EscapeComment(removal.TargetTitle)}");
                
                if (!string.IsNullOrEmpty(removal.TargetCommand))
                {
                    config.AppendLine($"    remove(find=title.{EscapeString(removal.TargetTitle)} and cmd.{EscapeString(removal.TargetCommand)})");
                }
                else
                {
                    config.AppendLine($"    remove(find=title.{EscapeString(removal.TargetTitle)})");
                }
            }

            config.AppendLine("}");
            config.AppendLine();
        }

        private static void GenerateModifications(StringBuilder config, List<ContextMenuModification> modifications)
        {
            config.AppendLine("// Modify existing context menu items");
            config.AppendLine("modify");
            config.AppendLine("{");

            foreach (var mod in modifications)
            {
                config.AppendLine($"    // Modify: {EscapeComment(mod.TargetTitle)}");
                config.AppendLine($"    item(find=title.{EscapeString(mod.TargetTitle)}");

                if (!string.IsNullOrEmpty(mod.TargetCommand))
                {
                    config.AppendLine($"         and cmd.{EscapeString(mod.TargetCommand)}");
                }

                config.AppendLine("    {");

                if (!string.IsNullOrEmpty(mod.NewTitle))
                {
                    config.AppendLine($"        title = {EscapeString(mod.NewTitle)}");
                }

                if (!string.IsNullOrEmpty(mod.NewIcon))
                {
                    config.AppendLine($"        image = {EscapeString(ExpandEnvironmentPath(mod.NewIcon))}");
                }

                if (!string.IsNullOrEmpty(mod.NewCommand))
                {
                    config.AppendLine($"        cmd = {EscapeString(ExpandEnvironmentPath(mod.NewCommand))}");
                }

                if (!string.IsNullOrEmpty(mod.NewArguments))
                {
                    config.AppendLine($"        args = {EscapeString(mod.NewArguments)}");
                }

                if (mod.Hide == true)
                {
                    config.AppendLine("        vis = false");
                }
                else if (mod.Hide == false)
                {
                    config.AppendLine("        vis = true");
                }

                config.AppendLine("    })");
            }

            config.AppendLine("}");
            config.AppendLine();
        }

        private static void GenerateNewItems(StringBuilder config, List<ContextMenuAction> actions)
        {
            config.AppendLine("// PowerToys custom context menu items");

            // Group by scope for better organization
            var scopeGroups = new Dictionary<ContextScope, (string Name, string Selector)>
            {
                { ContextScope.All, ("All contexts", "mode.extended") },
                { ContextScope.File, ("Files only", "mode.file") },
                { ContextScope.Folder, ("Folders only", "mode.directory") },
                { ContextScope.Background, ("Background only", "mode.back") }
            };

            foreach (var (scope, (scopeName, selector)) in scopeGroups)
            {
                var scopeActions = actions.Where(a => a.Scope == scope).ToList();
                if (scopeActions.Any())
                {
                    GenerateItemsForScope(config, scopeName, scopeActions, selector);
                }
            }
        }

        private static void GenerateItemsForScope(StringBuilder config, string scopeName, List<ContextMenuAction> actions, string modeSelector)
        {
            config.AppendLine($"// {scopeName}");
            config.AppendLine($"item(where={modeSelector})");
            config.AppendLine("{");

            foreach (var action in actions)
            {
                config.AppendLine($"    // {EscapeComment(action.Title)}");
                config.AppendLine($"    item(title={EscapeString(action.Title)}");
                config.AppendLine($"         cmd={EscapeString(ExpandEnvironmentPath(action.Command))}");

                if (!string.IsNullOrEmpty(action.Arguments))
                {
                    config.AppendLine($"         args={EscapeString(action.Arguments)}");
                }

                if (!string.IsNullOrEmpty(action.Icon))
                {
                    config.AppendLine($"         image={EscapeString(ExpandEnvironmentPath(action.Icon))}");
                }

                if (!string.IsNullOrEmpty(action.WorkingDirectory))
                {
                    config.AppendLine($"         directory={EscapeString(ExpandEnvironmentPath(action.WorkingDirectory))}");
                }

                if (action.RequiresAdmin)
                {
                    config.AppendLine("         admin=true");
                }

                if (action.ExtendedOnly)
                {
                    config.AppendLine("         keys=shift");
                }

                // File type filtering for file-scoped items
                if (action.FileTypes.Any())
                {
                    var extensions = string.Join("|", action.FileTypes.Select(ext => ext.StartsWith(".") ? ext : "." + ext));
                    config.AppendLine($"         type={EscapeString(extensions)}");
                }

                config.AppendLine("    )");
            }

            config.AppendLine("}");
            config.AppendLine();
        }

        private static string EscapeString(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "''";

            // Shell uses single quotes, escape both quotes and backslashes
            return $"'{input.Replace("\\", "\\\\").Replace("'", "\\'")}'";
        }

        private static string EscapeComment(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "";

            // Remove problematic characters from comments
            return input.Replace("*/", "").Replace("/*", "").Replace("\r", "").Replace("\n", " ");
        }

        private static string ExpandEnvironmentPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            try
            {
                return Environment.ExpandEnvironmentVariables(path);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"ShellManager: Failed to expand environment variables in path: {path} - {ex.Message}");
                return path;
            }
        }

        public static string GeneratePreviewConfig(ContextMenuEditSettings settings)
        {
            // Dry-run mode for troubleshooting
            return GenerateConfig(settings);
        }
    }
}
