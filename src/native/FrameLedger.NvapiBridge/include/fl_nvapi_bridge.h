// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

// fl_nvapi_bridge.h — the C ABI the managed Agent reaches NVAPI through (docs/18_GPU_VENDOR_APIS.md §L3).
//
// WHAT THIS IS. NVAPI is a C API behind a vendored import library, and CLAUDE.md puts the native
// boundary in FrameLedger.Infrastructure: this DLL is the second P/Invoke facade after the guard's,
// and it is READ-ONLY by construction — nothing here calls a setter, and nothing here is reachable
// from a game process. It is loaded by the Agent (and the unshipped capture host) BESIDE the
// binary, by absolute path, never into a target: the name deliberately carries none of the words
// the guard's own §S18 heuristic scans a launcher's ancestors for.
//
// WHAT IT IS NOT. Not Reflex latency: NvAPI_D3D_GetLatency is per frame and per device, and the
// Overlay's in-process hook is where that fact lives (20_OPEN_QUESTIONS §M8). Not a poller: the
// managed TelemetryPoller asks FlNvReadSample at 1 Hz; this answers what the driver says now.
// Not a source of identifiers: nothing here reads a serial number, a unique id or an EDID (D104).
//
// VERSIONED STRUCTS. Every out-struct carries its own size as the first field, set by the caller and
// checked here, so a managed mirror that drifts from this header is refused (FL_NV_BAD_SIZE) rather
// than read as garbage — the same rule fl_shm.h applies with its static_asserts, one step later.
#pragma once

#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

#ifdef FL_NV_BUILDING
#define FL_NV_API __declspec(dllexport)
#else
#define FL_NV_API __declspec(dllimport)
#endif

// 2 since 2026-09-25 (beta.8): FlNvDriverProfile and FlNvDriverProfileSize. Additive — the version exists so the
// managed side refuses a DLL that predates the entry point rather than failing on a missing export at the first read.
// 3 since 2026-11-11 (owner decision D101, "Advanced metrics"): FlNvReadFacts, FlNvReadLive, FlNvReadDisplays,
// FlNvSessionDisplay, FlNvProfileSettings and their size queries. Additive again: no struct of ABI 2 changed size.
#define FL_NV_ABI_VERSION 3u

// Status codes. Zero is success; negatives are ours; an NvAPI_Status is passed through where the
// caller can act on it (it is negative too, and never one of ours).
#define FL_NV_OK 0
#define FL_NV_NOT_INITIALISED (-1000)
#define FL_NV_BAD_SIZE (-1001)
#define FL_NV_NO_GPU (-1002)
#define FL_NV_BAD_ARGUMENT (-1003)

// Which fields of FlNvSample carry a value. A clear bit is N/A, never 0 (03_METRICS §Sensor aggregates).
#define FL_NV_FIELD_TEMP_CORE 0x001u
#define FL_NV_FIELD_TEMP_MEMORY 0x002u
#define FL_NV_FIELD_LOAD 0x004u
// Reserved, and never set. Until 2026-11-11 this line said the vendored headers carry no memory-info entry point;
// they do (NvAPI_GPU_GetMemoryInfoEx, nvapi_lite_common.h). It stays clear by decision: L1's PDH counter and L2 fill
// the base vram_adapter series, and which layer does must not change under a session. FlNvLive carries the driver's
// figures.
#define FL_NV_FIELD_VRAM 0x008u
#define FL_NV_FIELD_CORE_CLOCK 0x010u
#define FL_NV_FIELD_MEM_CLOCK 0x020u
#define FL_NV_FIELD_FAN 0x040u
#define FL_NV_FIELD_THROTTLE 0x080u
#define FL_NV_FIELD_PCIE_WIDTH 0x100u
#define FL_NV_FIELD_NAME 0x200u

