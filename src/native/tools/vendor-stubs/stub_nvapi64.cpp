// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

// A stand-in for NVIDIA's nvapi64.dll, for Reflex route D (docs/spike-notes §31; owner decisions D132, D136).
//
// NOT A VENDOR LIBRARY AND NOT SHIPPED. It carries the one name the real module's callers resolve everything through
// -- nvapi_QueryInterface -- and answers two interface ids, the two getters route D calls:
// NvAPI_D3D_GetLatency and NvAPI_D3D_GetSleepStatus. The ids and the structs are the vendored MIT headers' own
// (nvapi_interface.h, nvapi.h), so its callers and this stub cannot disagree about either.
//
// WHAT IT IS FOR: the things a real driver cannot be made to do on demand. A getter that is not there, a report of
// nothing, an error, a fault, a span the driver did not report, a span no frame can have, and a call that does not
// come back for a while -- so that each can be shown harmless before anything is tried against a driver, and long
// before anything is tried in a game.
//
// TWO CALLERS. The probe (tools/fl-probe-reflex) loads it into itself and turns its knobs through the exports
// below. The injected tests load it into hook-harness and turn the same knobs, and read the same counts, through the
// named section fl_stub_nvapi_shared.h describes.

#include <windows.h>

#include <d3d11.h>

#include <atomic>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <fl_stub_nvapi_shared.h>
#include <nvapi.h>

namespace {

constexpr unsigned int kIdInitialize = 0x0150e828u;
constexpr unsigned int kIdGetSleepStatus = 0xaef96ca1u;
constexpr unsigned int kIdGetLatency = 0x1a587f9cu;

// The section when it could be made, this otherwise: the stub works either way, only nobody outside can see it.
fl::stubnv::Shared  g_local{};
fl::stubnv::Shared* g_shared = &g_local;
HANDLE              g_section = nullptr;
std::atomic<void*>  g_lastDevice{nullptr};

std::uint32_t Get(const std::uint32_t& word) noexcept {
    return std::atomic_ref<const std::uint32_t>{word}.load(std::memory_order_relaxed);
}

std::uint32_t Bump(std::uint32_t& word) noexcept {
    return std::atomic_ref<std::uint32_t>{word}.fetch_add(1u, std::memory_order_relaxed);
}

void Put(std::uint32_t& word, std::uint32_t value) noexcept {
    std::atomic_ref<std::uint32_t>{word}.store(value, std::memory_order_relaxed);
}

void OpenSection() noexcept {
    wchar_t name[64]{};
    if (_snwprintf_s(name, _TRUNCATE, fl::stubnv::kSectionFormat, GetCurrentProcessId()) <= 0) {
        return;
    }
    g_section = CreateFileMappingW(INVALID_HANDLE_VALUE, nullptr, PAGE_READWRITE, 0, sizeof(fl::stubnv::Shared), name);
    if (g_section == nullptr) {
        return;
    }
    void* view = MapViewOfFile(g_section, FILE_MAP_READ | FILE_MAP_WRITE, 0, 0, sizeof(fl::stubnv::Shared));
    if (view != nullptr) {
        g_shared = static_cast<fl::stubnv::Shared*>(view);
    }
}

NvAPI_Status __cdecl StubInitialize() {
    return NVAPI_OK;
}

NvAPI_Status __cdecl StubGetLatency(IUnknown* device, NV_LATENCY_RESULT_PARAMS* out) {
    const std::uint32_t call = Bump(g_shared->latencyCalls);    // 0 for the first call
    g_lastDevice.store(device, std::memory_order_relaxed);
    if (out == nullptr) {
        return NVAPI_INVALID_ARGUMENT;
    }
    if (out->version != NV_LATENCY_RESULT_PARAMS_VER1) {
        return NVAPI_INCOMPATIBLE_STRUCT_VERSION;
    }
    const std::uint32_t block = Get(g_shared->blockMs);
    if (block != 0u) {
        Sleep(block);
    }
    const std::uint32_t mode = Get(g_shared->mode);
    if (mode == fl::stubnv::kModeError) {
        return NVAPI_ERROR;
    }
    if (mode == fl::stubnv::kModeNothing) {
        return NVAPI_OK;    // the caller's zeros are left as they are
    }
    if (mode == fl::stubnv::kModeFault) {
        volatile std::uint32_t* nowhere = nullptr;
        *nowhere = 1u;    // the fault a driver cannot be asked for
    }
    // Sixty-four frames a little over four milliseconds apart, the newest last (nvapi.h: "element 63"), and ten
    // frames further on at every call -- so a caller that reads twice finds ten new frames, not the same sixty-four.
    const std::uint64_t firstId =
        fl::stubnv::kFirstFrameId + static_cast<std::uint64_t>(call) * fl::stubnv::kFrameIdStep;
    for (std::uint32_t i = 0; i < 64u; ++i) {
        auto&               r = out->frameReport[i];
        const std::uint64_t id = firstId + i;
        const std::uint64_t t = 1'000'000ull + (id - fl::stubnv::kFirstFrameId) * 4200ull;
        r.frameID = id;
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
        if (mode == fl::stubnv::kModeNoDriverSpan) {
            r.driverStartTime = 0u;
        }
        if (mode == fl::stubnv::kModeImpossible) {
            r.presentEndTime = r.presentStartTime - 50u;
        }
    }
    return NVAPI_OK;
}

NvAPI_Status __cdecl StubGetSleepStatus(IUnknown* device, NV_GET_SLEEP_STATUS_PARAMS* out) {
    Bump(g_shared->sleepStatusCalls);
    g_lastDevice.store(device, std::memory_order_relaxed);
    if (out == nullptr) {
        return NVAPI_INVALID_ARGUMENT;
    }
    if (out->version != NV_GET_SLEEP_STATUS_PARAMS_VER1) {
        return NVAPI_INCOMPATIBLE_STRUCT_VERSION;
    }
    if (Get(g_shared->mode) == fl::stubnv::kModeError) {
        return NVAPI_ERROR;
    }
    out->bLowLatencyMode = 1;
    out->sleepIntervalUs = 4166u;
    out->bUseGameSleep = 1;
    out->fgMultiplier = 2;
    return NVAPI_OK;
}

}    // namespace

