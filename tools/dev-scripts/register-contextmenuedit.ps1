$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$Repo = Resolve-Path "$Root\..\.."
$Dll = Resolve-Path "$Repo\x64\Release\ContextMenuEdit.dll"
$Guid = '{E5B37D79-4DDA-4A78-B2C9-7B1E1FB1E4A4}'

# Minimal COM registration for dev (sparse package covers identity)
reg add "HKCU\Software\Classes\CLSID\$Guid\InprocServer32" /ve /t REG_SZ /d "$Dll" /f | Out-Null
reg add "HKCU\Software\Classes\CLSID\$Guid\InprocServer32" /v ThreadingModel /t REG_SZ /d "Apartment" /f | Out-Null

# Register sparse package
$Man = Resolve-Path "$Repo\installer\sparse\ContextMenuEdit\AppxManifest.xml"
Add-AppxPackage -Register "$Man" -ForceApplicationShutdown
Write-Host "Registered ContextMenuEdit shell extension + sparse package. Restart Explorer if needed."