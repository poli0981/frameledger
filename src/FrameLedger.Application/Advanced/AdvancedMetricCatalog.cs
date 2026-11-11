// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Collections.Frozen;

using static FrameLedger.Application.Advanced.AdvancedGroup;
using static FrameLedger.Application.Advanced.AdvancedReach;
using static FrameLedger.Application.Advanced.AdvancedSource;
using static FrameLedger.Application.Advanced.AdvancedUnit;

namespace FrameLedger.Application.Advanced;

/// <summary>
/// Everything "Advanced metrics" (owner decision D101) knows of: what the vendored NVAPI and Streamline headers, the
/// FidelityFX headers and LibreHardwareMonitor make available — what is read, and what is not with the reason.
/// </summary>
/// <remarks>
/// ONE LIST FOR EVERY CONSUMER. Before it, a metric's name, unit and label were repeated in three to five places
/// (the trend's enum, its unit switch, its label list, Compare's rows, the summary's cards). The reader that
/// collects, the recorder that stores, the page that shows and the table in
/// <c>docs/18_GPU_VENDOR_APIS.md</c> §The inventory all read this list, and a test holds that table to it — so an
/// entry cannot be stored without being shown, or documented as read while it is not.
/// <para>
/// AN ENTRY THAT IS NOT READ IS STILL AN ENTRY. The owner asked for everything the headers give; what stays out
/// stays out for a reason — a hook that is not built, a route nobody has proven safe, a licence, an identifier,
/// the player's own state — and the page's Sources tab says which.
/// </para>
/// </remarks>
public static class AdvancedMetricCatalog
{
    /// <summary>
    /// The entries, in the order the page lists them. An id is on disk from the first release that stores it and
    /// never changes after; an entry is added at the end of its block.
    /// </summary>
    public static IReadOnlyList<AdvancedMetric> All { get; } =
    [
        // ---- NVAPI, the graphics card: facts -------------------------------------------------------------------
        Machine("nvapi.gpu.type", Nvapi, Agent, Text, Gpu, "Integrated or discrete graphics"),
        Machine("nvapi.gpu.system_type", Nvapi, Agent, Text, Gpu, "Laptop or desktop, as the driver sees the PC"),
        Machine("nvapi.gpu.external", Nvapi, Agent, Flag, Gpu, "The card is an external one"),
        Machine("nvapi.gpu.bus_type", Nvapi, Agent, Text, Gpu, "The bus the card sits on"),
        Machine("nvapi.gpu.pci_ids", Nvapi, Agent, Text, Gpu, "PCI device, subsystem and revision ids - the model, not the unit"),
        Machine("nvapi.gpu.vbios_version", Nvapi, Agent, Text, Gpu, "Video BIOS version"),
        Machine("nvapi.gpu.architecture", Nvapi, Agent, Text, Gpu, "Chip architecture, implementation and revision"),
        Machine("nvapi.gpu.core_count", Nvapi, Agent, Count, Gpu, "Graphics cores"),
        Machine("nvapi.gpu.rt_cores", Nvapi, Agent, Count, Gpu, "Ray-tracing cores"),
        Machine("nvapi.gpu.tensor_cores", Nvapi, Agent, Count, Gpu, "Tensor cores"),
        Machine("nvapi.gpu.ram_bus_width", Nvapi, Agent, Bits, Gpu, "Width of the card's memory bus"),
        Machine("nvapi.gpu.clock_base_core", Nvapi, Agent, Megahertz, Gpu, "Base core clock"),
        Machine("nvapi.gpu.clock_base_memory", Nvapi, Agent, Megahertz, Gpu, "Base memory clock"),
        Machine("nvapi.gpu.clock_boost_core", Nvapi, Agent, Megahertz, Gpu, "Boost core clock"),
        Machine("nvapi.gpu.clock_boost_memory", Nvapi, Agent, Megahertz, Gpu, "Boost memory clock"),
        Machine("nvapi.gpu.temp_default_max", Nvapi, Agent, Celsius, Gpu, "The core sensor's default maximum temperature"),

        // ---- NVAPI, the card's memory --------------------------------------------------------------------------
        Machine("nvapi.vram.dedicated", Nvapi, Agent, Mebibytes, GpuMemory, "Dedicated video memory on the card"),
        Machine("nvapi.vram.dedicated_available", Nvapi, Agent, Mebibytes, GpuMemory, "Dedicated video memory available for surfaces"),
        Machine("nvapi.vram.system", Nvapi, Agent, Mebibytes, GpuMemory, "System memory the driver reserved at load"),
        Machine("nvapi.vram.shared", Nvapi, Agent, Mebibytes, GpuMemory, "Shared system memory the driver may commit"),
        Machine("nvapi.vram.framebuffer_physical", Nvapi, Agent, Mebibytes, GpuMemory, "Physical frame buffer size"),
        Machine("nvapi.vram.framebuffer_virtual", Nvapi, Agent, Mebibytes, GpuMemory, "Virtual frame buffer size"),
        Sampled("nvapi.vram.available", Nvapi, Agent, Mebibytes, GpuMemory, "Dedicated video memory available now, all programs"),
        Sampled("nvapi.vram.in_use", Nvapi, Agent, Mebibytes, GpuMemory, "Dedicated video memory in use now, all programs: available for surfaces less available now"),
        Sampled("nvapi.vram.evictions_size", Nvapi, Agent, Mebibytes, GpuMemory, "Memory released by evictions since the driver loaded"),
        Sampled("nvapi.vram.eviction_count", Nvapi, Agent, Count, GpuMemory, "Evictions from dedicated video memory since the driver loaded"),
        Sampled("nvapi.vram.promotions_size", Nvapi, Agent, Mebibytes, GpuMemory, "Memory moved in by promotions since the driver loaded"),
        Sampled("nvapi.vram.promotion_count", Nvapi, Agent, Count, GpuMemory, "Promotions into dedicated video memory since the driver loaded"),

        // ---- NVAPI, the card over time -------------------------------------------------------------------------
        Sampled("nvapi.load.framebuffer", Nvapi, Agent, Percent, Gpu, "Frame-buffer (memory controller) utilisation"),
        Sampled("nvapi.load.video", Nvapi, Agent, Percent, Gpu, "Video-engine utilisation"),
        Sampled("nvapi.load.bus", Nvapi, Agent, Percent, Gpu, "Bus-interface utilisation"),
        Sampled("nvapi.clock.video", Nvapi, Agent, Megahertz, Gpu, "Video clock"),
        Sampled("nvapi.pstate", Nvapi, Agent, Count, Gpu, "The card's current performance state (P-state id)"),
        Sampled("nvapi.temp.board", Nvapi, Agent, Celsius, Gpu, "Board ambient temperature, where the card has the sensor"),
        Sampled("nvapi.temp.power_supply", Nvapi, Agent, Celsius, Gpu, "The card's power-supply temperature, where it has the sensor"),
        Sampled("nvapi.limit.ac_battery", Nvapi, Agent, Percent, Gpu, "Held back by a switch to battery on the tick: 100 or 0"),
        Sampled("nvapi.limit.api", Nvapi, Agent, Percent, Gpu, "Held back at a program's request on the tick: 100 or 0"),
        Sampled("nvapi.limit.insufficient_power", Nvapi, Agent, Percent, Gpu, "Held back by a missing power connector on the tick: 100 or 0"),
        Sampled("nvapi.encoder.sessions", Nvapi, Agent, Count, Gpu, "Active hardware-encoder sessions, all programs"),
        Sampled("nvapi.encoder.fps", Nvapi, Agent, FramesPerSecond, Gpu, "Average encoding rate of the active sessions"),
        Sampled("nvapi.encoder.latency", Nvapi, Agent, Microseconds, Gpu, "Average encoding latency"),

        // ---- NVAPI, the driver ---------------------------------------------------------------------------------
        Machine("nvapi.driver.package", Nvapi, Agent, Text, Driver, "Game Ready, Studio or an RTX enterprise branch"),
        Machine("nvapi.driver.dch", Nvapi, Agent, Flag, Driver, "The driver is a DCH package"),
        Machine("nvapi.driver.base_branch", Nvapi, Agent, Text, Driver, "The driver's base branch"),
        Machine("nvapi.driver.interface_version", Nvapi, Agent, Text, Driver, "NVAPI's own interface version"),
        Machine("nvapi.driver.chipset", Nvapi, Agent, Text, Driver, "Chipset vendor and name, as the driver reports them"),
        Machine("nvapi.driver.global_profile", Nvapi, Agent, Table, Driver, "Every setting of the driver's global profile, read-only"),
        Session("nvapi.session.driver_profile", Nvapi, Agent, Table, Driver, "The settings the driver's profile for this game sets itself, read-only"),

        // ---- NVAPI, displays -----------------------------------------------------------------------------------
        MachineFamily("nvapi.display.hdr_formats", Nvapi, Agent, Text, Display, "HDR formats a display accepts: HDR10, HDR10+, HDR10+ Gaming, Dolby Vision, EDR"),
        MachineFamily("nvapi.display.luminance_max", Nvapi, Agent, Nits, Display, "A display's maximum luminance for HDR content"),
        MachineFamily("nvapi.display.luminance_min", Nvapi, Agent, Nits, Display, "Its minimum luminance"),
        MachineFamily("nvapi.display.luminance_max_frame_average", Nvapi, Agent, Nits, Display, "Its maximum frame-average light level"),
        MachineFamily("nvapi.display.vrr_support", Nvapi, Agent, Text, Display, "Variable refresh: supported, G-SYNC module or adaptive sync, ULMB, usable at the current mode"),
        MachineFamily("nvapi.display.adaptive_sync_off", Nvapi, Agent, Flag, Display, "Adaptive sync is switched off on the display"),
        MachineFamily("nvapi.display.max_frame_interval", Nvapi, Agent, Microseconds, Display, "The longest frame interval adaptive sync is set to hold"),
        Session("nvapi.session.vrr_enabled", Nvapi, Agent, Flag, Display, "Variable refresh was active on the game's display"),
        Session("nvapi.session.vrr_requested", Nvapi, Agent, Flag, Display, "Variable refresh was requested for it"),
        Session("nvapi.session.vrr_possible", Nvapi, Agent, Flag, Display, "Variable refresh was possible on it"),
        Session("nvapi.session.vrr_display_mode", Nvapi, Agent, Flag, Display, "The display itself was in variable-refresh mode"),
        Session("nvapi.session.source_colorspace", Nvapi, Agent, Text, Display, "The colour space the game presented in, as the driver reports it for the process"),

        // ---- LibreHardwareMonitor, the graphics card (every vendor, unelevated) ---------------------------------
        SampledFamily("lhm.gpu.temperature", Lhm, Agent, Celsius, Gpu, "Every temperature sensor of the card"),
        SampledFamily("lhm.gpu.load", Lhm, Agent, Percent, Gpu, "Every load of the card: its engines, its bus, its memory controller"),
        SampledFamily("lhm.gpu.memory", Lhm, Agent, Mebibytes, GpuMemory, "The card's memory counters: total, free, used, Direct3D dedicated and shared"),
        SampledFamily("lhm.gpu.clock", Lhm, Agent, Megahertz, Gpu, "Every clock of the card"),
        SampledFamily("lhm.gpu.power", Lhm, Agent, Watts, Gpu, "Every power reading of the card"),
        SampledFamily("lhm.gpu.fan", Lhm, Agent, Rpm, Gpu, "Every fan of the card"),
        SampledFamily("lhm.gpu.fan_control", Lhm, Agent, Percent, Gpu, "Each fan's duty"),
        SampledFamily("lhm.gpu.voltage", Lhm, Agent, Volts, Gpu, "Every voltage of the card"),
        SampledFamily("lhm.gpu.current", Lhm, Agent, Amperes, Gpu, "Every current of the card"),
        SampledFamily("lhm.gpu.throughput", Lhm, Agent, BytesPerSecond, Gpu, "PCI Express traffic to and from the card"),

        // ---- LibreHardwareMonitor through PawnIO (the Agent as administrator, D103) -----------------------------
        SampledFamily("lhm.cpu.temperature", Lhm, AgentElevated, Celsius, Cpu, "Processor temperatures: the package and each core"),
        SampledFamily("lhm.cpu.load", Lhm, AgentElevated, Percent, Cpu, "Processor load: the total and each core"),
        SampledFamily("lhm.cpu.clock", Lhm, AgentElevated, Megahertz, Cpu, "Each core's clock, and the bus clock"),
        SampledFamily("lhm.cpu.power", Lhm, AgentElevated, Watts, Cpu, "Processor power: the package, the cores"),
        SampledFamily("lhm.cpu.voltage", Lhm, AgentElevated, Volts, Cpu, "Processor voltages"),
        Machine("lhm.board.model", Lhm, AgentElevated, Text, Board, "The motherboard's maker and model"),
        Machine("lhm.board.sensor_chip", Lhm, AgentElevated, Text, Board, "The board's sensor chip"),
        SampledFamily("lhm.board.temperature", Lhm, AgentElevated, Celsius, Board, "The board's temperature sensors"),
        SampledFamily("lhm.board.fan", Lhm, AgentElevated, Rpm, Board, "Each fan header's speed"),
        SampledFamily("lhm.board.fan_control", Lhm, AgentElevated, Percent, Board, "Each fan header's duty"),
        SampledFamily("lhm.board.voltage", Lhm, AgentElevated, Volts, Board, "The board's voltage rails"),
        Machine("lhm.memory.modules", Lhm, AgentElevated, Table, Memory, "The memory modules: size, speed, maker's part name - never a serial number"),
        SampledFamily("lhm.memory.load", Lhm, AgentElevated, Percent, Memory, "Memory in use: physical and virtual"),
        SampledFamily("lhm.memory.temperature", Lhm, AgentElevated, Celsius, Memory, "Each memory module's temperature, where it has a sensor"),
        Machine("lhm.storage.models", Lhm, AgentElevated, Table, Storage, "The internal drives' model names - never a serial number"),
        SampledFamily("lhm.storage.temperature", Lhm, AgentElevated, Celsius, Storage, "Each internal drive's temperature, read no more than once a minute"),

        // ---- What the Agent could reach and does not ------------------------------------------------------------
        Machine("lhm.psu", Lhm, Refused, Table, Board, "Power-supply sensors: reading them makes the library search every USB device"),
        Machine("lhm.controller", Lhm, Refused, Table, Board, "Fan-controller sensors: the same search of USB devices"),
        Machine("lhm.battery", Lhm, Deferred, Table, Board, "Battery sensors"),
        Machine("nvapi.gpu.board_serial", Nvapi, Identifier, Text, Gpu, "The card's board serial number"),
        Machine("nvapi.gpu.uuid", Nvapi, Identifier, Text, Gpu, "The card's unique id"),
        MachineFamily("nvapi.display.edid", Nvapi, Identifier, Text, Display, "A display's EDID block, which carries its serial number"),
        Sampled("nvapi.encoder.session_processes", Nvapi, Identifier, Table, Gpu, "Which programs are encoding"),
        Sampled("nvapi.gpu.power", Nvapi, NotInHeaders, Watts, Gpu, "Card power through NVAPI (LibreHardwareMonitor reads it)"),
        Sampled("nvapi.gpu.fan_duty", Nvapi, NotInHeaders, Percent, Gpu, "Fan duty through NVAPI (LibreHardwareMonitor reads it)"),
        Sampled("nvapi.gpu.voltage", Nvapi, NotInHeaders, Volts, Gpu, "Card voltage through NVAPI (LibreHardwareMonitor reads it)"),
        Machine("nvapi.gpu.ram_type", Nvapi, NotInHeaders, Text, GpuMemory, "The card's memory type"),

        // ---- Inside the game: calls the Overlay already hooks ---------------------------------------------------
        Session("sl.dlss.output_size", Streamline, HookedCall, Text, Upscaling, "The output size the game gave DLSS"),
        Session("sl.dlss.presets", Streamline, HookedCall, Text, Upscaling, "The DLSS model preset per quality mode"),
        Session("sl.dlss.hdr_input", Streamline, HookedCall, Flag, Upscaling, "The game told DLSS its colour buffers are HDR"),
        Session("sl.dlss.auto_exposure", Streamline, HookedCall, Flag, Upscaling, "DLSS auto-exposure"),
        Session("sl.dlss.alpha_upscaling", Streamline, HookedCall, Flag, Upscaling, "DLSS upscales the alpha channel"),
        Session("sl.dlss.exposure", Streamline, HookedCall, Text, Upscaling, "Pre-exposure and exposure scale given to DLSS"),
        Session("ffx.upscale.sharpening", FidelityFx, HookedCall, Text, Upscaling, "FSR sharpening: on or off, and its strength"),
        Session("ffx.upscale.jitter", FidelityFx, HookedCall, Text, Upscaling, "The jitter offsets the game gave FSR"),
        Session("ffx.upscale.exposure", FidelityFx, HookedCall, Text, Upscaling, "Pre-exposure given to FSR"),
        Session("ffx.upscale.depth", FidelityFx, HookedCall, Text, Upscaling, "Near and far planes and field of view given to FSR"),
        Session("ffx.upscale.flags", FidelityFx, HookedCall, Text, Upscaling, "The flags of the game's FSR dispatch"),
        Sampled("ffx.upscale.frame_time_delta", FidelityFx, HookedCall, Milliseconds, Upscaling, "The frame time the game reported to FSR"),
        Sampled("ffx.memory_usage", FidelityFx, HookedCall, Mebibytes, Upscaling, "FSR's own video memory, when the game asks it"),

        // ---- Inside the game: hooks that are not built ----------------------------------------------------------
        Session("sl.constants.jitter", Streamline, NewHook, Text, Upscaling, "The jitter offsets the game gave Streamline"),
        Session("sl.constants.motion_vectors", Streamline, NewHook, Text, Upscaling, "Motion-vector scale, and whether they are 3D, dilated, jittered"),
        Session("sl.constants.depth", Streamline, NewHook, Text, Upscaling, "Near and far planes, and whether depth is inverted"),
        Session("sl.constants.projection", Streamline, NewHook, Text, Upscaling, "Field of view, aspect ratio, orthographic or not"),
        Session("sl.constants.resets", Streamline, NewHook, Count, Upscaling, "How often the game reset the upscaler's history"),
        Session("sl.feature.versions", Streamline, NewHook, Table, Upscaling, "Each Streamline feature's version and its NGX model version"),
        Session("sl.feature.requirements", Streamline, NewHook, Table, Upscaling, "What each Streamline feature requires: operating system, driver, buffers"),
        Session("sl.init.preferences", Streamline, NewHook, Text, Upscaling, "The preferences the game started Streamline with"),
        Session("ffx.framegen.config", FidelityFx, NewHook, Text, FrameGeneration, "The game's FSR frame-generation configuration"),
        Session("sl.constants.camera", Streamline, PlayerState, Text, Upscaling, "Camera position, axes and matrices"),

        // ---- Inside the game: routes a spike decides ------------------------------------------------------------
        Session("nvapi.reflex.mode", Nvapi, Spike, Text, Latency, "Reflex: low-latency mode, boost, the frame-rate limit"),
        Session("nvapi.reflex.sleep_status", Nvapi, Spike, Text, Latency, "What the driver's low-latency state is"),
        SampledFamily("nvapi.reflex.frame_report", Nvapi, Spike, Microseconds, Latency, "Latency per frame: simulation, render submit, present, driver, OS queue, GPU render"),
        Session("sl.dlss.optimal_settings", Streamline, Spike, Text, Upscaling, "The render size and sharpness DLSS recommends"),
        Sampled("sl.dlss.vram_estimate", Streamline, Spike, Mebibytes, Upscaling, "DLSS's own estimate of its video memory"),
        Session("sl.dlssg.options", Streamline, Spike, Text, FrameGeneration, "DLSS frame generation: mode, frames to generate"),
        Session("sl.dlssg.state", Streamline, Spike, Text, FrameGeneration, "DLSS frame generation: status, the most frames it can generate"),
        Sampled("sl.dlssg.frames_presented", Streamline, Spike, Count, FrameGeneration, "Frames DLSS frame generation presented, by its own count"),
        Sampled("sl.dlssg.vram_estimate", Streamline, Spike, Mebibytes, FrameGeneration, "DLSS frame generation's estimate of its video memory"),
        Session("sl.dlssd.options", Streamline, Spike, Text, Upscaling, "Ray reconstruction's options"),
        Session("sl.nis.options", Streamline, Spike, Text, Upscaling, "NVIDIA Image Scaling's options"),
        Session("sl.deepdvc.options", Streamline, Spike, Text, Upscaling, "RTX Dynamic Vibrance's options"),
        Session("sl.reflex.state", Streamline, Spike, Table, Latency, "Reflex through Streamline: availability and its frame reports"),
        Sampled("sl.pcl.markers", Streamline, Spike, Count, Latency, "PC-latency markers the game sets"),

        // ---- Closed by a licence --------------------------------------------------------------------------------
        Session("ngx.parameters", Ngx, Licence, Table, Upscaling, "NGX parameters: the quality value, the DLSS hints"),
        Session("xess.options", Xess, Licence, Table, Upscaling, "XeSS's options"),
        Sampled("sl.nvperf", Streamline, Licence, Table, Gpu, "Streamline's performance counters (NSight Perf SDK licence)"),
    ];

