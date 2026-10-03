// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

// fl-probe-processstats — the instrument behind spike-notes §16 (beta.12, owner decision D43): what
// FrameLedger.ProcessStats.dll reads about a process, how much it costs, and whether it agrees with what the process
// says about itself. Not shipped; never pointed at a game by anything in the repository.
//
//   fl-probe-processstats --hold-vram <MiB> <seconds>
//       BE the measured process: create a D3D11 device on the first HARDWARE adapter, allocate <MiB> in a DEFAULT
//       buffer, and print this process's own IDXGIAdapter3::QueryVideoMemoryInfo(LOCAL) before and after — the
//       figure a game sees about itself. Exit 77 when the machine has no hardware adapter (a CI runner).
//   fl-probe-processstats --read <pid> [--samples N] [--interval-ms M] [--v2-name <image name without .exe>]
//       Read <pid> N times through the DLL: dedicated and shared video memory, working set, private working set,
//       commit, and the time each collect + read took; at the end the median and maximum time, and how much this
//       probe's own private bytes grew (the leak check). --v2-name also reads
//       `\Process V2(<name>:<pid>)\Working Set - Private` for the comparison with PrivateWorkingSetSize.
#include <windows.h>

#include <d3d11.h>
#include <dxgi1_4.h>

#include <algorithm>
#include <cstdio>
#include <cstdlib>
#include <cwchar>
#include <pdh.h>
#include <psapi.h>
#include <string>
#include <vector>

#include "fl_process_stats.h"

#pragma comment(lib, "d3d11.lib")
#pragma comment(lib, "dxgi.lib")
#pragma comment(lib, "pdh.lib")

