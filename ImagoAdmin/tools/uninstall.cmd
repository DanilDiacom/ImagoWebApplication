@echo off
set UNINSTALL_PATH=%LOCALAPPDATA%\ImagoAdmin\Update.exe
if exist "%UNINSTALL_PATH%" (
  "%UNINSTALL_PATH%" --uninstall
)