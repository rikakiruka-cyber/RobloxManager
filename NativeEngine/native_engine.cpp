// Roblox RAM Manager — Copyright (c) 2026 RikaSoora — https://discord.com/users/1374334753242873908
// Licensed under the terms in LICENSE. Keep this notice. / Giữ nguyên dòng ghi công này.

/*
 * NativeEngine.cpp - Roblox RAM Manager Native Engine
 * Direct Win32 API calls for process monitoring.
 * Optimized: no heap allocations in hot path, fixed-size tracking arrays.
 *
 * Build: cl /LD /O2 /MT /EHsc /DNATIVEENGINE_EXPORTS native_engine.cpp /link psapi.lib user32.lib
 */

#define NATIVEENGINE_EXPORTS
#include "native_engine.h"
#include <psapi.h>
#include <tlhelp32.h>
#include <string.h>

#pragma comment(lib, "psapi.lib")
#pragma comment(lib, "user32.lib")

/* ── Helpers ── */

static inline ULONGLONG FT2ULL(const FILETIME* ft) {
    return ((ULONGLONG)ft->dwHighDateTime << 32) | ft->dwLowDateTime;
}

/* ── Per-process CPU tracking (fixed array, no heap) ── */

#define MAX_TRACKED 64

static struct CpuTrackEntry {
    DWORD     pid;
    ULONGLONG prevKernel;
    ULONGLONG prevUser;
    ULONGLONG prevWall;
    double    lastCpu;
} g_cpu[MAX_TRACKED];
static int g_cpuCount = 0;

/* System-wide CPU tracking */
static ULONGLONG g_sysIdlePrev = 0, g_sysKernelPrev = 0, g_sysUserPrev = 0;

/* Logical processor count, cached once (used to normalize per-process CPU%) */
static DWORD g_numProcessors = 0;
static DWORD GetLogicalProcessorCount() {
    if (g_numProcessors == 0) {
        SYSTEM_INFO si;
        GetSystemInfo(&si);
        g_numProcessors = (si.dwNumberOfProcessors > 0) ? si.dwNumberOfProcessors : 1;
    }
    return g_numProcessors;
}

static int FindOrAddTrack(DWORD pid) {
    for (int i = 0; i < g_cpuCount; i++)
        if (g_cpu[i].pid == pid) return i;
    if (g_cpuCount >= MAX_TRACKED) return -1;
    int idx = g_cpuCount++;
    memset(&g_cpu[idx], 0, sizeof(CpuTrackEntry));
    g_cpu[idx].pid = pid;
    return idx;
}

static void PruneStaleEntries(const DWORD* activePids, int activeCount) {
    for (int i = g_cpuCount - 1; i >= 0; i--) {
        BOOL found = FALSE;
        for (int j = 0; j < activeCount; j++) {
            if (activePids[j] == g_cpu[i].pid) { found = TRUE; break; }
        }
        if (!found)
            g_cpu[i] = g_cpu[--g_cpuCount];
    }
}

/* ── Window enumeration callback ── */

struct WndFindCtx {
    DWORD pid;
    HWND  hwnd;
    WCHAR title[256];
};

static BOOL CALLBACK FindWindowCB(HWND hwnd, LPARAM lp) {
    WndFindCtx* ctx = (WndFindCtx*)lp;
    DWORD wndPid = 0;
    GetWindowThreadProcessId(hwnd, &wndPid);
    if (wndPid != ctx->pid || !IsWindowVisible(hwnd))
        return TRUE;
    ctx->hwnd = hwnd;
    GetWindowTextW(hwnd, ctx->title, 256);
    if (ctx->title[0] != L'\0')
        return FALSE;   // found a titled window, stop
    return TRUE;
}

static HWND FindMainWindow(DWORD pid, WCHAR* titleOut, int titleMax) {
    WndFindCtx ctx;
    ctx.pid = pid;
    ctx.hwnd = NULL;
    ctx.title[0] = L'\0';
    EnumWindows(FindWindowCB, (LPARAM)&ctx);
    if (titleOut && titleMax > 0)
        wcsncpy_s(titleOut, titleMax, ctx.title, _TRUNCATE);
    return ctx.hwnd;
}

/* ── Exported: System Resources ── */

NE_API void __cdecl NE_GetSystemResources(NativeSystemInfo* info) {
    if (!info) return;
    memset(info, 0, sizeof(NativeSystemInfo));

    /* Memory */
    MEMORYSTATUSEX ms;
    ms.dwLength = sizeof(ms);
    if (GlobalMemoryStatusEx(&ms)) {
        info->TotalPhysicalBytes     = ms.ullTotalPhys;
        info->AvailablePhysicalBytes = ms.ullAvailPhys;
        info->MemoryLoadPercent      = ms.dwMemoryLoad;
    }

    /* Processor count */
    SYSTEM_INFO si;
    GetSystemInfo(&si);
    info->ProcessorCount = si.dwNumberOfProcessors;

    /* CPU % via GetSystemTimes */
    FILETIME idleFT, kernelFT, userFT;
    if (GetSystemTimes(&idleFT, &kernelFT, &userFT)) {
        ULONGLONG idle   = FT2ULL(&idleFT);
        ULONGLONG kernel = FT2ULL(&kernelFT);
        ULONGLONG user   = FT2ULL(&userFT);

        if (g_sysKernelPrev != 0) {
            ULONGLONG dIdle   = idle   - g_sysIdlePrev;
            ULONGLONG dKernel = kernel - g_sysKernelPrev;
            ULONGLONG dUser   = user   - g_sysUserPrev;
            ULONGLONG dTotal  = dKernel + dUser;
            if (dTotal > 0)
                info->TotalCpuPercent = (double)(dTotal - dIdle) * 100.0 / dTotal;
        }
        g_sysIdlePrev   = idle;
        g_sysKernelPrev = kernel;
        g_sysUserPrev   = user;
    }
}

