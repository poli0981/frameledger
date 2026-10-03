// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

// FrameLedger.ProcessStats's C ABI (beta.12, owner decision D43): against this test's own process for the system
// half, and — on a machine with a hardware GPU — against a buffer this test allocates for the video half. Links the
// SHIPPED DLL, copied beside the test, because what the managed side loads is that DLL's export table.
//
// Green on both kinds of machine and it says which: a CI runner has no hardware adapter, so the video half takes the
// NO_GPU_INSTANCE branch there and the GPU_INSTANCE branch only where a real device exists (the same alternation
// fl_nvapi_bridge pins, for the same reason).
#include <windows.h>

#include <d3d11.h>
#include <dxgi1_2.h>

#include <catch2/catch_test_macros.hpp>
#include <cstdio>

#include "fl_process_stats.h"

#pragma comment(lib, "d3d11.lib")
#pragma comment(lib, "dxgi.lib")

namespace {

uint64_t CreationTimeOfThisProcess() {
    FILETIME c{}, e{}, k{}, u{};
    REQUIRE(GetProcessTimes(GetCurrentProcess(), &c, &e, &k, &u));
    return (static_cast<uint64_t>(c.dwHighDateTime) << 32) | c.dwLowDateTime;
}

FlPsSample NewSample() {
    FlPsSample s{};
    s.size = sizeof(s);
    return s;
}

double Mib(uint64_t bytes) {
    return static_cast<double>(bytes) / (1024.0 * 1024.0);
}

}    // namespace

TEST_CASE("the ABI the managed mirror reads") {
    REQUIRE(FlPsAbiVersion() == FL_PS_ABI_VERSION);
    REQUIRE(FlPsSampleSize() == sizeof(FlPsSample));
    REQUIRE(FlPsBuildId() != nullptr);
    REQUIRE(FlPsBuildId()[0] != '\0');
}

TEST_CASE("arguments are checked before anything is read") {
    void* reader = FlPsOpen();
    REQUIRE(reader != nullptr);
    const auto pid = static_cast<uint32_t>(GetCurrentProcessId());

    FlPsSample wrong = NewSample();
    wrong.size = sizeof(FlPsSample) - 8;
    REQUIRE(FlPsRead(reader, pid, 0, nullptr, &wrong) == FL_PS_BAD_SIZE);

    FlPsSample s = NewSample();
    REQUIRE(FlPsRead(nullptr, pid, 0, nullptr, &s) == FL_PS_BAD_ARGUMENT);
    REQUIRE(FlPsRead(reader, 0, 0, nullptr, &s) == FL_PS_BAD_ARGUMENT);
    REQUIRE(FlPsRead(reader, pid, 0, nullptr, nullptr) == FL_PS_BAD_ARGUMENT);
    REQUIRE(FlPsCollect(nullptr) == FL_PS_BAD_ARGUMENT);

    FlPsClose(reader);
    FlPsClose(nullptr);    // a null reader is a no-op, never a fault
}

TEST_CASE("this process's system memory, through a handle opened with the least right") {
    void*      reader = FlPsOpen();
    FlPsSample s = NewSample();
    REQUIRE(FlPsRead(reader, static_cast<uint32_t>(GetCurrentProcessId()), CreationTimeOfThisProcess(), nullptr, &s) ==
            FL_PS_OK);

    REQUIRE(s.size == sizeof(FlPsSample));
    REQUIRE(s.ramStatus == FL_PS_RAM_OK);
    REQUIRE((s.present & FL_PS_FIELD_WORKING_SET) != 0);
    REQUIRE((s.present & FL_PS_FIELD_COMMIT) != 0);
    REQUIRE(s.workingSetBytes > 0);
    REQUIRE(s.commitBytes > 0);
    REQUIRE(s.peakWorkingSetBytes >= s.workingSetBytes);
    if ((s.present & FL_PS_FIELD_PRIVATE_WS) != 0) {
        REQUIRE(s.privateWorkingSetBytes > 0);
        REQUIRE(s.privateWorkingSetBytes <= s.workingSetBytes);
    }
    // No collection has run on this reader: the video half says so instead of reading zero.
    REQUIRE((s.present & (FL_PS_FIELD_GPU_DEDICATED | FL_PS_FIELD_GPU_SHARED)) == 0);
    REQUIRE(s.gpuStatus != FL_PS_GPU_OK);
    std::printf("RAM: working set %.1f MiB, private working set %.1f MiB (%s), commit %.1f MiB\n",
                Mib(s.workingSetBytes), Mib(s.privateWorkingSetBytes),
                (s.present & FL_PS_FIELD_PRIVATE_WS) != 0 ? "EX2" : "N/A on this OS", Mib(s.commitBytes));
    FlPsClose(reader);
}

