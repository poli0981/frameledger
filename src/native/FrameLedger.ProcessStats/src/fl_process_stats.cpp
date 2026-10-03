// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

// fl_process_stats.cpp — see fl_process_stats.h.
//
// EVERY CALL HERE IS A QUERY ABOUT A PROCESS, NEVER A READ OF IT. The performance counters are the graphics kernel's
// own per-process accounting, published system-wide; GetProcessMemoryInfo is the memory manager's counters for a
// process handle that carries PROCESS_QUERY_LIMITED_INFORMATION and nothing else. No PROCESS_VM_READ, no
// ReadProcessMemory, no module walk: what is in the game's memory is not this file's business (CLAUDE.md rule 4).
//
// It runs in the Agent's own process on a telemetry thread, so it may allocate — the counter arrays grow with the
// number of processes using a GPU — but it uses the process heap rather than the STL, because the native targets build
// without C++ exceptions (src/native/CMakeLists.txt strips /EHsc) and a throwing container has nowhere to throw to.
#define FL_PS_BUILDING
#include "fl_process_stats.h"

#include <windows.h>

#include <cstddef>
#include <cstring>
#include <cwchar>
#include <pdh.h>
#include <pdhmsg.h>
#include <psapi.h>

namespace {

// English names: the counter set is localised on a Japanese or Vietnamese Windows, and PdhAddEnglishCounterW is the
// one call that does not care (the adapter-wide counter in PdhAdapterMemoryCounter.cs does the same).
constexpr wchar_t kDedicatedPath[] = L"\\GPU Process Memory(*)\\Dedicated Usage";
constexpr wchar_t kSharedPath[] = L"\\GPU Process Memory(*)\\Shared Usage";

// PROCESS_MEMORY_COUNTERS_EX2 as Microsoft documents it (psapi.h, "Windows 10 22H2 with the September 2023
// cumulative update or Windows 11 22H2 with the September 2023 cumulative update"), declared here so the build does not
// depend on the SDK's NTDDI guard around it. The asserts tie it to the structure it extends.
struct ProcessMemoryCountersEx2 {
    DWORD   cb;
    DWORD   PageFaultCount;
    SIZE_T  PeakWorkingSetSize;
    SIZE_T  WorkingSetSize;
    SIZE_T  QuotaPeakPagedPoolUsage;
    SIZE_T  QuotaPagedPoolUsage;
    SIZE_T  QuotaPeakNonPagedPoolUsage;
    SIZE_T  QuotaNonPagedPoolUsage;
    SIZE_T  PagefileUsage;
    SIZE_T  PeakPagefileUsage;
    SIZE_T  PrivateUsage;
    SIZE_T  PrivateWorkingSetSize;
    ULONG64 SharedCommitUsage;
};
static_assert(offsetof(ProcessMemoryCountersEx2, PrivateUsage) == offsetof(PROCESS_MEMORY_COUNTERS_EX, PrivateUsage));
static_assert(sizeof(ProcessMemoryCountersEx2) == sizeof(PROCESS_MEMORY_COUNTERS_EX) + 2 * sizeof(ULONG64));

// One instance array, read after each collection and kept until the next.
struct InstanceArray {
    BYTE* buffer;
    DWORD capacity;
    DWORD count;
};

struct Reader {
    PDH_HQUERY    query;
    PDH_HCOUNTER  dedicated;
    PDH_HCOUNTER  shared;           // null when only the shared counter could not be added
    PDH_STATUS    openStatus;       // nonzero: the query or the dedicated counter could not be set up
    PDH_STATUS    collectStatus;    // the last collection's status; PDH_NO_DATA before the first
    bool          collected;        // the arrays below describe the last collection
    InstanceArray dedicatedItems;
    InstanceArray sharedItems;
};

void FreeArray(InstanceArray& a) {
    if (a.buffer != nullptr) {
        HeapFree(GetProcessHeap(), 0, a.buffer);
    }
    a = InstanceArray{};
}

// PdhGetFormattedCounterArrayW into a buffer that grows to fit. Three tries: a process can start using the GPU between
// the size query and the read, and PDH answers PDH_MORE_DATA again.
PDH_STATUS FetchArray(PDH_HCOUNTER counter, InstanceArray& into) {
    into.count = 0;
    for (int attempt = 0; attempt < 3; ++attempt) {
        DWORD      size = into.capacity;
        DWORD      items = 0;
        PDH_STATUS s = PdhGetFormattedCounterArrayW(counter, PDH_FMT_LARGE | PDH_FMT_NOCAP100, &size, &items,
                                                    reinterpret_cast<PPDH_FMT_COUNTERVALUE_ITEM_W>(into.buffer));
        if (s == ERROR_SUCCESS) {
            into.count = items;
            return s;
        }
        if (s != static_cast<PDH_STATUS>(PDH_MORE_DATA) || size <= into.capacity) {
            return s;
        }
        BYTE* grown = static_cast<BYTE*>(HeapAlloc(GetProcessHeap(), 0, size));
        if (grown == nullptr) {
            return static_cast<PDH_STATUS>(PDH_MEMORY_ALLOCATION_FAILURE);
        }
        FreeArray(into);
        into.buffer = grown;
        into.capacity = size;
    }
    return static_cast<PDH_STATUS>(PDH_MORE_DATA);
}

// "pid_12345_luid_0x00000000_0x0000D1A3_phys_0" names process 12345. Anything else is not a process instance.
bool InstancePid(const wchar_t* name, uint32_t* pid) {
    if (name == nullptr || std::wcsncmp(name, L"pid_", 4) != 0) {
        return false;
    }
    uint64_t       value = 0;
    int            digits = 0;
    const wchar_t* p = name + 4;
    while (*p >= L'0' && *p <= L'9') {
        value = value * 10 + static_cast<uint64_t>(*p - L'0');
        if (value > 0xFFFFFFFFull) {
            return false;
        }
        ++p;
        ++digits;
    }
    if (digits == 0 || *p != L'_') {
        return false;
    }
    *pid = static_cast<uint32_t>(value);
    return true;
}

// The sum over every instance that names `pid`; false when none does.
bool SumFor(const InstanceArray& a, uint32_t pid, uint64_t* bytes, uint32_t* instances) {
    *bytes = 0;
    *instances = 0;
    const auto* items = reinterpret_cast<const PDH_FMT_COUNTERVALUE_ITEM_W*>(a.buffer);
    for (DWORD i = 0; i < a.count; ++i) {
        uint32_t named = 0;
        if (!InstancePid(items[i].szName, &named) || named != pid) {
            continue;
        }
        const DWORD status = items[i].FmtValue.CStatus;
        if (status != PDH_CSTATUS_VALID_DATA && status != PDH_CSTATUS_NEW_DATA) {
            continue;
        }
        if (items[i].FmtValue.largeValue > 0) {
            *bytes += static_cast<uint64_t>(items[i].FmtValue.largeValue);
        }
        ++*instances;
    }
    return *instances > 0;
}

uint64_t FileTimeValue(const FILETIME& t) {
    return (static_cast<uint64_t>(t.dwHighDateTime) << 32) | t.dwLowDateTime;
}

void ReadSystemMemory(uint32_t pid, uint64_t creationTime, HANDLE held, FlPsSample* out) {
    HANDLE process = held;
    if (process == nullptr) {
        // The least right that answers: the one the watcher's own snapshot uses on every process it lists.
        process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, pid);
        if (process == nullptr) {
            out->ramStatus = FL_PS_RAM_OPEN_DENIED;
            out->ramError = GetLastError();
            return;
        }
    }

