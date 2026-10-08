# Сборка выпуска ImagoAdmin: публикация программы + установщик MSI.
#
# Перед запуском поднимите версию в ImagoAdmin\ImagoAdmin.csproj (<Version> и <AssemblyVersion>).
# Запуск из корня решения:   powershell -ExecutionPolicy Bypass -File .\build-admin-release.ps1
#
# Результат: ImagoInstaller\bin\x64\Release\ImagoAdminInstaller.msi — его прикрепить к выпуску (Release) на GitHub
# с тегом v<версия>, например v1.0.3. Программа у клиента найдёт выпуск сама.

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

$publish = "ImagoAdmin\bin\Release\publish"
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }

Write-Host "1/2 Publikace ImagoAdmin..." -ForegroundColor Cyan
dotnet publish ImagoAdmin\ImagoAdmin.csproj -c Release -r win-x64 --self-contained false -o $publish -nologo -v q
if ($LASTEXITCODE -ne 0) { throw "Publikace ImagoAdmin selhala" }

Write-Host "2/2 Instalátor MSI..." -ForegroundColor Cyan
if (Test-Path "ImagoInstaller\bin") { Remove-Item "ImagoInstaller\bin" -Recurse -Force }
if (Test-Path "ImagoInstaller\obj") { Remove-Item "ImagoInstaller\obj" -Recurse -Force }
dotnet build ImagoInstaller\ImagoInstaller.wixproj -c Release -nologo -v q
if ($LASTEXITCODE -ne 0) { throw "Sestavení instalátoru selhalo" }

$msi = Get-Item "ImagoInstaller\bin\x64\Release\ImagoAdminInstaller.msi"
$version = (Get-Item "$publish\ImagoAdmin.exe").VersionInfo.FileVersion
$hash = (Get-FileHash $msi.FullName -Algorithm SHA256).Hash

Write-Host ""
Write-Host "Hotovo:" -ForegroundColor Green
Write-Host "  Verze:   $version   (tag pro GitHub: v$($version -replace '\.0$', ''))"
Write-Host "  MSI:     $($msi.FullName)"
Write-Host ("  Velikost: {0:N1} MB" -f ($msi.Length / 1MB))
Write-Host "  SHA256:  $hash"
