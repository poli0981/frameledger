// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

// The call sequence Reflex route D would put into the Overlay (docs/spike-notes §30, §31; owner decision D132) --
// HERE, IN A PROBE, AND NOWHERE THAT SHIPS. Nothing loaded into a game includes this file. If the owner says go after
// the spike, this is what moves; until then it exists to be measured.
//
// WHAT IT DOES: finds NVIDIA's nvapi64.dll in the process IF IT IS ALREADY LOADED -- it never loads it -- asks its one
// export for the two getters by their interface ids, and calls them with a Direct3D device. Getters only: no marker,
// no sleep mode, no sleep, nothing that changes what the driver does (rule 4's note of 2026-11-13).
//
// NOT LINKED AGAINST nvapi64.lib. The static library loads the DLL itself, from a trusted location and with a
// signature check, on its first call; route D's whole shape is that the game brought the module and FrameLedger adds
// nothing to the process. The ids are the vendored header's (nvapi_interface.h), the structs nvapi.h's.

#ifndef FRAMELEDGER_FL_REFLEX_CANDIDATE_H
#define FRAMELEDGER_FL_REFLEX_CANDIDATE_H

#include <windows.h>

#include <d3d11.h>

#include <cstdint>
#include <cstring>
#include <nvapi.h>

namespace fl::reflex {

inline constexpr unsigned int kIdGetSleepStatus = 0xaef96ca1u;    // nvapi_interface.h: NvAPI_D3D_GetSleepStatus
inline constexpr unsigned int kIdGetLatency = 0x1a587f9cu;        // nvapi_interface.h: NvAPI_D3D_GetLatency

using QueryInterfaceFn = void*(__cdecl*)(unsigned int);
using GetLatencyFn = NvAPI_Status(__cdecl*)(IUnknown*, NV_LATENCY_RESULT_PARAMS*);
using GetSleepStatusFn = NvAPI_Status(__cdecl*)(IUnknown*, NV_GET_SLEEP_STATUS_PARAMS*);

struct Api {
    GetLatencyFn     getLatency = nullptr;
    GetSleepStatusFn getSleepStatus = nullptr;
};

enum class Resolved : std::uint32_t {
    Ok = 0,
    NoModule = 1,            // the process has not loaded nvapi64.dll: nothing was called and nothing was loaded
    NoQueryInterface = 2,    // a module of that name without the export
    NoGetter = 3,            // the module does not have one of the two getters
};

// Finds the two getters in a module the process ALREADY holds. Takes a reference on it (as the Overlay's
// ResolveScoped does on a module it patches) so the addresses stay valid; never loads a module.
inline Resolved Resolve(Api& api) noexcept {
    api = Api{};
    HMODULE module = nullptr;
    if (!GetModuleHandleExW(0, L"nvapi64.dll", &module) || module == nullptr) {
        return Resolved::NoModule;
    }
    const auto query = reinterpret_cast<QueryInterfaceFn>(GetProcAddress(module, "nvapi_QueryInterface"));
    if (query == nullptr) {
        return Resolved::NoQueryInterface;
    }
    api.getLatency = reinterpret_cast<GetLatencyFn>(query(kIdGetLatency));
    api.getSleepStatus = reinterpret_cast<GetSleepStatusFn>(query(kIdGetSleepStatus));
    if (api.getLatency == nullptr || api.getSleepStatus == nullptr) {
        api = Api{};
        return Resolved::NoGetter;
    }
    return Resolved::Ok;
}

// What a read returns when the driver's code faulted under it: not one of NVAPI's own codes.
inline constexpr NvAPI_Status kFaulted = static_cast<NvAPI_Status>(-9999);

// One read of the driver's report. SEH-guarded as every call the Overlay makes into foreign code is; a guard does not
// bound a call that never returns, which is why the probe measures that separately.
inline NvAPI_Status ReadLatency(const Api& api, IUnknown* device, NV_LATENCY_RESULT_PARAMS& out) noexcept {
    std::memset(&out, 0, sizeof(out));
    out.version = NV_LATENCY_RESULT_PARAMS_VER1;
    if (api.getLatency == nullptr || device == nullptr) {
        return NVAPI_INVALID_ARGUMENT;
    }
    __try {
        return api.getLatency(device, &out);
    } __except (EXCEPTION_EXECUTE_HANDLER) {
        return kFaulted;
    }
}

inline NvAPI_Status ReadSleepStatus(const Api& api, IUnknown* device, NV_GET_SLEEP_STATUS_PARAMS& out) noexcept {
    std::memset(&out, 0, sizeof(out));
    out.version = NV_GET_SLEEP_STATUS_PARAMS_VER1;
    if (api.getSleepStatus == nullptr || device == nullptr) {
        return NVAPI_INVALID_ARGUMENT;
    }
    __try {
        return api.getSleepStatus(device, &out);
    } __except (EXCEPTION_EXECUTE_HANDLER) {
        return kFaulted;
    }
}

// What a report holds, in numbers: how many of its sixty-four frames carry anything, and the newest one's id and
// two spans in the report's own unit -- simulation start to present end (the game's side) and to the end of GPU work.
struct Digest {
    std::uint32_t valid = 0;
    std::uint64_t newestFrameId = 0;
    std::uint64_t newestSimToPresentEnd = 0;
    std::uint64_t newestSimToGpuEnd = 0;
    std::uint32_t newestGpuActiveUs = 0;
};

inline Digest Summarise(const NV_LATENCY_RESULT_PARAMS& report) noexcept {
    Digest d{};
    for (int i = 0; i < 64; ++i) {
        const auto& r = report.frameReport[i];
        if (r.frameID == 0u && r.simStartTime == 0u && r.gpuRenderEndTime == 0u) {
            continue;
        }
        ++d.valid;
        d.newestFrameId = r.frameID;
        d.newestSimToPresentEnd = r.presentEndTime >= r.simStartTime ? r.presentEndTime - r.simStartTime : 0u;
        d.newestSimToGpuEnd = r.gpuRenderEndTime >= r.simStartTime ? r.gpuRenderEndTime - r.simStartTime : 0u;
        d.newestGpuActiveUs = r.gpuActiveRenderTimeUs;
    }
    return d;
}

}    // namespace fl::reflex

#endif    // FRAMELEDGER_FL_REFLEX_CANDIDATE_H
