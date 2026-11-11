// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

// nvapi_bridge_test.cpp — the bridge's C ABI, green on both kinds of machine and saying which.
//
// A hosted runner has no NVIDIA driver: FlNvInit answers an NvAPI_Status and every read answers
// FL_NV_NOT_INITIALISED; the NGX query answers DEGRADED. The dev box takes the other branch. Both
// are the contract (18_GPU_VENDOR_APIS §L3: unavailability is a normal condition, never a throw),
// and the sizes the managed mirror compares against are the same on both.
#include <windows.h>

#include <catch2/catch_test_macros.hpp>
#include <cstdio>
#include <cstring>
#include <iterator>

#include "fl_nvapi_bridge.h"

TEST_CASE("the bridge's sizes and version are what the managed mirror will compare against", "[nvapi][abi]") {
    CHECK(FlNvAbiVersion() == FL_NV_ABI_VERSION);
    CHECK(FlNvSampleSize() == sizeof(FlNvSample));
    CHECK(FlNvNgxStateSize() == sizeof(FlNvNgxWords));
    CHECK(FlNvDriverProfileSize() == sizeof(FlNvDriverProfileRead));
    CHECK(sizeof(FlNvDriverProfileRead) == 16u + 256u + FL_NV_PROFILE_MAX_SETTINGS * 24u);
    CHECK(std::strlen(FlNvBuildId()) > 0u);
    CHECK(std::strlen(FlNvBuildId()) < 32u);
    // ABI 3 (D101): the sizes are spelled out as well as compared, so a field added on one side alone is seen here
    // before the managed mirror test sees it.
    CHECK(FlNvFactsSize() == sizeof(FlNvFacts));
    CHECK(sizeof(FlNvFacts) == 480u);
    CHECK(FlNvLiveSize() == sizeof(FlNvLive));
    CHECK(sizeof(FlNvLive) == 120u);
    CHECK(FlNvDisplaysSize() == sizeof(FlNvDisplays));
    CHECK(sizeof(FlNvDisplay) == 48u);
    CHECK(sizeof(FlNvDisplays) == 16u + FL_NV_MAX_DISPLAYS * 48u);
    CHECK(FlNvSessionDisplaySize() == sizeof(FlNvSessionDisplayRead));
    CHECK(sizeof(FlNvSessionDisplayRead) == 32u);
    CHECK(FlNvProfileEntrySize() == sizeof(FlNvProfileEntry));
    CHECK(sizeof(FlNvProfileEntry) == 24u + 128u);
    CHECK(FlNvProfileListSize() == sizeof(FlNvProfileList));
    CHECK(sizeof(FlNvProfileList) == 32u + 256u);
}

TEST_CASE("the advanced reads refuse a wrong size and a null pointer before anything is read",
          "[nvapi][abi][advanced]") {
    FlNvFacts f{};
    f.size = sizeof(FlNvFacts) + 4u;
    CHECK(FlNvReadFacts(&f) == FL_NV_BAD_SIZE);
    CHECK(FlNvReadFacts(nullptr) == FL_NV_BAD_ARGUMENT);
    FlNvLive l{};
    l.size = 1u;
    CHECK(FlNvReadLive(&l) == FL_NV_BAD_SIZE);
    CHECK(FlNvReadLive(nullptr) == FL_NV_BAD_ARGUMENT);
    FlNvDisplays d{};
    d.size = 1u;
    CHECK(FlNvReadDisplays(&d) == FL_NV_BAD_SIZE);
    CHECK(FlNvReadDisplays(nullptr) == FL_NV_BAD_ARGUMENT);
    FlNvSessionDisplayRead sd{};
    sd.size = 1u;
    CHECK(FlNvSessionDisplay("\\\\.\\DISPLAY1", 0u, &sd) == FL_NV_BAD_SIZE);
    sd.size = sizeof(FlNvSessionDisplayRead);
    CHECK(FlNvSessionDisplay(nullptr, 0u, &sd) == FL_NV_BAD_ARGUMENT);
    CHECK(FlNvSessionDisplay("\\\\.\\DISPLAY1", 0u, nullptr) == FL_NV_BAD_ARGUMENT);
    FlNvProfileList  list{};
    FlNvProfileEntry entries[2]{};
    list.size = 1u;
    CHECK(FlNvProfileSettings(nullptr, 0u, entries, 2u, &list) == FL_NV_BAD_SIZE);
    list.size = sizeof(FlNvProfileList);
    CHECK(FlNvProfileSettings(nullptr, 0u, nullptr, 2u, &list) == FL_NV_BAD_ARGUMENT);
    CHECK(FlNvProfileSettings(nullptr, 0u, entries, 2u, nullptr) == FL_NV_BAD_ARGUMENT);
    static uint16_t too_long[4096];
    for (size_t i = 0; i + 1u < std::size(too_long); ++i) {
        too_long[i] = u'a';
    }
    too_long[std::size(too_long) - 1u] = 0u;
    CHECK(FlNvProfileSettings(too_long, 1u, entries, 2u, &list) == FL_NV_BAD_ARGUMENT);
}

