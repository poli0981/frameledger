// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

// fl-probe-reflex -- the instrument of docs/spike-notes §31 (Reflex route D, owner decision D132). NOT SHIPPED, and it
// touches no game: it runs the call sequence of fl_reflex_candidate.h in its own process, against two things.
//
//   --stub <path>   a stand-in nvapi64.dll (tools/vendor-stubs/stub_nvapi64.cpp): the module absent, a getter absent,
//                   a report of nothing, an error, sixty-four reports, both device kinds, and a call that does not come
//                   back for seconds. ctest fl_reflex_probe_stub. Software devices are enough: the stub does not look.
//   --real          the installed driver, on this PC's NVIDIA adapter, with the probe playing the game's part: it
//                   loads nvapi64.dll itself, initialises it, and SETS LATENCY MARKERS ON ITS OWN DEVICE for a few
//                   hundred frames -- a write, made here because a getter has nothing to return without it, in a
//                   process that is nobody's game. Then it measures what route D would do: the two getters. A
//                   measurement, not a test: exit 77 where there is no NVIDIA adapter.
//
// Built with the Overlay's own flags, because the candidate is the Overlay's code-to-be.

#include <windows.h>

#include <d3d11.h>
#include <d3d12.h>
#include <dxgi1_4.h>

#include <atomic>
#include <cstdint>
#include <cstdio>
#include <cstring>

#include "fl_reflex_candidate.h"

