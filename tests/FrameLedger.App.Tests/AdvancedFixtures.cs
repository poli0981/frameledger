// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Security.Cryptography;
using System.Text;
using FrameLedger.App.Services;
using FrameLedger.Application.Advanced;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Recording;
using FrameLedger.Infrastructure.Blobs;

namespace FrameLedger.App.Tests;

/// <summary>
/// One PC and one session as Advanced metrics see them, for the page's tests: a card with two fans read every fifth
/// tick, a negative rail, a table-valued fact, a fact about one display, a token — every shape the page has to show.
/// </summary>
internal static class AdvancedFixtures
{
    public const string Vram = "adv.nvapi.vram.in_use";
    public const string Fan1 = "adv.lhm.gpu.fan.gpu_nvidia_0_1";
    public const string Fan2 = "adv.lhm.gpu.fan.gpu_nvidia_0_2";
    public const string Rail = "adv.lhm.board.voltage.lpc_nct6798d_0_7";

    public static readonly DateTimeOffset T0 = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);

    public static IReadOnlyList<AdvancedSeriesInfo> Directory { get; } =
    [
        new(Fan1, "lhm.gpu.fan", "GPU Fan 1"),
        new(Fan2, "lhm.gpu.fan", "GPU Fan 2"),
        new(Rail, "lhm.board.voltage", "-12V"),
    ];

    public static AdvancedSources Sources { get; } = new(AdvancedSourceStatus.Reading, NvapiCode: null, AdvancedSourceStatus.Reading, AdvancedSourceStatus.NotElevated);

    public static IReadOnlyList<AdvancedFact> MachineFacts { get; } =
    [
        new("nvapi.driver.global_profile", Instance: null, AdvancedValue.OfRows([new AdvancedRow("Vertical sync", "0x60925292", "driver_default"), new AdvancedRow("Power management mode", "1")])),
        new("nvapi.gpu.rt_cores", Instance: null, AdvancedValue.Of(84)),
        new("nvapi.gpu.type", Instance: null, AdvancedValue.OfTokens("discrete")),
        new("nvapi.display.luminance_max", "DISPLAY1", AdvancedValue.Of(417)),
        new("nvapi.gpu.vbios_version", Instance: null, AdvancedValue.OfText("98.01.2F.00.6B")),
    ];

    public static IReadOnlyList<AdvancedFact> SessionFacts { get; } =
    [
        new("nvapi.session.vrr_enabled", Instance: null, AdvancedValue.Of(true)),
    ];

    /// <summary>What the feed shows after two ticks: memory read on both, the fans on the first only.</summary>
    public static AdvancedMachineView View(AdvancedFeedState state = AdvancedFeedState.Live) => new(
        state,
        T0.AddSeconds(1),
        MachineFacts,
        new Dictionary<string, AdvancedLivePoint>(StringComparer.Ordinal)
        {
            [Vram] = new(T0.AddSeconds(1), 5200),
            [Fan1] = new(T0, 1400),
            [Fan2] = new(T0, 1380),
        },
        Directory,
        Sources);

    /// <summary>Six seconds of a session: memory every tick, the fans and the rail on the first and the last.</summary>
    public static SensorBlob[] Blobs() =>
    [
        Blob("t_ms", SeriesCodec.EncodeFloat32([0f, 1000f, 2000f, 3000f, 4000f, 5000f])),
        Blob(Vram, SeriesCodec.EncodeSparseFloat32([5000f, 5100f, 5200f, 5300f, 5400f, 5500f])),
        Blob(Fan1, SeriesCodec.EncodeSparseFloat32([1400f, float.NaN, float.NaN, float.NaN, float.NaN, 1500f])),
        Blob(Fan2, SeriesCodec.EncodeSparseFloat32([1380f, float.NaN, float.NaN, float.NaN, float.NaN, 1480f])),
        Blob(Rail, SeriesCodec.EncodeSparseFloat32([-11.97f, float.NaN, float.NaN, float.NaN, float.NaN, -12.03f])),
    ];

    /// <summary>The session's row, written as the recorder writes it.</summary>
    public static AdvancedSessionRow Row()
    {
        string machine = AdvancedJson.OfFacts(MachineFacts);
        return new AdvancedSessionRow
        {
            MachineFactsJson = machine,
            MachineHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(machine))),
            FactsJson = AdvancedJson.OfSession(SessionFacts, Sources),
            StatsJson = AdvancedJson.OfStats(new Dictionary<string, SensorSeriesStats>(StringComparer.Ordinal)
            {
                [Vram] = new(6, 5250, 5250, 5000, 5500),
                [Fan1] = new(2, 1450, 1450, 1400, 1500),
                [Rail] = new(2, -12, -12, -12.03, -11.97),
            }),
            SeriesJson = AdvancedJson.OfSeries(Directory, 0),
        };
    }

    private static SensorBlob Blob(string series, byte[] data) => new() { Series = series, Hz = 1, Codec = SeriesCodec.Tag, Data = data };
}
