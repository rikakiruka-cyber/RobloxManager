@echo off
chcp 65001 >nul
title Build RobloxManager
color 0B
cd /d "%~dp0"

where dotnet >nul 2>&1
if errorlevel 1 (
    echo [ERROR] .NET SDK 10 not found / Chua cai .NET SDK 10: https://dotnet.microsoft.com/download
    if /i not "%~1"=="nopause" pause
    exit /b 1
)

set VER=
for /f "delims=" %%v in ('powershell -NoProfile -Command "([xml](Get-Content 'App\RobloxRAMManager.csproj')).Project.PropertyGroup.Version | Select-Object -First 1"') do set VER=%%v
if "%VER%"=="" set VER=0.0.0
set OUTNAME=RobloxManager_%VER%_x64

echo ============================================
echo   BUILD %OUTNAME%.exe  (single file / 1 file)
echo ============================================
echo.
if exist "Release_EXE" rmdir /s /q "Release_EXE"

echo Building... first run may take 1-3 min / Dang build... lan dau co the mat 1-3 phut
dotnet publish App\RobloxRAMManager.csproj -c Release -r win-x64 --self-contained true ^
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:EnableCompressionInSingleFile=true -p:SatelliteResourceLanguages=en ^
    -p:DebugType=none -p:DebugSymbols=false ^
    -o Release_EXE
if errorlevel 1 (
    echo.
    echo [ERROR] Build failed - send a screenshot of the "error" lines / Build that bai - gui anh cac dong "error"
    if /i not "%~1"=="nopause" pause
    exit /b 1
)

ren "Release_EXE\RobloxRAMManager.exe" "%OUTNAME%.exe"
if exist "build\NativeEngine.dll" copy /Y "build\NativeEngine.dll" "Release_EXE\" >nul

echo.
echo ============================================
echo   DONE / XONG:  Release_EXE\%OUTNAME%.exe
echo ============================================
if /i not "%~1"=="nopause" (
    explorer "Release_EXE"
    pause
)
