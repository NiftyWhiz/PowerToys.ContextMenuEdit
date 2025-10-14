$Guid = '{E5B37D79-4DDA-4A78-B2C9-7B1E1FB1E4A4}'
reg delete "HKCU\Software\Classes\CLSID\$Guid" /f | Out-Null
Get-AppxPackage -Name 'NiftyWhiz.PowerToys.ContextMenuEdit' | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue
Write-Host "Unregistered ContextMenuEdit and removed sparse package."