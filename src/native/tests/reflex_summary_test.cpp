// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

// fl_adv_fields.h's reader of the driver's latency report (Advanced metrics train C; spike-notes §31), driven
// without the Overlay and without a driver: the header the Overlay compiles, on reports written here by hand.
//
// What it holds is what a real report made necessary. The newest frames of a report are not final -- one run of the
// probe read a span of half an hour in the newest -- so the two newest are never summed. An end point of zero is a
// span the driver did not report, not a fault of the frame: without a sleep mode it leaves driverStartTime zero in
// every frame, and a reader that refused those frames would refuse every frame of such a game. A span that runs
// backwards or lasts over a second is refused with its frame. And a game whose frame ids start again must not be
// read as a game that has stopped.

#include <catch2/catch_test_macros.hpp>
#include <cstdint>
#include <fl_adv_fields.h>
#include <memory>

namespace {

using Frame = NV_LATENCY_RESULT_PARAMS::FrameReport;

// A frame as the stand-in module writes one: every span reported, 7500 us from simulation start to the GPU's end.
Frame Whole(std::uint64_t id) {
    Frame               f{};
    const std::uint64_t t = 1'000'000ull + id * 4200ull;
    f.frameID = id;
    f.simStartTime = t + 100u;
    f.simEndTime = t + 900u;    // 800
    f.renderSubmitStartTime = t + 950u;
    f.renderSubmitEndTime = t + 2100u;    // 1150
    f.presentStartTime = t + 2150u;
    f.presentEndTime = t + 2300u;    // 150
    f.driverStartTime = t + 2300u;
    f.driverEndTime = t + 2700u;    // 400
    f.osRenderQueueStartTime = t + 2700u;
    f.osRenderQueueEndTime = t + 3900u;    // 1200
    f.gpuRenderStartTime = t + 3900u;
    f.gpuRenderEndTime = t + 7600u;    // 3700; total 7500
    return f;
}

// The report is 15 KB: on the heap here, as it is in static storage in the Overlay.
std::unique_ptr<NV_LATENCY_RESULT_PARAMS> Report(std::uint64_t firstId, std::uint32_t frames = 64u) {
    auto report = std::make_unique<NV_LATENCY_RESULT_PARAMS>();
    for (std::uint32_t i = 0; i < frames; ++i) {
        report->frameReport[64u - frames + i] = Whole(firstId + i);    // the newest last, as the driver writes it
    }
    return report;
}

}    // namespace

TEST_CASE("a report's two newest frames are left for the next read, and a frame is summed once", "[latency]") {
    fl::adv::LatencyCursor cursor;
    fl::adv::LatencyTally  tally;

    auto first = Report(5000u);
    fl::adv::ObserveLatencyReport(*first, cursor, tally);
    CHECK(tally.frames == 62u);    // 5000..5061; 5062 and 5063 are not final yet
    CHECK(tally.refused == 0u);
    CHECK(tally.lacking == 0u);
    CHECK(tally.sumUs[0] == 62u * 800u);
    CHECK(tally.sumUs[1] == 62u * 1150u);
    CHECK(tally.sumUs[2] == 62u * 150u);
    CHECK(tally.sumUs[3] == 62u * 400u);
    CHECK(tally.sumUs[4] == 62u * 1200u);
    CHECK(tally.sumUs[5] == 62u * 3700u);
    CHECK(tally.sumUs[fl::adv::kLatTotal] == 62u * 7500u);
    CHECK(cursor.lastFrameId == 5061u);

    // The same report again: nothing is new.
    fl::adv::ObserveLatencyReport(*first, cursor, tally);
    CHECK(tally.frames == 0u);
    CHECK(tally.refused == 0u);
    CHECK(cursor.lastFrameId == 5061u);

    // Ten frames further on: the two that were held back and eight more, and again not the two newest.
    auto second = Report(5010u);
    fl::adv::ObserveLatencyReport(*second, cursor, tally);
    CHECK(tally.frames == 10u);    // 5062..5071
    CHECK(tally.sumUs[fl::adv::kLatTotal] == 10u * 7500u);
    CHECK(cursor.lastFrameId == 5071u);
}

