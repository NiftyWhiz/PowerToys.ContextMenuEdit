using System.Collections.Generic;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class ContextMenuEditProperties
    {
        public BoolProperty Enabled { get; set; }
        public StringProperty ShellInstallPath { get; set; }
        public BoolProperty AutoInstallShell { get; set; }
        public BoolProperty AutoBackupConfigs { get; set; }
        public BoolProperty ShowNotifications { get; set; }

        public ContextMenuEditProperties()
        {
            Enabled = new BoolProperty(true);
            ShellInstallPath = new StringProperty("");
            AutoInstallShell = new BoolProperty(true);
            AutoBackupConfigs = new BoolProperty(true);
            ShowNotifications = new BoolProperty(true);
        }
    }
}
