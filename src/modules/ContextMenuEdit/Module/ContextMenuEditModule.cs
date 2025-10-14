using Microsoft.PowerToys.Settings.UI.Library;

namespace Microsoft.PowerToys.Settings.UI
{
    public class ContextMenuEditModule
    {
        public string Name => "ContextMenuEdit";
        public string Version => "0.1.0";
        public void Enable() { /* noop: shell ext reads settings */ }
        public void Disable() { /* noop */ }
        public object GetSettingsPage() => new Views.ContextMenuEditPage();
    }
}