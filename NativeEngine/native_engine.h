// Roblox RAM Manager — Copyright (c) 2026 RikaSoora — https://discord.com/users/1374334753242873908
// Licensed under the terms in LICENSE. Keep this notice. / Giữ nguyên dòng ghi công này.

#ifndef NATIVE_ENGINE_H
#define NATIVE_ENGINE_H

/*
 * NativeEngine.dll - Roblox RAM Manager Native Engine
 * Low-overhead Windows process monitoring via Win32 API.
 * Optimized for low-end hardware (i3 / 8GB DDR3 / Intel HD 4400).
 */

#include <windows.h>

#ifdef NATIVEENGINE_EXPORTS
#define NE_API __declspec(dllexport)
#else
#define NE_API __declspec(dllimport)
#endif

/* ── Shared structs (must match C# StructLayout Pack=1) ── */

#pragma pack(push, 1)

typedef struct {
    DWORD  ProcessId;
    WCHAR  WindowTitle[256];
    SIZE_T WorkingSetBytes;
    SIZE_T PeakWorkingSetBytes;
    SIZE_T PrivateBytes;
    double CpuPercent;
    DWORD  PriorityClass;
    BOOL   Responding;
} NativeProcessInfo;

typedef struct {
    ULONGLONG TotalPhysicalBytes;
    ULONGLONG AvailablePhysicalBytes;
    DWORD     MemoryLoadPercent;
    double    TotalCpuPercent;
    DWORD     ProcessorCount;
} NativeSystemInfo;

#pragma pack(pop)

/* ── Exported functions ── */

#ifdef __cplusplus
extern "C" {
#endif

NE_API void __cdecl NE_GetSystemResources(NativeSystemInfo* info);
NE_API int  __cdecl NE_EnumRobloxProcesses(NativeProcessInfo* buffer, int maxCount);
NE_API BOOL __cdecl NE_SetProcessPriority(DWORD pid, DWORD priorityClass);
NE_API BOOL __cdecl NE_TrimProcessMemory(DWORD pid);
NE_API BOOL __cdecl NE_IsProcessResponding(DWORD pid);

#ifdef __cplusplus
}
#endif

#endif /* NATIVE_ENGINE_H */
