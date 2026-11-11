@echo off
chcp 65001 >nul
title Push update to GitHub / Day ban cap nhat len GitHub
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "tools\release.ps1"
echo.
pause
