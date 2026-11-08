@echo off
:: Optional: build the C++ NativeEngine.dll (app falls back to managed code without it).
:: Tuy chon: build NativeEngine.dll bang C++ (khong co thi app tu dung ban .NET).
:: Run from "x64 Native Tools Command Prompt for VS" / Chay trong "x64 Native Tools Command Prompt for VS".
cd /d "%~dp0.."
where cl >nul 2>&1 || (echo [SKIP] cl.exe not found / khong tim thay cl.exe & pause & exit /b 1)
if not exist "build" mkdir build
cl /LD /O2 /MT /EHsc /W3 /D_UNICODE /DUNICODE /DWIN32 /DNATIVEENGINE_EXPORTS NativeEngine\native_engine.cpp /Fe:build\NativeEngine.dll /Fo:build\ /link /MACHINE:X64 psapi.lib kernel32.lib user32.lib
if exist "build\NativeEngine.dll" (echo [OK] build\NativeEngine.dll) else (echo [FAIL] build failed / build that bai)
pause
