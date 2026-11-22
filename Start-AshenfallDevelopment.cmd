@echo off
rem Double-click this file to verify/start the local API and open the Godot editor.
rem The PowerShell helper uses private Development configuration; no secrets are here.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0backend\Start-AshenfallDevelopment.ps1" %*
if errorlevel 1 pause
