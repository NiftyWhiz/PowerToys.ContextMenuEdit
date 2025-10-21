using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public enum ContextScope
    {
        All,
        File,
        Folder,
        Background
    }

    public class ContextMenuAction : INotifyPropertyChanged
    {
        private string _id = Guid.NewGuid().ToString("N")[..8];
        private string _title = "";
        private string _command = "";
        private string _arguments = "";
        private string _icon = "";
        private string _workingDirectory = "";
        private ContextScope _scope = ContextScope.All;
        private string[] _fileTypes = Array.Empty<string>();
        private bool _requiresAdmin = false;
        private bool _extendedOnly = false;
        private bool _enabled = true;

        public string Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public string Command
        {
            get => _command;
            set => SetProperty(ref _command, value);
        }

        public string Arguments
        {
            get => _arguments;
            set => SetProperty(ref _arguments, value);
        }

        public string Icon
        {
            get => _icon;
            set => SetProperty(ref _icon, value);
        }

        public string WorkingDirectory
        {
            get => _workingDirectory;
            set => SetProperty(ref _workingDirectory, value);
        }

        public ContextScope Scope
        {
            get => _scope;
            set => SetProperty(ref _scope, value);
        }

        public string[] FileTypes
        {
            get => _fileTypes;
            set => SetProperty(ref _fileTypes, value);
        }

        public bool RequiresAdmin
        {
            get => _requiresAdmin;
            set => SetProperty(ref _requiresAdmin, value);
        }

        public bool ExtendedOnly
        {
            get => _extendedOnly;
            set => SetProperty(ref _extendedOnly, value);
        }

        public bool Enabled
        {
            get => _enabled;
            set => SetProperty(ref _enabled, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void SetProperty<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string propertyName = "")
        {
            if (!EqualityComparer<T>.Default.Equals(field, value))
            {
                field = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }

    public class ContextMenuModification : INotifyPropertyChanged
    {
        private string _targetTitle = "";
        private string? _targetCommand = null;
        private string? _newTitle = null;
        private string? _newIcon = null;
        private string? _newCommand = null;
        private string? _newArguments = null;
        private bool? _hide = null;
        private bool _enabled = true;

        public string TargetTitle
        {
            get => _targetTitle;
            set => SetProperty(ref _targetTitle, value);
        }

        public string? TargetCommand
        {
            get => _targetCommand;
            set => SetProperty(ref _targetCommand, value);
        }

        public string? NewTitle
        {
            get => _newTitle;
            set => SetProperty(ref _newTitle, value);
        }

        public string? NewIcon
        {
            get => _newIcon;
            set => SetProperty(ref _newIcon, value);
        }

        public string? NewCommand
        {
            get => _newCommand;
            set => SetProperty(ref _newCommand, value);
        }

        public string? NewArguments
        {
            get => _newArguments;
            set => SetProperty(ref _newArguments, value);
        }

        public bool? Hide
        {
            get => _hide;
            set => SetProperty(ref _hide, value);
        }

        public bool Enabled
        {
            get => _enabled;
            set => SetProperty(ref _enabled, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void SetProperty<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string propertyName = "")
        {
            if (!EqualityComparer<T>.Default.Equals(field, value))
            {
                field = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }

    public class ContextMenuRemoval : INotifyPropertyChanged
    {
        private string _targetTitle = "";
        private string? _targetCommand = null;
        private bool _enabled = true;

        public string TargetTitle
        {
            get => _targetTitle;
            set => SetProperty(ref _targetTitle, value);
        }

        public string? TargetCommand
        {
            get => _targetCommand;
            set => SetProperty(ref _targetCommand, value);
        }

        public bool Enabled
        {
            get => _enabled;
            set => SetProperty(ref _enabled, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void SetProperty<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string propertyName = "")
        {
            if (!EqualityComparer<T>.Default.Equals(field, value))
            {
                field = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }

    public class ContextMenuEditSettings : BasePTModuleSettings, INotifyPropertyChanged
    {
        public const string ModuleName = "ContextMenuEdit";

        private bool _enabled = true;
        private string _shellInstallPath = "";
        private bool _autoInstallShell = true;
        private bool _autoBackupConfigs = true;
        private bool _showNotifications = true;
        private List<ContextMenuAction> _newActions = new();
        private List<ContextMenuModification> _modifications = new();
        private List<ContextMenuRemoval> _removals = new();

        public ContextMenuEditSettings()
        {
            Name = ModuleName;
            Version = "1.0.0";
            
            // Initialize with some default actions
            InitializeDefaults();
        }

        public bool Enabled
        {
            get => _enabled;
            set => SetProperty(ref _enabled, value);
        }

        public string ShellInstallPath
        {
            get => _shellInstallPath;
            set => SetProperty(ref _shellInstallPath, value);
        }

        public bool AutoInstallShell
        {
            get => _autoInstallShell;
            set => SetProperty(ref _autoInstallShell, value);
        }

        public bool AutoBackupConfigs
        {
            get => _autoBackupConfigs;
            set => SetProperty(ref _autoBackupConfigs, value);
        }

        public bool ShowNotifications
        {
            get => _showNotifications;
            set => SetProperty(ref _showNotifications, value);
        }

        public List<ContextMenuAction> NewActions
        {
            get => _newActions;
            set => SetProperty(ref _newActions, value);
        }

        public List<ContextMenuModification> Modifications
        {
            get => _modifications;
            set => SetProperty(ref _modifications, value);
        }

        public List<ContextMenuRemoval> Removals
        {
            get => _removals;
            set => SetProperty(ref _removals, value);
        }

        private void InitializeDefaults()
        {
            NewActions = new List<ContextMenuAction>
            {
                new ContextMenuAction
                {
                    Title = "PowerShell Here",
                    Command = "powershell.exe",
                    Arguments = "-NoExit -Command \"Set-Location '%cd%'\"",
                    Icon = "%SystemRoot%\\System32\\WindowsPowerShell\\v1.0\\powershell.exe",
                    Scope = ContextScope.Background,
                    Enabled = true
                },
                new ContextMenuAction
                {
                    Title = "Open with VS Code",
                    Command = "%ProgramFiles%\\Microsoft VS Code\\Code.exe",
                    Arguments = "\"%1\"",
                    Icon = "%ProgramFiles%\\Microsoft VS Code\\Code.exe",
                    Scope = ContextScope.All,
                    FileTypes = new[] { ".txt", ".md", ".json", ".cs", ".js", ".ts", ".py", ".cpp", ".h" },
                    Enabled = true
                },
                new ContextMenuAction
                {
                    Title = "Copy Path",
                    Command = "powershell.exe",
                    Arguments = "-WindowStyle Hidden -Command \"Set-Clipboard '%1'\"",
                    Icon = "shell32.dll,134",
                    Scope = ContextScope.All,
                    Enabled = true
                }
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void SetProperty<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string propertyName = "")
        {
            if (!EqualityComparer<T>.Default.Equals(field, value))
            {
                field = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public override string GetModuleName() => ModuleName;

        public string ToJsonString()
        {
            return System.Text.Json.JsonSerializer.Serialize(this, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
        }
    }
}
