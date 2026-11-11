// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

// fl_nvapi_bridge.cpp — see fl_nvapi_bridge.h.
//
// EVERY NVAPI CALL HERE IS A READ. 18_GPU_VENDOR_APIS §Runtime policy: the libraries behind the
// layers can also set clocks, fan curves and power limits; FrameLedger never calls a setter, and
// this translation unit is the whole of what links nvapi64.lib into a shipped binary, so the claim
// is checkable by reading one file - and, since ABI 3, checked: tools/nvapi-readonly-check.ps1 compares
// every NvAPI_* call below with tools/nvapi-allowed-calls.txt on every build (D108).
#define FL_NV_BUILDING
#include "fl_nvapi_bridge.h"

#include <windows.h>

#include <atomic>
#include <cstring>

#include "nvapi.h"

namespace {

// Initialise/shutdown are serialised; reads are independent NVAPI calls on a handle that is set
// once and never changes while the count is above zero.
CRITICAL_SECTION    g_lock;
bool                g_lockInit = false;
int                 g_refs = 0;
NvPhysicalGpuHandle g_gpu = nullptr;
bool                g_haveGpu = false;
uint32_t            g_gpuCount = 0;
std::atomic<bool>   g_ready{false};

void EnsureLock() {
    // DllMain is not used (no work under the loader lock); the first caller creates the section.
    // A race between two first callers is not a shape the managed side produces — the facade is
    // constructed once — and the section is never deleted, so a second init would only leak one.
    if (!g_lockInit) {
        InitializeCriticalSection(&g_lock);
        g_lockInit = true;
    }
}

struct Locked {
    Locked() {
        EnsureLock();
        EnterCriticalSection(&g_lock);
    }
    ~Locked() { LeaveCriticalSection(&g_lock); }
    Locked(const Locked&) = delete;
    Locked& operator=(const Locked&) = delete;
};

void ReadThermal(FlNvSample& s) {
    NV_GPU_THERMAL_SETTINGS t{};
    t.version = NV_GPU_THERMAL_SETTINGS_VER;
    if (NvAPI_GPU_GetThermalSettings(g_gpu, NVAPI_THERMAL_TARGET_ALL, &t) != NVAPI_OK) {
        return;
    }
    for (NvU32 i = 0; i < t.count && i < NVAPI_MAX_THERMAL_SENSORS_PER_GPU; ++i) {
        if (t.sensor[i].target == NVAPI_THERMAL_TARGET_GPU && (s.present & FL_NV_FIELD_TEMP_CORE) == 0u) {
            s.tempCoreC = static_cast<float>(t.sensor[i].currentTemp);
            s.present |= FL_NV_FIELD_TEMP_CORE;
        } else if (t.sensor[i].target == NVAPI_THERMAL_TARGET_MEMORY && (s.present & FL_NV_FIELD_TEMP_MEMORY) == 0u) {
            s.tempMemoryC = static_cast<float>(t.sensor[i].currentTemp);
            s.present |= FL_NV_FIELD_TEMP_MEMORY;
        }
    }
}

void ReadLoad(FlNvSample& s) {
    NV_GPU_DYNAMIC_PSTATES_INFO_EX p{};
    p.version = NV_GPU_DYNAMIC_PSTATES_INFO_EX_VER;
    if (NvAPI_GPU_GetDynamicPstatesInfoEx(g_gpu, &p) != NVAPI_OK) {
        return;
    }
    // Index 0 is the GPU domain: nvapi.h documents utilization[NVAPI_GPU_UTILIZATION_DOMAIN_GPU] and
    // defines no such constant in this vintage of the header, so the documented index is spelled out.
    constexpr NvU32 kGpuDomain = 0;
    if (p.utilization[kGpuDomain].bIsPresent != 0u) {
        s.loadPct = static_cast<float>(p.utilization[kGpuDomain].percentage);
        s.present |= FL_NV_FIELD_LOAD;
    }
}

void ReadClocks(FlNvSample& s) {
    NV_GPU_CLOCK_FREQUENCIES c{};
    c.version = NV_GPU_CLOCK_FREQUENCIES_VER;
    c.ClockType = NV_GPU_CLOCK_FREQUENCIES_CURRENT_FREQ;
    if (NvAPI_GPU_GetAllClockFrequencies(g_gpu, &c) != NVAPI_OK) {
        return;
    }
    if (c.domain[NVAPI_GPU_PUBLIC_CLOCK_GRAPHICS].bIsPresent != 0u) {
        s.coreClockMhz = static_cast<float>(c.domain[NVAPI_GPU_PUBLIC_CLOCK_GRAPHICS].frequency) / 1000.0f;
        s.present |= FL_NV_FIELD_CORE_CLOCK;
    }
    if (c.domain[NVAPI_GPU_PUBLIC_CLOCK_MEMORY].bIsPresent != 0u) {
        s.memClockMhz = static_cast<float>(c.domain[NVAPI_GPU_PUBLIC_CLOCK_MEMORY].frequency) / 1000.0f;
        s.present |= FL_NV_FIELD_MEM_CLOCK;
    }
}

void ReadExtras(FlNvSample& s) {
    NvU32 v = 0;
    if (NvAPI_GPU_GetTachReading(g_gpu, &v) == NVAPI_OK) {
        s.fanRpm = static_cast<float>(v);
        s.present |= FL_NV_FIELD_FAN;
    }
    v = 0;
    if (NvAPI_GPU_GetPerfDecreaseInfo(g_gpu, &v) == NVAPI_OK) {
        s.throttleReasons = v;
        s.present |= FL_NV_FIELD_THROTTLE;
    }
    v = 0;
    if (NvAPI_GPU_GetCurrentPCIEDownstreamWidth(g_gpu, &v) == NVAPI_OK && v != 0u) {
        s.pcieWidth = v;
        s.present |= FL_NV_FIELD_PCIE_WIDTH;
    }
    NvAPI_ShortString name{};
    if (NvAPI_GPU_GetFullName(g_gpu, name) == NVAPI_OK) {
        std::memcpy(s.name, name, sizeof(s.name) - 1);
        s.name[sizeof(s.name) - 1] = '\0';
        s.present |= FL_NV_FIELD_NAME;
    }
}

// ---- ABI 3: what "Advanced metrics" reads (D101). Every call below is a read; tools/nvapi-readonly-check.ps1 holds
// the list of them (D108), and no call that returns an identifier is on it (D104).
// -------------------------------------

// The driver's own fixed strings: bounded by the buffer, not by a terminator the driver might not have written.
void CopyShort(char (&to)[NVAPI_SHORT_STRING_MAX], const char* from) {
    const size_t n = strnlen(from, NVAPI_SHORT_STRING_MAX);
    const size_t copy = n < sizeof(to) - 1u ? n : sizeof(to) - 1u;
    std::memcpy(to, from, copy);
    to[copy] = '\0';
}

void CopyWide(uint16_t* to, size_t capacity, const NvU16* from, size_t fromCapacity) {
    size_t i = 0;
    for (; i + 1u < capacity && i < fromCapacity && from[i] != 0u; ++i) {
        to[i] = from[i];
    }
    to[i] = 0u;
}

void ReadFactIdentity(FlNvFacts& f) {
    NV_GPU_TYPE type = NV_SYSTEM_TYPE_GPU_UNKNOWN;
    if (NvAPI_GPU_GetGPUType(g_gpu, &type) == NVAPI_OK) {
        f.gpuType = static_cast<uint32_t>(type);
        f.present |= FL_NV_FACT_GPU_TYPE;
    }
    NV_SYSTEM_TYPE system = NV_SYSTEM_TYPE_UNKNOWN;
    if (NvAPI_GPU_GetSystemType(g_gpu, &system) == NVAPI_OK) {
        f.systemType = static_cast<uint32_t>(system);
        f.present |= FL_NV_FACT_SYSTEM_TYPE;
    }
    NV_GPU_INFO info{};
    info.version = NV_GPU_INFO_VER;
    if (NvAPI_GPU_GetGPUInfo(g_gpu, &info) == NVAPI_OK) {
        f.external = info.bIsExternalGpu != 0u ? 1u : 0u;
        f.rtCores = info.rayTracingCores;
        f.tensorCores = info.tensorCores;
        f.present |= FL_NV_FACT_GPU_INFO;
    }
    NV_GPU_BUS_TYPE bus = NVAPI_GPU_BUS_TYPE_UNDEFINED;
    if (NvAPI_GPU_GetBusType(g_gpu, &bus) == NVAPI_OK) {
        f.busType = static_cast<uint32_t>(bus);
        f.present |= FL_NV_FACT_BUS_TYPE;
    }
    NvU32 device = 0;
    NvU32 subSystem = 0;
    NvU32 revision = 0;
    NvU32 extDevice = 0;
    if (NvAPI_GPU_GetPCIIdentifiers(g_gpu, &device, &subSystem, &revision, &extDevice) == NVAPI_OK) {
        f.pciDeviceId = device;
        f.pciSubSystemId = subSystem;
        f.pciRevisionId = revision;
        f.pciExtDeviceId = extDevice;
        f.present |= FL_NV_FACT_PCI_IDS;
    }
    NvAPI_ShortString bios{};
    if (NvAPI_GPU_GetVbiosVersionString(g_gpu, bios) == NVAPI_OK) {
        CopyShort(f.vbios, bios);
        f.present |= FL_NV_FACT_VBIOS;
    }
    NV_GPU_ARCH_INFO arch{};
    arch.version = NV_GPU_ARCH_INFO_VER;
    if (NvAPI_GPU_GetArchInfo(g_gpu, &arch) == NVAPI_OK) {
        f.archId = arch.architecture;
        f.archImplementation = arch.implementation;
        f.archRevision = arch.revision;
        f.present |= FL_NV_FACT_ARCH;
    }
    NvU32 v = 0;
    if (NvAPI_GPU_GetGpuCoreCount(g_gpu, &v) == NVAPI_OK) {
        f.coreCount = v;
        f.present |= FL_NV_FACT_CORE_COUNT;
    }
    v = 0;
    if (NvAPI_GPU_GetRamBusWidth(g_gpu, &v) == NVAPI_OK) {
        f.ramBusWidth = v;
        f.present |= FL_NV_FACT_RAM_BUS_WIDTH;
    }
    v = 0;
    if (NvAPI_GPU_GetPhysicalFrameBufferSize(g_gpu, &v) == NVAPI_OK) {
        f.fbPhysicalKb = v;
        f.present |= FL_NV_FACT_FB_PHYSICAL;
    }
    v = 0;
    if (NvAPI_GPU_GetVirtualFrameBufferSize(g_gpu, &v) == NVAPI_OK) {
        f.fbVirtualKb = v;
        f.present |= FL_NV_FACT_FB_VIRTUAL;
    }
}

// The clocks the card is specified at - the BASE and BOOST queries of the call ReadClocks makes for the current ones.
void ReadFactClocks(FlNvFacts& f) {
    NV_GPU_CLOCK_FREQUENCIES base{};
    base.version = NV_GPU_CLOCK_FREQUENCIES_VER;
    base.ClockType = NV_GPU_CLOCK_FREQUENCIES_BASE_CLOCK;
    if (NvAPI_GPU_GetAllClockFrequencies(g_gpu, &base) == NVAPI_OK) {
        if (base.domain[NVAPI_GPU_PUBLIC_CLOCK_GRAPHICS].bIsPresent != 0u) {
            f.clockBaseCoreMhz = static_cast<float>(base.domain[NVAPI_GPU_PUBLIC_CLOCK_GRAPHICS].frequency) / 1000.0f;
            f.present |= FL_NV_FACT_CLOCK_BASE_CORE;
        }
        if (base.domain[NVAPI_GPU_PUBLIC_CLOCK_MEMORY].bIsPresent != 0u) {
            f.clockBaseMemoryMhz = static_cast<float>(base.domain[NVAPI_GPU_PUBLIC_CLOCK_MEMORY].frequency) / 1000.0f;
            f.present |= FL_NV_FACT_CLOCK_BASE_MEMORY;
        }
    }
    NV_GPU_CLOCK_FREQUENCIES boost{};
    boost.version = NV_GPU_CLOCK_FREQUENCIES_VER;
    boost.ClockType = NV_GPU_CLOCK_FREQUENCIES_BOOST_CLOCK;
    if (NvAPI_GPU_GetAllClockFrequencies(g_gpu, &boost) == NVAPI_OK) {
        if (boost.domain[NVAPI_GPU_PUBLIC_CLOCK_GRAPHICS].bIsPresent != 0u) {
            f.clockBoostCoreMhz = static_cast<float>(boost.domain[NVAPI_GPU_PUBLIC_CLOCK_GRAPHICS].frequency) / 1000.0f;
            f.present |= FL_NV_FACT_CLOCK_BOOST_CORE;
        }
        if (boost.domain[NVAPI_GPU_PUBLIC_CLOCK_MEMORY].bIsPresent != 0u) {
            f.clockBoostMemoryMhz = static_cast<float>(boost.domain[NVAPI_GPU_PUBLIC_CLOCK_MEMORY].frequency) / 1000.0f;
            f.present |= FL_NV_FACT_CLOCK_BOOST_MEMORY;
        }
    }
}

void ReadFactThermalAndMemory(FlNvFacts& f) {
    NV_GPU_THERMAL_SETTINGS t{};
    t.version = NV_GPU_THERMAL_SETTINGS_VER;
    if (NvAPI_GPU_GetThermalSettings(g_gpu, NVAPI_THERMAL_TARGET_ALL, &t) == NVAPI_OK) {
        for (NvU32 i = 0; i < t.count && i < NVAPI_MAX_THERMAL_SENSORS_PER_GPU; ++i) {
            if (t.sensor[i].target == NVAPI_THERMAL_TARGET_GPU) {
                f.tempDefaultMaxC = static_cast<float>(t.sensor[i].defaultMaxTemp);
                f.present |= FL_NV_FACT_TEMP_DEFAULT_MAX;
                break;
            }
        }
    }
    // NvAPI_GPU_GetMemoryInfoEx, not the deprecated NvAPI_GPU_GetMemoryInfo, which fails the build under /W4 /WX.
    NV_GPU_MEMORY_INFO_EX m{};
    m.version = NV_GPU_MEMORY_INFO_EX_VER;
    if (NvAPI_GPU_GetMemoryInfoEx(g_gpu, &m) == NVAPI_OK) {
        f.vramDedicatedBytes = m.dedicatedVideoMemory;
        f.vramDedicatedAvailableBytes = m.availableDedicatedVideoMemory;
        f.vramSystemBytes = m.systemVideoMemory;
        f.vramSharedBytes = m.sharedSystemMemory;
        f.present |= FL_NV_FACT_MEMORY;
    }
}

// What is true of the driver and the PC rather than of one card: none of these takes a GPU handle.
void ReadFactSystem(FlNvFacts& f) {
    NV_DISPLAY_DRIVER_INFO d{};
    d.version = NV_DISPLAY_DRIVER_INFO_VER;
    if (NvAPI_SYS_GetDisplayDriverInfo(&d) == NVAPI_OK) {
        f.driverFlags = (d.bIsDCHDriver != 0u ? FL_NV_DRIVER_DCH : 0u) |
                        (d.bIsNVIDIAStudioPackage != 0u ? FL_NV_DRIVER_STUDIO : 0u) |
                        (d.bIsNVIDIAGameReadyPackage != 0u ? FL_NV_DRIVER_GAME_READY : 0u) |
                        (d.bIsNVIDIARTXProductionBranchPackage != 0u ? FL_NV_DRIVER_RTX_PRODUCTION : 0u) |
                        (d.bIsNVIDIARTXNewFeatureBranchPackage != 0u ? FL_NV_DRIVER_RTX_NEW_FEATURE : 0u);
        CopyShort(f.baseBranch, d.szBuildBaseBranch);
        f.present |= FL_NV_FACT_DRIVER_INFO;
    }
    NvAPI_ShortString version{};
    if (NvAPI_GetInterfaceVersionString(version) == NVAPI_OK) {
        CopyShort(f.interfaceVersion, version);
        f.present |= FL_NV_FACT_INTERFACE_VERSION;
    }
    NV_CHIPSET_INFO c{};
    c.version = NV_CHIPSET_INFO_VER;
    if (NvAPI_SYS_GetChipSetInfo(&c) == NVAPI_OK) {
        CopyShort(f.chipsetVendor, c.szVendorName);
        CopyShort(f.chipsetName, c.szChipsetName);
        f.present |= FL_NV_FACT_CHIPSET;
    }
}

void ReadLiveMemory(FlNvLive& l) {
    NV_GPU_MEMORY_INFO_EX m{};
    m.version = NV_GPU_MEMORY_INFO_EX_VER;
    if (NvAPI_GPU_GetMemoryInfoEx(g_gpu, &m) != NVAPI_OK) {
        return;
    }
    l.vramAvailableBytes = m.curAvailableDedicatedVideoMemory;
    l.vramDedicatedAvailableBytes = m.availableDedicatedVideoMemory;
    l.evictionsBytes = m.dedicatedVideoMemoryEvictionsSize;
    l.evictionCount = m.dedicatedVideoMemoryEvictionCount;
    l.promotionsBytes = m.dedicatedVideoMemoryPromotionsSize;
    l.promotionCount = m.dedicatedVideoMemoryPromotionCount;
    l.present |= FL_NV_LIVE_MEMORY;
}

void ReadLiveLoadAndClock(FlNvLive& l) {
    NV_GPU_DYNAMIC_PSTATES_INFO_EX p{};
    p.version = NV_GPU_DYNAMIC_PSTATES_INFO_EX_VER;
    if (NvAPI_GPU_GetDynamicPstatesInfoEx(g_gpu, &p) == NVAPI_OK) {
        // nvapi.h names the four domains in this order - graphic engine, frame buffer, video engine, bus interface -
        // and, as ReadLoad notes for index 0, defines no constant for them in this vintage of the header.
        constexpr NvU32 kFrameBuffer = 1;
        constexpr NvU32 kVideo = 2;
        constexpr NvU32 kBus = 3;
        if (p.utilization[kFrameBuffer].bIsPresent != 0u) {
            l.loadFramebufferPct = static_cast<float>(p.utilization[kFrameBuffer].percentage);
            l.present |= FL_NV_LIVE_LOAD_FRAMEBUFFER;
        }
        if (p.utilization[kVideo].bIsPresent != 0u) {
            l.loadVideoPct = static_cast<float>(p.utilization[kVideo].percentage);
            l.present |= FL_NV_LIVE_LOAD_VIDEO;
        }
        if (p.utilization[kBus].bIsPresent != 0u) {
            l.loadBusPct = static_cast<float>(p.utilization[kBus].percentage);
            l.present |= FL_NV_LIVE_LOAD_BUS;
        }
    }
    NV_GPU_CLOCK_FREQUENCIES c{};
    c.version = NV_GPU_CLOCK_FREQUENCIES_VER;
    c.ClockType = NV_GPU_CLOCK_FREQUENCIES_CURRENT_FREQ;
    if (NvAPI_GPU_GetAllClockFrequencies(g_gpu, &c) == NVAPI_OK &&
        c.domain[NVAPI_GPU_PUBLIC_CLOCK_VIDEO].bIsPresent != 0u) {
        l.clockVideoMhz = static_cast<float>(c.domain[NVAPI_GPU_PUBLIC_CLOCK_VIDEO].frequency) / 1000.0f;
        l.present |= FL_NV_LIVE_CLOCK_VIDEO;
    }
}

void ReadLiveState(FlNvLive& l) {
    NV_GPU_PERF_PSTATE_ID pstate = NVAPI_GPU_PERF_PSTATE_UNDEFINED;
    if (NvAPI_GPU_GetCurrentPstate(g_gpu, &pstate) == NVAPI_OK && pstate != NVAPI_GPU_PERF_PSTATE_UNDEFINED) {
        l.pstate = static_cast<uint32_t>(pstate);
        l.present |= FL_NV_LIVE_PSTATE;
    }
    NV_GPU_THERMAL_SETTINGS t{};
    t.version = NV_GPU_THERMAL_SETTINGS_VER;
    if (NvAPI_GPU_GetThermalSettings(g_gpu, NVAPI_THERMAL_TARGET_ALL, &t) == NVAPI_OK) {
        for (NvU32 i = 0; i < t.count && i < NVAPI_MAX_THERMAL_SENSORS_PER_GPU; ++i) {
            if (t.sensor[i].target == NVAPI_THERMAL_TARGET_BOARD && (l.present & FL_NV_LIVE_TEMP_BOARD) == 0u) {
                l.tempBoardC = static_cast<float>(t.sensor[i].currentTemp);
                l.present |= FL_NV_LIVE_TEMP_BOARD;
            } else if (t.sensor[i].target == NVAPI_THERMAL_TARGET_POWER_SUPPLY &&
                       (l.present & FL_NV_LIVE_TEMP_POWER_SUPPLY) == 0u) {
                l.tempPowerSupplyC = static_cast<float>(t.sensor[i].currentTemp);
                l.present |= FL_NV_LIVE_TEMP_POWER_SUPPLY;
            }
        }
    }
    NvU32 reasons = 0;
    if (NvAPI_GPU_GetPerfDecreaseInfo(g_gpu, &reasons) == NVAPI_OK) {
        l.throttleReasons = reasons;
        l.present |= FL_NV_LIVE_THROTTLE;
    }
    // The count and the averages of every program's encoder sessions. NvAPI_GPU_GetEncoderSessionsInfo, which lists
    // WHICH programs, is never called (D104).
    NV_ENCODER_STATISTICS e{};
    // VER1 by name: this vintage of nvapi.h misspells the unversioned macro (NNV_ENCODER_STATISTICS_VER).
    e.version = NV_ENCODER_STATISTICS_VER1;
    if (NvAPI_GPU_GetEncoderStatistics(g_gpu, &e) == NVAPI_OK) {
        l.encoderSessions = e.sessionsCount;
        l.encoderFps = e.averageFps;
        l.encoderLatencyUs = e.averageLatency;
        l.present |= FL_NV_LIVE_ENCODER;
    }
}

uint32_t VrrFlagsOf(const NV_GET_VRR_INFO& v) {
    return (v.bIsVRREnabled != 0u ? FL_NV_VRR_ENABLED : 0u) | (v.bIsVRRPossible != 0u ? FL_NV_VRR_POSSIBLE : 0u) |
           (v.bIsVRRRequested != 0u ? FL_NV_VRR_REQUESTED : 0u) |
           (v.bIsVRRIndicatorEnabled != 0u ? FL_NV_VRR_INDICATOR : 0u) |
           (v.bIsDisplayInVRRMode != 0u ? FL_NV_VRR_DISPLAY_MODE : 0u);
}

// What one display can do. The EDID is not read: it carries the monitor's serial number (D104).
void ReadDisplay(NvU32 displayId, FlNvDisplay& d) {
    NV_HDR_CAPABILITIES h{};
    h.version = NV_HDR_CAPABILITIES_VER;
    if (NvAPI_Disp_GetHdrCapabilities(displayId, &h) == NVAPI_OK) {
        d.hdrFlags = (h.isST2084EotfSupported != 0u ? FL_NV_HDR_ST2084 : 0u) |
                     (h.isTraditionalHdrGammaSupported != 0u ? FL_NV_HDR_TRADITIONAL_HDR : 0u) |
                     (h.isEdrSupported != 0u ? FL_NV_HDR_EDR : 0u) |
                     (h.isTraditionalSdrGammaSupported != 0u ? FL_NV_HDR_TRADITIONAL_SDR : 0u) |
                     (h.isDolbyVisionSupported != 0u ? FL_NV_HDR_DOLBY_VISION : 0u) |
                     (h.isHdr10PlusSupported != 0u ? FL_NV_HDR_HDR10_PLUS : 0u) |
                     (h.isHdr10PlusGamingSupported != 0u ? FL_NV_HDR_HDR10_PLUS_GAMING : 0u) |
                     (h.isNvidiaCertifiedDisplay != 0u ? FL_NV_HDR_NVIDIA_CERTIFIED : 0u);
        d.luminanceMax = h.display_data.desired_content_max_luminance;
        d.luminanceMinTenThousandths = h.display_data.desired_content_min_luminance;
        d.luminanceMaxFrameAverage = h.display_data.desired_content_max_frame_average_luminance;
        d.present |= FL_NV_DISPLAY_HDR;
    }
    NV_MONITOR_CAPABILITIES m{};
    m.version = NV_MONITOR_CAPABILITIES_VER;
    m.size = static_cast<NvU16>(sizeof(m));
    m.infoType = NV_MONITOR_CAPS_TYPE_GENERIC;
    if (NvAPI_DISP_GetMonitorCapabilities(displayId, &m) == NVAPI_OK && m.bIsValidInfo != 0u) {
        d.capsFlags = (m.data.caps.supportVRR != 0u ? FL_NV_CAPS_VRR : 0u) |
                      (m.data.caps.supportULMB != 0u ? FL_NV_CAPS_ULMB : 0u) |
                      (m.data.caps.isTrueGsync != 0u ? FL_NV_CAPS_TRUE_GSYNC : 0u) |
                      (m.data.caps.isRLACapable != 0u ? FL_NV_CAPS_RLA : 0u) |
                      (m.data.caps.currentlyCapableOfVRR != 0u ? FL_NV_CAPS_VRR_NOW : 0u) |
                      (m.data.caps.isBasicVRR != 0u ? FL_NV_CAPS_BASIC_VRR : 0u);
        d.present |= FL_NV_DISPLAY_CAPS;
    }
    NV_GET_VRR_INFO v{};
    v.version = NV_GET_VRR_INFO_VER;
    if (NvAPI_Disp_GetVRRInfo(displayId, &v) == NVAPI_OK) {
        d.vrrFlags = VrrFlagsOf(v);
        d.present |= FL_NV_DISPLAY_VRR;
    }
    // The flags and the interval only: the last flip's count and timestamp mean nothing read once a second.
    NV_GET_ADAPTIVE_SYNC_DATA a{};
    a.version = NV_GET_ADAPTIVE_SYNC_DATA_VER;
    if (NvAPI_DISP_GetAdaptiveSyncData(displayId, &a) == NVAPI_OK) {
        d.adaptiveSyncFlags = (a.bDisableAdaptiveSync != 0u ? FL_NV_ADAPTIVE_SYNC_DISABLED : 0u) |
                              (a.bDisableFrameSplitting != 0u ? FL_NV_ADAPTIVE_SYNC_NO_FRAME_SPLITTING : 0u);
        d.maxFrameIntervalUs = a.maxFrameInterval;
        d.present |= FL_NV_DISPLAY_ADAPTIVE_SYNC;
    }
}

// NVDRS_SETTING carries a 2048-character name and two unions sized for a binary value - about 12 KB each - so the
// enumeration reads a few at a time into this buffer rather than onto the stack. Only ever touched under g_lock.
constexpr NvU32 kEnumBatch = 8;
NVDRS_SETTING   g_enum[kEnumBatch];

}    // namespace

