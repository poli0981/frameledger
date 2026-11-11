// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Infrastructure.Telemetry;

namespace FrameLedger.Infrastructure.Tests.Telemetry;

/// <summary>A scripted <see cref="INvapiAdvancedBridge"/>: what each ABI 3 read answers. A read left null answers a status.</summary>
internal sealed class FakeNvapiAdvancedBridge : INvapiAdvancedBridge
{
    /// <summary>What a read with nothing scripted answers: <c>FL_NV_NOT_INITIALISED</c>.</summary>
    public const int NotAnswered = -1000;

    public NvapiFacts? Facts { get; set; }

    public NvapiLive? Live { get; set; }

    public NvapiDisplays? Displays { get; set; }

    public NvapiSessionDisplay? Session { get; set; }

    /// <summary>The global profile's list and entries, answered for a null path.</summary>
    public (NvapiProfileList List, NvapiProfileEntry[] Entries)? GlobalProfile { get; set; }

    /// <summary>What a path's own profile answers.</summary>
    public (NvapiProfileList List, NvapiProfileEntry[] Entries)? GameProfile { get; set; }

    public List<string> Calls { get; } = [];

    public int ReadFacts(out NvapiFacts facts)
    {
        Calls.Add("facts");
        facts = Facts ?? default;
        return Facts is null ? NotAnswered : 0;
    }

    public int ReadLive(out NvapiLive live)
    {
        Calls.Add("live");
        live = Live ?? default;
        return Live is null ? NotAnswered : 0;
    }

    public int ReadDisplays(out NvapiDisplays displays)
    {
        Calls.Add("displays");
        displays = Displays ?? default;
        return Displays is null ? NotAnswered : 0;
    }

    public int SessionDisplay(string gdiDeviceName, uint pid, out NvapiSessionDisplay display)
    {
        Calls.Add($"session-display {gdiDeviceName} {pid}");
        display = Session ?? default;
        return Session is null ? NotAnswered : 0;
    }

    public int ProfileSettings(string? exePath, bool ownOnly, out NvapiProfileList list, out IReadOnlyList<NvapiProfileEntry> entries)
    {
        Calls.Add($"profile {exePath ?? "(global)"} own={ownOnly}");
        (NvapiProfileList List, NvapiProfileEntry[] Entries)? scripted = exePath is null ? GlobalProfile : GameProfile;
        list = scripted?.List ?? default;
        entries = scripted?.Entries ?? [];
        return scripted is null ? NotAnswered : 0;
    }

    /// <summary>Every field of <c>FlNvFacts</c> present — the bridge's answer on the dev box's RTX 5080, 2026-11-11.</summary>
    public static NvapiFacts FullFacts() => new()
    {
        Present = 0xFFFFF,
        GpuIndex = 0,
        GpuCount = 1,
        GpuType = 2,
        SystemType = 2,
        External = 0,
        BusType = 3,
        PciDeviceId = 0x2C0210DE,
        PciSubSystemId = 0x41961458,
        PciRevisionId = 0xA1,
        ArchId = 0x1B0,
        ArchImplementation = 0x3,
        ArchRevision = 0xA1,
        CoreCount = 10752,
        RtCores = 84,
        TensorCores = 336,
        RamBusWidth = 256,
        DriverFlags = 0x5,
        ClockBaseCoreMhz = 2295,
        ClockBaseMemoryMhz = 15001,
        ClockBoostCoreMhz = 2670,
        ClockBoostMemoryMhz = 15001,
        TempDefaultMaxC = 150,
        FbPhysicalKb = 16666368,
        FbVirtualKb = 16694272,
        VramDedicatedBytes = 16303UL << 20,
        VramDedicatedAvailableBytes = 15979UL << 20,
        VramSystemBytes = 0,
        VramSharedBytes = 16300UL << 20,
        Vbios = "98.03.6c.00.68",
        BaseBranch = "R620",
        InterfaceVersion = "NVidia Complete Version 1.10",
        ChipsetVendor = "Intel",
        ChipsetName = "Intel-RaptorLake",
    };

    /// <summary>Every field of <c>FlNvLive</c> present, including the two temperatures the RTX 5080 does not have.</summary>
    public static NvapiLive FullLive() => new()
    {
        Present = 0x3FF,
        VramAvailableBytes = 14714UL << 20,
        VramDedicatedAvailableBytes = 15979UL << 20,
        EvictionsBytes = 89UL << 20,
        EvictionCount = 184,
        PromotionsBytes = 3UL << 20,
        PromotionCount = 1,
        LoadFramebufferPct = 1,
        LoadVideoPct = 0,
        LoadBusPct = 3,
        ClockVideoMhz = 2295,
        TempBoardC = 41,
        TempPowerSupplyC = 47,
        Pstate = 0,
        ThrottleReasons = 0x08,
        EncoderSessions = 1,
        EncoderFps = 60,
        EncoderLatencyUs = 4200,
    };

    /// <summary>Two displays: an HDR adaptive-sync one answering everything, and one that answered nothing.</summary>
    public static NvapiDisplays TwoDisplays()
    {
        var displays = new NvapiDisplay[NvapiDisplays.MaxDisplays];
        displays[0] = new NvapiDisplay
        {
            DisplayId = 0x80061082,
            Present = NvapiDisplay.HasHdr | NvapiDisplay.HasCaps | NvapiDisplay.HasVrr | NvapiDisplay.HasAdaptiveSync,
            Active = 1,
            HdrFlags = NvapiDisplay.HdrSt2084 | NvapiDisplay.HdrHdr10Plus | 0x0A,
            LuminanceMax = 417,
            LuminanceMinTenThousandths = 698,
            LuminanceMaxFrameAverage = 391,
            CapsFlags = NvapiDisplay.CapsVrr | NvapiDisplay.CapsVrrNow,
            VrrFlags = NvapiDisplay.VrrEnabled,
            AdaptiveSyncFlags = 0,
            MaxFrameIntervalUs = 0,
        };
        displays[1] = new NvapiDisplay { DisplayId = 0x80061083, Present = 0, Active = 0 };
        return new NvapiDisplays { Count = 2, Displays = displays };
    }
}