BOOL WINAPI DllMain(HINSTANCE, DWORD reason, LPVOID) {
    if (reason == DLL_PROCESS_ATTACH) {
        OpenSection();    // kernel32 only, and nothing that takes another module's lock
    }
    return TRUE;
}

extern "C" {

// The real module's one way in: an address for an interface id, or null for one it does not have.
__declspec(dllexport) void* __cdecl nvapi_QueryInterface(unsigned int id) {
    Bump(g_shared->queries);
    if (id == kIdInitialize) {
        return reinterpret_cast<void*>(&StubInitialize);
    }
    if (Get(g_shared->hideGetters) != 0u) {
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
    Put(g_shared->mode, mode);
}
__declspec(dllexport) void __cdecl FlStubNvSetBlockMs(std::uint32_t milliseconds) {
    Put(g_shared->blockMs, milliseconds);
}
__declspec(dllexport) void __cdecl FlStubNvHideGetters(std::uint32_t hide) {
    Put(g_shared->hideGetters, hide);
}
__declspec(dllexport) std::uint32_t __cdecl FlStubNvQueries() {
    return Get(g_shared->queries);
}
__declspec(dllexport) std::uint32_t __cdecl FlStubNvLatencyCalls() {
    return Get(g_shared->latencyCalls);
}
__declspec(dllexport) std::uint32_t __cdecl FlStubNvSleepStatusCalls() {
    return Get(g_shared->sleepStatusCalls);
}
__declspec(dllexport) void* __cdecl FlStubNvLastDevice() {
    return g_lastDevice.load(std::memory_order_relaxed);
}

}    // extern "C"
