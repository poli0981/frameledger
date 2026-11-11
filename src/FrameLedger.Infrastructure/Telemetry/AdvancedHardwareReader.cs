// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Application.Advanced;

namespace FrameLedger.Infrastructure.Telemetry;

/// <summary>
/// One tick of "Advanced metrics" (owner decision D101): NVAPI's readings and LibreHardwareMonitor's, the PC's facts
/// and the state of each source, as one <see cref="AdvancedSnapshot"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>A source that is not there is a state, never an exception.</b> No NVIDIA driver, an Agent that is not
/// administrator, PawnIO not installed: <see cref="AdvancedSources"/> says which, and the entries of that source read
/// N/A with that reason. Nothing is estimated in their place (FR-4.9).
/// </para>
/// <para>
/// <b>A source that fails twice is off</b> until the reader is opened again (<c>18_GPU_VENDOR_APIS</c> §Runtime
/// policy). One case is given a single second chance first, by the outcome written down before it was built
/// (<c>20_OPEN_QUESTIONS</c> §M12): NVAPI answering with no field at all, two ticks running, after it had answered —
/// the shape a handle the driver invalidated has. The bridge is then released and initialised again, once.
/// </para>
/// <para>
/// <b>NVAPI every tick, the sensor library every fifth.</b> Measured on the dev box before anything composed this
/// (2026-11-11, <c>probe-advanced</c>, RTX 5080, unelevated): one NVAPI live read costs 0.4 ms of processor time; one
/// refresh of LibreHardwareMonitor's graphics-card group costs 14 ms — 62 ms elapsed — which at one a second is 1.4%
/// of one core, above what NFR-2 allows the whole Agent. So the sensors are refreshed on every
/// <see cref="SensorsEveryTicks"/>th tick, 0.28%, by the outcome written down before the measurement
/// (<c>20_OPEN_QUESTIONS</c> §M13). On the ticks between, a sensor has NO reading: its last value is not repeated,
/// because a value that was not read on a tick is not that tick's value. The series directory stays, so a reader of
/// the snapshot always knows what each series is.
/// </para>
/// <para>
/// <b>The PC's facts are read when the reader opens</b> and not again: they are facts. A display plugged in while the
/// page stays open is seen the next time the reader opens — some seconds after the page is left and entered again.
/// </para>
/// </remarks>
public sealed class AdvancedHardwareReader : IAdvancedHardwareReader
{
    /// <summary>Faults, or empty ticks in a row, after which a source is given up.</summary>
    public const int MaxFaults = 2;

    /// <summary>The sensor library is refreshed on the first tick and every this-many-th after it.</summary>
    public const int SensorsEveryTicks = 5;

    private readonly Lock _gate = new();
    private readonly Func<NvapiAdvancedReader> _nvapiFactory;
    private readonly Func<LhmGroups, LhmAdvancedReader> _lhmFactory;
    private readonly bool _elevated;
    private readonly bool? _pawnIoInstalled;
    private readonly TimeProvider _time;
    private readonly Action<string> _note;
    private readonly Func<uint, string?>? _monitorOf;

    private NvapiAdvancedReader? _nvapi;
    private LhmAdvancedReader? _lhm;
    private IReadOnlyList<AdvancedFact> _facts = [];
    private IReadOnlyList<AdvancedSeriesInfo> _series = [];
    private long _tick;
    private AdvancedSources _sources = AdvancedSources.Unknown;
    private int _nvapiFaults;
    private int _nvapiEmptyTicks;
    private bool _nvapiEverRead;
    private bool _nvapiReinitialised;
    private bool _opened;
    private bool _disposed;

