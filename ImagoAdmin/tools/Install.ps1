# tools/Install.ps1
param($installPath, $toolsPath, $package, $project)

# Создаем ярлыки
$desktop = [System.Environment]::GetFolderPath('Desktop')
$startMenu = [System.Environment]::GetFolderPath('StartMenu')
$target = Join-Path $installPath "ImagoAdmin.exe"

$ws = New-Object -ComObject WScript.Shell
$shortcut = $ws.CreateShortcut("$desktop\ImagoAdmin.lnk")
$shortcut.TargetPath = $target
$shortcut.Save()

$shortcut = $ws.CreateShortcut("$startMenu\Programs\ImagoAdmin.lnk")
$shortcut.TargetPath = $target
$shortcut.Save()