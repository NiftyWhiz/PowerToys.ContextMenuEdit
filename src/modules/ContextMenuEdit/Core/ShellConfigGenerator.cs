using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.PowerToys.Settings.UI.Library;
using ManagedCommon;

namespace Microsoft.PowerToys.Settings.UI.ContextMenuEdit.Core
{
    public class ShellConfigGenerator
    {
        private const string ConfigHeader = @"// PowerToys Context Menu Edit Configuration
// Generated automatically - do not edit manually
// Changes will be overwritten when PowerToys settings are updated
// Visit https://nilesoft.org/docs for advanced Shell configuration

";

        public string GenerateConfig(ContextMenuEditSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            var config = new StringBuilder(ConfigHeader);
            
            // Import user's existing config if it exists
            config.AppendLine("// Import user's custom configuration");
            config.AppendLine("import 'user-custom.nss'");
            config.AppendLine();

            // Generate removals first
            if (settings.Removals.Any(r => r.Enabled))
            {
                GenerateRemovals(config, settings.Removals.Where(r => r.Enabled));
            }

            // Generate modifications
            if (settings.Modifications.Any(m => m.Enabled))
            {
                GenerateModifications(config, settings.Modifications.Where(m => m.Enabled));
            }

            // Generate new items
            if (settings.NewActions.Any(a => a.Enabled))
            {
                GenerateNewItems(config, settings.NewActions.Where(a => a.Enabled));
            }

            return config.ToString();
        }

        private void GenerateRemovals(StringBuilder config, IEnumerable<ContextMenuRemoval> removals)
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

        private void GenerateModifications(StringBuilder config, IEnumerable<ContextMenuModification> modifications)
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

        private void GenerateNewItems(StringBuilder config, IEnumerable<ContextMenuAction> actions)
        {
            config.AppendLine("// PowerToys custom context menu items");

            // Group by scope for better organization
            var fileActions = actions.Where(a => a.Scope == ContextScope.File).ToList();
            var folderActions = actions.Where(a => a.Scope == ContextScope.Folder).ToList();
            var backgroundActions = actions.Where(a => a.Scope == ContextScope.Background).ToList();
            var allActions = actions.Where(a => a.Scope == ContextScope.All).ToList();

            if (allActions.Any())
            {
                GenerateItemsForScope(config, "All contexts", allActions, "mode.extended");
            }

            if (fileActions.Any())
            {
                GenerateItemsForScope(config, "Files only", fileActions, "mode.file");
            }

            if (folderActions.Any())
            {
                GenerateItemsForScope(config, "Folders only", folderActions, "mode.directory");
            }

            if (backgroundActions.Any())
            {
                GenerateItemsForScope(config, "Background only", backgroundActions, "mode.back");
            }
        }

        private void GenerateItemsForScope(StringBuilder config, string scopeName, List<ContextMenuAction> actions, string modeFilter)
        {
            config.AppendLine($"// {scopeName}");
            config.AppendLine($"item(where={modeFilter})");
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

        private string EscapeString(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "''";

            // Shell uses single quotes, escape internal quotes
            return $"'{input.Replace("'", "\\'")}'";
        }

        private string EscapeComment(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "";

            // Remove problematic characters from comments
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
            catch (Exception ex)
            {
                Logger.LogError($"Failed to expand environment variables in path: {path}", ex);
                return path;
            }
        }
    }
}
