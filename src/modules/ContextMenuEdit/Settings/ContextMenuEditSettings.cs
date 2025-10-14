using System.Collections.Generic;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class ContextMenuEditAction
    {
        public string Id { get; set; } = "";
        public string Label { get; set; } = "";
        public string? IconPath { get; set; }
        public string Scope { get; set; } = "*"; // "*", "file", "folder", "background"
        public string? FileTypes { get; set; } // e.g., ".txt;.md"
        public bool ExtendedOnly { get; set; } // show only on Shift
        public bool RequiresElevation { get; set; }
        public string Command { get; set; } = ""; // exe or script
        public string Arguments { get; set; } = "%1"; // supports %1 (first), %V (all), %W (cwd)
        public string? WorkingDirectory { get; set; }
    }

    public class ContextMenuEditSettings
    {
        public bool Enabled { get; set; } = true;
        public bool UseCascade { get; set; } = true; // show submenu vs. flat
        public List<ContextMenuEditAction> Actions { get; set; } = new();
    }
}