typedef struct FlNvSample {
    uint32_t size;       // sizeof(FlNvSample), set by the caller
    uint32_t present;    // FL_NV_FIELD_* bits
    float    tempCoreC;
    float    tempMemoryC;
    float    loadPct;       // NVAPI_GPU_UTILIZATION_DOMAIN_GPU, last 1 s
    float    vramUsedMb;    // reserved (FL_NV_FIELD_VRAM is never set by this build)
    float    coreClockMhz;
    float    memClockMhz;
    float    fanRpm;
    uint32_t throttleReasons;    // NV_GPU_PERF_DECREASE_REASON_* bits, raw
    uint32_t pcieWidth;
    uint32_t reserved[4];
    char     name[64];    // NvAPI_ShortString, NUL-terminated
} FlNvSample;

// NvAPI_NGX_GetNGXOverrideState for one process (R570+): the driver's own per-process NGX words.
#define FL_NV_NGX_ANSWERED 0
#define FL_NV_NGX_UNANSWERED 1    // NvAPI answered but not for this pid
#define FL_NV_NGX_DEGRADED 2      // no usable NVIDIA driver

typedef struct FlNvNgxWords {
    uint32_t size;           // sizeof(FlNvNgxWords), set by the caller
    int32_t  status;         // FL_NV_NGX_*
    int32_t  nvapiStatus;    // the NvAPI_Status behind status, verbatim
    uint32_t driver;         // NvAPI_SYS_GetDriverAndBranchVersion, e.g. 61664
    uint64_t sr;             // feedbackMaskSR
    uint64_t rr;             // feedbackMaskRR
    uint64_t fg;             // feedbackMaskFG
    float    scalingRatio;
    uint32_t performanceMode;
    uint32_t renderPreset;
    uint32_t frameGenerationCount;
    uint32_t frameGenerationPreset;
    uint32_t frameGenerationMode;
    uint32_t reserved[2];
} FlNvNgxWords;

// NvAPI_DRS: the driver profile the NVIDIA driver applies to an executable, and the values it gives the settings the
// caller names (beta.8, 2026-09-25) — which is where the NVIDIA App writes its per-game DLSS / frame-generation
// overrides. READ-ONLY like everything here: a DRS session is created, the settings database loaded, the profile
// found, its values read, and the session destroyed. Nothing is set, saved, restored or deleted — NvAPI_DRS_SetSetting,
// NvAPI_DRS_SaveSettings and every other DRS writer are never called (18_GPU_VENDOR_APIS §Runtime policy).
#define FL_NV_PROFILE_MAX_SETTINGS 24u
#define FL_NV_PROFILE_APPLICATION 0    // an application profile names this executable; the values are what it gives it
#define FL_NV_PROFILE_GLOBAL 1         // no application profile names it: the values the current global profile gives
#define FL_NV_PROFILE_DEGRADED 2       // no usable NVIDIA driver, or DRS refused — nvapiStatus says which

typedef struct FlNvProfileSetting {
    uint32_t id;            // the setting id asked for
    int32_t  status;        // NvAPI_DRS_GetSetting's NvAPI_Status, verbatim (NVAPI_SETTING_NOT_FOUND: set nowhere)
    uint32_t value;         // u32CurrentValue, when status is NVAPI_OK and the setting is a DWORD
    uint32_t location;      // NVDRS_SETTING_LOCATION: 0 this profile, 1 the global profile, 2 base, 3 the default
    uint32_t predefined;    // isCurrentPredefined: nonzero = the driver's shipped value, 0 = a value someone set
    uint32_t dword;         // 1 when the setting is a DWORD (NVDRS_DWORD_TYPE); value is 0 otherwise
} FlNvProfileSetting;

typedef struct FlNvDriverProfileRead {
    uint32_t           size;                // sizeof(FlNvDriverProfileRead), set by the caller
    int32_t            status;              // FL_NV_PROFILE_*
    int32_t            nvapiStatus;         // the NvAPI_Status behind status (FindApplicationByName's for GLOBAL)
    uint32_t           count;               // settings[] filled: the count asked for, 0 when DEGRADED
    uint16_t           profileName[128];    // UTF-16, NUL-terminated, truncated
    FlNvProfileSetting settings[FL_NV_PROFILE_MAX_SETTINGS];
} FlNvDriverProfileRead;

