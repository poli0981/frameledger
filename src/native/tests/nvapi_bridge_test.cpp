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

#include "fl_nvapi_bridge.h"

TEST_CASE("the bridge's sizes and version are what the managed mirror will compare against", "[nvapi][abi]") {
    CHECK(FlNvAbiVersion() == FL_NV_ABI_VERSION);
    CHECK(FlNvSampleSize() == sizeof(FlNvSample));
    CHECK(FlNvNgxStateSize() == sizeof(FlNvNgxWords));
    CHECK(FlNvDriverProfileSize() == sizeof(FlNvDriverProfileRead));
    CHECK(sizeof(FlNvDriverProfileRead) == 16u + 256u + FL_NV_PROFILE_MAX_SETTINGS * 24u);
    CHECK(std::strlen(FlNvBuildId()) > 0u);
    CHECK(std::strlen(FlNvBuildId()) < 32u);
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
    for (size_t i = 0; i + 1u < sizeof(long_path) / sizeof(long_path[0]); ++i) {
        long_path[i] = u'a';
    }
    long_path[sizeof(long_path) / sizeof(long_path[0]) - 1u] = 0u;
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