namespace {

int g_failures = 0;

void Check(bool ok, const char* what) noexcept {
    std::printf("%s  %s\n", ok ? "PASS" : "FAIL", what);
    if (!ok) {
        ++g_failures;
    }
}

template <class T>
void ReleaseIf(T*& p) noexcept {
    if (p != nullptr) {
        p->Release();
        p = nullptr;
    }
}

double QpcSeconds() noexcept {
    // QuadPart named in the initialiser, so the member read back is the one the union holds (cpp:S6232).
    LARGE_INTEGER f{.QuadPart = 0};
    LARGE_INTEGER c{.QuadPart = 0};
    QueryPerformanceFrequency(&f);
    QueryPerformanceCounter(&c);
    return static_cast<double>(c.QuadPart) / static_cast<double>(f.QuadPart);
}

// ---------------------------------------------------------------------------------------------------------------------
// Devices. `nvidia` picks this PC's NVIDIA adapter (vendor 0x10DE) or fails; otherwise the software adapter.
// ---------------------------------------------------------------------------------------------------------------------
IDXGIAdapter1* FindAdapter(IDXGIFactory4* factory, bool nvidia) noexcept {
    if (!nvidia) {
        IDXGIAdapter1* warp = nullptr;
        factory->EnumWarpAdapter(__uuidof(IDXGIAdapter1), reinterpret_cast<void**>(&warp));
        return warp;
    }
    for (UINT i = 0;; ++i) {
        IDXGIAdapter1* adapter = nullptr;
        if (factory->EnumAdapters1(i, &adapter) != S_OK) {
            return nullptr;
        }
        DXGI_ADAPTER_DESC1 desc{};
        if (SUCCEEDED(adapter->GetDesc1(&desc)) && desc.VendorId == 0x10DEu &&
            (desc.Flags & DXGI_ADAPTER_FLAG_SOFTWARE) == 0u) {
            return adapter;
        }
        adapter->Release();
    }
}

struct Dx11 {
    ID3D11Device*           device = nullptr;
    ID3D11DeviceContext*    context = nullptr;
    IDXGISwapChain1*        chain = nullptr;
    ID3D11RenderTargetView* target = nullptr;
};

bool MakeDx11(IDXGIFactory4* factory, IDXGIAdapter1* adapter, Dx11& out) noexcept {
    if (FAILED(D3D11CreateDevice(adapter, D3D_DRIVER_TYPE_UNKNOWN, nullptr, D3D11_CREATE_DEVICE_BGRA_SUPPORT, nullptr,
                                 0, D3D11_SDK_VERSION, &out.device, nullptr, &out.context))) {
        return false;
    }
    DXGI_SWAP_CHAIN_DESC1 desc{};
    desc.Width = 256;
    desc.Height = 256;
    desc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
    desc.SampleDesc.Count = 1;
    desc.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT;
    desc.BufferCount = 2;
    desc.SwapEffect = DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL;
    desc.AlphaMode = DXGI_ALPHA_MODE_PREMULTIPLIED;
    if (FAILED(factory->CreateSwapChainForComposition(out.device, &desc, nullptr, &out.chain))) {
        return false;
    }
    ID3D11Texture2D* back = nullptr;
    if (FAILED(out.chain->GetBuffer(0, __uuidof(ID3D11Texture2D), reinterpret_cast<void**>(&back)))) {
        return false;
    }
    const HRESULT hr = out.device->CreateRenderTargetView(back, nullptr, &out.target);
    back->Release();
    return SUCCEEDED(hr);
}

void FreeDx11(Dx11& d) noexcept {
    ReleaseIf(d.target);
    ReleaseIf(d.chain);
    ReleaseIf(d.context);
    ReleaseIf(d.device);
}

struct Dx12 {
    ID3D12Device*       device = nullptr;
    ID3D12CommandQueue* queue = nullptr;
    IDXGISwapChain1*    chain = nullptr;
};

bool MakeDx12(IDXGIFactory4* factory, IDXGIAdapter1* adapter, Dx12& out) noexcept {
    if (FAILED(D3D12CreateDevice(adapter, D3D_FEATURE_LEVEL_11_0, __uuidof(ID3D12Device),
                                 reinterpret_cast<void**>(&out.device)))) {
        return false;
    }
    D3D12_COMMAND_QUEUE_DESC q{};
    q.Type = D3D12_COMMAND_LIST_TYPE_DIRECT;
    if (FAILED(
            out.device->CreateCommandQueue(&q, __uuidof(ID3D12CommandQueue), reinterpret_cast<void**>(&out.queue)))) {
        return false;
    }
    DXGI_SWAP_CHAIN_DESC1 desc{};
    desc.Width = 256;
    desc.Height = 256;
    desc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
    desc.SampleDesc.Count = 1;
    desc.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT;
    desc.BufferCount = 2;
    desc.SwapEffect = DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL;
    desc.AlphaMode = DXGI_ALPHA_MODE_PREMULTIPLIED;
    return SUCCEEDED(factory->CreateSwapChainForComposition(out.queue, &desc, nullptr, &out.chain));
}

void FreeDx12(Dx12& d) noexcept {
    ReleaseIf(d.chain);
    ReleaseIf(d.queue);
    ReleaseIf(d.device);
}

// ---------------------------------------------------------------------------------------------------------------------
// --stub
// ---------------------------------------------------------------------------------------------------------------------
using SetU32Fn = void(__cdecl*)(std::uint32_t);
using GetU32Fn = std::uint32_t(__cdecl*)();
using GetPtrFn = void*(__cdecl*)();

struct BlockedCall {
    fl::reflex::Api api;
    IUnknown*       device;
    NvAPI_Status    status;
};

DWORD WINAPI BlockedCallThread(LPVOID p) noexcept {
    auto*                           call = static_cast<BlockedCall*>(p);
    static NV_LATENCY_RESULT_PARAMS report;    // 15 KB: not on a new thread's stack for no reason
    call->status = fl::reflex::ReadLatency(call->api, call->device, report);
    return 0;
}

// A stand-in for the Overlay's watchdog: a loop that ticks every 50 ms and, when told, makes the call ITSELF.
struct Ticker {
    std::atomic<std::uint32_t> ticks{0};
    std::atomic<std::uint32_t> callNow{0};
    std::atomic<std::uint32_t> quit{0};
    fl::reflex::Api            api;
    IUnknown*                  device = nullptr;
};

DWORD WINAPI TickerThread(LPVOID p) noexcept {
    auto*                           t = static_cast<Ticker*>(p);
    static NV_LATENCY_RESULT_PARAMS report;
    while (t->quit.load(std::memory_order_acquire) == 0u) {
        if (t->callNow.exchange(0u, std::memory_order_acq_rel) != 0u) {
            fl::reflex::ReadLatency(t->api, t->device, report);
        }
        t->ticks.fetch_add(1u, std::memory_order_relaxed);
        Sleep(50);
    }
    return 0;
}

int RunStub(const wchar_t* stubPath) noexcept {
    using fl::reflex::Resolved;
    fl::reflex::Api api{};

    // 1. The module is not in the process: nothing is called, and nothing gets loaded by asking.
    Check(GetModuleHandleW(L"nvapi64.dll") == nullptr, "before: no nvapi64.dll in this process");
    Check(fl::reflex::Resolve(api) == Resolved::NoModule, "module absent -> NoModule");
    Check(GetModuleHandleW(L"nvapi64.dll") == nullptr, "resolving loaded nothing");
    Check(api.getLatency == nullptr && api.getSleepStatus == nullptr, "no address was kept");

    HMODULE stub = LoadLibraryExW(stubPath, nullptr, LOAD_WITH_ALTERED_SEARCH_PATH);
    if (stub == nullptr) {
        std::printf("FAIL  the stub did not load (%lu)\n", GetLastError());
        return 1;
    }
    const auto setMode = reinterpret_cast<SetU32Fn>(GetProcAddress(stub, "FlStubNvSetMode"));
    const auto setBlock = reinterpret_cast<SetU32Fn>(GetProcAddress(stub, "FlStubNvSetBlockMs"));
    const auto hide = reinterpret_cast<SetU32Fn>(GetProcAddress(stub, "FlStubNvHideGetters"));
    const auto latencyCalls = reinterpret_cast<GetU32Fn>(GetProcAddress(stub, "FlStubNvLatencyCalls"));
    const auto lastDevice = reinterpret_cast<GetPtrFn>(GetProcAddress(stub, "FlStubNvLastDevice"));
    if (setMode == nullptr || setBlock == nullptr || hide == nullptr || latencyCalls == nullptr ||
        lastDevice == nullptr) {
        std::printf("FAIL  the module at that path is not the stub\n");
        return 1;
    }

    // 2. A module that does not have the getters.
    hide(1u);
    Check(fl::reflex::Resolve(api) == Resolved::NoGetter, "a getter the module does not have -> NoGetter");
    Check(api.getLatency == nullptr, "and nothing half-resolved is kept");
    hide(0u);
    Check(fl::reflex::Resolve(api) == Resolved::Ok, "both getters resolved by interface id");

    IDXGIFactory4* factory = nullptr;
    if (FAILED(CreateDXGIFactory1(__uuidof(IDXGIFactory4), reinterpret_cast<void**>(&factory)))) {
        std::printf("SKIP  no DXGI factory\n");
        return 77;
    }
    IDXGIAdapter1* adapter = FindAdapter(factory, false);
    Dx11           dx11{};
    Dx12           dx12{};
    const bool     have11 = MakeDx11(factory, adapter, dx11);
    const bool     have12 = MakeDx12(factory, adapter, dx12);
    if (!have11) {
        std::printf("SKIP  no Direct3D 11 device here\n");
        return 77;
    }

    static NV_LATENCY_RESULT_PARAMS report;
    NV_GET_SLEEP_STATUS_PARAMS      sleep{};

    // 3. Sixty-four reports, read as the header lays them out, with the device handed through untouched.
    setMode(0u);
    Check(fl::reflex::ReadLatency(api, dx11.device, report) == NVAPI_OK, "D3D11: the report is read");
    fl::reflex::Digest d = fl::reflex::Summarise(report);
    Check(d.valid == 64u && d.newestFrameId == 5063u, "sixty-four frames, the newest last");
    Check(d.newestSimToPresentEnd == 2200u && d.newestSimToGpuEnd == 7500u && d.newestGpuActiveUs == 3500u,
          "the spans are the ones the stub wrote");
    Check(lastDevice() == dx11.device, "the driver was handed the device, and only the device");
    if (have12) {
        Check(fl::reflex::ReadLatency(api, dx12.device, report) == NVAPI_OK && lastDevice() == dx12.device,
              "D3D12: the same call with the ID3D12Device the Overlay already holds");
    } else {
        std::printf("NOTE  no Direct3D 12 device here; that half is not run\n");
    }
    Check(fl::reflex::ReadSleepStatus(api, dx11.device, sleep) == NVAPI_OK && sleep.bLowLatencyMode != 0 &&
              sleep.sleepIntervalUs == 4166u && sleep.fgMultiplier == 2,
          "the sleep status is read");

    // 4. A game that set no marker (nvapi.h: every frame comes back zero), and a driver that says no.
    setMode(1u);
    Check(fl::reflex::ReadLatency(api, dx11.device, report) == NVAPI_OK && fl::reflex::Summarise(report).valid == 0u,
          "a report of nothing is zero frames, not sixty-four");
    setMode(2u);
    Check(fl::reflex::ReadLatency(api, dx11.device, report) == NVAPI_ERROR, "an error is returned as the error");
    Check(fl::reflex::ReadSleepStatus(api, dx11.device, sleep) == NVAPI_ERROR, "for the sleep status as well");
    Check(fl::reflex::ReadLatency(api, nullptr, report) == NVAPI_INVALID_ARGUMENT &&
              latencyCalls() == (have12 ? 4u : 3u),
          "no device, no call: the driver was not entered");
    setMode(0u);

    // 5. A call that does not come back for a while. On a thread of its own, nothing else waits for it...
    setBlock(1500u);
    Ticker ticker{};
    ticker.api = api;
    ticker.device = dx11.device;
    HANDLE tick = CreateThread(nullptr, 0, &TickerThread, &ticker, 0, nullptr);
    Sleep(300);
    BlockedCall         blocked{api, dx11.device, NVAPI_ERROR};
    const std::uint32_t before = ticker.ticks.load();
    const double        began = QpcSeconds();
    HANDLE              caller = CreateThread(nullptr, 0, &BlockedCallThread, &blocked, 0, nullptr);
    Sleep(1000);
    const std::uint32_t during = ticker.ticks.load() - before;
    Check(during >= 10u, "a call blocked on its own thread: the ticking thread went on (>= 10 ticks in the second)");
    WaitForSingleObject(caller, 5000);
    const double took = QpcSeconds() - began;
    Check(blocked.status == NVAPI_OK && took >= 1.4, "and the blocked call came back with its report");
    std::printf("NOTE  the blocked call took %.2f s; the other thread ticked %u times in its first second\n", took,
                during);
    CloseHandle(caller);

    // ...and made BY the ticking thread, that thread stands still for as long as the driver keeps it.
    const std::uint32_t before2 = ticker.ticks.load();
    ticker.callNow.store(1u);
    Sleep(1000);
    const std::uint32_t during2 = ticker.ticks.load() - before2;
    Check(during2 <= 3u, "the same call made by the ticking thread: it stopped ticking while the driver held it");
    std::printf("NOTE  made by the ticking thread itself, it ticked %u times in that second\n", during2);
    Sleep(1000);
    ticker.quit.store(1u);
    WaitForSingleObject(tick, 3000);
    CloseHandle(tick);
    setBlock(0u);

    FreeDx12(dx12);
    FreeDx11(dx11);
    ReleaseIf(adapter);
    ReleaseIf(factory);
    std::printf("%s: %d failure(s)\n", g_failures == 0 ? "STUB OK" : "STUB FAILED", g_failures);
    return g_failures == 0 ? 0 : 1;
}

// ---------------------------------------------------------------------------------------------------------------------
// --real: the installed driver. The probe is the "game" here: it loads NVAPI, initialises it and sets markers.
// ---------------------------------------------------------------------------------------------------------------------
constexpr unsigned int kIdInitialize = 0x0150e828u;          // nvapi_interface.h: NvAPI_Initialize
constexpr unsigned int kIdUnload = 0xd22bdd7eu;              // NvAPI_Unload
constexpr unsigned int kIdSetLatencyMarker = 0xd9984c05u;    // NvAPI_D3D_SetLatencyMarker -- the game's call

using PlainFn = NvAPI_Status(__cdecl*)();
using SetMarkerFn = NvAPI_Status(__cdecl*)(IUnknown*, NV_LATENCY_MARKER_PARAMS*);

struct Game {
    SetMarkerFn   setMarker = nullptr;
    IUnknown*     markerDevice = nullptr;
    std::uint64_t frame = 0;
    std::uint32_t markerErrors = 0;
    NvAPI_Status  lastMarkerStatus = NVAPI_OK;
};

void Mark(Game& g, NV_LATENCY_MARKER_TYPE type) noexcept {
    NV_LATENCY_MARKER_PARAMS p{};
    p.version = NV_LATENCY_MARKER_PARAMS_VER1;
    p.frameID = g.frame;
    p.markerType = type;
    const NvAPI_Status s = g.setMarker(g.markerDevice, &p);
    if (s != NVAPI_OK) {
        ++g.markerErrors;
        g.lastMarkerStatus = s;
    }
}

void Frame11(Game& g, Dx11& dx, bool markers) noexcept {
    ++g.frame;
    if (markers) {
        Mark(g, SIMULATION_START);
        Mark(g, SIMULATION_END);
        Mark(g, RENDERSUBMIT_START);
    }
    const float colour[4] = {static_cast<float>(g.frame % 255u) / 255.0f, 0.2f, 0.4f, 1.0f};
    dx.context->OMSetRenderTargets(1, &dx.target, nullptr);
    dx.context->ClearRenderTargetView(dx.target, colour);
    if (markers) {
        Mark(g, RENDERSUBMIT_END);
        Mark(g, PRESENT_START);
    }
    dx.chain->Present(0, 0);
    if (markers) {
        Mark(g, PRESENT_END);
    }
}

void Frame12(Game& g, Dx12& dx) noexcept {
    ++g.frame;
    Mark(g, SIMULATION_START);
    Mark(g, SIMULATION_END);
    Mark(g, RENDERSUBMIT_START);
    Mark(g, RENDERSUBMIT_END);
    Mark(g, PRESENT_START);
    dx.chain->Present(0, 0);
    Mark(g, PRESENT_END);
}

void PrintDigest(const char* label, NvAPI_Status status, const NV_LATENCY_RESULT_PARAMS& report) noexcept {
    const fl::reflex::Digest d = fl::reflex::Summarise(report);
    std::printf("%-46s status %d, frames with anything %u, newest id %llu, sim->presentEnd %llu, sim->gpuEnd %llu, "
                "gpuActive %u us\n",
                label, static_cast<int>(status), d.valid, static_cast<unsigned long long>(d.newestFrameId),
                static_cast<unsigned long long>(d.newestSimToPresentEnd),
                static_cast<unsigned long long>(d.newestSimToGpuEnd), d.newestGpuActiveUs);
}

struct Poller {
    fl::reflex::Api            api;
    IUnknown*                  device = nullptr;
    std::atomic<std::uint32_t> quit{0};
    std::atomic<std::uint32_t> paused{0};
    std::atomic<std::uint32_t> intervalMs{250};    // 0: one read after another, as fast as the driver answers
    std::uint32_t              calls = 0;
    std::uint32_t              notOk = 0;
    std::uint32_t              faults = 0;
    double                     worstSeconds = 0.0;
    std::uint32_t              emptyReports = 0;
    std::uint32_t              atLeast1Ms = 0;    // reads that took a millisecond or more, and two (R6)
    std::uint32_t              atLeast2Ms = 0;
};

DWORD WINAPI PollerThread(LPVOID p) noexcept {
    auto*                           poll = static_cast<Poller*>(p);
    static NV_LATENCY_RESULT_PARAMS report;
    NV_GET_SLEEP_STATUS_PARAMS      sleep{};
    while (poll->quit.load(std::memory_order_acquire) == 0u) {
        if (poll->paused.load(std::memory_order_acquire) != 0u) {
            Sleep(50);
            continue;
        }
        const double       t0 = QpcSeconds();
        const NvAPI_Status a = fl::reflex::ReadLatency(poll->api, poll->device, report);
        const NvAPI_Status b = fl::reflex::ReadSleepStatus(poll->api, poll->device, sleep);
        const double       took = QpcSeconds() - t0;
        ++poll->calls;
        poll->notOk += (a != NVAPI_OK || b != NVAPI_OK) ? 1u : 0u;
        poll->faults += (a == fl::reflex::kFaulted || b == fl::reflex::kFaulted) ? 1u : 0u;
        poll->emptyReports += (a == NVAPI_OK && fl::reflex::Summarise(report).valid == 0u) ? 1u : 0u;
        if (took > poll->worstSeconds) {
            poll->worstSeconds = took;
        }
        poll->atLeast1Ms += took >= 0.001 ? 1u : 0u;
        poll->atLeast2Ms += took >= 0.002 ? 1u : 0u;
        const std::uint32_t interval = poll->intervalMs.load(std::memory_order_relaxed);
        if (interval != 0u) {
            Sleep(interval);
        }
    }
    return 0;
}

// Microseconds a call, over `n` calls: the mean, the median and the worst.
struct Cost {
    double mean = 0.0;
    double median = 0.0;
    double worst = 0.0;
};

template <class Call>
Cost Measure(int n, const Call& call) noexcept {
    static double samples[4096];
    if (n <= 0) {
        return Cost{};
    }
    if (n > 4096) {
        n = 4096;
    }
    double sum = 0.0;
    for (int i = 0; i < n; ++i) {
        const double t0 = QpcSeconds();
        call();
        samples[i] = (QpcSeconds() - t0) * 1e6;
        sum += samples[i];
    }
    for (int i = 1; i < n; ++i) {    // insertion sort: a few thousand doubles, once
        const double v = samples[i];
        int          j = i - 1;
        while (j >= 0 && samples[j] > v) {
            samples[j + 1] = samples[j];
            --j;
        }
        samples[j + 1] = v;
    }
    return Cost{sum / n, samples[n / 2], samples[n - 1]};
}

double FramesPerSecond11(Game& g, Dx11& dx, double seconds) noexcept {
    const double  t0 = QpcSeconds();
    std::uint32_t frames = 0;
    while (QpcSeconds() - t0 < seconds) {
        Frame11(g, dx, true);
        ++frames;
    }
    return static_cast<double>(frames) / (QpcSeconds() - t0);
}

// ---------------------------------------------------------------------------------------------------------------------
// R6 (owner decision D139; spike-notes §31, "The leg with a sleep"): THE GETTERS WHILE THE GAME SLEEPS.
// No run above set a sleep mode or called NvAPI_D3D_Sleep, and a game that uses Reflex does both every frame. If the
// driver makes a getter wait behind the game's sleep, a read on the presenting thread holds a present for as long as
// the sleep lasts. The probe is the game once more: BOTH CALLS BELOW ARE WRITES, made on the probe's own device in its
// own process; no shipped binary contains either, and the mode is switched back off before the device goes.
// ---------------------------------------------------------------------------------------------------------------------
constexpr unsigned int kIdSetSleepMode = 0xac1ca9e0u;    // nvapi_interface.h: NvAPI_D3D_SetSleepMode -- the game's call
constexpr unsigned int kIdSleep = 0x852cd1d2u;           // NvAPI_D3D_Sleep -- the game's call
constexpr unsigned int kSleepIntervalUs = 4166u;         // a 240-a-second limit: every frame really sleeps

using SetSleepModeFn = NvAPI_Status(__cdecl*)(IUnknown*, NV_SET_SLEEP_MODE_PARAMS*);
using SleepFn = NvAPI_Status(__cdecl*)(IUnknown*);

struct Sleeper {
    SleepFn       sleep = nullptr;
    IUnknown*     device = nullptr;
    bool          on = false;
    std::uint32_t errors = 0;
    std::uint32_t calls = 0;
    double        seconds = 0.0;    // spent inside NvAPI_D3D_Sleep
};

// One frame of a game that uses Reflex: the sleep first, then the markers and the present.
void SleepingFrame12(Game& g, Dx12& dx, Sleeper& s) noexcept {
    if (s.on) {
        const double t0 = QpcSeconds();
        if (s.sleep(s.device) != NVAPI_OK) {
            ++s.errors;
        }
        s.seconds += QpcSeconds() - t0;
        ++s.calls;
    }
    Frame12(g, dx);
}

struct Beside {
    std::uint32_t calls = 0;
    std::uint32_t notOk = 0;
    std::uint32_t faults = 0;
    std::uint32_t atLeast1Ms = 0;
    std::uint32_t atLeast2Ms = 0;
    double        worstUs = 0.0;
    double        framesPerSecond = 0.0;
};

// The getters on another thread every `intervalMs` (0: without a pause) for `seconds`, while this thread is the game.
Beside PollBeside(const fl::reflex::Api& api, Game& g, Dx12& dx, Sleeper& s, std::uint32_t intervalMs,
                  double seconds) noexcept {
    Poller poll{};
    poll.api = api;
    poll.device = dx.device;
    poll.intervalMs.store(intervalMs);
    HANDLE        thread = CreateThread(nullptr, 0, &PollerThread, &poll, 0, nullptr);
    const double  t0 = QpcSeconds();
    std::uint32_t frames = 0;
    while (QpcSeconds() - t0 < seconds) {
        SleepingFrame12(g, dx, s);
        ++frames;
    }
    const double took = QpcSeconds() - t0;
    poll.quit.store(1u);
    if (thread != nullptr) {
        WaitForSingleObject(thread, 5000);
        CloseHandle(thread);
    }
    Beside out{};
    out.calls = poll.calls;
    out.notOk = poll.notOk;
    out.faults = poll.faults;
    out.atLeast1Ms = poll.atLeast1Ms;
    out.atLeast2Ms = poll.atLeast2Ms;
    out.worstUs = poll.worstSeconds * 1e6;
    out.framesPerSecond = static_cast<double>(frames) / took;
    return out;
}

void PrintBeside(const char* label, const Beside& b) noexcept {
    std::printf(
        "%-52s %u reads, %u not OK, %u faulted, worst %.1f us, >= 1 ms: %u, >= 2 ms: %u; %.0f frames a second\n", label,
        b.calls, b.notOk, b.faults, b.worstUs, b.atLeast1Ms, b.atLeast2Ms, b.framesPerSecond);
}

void RunSleepLeg(IDXGIFactory4* factory, IDXGIAdapter1* adapter, const fl::reflex::Api& api,
                 fl::reflex::QueryInterfaceFn query, SetMarkerFn setMarker) noexcept {
    const auto setMode = reinterpret_cast<SetSleepModeFn>(query(kIdSetSleepMode));
    Sleeper    sleeper{};
    sleeper.sleep = reinterpret_cast<SleepFn>(query(kIdSleep));
    Dx12 dx{};
    if (setMode == nullptr || sleeper.sleep == nullptr || !MakeDx12(factory, adapter, dx)) {
        std::printf("R6 SKIPPED: no sleep functions from this nvapi64.dll, or no Direct3D 12 device\n");
        FreeDx12(dx);
        return;
    }
    sleeper.device = dx.device;
    Game game{};
    game.setMarker = setMarker;
    game.markerDevice = dx.device;
    game.frame = 300000;
    static NV_LATENCY_RESULT_PARAMS report;
    NV_GET_SLEEP_STATUS_PARAMS      status{};

    // THE CONTROL: the same device, the same two pollers, before any sleep mode.
    for (int i = 0; i < 300; ++i) {
        SleepingFrame12(game, dx, sleeper);
    }
    const Beside controlSlow = PollBeside(api, game, dx, sleeper, 250u, 20.0);
    const Beside controlFast = PollBeside(api, game, dx, sleeper, 0u, 10.0);
    PrintBeside("R6 control, no sleep mode, 4 reads a second:", controlSlow);
    PrintBeside("R6 control, no sleep mode, reads without a pause:", controlFast);

    // S1. THE GAME SETS A SLEEP MODE AND SLEEPS EVERY FRAME.
    NV_SET_SLEEP_MODE_PARAMS mode{};
    mode.version = NV_SET_SLEEP_MODE_PARAMS_VER1;
    mode.bLowLatencyMode = 1;
    mode.minimumIntervalUs = kSleepIntervalUs;
    mode.bUseMarkersToOptimize = 1;
    const NvAPI_Status set = setMode(dx.device, &mode);
    sleeper.on = set == NVAPI_OK;
    const double began = QpcSeconds();
    for (int i = 0; i < 600; ++i) {
        SleepingFrame12(game, dx, sleeper);
    }
    const double       frameUs = (QpcSeconds() - began) / 600.0 * 1e6;
    const NvAPI_Status s1 = fl::reflex::ReadSleepStatus(api, dx.device, status);
    std::printf("S1 SetSleepMode %d; 600 frames: %.1f us a frame, %.1f us of it inside Sleep, %u sleep error(s)\n",
                static_cast<int>(set), frameUs,
                sleeper.calls != 0u ? sleeper.seconds / static_cast<double>(sleeper.calls) * 1e6 : 0.0, sleeper.errors);
    std::printf("S1 sleep status %d: lowLatency %u, fsVrr %u, cplVsync %u, interval %u us, gameSleep %u, iFlip %u, "
                "fgMultiplier %u\n",
                static_cast<int>(s1), static_cast<unsigned>(status.bLowLatencyMode),
                static_cast<unsigned>(status.bFsVrr), static_cast<unsigned>(status.bCplVsyncOn),
                static_cast<unsigned>(status.sleepIntervalUs), static_cast<unsigned>(status.bUseGameSleep),
                static_cast<unsigned>(status.bFullscreenIFlip), static_cast<unsigned>(status.fgMultiplier));

    // S4. What the report holds once the game sleeps.
    PrintDigest("S4 D3D12, sleeping, 600 frames:", fl::reflex::ReadLatency(api, dx.device, report), report);
    {
        std::uint32_t input = 0;
        std::uint32_t driverStart = 0;
        std::uint32_t gpuEnd = 0;
        for (const auto& r : report.frameReport) {
            input += r.inputSampleTime != 0u ? 1u : 0u;
            driverStart += r.driverStartTime != 0u ? 1u : 0u;
            gpuEnd += r.gpuRenderEndTime != 0u ? 1u : 0u;
        }
        std::printf("S4 of 64 frames: inputSampleTime in %u, driverStartTime in %u, gpuRenderEndTime in %u\n", input,
                    driverStart, gpuEnd);
    }

    // S2. THE GETTERS ON THE THREAD THAT SLEEPS AND PRESENTS: one read after each frame's present -- where the Overlay
    // would make it -- and then back to back.
    {
        static double samples[2000];
        double        sum = 0.0;
        double        worst = 0.0;
        std::uint32_t slow = 0;
        for (double& sample : samples) {
            SleepingFrame12(game, dx, sleeper);
            const double t0 = QpcSeconds();
            fl::reflex::ReadLatency(api, dx.device, report);
            sample = (QpcSeconds() - t0) * 1e6;
            sum += sample;
            worst = sample > worst ? sample : worst;
            slow += sample >= 2000.0 ? 1u : 0u;
        }
        for (int i = 1; i < 2000; ++i) {    // insertion sort, as Measure does
            const double v = samples[i];
            int          j = i - 1;
            while (j >= 0 && samples[j] > v) {
                samples[j + 1] = samples[j];
                --j;
            }
            samples[j + 1] = v;
        }
        std::printf("S2 GetLatency after each of 2000 sleeping frames, us: mean %.2f, median %.2f, worst %.2f, "
                    ">= 2 ms: %u\n",
                    sum / 2000.0, samples[1000], worst, slow);
    }
    const Cost latency = Measure(2000, [&]() noexcept { fl::reflex::ReadLatency(api, dx.device, report); });
    const Cost sleepStatus = Measure(2000, [&]() noexcept { fl::reflex::ReadSleepStatus(api, dx.device, status); });
    std::printf("S2 GetLatency     back to back, us: mean %.2f, median %.2f, worst %.2f (2000 calls)\n", latency.mean,
                latency.median, latency.worst);
    std::printf("S2 GetSleepStatus back to back, us: mean %.2f, median %.2f, worst %.2f (2000 calls)\n",
                sleepStatus.mean, sleepStatus.median, sleepStatus.worst);

    // S3. THE GETTERS ON ANOTHER THREAD WHILE THIS ONE SLEEPS AND PRESENTS -- a game's sleep is often on a thread that
    // is not the one that presents.
    const Beside sleepSlow = PollBeside(api, game, dx, sleeper, 250u, 20.0);
    const Beside sleepFast = PollBeside(api, game, dx, sleeper, 0u, 10.0);
    PrintBeside("S3 sleeping, 4 reads a second:", sleepSlow);
    PrintBeside("S3 sleeping, reads without a pause:", sleepFast);
    std::printf("S3 marker errors %u (last status %d), sleep errors %u\n", game.markerErrors,
                static_cast<int>(game.lastMarkerStatus), sleeper.errors);

    // The mode goes back off before the device does.
    NV_SET_SLEEP_MODE_PARAMS off{};
    off.version = NV_SET_SLEEP_MODE_PARAMS_VER1;
    std::printf("R6 SetSleepMode back off: %d\n", static_cast<int>(setMode(dx.device, &off)));
    FreeDx12(dx);
}

int RunReal(int pollSeconds) noexcept {
    IDXGIFactory4* factory = nullptr;
    if (FAILED(CreateDXGIFactory1(__uuidof(IDXGIFactory4), reinterpret_cast<void**>(&factory)))) {
        std::printf("SKIP  no DXGI factory\n");
        return 77;
    }
    IDXGIAdapter1* adapter = FindAdapter(factory, true);
    if (adapter == nullptr) {
        std::printf("SKIP  no NVIDIA adapter on this PC\n");
        return 77;
    }
    DXGI_ADAPTER_DESC1 desc{};
    adapter->GetDesc1(&desc);
    std::printf("adapter: %ls\n", desc.Description);

    // THE GAME'S PART: nvapi64.dll from the system directory, by full path, and NvAPI_Initialize.
    wchar_t    path[MAX_PATH]{};
    const UINT n = GetSystemDirectoryW(path, MAX_PATH);
    if (n == 0 || n + 16 > MAX_PATH) {
        return 77;
    }
    wcscat_s(path, L"\\nvapi64.dll");
    fl::reflex::Api api{};
    std::printf("before the game loaded it: Resolve = %u (1 = NoModule)\n",
                static_cast<unsigned>(fl::reflex::Resolve(api)));
    HMODULE nvapi = LoadLibraryExW(path, nullptr, LOAD_WITH_ALTERED_SEARCH_PATH);
    if (nvapi == nullptr) {
        std::printf("SKIP  %ls did not load (%lu)\n", path, GetLastError());
        return 77;
    }
    const auto query = reinterpret_cast<fl::reflex::QueryInterfaceFn>(GetProcAddress(nvapi, "nvapi_QueryInterface"));
    const auto initialize = query != nullptr ? reinterpret_cast<PlainFn>(query(kIdInitialize)) : nullptr;
    const auto unload = query != nullptr ? reinterpret_cast<PlainFn>(query(kIdUnload)) : nullptr;
    Game       game{};
    game.setMarker = query != nullptr ? reinterpret_cast<SetMarkerFn>(query(kIdSetLatencyMarker)) : nullptr;
    if (initialize == nullptr || game.setMarker == nullptr) {
        std::printf("SKIP  this nvapi64.dll does not answer the ids\n");
        return 77;
    }

    Dx11 dx11{};
    if (!MakeDx11(factory, adapter, dx11)) {
        std::printf("SKIP  no Direct3D 11 device on the NVIDIA adapter\n");
        return 77;
    }
    static NV_LATENCY_RESULT_PARAMS report;
    NV_GET_SLEEP_STATUS_PARAMS      sleep{};

    // R0. THE GETTERS BEFORE NvAPI_Initialize -- a game that has the module but has not initialised it.
    std::printf("Resolve with the module loaded: %u (0 = Ok)\n", static_cast<unsigned>(fl::reflex::Resolve(api)));
    PrintDigest("R0 before NvAPI_Initialize:", fl::reflex::ReadLatency(api, dx11.device, report), report);
    std::printf("NvAPI_Initialize: %d\n", static_cast<int>(initialize()));

    // R1. Initialised, no marker ever set: what a game that does not use Reflex gives.
    for (int i = 0; i < 120; ++i) {
        Frame11(game, dx11, false);
    }
    PrintDigest("R1 D3D11, 120 frames, no marker:", fl::reflex::ReadLatency(api, dx11.device, report), report);
    const NvAPI_Status s1 = fl::reflex::ReadSleepStatus(api, dx11.device, sleep);
    std::printf("   sleep status %d: lowLatency %u, fsVrr %u, cplVsync %u, interval %u us, gameSleep %u, iFlip %u, "
                "fgMultiplier %u\n",
                static_cast<int>(s1), static_cast<unsigned>(sleep.bLowLatencyMode), static_cast<unsigned>(sleep.bFsVrr),
                static_cast<unsigned>(sleep.bCplVsyncOn), static_cast<unsigned>(sleep.sleepIntervalUs),
                static_cast<unsigned>(sleep.bUseGameSleep), static_cast<unsigned>(sleep.bFullscreenIFlip),
                static_cast<unsigned>(sleep.fgMultiplier));

    // R2. The game sets markers: 300 frames.
    game.markerDevice = dx11.device;
    const double began = QpcSeconds();
    for (int i = 0; i < 300; ++i) {
        Frame11(game, dx11, true);
    }
    const double frameSeconds = (QpcSeconds() - began) / 300.0;
    std::printf("markers: %u error(s), last status %d; a frame took %.1f us by this probe's clock\n", game.markerErrors,
                static_cast<int>(game.lastMarkerStatus), frameSeconds * 1e6);
    PrintDigest("R2 D3D11, 300 frames with markers:", fl::reflex::ReadLatency(api, dx11.device, report), report);
    {
        // The unit of the report's times: the gap between two neighbouring frames' simStartTime against the probe's
        // own measured frame time.
        const auto& a = report.frameReport[62];
        const auto& b = report.frameReport[63];
        std::printf("   frames 62,63: id %llu,%llu simStart %llu,%llu (gap %llu) gpuFrameTimeUs %u\n",
                    static_cast<unsigned long long>(a.frameID), static_cast<unsigned long long>(b.frameID),
                    static_cast<unsigned long long>(a.simStartTime), static_cast<unsigned long long>(b.simStartTime),
                    static_cast<unsigned long long>(b.simStartTime - a.simStartTime),
                    static_cast<unsigned>(b.gpuFrameTimeUs));
        // Every time of the last four frames, as its distance from that frame's simStartTime ("-" where the driver
        // left the field zero): which fields a report fills in, and when a frame's report is complete.
        for (int i = 60; i < 64; ++i) {
            const auto&              r = report.frameReport[i];
            const std::uint64_t      fields[] = {r.inputSampleTime,      r.simEndTime,         r.renderSubmitStartTime,
                                                 r.renderSubmitEndTime,  r.presentStartTime,   r.presentEndTime,
                                                 r.driverStartTime,      r.driverEndTime,      r.osRenderQueueStartTime,
                                                 r.osRenderQueueEndTime, r.gpuRenderStartTime, r.gpuRenderEndTime};
            static const char* const names[] = {"input",    "simEnd", "subStart", "subEnd", "presStart", "presEnd",
                                                "drvStart", "drvEnd", "osqStart", "osqEnd", "gpuStart",  "gpuEnd"};
            std::printf("   frame %d id %llu:", i, static_cast<unsigned long long>(r.frameID));
            for (int f = 0; f < 12; ++f) {
                if (fields[f] == 0u) {
                    std::printf(" %s -", names[f]);
                } else {
                    std::printf(" %s %lld", names[f],
                                static_cast<long long>(fields[f]) - static_cast<long long>(r.simStartTime));
                }
            }
            std::printf(" | gpuActive %u gpuFrame %u\n", static_cast<unsigned>(r.gpuActiveRenderTimeUs),
                        static_cast<unsigned>(r.gpuFrameTimeUs));
        }
    }

    // R3. What a call costs, on the thread that presents, between two frames.
    const Cost latency = Measure(2000, [&]() noexcept { fl::reflex::ReadLatency(api, dx11.device, report); });
    const Cost status = Measure(2000, [&]() noexcept { fl::reflex::ReadSleepStatus(api, dx11.device, sleep); });
    std::printf("R3 GetLatency     us a call: mean %.2f, median %.2f, worst %.2f (2000 calls)\n", latency.mean,
                latency.median, latency.worst);
    std::printf("R3 GetSleepStatus us a call: mean %.2f, median %.2f, worst %.2f (2000 calls)\n", status.mean,
                status.median, status.worst);

    // R4. The getters on ANOTHER thread, four times a second, while this one presents and sets markers.
    // LEGS OF TEN SECONDS, ALTERNATING -- alone, beside the poller, alone, ... -- because one long leg against one
    // short one measures the drift of an uncapped loop over a minute, not the poller (the first run of this probe
    // compared 5 s alone with 60 s beside and read 1.3 % apart).
    Poller poll{};
    poll.api = api;
    poll.device = dx11.device;
    poll.paused.store(1u);
    HANDLE    thread = CreateThread(nullptr, 0, &PollerThread, &poll, 0, nullptr);
    double    aloneSum = 0.0;
    double    besideSum = 0.0;
    const int legs = pollSeconds / 10 > 0 ? pollSeconds / 10 : 1;
    for (int leg = 0; leg < legs; ++leg) {
        poll.paused.store(1u);
        const double alone = FramesPerSecond11(game, dx11, 10.0);
        poll.paused.store(0u);
        const double beside = FramesPerSecond11(game, dx11, 10.0);
        aloneSum += alone;
        besideSum += beside;
        std::printf("R4 leg %d: frames a second %.0f alone, %.0f beside the poller (%+.2f %%)\n", leg + 1, alone,
                    beside, (beside - alone) * 100.0 / alone);
    }
    const std::uint32_t slowCalls = poll.calls;
    const std::uint32_t slowNotOk = poll.notOk;
    const double        slowWorst = poll.worstSeconds;

    // THE SAME WITH THE POLLER READING WITHOUT A PAUSE -- hundreds of thousands of reads a second where route D would
    // make four. The legs above cannot tell half a percent from the loop's own drift; a dose this large can be seen,
    // and what four reads a second cost is that, scaled down by the ratio of the two rates.
    poll.intervalMs.store(0u);
    double        aloneFast = 0.0;
    double        besideFast = 0.0;
    std::uint32_t fastCallsBefore = poll.calls;
    for (int leg = 0; leg < 3; ++leg) {
        poll.paused.store(1u);
        const double alone = FramesPerSecond11(game, dx11, 10.0);
        poll.paused.store(0u);
        const double beside = FramesPerSecond11(game, dx11, 10.0);
        aloneFast += alone;
        besideFast += beside;
        std::printf("R4b leg %d: frames a second %.0f alone, %.0f beside a poller that never pauses (%+.2f %%)\n",
                    leg + 1, alone, beside, (beside - alone) * 100.0 / alone);
    }
    poll.quit.store(1u);
    WaitForSingleObject(thread, 5000);
    CloseHandle(thread);
    std::printf("R4b 30 s beside a poller that never pauses: %u reads (%.0f a second), %u not OK, %u faulted, worst "
                "read %.1f us; frames a second %.0f alone, %.0f beside it (%+.2f %%); marker errors %u\n",
                poll.calls - fastCallsBefore, static_cast<double>(poll.calls - fastCallsBefore) / 30.0,
                poll.notOk - slowNotOk, poll.faults, poll.worstSeconds * 1e6, aloneFast / 3.0, besideFast / 3.0,
                (besideFast - aloneFast) * 100.0 / aloneFast, game.markerErrors);
    poll.calls = slowCalls;
    poll.notOk = slowNotOk;
    poll.worstSeconds = slowWorst;
    std::printf("R4 %d s beside a poller in %d legs: %u reads, %u not OK, %u faulted, %u empty, worst read %.1f us; "
                "frames a second %.0f alone, %.0f beside it (%+.2f %%); marker errors %u\n",
                legs * 10, legs, poll.calls, poll.notOk, poll.faults, poll.emptyReports, poll.worstSeconds * 1e6,
                aloneSum / legs, besideSum / legs, (besideSum - aloneSum) * 100.0 / aloneSum, game.markerErrors);

    // R5. Direct3D 12: which object the driver wants -- the device (what the Overlay holds) or the queue.
    Dx12 dx12{};
    if (MakeDx12(factory, adapter, dx12)) {
        Game game12{};
        game12.setMarker = game.setMarker;
        game12.markerDevice = dx12.device;
        game12.frame = 100000;
        for (int i = 0; i < 300; ++i) {
            Frame12(game12, dx12);
        }
        std::printf("R5 D3D12, markers on the DEVICE: %u error(s), last status %d\n", game12.markerErrors,
                    static_cast<int>(game12.lastMarkerStatus));
        PrintDigest("R5 D3D12, read with the device:", fl::reflex::ReadLatency(api, dx12.device, report), report);
        PrintDigest("R5 D3D12, read with the queue:", fl::reflex::ReadLatency(api, dx12.queue, report), report);
        const NvAPI_Status s12 = fl::reflex::ReadSleepStatus(api, dx12.device, sleep);
        std::printf("   sleep status with the device %d, with the queue %d\n", static_cast<int>(s12),
                    static_cast<int>(fl::reflex::ReadSleepStatus(api, dx12.queue, sleep)));
        Game queue12{};
        queue12.setMarker = game.setMarker;
        queue12.markerDevice = dx12.queue;
        queue12.frame = 200000;
        for (int i = 0; i < 300; ++i) {
            Frame12(queue12, dx12);
        }
        std::printf("R5 D3D12, markers on the QUEUE: %u error(s), last status %d\n", queue12.markerErrors,
                    static_cast<int>(queue12.lastMarkerStatus));
        PrintDigest("R5 D3D12, read with the device after that:", fl::reflex::ReadLatency(api, dx12.device, report),
                    report);
        FreeDx12(dx12);
    } else {
        std::printf("R5 no Direct3D 12 device on the NVIDIA adapter\n");
    }

    // R6. The getters while the game sleeps (D139).
    RunSleepLeg(factory, adapter, api, query, game.setMarker);

    FreeDx11(dx11);
    if (unload != nullptr) {
        std::printf("NvAPI_Unload: %d\n", static_cast<int>(unload()));
    }
    ReleaseIf(adapter);
    ReleaseIf(factory);
    return 0;
}

}    // namespace

int wmain(int argc, wchar_t** argv) {
    if (argc >= 3 && wcscmp(argv[1], L"--stub") == 0) {
        return RunStub(argv[2]);
    }
    if (argc >= 2 && wcscmp(argv[1], L"--real") == 0) {
        return RunReal(argc >= 3 ? _wtoi(argv[2]) : 60);
    }
    std::printf("usage: fl-probe-reflex --stub <path to the stub nvapi64.dll> | --real [seconds beside a poller]\n");
    return 2;
}
