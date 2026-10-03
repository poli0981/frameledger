// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

// fl_process_stats.h — the C ABI the managed Agent reads A GAME PROCESS'S OWN MEMORY through (beta.12, owner
// decision D43; docs/18_GPU_VENDOR_APIS.md §L1, docs/03_METRICS.md §Per-process memory).
//
// WHAT THIS IS. Two documented Windows sources, read from OUTSIDE the game:
//   - video memory: the performance counters `\GPU Process Memory(pid_<pid>_luid_<luid>_phys_<n>)\Dedicated Usage`
//     and `\Shared Usage` — the numbers Task Manager's Details tab shows as "Dedicated GPU memory" and "Shared GPU
//     memory". A counter query opens NO handle to the game at all.
//   - system memory: GetProcessMemoryInfo with PROCESS_MEMORY_COUNTERS_EX2 (its PrivateWorkingSetSize is Task
//     Manager's "Memory" column), falling back to PROCESS_MEMORY_COUNTERS_EX where the OS predates it. It needs a
//     handle with PROCESS_QUERY_LIMITED_INFORMATION and nothing more — the caller's held handle when it has one, else
//     one this call opens and closes before it returns.
// Neither reads the game's memory: both are the kernel's bookkeeping ABOUT the process (CLAUDE.md rule 4 untouched).
// D3DKMTQueryStatistics is deliberately not used: Microsoft documents it as "Reserved for system use".
//
// NEVER LOADED INTO A GAME. The Agent loads it beside its own binary by absolute path (the third facade after the
// guard's and the NVAPI bridge's, Infrastructure.Native.BesideThisAssembly). Its name carries none of the words the
// guard's §S18 heuristic scans for (guard / hook / inject / cheat / protect).
//
// VERSIONED STRUCTS, as fl_nvapi_bridge.h: the out-struct carries its own size, set by the caller and checked here.
#pragma once

#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

#ifdef FL_PS_BUILDING
#define FL_PS_API __declspec(dllexport)
#else
#define FL_PS_API __declspec(dllimport)
#endif

#define FL_PS_ABI_VERSION 1u

// Return codes. Zero is success; negatives are ours.
#define FL_PS_OK 0
#define FL_PS_BAD_SIZE (-1001)
#define FL_PS_BAD_ARGUMENT (-1003)

// Which fields of FlPsSample carry a value. A clear bit is N/A, never 0 (03_METRICS §Sensor aggregates).
#define FL_PS_FIELD_WORKING_SET 0x01u      // workingSetBytes and peakWorkingSetBytes
#define FL_PS_FIELD_PRIVATE_WS 0x02u       // privateWorkingSetBytes (PROCESS_MEMORY_COUNTERS_EX2 only)
#define FL_PS_FIELD_COMMIT 0x04u           // commitBytes (PrivateUsage, the process's commit charge)
#define FL_PS_FIELD_GPU_DEDICATED 0x08u    // gpuDedicatedBytes
#define FL_PS_FIELD_GPU_SHARED 0x10u       // gpuSharedBytes

// Why the system-memory half is missing.
#define FL_PS_RAM_OK 0
#define FL_PS_RAM_OPEN_DENIED 1     // OpenProcess refused: gone, protected, another user's — ramError says which
#define FL_PS_RAM_PID_REUSED 2      // the pid names a process created at another time: not the one asked about
#define FL_PS_RAM_QUERY_FAILED 3    // GetProcessMemoryInfo refused both structure sizes

// Why the video-memory half is missing.
#define FL_PS_GPU_OK 0
#define FL_PS_GPU_NO_COUNTERS 1      // the GPU Process Memory counter set could not be opened on this machine
#define FL_PS_GPU_NO_INSTANCE 2      // no counter instance names this pid (it has made no GPU allocation yet)
#define FL_PS_GPU_NOT_COLLECTED 3    // FlPsCollect has not succeeded on this reader — pdhStatus says why

typedef struct FlPsSample {
    uint32_t size;       // sizeof(FlPsSample), set by the caller
    uint32_t present;    // FL_PS_FIELD_* bits
    uint64_t privateWorkingSetBytes;
    uint64_t workingSetBytes;
    uint64_t peakWorkingSetBytes;
    uint64_t commitBytes;
    uint64_t gpuDedicatedBytes;    // summed over every instance naming the pid (every adapter, every physical index)
    uint64_t gpuSharedBytes;
    uint32_t gpuInstances;    // how many counter instances were summed
    int32_t  ramStatus;       // FL_PS_RAM_*
    uint32_t ramError;        // GetLastError behind ramStatus (0 on success)
    int32_t  gpuStatus;       // FL_PS_GPU_*
    uint32_t pdhStatus;       // the PDH status behind gpuStatus (0 on success)
    uint32_t reserved[3];
} FlPsSample;

// A reader: one PDH query over the two GPU Process Memory counters, plus the buffer their instance arrays are read
// into. Null only when memory is exhausted; a machine without the counter set still gets a reader, whose GPU half
// reports FL_PS_GPU_NO_COUNTERS. One reader per thread — the Agent has one per session's telemetry thread.
FL_PS_API void* FlPsOpen(void);
FL_PS_API void  FlPsClose(void* reader);

// One counter collection; FlPsRead answers from the latest. Returns 0 or the PDH status that refused it.
FL_PS_API int32_t FlPsCollect(void* reader);

// The memory of process `pid`: the video half from the reader's latest collection, the system half through
// `process` when the caller holds a handle to it (PROCESS_QUERY_LIMITED_INFORMATION is enough), else through a handle
// opened here with exactly that right and closed before returning. `creationTime` is the process's creation time as a
// FILETIME value (0 = do not check); a different one means the pid was reused, and the system half is refused rather
// than read off a stranger. Each half is independent: a refused one leaves its bits clear and says why.
FL_PS_API int32_t FlPsRead(void* reader, uint32_t pid, uint64_t creationTime, void* process, FlPsSample* out);

// What the managed mirror test compares against, and what a build id check reads.
FL_PS_API uint32_t    FlPsAbiVersion(void);
FL_PS_API uint32_t    FlPsSampleSize(void);
FL_PS_API const char* FlPsBuildId(void);

#ifdef __cplusplus
}
#endif
