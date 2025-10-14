using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.PowerToys.Settings.UI.Library;
using System.IO;
using System.Text.Json;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class ContextMenuEditPage : Page
    {
        private const string SettingsPath = "%LocalAppData%/Microsoft/PowerToys/ContextMenuEdit/settings.json";
        private ContextMenuEditSettings _settings = new();

        public ContextMenuEditPage()
        {
            this.InitializeComponent();
            LoadSettings();
            BindUi();
        }

        private void LoadSettings()
        {
            try
            {
                var path = Environment.ExpandEnvironmentVariables(SettingsPath);
                if (File.Exists(path))
                {
                    _settings = JsonSerializer.Deserialize<ContextMenuEditSettings>(File.ReadAllText(path)) ?? new();
                }
            }
            catch { /* TODO: log */ }
        }

        private void SaveSettings()
        {
            try
            {
                var path = Environment.ExpandEnvironmentVariables(SettingsPath);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch { /* TODO: log */ }
        }

        private void BindUi()
        {
            EnabledToggle.IsOn = _settings.Enabled;
            CascadeToggle.IsOn = _settings.UseCascade;
            ActionsList.ItemsSource = _settings.Actions;

            EnabledToggle.Toggled += (_, __) => { _settings.Enabled = EnabledToggle.IsOn; SaveSettings(); };
            CascadeToggle.Toggled += (_, __) => { _settings.UseCascade = CascadeToggle.IsOn; SaveSettings(); };
            // TODO: implement Add/Remove/Edit dialogs
        }
    }
}