    /// <summary>A reader over the two sources it is given the makers of. Nothing is opened until <see cref="Open"/>.</summary>
    /// <param name="nvapi">Makes NVAPI's reader; called at <see cref="Open"/> and once more if the bridge is initialised again.</param>
    /// <param name="lhm">Makes LibreHardwareMonitor's reader over the groups it is given.</param>
    /// <param name="elevated">Whether the Agent runs as administrator.</param>
    /// <param name="pawnIoInstalled">Whether PawnIO is installed; null when that could not be told, which is not a yes.</param>
    /// <param name="time">The clock a snapshot is stamped with.</param>
    /// <param name="note">Told, in one line, when a source is given up or initialised again — the Agent's log.</param>
    /// <param name="monitorOf">Finds the GDI device name of the monitor a process's window is on, for a session that did not name one; null finds none.</param>
    public AdvancedHardwareReader(
        Func<NvapiAdvancedReader> nvapi,
        Func<LhmGroups, LhmAdvancedReader> lhm,
        bool elevated,
        bool? pawnIoInstalled,
        TimeProvider time,
        Action<string>? note = null,
        Func<uint, string?>? monitorOf = null)
    {
        _monitorOf = monitorOf;
        ArgumentNullException.ThrowIfNull(nvapi);
        ArgumentNullException.ThrowIfNull(lhm);
        ArgumentNullException.ThrowIfNull(time);
        _nvapiFactory = nvapi;
        _lhmFactory = lhm;
        _elevated = elevated;
        _pawnIoInstalled = pawnIoInstalled;
        _time = time;
        _note = note ?? (static _ => { });
    }

    /// <summary>The real sources, on this process's own rights.</summary>
    public static AdvancedHardwareReader OverThisPc(TimeProvider time, Action<string>? note = null)
        => new(
            NvapiAdvancedReader.OverTheNativeBridge,
            static groups => new LhmAdvancedReader(new LhmComputerAdapter(groups)),
            LhmEnvironment.IsElevated,
            LhmEnvironment.IsPawnIoInstalled,
            time,
            note,
            static pid => Display.WindowGeometryProbe.MonitorDeviceOf([(int)pid]));