    private static readonly FrozenDictionary<string, AdvancedMetric> _byId = All.ToFrozenDictionary(static m => m.Id, StringComparer.Ordinal);

    /// <summary>The entry with this id, or null.</summary>
    public static AdvancedMetric? Find(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return _byId.GetValueOrDefault(id);
    }

    /// <summary>The longest key <see cref="InstanceName"/> accepts.</summary>
    public const int MaxInstanceKeyLength = 48;

    /// <summary>
    /// The series name of one member of a family: the family's <see cref="AdvancedMetric.SeriesName"/>, a dot, and a
    /// key made of lower-case letters, digits and underscores that names the sensor on this PC.
    /// </summary>
    /// <exception cref="ArgumentException">The entry is not a family of series, or the key is not such a key.</exception>
    public static string InstanceName(AdvancedMetric family, string key)
    {
        ArgumentNullException.ThrowIfNull(family);
        ArgumentNullException.ThrowIfNull(key);
        if (!family.IsFamily || family.SeriesName is not string stem)
        {
            throw new ArgumentException($"{family.Id} is not a family of series", nameof(family));
        }

        if (!IsInstanceKey(key))
        {
            throw new ArgumentException($"'{key}' is not an instance key: 1 to {MaxInstanceKeyLength} of a-z, 0-9 and _", nameof(key));
        }

        return stem + "." + key;
    }