TEST_CASE("a wrong struct size is refused before anything is read", "[nvapi][abi]") {
    FlNvSample s{};
    s.size = sizeof(FlNvSample) - 4u;
    CHECK(FlNvReadSample(&s) == FL_NV_BAD_SIZE);
    CHECK(FlNvReadSample(nullptr) == FL_NV_BAD_ARGUMENT);
    FlNvNgxWords n{};
    n.size = 1u;
    CHECK(FlNvNgxState(0u, &n) == FL_NV_BAD_SIZE);
    const uint32_t        ids[1] = {0x10E41E01u};
    const uint16_t        path[] = {u'a', u'.', u'e', u'x', u'e', 0u};
    FlNvDriverProfileRead p{};
    p.size = 1u;
    CHECK(FlNvDriverProfile(path, ids, 1u, &p) == FL_NV_BAD_SIZE);
    p.size = sizeof(FlNvDriverProfileRead);
    CHECK(FlNvDriverProfile(nullptr, ids, 1u, &p) == FL_NV_BAD_ARGUMENT);
    CHECK(FlNvDriverProfile(path, nullptr, 1u, &p) == FL_NV_BAD_ARGUMENT);
    CHECK(FlNvDriverProfile(path, ids, FL_NV_PROFILE_MAX_SETTINGS + 1u, &p) == FL_NV_BAD_ARGUMENT);
    CHECK(FlNvDriverProfile(path, ids, 1u, nullptr) == FL_NV_BAD_ARGUMENT);
}

TEST_CASE("a path longer than NVAPI's string is refused, never truncated into another program's", "[nvapi][abi]") {
    static uint16_t long_path[4096];
    for (size_t i = 0; i + 1u < std::size(long_path); ++i) {
        long_path[i] = u'a';
    }
    long_path[std::size(long_path) - 1u] = 0u;
    FlNvDriverProfileRead p{};
    p.size = sizeof(FlNvDriverProfileRead);
    CHECK(FlNvDriverProfile(long_path, nullptr, 0u, &p) == FL_NV_BAD_ARGUMENT);
}

TEST_CASE("before FlNvInit every read says so, and the NGX query answers DEGRADED rather than failing",
          "[nvapi][abi]") {
    FlNvSample s{};
    s.size = sizeof(FlNvSample);
    CHECK(FlNvReadSample(&s) == FL_NV_NOT_INITIALISED);
    uint32_t v = 0;
    CHECK(FlNvDriverVersion(&v, nullptr, 0u) == FL_NV_NOT_INITIALISED);
    FlNvNgxWords n{};
    n.size = sizeof(FlNvNgxWords);
    REQUIRE(FlNvNgxState(GetCurrentProcessId(), &n) == FL_NV_OK);
    CHECK(n.status == FL_NV_NGX_DEGRADED);
    const uint32_t        ids[1] = {0x10E41E01u};
    const uint16_t        path[] = {u'C', u':', u'\\', u'a', u'.', u'e', u'x', u'e', 0u};
    FlNvDriverProfileRead p{};
    p.size = sizeof(FlNvDriverProfileRead);
    REQUIRE(FlNvDriverProfile(path, ids, 1u, &p) == FL_NV_OK);
    CHECK(p.status == FL_NV_PROFILE_DEGRADED);
    CHECK(p.count == 0u);
    // ABI 3: the reads that need a GPU say they have none; the two that answer in a struct say so there.
    FlNvFacts f{};
    f.size = sizeof(FlNvFacts);
    CHECK(FlNvReadFacts(&f) == FL_NV_NOT_INITIALISED);
    FlNvLive l{};
    l.size = sizeof(FlNvLive);
    CHECK(FlNvReadLive(&l) == FL_NV_NOT_INITIALISED);
    FlNvDisplays d{};
    d.size = sizeof(FlNvDisplays);
    CHECK(FlNvReadDisplays(&d) == FL_NV_NOT_INITIALISED);
    FlNvSessionDisplayRead sd{};
    sd.size = sizeof(FlNvSessionDisplayRead);
    REQUIRE(FlNvSessionDisplay("\\\\.\\DISPLAY1", GetCurrentProcessId(), &sd) == FL_NV_OK);
    CHECK(sd.nvapiStatus == FL_NV_NOT_INITIALISED);
    CHECK(sd.present == 0u);
    FlNvProfileList list{};
    list.size = sizeof(FlNvProfileList);
    REQUIRE(FlNvProfileSettings(nullptr, 0u, nullptr, 0u, &list) == FL_NV_OK);
    CHECK(list.status == FL_NV_PROFILE_DEGRADED);
    CHECK(list.count == 0u);
    CHECK(list.total == 0u);
}