// Reference-counted: every FlNvInit is matched by an FlNvShutdown, and NvAPI_Unload runs at zero.
// Returns FL_NV_OK, or the NvAPI_Status NvAPI_Initialize answered (a normal condition on a machine
// with no NVIDIA driver — L3 disables cleanly, never throws). A machine whose driver initialises
// but enumerates no GPU answers FL_NV_NO_GPU.
FL_NV_API int32_t FlNvInit(void);
FL_NV_API void    FlNvShutdown(void);

// One reading of the first physical GPU. Each field is independent: a call the driver refuses
// leaves its bit clear and the others intact. FL_NV_NOT_INITIALISED / FL_NV_BAD_SIZE otherwise.
FL_NV_API int32_t FlNvReadSample(FlNvSample* out);

// The driver's NGX words for `pid`. Never fails for a reason the caller cannot read: `status` says
// which branch was taken, and `nvapiStatus` says why.
FL_NV_API int32_t FlNvNgxState(uint32_t pid, FlNvNgxWords* out);

// The profile the driver applies to `exePath` (a fully qualified path — the one the driver will match on launch) and
// the values it gives the `count` settings in `ids`. Never fails for a reason the caller cannot read: `status` says
// which branch was taken. FL_NV_BAD_ARGUMENT for a null pointer, more than FL_NV_PROFILE_MAX_SETTINGS ids, or a
// path too long for NVAPI's string — refused, never truncated, since a truncated path matches another program.
FL_NV_API int32_t FlNvDriverProfile(const uint16_t* exePath, const uint32_t* ids, uint32_t count,
                                    FlNvDriverProfileRead* out);

// Driver version (e.g. 61664) and branch string; FL_NV_NOT_INITIALISED before FlNvInit.
FL_NV_API int32_t FlNvDriverVersion(uint32_t* version, char* branch, uint32_t branchCapacity);

// ---------------------------------------------------------------------------------------------------------------------
// ABI 3 - what "Advanced metrics" reads (owner decision D101; docs/18_GPU_VENDOR_APIS.md section Advanced metrics).
//
// STILL EVERY CALL A READ, and since this ABI a gate holds it: tools/nvapi-readonly-check.ps1 compares every NvAPI_*
// call of the .cpp with a committed list (D108). NEVER AN IDENTIFIER (D104): no board serial number, no unique id, no
// EDID, no list of the programs that are encoding - their entry points are not on that list.
//
// Each struct below carries its size first, like the ones above, and a `present` mask: a call the driver refuses
// leaves its bit clear and the other fields intact, and a clear bit is N/A, never 0.
// ---------------------------------------------------------------------------------------------------------------------

// FlNvFacts.present: what is true of the card whatever is running.
#define FL_NV_FACT_GPU_TYPE 0x00000001u              // gpuType
#define FL_NV_FACT_SYSTEM_TYPE 0x00000002u           // systemType
#define FL_NV_FACT_GPU_INFO 0x00000004u              // external, rtCores, tensorCores
#define FL_NV_FACT_BUS_TYPE 0x00000008u              // busType
#define FL_NV_FACT_PCI_IDS 0x00000010u               // pci*
#define FL_NV_FACT_VBIOS 0x00000020u                 // vbios
#define FL_NV_FACT_ARCH 0x00000040u                  // arch*
#define FL_NV_FACT_CORE_COUNT 0x00000080u            // coreCount
#define FL_NV_FACT_RAM_BUS_WIDTH 0x00000100u         // ramBusWidth
#define FL_NV_FACT_CLOCK_BASE_CORE 0x00000200u       // clockBaseCoreMhz
#define FL_NV_FACT_CLOCK_BASE_MEMORY 0x00000400u     // clockBaseMemoryMhz
#define FL_NV_FACT_CLOCK_BOOST_CORE 0x00000800u      // clockBoostCoreMhz
#define FL_NV_FACT_CLOCK_BOOST_MEMORY 0x00001000u    // clockBoostMemoryMhz
#define FL_NV_FACT_TEMP_DEFAULT_MAX 0x00002000u      // tempDefaultMaxC
#define FL_NV_FACT_MEMORY 0x00004000u                // vram*Bytes
#define FL_NV_FACT_FB_PHYSICAL 0x00008000u           // fbPhysicalKb
#define FL_NV_FACT_FB_VIRTUAL 0x00010000u            // fbVirtualKb
#define FL_NV_FACT_DRIVER_INFO 0x00020000u           // driverFlags, baseBranch
#define FL_NV_FACT_INTERFACE_VERSION 0x00040000u     // interfaceVersion
#define FL_NV_FACT_CHIPSET 0x00080000u               // chipsetVendor, chipsetName