TEST_CASE("a report of nothing, of one frame and of two sums nothing", "[latency]") {
    fl::adv::LatencyCursor cursor;
    fl::adv::LatencyTally  tally;
    tally.frames = 9u;    // whatever was there is replaced, not added to

    auto empty = Report(0u, 0u);
    fl::adv::ObserveLatencyReport(*empty, cursor, tally);
    CHECK(tally.frames == 0u);
    CHECK(cursor.lastFrameId == 0u);

    auto one = Report(700u, 1u);
    fl::adv::ObserveLatencyReport(*one, cursor, tally);
    CHECK(tally.frames == 0u);

    auto two = Report(700u, 2u);
    fl::adv::ObserveLatencyReport(*two, cursor, tally);
    CHECK(tally.frames == 0u);
    CHECK(tally.refused == 0u);

    auto three = Report(700u, 3u);
    fl::adv::ObserveLatencyReport(*three, cursor, tally);
    CHECK(tally.frames == 1u);    // 700; 701 and 702 wait
    CHECK(cursor.lastFrameId == 700u);
}

TEST_CASE("an end point of zero is a span the driver did not report: the frame is summed and the span reads none",
          "[latency]") {
    fl::adv::LatencyCursor cursor;
    fl::adv::LatencyTally  tally;
    auto                   report = Report(100u);
    for (auto& f : report->frameReport) {
        f.driverStartTime = 0u;    // what the driver gives a game that sets markers and no sleep mode
    }
    fl::adv::ObserveLatencyReport(*report, cursor, tally);
    CHECK(tally.frames == 62u);
    CHECK(tally.refused == 0u);
    CHECK(tally.lacking == (1u << 3));
    CHECK(tally.sumUs[3] == 0u);
    CHECK(tally.sumUs[fl::adv::kLatTotal] == 62u * 7500u);

    // What is published for it: the sum of a span some summed frame lacked is all ones, never a smaller number.
    CHECK(fl::adv::LatencySum(tally, 3u) == FL_LAT_SUM_NONE);
    CHECK(fl::adv::LatencySum(tally, 0u) == 62u * 800u);
    CHECK(fl::adv::LatencySum(fl::adv::LatencyTally{}, 0u) == 0u);    // no frame: zero, not "none"
}

TEST_CASE("a span no frame can have refuses the frame whole, and so does a frame with no total", "[latency]") {
    fl::adv::LatencyCursor cursor;
    fl::adv::LatencyTally  tally;
    auto                   report = Report(100u);
    report->frameReport[0].presentEndTime = report->frameReport[0].presentStartTime - 50u;               // backwards
    report->frameReport[1].gpuRenderEndTime = report->frameReport[1].simStartTime + 1'844'674'519ull;    // half an hour
    report->frameReport[2].gpuRenderEndTime = 0u;                                                        // no total
    report->frameReport[3].simStartTime = 0u;                                                            // no total
    report->frameReport[4].osRenderQueueEndTime = report->frameReport[4].osRenderQueueStartTime + 1'000'001ull;
    report->frameReport[5].osRenderQueueEndTime = report->frameReport[5].osRenderQueueStartTime + 1'000'000ull;

    fl::adv::ObserveLatencyReport(*report, cursor, tally);
    CHECK(tally.refused == 5u);
    CHECK(tally.frames == 57u);    // 62 that are not the two newest, less the five; frame 5's second is still a span
    CHECK(tally.lacking == 0u);
    CHECK(tally.sumUs[2] == 57u * 150u);                  // the backwards present added nothing
    CHECK(tally.sumUs[4] == 56u * 1200u + 1'000'000u);    // exactly a second is allowed; a microsecond more is not
    CHECK(cursor.lastFrameId == 161u);                    // a refused frame is not looked at again either
}