extern "C" {

FL_NV_API int32_t FlNvInit(void) {
    Locked lock;
    if (g_refs > 0) {
        ++g_refs;
        return g_haveGpu ? FL_NV_OK : FL_NV_NO_GPU;
    }
    const NvAPI_Status init = NvAPI_Initialize();
    if (init != NVAPI_OK) {
        return static_cast<int32_t>(init);
    }
    NvPhysicalGpuHandle handles[NVAPI_MAX_PHYSICAL_GPUS]{};
    NvU32               count = 0;
    g_haveGpu = NvAPI_EnumPhysicalGPUs(handles, &count) == NVAPI_OK && count > 0u;
    g_gpu = g_haveGpu ? handles[0] : nullptr;
    g_gpuCount = g_haveGpu ? count : 0u;
    g_refs = 1;
    g_ready.store(true, std::memory_order_release);
    return g_haveGpu ? FL_NV_OK : FL_NV_NO_GPU;
}

FL_NV_API void FlNvShutdown(void) {
    Locked lock;
    if (g_refs == 0) {
        return;
    }
    if (--g_refs == 0) {
        g_ready.store(false, std::memory_order_release);
        g_gpu = nullptr;
        g_haveGpu = false;
        g_gpuCount = 0u;
        NvAPI_Unload();
    }
}

FL_NV_API int32_t FlNvReadSample(FlNvSample* out) {
    if (out == nullptr) {
        return FL_NV_BAD_ARGUMENT;
    }
    if (out->size != sizeof(FlNvSample)) {
        return FL_NV_BAD_SIZE;
    }
    if (!g_ready.load(std::memory_order_acquire)) {
        return FL_NV_NOT_INITIALISED;
    }
    Locked lock;
    if (!g_haveGpu) {
        return FL_NV_NO_GPU;
    }
    FlNvSample s{};
    s.size = sizeof(FlNvSample);
    ReadThermal(s);
    ReadLoad(s);
    ReadClocks(s);
    ReadExtras(s);
    *out = s;
    return FL_NV_OK;
}

FL_NV_API int32_t FlNvNgxState(uint32_t pid, FlNvNgxWords* out) {
    if (out == nullptr) {
        return FL_NV_BAD_ARGUMENT;
    }
    if (out->size != sizeof(FlNvNgxWords)) {
        return FL_NV_BAD_SIZE;
    }
    FlNvNgxWords r{};
    r.size = sizeof(FlNvNgxWords);
    if (!g_ready.load(std::memory_order_acquire)) {
        r.status = FL_NV_NGX_DEGRADED;
        r.nvapiStatus = FL_NV_NOT_INITIALISED;
        *out = r;
        return FL_NV_OK;
    }
    Locked            lock;
    NvU32             driver = 0;
    NvAPI_ShortString branch{};
    if (NvAPI_SYS_GetDriverAndBranchVersion(&driver, branch) == NVAPI_OK) {
        r.driver = driver;
    }
    NV_NGX_DLSS_OVERRIDE_GET_STATE_PARAMS params{};
    params.version = NV_NGX_DLSS_OVERRIDE_GET_STATE_PARAMS_VER;
    params.processIdentifier = static_cast<NvU32>(pid);
    const NvAPI_Status status = NvAPI_NGX_GetNGXOverrideState(&params);
    r.nvapiStatus = static_cast<int32_t>(status);
    if (status != NVAPI_OK) {
        r.status = FL_NV_NGX_UNANSWERED;
        *out = r;
        return FL_NV_OK;
    }
    r.status = FL_NV_NGX_ANSWERED;
    r.sr = params.feedbackMaskSR;
    r.rr = params.feedbackMaskRR;
    r.fg = params.feedbackMaskFG;
    r.scalingRatio = params.scalingRatio;
    r.performanceMode = params.performanceMode;
    r.renderPreset = params.renderPreset;
    r.frameGenerationCount = params.frameGenerationCount;
    r.frameGenerationPreset = params.frameGenerationPreset;
    r.frameGenerationMode = params.frameGenerationMode;
    *out = r;
    return FL_NV_OK;
}

FL_NV_API int32_t FlNvDriverProfile(const uint16_t* exePath, const uint32_t* ids, uint32_t count,
                                    FlNvDriverProfileRead* out) {
    if (out == nullptr || exePath == nullptr || (ids == nullptr && count != 0u) || count > FL_NV_PROFILE_MAX_SETTINGS) {
        return FL_NV_BAD_ARGUMENT;
    }
    if (out->size != sizeof(FlNvDriverProfileRead)) {
        return FL_NV_BAD_SIZE;
    }
    // NVAPI's fixed Unicode string: a path that does not fit is refused, never truncated — a truncated path would
    // find the profile of whatever program it now names.
    NvAPI_UnicodeString app{};
    size_t              length = 0;
    while (exePath[length] != 0u) {
        if (++length >= NVAPI_UNICODE_STRING_MAX) {
            return FL_NV_BAD_ARGUMENT;
        }
    }
    std::memcpy(app, exePath, length * sizeof(NvU16));

    FlNvDriverProfileRead r{};
    r.size = sizeof(FlNvDriverProfileRead);
    r.status = FL_NV_PROFILE_DEGRADED;
    if (!g_ready.load(std::memory_order_acquire)) {
        r.nvapiStatus = FL_NV_NOT_INITIALISED;
        *out = r;
        return FL_NV_OK;
    }
    Locked             lock;
    NvDRSSessionHandle session = nullptr;
    NvAPI_Status       status = NvAPI_DRS_CreateSession(&session);
    if (status != NVAPI_OK) {
        r.nvapiStatus = static_cast<int32_t>(status);
        *out = r;
        return FL_NV_OK;
    }
    status = NvAPI_DRS_LoadSettings(session);
    NvDRSProfileHandle profile = nullptr;
    if (status == NVAPI_OK) {
        NVDRS_APPLICATION application{};
        application.version = NVDRS_APPLICATION_VER;
        status = NvAPI_DRS_FindApplicationByName(session, app, &profile, &application);
        if (status == NVAPI_OK) {
            r.status = FL_NV_PROFILE_APPLICATION;
        } else if (status == NVAPI_EXECUTABLE_NOT_FOUND) {
            // No profile names this executable: the global profile is what the driver gives it.
            const NvAPI_Status global = NvAPI_DRS_GetCurrentGlobalProfile(session, &profile);
            r.status = global == NVAPI_OK ? FL_NV_PROFILE_GLOBAL : FL_NV_PROFILE_DEGRADED;
            if (global != NVAPI_OK) {
                status = global;
            }
        }
    }
    r.nvapiStatus = static_cast<int32_t>(status);
    if (r.status != FL_NV_PROFILE_DEGRADED) {
        NVDRS_PROFILE info{};
        info.version = NVDRS_PROFILE_VER;
        if (NvAPI_DRS_GetProfileInfo(session, profile, &info) == NVAPI_OK) {
            constexpr size_t kName = sizeof(r.profileName) / sizeof(r.profileName[0]);
            for (size_t i = 0; i + 1u < kName && info.profileName[i] != 0u; ++i) {
                r.profileName[i] = info.profileName[i];
            }
        }
        for (uint32_t i = 0; i < count; ++i) {
            // NVDRS_SETTING carries two unions sized for a binary value: ~8 KB, on the Agent's stack, never a game's.
            NVDRS_SETTING setting{};
            setting.version = NVDRS_SETTING_VER;
            const NvAPI_Status  read = NvAPI_DRS_GetSetting(session, profile, ids[i], &setting);
            FlNvProfileSetting& s = r.settings[i];
            s.id = ids[i];
            s.status = static_cast<int32_t>(read);
            if (read == NVAPI_OK) {
                s.dword = setting.settingType == NVDRS_DWORD_TYPE ? 1u : 0u;
                s.value = s.dword != 0u ? setting.u32CurrentValue : 0u;
                s.location = static_cast<uint32_t>(setting.settingLocation);
                s.predefined = setting.isCurrentPredefined != 0u ? 1u : 0u;
            }
        }
        r.count = count;
    }
    NvAPI_DRS_DestroySession(session);
    *out = r;
    return FL_NV_OK;
}

FL_NV_API int32_t FlNvDriverVersion(uint32_t* version, char* branch, uint32_t branchCapacity) {
    if (version == nullptr) {
        return FL_NV_BAD_ARGUMENT;
    }
    if (!g_ready.load(std::memory_order_acquire)) {
        return FL_NV_NOT_INITIALISED;
    }
    Locked             lock;
    NvU32              v = 0;
    NvAPI_ShortString  b{};
    const NvAPI_Status status = NvAPI_SYS_GetDriverAndBranchVersion(&v, b);
    if (status != NVAPI_OK) {
        return static_cast<int32_t>(status);
    }
    *version = v;
    if (branch != nullptr && branchCapacity > 0u) {
        // Bounded by the buffer, not by the terminator: the string is the driver's, and a driver that filled all
        // NVAPI_SHORT_STRING_MAX bytes without one must not send this read past it (SonarCloud cpp:S5813, beta.24).
        const size_t n = strnlen(b, sizeof(b));
        const size_t copy = n < branchCapacity - 1u ? n : branchCapacity - 1u;
        std::memcpy(branch, b, copy);
        branch[copy] = '\0';
    }
    return FL_NV_OK;
}

FL_NV_API int32_t FlNvReadFacts(FlNvFacts* out) {
    if (out == nullptr) {
        return FL_NV_BAD_ARGUMENT;
    }
    if (out->size != sizeof(FlNvFacts)) {
        return FL_NV_BAD_SIZE;
    }
    if (!g_ready.load(std::memory_order_acquire)) {
        return FL_NV_NOT_INITIALISED;
    }
    Locked lock;
    if (!g_haveGpu) {
        return FL_NV_NO_GPU;
    }
    FlNvFacts f{};
    f.size = sizeof(FlNvFacts);
    f.gpuIndex = 0u;
    f.gpuCount = g_gpuCount;
    ReadFactIdentity(f);
    ReadFactClocks(f);
    ReadFactThermalAndMemory(f);
    ReadFactSystem(f);
    *out = f;
    return FL_NV_OK;
}

FL_NV_API int32_t FlNvReadLive(FlNvLive* out) {
    if (out == nullptr) {
        return FL_NV_BAD_ARGUMENT;
    }
    if (out->size != sizeof(FlNvLive)) {
        return FL_NV_BAD_SIZE;
    }
    if (!g_ready.load(std::memory_order_acquire)) {
        return FL_NV_NOT_INITIALISED;
    }
    Locked lock;
    if (!g_haveGpu) {
        return FL_NV_NO_GPU;
    }
    FlNvLive l{};
    l.size = sizeof(FlNvLive);
    ReadLiveMemory(l);
    ReadLiveLoadAndClock(l);
    ReadLiveState(l);
    *out = l;
    return FL_NV_OK;
}

FL_NV_API int32_t FlNvReadDisplays(FlNvDisplays* out) {
    if (out == nullptr) {
        return FL_NV_BAD_ARGUMENT;
    }
    if (out->size != sizeof(FlNvDisplays)) {
        return FL_NV_BAD_SIZE;
    }
    if (!g_ready.load(std::memory_order_acquire)) {
        return FL_NV_NOT_INITIALISED;
    }
    Locked lock;
    if (!g_haveGpu) {
        return FL_NV_NO_GPU;
    }
    FlNvDisplays r{};
    r.size = sizeof(FlNvDisplays);
    // Twice the displays reported, so a card with more connectors than FL_NV_MAX_DISPLAYS is read up to that count
    // rather than refused: the driver answers NVAPI_INSUFFICIENT_BUFFER for a buffer smaller than what is connected.
    constexpr NvU32   kIds = FL_NV_MAX_DISPLAYS * 2u;
    NV_GPU_DISPLAYIDS ids[kIds]{};
    for (NV_GPU_DISPLAYIDS& id : ids) {
        id.version = NV_GPU_DISPLAYIDS_VER;
    }
    NvU32              count = kIds;
    const NvAPI_Status status = NvAPI_GPU_GetConnectedDisplayIds(g_gpu, ids, &count, 0u);
    r.nvapiStatus = static_cast<int32_t>(status);
    if (status == NVAPI_OK) {
        for (NvU32 i = 0; i < count && i < kIds && r.count < FL_NV_MAX_DISPLAYS; ++i) {
            FlNvDisplay& d = r.displays[r.count++];
            d.displayId = ids[i].displayId;
            d.active = ids[i].isActive != 0u ? 1u : 0u;
            ReadDisplay(ids[i].displayId, d);
        }
    }
    *out = r;
    return FL_NV_OK;
}

FL_NV_API int32_t FlNvSessionDisplay(const char* gdiDeviceName, uint32_t pid, FlNvSessionDisplayRead* out) {
    if (out == nullptr || gdiDeviceName == nullptr) {
        return FL_NV_BAD_ARGUMENT;
    }
    if (out->size != sizeof(FlNvSessionDisplayRead)) {
        return FL_NV_BAD_SIZE;
    }
    FlNvSessionDisplayRead r{};
    r.size = sizeof(FlNvSessionDisplayRead);
    if (!g_ready.load(std::memory_order_acquire)) {
        r.nvapiStatus = FL_NV_NOT_INITIALISED;
        r.colorSpaceStatus = FL_NV_NOT_INITIALISED;
        *out = r;
        return FL_NV_OK;
    }
    Locked             lock;
    NvU32              displayId = 0;
    const NvAPI_Status found = NvAPI_DISP_GetDisplayIdByDisplayName(gdiDeviceName, &displayId);
    r.nvapiStatus = static_cast<int32_t>(found);
    r.colorSpaceStatus = static_cast<int32_t>(found);
    if (found == NVAPI_OK) {
        NV_GET_VRR_INFO v{};
        v.version = NV_GET_VRR_INFO_VER;
        if (NvAPI_Disp_GetVRRInfo(displayId, &v) == NVAPI_OK) {
            r.vrrFlags = VrrFlagsOf(v);
            r.present |= FL_NV_SESSION_VRR;
        }
        NV_COLORSPACE_TYPE space = NV_COLORSPACE_sRGB;
        const NvAPI_Status asked = NvAPI_Disp_GetSourceColorSpace(displayId, &space, static_cast<NvU64>(pid));
        r.colorSpaceStatus = static_cast<int32_t>(asked);
        if (asked == NVAPI_OK) {
            r.colorSpace = static_cast<uint32_t>(space);
            r.present |= FL_NV_SESSION_COLORSPACE;
        }
    }
    *out = r;
    return FL_NV_OK;
}

FL_NV_API int32_t FlNvProfileSettings(const uint16_t* exePath, uint32_t ownOnly, FlNvProfileEntry* entries,
                                      uint32_t capacity, FlNvProfileList* out) {
    if (out == nullptr || (entries == nullptr && capacity != 0u)) {
        return FL_NV_BAD_ARGUMENT;
    }
    if (out->size != sizeof(FlNvProfileList)) {
        return FL_NV_BAD_SIZE;
    }
    // The same refusal FlNvDriverProfile makes: a path that does not fit NVAPI's string is never truncated.
    NvAPI_UnicodeString app{};
    if (exePath != nullptr) {
        size_t length = 0;
        while (exePath[length] != 0u) {
            if (++length >= NVAPI_UNICODE_STRING_MAX) {
                return FL_NV_BAD_ARGUMENT;
            }
        }
        std::memcpy(app, exePath, length * sizeof(NvU16));
    }

    FlNvProfileList r{};
    r.size = sizeof(FlNvProfileList);
    r.status = FL_NV_PROFILE_DEGRADED;
    if (!g_ready.load(std::memory_order_acquire)) {
        r.nvapiStatus = FL_NV_NOT_INITIALISED;
        *out = r;
        return FL_NV_OK;
    }
    Locked             lock;
    NvDRSSessionHandle session = nullptr;
    NvAPI_Status       status = NvAPI_DRS_CreateSession(&session);
    if (status != NVAPI_OK) {
        r.nvapiStatus = static_cast<int32_t>(status);
        *out = r;
        return FL_NV_OK;
    }
    status = NvAPI_DRS_LoadSettings(session);
    NvDRSProfileHandle profile = nullptr;
    bool               list = false;
    if (status == NVAPI_OK && exePath == nullptr) {
        status = NvAPI_DRS_GetCurrentGlobalProfile(session, &profile);
        if (status == NVAPI_OK) {
            r.status = FL_NV_PROFILE_GLOBAL;
            list = true;
        }
    } else if (status == NVAPI_OK) {
        NVDRS_APPLICATION application{};
        application.version = NVDRS_APPLICATION_VER;
        status = NvAPI_DRS_FindApplicationByName(session, app, &profile, &application);
        if (status == NVAPI_OK) {
            r.status = FL_NV_PROFILE_APPLICATION;
            list = true;
        } else if (status == NVAPI_EXECUTABLE_NOT_FOUND) {
            // No profile names this executable. What the driver gives it is the global profile, which the caller
            // reads with a null path; listing it again here would file the PC's settings under one game.
            r.status = FL_NV_PROFILE_GLOBAL;
        }
    }
    r.nvapiStatus = static_cast<int32_t>(status);
    if (list) {
        NVDRS_PROFILE info{};
        info.version = NVDRS_PROFILE_VER;
        if (NvAPI_DRS_GetProfileInfo(session, profile, &info) == NVAPI_OK) {
            CopyWide(r.profileName, sizeof(r.profileName) / sizeof(r.profileName[0]), info.profileName,
                     NVAPI_UNICODE_STRING_MAX);
        }
        // Bounded: a store that never answers NVAPI_END_ENUMERATION must not hold the lock for ever.
        constexpr NvU32 kMostSettings = 4096;
        for (NvU32 start = 0; start < kMostSettings;) {
            for (NVDRS_SETTING& setting : g_enum) {
                setting = NVDRS_SETTING{};
                setting.version = NVDRS_SETTING_VER;
            }
            NvU32              read = kEnumBatch;
            const NvAPI_Status more = NvAPI_DRS_EnumSettings(session, profile, start, &read, g_enum);
            if (more != NVAPI_OK || read == 0u) {
                if (more != NVAPI_OK && more != NVAPI_END_ENUMERATION) {
                    r.nvapiStatus = static_cast<int32_t>(more);
                }
                break;
            }
            for (NvU32 i = 0; i < read && i < kEnumBatch; ++i) {
                const NVDRS_SETTING& setting = g_enum[i];
                if (ownOnly != 0u && setting.settingLocation != NVDRS_CURRENT_PROFILE_LOCATION) {
                    continue;
                }
                if (r.count < capacity) {
                    FlNvProfileEntry e{};
                    e.id = setting.settingId;
                    e.type = static_cast<uint32_t>(setting.settingType);
                    e.value = setting.settingType == NVDRS_DWORD_TYPE ? setting.u32CurrentValue : 0u;
                    e.location = static_cast<uint32_t>(setting.settingLocation);
                    e.predefined = setting.isCurrentPredefined != 0u ? 1u : 0u;
                    CopyWide(e.name, sizeof(e.name) / sizeof(e.name[0]), setting.settingName, NVAPI_UNICODE_STRING_MAX);
                    entries[r.count++] = e;
                }
                ++r.total;
            }
            start += read;
        }
    }
    NvAPI_DRS_DestroySession(session);
    *out = r;
    return FL_NV_OK;
}

FL_NV_API uint32_t FlNvFactsSize(void) {
    return sizeof(FlNvFacts);
}

FL_NV_API uint32_t FlNvLiveSize(void) {
    return sizeof(FlNvLive);
}

FL_NV_API uint32_t FlNvDisplaysSize(void) {
    return sizeof(FlNvDisplays);
}

FL_NV_API uint32_t FlNvSessionDisplaySize(void) {
    return sizeof(FlNvSessionDisplayRead);
}

FL_NV_API uint32_t FlNvProfileEntrySize(void) {
    return sizeof(FlNvProfileEntry);
}

FL_NV_API uint32_t FlNvProfileListSize(void) {
    return sizeof(FlNvProfileList);
}

FL_NV_API uint32_t FlNvAbiVersion(void) {
    return FL_NV_ABI_VERSION;
}

FL_NV_API uint32_t FlNvSampleSize(void) {
    return sizeof(FlNvSample);
}

FL_NV_API uint32_t FlNvNgxStateSize(void) {
    return sizeof(FlNvNgxWords);
}

FL_NV_API uint32_t FlNvDriverProfileSize(void) {
    return sizeof(FlNvDriverProfileRead);
}

FL_NV_API const char* FlNvBuildId(void) {
    return FL_BUILD_ID;
}

}    // extern "C"