    if (creationTime != 0) {
        FILETIME created{}, exited{}, kernel{}, user{};
        if (!GetProcessTimes(process, &created, &exited, &kernel, &user) || FileTimeValue(created) != creationTime) {
            out->ramStatus = FL_PS_RAM_PID_REUSED;
            out->ramError = GetLastError();
            if (held == nullptr) {
                CloseHandle(process);
            }
            return;
        }
    }

    ProcessMemoryCountersEx2 ex2{};
    ex2.cb = sizeof(ex2);
    // A sentinel in the one field EX2 adds that we read: an OS that accepts the larger size but fills only the EX part
    // leaves it as written. One that cleared the buffer first is caught by the second test — a live process always has
    // some private working set, so zero beside a nonzero working set is not a reading. Either way it is N/A.
    constexpr SIZE_T kNotFilled = static_cast<SIZE_T>(-1);
    ex2.PrivateWorkingSetSize = kNotFilled;
    if (GetProcessMemoryInfo(process, reinterpret_cast<PROCESS_MEMORY_COUNTERS*>(&ex2), sizeof(ex2))) {
        out->workingSetBytes = ex2.WorkingSetSize;
        out->peakWorkingSetBytes = ex2.PeakWorkingSetSize;
        out->commitBytes = ex2.PrivateUsage;
        out->present |= FL_PS_FIELD_WORKING_SET | FL_PS_FIELD_COMMIT;
        if (ex2.PrivateWorkingSetSize != kNotFilled && (ex2.PrivateWorkingSetSize > 0 || ex2.WorkingSetSize == 0)) {
            out->privateWorkingSetBytes = ex2.PrivateWorkingSetSize;
            out->present |= FL_PS_FIELD_PRIVATE_WS;
        }
    } else {
        PROCESS_MEMORY_COUNTERS_EX ex{};
        ex.cb = sizeof(ex);
        if (GetProcessMemoryInfo(process, reinterpret_cast<PROCESS_MEMORY_COUNTERS*>(&ex), sizeof(ex))) {
            out->workingSetBytes = ex.WorkingSetSize;
            out->peakWorkingSetBytes = ex.PeakWorkingSetSize;
            out->commitBytes = ex.PrivateUsage;
            out->present |= FL_PS_FIELD_WORKING_SET | FL_PS_FIELD_COMMIT;
        } else {
            out->ramStatus = FL_PS_RAM_QUERY_FAILED;
            out->ramError = GetLastError();
        }
    }

