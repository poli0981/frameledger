// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Globalization;
using FrameLedger.Application.Advanced;

namespace FrameLedger.Infrastructure.Telemetry;

/// <summary>
/// NVAPI's part of "Advanced metrics" (owner decision D101): the bridge's ABI 3 structs turned into the facts and
/// readings of <see cref="AdvancedMetricCatalog"/> — every <c>nvapi.</c> entry whose reach is the Agent, and no other.
/// </summary>
/// <remarks>
/// <para>
/// <b>What was not read is not there.</b> A bit the driver left clear, a call it refused, a display it did not find:
/// the fact or the reading is absent, and the page shows N/A. Nothing is put in its place — not a zero, not the last
/// value (FR-4.9).
/// </para>
/// <para>
/// <b>Not a poller, and it counts no faults.</b> Whoever calls it decides how often, on which thread, and when a read
/// that keeps failing is given up (<c>18_GPU_VENDOR_APIS</c> §Runtime policy) — the reader that owns the thread, not
/// this mapping.
/// </para>
/// <para>
/// <b>Identifiers never reach it</b> (D104): the bridge has no entry point for a serial number, a unique id or an
/// EDID, and the one handle it does return — NVAPI's display id — is replaced here by the display's place in the list.
/// </para>
/// </remarks>
public sealed class NvapiAdvancedReader : IDisposable
{
    private const double _mebibyte = 1024.0 * 1024.0;

    private readonly INvapiBridge _bridge;
    private readonly INvapiAdvancedBridge _advanced;
    private bool _started;
    private bool _disposed;

    public NvapiAdvancedReader(INvapiBridge bridge, INvapiAdvancedBridge advanced)
    {
        ArgumentNullException.ThrowIfNull(bridge);
        ArgumentNullException.ThrowIfNull(advanced);
        _bridge = bridge;
        _advanced = advanced;
    }

    /// <summary>The real bridge behind both ports. The reader owns it and disposes it.</summary>
    public static NvapiAdvancedReader OverTheNativeBridge()
    {
        var bridge = new NativeNvapiBridge();
        return new NvapiAdvancedReader(bridge, bridge);
    }

    /// <summary>What <c>FlNvInit</c> answered, or null before <see cref="Start"/>. Not 0: every read below is empty, and this is why.</summary>
    public int? InitStatus { get; private set; }

    /// <summary>True when the driver initialised and listed a graphics card.</summary>
    public bool IsAvailable => InitStatus == 0;