// FlNvFacts.driverFlags (NV_DISPLAY_DRIVER_INFO).
#define FL_NV_DRIVER_DCH 0x01u
#define FL_NV_DRIVER_STUDIO 0x02u
#define FL_NV_DRIVER_GAME_READY 0x04u
#define FL_NV_DRIVER_RTX_PRODUCTION 0x08u
#define FL_NV_DRIVER_RTX_NEW_FEATURE 0x10u

typedef struct FlNvFacts {
    uint32_t size;           // sizeof(FlNvFacts), set by the caller
    uint32_t present;        // FL_NV_FACT_* bits
    uint32_t gpuIndex;       // which enumerated GPU these are the facts of: 0, the one FlNvReadSample reads
    uint32_t gpuCount;       // how many NvAPI_EnumPhysicalGPUs listed
    uint32_t gpuType;        // NV_GPU_TYPE: 1 integrated, 2 discrete
    uint32_t systemType;     // NV_SYSTEM_TYPE: 1 laptop, 2 desktop
    uint32_t external;       // NV_GPU_INFO.bIsExternalGpu
    uint32_t busType;        // NV_GPU_BUS_TYPE: 1 PCI, 2 AGP, 3 PCI Express, 4 FPCI, 5 AXI
    uint32_t pciDeviceId;    // the model, not the unit
    uint32_t pciSubSystemId;
    uint32_t pciRevisionId;
    uint32_t pciExtDeviceId;
    uint32_t archId;    // NV_GPU_ARCH_INFO.architecture
    uint32_t archImplementation;
    uint32_t archRevision;
    uint32_t coreCount;           // NvAPI_GPU_GetGpuCoreCount
    uint32_t rtCores;             // NV_GPU_INFO.rayTracingCores
    uint32_t tensorCores;         // NV_GPU_INFO.tensorCores
    uint32_t ramBusWidth;         // bits
    uint32_t driverFlags;         // FL_NV_DRIVER_* bits
    float    clockBaseCoreMhz;    // NV_GPU_CLOCK_FREQUENCIES_BASE_CLOCK
    float    clockBaseMemoryMhz;
    float    clockBoostCoreMhz;    // NV_GPU_CLOCK_FREQUENCIES_BOOST_CLOCK
    float    clockBoostMemoryMhz;
    float    tempDefaultMaxC;    // the core sensor's defaultMaxTemp
    uint32_t fbPhysicalKb;       // NvAPI_GPU_GetPhysicalFrameBufferSize
    uint32_t fbVirtualKb;        // NvAPI_GPU_GetVirtualFrameBufferSize
    uint32_t reserved0;
    uint64_t vramDedicatedBytes;             // NV_GPU_MEMORY_INFO_EX.dedicatedVideoMemory
    uint64_t vramDedicatedAvailableBytes;    // .availableDedicatedVideoMemory
    uint64_t vramSystemBytes;                // .systemVideoMemory
    uint64_t vramSharedBytes;                // .sharedSystemMemory
    uint32_t reserved[4];
    char     vbios[64];               // NvAPI_ShortString, NUL-terminated - as are the four below
    char     baseBranch[64];          // NV_DISPLAY_DRIVER_INFO.szBuildBaseBranch
    char     interfaceVersion[64];    // NvAPI_GetInterfaceVersionString
    char     chipsetVendor[64];       // NV_CHIPSET_INFO.szVendorName
    char     chipsetName[64];         // NV_CHIPSET_INFO.szChipsetName
} FlNvFacts;