TEST_CASE("frame ids that start again restart the reader instead of silencing it", "[latency]") {
    fl::adv::LatencyCursor cursor;
    fl::adv::LatencyTally  tally;
    auto                   late = Report(90'000u);
    fl::adv::ObserveLatencyReport(*late, cursor, tally);
    REQUIRE(cursor.lastFrameId == 90'061u);

    auto again = Report(1u);    // the game went back to its menu and counts from one
    fl::adv::ObserveLatencyReport(*again, cursor, tally);
    CHECK(tally.frames == 62u);
    CHECK(cursor.lastFrameId == 62u);
}

TEST_CASE("the frames are found whatever order the driver left them in", "[latency]") {
    fl::adv::LatencyCursor cursor;
    fl::adv::LatencyTally  tally;
    auto                   report = Report(300u);
    const Frame            newest = report->frameReport[63];
    report->frameReport[63] = report->frameReport[10];
    report->frameReport[10] = newest;    // the newest id is in the middle now
    fl::adv::ObserveLatencyReport(*report, cursor, tally);
    CHECK(tally.frames == 62u);
    CHECK(cursor.lastFrameId == 361u);    // 362 and 363 are the two newest wherever they sit
}

TEST_CASE("the sleep status is the driver's five switches and its multiplier, and nothing else of it", "[latency]") {
    NV_GET_SLEEP_STATUS_PARAMS status{};
    CHECK(fl::adv::ObserveSleepStatus(status).on == 0u);
    status.bLowLatencyMode = 1;
    status.bCplVsyncOn = 1;
    status.bFullscreenIFlip = 1;
    status.fgMultiplier = 3;
    status.sleepIntervalUs = 4166u;    // changes with every frame: not kept
    const fl::adv::LatencySleep seen = fl::adv::ObserveSleepStatus(status);
    CHECK(seen.on == (1u | 4u | 16u));
    CHECK(seen.multiplier == 3u);
    status = NV_GET_SLEEP_STATUS_PARAMS{};
    status.bFsVrr = 1;
    status.bUseGameSleep = 1;
    CHECK(fl::adv::ObserveSleepStatus(status).on == (2u | 8u));
}

TEST_CASE("the driver's two own times are summed over the frames that gave one, each with its own count", "[latency]") {
    fl::adv::LatencyCursor cursor;
    fl::adv::LatencyTally  tally;

    // Twelve frames, 5000..5011: ten are summed, the two newest wait.
    auto report = Report(5000u, 12u);
    for (std::uint32_t i = 52u; i < 64u; ++i) {
        report->frameReport[i].gpuActiveRenderTimeUs = 3500u;
        report->frameReport[i].gpuFrameTimeUs = 4200u;
    }
    report->frameReport[52].gpuActiveRenderTimeUs = 0u;            // 5000: the driver gave no active time
    report->frameReport[53].gpuFrameTimeUs = 1'000'001u;           // 5001: a frame time no frame can have
    report->frameReport[54].gpuActiveRenderTimeUs = 1'000'000u;    // 5002: a second, to the microsecond, is kept
    fl::adv::ObserveLatencyReport(*report, cursor, tally);
    CHECK(tally.frames == 10u);    // neither of the two refuses a frame
    CHECK(tally.refused == 0u);
    CHECK(tally.sumUs[fl::adv::kLatTotal] == 10u * 7500u);
    CHECK(tally.lacking == 0u);
    CHECK(tally.activeFrames == 9u);
    CHECK(tally.activeSumUs == 8u * 3500u + 1'000'000u);
    CHECK(tally.frameTimeFrames == 9u);
    CHECK(tally.frameTimeSumUs == 9u * 4200u);

    // A frame that is refused gives neither, and a driver that fills in neither leaves both counts at zero.
    cursor = fl::adv::LatencyCursor{};
    auto refused = Report(6000u, 3u);
    refused->frameReport[61].gpuActiveRenderTimeUs = 3500u;
    refused->frameReport[61].gpuFrameTimeUs = 4200u;
    refused->frameReport[61].presentEndTime = refused->frameReport[61].presentStartTime - 50u;
    fl::adv::ObserveLatencyReport(*refused, cursor, tally);
    CHECK(tally.frames == 0u);
    CHECK(tally.refused == 1u);
    CHECK(tally.activeFrames == 0u);
    CHECK(tally.frameTimeFrames == 0u);

    cursor = fl::adv::LatencyCursor{};
    auto older = Report(7000u);
    fl::adv::ObserveLatencyReport(*older, cursor, tally);
    CHECK(tally.frames == 62u);
    CHECK(tally.activeFrames == 0u);
    CHECK(tally.activeSumUs == 0u);
    CHECK(tally.frameTimeFrames == 0u);
}

TEST_CASE("a multiplier the status gave has a bit of its own, and thirty-one and more share the last", "[latency]") {
    CHECK(fl::adv::LatencyMultiplierBit(0u) == 1u);
    CHECK(fl::adv::LatencyMultiplierBit(1u) == 2u);
    CHECK(fl::adv::LatencyMultiplierBit(4u) == 16u);
    CHECK(fl::adv::LatencyMultiplierBit(31u) == 0x80000000u);
    CHECK(fl::adv::LatencyMultiplierBit(255u) == 0x80000000u);
}

TEST_CASE("a publication carries one read, packed as fl_shm.h says, and carries it once", "[latency]") {
    fl::adv::Accumulators a;
    fl::FlAdvancedState   region{};
    fl::adv::LatencyOut   out;
    out.state = fl::FL_LAT_READING;
    out.sleepSeenOn = 1u | 8u;
    out.sleepSeenOff = 2u | 4u | 16u;
    out.multiplier = 2u;
    out.multipliersSeen = (1u << 1) | (1u << 2);
    out.reads = 70'000u;    // stops at 65535
    out.worstUs = 66u;
    out.tally.frames = 10u;
    out.tally.refused = 1u;
    for (std::uint32_t i = 0; i < fl::adv::kLatSpans; ++i) {
        out.tally.sumUs[i] = 1000u * (i + 1u);
    }
    out.tally.lacking = 1u << 3;
    out.tally.activeSumUs = 31'500u;
    out.tally.activeFrames = 9u;
    out.tally.frameTimeSumUs = 42'000u;
    out.tally.frameTimeFrames = 10u;

    fl::adv::Publish(a, &region, 7u, 1234u, &out);
    CHECK((region.latState & FL_LAT_STATE_MASK) == fl::FL_LAT_READING);
    CHECK(((region.latState >> FL_LAT_SLEEP_SEEN_ON_SHIFT) & FL_LAT_SLEEP_BITS) == (1u | 8u));
    CHECK(((region.latState >> FL_LAT_SLEEP_SEEN_OFF_SHIFT) & FL_LAT_SLEEP_BITS) == (2u | 4u | 16u));
    CHECK((region.latState >> FL_LAT_FG_MULTIPLIER_SHIFT) == 2u);
    CHECK((region.latReads & 0xFFFFu) == 0xFFFFu);
    CHECK((region.latReads >> 16) == 66u);
    CHECK((region.latFrames & 0xFFFFu) == 10u);
    CHECK((region.latFrames >> 16) == 1u);
    CHECK(region.latSimulationSumUs == 1000u);
    CHECK(region.latSubmitSumUs == 2000u);
    CHECK(region.latPresentSumUs == 3000u);
    CHECK(region.latDriverSumUs == FL_LAT_SUM_NONE);
    CHECK(region.latOsQueueSumUs == 5000u);
    CHECK(region.latGpuSumUs == 6000u);
    CHECK(region.latTotalSumUs == 7000u);
    CHECK(region.latGpuActiveSumUs == 31'500u);
    CHECK(region.latGpuFrameSumUs == 42'000u);
    CHECK((region.latGpuCounts & 0xFFFFu) == 9u);
    CHECK((region.latGpuCounts >> 16) == 10u);
    CHECK(region.latMultipliersSeen == ((1u << 1) | (1u << 2)));
    CHECK(region.request == 7u);

    // The next publication, with no read in between: the state stands, the frames and the sums are gone.
    fl::adv::Publish(a, &region, 7u, 1235u, &out);
    CHECK((region.latState & FL_LAT_STATE_MASK) == fl::FL_LAT_READING);
    CHECK(region.latFrames == 0u);
    CHECK(region.latTotalSumUs == 0u);
    CHECK(region.latDriverSumUs == 0u);
    CHECK(region.latGpuActiveSumUs == 0u);
    CHECK(region.latGpuFrameSumUs == 0u);
    CHECK(region.latGpuCounts == 0u);
    CHECK(region.latMultipliersSeen == ((1u << 1) | (1u << 2)));    // the session's, like the switches
    CHECK((region.latReads >> 16) == 66u);

    // A caller with no latency summary leaves the latency words alone.
    fl::FlAdvancedState untouched{};
    fl::adv::Publish(a, &untouched, 7u, 1236u);
    CHECK(untouched.latState == 0u);
    CHECK(untouched.latReads == 0u);
    CHECK(untouched.latTotalSumUs == 0u);
    CHECK(untouched.latGpuActiveSumUs == 0u);
    CHECK(untouched.latGpuCounts == 0u);
    CHECK(untouched.latMultipliersSeen == 0u);
}
