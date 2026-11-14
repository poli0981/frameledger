// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using FrameLedger.Application.Input;
using FrameLedger.Application.Telemetry;

namespace FrameLedger.Infrastructure.Input;

/// <summary>
/// The Agent's one input reader, on a thread of its own, running only while a session has a view of it (owner
/// decisions D155-D163, <c>21_INPUT_DEVICES</c> §The reader). With no session it has no thread, no window and no
/// registration: FrameLedger reads nothing about a controller, the keyboard or the mouse.
/// </summary>
/// <remarks>
/// <para>
/// <b>One reader, several sessions.</b> Sessions run side by side (the watcher keeps a table of them), and a
/// controller's reports arrive once. So the thread makes one <see cref="InputSlice"/> about ten times a second and
/// hands it to every view; each view counts it for its own session when a window of that session's game was in front
/// (<see cref="InputTickAccumulator"/>).
/// </para>
/// <para>
/// <b>A pause is a gap here too.</b> While recording is paused (FR-3.9) a slice is not observed by any session.
/// </para>
/// <para>
/// <b>A slice that throws is counted and skipped</b>; <see cref="MaxFaults"/> in a row end the run with one line in
/// the Agent's log, and the sessions go on without readings. Nothing the reader does may end a session.
/// </para>
/// </remarks>
public sealed class InputDeviceHub : IInputDevices, IDisposable
{
    /// <summary>Slices that may throw in a row before the run ends.</summary>
    public const int MaxFaults = 20;

    /// <summary>How long a slice is.</summary>
    public static readonly TimeSpan SliceInterval = TimeSpan.FromMilliseconds(100);

    private readonly Lock _gate = new();
    private readonly Func<IInputBackend> _backendFactory;
    private readonly Func<bool> _paused;
    private readonly TimeSpan _interval;
    private readonly Action<string> _note;
    private readonly ManualResetEventSlim _wake = new(initialState: false);
    private readonly List<View> _views = [];
    private Thread? _thread;
    private bool _disposed;

    /// <summary>A hub that reads nothing until a view of it is opened.</summary>
    /// <param name="backendFactory">Makes the Windows side a run uses; called on the hub's thread, once per run.</param>
    /// <param name="paused">Whether recording is paused now; null is never.</param>
    /// <param name="interval">How long a slice is; null is <see cref="SliceInterval"/>.</param>
    /// <param name="note">Told, in one line, when a run ends - the Agent's log. No line names a device.</param>
    public InputDeviceHub(Func<IInputBackend> backendFactory, Func<bool>? paused = null, TimeSpan? interval = null, Action<string>? note = null)
    {
        ArgumentNullException.ThrowIfNull(backendFactory);
        _backendFactory = backendFactory;
        _paused = paused ?? (static () => false);
        _interval = interval ?? SliceInterval;
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_interval, TimeSpan.Zero, nameof(interval));
        _note = note ?? (static _ => { });
    }

    /// <summary>Whether a run is in progress.</summary>
    public bool IsRunning
    {
        get
        {
            lock (_gate)
            {
                return _thread is not null;
            }
        }
    }

    public IInputActivitySource Open()
    {
        var view = new View(this);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _views.Add(view);
            if (_thread is null)
            {
                _thread = new Thread(Run) { IsBackground = true, Name = "fl-input" };
                _thread.Start();
            }
        }

        return view;
    }

    public void Dispose()
    {
        Thread? thread;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            thread = _thread;
        }

        _wake.Set();
        if (thread is not null && thread != Thread.CurrentThread)
        {
            thread.Join(TimeSpan.FromSeconds(5));
        }

        _wake.Dispose();
    }

    private void Close(View view)
    {
        lock (_gate)
        {
            _views.Remove(view);
        }
    }

    private void Run()
    {
        IInputBackend? backend = null;
        InputHealth last = new();
        try
        {
            backend = _backendFactory();
            var reader = new InputReader(backend);
            reader.Open();
            long before = Stopwatch.GetTimestamp();
            int faults = 0;
            while (Wait())
            {
                long now = Stopwatch.GetTimestamp();
                int elapsed = (int)Math.Min(Stopwatch.GetElapsedTime(before, now).TotalMilliseconds, int.MaxValue);
                before = now;
                try
                {
                    InputSlice slice = reader.Slice(elapsed);
                    last = slice.Health;
                    faults = 0;
                    Hand(slice);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    reader.Fault();
                    if (++faults >= MaxFaults)
                    {
                        _note($"input: the reader's slice threw {MaxFaults} times running and the run ends ({e.GetType().Name}: {e.Message})");
                        break;
                    }
                }
            }
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _note($"input: the reader did not start ({e.GetType().Name}: {e.Message})");
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_thread, Thread.CurrentThread))
                {
                    _thread = null;
                }
            }

            backend?.Dispose();
            _note(Summary(last));
        }
    }

    /// <summary>What a run did, as the Agent's log keeps it: counts and yes-or-no answers, nothing of any device.</summary>
    private static string Summary(InputHealth h) => string.Create(CultureInfo.InvariantCulture,
        $"input: run ended - slices {h.Slices}, raw input {(h.RawInputRegistered ? "registered" : "NOT registered")}, reports {h.RawReports} ({h.RawReportsUnread} not read), "
        + $"xinput {(h.XInputAvailable ? "there" : "absent")} with {h.XInputReads} reads and {h.XInputSilent} silent slices, last input {(h.LastInputAvailable ? "read" : "NOT read")}, "
        + $"thread cpu {h.ThreadCpuMs} ms, faults {h.Faults}");

    /// <summary>Waits a slice; false when the run is over - the hub is disposed, or no view is open.</summary>
    private bool Wait()
    {
        try
        {
            _wake.Wait(_interval);
        }
        catch (ObjectDisposedException)
        {
            // Dispose gave up waiting for this thread and released the event.
            return false;
        }

        lock (_gate)
        {
            if (!_disposed && _views.Count > 0)
            {
                return true;
            }

            // Decided under the lock and the slot cleared in the same step, so an Open that arrives a moment later
            // starts a run of its own rather than joining one that is closing.
            if (ReferenceEquals(_thread, Thread.CurrentThread))
            {
                _thread = null;
            }

            return false;
        }
    }

    private void Hand(InputSlice slice)
    {
        View[] views;
        lock (_gate)
        {
            views = [.. _views];
        }

        bool paused = _paused();
        foreach (View view in views)
        {
            view.Add(slice, paused);
        }
    }

    /// <summary>One session's view.</summary>
    private sealed class View(InputDeviceHub hub) : IInputActivitySource
    {
        private readonly InputTickAccumulator _sum = new();
        private volatile int[] _pids = [];

        public void Follow(GameProcess target)
        {
            ArgumentNullException.ThrowIfNull(target);
            _pids = target.Pid is { } pinned ? [pinned] : [.. target.Processes.Select(static p => p.Pid)];
        }

        public bool TryRead([NotNullWhen(true)] out InputReading? reading)
        {
            reading = _sum.Take();
            return reading is not null;
        }

        public void Add(InputSlice slice, bool paused) =>
            _sum.Add(slice, inFront: slice.ForegroundProcessId != 0 && Array.IndexOf(_pids, (int)slice.ForegroundProcessId) >= 0, paused);

        public void Dispose() => hub.Close(this);
    }
}