// FlNvLive.present: what the card says now, beside FlNvSample.
#define FL_NV_LIVE_MEMORY 0x001u               // the six memory fields
#define FL_NV_LIVE_LOAD_FRAMEBUFFER 0x002u     // loadFramebufferPct
#define FL_NV_LIVE_LOAD_VIDEO 0x004u           // loadVideoPct
#define FL_NV_LIVE_LOAD_BUS 0x008u             // loadBusPct
#define FL_NV_LIVE_CLOCK_VIDEO 0x010u          // clockVideoMhz
#define FL_NV_LIVE_PSTATE 0x020u               // pstate
#define FL_NV_LIVE_TEMP_BOARD 0x040u           // tempBoardC
#define FL_NV_LIVE_TEMP_POWER_SUPPLY 0x080u    // tempPowerSupplyC
#define FL_NV_LIVE_THROTTLE 0x100u             // throttleReasons
#define FL_NV_LIVE_ENCODER 0x200u              // the three encoder fields

typedef struct FlNvLive {
    uint32_t size;                           // sizeof(FlNvLive), set by the caller
    uint32_t present;                        // FL_NV_LIVE_* bits
    uint64_t vramAvailableBytes;             // NV_GPU_MEMORY_INFO_EX.curAvailableDedicatedVideoMemory
    uint64_t vramDedicatedAvailableBytes;    // .availableDedicatedVideoMemory: what "in use" is taken from
    uint64_t evictionsBytes;                 // .dedicatedVideoMemoryEvictionsSize, since the driver loaded
    uint64_t evictionCount;
    uint64_t promotionsBytes;
    uint64_t promotionCount;
    float    loadFramebufferPct;    // utilization[1]: the frame-buffer domain
    float    loadVideoPct;          // utilization[2]: the video engine
    float    loadBusPct;            // utilization[3]: the bus interface
    float    clockVideoMhz;         // NVAPI_GPU_PUBLIC_CLOCK_VIDEO, current
    float    tempBoardC;            // NVAPI_THERMAL_TARGET_BOARD, where the card has the sensor
    float    tempPowerSupplyC;      // NVAPI_THERMAL_TARGET_POWER_SUPPLY
    uint32_t pstate;                // NV_GPU_PERF_PSTATE_ID: 0 is P0, the highest
    uint32_t throttleReasons;       // NV_GPU_PERF_DECREASE_REASON_* bits, raw - the same word FlNvSample carries
    uint32_t encoderSessions;       // NV_ENCODER_STATISTICS: active sessions, all programs - a count, never who
    uint32_t encoderFps;
    uint32_t encoderLatencyUs;
    uint32_t reserved[5];
} FlNvLive;

// What a connected display can do. `displayId` is NVAPI's handle for this boot's topology - not an identity of the
// monitor, and never stored. The EDID, which does carry the monitor's serial number, is not read (D104).
#define FL_NV_MAX_DISPLAYS 8u

#define FL_NV_DISPLAY_HDR 0x1u              // hdrFlags, luminance*
#define FL_NV_DISPLAY_CAPS 0x2u             // capsFlags
#define FL_NV_DISPLAY_VRR 0x4u              // vrrFlags
#define FL_NV_DISPLAY_ADAPTIVE_SYNC 0x8u    // adaptiveSyncFlags, maxFrameIntervalUs

