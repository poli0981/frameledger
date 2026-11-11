// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Runtime.ExceptionServices;
using LibreHardwareMonitor.Hardware;

namespace FrameLedger.Infrastructure.Telemetry;

/// <summary>
/// The one LibreHardwareMonitor a process has: every <c>Computer</c> this product opens is opened and closed
/// through here, one at a time, and none is really closed while another is still in use.
/// </summary>
/// <remarks>
/// <para>
/// <b>WHY (2026-11-12, the first sessions recorded as administrator with PawnIO).</b> The Agent holds up to three
/// <c>Computer</c>s at once — a session's L2 source, its CPU-temperature reader, and the shared reader of Advanced
/// metrics — each opened and closed on its own schedule, from its own thread. LibreHardwareMonitorLib keeps part of
/// its state per process, not per <c>Computer</c>: the driver it reads the processor through and the mutexes it
/// guards the buses with. <c>Computer.Close()</c> lets go of that shared part whoever else still has a
/// <c>Computer</c> open. On the owner's PC, with an i7-14700KF:
/// </para>
/// <list type="bullet">
/// <item>after seven sessions of ten, some seconds after the session's own sources closed, the Advanced reader's
/// processor device threw <c>NullReferenceException</c> on two updates running and was left alone — the page lost
/// the processor's sensors until the reader was opened again;</item>
/// <item>once, a session's start and another instance's close met: the Advanced reader's <c>Open</c> threw
/// <c>ObjectDisposedException</c> on a wait handle, and that session stored the NVIDIA driver's seventeen series
/// and none of the sensor library's hundred and ninety-five.</item>
/// </list>
/// <para>
/// Neither can be seen unelevated — without PawnIO the processor group reads nothing through a driver — which is why
/// no test met it before a real run did. The cause is read from the timing in the Agent's log, not reproduced: the
/// tests here hold the two rules below, and whether they end the faults is the owner's next sessions to show.
/// </para>
/// <para>
/// <b>THE TWO RULES.</b> (1) <c>Open</c> and <c>Close</c> are serialised: no two run at once. (2) A <c>Computer</c>
/// closed by its user while another is open is <b>parked</b> — left open, read by nobody — and handed to the next user
/// who asks for the same groups; the library is closed for real when its last user lets go, and then every parked
/// one is closed with it. So the shared part goes only when nobody is reading.
/// </para>
/// <para>
/// <b>WHAT IS NOT SERIALISED: the reads.</b> A reader updates its devices on its own thread, as before. A lock around
/// every read would put the session's once-a-second L2 poll behind the Advanced reader's — the two were put on
/// separate threads so that a sensor that hangs in one holds up neither the other nor the pipe
/// (<c>18_GPU_VENDOR_APIS</c> §Advanced metrics). Two <c>Computer</c>s reading side by side is what ten real sessions
/// did without a fault; it is one closing under the other that failed.
/// </para>
/// <para>Parked instances are bounded by how many users were ever open at once — three in the Agent.</para>
/// </remarks>
public sealed class LhmLibrary
{
    private readonly Func<LhmGroups, ILhmComputer> _create;
    private readonly Lock _gate = new();
    private readonly List<(LhmGroups Groups, ILhmComputer Computer)> _parked = [];
    private int _users;

    /// <summary>A library over <paramref name="create"/>; the process has one (<see cref="Process"/>), a test makes its own.</summary>
    /// <param name="create">Makes a <c>Computer</c> with exactly these groups, not yet opened.</param>
    public LhmLibrary(Func<LhmGroups, ILhmComputer> create) => _create = create ?? throw new ArgumentNullException(nameof(create));

    /// <summary>The process's own: every <see cref="LhmComputerAdapter"/> goes through it.</summary>
    public static LhmLibrary Process { get; } = new(static groups => new RealComputer(groups));

    /// <summary>How many users hold a <c>Computer</c> now.</summary>
    public int Users
    {
        get
        {
            lock (_gate)
            {
                return _users;
            }
        }
    }

    /// <summary>How many <c>Computer</c>s are open with no user, waiting for the next one or for the last close.</summary>
    public int Parked
    {
        get
        {
            lock (_gate)
            {
                return _parked.Count;
            }
        }
    }

    /// <summary>
    /// An open <c>Computer</c> with these groups: a parked one where there is one, else a new one, opened here. An
    /// open that throws leaves nothing behind and counts no user.
    /// </summary>
    public ILhmComputer Open(LhmGroups groups)
    {
        lock (_gate)
        {
            int at = _parked.FindIndex(p => p.Groups == groups);
            ILhmComputer computer;
            if (at >= 0)
            {
                computer = _parked[at].Computer;
                _parked.RemoveAt(at);
            }
            else
            {
                computer = _create(groups);
                computer.Open();
            }

            _users++;
            return computer;
        }
    }

    /// <summary>
    /// Lets go of a <c>Computer</c> that <see cref="Open"/> returned. While another user remains it is parked, still
    /// open; the last user's close closes it and every parked one — all of them, even when one throws, and then the
    /// first exception is thrown on.
    /// </summary>
    public void Close(LhmGroups groups, ILhmComputer computer)
    {
        ArgumentNullException.ThrowIfNull(computer);
        lock (_gate)
        {
            if (_users > 0)
            {
                _users--;
            }

            if (_users > 0)
            {
                _parked.Add((groups, computer));
                return;
            }

            // Every one is closed whatever the one before it did: `first ??= TryClose(...)` would stop closing at the
            // first throw, which is the one outcome this method exists to rule out.
            ExceptionDispatchInfo? first = null;
            foreach ((LhmGroups _, ILhmComputer parked) in _parked)
            {
                ExceptionDispatchInfo? thrown = TryClose(parked);
                first ??= thrown;
            }

            _parked.Clear();
            ExceptionDispatchInfo? last = TryClose(computer);
            (first ?? last)?.Throw();
        }
    }

    private static ExceptionDispatchInfo? TryClose(ILhmComputer computer)
    {
        try
        {
            computer.Close();
            return null;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // Kept, not thrown here: the others are still to be closed, and the shared part of the library with them.
            return ExceptionDispatchInfo.Capture(e);
        }
    }

    /// <summary>The real <c>Computer</c>, with the groups asked for and no other (D103).</summary>
    private sealed class RealComputer(LhmGroups groups) : ILhmComputer
    {
        private readonly Computer _computer = new()
        {
            IsGpuEnabled = groups.HasFlag(LhmGroups.Gpu),
            IsCpuEnabled = groups.HasFlag(LhmGroups.Cpu),
            IsMemoryEnabled = groups.HasFlag(LhmGroups.Memory),
            IsMotherboardEnabled = groups.HasFlag(LhmGroups.Motherboard),
        };

        public IReadOnlyList<IHardware> Hardware => [.. _computer.Hardware];

        public void Open() => _computer.Open();

        public void Update() => _computer.Accept(new UpdateVisitor());

        public void Close() => _computer.Close();
    }
}
