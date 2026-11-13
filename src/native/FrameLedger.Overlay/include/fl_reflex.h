// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

// The two calls the Overlay makes into NVIDIA's driver, and how it finds them (Advanced metrics train C; owner
// decisions D132, D136, D138-D141; docs/spike-notes §30, §31; 17_HOOK_ENGINE §Memory / latency).
//
// NOT A HOOK. Every other thing the Overlay learns in a game is an argument of a call it hooks. These are calls the
// Overlay MAKES: NvAPI_D3D_GetLatency and NvAPI_D3D_GetSleepStatus, the driver's two read-only latency getters, with
// the Direct3D 12 device behind the swap chain it hooks (rule 4's note of 2026-11-13). Getters only: no marker, no
// sleep mode, no sleep, nothing that changes what the driver does.
//
// THE GAME BROUGHT THE MODULE. nvapi64.dll is looked for by name among the modules the process ALREADY holds and is
// never loaded. This file is not linked against nvapi64.lib -- that static library loads the DLL itself -- and takes
// only the vendored MIT headers' ids (nvapi_interface.h) and structs (nvapi.h). tools/nvapi-readonly-check.ps1 holds
// that the two ids below are the only NVAPI ids in code loaded into a game.
//
// A MODULE IN THE EXECUTABLE'S OWN FOLDER IS REFUSED (D140). The driver's nvapi64.dll lives in Windows' own
// directories; one beside the game is a replacement somebody put there, and FrameLedger neither calls into it on a
// presenting thread nor records its numbers as NVIDIA's.
//
// WHERE THE TWO ADDRESSES ARE (measured by the probe, driver 32.0.16.2114). System32's nvapi64.dll is a shim: it
// answers the two ids with addresses inside ANOTHER image, nvapi64_impl.dll in the driver's own store. So an address
// is accepted when it is executable code of an image the process has loaded -- never the heap, never nowhere -- and
// a reference is kept on THAT image as well, because the addresses are only code for as long as it stays loaded and
// the game may unload NVAPI while a presenting thread is about to call.
//
// SHARED WITH THE PROBE (tools/fl-probe-reflex), which is built with the Overlay's flags and enforces Control Flow
// Guard: its ctest runs THIS sequence. hook-harness does not enforce it, so the injected tests say nothing about CFG.
// nvapi64_impl.dll IS a CFG image; its getters are valid call targets, which the probe's real runs show by having
// made the calls from an enforcing process. A target that were not would be a fast-fail no guard catches: in a game
// that enforces CFG, on a driver nobody has run the probe against, that is not excluded.

#ifndef FRAMELEDGER_FL_REFLEX_H
#define FRAMELEDGER_FL_REFLEX_H

#include <windows.h>

// nvapi.h declares its Direct3D structs only after a Direct3D header, and names DXGI's swap chain in passing.
#include <d3d12.h>
#include <dxgi.h>

#include <cstdint>
#include <cstring>
#include <nvapi.h>