    /// <summary>True for 1 to <see cref="MaxInstanceKeyLength"/> characters of <c>a-z</c>, <c>0-9</c> and <c>_</c>.</summary>
    public static bool IsInstanceKey(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Length is 0 or > MaxInstanceKeyLength)
        {
            return false;
        }

        foreach (char c in key)
        {
            if (c is not ((>= 'a' and <= 'z') or (>= '0' and <= '9') or '_'))
            {
                return false;
            }
        }

        return true;
    }

    private static AdvancedMetric Machine(string id, AdvancedSource source, AdvancedReach reach, AdvancedUnit unit, AdvancedGroup group, string summary)
        => new(id, AdvancedScope.MachineFact, source, reach, unit, group, summary);

    private static AdvancedMetric MachineFamily(string id, AdvancedSource source, AdvancedReach reach, AdvancedUnit unit, AdvancedGroup group, string summary)
        => new(id, AdvancedScope.MachineFact, source, reach, unit, group, summary) { IsFamily = true };

    private static AdvancedMetric Session(string id, AdvancedSource source, AdvancedReach reach, AdvancedUnit unit, AdvancedGroup group, string summary)
        => new(id, AdvancedScope.SessionFact, source, reach, unit, group, summary);

    private static AdvancedMetric Sampled(string id, AdvancedSource source, AdvancedReach reach, AdvancedUnit unit, AdvancedGroup group, string summary)
        => new(id, AdvancedScope.Series, source, reach, unit, group, summary);

    private static AdvancedMetric SampledFamily(string id, AdvancedSource source, AdvancedReach reach, AdvancedUnit unit, AdvancedGroup group, string summary)
        => new(id, AdvancedScope.Series, source, reach, unit, group, summary) { IsFamily = true };
}