    if (held == nullptr) {
        CloseHandle(process);
    }
}

void ReadVideoMemory(const Reader& r, uint32_t pid, FlPsSample* out) {
    if (r.openStatus != ERROR_SUCCESS) {
        out->gpuStatus = FL_PS_GPU_NO_COUNTERS;
        out->pdhStatus = static_cast<uint32_t>(r.openStatus);
        return;
    }
    if (!r.collected) {
        out->gpuStatus = FL_PS_GPU_NOT_COLLECTED;
        out->pdhStatus = static_cast<uint32_t>(r.collectStatus);
        return;
    }

    uint64_t dedicated = 0;
    uint32_t instances = 0;
    if (!SumFor(r.dedicatedItems, pid, &dedicated, &instances)) {
        out->gpuStatus = FL_PS_GPU_NO_INSTANCE;
        return;
    }
    out->gpuDedicatedBytes = dedicated;
    out->gpuInstances = instances;
    out->present |= FL_PS_FIELD_GPU_DEDICATED;

    uint64_t shared = 0;
    uint32_t sharedInstances = 0;
    if (r.shared != nullptr && SumFor(r.sharedItems, pid, &shared, &sharedInstances)) {
        out->gpuSharedBytes = shared;
        out->present |= FL_PS_FIELD_GPU_SHARED;
    }
}

}    // namespace

extern "C" {

FL_PS_API void* FlPsOpen(void) {
    auto* r = static_cast<Reader*>(HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, sizeof(Reader)));
    if (r == nullptr) {
        return nullptr;
    }
    r->collectStatus = static_cast<PDH_STATUS>(PDH_NO_DATA);

    r->openStatus = PdhOpenQueryW(nullptr, 0, &r->query);
    if (r->openStatus == ERROR_SUCCESS) {
        r->openStatus = PdhAddEnglishCounterW(r->query, kDedicatedPath, 0, &r->dedicated);
    }
    if (r->openStatus == ERROR_SUCCESS &&
        PdhAddEnglishCounterW(r->query, kSharedPath, 0, &r->shared) != ERROR_SUCCESS) {
        r->shared = nullptr;    // the dedicated figure stands on its own; shared reads N/A
    }
    return r;
}

FL_PS_API void FlPsClose(void* reader) {
    auto* r = static_cast<Reader*>(reader);
    if (r == nullptr) {
        return;
    }
    if (r->query != nullptr) {
        PdhCloseQuery(r->query);
    }
    FreeArray(r->dedicatedItems);
    FreeArray(r->sharedItems);
    HeapFree(GetProcessHeap(), 0, r);
}

FL_PS_API int32_t FlPsCollect(void* reader) {
    auto* r = static_cast<Reader*>(reader);
    if (r == nullptr) {
        return FL_PS_BAD_ARGUMENT;
    }
    if (r->openStatus != ERROR_SUCCESS) {
        return static_cast<int32_t>(r->openStatus);
    }

    r->collected = false;
    r->collectStatus = PdhCollectQueryData(r->query);
    if (r->collectStatus != ERROR_SUCCESS) {
        return static_cast<int32_t>(r->collectStatus);
    }
    r->collectStatus = FetchArray(r->dedicated, r->dedicatedItems);
    if (r->collectStatus != ERROR_SUCCESS) {
        return static_cast<int32_t>(r->collectStatus);
    }
    if (r->shared != nullptr && FetchArray(r->shared, r->sharedItems) != ERROR_SUCCESS) {
        r->sharedItems.count = 0;    // shared reads N/A for this collection; dedicated stands
    }
    r->collected = true;
    return FL_PS_OK;
}

FL_PS_API int32_t FlPsRead(void* reader, uint32_t pid, uint64_t creationTime, void* process, FlPsSample* out) {
    if (out == nullptr) {
        return FL_PS_BAD_ARGUMENT;
    }
    if (out->size != sizeof(FlPsSample)) {
        return FL_PS_BAD_SIZE;
    }
    auto* r = static_cast<Reader*>(reader);
    if (r == nullptr || pid == 0) {
        return FL_PS_BAD_ARGUMENT;
    }

    std::memset(out, 0, sizeof(FlPsSample));
    out->size = sizeof(FlPsSample);
    ReadVideoMemory(*r, pid, out);
    ReadSystemMemory(pid, creationTime, static_cast<HANDLE>(process), out);
    return FL_PS_OK;
}

FL_PS_API uint32_t FlPsAbiVersion(void) {
    return FL_PS_ABI_VERSION;
}

FL_PS_API uint32_t FlPsSampleSize(void) {
    return sizeof(FlPsSample);
}

FL_PS_API const char* FlPsBuildId(void) {
    return FL_BUILD_ID;
}

}    // extern "C"
