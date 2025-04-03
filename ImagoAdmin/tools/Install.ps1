param($installPath, $toolsPath, $package, $project)

# Создаем ярлыки
$exePath = Join-Path $installPath "YourApp.exe"
$iconPath = Join-Path $installPath "favicon.ico"

# Ярлык в меню Пуск
Install-ChocolateyShortcut `
    -ShortcutFilePath "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\Imago Admin.lnk" `
    -TargetPath $exePath `
    -IconLocation $iconPath

# Запись в реестр для "Программы и компоненты"
$registryPath = "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\ImagoAdmin"
if (-not (Test-Path $registryPath)) {
    New-Item -Path $registryPath -Force | Out-Null
}

New-ItemProperty -Path $registryPath -Name "DisplayName" -Value "Imago Admin" -PropertyType String -Force
New-ItemProperty -Path $registryPath -Name "UninstallString" -Value "${env:LocalAppData}\ImagoAdmin\Update.exe --uninstall" -PropertyType String -Force
New-ItemProperty -Path $registryPath -Name "DisplayIcon" -Value $iconPath -PropertyType String -Force
New-ItemProperty -Path $registryPath -Name "Publisher" -Value "ImagoDT s.r.o" -PropertyType String -Force
New-ItemProperty -Path $registryPath -Name "InstallLocation" -Value $installPath -PropertyType String -Force