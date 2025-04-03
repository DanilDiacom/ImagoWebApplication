param($installPath, $toolsPath, $package, $project)

# Удаляем ярлыки
Remove-Item "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\Imago Admin.lnk" -ErrorAction SilentlyContinue

# Удаляем запись из реестра
if (Test-Path "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\ImagoAdmin") {
    Remove-Item -Path "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\ImagoAdmin" -Recurse -Force
}