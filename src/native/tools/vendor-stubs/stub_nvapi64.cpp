// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

// A stand-in for NVIDIA's nvapi64.dll, for the spike of Reflex route D (docs/spike-notes §31, owner decision D132).
//
// NOT A VENDOR LIBRARY AND NOT SHIPPED. It carries the one name the real module's callers resolve everything through
// -- nvapi_QueryInterface -- and answers two interface ids, the two getters route D would call:
// NvAPI_D3D_GetLatency and NvAPI_D3D_GetSleepStatus. The ids and the structs are the vendored MIT headers' own
// (nvapi_interface.h, nvapi.h), so the probe and this stub cannot disagree about either.
//
// WHAT IT IS FOR: the things a real driver cannot be made to do on demand. A getter that is not there, a report of
// nothing, an error, and a call that does not come back for seconds -- so that the probe can show each is harmless
// before anything is tried against a driver, and long before anything is tried in a game.

#include <windows.h>

#include <d3d11.h>

#include <atomic>
#include <cstdint>
#include <cstring>
#include <nvapi.h>

namespace {

constexpr unsigned int kIdInitialize = 0x0150e828u;
constexpr unsigned int kIdGetSleepStatus = 0xaef96ca1u;
constexpr unsigned int kIdGetLatency = 0x1a587f9cu;

// 0: sixty-four reports. 1: a report of nothing (what the header says a game without markers gets). 2: an error.
std::atomic<std::uint32_t> g_mode{0};
std::atomic<std::uint32_t> g_blockMs{0};
std::atomic<std::uint32_t> g_hideGetters{0};
std::atomic<std::uint32_t> g_queries{0};
std::atomic<std::uint32_t> g_latencyCalls{0};
std::atomic<std::uint32_t> g_sleepStatusCalls{0};
std::atomic<void*>         g_lastDevice{nullptr};

NvAPI_Status __cdecl StubInitialize() {
    return NVAPI_OK;
}

NvAPI_Status __cdecl StubGetLatency(IUnknown* device, NV_LATENCY_RESULT_PARAMS* out) {
    g_latencyCalls.fetch_add(1u, std::memory_order_relaxed);
    g_lastDevice.store(device, std::memory_order_relaxed);
    if (out == nullptr) {
        return NVAPI_INVALID_ARGUMENT;
    }
    if (out->version != NV_LATENCY_RESULT_PARAMS_VER1) {
        return NVAPI_INCOMPATIBLE_STRUCT_VERSION;
    }
    const std::uint32_t block = g_blockMs.load(std::memory_order_relaxed);
    if (block != 0u) {
        Sleep(block);
    }
    const std::uint32_t mode = g_mode.load(std::memory_order_relaxed);
    if (mode == 2u) {
        return NVAPI_ERROR;
    }
    if (mode == 1u) {
        return NVAPI_OK;    // the caller's zeros are left as they are
    }
    // Sixty-four frames a little over four milliseconds apart, the newest last (nvapi.h: "element 63").
    for (std::uint32_t i = 0; i < 64u; ++i) {
        auto&               r = out->frameReport[i];
        const std::uint64_t t = 1'000'000ull + static_cast<std::uint64_t>(i) * 4200ull;
        r.frameID = 5000ull + i;
        r.inputSampleTime = t;
        r.simStartTime = t + 100u;
        r.simEndTime = t + 900u;
        r.renderSubmitStartTime = t + 950u;
        r.renderSubmitEndTime = t + 2100u;
        r.presentStartTime = t + 2150u;
        r.presentEndTime = t + 2300u;
        r.driverStartTime = t + 2300u;
        r.driverEndTime = t + 2700u;
        r.osRenderQueueStartTime = t + 2700u;
        r.osRenderQueueEndTime = t + 3900u;
        r.gpuRenderStartTime = t + 3900u;
        r.gpuRenderEndTime = t + 7600u;
        r.gpuActiveRenderTimeUs = 3500u;
        r.gpuFrameTimeUs = 4200u;
    }
    return NVAPI_OK;
}

NvAPI_Status __cdecl StubGetSleepStatus(IUnknown* device, NV_GET_SLEEP_STATUS_PARAMS* out) {
    g_sleepStatusCalls.fetch_add(1u, std::memory_order_relaxed);
    g_lastDevice.store(device, std::memory_order_relaxed);
    if (out == nullptr) {
        return NVAPI_INVALID_ARGUMENT;
    }
    if (out->version != NV_GET_SLEEP_STATUS_PARAMS_VER1) {
        return NVAPI_INCOMPATIBLE_STRUCT_VERSION;
    }
    if (g_mode.load(std::memory_order_relaxed) == 2u) {
        return NVAPI_ERROR;
    }
    out->bLowLatencyMode = 1;
    out->sleepIntervalUs = 4166u;
    out->bUseGameSleep = 1;
    out->fgMultiplier = 2;
    return NVAPI_OK;
}

}    // namespace

extern "C" {

// The real module's one way in: an address for an interface id, or null for one it does not have.
__declspec(dllexport) void* __cdecl nvapi_QueryInterface(unsigned int id) {
    g_queries.fetch_add(1u, std::memory_order_relaxed);
    if (id == kIdInitialize) {
        return reinterpret_cast<void*>(&StubInitialize);
    }
    if (g_hideGetters.load(std::memory_order_relaxed) != 0u) {
        return nullptr;
    }
    if (id == kIdGetLatency) {
        return reinterpret_cast<void*>(&StubGetLatency);
    }
    if (id == kIdGetSleepStatus) {
        return reinterpret_cast<void*>(&StubGetSleepStatus);
    }
    return nullptr;
}

// The probe's knobs and counters. Names nobody else exports.
__declspec(dllexport) void __cdecl FlStubNvSetMode(std::uint32_t mode) {
    g_mode.store(mode, std::memory_order_relaxed);
}
__declspec(dllexport) void __cdecl FlStubNvSetBlockMs(std::uint32_t milliseconds) {
    g_blockMs.store(milliseconds, std::memory_order_relaxed);
}
__declspec(dllexport) void __cdecl FlStubNvHideGetters(std::uint32_t hide) {
    g_hideGetters.store(hide, std::memory_order_relaxed);
}
__declspec(dllexport) std::uint32_t __cdecl FlStubNvQueries() {
    return g_queries.load(std::memory_order_relaxed);
}
__declspec(dllexport) std::uint32_t __cdecl FlStubNvLatencyCalls() {
    return g_latencyCalls.load(std::memory_order_relaxed);
}
__declspec(dllexport) std::uint32_t __cdecl FlStubNvSleepStatusCalls() {
    return g_sleepStatusCalls.load(std::memory_order_relaxed);
}
__declspec(dllexport) void* __cdecl FlStubNvLastDevice() {
    return g_lastDevice.load(std::memory_order_relaxed);
}

}    // extern "C"