#define FL_NV_HDR_ST2084 0x01u    // NV_HDR_CAPABILITIES.isST2084EotfSupported: HDR10
#define FL_NV_HDR_TRADITIONAL_HDR 0x02u
#define FL_NV_HDR_EDR 0x04u
#define FL_NV_HDR_TRADITIONAL_SDR 0x08u
#define FL_NV_HDR_DOLBY_VISION 0x10u
#define FL_NV_HDR_HDR10_PLUS 0x20u
#define FL_NV_HDR_HDR10_PLUS_GAMING 0x40u
#define FL_NV_HDR_NVIDIA_CERTIFIED 0x80u

#define FL_NV_CAPS_VRR 0x01u    // NV_MONITOR_CAPS_GENERIC.supportVRR
#define FL_NV_CAPS_ULMB 0x02u
#define FL_NV_CAPS_TRUE_GSYNC 0x04u    // a G-SYNC module; clear with FL_NV_CAPS_VRR set is adaptive sync
#define FL_NV_CAPS_RLA 0x08u
#define FL_NV_CAPS_VRR_NOW 0x10u    // currentlyCapableOfVRR, at the applied display settings
#define FL_NV_CAPS_BASIC_VRR 0x20u

#define FL_NV_VRR_ENABLED 0x01u    // NV_GET_VRR_INFO.bIsVRREnabled
#define FL_NV_VRR_POSSIBLE 0x02u
#define FL_NV_VRR_REQUESTED 0x04u
#define FL_NV_VRR_INDICATOR 0x08u
#define FL_NV_VRR_DISPLAY_MODE 0x10u    // bIsDisplayInVRRMode

#define FL_NV_ADAPTIVE_SYNC_DISABLED 0x01u
#define FL_NV_ADAPTIVE_SYNC_NO_FRAME_SPLITTING 0x02u

typedef struct FlNvDisplay {
    uint32_t displayId;
    uint32_t present;                       // FL_NV_DISPLAY_* bits
    uint32_t active;                        // NV_GPU_DISPLAYIDS.isActive
    uint32_t hdrFlags;                      // FL_NV_HDR_* bits
    uint32_t luminanceMax;                  // cd/m2
    uint32_t luminanceMinTenThousandths;    // 0.0001 cd/m2, the unit the driver reports it in
    uint32_t luminanceMaxFrameAverage;      // cd/m2
    uint32_t capsFlags;                     // FL_NV_CAPS_* bits
    uint32_t vrrFlags;                      // FL_NV_VRR_* bits
    uint32_t adaptiveSyncFlags;             // FL_NV_ADAPTIVE_SYNC_* bits
    uint32_t maxFrameIntervalUs;
    uint32_t reserved;
} FlNvDisplay;

typedef struct FlNvDisplays {
    uint32_t    size;           // sizeof(FlNvDisplays), set by the caller
    int32_t     nvapiStatus;    // NvAPI_GPU_GetConnectedDisplayIds' status, verbatim
    uint32_t    count;          // displays[] filled, at most FL_NV_MAX_DISPLAYS
    uint32_t    reserved;
    FlNvDisplay displays[FL_NV_MAX_DISPLAYS];
} FlNvDisplays;

// One session's display: the state of the display a game presents on, and the colour space the driver reports for
// the game's process. Named by the monitor's GDI device name (the szDevice of MONITORINFOEX), which the Agent has.
#define FL_NV_SESSION_VRR 0x1u           // vrrFlags
#define FL_NV_SESSION_COLORSPACE 0x2u    // colorSpace

typedef struct FlNvSessionDisplayRead {
    uint32_t size;                // sizeof(FlNvSessionDisplayRead), set by the caller
    int32_t  nvapiStatus;         // NvAPI_DISP_GetDisplayIdByDisplayName's status: not NVAPI_OK, nothing else was asked
    uint32_t present;             // FL_NV_SESSION_* bits
    uint32_t vrrFlags;            // FL_NV_VRR_* bits
    uint32_t colorSpace;          // NV_COLORSPACE_TYPE: 0 sRGB, 1 linear FP16 with sRGB primaries, 12 BT.2100 (HDR10)
    int32_t  colorSpaceStatus;    // NvAPI_Disp_GetSourceColorSpace's status, verbatim
    uint32_t reserved[2];
} FlNvSessionDisplayRead;

