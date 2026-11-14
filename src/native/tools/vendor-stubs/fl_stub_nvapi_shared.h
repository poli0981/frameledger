// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

// The knobs and counters of the stand-in nvapi64.dll (stub_nvapi64.cpp), as a named section another process can open.
//
// WHY A SECTION. The probe loads the stub into itself and calls its exports. The injected tests cannot: the stub is
// in the harness's process, and a test that asks "did the Overlay call the driver at all" has to read the stub's
// count from outside -- the harness's output is not captured. The stub creates `Local\FrameLedger.StubNvapi.<pid>`
// when it is loaded and keeps every knob and count in it; the exports the probe calls write the same words.
//
// A TEST FIXTURE'S, in a fixture's own namespace of names. Nothing shipped opens it.

#ifndef FRAMELEDGER_FL_STUB_NVAPI_SHARED_H
#define FRAMELEDGER_FL_STUB_NVAPI_SHARED_H

#include <cstdint>

namespace fl::stubnv {

// What GetLatency does (`mode`).
enum Mode : std::uint32_t {
    kModeReports = 0,         // sixty-four frames, their ids ten further on at every call
    kModeNothing = 1,         // OK and a report of nothing: a game that sets no marker
    kModeError = 2,           // NVAPI_ERROR, from both getters
    kModeFault = 3,           // an access violation inside GetLatency
    kModeNoDriverSpan = 4,    // reports whose driverStartTime is zero: what the driver gives without a sleep mode
    kModeImpossible = 5,      // reports whose present ends before it starts
    // Reports whose frames differ in their GPU span and in the driver's active time, by frameID % 5:
    //   0  as kModeReports: a span of kGpuUs and an active time of kGpuActiveUs, which is the shorter
    //   1  the two GPU marks at the same instant, and no active time
    //   2  an active time of kGpuActiveLongUs, longer than the span
    //   3  the span, and no active time
    //   4  the two GPU marks at the same instant, and an active time
    kModeGpuShapes = 6,
};

// One frame of kModeReports, in microseconds: every frame has these spans.
inline constexpr std::uint32_t kSimulationUs = 800u;
inline constexpr std::uint32_t kSubmitUs = 1150u;
inline constexpr std::uint32_t kPresentUs = 150u;
inline constexpr std::uint32_t kDriverUs = 400u;
inline constexpr std::uint32_t kOsQueueUs = 1200u;
inline constexpr std::uint32_t kGpuUs = 3700u;
inline constexpr std::uint32_t kTotalUs = 7500u;
// The two times a driver works out itself, of every frame: the card at work, and one frame's GPU end to the next's.
inline constexpr std::uint32_t kGpuActiveUs = 3500u;
inline constexpr std::uint32_t kGpuFrameUs = 4200u;
inline constexpr std::uint32_t kGpuActiveLongUs = 4000u;    // kModeGpuShapes: longer than kGpuUs
inline constexpr std::uint32_t kFgMultiplier = 2u;          // what GetSleepStatus gives while `fgMultiplier` is zero
inline constexpr std::uint32_t kFirstFrameId = 5000u;
inline constexpr std::uint32_t kFrameIdStep = 10u;    // how far the ids move on at each call

// Plain words, read and written with interlocked operations on both sides.
struct Shared {
    std::uint32_t mode;                // Mode
    std::uint32_t blockMs;             // GetLatency sleeps this long before it answers
    std::uint32_t hideGetters;         // nvapi_QueryInterface answers null for both getters
    std::uint32_t queries;             // nvapi_QueryInterface calls
    std::uint32_t latencyCalls;        // GetLatency calls
    std::uint32_t sleepStatusCalls;    // GetSleepStatus calls
    std::uint32_t fgMultiplier;        // the multiplier GetSleepStatus gives; zero = kFgMultiplier
    std::uint32_t reserved[1];
};

inline constexpr wchar_t kSectionFormat[] = L"Local\\FrameLedger.StubNvapi.%lu";

}    // namespace fl::stubnv

#endif    // FRAMELEDGER_FL_STUB_NVAPI_SHARED_H