    public void Open()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_opened)
            {
                return;
            }

            _opened = true;
            (AdvancedSourceStatus nvapi, int? code) = OpenNvapi();
            LhmGroups groups = LhmAdvancedReader.GroupsFor(_elevated, _pawnIoInstalled);
            (AdvancedSourceStatus sensors, AdvancedSourceStatus elevatedSensors) = OpenLhm(groups);
            _sources = new AdvancedSources(nvapi, code, sensors, elevatedSensors);
            _facts = ReadMachineFacts();
        }
    }

    public AdvancedSnapshot Tick()
    {
        lock (_gate)
        {
            var readings = new List<AdvancedReading>();
            if (_opened && !_disposed)
            {
                readings.AddRange(TickNvapi());
                if (_tick++ % SensorsEveryTicks == 0)
                {
                    (IReadOnlyList<AdvancedReading> sensorReadings, _series) = TickLhm();
                    readings.AddRange(sensorReadings);
                }
            }

            return new AdvancedSnapshot(_time.GetUtcNow(), readings, _facts, _series, _sources);
        }
    }

    public IReadOnlyList<AdvancedFact> ReadSessionFacts(AdvancedSessionTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        lock (_gate)
        {
            if (_disposed || _nvapi is null || _sources.Nvapi != AdvancedSourceStatus.Reading)
            {
                return [];
            }

            try
            {
                // A session that names no monitor is asked about the one its window is on now - found from outside
                // the game, by a user32 question about a top-level window (WindowGeometryProbe).
                string? device = target.GdiDeviceName ?? _monitorOf?.Invoke(target.ProcessId);
                return _nvapi.ReadSessionFacts(device, target.ProcessId, target.ExecutablePath);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                NvapiFault($"session facts: {e.GetType().Name}: {e.Message}");
                return [];
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _nvapi?.Dispose();
            _nvapi = null;
            _lhm?.Dispose();
            _lhm = null;
        }
    }

    private (AdvancedSourceStatus Status, int? Code) OpenNvapi()
    {
        try
        {
            _nvapi = _nvapiFactory();
            return _nvapi.Start() ? (AdvancedSourceStatus.Reading, null) : (AdvancedSourceStatus.NotPresent, _nvapi.InitStatus);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _note($"advanced: NVAPI did not open ({e.GetType().Name}: {e.Message})");
            return (AdvancedSourceStatus.Faulted, null);
        }
    }

    private (AdvancedSourceStatus Sensors, AdvancedSourceStatus Elevated) OpenLhm(LhmGroups groups)
    {
        AdvancedSourceStatus whyNotElevated = !_elevated ? AdvancedSourceStatus.NotElevated : AdvancedSourceStatus.PawnIoMissing;
        bool wantsElevated = groups.HasFlag(LhmGroups.Cpu);
        try
        {
            _lhm = _lhmFactory(groups);
            if (_lhm.Open())
            {
                return (AdvancedSourceStatus.Reading, wantsElevated ? AdvancedSourceStatus.Reading : whyNotElevated);
            }

            _note($"advanced: the sensor library did not open ({_lhm.LastFault})");
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _note($"advanced: the sensor library did not open ({e.GetType().Name}: {e.Message})");
        }

        return (AdvancedSourceStatus.Faulted, wantsElevated ? AdvancedSourceStatus.Faulted : whyNotElevated);
    }

    private List<AdvancedFact> ReadMachineFacts()
    {
        var facts = new List<AdvancedFact>();
        if (_nvapi is not null && _sources.Nvapi == AdvancedSourceStatus.Reading)
        {
            try
            {
                facts.AddRange(_nvapi.ReadMachineFacts());
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                NvapiFault($"facts: {e.GetType().Name}: {e.Message}");
            }
        }

        if (_lhm is not null && _sources.Sensors == AdvancedSourceStatus.Reading)
        {
            facts.AddRange(_lhm.ReadFacts());
        }

        return facts;
    }

    private IReadOnlyList<AdvancedReading> TickNvapi()
    {
        if (_nvapi is null || _sources.Nvapi != AdvancedSourceStatus.Reading)
        {
            return [];
        }

        IReadOnlyList<AdvancedReading> readings;
        try
        {
            readings = _nvapi.ReadLive();
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            NvapiFault($"live: {e.GetType().Name}: {e.Message}");
            return [];
        }

        if (readings.Count > 0)
        {
            _nvapiEverRead = true;
            _nvapiEmptyTicks = 0;
            return readings;
        }

        // A card that never answers a live field is a card without those sensors, not a fault. One that answered and
        // then answers nothing, two ticks running, is the shape of a handle the driver invalidated (M12).
        if (_nvapiEverRead && ++_nvapiEmptyTicks >= MaxFaults)
        {
            _nvapiEmptyTicks = 0;
            if (_nvapiReinitialised)
            {
                GiveUpNvapi("it answered with no field again after being initialised again");
            }
            else
            {
                ReinitialiseNvapi();
            }
        }

        return [];
    }

    private void ReinitialiseNvapi()
    {
        _nvapiReinitialised = true;
        _note("advanced: NVAPI answered with no field twice running; releasing it and initialising it again, once");
        _nvapi?.Dispose();
        _nvapi = null;
        (AdvancedSourceStatus status, int? code) = OpenNvapi();
        _sources = _sources with { Nvapi = status, NvapiCode = code };
    }

    private void NvapiFault(string what)
    {
        if (++_nvapiFaults >= MaxFaults)
        {
            GiveUpNvapi(what);
        }
    }

    private void GiveUpNvapi(string why)
    {
        _note($"advanced: NVAPI is off until the reader is opened again ({why})");
        _sources = _sources with { Nvapi = AdvancedSourceStatus.Faulted, NvapiCode = null };
        _nvapi?.Dispose();
        _nvapi = null;
    }

    private (IReadOnlyList<AdvancedReading> Readings, IReadOnlyList<AdvancedSeriesInfo> Series) TickLhm()
    {
        if (_lhm is null || _sources.Sensors != AdvancedSourceStatus.Reading)
        {
            return ([], []);
        }

        int skippedBefore = _lhm.SkippedDevices;
        _lhm.Refresh();
        if (_lhm.SkippedDevices > skippedBefore)
        {
            _note($"advanced: a device is left alone after {LhmAdvancedReader.MaxFaults} faults ({_lhm.LastFault})");
        }

        return _lhm.Read();
    }
}