namespace {

double Mib(uint64_t bytes) {
    return static_cast<double>(bytes) / (1024.0 * 1024.0);
}

int HoldVram(int mib, int seconds) {
    IDXGIFactory1* factory = nullptr;
    if (FAILED(CreateDXGIFactory1(__uuidof(IDXGIFactory1), reinterpret_cast<void**>(&factory)))) {
        std::printf("SKIP: no DXGI factory\n");
        return 77;
    }
    IDXGIAdapter1* adapter = nullptr;
    for (UINT i = 0; factory->EnumAdapters1(i, &adapter) != DXGI_ERROR_NOT_FOUND; ++i) {
        DXGI_ADAPTER_DESC1 d{};
        adapter->GetDesc1(&d);
        if ((d.Flags & DXGI_ADAPTER_FLAG_SOFTWARE) == 0 && d.DedicatedVideoMemory > 0) {
            std::printf("adapter: %ls (dedicated %.0f MiB)\n", d.Description, Mib(d.DedicatedVideoMemory));
            break;
        }
        adapter->Release();
        adapter = nullptr;
    }
    factory->Release();
    if (adapter == nullptr) {
        std::printf("SKIP: no hardware adapter with dedicated memory\n");
        return 77;
    }

    ID3D11Device*        device = nullptr;
    ID3D11DeviceContext* context = nullptr;
    if (FAILED(D3D11CreateDevice(adapter, D3D_DRIVER_TYPE_UNKNOWN, nullptr, 0, nullptr, 0, D3D11_SDK_VERSION, &device,
                                 nullptr, &context))) {
        std::printf("SKIP: D3D11CreateDevice failed on the hardware adapter\n");
        adapter->Release();
        return 77;
    }
    IDXGIAdapter3* adapter3 = nullptr;
    adapter->QueryInterface(__uuidof(IDXGIAdapter3), reinterpret_cast<void**>(&adapter3));

    auto report = [&](const char* when) {
        DXGI_QUERY_VIDEO_MEMORY_INFO info{};
        if (adapter3 != nullptr &&
            SUCCEEDED(adapter3->QueryVideoMemoryInfo(0, DXGI_MEMORY_SEGMENT_GROUP_LOCAL, &info))) {
            std::printf("pid=%lu %s local_current_usage=%llu (%.1f MiB) budget=%llu (%.1f MiB)\n",
                        GetCurrentProcessId(), when, static_cast<unsigned long long>(info.CurrentUsage),
                        Mib(info.CurrentUsage), static_cast<unsigned long long>(info.Budget), Mib(info.Budget));
        }
        std::fflush(stdout);
    };

    report("before");
    D3D11_BUFFER_DESC bd{};
    bd.ByteWidth = static_cast<UINT>(mib) * 1024u * 1024u;
    bd.Usage = D3D11_USAGE_DEFAULT;
    bd.BindFlags = D3D11_BIND_UNORDERED_ACCESS;
    bd.MiscFlags = D3D11_RESOURCE_MISC_BUFFER_ALLOW_RAW_VIEWS;
    ID3D11Buffer* buffer = nullptr;
    if (FAILED(device->CreateBuffer(&bd, nullptr, &buffer))) {
        std::printf("FAILED: CreateBuffer(%d MiB)\n", mib);
        return 2;
    }
    // Touch it so it is resident, not merely reserved.
    D3D11_UNORDERED_ACCESS_VIEW_DESC uav{};
    uav.Format = DXGI_FORMAT_R32_TYPELESS;
    uav.ViewDimension = D3D11_UAV_DIMENSION_BUFFER;
    uav.Buffer.NumElements = bd.ByteWidth / 4;
    uav.Buffer.Flags = D3D11_BUFFER_UAV_FLAG_RAW;
    ID3D11UnorderedAccessView* view = nullptr;
    if (SUCCEEDED(device->CreateUnorderedAccessView(buffer, &uav, &view))) {
        const UINT zero[4] = {1, 2, 3, 4};
        context->ClearUnorderedAccessViewUint(view, zero);
    }
    context->Flush();
    report("after");
    std::printf("holding %d MiB for %d s\n", mib, seconds);
    std::fflush(stdout);
    Sleep(static_cast<DWORD>(seconds) * 1000u);
    report("end");
    if (view != nullptr) {
        view->Release();
    }
    buffer->Release();
    context->Release();
    device->Release();
    if (adapter3 != nullptr) {
        adapter3->Release();
    }
    adapter->Release();
    return 0;
}

uint64_t OwnPrivateBytes() {
    PROCESS_MEMORY_COUNTERS_EX ex{};
    ex.cb = sizeof(ex);
    GetProcessMemoryInfo(GetCurrentProcess(), reinterpret_cast<PROCESS_MEMORY_COUNTERS*>(&ex), sizeof(ex));
    return ex.PrivateUsage;
}

uint64_t CreationTime(uint32_t pid) {
    HANDLE h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, pid);
    if (h == nullptr) {
        return 0;
    }
    FILETIME c{}, e{}, k{}, u{};
    uint64_t value = 0;
    if (GetProcessTimes(h, &c, &e, &k, &u)) {
        value = (static_cast<uint64_t>(c.dwHighDateTime) << 32) | c.dwLowDateTime;
    }
    CloseHandle(h);
    return value;
}