namespace fl::reflex {

inline constexpr unsigned int kIdGetSleepStatus = 0xaef96ca1u;    // nvapi_interface.h: NvAPI_D3D_GetSleepStatus
inline constexpr unsigned int kIdGetLatency = 0x1a587f9cu;        // nvapi_interface.h: NvAPI_D3D_GetLatency

inline constexpr DWORD kPathCapacity = 520;

using QueryInterfaceFn = void*(__cdecl*)(unsigned int);
// The status type under a name of its own: tools/nvapi-readonly-check.ps1 takes an `NvAPI_` name followed by a
// parenthesis for a call by name, and code loaded into a game makes none.
using Status = NvAPI_Status;
using GetLatencyFn = Status(__cdecl*)(IUnknown*, NV_LATENCY_RESULT_PARAMS*);
using GetSleepStatusFn = Status(__cdecl*)(IUnknown*, NV_GET_SLEEP_STATUS_PARAMS*);

struct Api {
    GetLatencyFn     getLatency = nullptr;
    GetSleepStatusFn getSleepStatus = nullptr;
    HMODULE          nvapi = nullptr;                // the nvapi64.dll that was asked; one reference is held
    HMODULE          owner = nullptr;                // the image GetLatency's address is in; references are held
    wchar_t          modulePath[kPathCapacity]{};    // where nvapi64.dll was loaded from, for the log
    wchar_t          ownerPath[kPathCapacity]{};     // ... and the image the getters are in
};

enum class Resolved : std::uint32_t {
    Ok = 0,
    NoModule = 1,            // the process has not loaded nvapi64.dll: nothing was called and nothing was loaded
    NoQueryInterface = 2,    // a module of that name without the export
    NoGetter = 3,            // it does not answer for both getters, or answered with an address in no loaded image
    Refused = 4,             // it sits in the executable's own folder, or its path could not be read (D140)
    Faulted = 5,             // asking it for the getters faulted
};

// The Overlay's own filter (dllmain.cpp's FlFilter): a debugger's exceptions are not ours to swallow.
inline LONG Filter(DWORD code) noexcept {
    if (code == EXCEPTION_BREAKPOINT || code == EXCEPTION_SINGLE_STEP) {
        return EXCEPTION_CONTINUE_SEARCH;
    }
    return EXCEPTION_EXECUTE_HANDLER;
}

// The length of the directory part of a path, its last separator included; 0 when there is none.
inline DWORD DirectoryLength(const wchar_t* path, DWORD length) noexcept {
    for (DWORD i = length; i > 0; --i) {
        if (path[i - 1] == L'\\' || path[i - 1] == L'/') {
            return i;
        }
    }
    return 0;
}

// True when `nvapi` was loaded from the folder the process's executable is in -- or when either path cannot be read
// whole, which is refused the same way: what cannot be placed is not taken for the driver's.
inline bool InExecutableFolder(HMODULE nvapi, wchar_t* modulePath) noexcept {
    wchar_t     exePath[kPathCapacity]{};
    const DWORD moduleLength = GetModuleFileNameW(nvapi, modulePath, kPathCapacity);
    const DWORD exeLength = GetModuleFileNameW(nullptr, exePath, kPathCapacity);
    if (moduleLength == 0 || moduleLength >= kPathCapacity || exeLength == 0 || exeLength >= kPathCapacity) {
        return true;
    }
    const DWORD moduleDir = DirectoryLength(modulePath, moduleLength);
    const DWORD exeDir = DirectoryLength(exePath, exeLength);
    if (moduleDir == 0 || exeDir == 0) {
        return true;
    }
    return CompareStringOrdinal(modulePath, static_cast<int>(moduleDir), exePath, static_cast<int>(exeDir), TRUE) ==
           CSTR_EQUAL;
}

// The loaded image `address` is executable code of, with a reference taken on it -- or null. A getter's address
// comes from the module's own answer; this refuses one that is not code of an image before it is ever called, and
// keeps the image that holds it from being unloaded under a later call.
inline HMODULE OwnerImage(const void* address) noexcept {
    MEMORY_BASIC_INFORMATION info{};
    if (address == nullptr || VirtualQuery(address, &info, sizeof(info)) == 0) {
        return nullptr;
    }
    const DWORD executable = PAGE_EXECUTE | PAGE_EXECUTE_READ | PAGE_EXECUTE_READWRITE | PAGE_EXECUTE_WRITECOPY;
    if (info.Type != MEM_IMAGE || info.State != MEM_COMMIT || (info.Protect & executable) == 0u) {
        return nullptr;
    }
    HMODULE owner = nullptr;
    if (!GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS, static_cast<LPCWSTR>(address), &owner)) {
        return nullptr;
    }
    return owner;
}

// The module's answers for the two ids, asked under a guard: the lookup runs the module's code.
inline bool AskForGetters(QueryInterfaceFn query, void*& latency, void*& sleepStatus, DWORD& faultCode) noexcept {
    __try {
        latency = query(kIdGetLatency);
        sleepStatus = query(kIdGetSleepStatus);
        return true;
    } __except (Filter(GetExceptionCode())) {
        faultCode = GetExceptionCode();
        return false;
    }
}