TEST_CASE("a held handle is used as given and left open") {
    HANDLE held = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, GetCurrentProcessId());
    REQUIRE(held != nullptr);
    void*      reader = FlPsOpen();
    FlPsSample s = NewSample();
    REQUIRE(FlPsRead(reader, static_cast<uint32_t>(GetCurrentProcessId()), 0, held, &s) == FL_PS_OK);
    REQUIRE(s.ramStatus == FL_PS_RAM_OK);
    REQUIRE(GetProcessId(held) == GetCurrentProcessId());    // still a valid handle: the reader did not close it
    CloseHandle(held);
    FlPsClose(reader);
}

TEST_CASE("a pid that names a process created at another time is refused, not read") {
    void*      reader = FlPsOpen();
    FlPsSample s = NewSample();
    REQUIRE(FlPsRead(reader, static_cast<uint32_t>(GetCurrentProcessId()), CreationTimeOfThisProcess() + 1, nullptr,
                     &s) == FL_PS_OK);
    REQUIRE(s.ramStatus == FL_PS_RAM_PID_REUSED);
    REQUIRE((s.present & (FL_PS_FIELD_WORKING_SET | FL_PS_FIELD_PRIVATE_WS | FL_PS_FIELD_COMMIT)) == 0);
    REQUIRE(s.workingSetBytes == 0);
    FlPsClose(reader);
}

TEST_CASE("a process that cannot be opened is a refusal with its reason") {
    void*      reader = FlPsOpen();
    FlPsSample s = NewSample();
    // Pids are multiples of four; this one is never handed out on a desktop machine.
    REQUIRE(FlPsRead(reader, 0xFFFFFFF0u, 0, nullptr, &s) == FL_PS_OK);
    REQUIRE(s.ramStatus == FL_PS_RAM_OPEN_DENIED);
    REQUIRE(s.ramError != 0);
    REQUIRE((s.present & FL_PS_FIELD_WORKING_SET) == 0);
    FlPsClose(reader);
}