int Read(uint32_t pid, int samples, int intervalMs, const wchar_t* v2Name) {
    void* reader = FlPsOpen();
    if (reader == nullptr) {
        std::printf("FAILED: FlPsOpen\n");
        return 2;
    }

    PDH_HQUERY   v2Query = nullptr;
    PDH_HCOUNTER v2Counter = nullptr;
    if (v2Name != nullptr) {
        wchar_t path[512];
        std::swprintf(path, 512, L"\\Process V2(%ls:%lu)\\Working Set - Private", v2Name,
                      static_cast<unsigned long>(pid));
        if (PdhOpenQueryW(nullptr, 0, &v2Query) != ERROR_SUCCESS ||
            PdhAddEnglishCounterW(v2Query, path, 0, &v2Counter) != ERROR_SUCCESS) {
            std::printf("note: Process V2 counter unavailable for %ls\n", path);
            v2Counter = nullptr;
        }
    }

    const uint64_t      created = CreationTime(pid);
    const uint64_t      ownBefore = OwnPrivateBytes();
    std::vector<double> costs;
    LARGE_INTEGER       freq{};
    QueryPerformanceFrequency(&freq);
    for (int i = 0; i < samples; ++i) {
        LARGE_INTEGER t0{}, t1{};
        QueryPerformanceCounter(&t0);
        const int32_t collected = FlPsCollect(reader);
        FlPsSample    s{};
        s.size = sizeof(s);
        const int32_t read = FlPsRead(reader, pid, created, nullptr, &s);
        QueryPerformanceCounter(&t1);
        const double ms = static_cast<double>(t1.QuadPart - t0.QuadPart) * 1000.0 / static_cast<double>(freq.QuadPart);
        costs.push_back(ms);

        double v2 = -1;
        if (v2Counter != nullptr && PdhCollectQueryData(v2Query) == ERROR_SUCCESS) {
            PDH_FMT_COUNTERVALUE value{};
            if (PdhGetFormattedCounterValue(v2Counter, PDH_FMT_LARGE, nullptr, &value) == ERROR_SUCCESS) {
                v2 = Mib(static_cast<uint64_t>(value.largeValue));
            }
        }
        std::printf(
            "#%03d collect=%d read=%d ms=%.3f dedicated=%.1f shared=%.1f inst=%u gpu=%d ws=%.1f private_ws=%.1f "
            "commit=%.1f ram=%d v2_private_ws=%.1f present=0x%02x\n",
            i, collected, read, ms, Mib(s.gpuDedicatedBytes), Mib(s.gpuSharedBytes), s.gpuInstances, s.gpuStatus,
            Mib(s.workingSetBytes), Mib(s.privateWorkingSetBytes), Mib(s.commitBytes), s.ramStatus, v2, s.present);
        std::fflush(stdout);
        if (intervalMs > 0 && i + 1 < samples) {
            Sleep(static_cast<DWORD>(intervalMs));
        }
    }
    const uint64_t ownAfter = OwnPrivateBytes();
    std::sort(costs.begin(), costs.end());
    const double median = costs.empty() ? 0 : costs[costs.size() / 2];
    const double worst = costs.empty() ? 0 : costs.back();
    std::printf("SUMMARY samples=%d cost_median_ms=%.3f cost_max_ms=%.3f probe_private_bytes_delta=%lld\n", samples,
                median, worst, static_cast<long long>(ownAfter) - static_cast<long long>(ownBefore));
    if (v2Query != nullptr) {
        PdhCloseQuery(v2Query);
    }
    FlPsClose(reader);
    return 0;
}

}    // namespace

int wmain(int argc, wchar_t** argv) {
    if (argc >= 4 && std::wcscmp(argv[1], L"--hold-vram") == 0) {
        return HoldVram(_wtoi(argv[2]), _wtoi(argv[3]));
    }
    if (argc >= 3 && std::wcscmp(argv[1], L"--read") == 0) {
        const auto     pid = static_cast<uint32_t>(std::wcstoul(argv[2], nullptr, 10));
        int            samples = 60;
        int            intervalMs = 1000;
        const wchar_t* v2Name = nullptr;
        for (int i = 3; i + 1 < argc; i += 2) {
            if (std::wcscmp(argv[i], L"--samples") == 0) {
                samples = _wtoi(argv[i + 1]);
            } else if (std::wcscmp(argv[i], L"--interval-ms") == 0) {
                intervalMs = _wtoi(argv[i + 1]);
            } else if (std::wcscmp(argv[i], L"--v2-name") == 0) {
                v2Name = argv[i + 1];
            }
        }
        return Read(pid, samples, intervalMs, v2Name);
    }
    std::printf("usage: fl-probe-processstats --hold-vram <MiB> <seconds>\n"
                "       fl-probe-processstats --read <pid> [--samples N] [--interval-ms M] [--v2-name <image>]\n");
    return 64;
}