    /// <summary>Initialises NVAPI once. A PC with no NVIDIA driver answers a status, which is a normal condition and not a fault.</summary>
    public bool Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_started)
        {
            _started = true;
            InitStatus = _bridge.Init();
        }

        return IsAvailable;
    }

    /// <summary>The facts of the PC: the card, the driver, each connected display, the driver's global profile.</summary>
    public IReadOnlyList<AdvancedFact> ReadMachineFacts()
    {
        var facts = new List<AdvancedFact>();
        if (!IsAvailable)
        {
            return facts;
        }

        if (_advanced.ReadFacts(out NvapiFacts card) == 0)
        {
            AddCardFacts(facts, card);
        }

        if (_advanced.ReadDisplays(out NvapiDisplays displays) == 0)
        {
            AddDisplayFacts(facts, displays);
        }

        if (_advanced.ProfileSettings(exePath: null, ownOnly: false, out NvapiProfileList list, out IReadOnlyList<NvapiProfileEntry> entries) == 0
            && list.Status == NvapiDriverProfile.Global)
        {
            facts.Add(new AdvancedFact(Ids.GlobalProfile, Instance: null, AdvancedValue.OfRows(RowsOf(entries))));
        }

        return facts;
    }

    /// <summary>
    /// The facts of one session: the state of the display the game presents on and the colour space of its process
    /// (when <paramref name="gdiDeviceName"/> is known), and what the driver's profile for the game sets itself (when
    /// <paramref name="exePath"/> is known).
    /// </summary>
    public IReadOnlyList<AdvancedFact> ReadSessionFacts(string? gdiDeviceName, uint pid, string? exePath)
    {
        var facts = new List<AdvancedFact>();
        if (!IsAvailable)
        {
            return facts;
        }

        if (!string.IsNullOrWhiteSpace(gdiDeviceName) && _advanced.SessionDisplay(gdiDeviceName, pid, out NvapiSessionDisplay display) == 0)
        {
            AddSessionDisplayFacts(facts, display);
        }

        if (!string.IsNullOrWhiteSpace(exePath)
            && _advanced.ProfileSettings(exePath, ownOnly: true, out NvapiProfileList list, out IReadOnlyList<NvapiProfileEntry> entries) == 0)
        {
            if (list.Status == NvapiDriverProfile.Application)
            {
                facts.Add(new AdvancedFact(Ids.SessionProfile, Instance: null, AdvancedValue.OfRows(RowsOf(entries))));
            }
            else if (list.Status == NvapiDriverProfile.Global)
            {
                // The driver answered, and no profile names this game: said so, rather than left to read as "not asked".
                facts.Add(new AdvancedFact(Ids.SessionProfile, Instance: null, AdvancedValue.OfTokens(NvapiAdvancedTokens.NoProfileForGame)));
            }
        }

        return facts;
    }

    /// <summary>One reading of every series the card answers now.</summary>
    public IReadOnlyList<AdvancedReading> ReadLive()
    {
        if (!IsAvailable || _advanced.ReadLive(out NvapiLive live) != 0)
        {
            return [];
        }

        return ReadingsOf(live);
    }

    /// <summary>The card's own facts, one per field the driver filled.</summary>
    public static void AddCardFacts(ICollection<AdvancedFact> facts, NvapiFacts card)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (card.Has(NvapiFactField.GpuType) && card.GpuType switch { 1 => NvapiAdvancedTokens.Integrated, 2 => NvapiAdvancedTokens.Discrete, _ => null } is string type)
        {
            Add(facts, Ids.GpuType, AdvancedValue.OfTokens(type));
        }

        if (card.Has(NvapiFactField.SystemType) && card.SystemType switch { 1 => NvapiAdvancedTokens.Laptop, 2 => NvapiAdvancedTokens.Desktop, _ => null } is string system)
        {
            Add(facts, Ids.SystemType, AdvancedValue.OfTokens(system));
        }

        if (card.Has(NvapiFactField.GpuInfo))
        {
            Add(facts, Ids.External, AdvancedValue.Of(card.External != 0));
            Add(facts, Ids.RtCores, AdvancedValue.Of(card.RtCores));
            Add(facts, Ids.TensorCores, AdvancedValue.Of(card.TensorCores));
        }

        if (card.Has(NvapiFactField.BusType) && BusTokenOf(card.BusType) is string bus)
        {
            Add(facts, Ids.BusType, AdvancedValue.OfTokens(bus));
        }

        if (card.Has(NvapiFactField.PciIds))
        {
            Add(facts, Ids.PciIds, AdvancedValue.OfText(string.Create(CultureInfo.InvariantCulture, $"{card.PciDeviceId:X8} / {card.PciSubSystemId:X8} / {card.PciRevisionId:X2}")));
        }

        if (card.Has(NvapiFactField.Vbios) && !string.IsNullOrWhiteSpace(card.Vbios))
        {
            Add(facts, Ids.Vbios, AdvancedValue.OfText(card.Vbios));
        }

        if (card.Has(NvapiFactField.Arch))
        {
            Add(facts, Ids.Architecture, AdvancedValue.OfText(string.Create(CultureInfo.InvariantCulture, $"0x{card.ArchId:X} / 0x{card.ArchImplementation:X} / 0x{card.ArchRevision:X}")));
        }

        AddCapacityFacts(facts, card);
        AddDriverFacts(facts, card);
    }

    private static void AddCapacityFacts(ICollection<AdvancedFact> facts, NvapiFacts card)
    {
        AddNumber(facts, Ids.CoreCount, card.Has(NvapiFactField.CoreCount), card.CoreCount);
        AddNumber(facts, Ids.RamBusWidth, card.Has(NvapiFactField.RamBusWidth), card.RamBusWidth);
        AddNumber(facts, Ids.ClockBaseCore, card.Has(NvapiFactField.ClockBaseCore), card.ClockBaseCoreMhz);
        AddNumber(facts, Ids.ClockBaseMemory, card.Has(NvapiFactField.ClockBaseMemory), card.ClockBaseMemoryMhz);
        AddNumber(facts, Ids.ClockBoostCore, card.Has(NvapiFactField.ClockBoostCore), card.ClockBoostCoreMhz);
        AddNumber(facts, Ids.ClockBoostMemory, card.Has(NvapiFactField.ClockBoostMemory), card.ClockBoostMemoryMhz);
        AddNumber(facts, Ids.TempDefaultMax, card.Has(NvapiFactField.TempDefaultMax), card.TempDefaultMaxC);
        if (card.Has(NvapiFactField.Memory))
        {
            Add(facts, Ids.VramDedicated, AdvancedValue.Of(card.VramDedicatedBytes / _mebibyte));
            Add(facts, Ids.VramDedicatedAvailable, AdvancedValue.Of(card.VramDedicatedAvailableBytes / _mebibyte));
            Add(facts, Ids.VramSystem, AdvancedValue.Of(card.VramSystemBytes / _mebibyte));
            Add(facts, Ids.VramShared, AdvancedValue.Of(card.VramSharedBytes / _mebibyte));
        }

        AddNumber(facts, Ids.FramebufferPhysical, card.Has(NvapiFactField.FbPhysical), card.FbPhysicalKb / 1024.0);
        AddNumber(facts, Ids.FramebufferVirtual, card.Has(NvapiFactField.FbVirtual), card.FbVirtualKb / 1024.0);
    }

    private static void AddDriverFacts(ICollection<AdvancedFact> facts, NvapiFacts card)
    {
        if (card.Has(NvapiFactField.DriverInfo))
        {
            Add(facts, Ids.DriverPackage, AdvancedValue.OfTokens(PackageTokensOf(card.DriverFlags)));
            Add(facts, Ids.DriverDch, AdvancedValue.Of((card.DriverFlags & 0x01) != 0));
            if (!string.IsNullOrWhiteSpace(card.BaseBranch))
            {
                Add(facts, Ids.DriverBaseBranch, AdvancedValue.OfText(card.BaseBranch));
            }
        }

        if (card.Has(NvapiFactField.InterfaceVersion) && !string.IsNullOrWhiteSpace(card.InterfaceVersion))
        {
            Add(facts, Ids.InterfaceVersion, AdvancedValue.OfText(card.InterfaceVersion));
        }

        if (card.Has(NvapiFactField.Chipset) && $"{card.ChipsetVendor} {card.ChipsetName}".Trim() is { Length: > 0 } chipset)
        {
            Add(facts, Ids.Chipset, AdvancedValue.OfText(chipset));
        }
    }

    /// <summary>What each connected display can do, each under its place in the driver's list — 1, 2, …</summary>
    public static void AddDisplayFacts(ICollection<AdvancedFact> facts, NvapiDisplays displays)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (displays.Displays is not { } list)
        {
            return;
        }

        for (int i = 0; i < displays.Count && i < list.Length; i++)
        {
            NvapiDisplay d = list[i];
            string instance = (i + 1).ToString(CultureInfo.InvariantCulture);
            if ((d.Present & NvapiDisplay.HasHdr) != 0)
            {
                facts.Add(new AdvancedFact(Ids.HdrFormats, instance, AdvancedValue.OfTokens(HdrTokensOf(d.HdrFlags))));
                facts.Add(new AdvancedFact(Ids.LuminanceMax, instance, AdvancedValue.Of(d.LuminanceMax)));
                facts.Add(new AdvancedFact(Ids.LuminanceMin, instance, AdvancedValue.Of(d.LuminanceMinTenThousandths / 10000.0)));
                facts.Add(new AdvancedFact(Ids.LuminanceMaxFrameAverage, instance, AdvancedValue.Of(d.LuminanceMaxFrameAverage)));
            }

            if ((d.Present & NvapiDisplay.HasCaps) != 0)
            {
                facts.Add(new AdvancedFact(Ids.VrrSupport, instance, AdvancedValue.OfTokens(VrrSupportTokensOf(d.CapsFlags))));
            }

            if ((d.Present & NvapiDisplay.HasAdaptiveSync) != 0)
            {
                facts.Add(new AdvancedFact(Ids.AdaptiveSyncOff, instance, AdvancedValue.Of((d.AdaptiveSyncFlags & NvapiDisplay.AdaptiveSyncDisabled) != 0)));
                facts.Add(new AdvancedFact(Ids.MaxFrameInterval, instance, AdvancedValue.Of(d.MaxFrameIntervalUs)));
            }
        }
    }

    /// <summary>The game's display and its process's colour space; nothing when the driver did not find the display.</summary>
    public static void AddSessionDisplayFacts(ICollection<AdvancedFact> facts, NvapiSessionDisplay display)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (display.NvapiStatus != 0)
        {
            return;
        }

        if ((display.Present & NvapiSessionDisplay.HasVrr) != 0)
        {
            Add(facts, Ids.SessionVrrEnabled, AdvancedValue.Of((display.VrrFlags & NvapiDisplay.VrrEnabled) != 0));
            Add(facts, Ids.SessionVrrRequested, AdvancedValue.Of((display.VrrFlags & NvapiDisplay.VrrRequested) != 0));
            Add(facts, Ids.SessionVrrPossible, AdvancedValue.Of((display.VrrFlags & NvapiDisplay.VrrPossible) != 0));
            Add(facts, Ids.SessionVrrDisplayMode, AdvancedValue.Of((display.VrrFlags & NvapiDisplay.VrrDisplayMode) != 0));
        }

        if ((display.Present & NvapiSessionDisplay.HasColorSpace) != 0)
        {
            // NV_COLORSPACE_TYPE has three values today. One it gains later is shown as its number, never as a guess.
            AdvancedValue space = display.ColorSpace switch
            {
                0 => AdvancedValue.OfTokens(NvapiAdvancedTokens.Srgb),
                1 => AdvancedValue.OfTokens(NvapiAdvancedTokens.ScRgbLinear),
                12 => AdvancedValue.OfTokens(NvapiAdvancedTokens.Bt2100Pq),
                _ => AdvancedValue.OfText(display.ColorSpace.ToString(CultureInfo.InvariantCulture)),
            };
            Add(facts, Ids.SessionColorSpace, space);
        }
    }

    /// <summary>A profile's settings as rows: the driver's name for each with its id, its value in hexadecimal, and who set it.</summary>
    public static IReadOnlyList<AdvancedRow> RowsOf(IReadOnlyList<NvapiProfileEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var rows = new List<AdvancedRow>(entries.Count);
        foreach (NvapiProfileEntry e in entries)
        {
            string id = string.Create(CultureInfo.InvariantCulture, $"0x{e.Id:X8}");
            string name = string.IsNullOrWhiteSpace(e.Name) ? id : $"{e.Name} ({id})";
            // A binary or string value is not copied out of the driver: its row says the setting is there, and no more.
            rows.Add(e.Type == NvapiProfileEntry.DwordType
                ? new AdvancedRow(name, string.Create(CultureInfo.InvariantCulture, $"0x{e.Value:X8}"), e.Predefined != 0 ? NvapiAdvancedTokens.DriverDefault : NvapiAdvancedTokens.UserSet)
                : new AdvancedRow(name, string.Empty, NvapiAdvancedTokens.NotANumber));
        }

        return rows;
    }

    /// <summary>One reading per series the driver filled.</summary>
    public static IReadOnlyList<AdvancedReading> ReadingsOf(NvapiLive live)
    {
        var readings = new List<AdvancedReading>(18);
        if (live.Has(NvapiLiveField.Memory))
        {
            readings.Add(new AdvancedReading(Series.VramAvailable, live.VramAvailableBytes / _mebibyte));
            if (live.VramDedicatedAvailableBytes >= live.VramAvailableBytes)
            {
                // Arithmetic on two figures of one read, not an estimate: what surfaces can use, less what is free now.
                readings.Add(new AdvancedReading(Series.VramInUse, (live.VramDedicatedAvailableBytes - live.VramAvailableBytes) / _mebibyte));
            }

            readings.Add(new AdvancedReading(Series.EvictionsSize, live.EvictionsBytes / _mebibyte));
            readings.Add(new AdvancedReading(Series.EvictionCount, live.EvictionCount));
            readings.Add(new AdvancedReading(Series.PromotionsSize, live.PromotionsBytes / _mebibyte));
            readings.Add(new AdvancedReading(Series.PromotionCount, live.PromotionCount));
        }

        AddReading(readings, Series.LoadFramebuffer, live.Has(NvapiLiveField.LoadFramebuffer), live.LoadFramebufferPct);
        AddReading(readings, Series.LoadVideo, live.Has(NvapiLiveField.LoadVideo), live.LoadVideoPct);
        AddReading(readings, Series.LoadBus, live.Has(NvapiLiveField.LoadBus), live.LoadBusPct);
        AddReading(readings, Series.ClockVideo, live.Has(NvapiLiveField.ClockVideo), live.ClockVideoMhz);
        AddReading(readings, Series.Pstate, live.Has(NvapiLiveField.Pstate), live.Pstate);
        AddReading(readings, Series.TempBoard, live.Has(NvapiLiveField.TempBoard), live.TempBoardC);
        AddReading(readings, Series.TempPowerSupply, live.Has(NvapiLiveField.TempPowerSupply), live.TempPowerSupplyC);
        if (live.Has(NvapiLiveField.Throttle))
        {
            // NV_GPU_PERF_DECREASE_REASON_*: the thermal and power bits are the fixed series gpu_thermal_limit and
            // gpu_power_limit; these are the other three, in the same 100-or-0 form so a mean is a share in percent.
            readings.Add(new AdvancedReading(Series.LimitAcBattery, (live.ThrottleReasons & 0x04) != 0 ? 100 : 0));
            readings.Add(new AdvancedReading(Series.LimitApi, (live.ThrottleReasons & 0x08) != 0 ? 100 : 0));
            readings.Add(new AdvancedReading(Series.LimitInsufficientPower, (live.ThrottleReasons & 0x10) != 0 ? 100 : 0));
        }

        if (live.Has(NvapiLiveField.Encoder))
        {
            readings.Add(new AdvancedReading(Series.EncoderSessions, live.EncoderSessions));
            readings.Add(new AdvancedReading(Series.EncoderFps, live.EncoderFps));
            readings.Add(new AdvancedReading(Series.EncoderLatency, live.EncoderLatencyUs));
        }

        return readings;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_started && InitStatus is 0 or NativeNvapiBridge.NoGpu)
        {
            _bridge.Shutdown();
        }

        _bridge.Dispose();
    }

    private static void Add(ICollection<AdvancedFact> facts, string id, AdvancedValue value) => facts.Add(new AdvancedFact(id, Instance: null, value));

    private static void AddNumber(ICollection<AdvancedFact> facts, string id, bool present, double value)
    {
        if (present && double.IsFinite(value))
        {
            Add(facts, id, AdvancedValue.Of(value));
        }
    }

    private static void AddReading(List<AdvancedReading> readings, string series, bool present, double value)
    {
        if (present && double.IsFinite(value))
        {
            readings.Add(new AdvancedReading(series, value));
        }
    }

    private static string? BusTokenOf(uint busType) => busType switch
    {
        1 => NvapiAdvancedTokens.Pci,
        2 => NvapiAdvancedTokens.Agp,
        3 => NvapiAdvancedTokens.PciExpress,
        4 => NvapiAdvancedTokens.Fpci,
        5 => NvapiAdvancedTokens.Axi,
        _ => null,
    };

    private static List<string> PackageTokensOf(uint driverFlags)
    {
        var tokens = new List<string>(2);
        if ((driverFlags & 0x04) != 0)
        {
            tokens.Add(NvapiAdvancedTokens.GameReady);
        }

        if ((driverFlags & 0x02) != 0)
        {
            tokens.Add(NvapiAdvancedTokens.Studio);
        }

        if ((driverFlags & 0x08) != 0)
        {
            tokens.Add(NvapiAdvancedTokens.RtxProduction);
        }

        if ((driverFlags & 0x10) != 0)
        {
            tokens.Add(NvapiAdvancedTokens.RtxNewFeature);
        }

        return tokens;
    }

    private static List<string> HdrTokensOf(uint hdrFlags)
    {
        var tokens = new List<string>(5);
        if ((hdrFlags & NvapiDisplay.HdrSt2084) != 0)
        {
            tokens.Add(NvapiAdvancedTokens.Hdr10);
        }

        if ((hdrFlags & NvapiDisplay.HdrHdr10Plus) != 0)
        {
            tokens.Add(NvapiAdvancedTokens.Hdr10Plus);
        }

        if ((hdrFlags & NvapiDisplay.HdrHdr10PlusGaming) != 0)
        {
            tokens.Add(NvapiAdvancedTokens.Hdr10PlusGaming);
        }

        if ((hdrFlags & NvapiDisplay.HdrDolbyVision) != 0)
        {
            tokens.Add(NvapiAdvancedTokens.DolbyVision);
        }

        if ((hdrFlags & NvapiDisplay.HdrEdr) != 0)
        {
            tokens.Add(NvapiAdvancedTokens.Edr);
        }

        return tokens;
    }

    private static List<string> VrrSupportTokensOf(uint capsFlags)
    {
        var tokens = new List<string>(4);
        if ((capsFlags & NvapiDisplay.CapsVrr) == 0)
        {
            tokens.Add(NvapiAdvancedTokens.NotSupported);
            return tokens;
        }

        tokens.Add(NvapiAdvancedTokens.VariableRefresh);
        tokens.Add((capsFlags & NvapiDisplay.CapsTrueGsync) != 0 ? NvapiAdvancedTokens.GsyncModule : NvapiAdvancedTokens.AdaptiveSync);
        if ((capsFlags & NvapiDisplay.CapsUlmb) != 0)
        {
            tokens.Add(NvapiAdvancedTokens.Ulmb);
        }

        if ((capsFlags & NvapiDisplay.CapsVrrNow) != 0)
        {
            tokens.Add(NvapiAdvancedTokens.UsableNow);
        }

        return tokens;
    }

    private static class Ids
    {
        public const string GpuType = "nvapi.gpu.type";
        public const string SystemType = "nvapi.gpu.system_type";
        public const string External = "nvapi.gpu.external";
        public const string BusType = "nvapi.gpu.bus_type";
        public const string PciIds = "nvapi.gpu.pci_ids";
        public const string Vbios = "nvapi.gpu.vbios_version";
        public const string Architecture = "nvapi.gpu.architecture";
        public const string CoreCount = "nvapi.gpu.core_count";
        public const string RtCores = "nvapi.gpu.rt_cores";
        public const string TensorCores = "nvapi.gpu.tensor_cores";
        public const string RamBusWidth = "nvapi.gpu.ram_bus_width";
        public const string ClockBaseCore = "nvapi.gpu.clock_base_core";
        public const string ClockBaseMemory = "nvapi.gpu.clock_base_memory";
        public const string ClockBoostCore = "nvapi.gpu.clock_boost_core";
        public const string ClockBoostMemory = "nvapi.gpu.clock_boost_memory";
        public const string TempDefaultMax = "nvapi.gpu.temp_default_max";
        public const string VramDedicated = "nvapi.vram.dedicated";
        public const string VramDedicatedAvailable = "nvapi.vram.dedicated_available";
        public const string VramSystem = "nvapi.vram.system";
        public const string VramShared = "nvapi.vram.shared";
        public const string FramebufferPhysical = "nvapi.vram.framebuffer_physical";
        public const string FramebufferVirtual = "nvapi.vram.framebuffer_virtual";
        public const string DriverPackage = "nvapi.driver.package";
        public const string DriverDch = "nvapi.driver.dch";
        public const string DriverBaseBranch = "nvapi.driver.base_branch";
        public const string InterfaceVersion = "nvapi.driver.interface_version";
        public const string Chipset = "nvapi.driver.chipset";
        public const string GlobalProfile = "nvapi.driver.global_profile";
        public const string SessionProfile = "nvapi.session.driver_profile";
        public const string HdrFormats = "nvapi.display.hdr_formats";
        public const string LuminanceMax = "nvapi.display.luminance_max";
        public const string LuminanceMin = "nvapi.display.luminance_min";
        public const string LuminanceMaxFrameAverage = "nvapi.display.luminance_max_frame_average";
        public const string VrrSupport = "nvapi.display.vrr_support";
        public const string AdaptiveSyncOff = "nvapi.display.adaptive_sync_off";
        public const string MaxFrameInterval = "nvapi.display.max_frame_interval";
        public const string SessionVrrEnabled = "nvapi.session.vrr_enabled";
        public const string SessionVrrRequested = "nvapi.session.vrr_requested";
        public const string SessionVrrPossible = "nvapi.session.vrr_possible";
        public const string SessionVrrDisplayMode = "nvapi.session.vrr_display_mode";
        public const string SessionColorSpace = "nvapi.session.source_colorspace";
    }

    private static class Series
    {
        public const string VramAvailable = AdvancedMetric.SeriesPrefix + "nvapi.vram.available";
        public const string VramInUse = AdvancedMetric.SeriesPrefix + "nvapi.vram.in_use";
        public const string EvictionsSize = AdvancedMetric.SeriesPrefix + "nvapi.vram.evictions_size";
        public const string EvictionCount = AdvancedMetric.SeriesPrefix + "nvapi.vram.eviction_count";
        public const string PromotionsSize = AdvancedMetric.SeriesPrefix + "nvapi.vram.promotions_size";
        public const string PromotionCount = AdvancedMetric.SeriesPrefix + "nvapi.vram.promotion_count";
        public const string LoadFramebuffer = AdvancedMetric.SeriesPrefix + "nvapi.load.framebuffer";
        public const string LoadVideo = AdvancedMetric.SeriesPrefix + "nvapi.load.video";
        public const string LoadBus = AdvancedMetric.SeriesPrefix + "nvapi.load.bus";
        public const string ClockVideo = AdvancedMetric.SeriesPrefix + "nvapi.clock.video";
        public const string Pstate = AdvancedMetric.SeriesPrefix + "nvapi.pstate";
        public const string TempBoard = AdvancedMetric.SeriesPrefix + "nvapi.temp.board";
        public const string TempPowerSupply = AdvancedMetric.SeriesPrefix + "nvapi.temp.power_supply";
        public const string LimitAcBattery = AdvancedMetric.SeriesPrefix + "nvapi.limit.ac_battery";
        public const string LimitApi = AdvancedMetric.SeriesPrefix + "nvapi.limit.api";
        public const string LimitInsufficientPower = AdvancedMetric.SeriesPrefix + "nvapi.limit.insufficient_power";
        public const string EncoderSessions = AdvancedMetric.SeriesPrefix + "nvapi.encoder.sessions";
        public const string EncoderFps = AdvancedMetric.SeriesPrefix + "nvapi.encoder.fps";
        public const string EncoderLatency = AdvancedMetric.SeriesPrefix + "nvapi.encoder.latency";
    }
}