TEST_CASE("video memory: a buffer this process allocates on a hardware adapter is counted as its dedicated memory") {
    void* reader = FlPsOpen();
    REQUIRE(reader != nullptr);
    const auto pid = static_cast<uint32_t>(GetCurrentProcessId());

    // The first adapter that is not a software rasteriser and has memory of its own. A CI runner has none.
    IDXGIFactory1* factory = nullptr;
    IDXGIAdapter1* adapter = nullptr;
    if (SUCCEEDED(CreateDXGIFactory1(__uuidof(IDXGIFactory1), reinterpret_cast<void**>(&factory)))) {
        for (UINT i = 0; factory->EnumAdapters1(i, &adapter) != DXGI_ERROR_NOT_FOUND; ++i) {
            DXGI_ADAPTER_DESC1 d{};
            adapter->GetDesc1(&d);
            if ((d.Flags & DXGI_ADAPTER_FLAG_SOFTWARE) == 0 && d.DedicatedVideoMemory > 0) {
                break;
            }
            adapter->Release();
            adapter = nullptr;
        }
        factory->Release();
    }

    ID3D11Device*        device = nullptr;
    ID3D11DeviceContext* context = nullptr;
    const bool           hardware =
        adapter != nullptr && SUCCEEDED(D3D11CreateDevice(adapter, D3D_DRIVER_TYPE_UNKNOWN, nullptr, 0, nullptr, 0,
                                                          D3D11_SDK_VERSION, &device, nullptr, &context));
    if (!hardware || FlPsCollect(reader) != FL_PS_OK) {
        std::printf(
            "BRANCH: NO_GPU_INSTANCE (no hardware adapter, or the GPU Process Memory counters did not collect)\n");
        if (device != nullptr) {
            context->Release();
            device->Release();
        }
        if (adapter != nullptr) {
            adapter->Release();
        }
        FlPsClose(reader);
        return;
    }

    FlPsSample before = NewSample();
    REQUIRE(FlPsRead(reader, pid, 0, nullptr, &before) == FL_PS_OK);

    constexpr UINT    kMib = 256;
    D3D11_BUFFER_DESC bd{};
    bd.ByteWidth = kMib * 1024u * 1024u;
    bd.Usage = D3D11_USAGE_DEFAULT;
    bd.BindFlags = D3D11_BIND_UNORDERED_ACCESS;
    bd.MiscFlags = D3D11_RESOURCE_MISC_BUFFER_ALLOW_RAW_VIEWS;
    ID3D11Buffer* buffer = nullptr;
    REQUIRE(SUCCEEDED(device->CreateBuffer(&bd, nullptr, &buffer)));
    D3D11_UNORDERED_ACCESS_VIEW_DESC uav{};
    uav.Format = DXGI_FORMAT_R32_TYPELESS;
    uav.ViewDimension = D3D11_UAV_DIMENSION_BUFFER;
    uav.Buffer.NumElements = bd.ByteWidth / 4;
    uav.Buffer.Flags = D3D11_BUFFER_UAV_FLAG_RAW;
    ID3D11UnorderedAccessView* view = nullptr;
    REQUIRE(SUCCEEDED(device->CreateUnorderedAccessView(buffer, &uav, &view)));
    const UINT fill[4] = {1, 2, 3, 4};
    context->ClearUnorderedAccessViewUint(view, fill);
    context->Flush();

    // The counter provider reads the kernel's accounting at each collection; allow it a few seconds to see the
    // allocation become resident.
    FlPsSample after = NewSample();
    for (int attempt = 0; attempt < 30; ++attempt) {
        REQUIRE(FlPsCollect(reader) == FL_PS_OK);
        REQUIRE(FlPsRead(reader, pid, 0, nullptr, &after) == FL_PS_OK);
        if ((after.present & FL_PS_FIELD_GPU_DEDICATED) != 0 &&
            after.gpuDedicatedBytes >= before.gpuDedicatedBytes + bd.ByteWidth * 98ull / 100ull) {
            break;
        }
        Sleep(100);
    }

    std::printf(
        "BRANCH: GPU_INSTANCE dedicated %.1f -> %.1f MiB (+%.1f, allocated %u), shared %.1f MiB, %u instance(s)\n",
        Mib(before.gpuDedicatedBytes), Mib(after.gpuDedicatedBytes),
        Mib(after.gpuDedicatedBytes - before.gpuDedicatedBytes), kMib, Mib(after.gpuSharedBytes), after.gpuInstances);
    REQUIRE((after.present & FL_PS_FIELD_GPU_DEDICATED) != 0);
    REQUIRE(after.gpuStatus == FL_PS_GPU_OK);
    REQUIRE(after.gpuInstances >= 1);
    REQUIRE(after.gpuDedicatedBytes >= before.gpuDedicatedBytes + bd.ByteWidth * 98ull / 100ull);

    view->Release();
    buffer->Release();
    context->Release();
    device->Release();
    adapter->Release();
    FlPsClose(reader);
}