/* ── Exported: Enumerate Roblox Processes ── */

NE_API int __cdecl NE_EnumRobloxProcesses(NativeProcessInfo* buffer, int maxCount) {
    if (!buffer || maxCount <= 0) return 0;

    HANDLE snap = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
    if (snap == INVALID_HANDLE_VALUE) return 0;

    FILETIME nowFT;
    GetSystemTimeAsFileTime(&nowFT);
    ULONGLONG now = FT2ULL(&nowFT);

    int count = 0;
    DWORD activePids[MAX_TRACKED];

    PROCESSENTRY32W pe;
    pe.dwSize = sizeof(pe);

    if (Process32FirstW(snap, &pe)) {
        do {
            if (_wcsicmp(pe.szExeFile, L"RobloxPlayerBeta.exe") != 0)
                continue;
            if (count >= maxCount) break;

            NativeProcessInfo* p = &buffer[count];
            memset(p, 0, sizeof(NativeProcessInfo));
            p->ProcessId = pe.th32ProcessID;
            activePids[count] = pe.th32ProcessID;

            HANDLE hProc = OpenProcess(
                PROCESS_QUERY_INFORMATION | PROCESS_VM_READ,
                FALSE, pe.th32ProcessID);

            if (hProc) {
                /* Memory */
                PROCESS_MEMORY_COUNTERS pmc;
                pmc.cb = sizeof(pmc);
                if (GetProcessMemoryInfo(hProc, &pmc, sizeof(pmc))) {
                    p->WorkingSetBytes     = pmc.WorkingSetSize;
                    p->PeakWorkingSetBytes = pmc.PeakWorkingSetSize;
                    p->PrivateBytes        = pmc.PagefileUsage;
                }

                /* Priority */
                p->PriorityClass = GetPriorityClass(hProc);

                /* Per-process CPU */
                FILETIME createFT, exitFT, kernelFT, userFT;
                if (GetProcessTimes(hProc, &createFT, &exitFT, &kernelFT, &userFT)) {
                    ULONGLONG kernel = FT2ULL(&kernelFT);
                    ULONGLONG user   = FT2ULL(&userFT);
                    int idx = FindOrAddTrack(pe.th32ProcessID);
                    if (idx >= 0) {
                        if (g_cpu[idx].prevWall != 0) {
                            ULONGLONG dK = kernel - g_cpu[idx].prevKernel;
                            ULONGLONG dU = user   - g_cpu[idx].prevUser;
                            ULONGLONG dW = now    - g_cpu[idx].prevWall;
                            /* Normalize by logical core count so a single-core-pegged
                               process reads ~ (100/cores)%, matching Task Manager,
                               instead of up to (100 * cores)% on multi-core systems. */
                            if (dW > 0) {
                                double raw = (double)(dK + dU) * 100.0 / dW;
                                raw /= (double)GetLogicalProcessorCount();
                                if (raw < 0) raw = 0;
                                if (raw > 100.0) raw = 100.0;
                                p->CpuPercent = raw;
                            }
                        }
                        g_cpu[idx].prevKernel = kernel;
                        g_cpu[idx].prevUser   = user;
                        g_cpu[idx].prevWall   = now;
                        g_cpu[idx].lastCpu    = p->CpuPercent;
                    }
                }

                CloseHandle(hProc);
            }

            /* Window title */
            HWND hwnd = FindMainWindow(pe.th32ProcessID, p->WindowTitle, 256);

            /* Responding check */
            if (hwnd) {
                DWORD_PTR result;
                LRESULT lr = SendMessageTimeoutW(
                    hwnd, WM_NULL, 0, 0,
                    SMTO_ABORTIFHUNG, 800, &result);
                p->Responding = (lr != 0) ? TRUE : FALSE;
            } else {
                p->Responding = TRUE; // no window yet = still loading
            }

            count++;
        } while (Process32NextW(snap, &pe));
    }

    CloseHandle(snap);
    PruneStaleEntries(activePids, count);
    return count;
}

/* ── Exported: Set Priority ── */

NE_API BOOL __cdecl NE_SetProcessPriority(DWORD pid, DWORD priorityClass) {
    HANDLE h = OpenProcess(PROCESS_SET_INFORMATION, FALSE, pid);
    if (!h) return FALSE;
    BOOL ok = SetPriorityClass(h, priorityClass);
    CloseHandle(h);
    return ok;
}

/* ── Exported: Trim Working Set ── */

NE_API BOOL __cdecl NE_TrimProcessMemory(DWORD pid) {
    HANDLE h = OpenProcess(PROCESS_SET_QUOTA | PROCESS_QUERY_INFORMATION, FALSE, pid);
    if (!h) return FALSE;
    BOOL ok = SetProcessWorkingSetSize(h, (SIZE_T)-1, (SIZE_T)-1);
    CloseHandle(h);
    return ok;
}

/* ── Exported: Check Responding ── */

NE_API BOOL __cdecl NE_IsProcessResponding(DWORD pid) {
    WCHAR title[256];
    HWND hwnd = FindMainWindow(pid, title, 256);
    if (!hwnd) return TRUE;
    DWORD_PTR result;
    LRESULT lr = SendMessageTimeoutW(
        hwnd, WM_NULL, 0, 0,
        SMTO_ABORTIFHUNG, 1500, &result);
    return (lr != 0) ? TRUE : FALSE;
}

/* ── DLL Entry ── */

BOOL APIENTRY DllMain(HMODULE hModule, DWORD reason, LPVOID reserved) {
    if (reason == DLL_PROCESS_ATTACH)
        DisableThreadLibraryCalls(hModule);
    return TRUE;
}