// Every setting of one driver profile, read-only - the list FlNvDriverProfile reads eighteen named ids of.
typedef struct FlNvProfileEntry {
    uint32_t id;            // settingId
    uint32_t type;          // NVDRS_SETTING_TYPE: 0 DWORD, 1 binary, 2 string, 3 wide string, 4 QWORD
    uint32_t value;         // u32CurrentValue when type is 0; 0 otherwise - a binary or string value is not copied
    uint32_t location;      // NVDRS_SETTING_LOCATION: 0 this profile, 1 the global profile, 2 base, 3 the default
    uint32_t predefined;    // isCurrentPredefined: nonzero = the driver's shipped value, 0 = a value someone set
    uint32_t reserved;
    uint16_t name[64];    // settingName, UTF-16, NUL-terminated, truncated
} FlNvProfileEntry;

typedef struct FlNvProfileList {
    uint32_t size;           // sizeof(FlNvProfileList), set by the caller
    int32_t  status;         // FL_NV_PROFILE_*: APPLICATION, GLOBAL (the global profile was read), DEGRADED
    int32_t  nvapiStatus;    // the NvAPI_Status behind status
    uint32_t count;          // entries written, at most the capacity given
    uint32_t total;          // settings that passed the filter: above count, the caller's buffer was too small
    uint32_t reserved[3];
    uint16_t profileName[128];    // UTF-16, NUL-terminated, truncated
} FlNvProfileList;

// The facts of the first physical GPU. FL_NV_NOT_INITIALISED / FL_NV_BAD_SIZE / FL_NV_NO_GPU otherwise.
FL_NV_API int32_t FlNvReadFacts(FlNvFacts* out);

// One reading of what FlNvSample does not carry. Same refusals.
FL_NV_API int32_t FlNvReadLive(FlNvLive* out);

// The displays connected to the first physical GPU and what each can do. Same refusals; a driver that lists none
// answers FL_NV_OK with count 0 and its status in nvapiStatus.
FL_NV_API int32_t FlNvReadDisplays(FlNvDisplays* out);

// The display `gdiDeviceName` names, and the colour space of process `pid` on it. Never fails for a reason the caller
// cannot read: nvapiStatus says whether the display was found. FL_NV_BAD_ARGUMENT for a null pointer.
FL_NV_API int32_t FlNvSessionDisplay(const char* gdiDeviceName, uint32_t pid, FlNvSessionDisplayRead* out);

// Every setting of a profile: `exePath` null reads the current global profile; otherwise the profile that names that
// executable, and when none does, status is FL_NV_PROFILE_GLOBAL and nothing is listed. `ownOnly` nonzero keeps the
// settings the profile itself sets (location 0) and leaves out what it inherits. `entries` may be null with capacity
// 0 to learn `total`. FL_NV_BAD_ARGUMENT for a null `out`, a null `entries` with a capacity, or a path too long.
FL_NV_API int32_t FlNvProfileSettings(const uint16_t* exePath, uint32_t ownOnly, FlNvProfileEntry* entries,
                                      uint32_t capacity, FlNvProfileList* out);

// What the managed mirror tests compare against, and what a build id check reads.
FL_NV_API uint32_t    FlNvAbiVersion(void);
FL_NV_API uint32_t    FlNvSampleSize(void);
FL_NV_API uint32_t    FlNvNgxStateSize(void);
FL_NV_API uint32_t    FlNvDriverProfileSize(void);
FL_NV_API uint32_t    FlNvFactsSize(void);
FL_NV_API uint32_t    FlNvLiveSize(void);
FL_NV_API uint32_t    FlNvDisplaysSize(void);
FL_NV_API uint32_t    FlNvSessionDisplaySize(void);
FL_NV_API uint32_t    FlNvProfileEntrySize(void);
FL_NV_API uint32_t    FlNvProfileListSize(void);
FL_NV_API const char* FlNvBuildId(void);

#ifdef __cplusplus
}
#endif