TEST_CASE("FlNvInit takes one of two branches and says which; a sample never claims a field it did not read",
          "[nvapi][abi]") {
    const int32_t init = FlNvInit();
    if (init != FL_NV_OK && init != FL_NV_NO_GPU) {
        std::printf("BRANCH: DEGRADED (NvAPI_Initialize %d)\n", init);
        FlNvSample s{};
        s.size = sizeof(FlNvSample);
        CHECK(FlNvReadSample(&s) == FL_NV_NOT_INITIALISED);
        return;
    }
    std::printf("BRANCH: AVAILABLE (%s)\n", init == FL_NV_OK ? "a GPU" : "no GPU enumerated");
    FlNvSample s{};
    s.size = sizeof(FlNvSample);
    const int32_t read = FlNvReadSample(&s);
    if (init == FL_NV_NO_GPU) {
        CHECK(read == FL_NV_NO_GPU);
    } else {
        REQUIRE(read == FL_NV_OK);
        // A bit set is a value read; a bit clear is N/A. Both directions are pinned here on
        // the only fields whose absence has a stable meaning: a name always reads, and a load
        // percentage is within range when it does.
        CHECK((s.present & FL_NV_FIELD_NAME) != 0u);
        CHECK(std::strlen(s.name) > 0u);
        if ((s.present & FL_NV_FIELD_LOAD) != 0u) {
            CHECK(s.loadPct >= 0.0f);
            CHECK(s.loadPct <= 100.0f);
        }
        if ((s.present & FL_NV_FIELD_TEMP_CORE) != 0u) {
            CHECK(s.tempCoreC > -50.0f);
            CHECK(s.tempCoreC < 150.0f);
        }
        std::printf("  present=0x%X name=%s temp=%.0f load=%.0f vram=%.0f MB core=%.0f MHz mem=%.0f MHz fan=%.0f "
                    "throttle=0x%X pcie=x%u\n",
                    s.present, s.name, s.tempCoreC, s.loadPct, s.vramUsedMb, s.coreClockMhz, s.memClockMhz, s.fanRpm,
                    s.throttleReasons, s.pcieWidth);
        uint32_t version = 0;
        char     branch[64]{};
        CHECK(FlNvDriverVersion(&version, branch, sizeof(branch)) == FL_NV_OK);
        CHECK(version > 0u);
        std::printf("  driver %u.%02u (%s)\n", version / 100u, version % 100u, branch);
    }
    // The driver profile of an executable no profile names: the global one, read and never written. Every id answers
    // for itself — a value, or "set nowhere" — and the ids come back in the order asked.
    {
        wchar_t path[MAX_PATH]{};
        REQUIRE(GetModuleFileNameW(nullptr, path, MAX_PATH) > 0u);
        const uint32_t        ids[3] = {0x10E41E01u, 0x10E41DF3u, 0x104D6667u};
        FlNvDriverProfileRead p{};
        p.size = sizeof(FlNvDriverProfileRead);
        REQUIRE(FlNvDriverProfile(reinterpret_cast<const uint16_t*>(path), ids, 3u, &p) == FL_NV_OK);
        if (p.status == FL_NV_PROFILE_DEGRADED) {
            std::printf("  profile: DEGRADED (NvAPI %d)\n", p.nvapiStatus);
            CHECK(p.count == 0u);
        } else {
            CHECK(p.count == 3u);
            for (uint32_t i = 0; i < 3u; ++i) {
                CHECK(p.settings[i].id == ids[i]);
            }
            std::printf("  profile: %s, SR override status %d value %u location %u\n",
                        p.status == FL_NV_PROFILE_APPLICATION ? "APPLICATION" : "GLOBAL", p.settings[0].status,
                        p.settings[0].value, p.settings[0].location);
        }
    }
    // ABI 3 (D101): what "Advanced metrics" reads. Each field answers for itself, so what is pinned is only what has
    // one meaning on every card - a bit that is set carries a value in range - and the rest is printed: this output on
    // the owner's PC is the measurement 20_OPEN_QUESTIONS M12 asks for.
    if (init == FL_NV_OK) {
        FlNvFacts f{};
        f.size = sizeof(FlNvFacts);
        REQUIRE(FlNvReadFacts(&f) == FL_NV_OK);
        CHECK(f.gpuCount >= 1u);
        CHECK(f.gpuIndex == 0u);
        if ((f.present & FL_NV_FACT_VBIOS) != 0u) {
            CHECK(std::strlen(f.vbios) > 0u);
        }
        if ((f.present & FL_NV_FACT_MEMORY) != 0u) {
            CHECK(f.vramDedicatedAvailableBytes <= f.vramDedicatedBytes);
        }
        std::printf("  facts present=0x%05X gpus=%u type=%u system=%u external=%u bus=%u pci=%08X/%08X rev=%02X\n",
                    f.present, f.gpuCount, f.gpuType, f.systemType, f.external, f.busType, f.pciDeviceId,
                    f.pciSubSystemId, f.pciRevisionId);
        std::printf("  facts vbios=%s arch=0x%X/0x%X/0x%X cores=%u rt=%u tensor=%u ramBus=%u\n", f.vbios, f.archId,
                    f.archImplementation, f.archRevision, f.coreCount, f.rtCores, f.tensorCores, f.ramBusWidth);
        std::printf("  facts clocks base %.0f/%.0f boost %.0f/%.0f MHz tempMax=%.0f fb=%u/%u KB\n", f.clockBaseCoreMhz,
                    f.clockBaseMemoryMhz, f.clockBoostCoreMhz, f.clockBoostMemoryMhz, f.tempDefaultMaxC, f.fbPhysicalKb,
                    f.fbVirtualKb);
        std::printf("  facts vram dedicated=%llu available=%llu system=%llu shared=%llu MiB\n",
                    f.vramDedicatedBytes >> 20, f.vramDedicatedAvailableBytes >> 20, f.vramSystemBytes >> 20,
                    f.vramSharedBytes >> 20);
        std::printf("  facts driver flags=0x%X base=%s nvapi=%s chipset=%s %s\n", f.driverFlags, f.baseBranch,
                    f.interfaceVersion, f.chipsetVendor, f.chipsetName);

        FlNvLive l{};
        l.size = sizeof(FlNvLive);
        REQUIRE(FlNvReadLive(&l) == FL_NV_OK);
        if ((l.present & FL_NV_LIVE_MEMORY) != 0u) {
            CHECK(l.vramAvailableBytes <= l.vramDedicatedAvailableBytes);
        }
        if ((l.present & FL_NV_LIVE_LOAD_FRAMEBUFFER) != 0u) {
            CHECK(l.loadFramebufferPct >= 0.0f);
            CHECK(l.loadFramebufferPct <= 100.0f);
        }
        std::printf(
            "  live present=0x%03X vram available=%llu MiB evictions=%llu (%llu MiB) promotions=%llu (%llu MiB)\n",
            l.present, l.vramAvailableBytes >> 20, l.evictionCount, l.evictionsBytes >> 20, l.promotionCount,
            l.promotionsBytes >> 20);
        std::printf("  live load fb=%.0f vid=%.0f bus=%.0f clockVideo=%.0f pstate=%u board=%.0f psu=%.0f throttle=0x%X "
                    "encoder=%u/%u fps/%u us\n",
                    l.loadFramebufferPct, l.loadVideoPct, l.loadBusPct, l.clockVideoMhz, l.pstate, l.tempBoardC,
                    l.tempPowerSupplyC, l.throttleReasons, l.encoderSessions, l.encoderFps, l.encoderLatencyUs);

        FlNvDisplays d{};
        d.size = sizeof(FlNvDisplays);
        REQUIRE(FlNvReadDisplays(&d) == FL_NV_OK);
        CHECK(d.count <= FL_NV_MAX_DISPLAYS);
        std::printf("  displays status=%d count=%u\n", d.nvapiStatus, d.count);
        for (uint32_t i = 0; i < d.count && i < FL_NV_MAX_DISPLAYS; ++i) {
            const FlNvDisplay& x = d.displays[i];
            // The id is a handle of this boot's topology: printed as its ordinal, never as itself.
            std::printf("  display %u present=0x%X active=%u hdr=0x%X lum=%u/%u.%04u/%u caps=0x%X vrr=0x%X async=0x%X "
                        "interval=%u us\n",
                        i + 1u, x.present, x.active, x.hdrFlags, x.luminanceMax, x.luminanceMinTenThousandths / 10000u,
                        x.luminanceMinTenThousandths % 10000u, x.luminanceMaxFrameAverage, x.capsFlags, x.vrrFlags,
                        x.adaptiveSyncFlags, x.maxFrameIntervalUs);
        }
    }
    // The global profile, whole, and what this executable's own profile sets - which is nothing: no profile names a
    // test binary, so the answer is GLOBAL with nothing listed, and never the PC's settings filed under it.
    {
        static FlNvProfileEntry entries[512];
        FlNvProfileList         list{};
        list.size = sizeof(FlNvProfileList);
        REQUIRE(FlNvProfileSettings(nullptr, 0u, entries, 512u, &list) == FL_NV_OK);
        if (list.status == FL_NV_PROFILE_DEGRADED) {
            std::printf("  global profile: DEGRADED (NvAPI %d)\n", list.nvapiStatus);
            CHECK(list.count == 0u);
        } else {
            CHECK(list.status == FL_NV_PROFILE_GLOBAL);
            CHECK(list.count <= 512u);
            CHECK(list.count <= list.total);
            std::printf("  global profile: %u setting(s), %u listed\n", list.total, list.count);
            // Asking with no buffer learns the same total and writes nothing.
            FlNvProfileList counted{};
            counted.size = sizeof(FlNvProfileList);
            REQUIRE(FlNvProfileSettings(nullptr, 0u, nullptr, 0u, &counted) == FL_NV_OK);
            CHECK(counted.count == 0u);
            CHECK(counted.total == list.total);
        }
        wchar_t self[MAX_PATH]{};
        REQUIRE(GetModuleFileNameW(nullptr, self, MAX_PATH) > 0u);
        FlNvProfileList own{};
        own.size = sizeof(FlNvProfileList);
        REQUIRE(FlNvProfileSettings(reinterpret_cast<const uint16_t*>(self), 1u, entries, 512u, &own) == FL_NV_OK);
        if (own.status != FL_NV_PROFILE_DEGRADED) {
            CHECK(own.status == FL_NV_PROFILE_GLOBAL);
            CHECK(own.count == 0u);
            CHECK(own.total == 0u);
        }
    }
    // This process renders nothing through NGX, so the only honest NGX answers are the two that say so.
    FlNvNgxWords n{};
    n.size = sizeof(FlNvNgxWords);
    REQUIRE(FlNvNgxState(GetCurrentProcessId(), &n) == FL_NV_OK);
    CHECK((n.status == FL_NV_NGX_UNANSWERED || n.status == FL_NV_NGX_DEGRADED));
    // Reference counting: a second init is a second reference, and the reads survive the first shutdown. (Until
    // 2026-09-25 this shut down twice before the read — two references, two releases, nothing left to read with — and
    // the failure was invisible: ctest passed on the branch line alone. See CMakeLists.txt.)
    const int32_t again = FlNvInit();
    CHECK((again == FL_NV_OK || again == FL_NV_NO_GPU));
    FlNvShutdown();
    const int32_t afterOne = FlNvReadSample(&s);
    CHECK((afterOne == FL_NV_OK || afterOne == FL_NV_NO_GPU));
    FlNvShutdown();
    CHECK(FlNvReadSample(&s) == FL_NV_NOT_INITIALISED);
}