// Finds the two getters in a module the process ALREADY holds; never loads one.
//
// A REFERENCE IS TAKEN WHEN THE MODULE IS FOUND, and kept whatever is returned after that -- as the Overlay's
// ResolveScoped keeps the one on a module it patches -- so the caller must not call again once this has returned
// anything but NoModule: every other answer is final for the process. One more is taken on the image each getter is
// in. None is ever released: a presenting thread may be inside one of the calls.
inline Resolved Resolve(Api& api, DWORD& faultCode) noexcept {
    api.getLatency = nullptr;
    api.getSleepStatus = nullptr;
    api.nvapi = nullptr;
    api.owner = nullptr;
    api.modulePath[0] = L'\0';
    api.ownerPath[0] = L'\0';
    faultCode = 0;
    HMODULE nvapi = nullptr;
    if (!GetModuleHandleExW(0, L"nvapi64.dll", &nvapi) || nvapi == nullptr) {
        return Resolved::NoModule;
    }
    api.nvapi = nvapi;
    if (InExecutableFolder(nvapi, api.modulePath)) {
        return Resolved::Refused;
    }
    const auto query = reinterpret_cast<QueryInterfaceFn>(GetProcAddress(nvapi, "nvapi_QueryInterface"));
    if (query == nullptr) {
        return Resolved::NoQueryInterface;
    }
    void* latency = nullptr;
    void* sleepStatus = nullptr;
    if (!AskForGetters(query, latency, sleepStatus, faultCode)) {
        return Resolved::Faulted;
    }
    const HMODULE latencyOwner = OwnerImage(latency);
    const HMODULE statusOwner = OwnerImage(sleepStatus);
    if (latencyOwner == nullptr || statusOwner == nullptr) {
        return Resolved::NoGetter;
    }
    api.owner = latencyOwner;
    if (const DWORD length = GetModuleFileNameW(latencyOwner, api.ownerPath, kPathCapacity);
        length == 0 || length >= kPathCapacity) {
        api.ownerPath[0] = L'\0';
    }
    api.getLatency = reinterpret_cast<GetLatencyFn>(latency);
    api.getSleepStatus = reinterpret_cast<GetSleepStatusFn>(sleepStatus);
    return Resolved::Ok;
}

inline Resolved Resolve(Api& api) noexcept {
    DWORD faultCode = 0;
    return Resolve(api, faultCode);
}

// What a read returns when the driver's code faulted under it: not one of NVAPI's own codes.
inline constexpr NvAPI_Status kFaulted = static_cast<NvAPI_Status>(-9999);

// One read of the driver's report. Guarded -- the first guard the Overlay puts around code that is not its own
// (17_HOOK_ENGINE §Fault policy says why this one is). A guard does not bound a call that never returns, and a
// caught fault does not give back a lock the driver held: the caller makes no second call after either.
inline NvAPI_Status ReadLatency(const Api& api, IUnknown* device, NV_LATENCY_RESULT_PARAMS& out,
                                DWORD& faultCode) noexcept {
    std::memset(&out, 0, sizeof(out));
    out.version = NV_LATENCY_RESULT_PARAMS_VER1;
    if (api.getLatency == nullptr || device == nullptr) {
        return NVAPI_INVALID_ARGUMENT;
    }
    __try {
        return api.getLatency(device, &out);
    } __except (Filter(GetExceptionCode())) {
        faultCode = GetExceptionCode();
        return kFaulted;
    }
}

inline NvAPI_Status ReadSleepStatus(const Api& api, IUnknown* device, NV_GET_SLEEP_STATUS_PARAMS& out,
                                    DWORD& faultCode) noexcept {
    std::memset(&out, 0, sizeof(out));
    out.version = NV_GET_SLEEP_STATUS_PARAMS_VER1;
    if (api.getSleepStatus == nullptr || device == nullptr) {
        return NVAPI_INVALID_ARGUMENT;
    }
    __try {
        return api.getSleepStatus(device, &out);
    } __except (Filter(GetExceptionCode())) {
        faultCode = GetExceptionCode();
        return kFaulted;
    }
}

inline NvAPI_Status ReadLatency(const Api& api, IUnknown* device, NV_LATENCY_RESULT_PARAMS& out) noexcept {
    DWORD faultCode = 0;
    return ReadLatency(api, device, out, faultCode);
}

inline NvAPI_Status ReadSleepStatus(const Api& api, IUnknown* device, NV_GET_SLEEP_STATUS_PARAMS& out) noexcept {
    DWORD faultCode = 0;
    return ReadSleepStatus(api, device, out, faultCode);
}

}    // namespace fl::reflex

#endif    // FRAMELEDGER_FL_REFLEX_H
