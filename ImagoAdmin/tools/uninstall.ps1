# tools/uninstall.ps1
$uninstallPath = Join-Path $env:LOCALAPPDATA "ImagoAdmin\Update.exe"
if (Test-Path $uninstallPath) {
  Start-Process $uninstallPath "-uninstall" -Wait
}