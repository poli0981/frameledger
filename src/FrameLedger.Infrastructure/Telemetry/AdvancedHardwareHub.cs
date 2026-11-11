// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Diagnostics;
using FrameLedger.Application.Advanced;

namespace FrameLedger.Infrastructure.Telemetry;

/// <summary>
/// <see cref="IAdvancedHardware"/>: one <see cref="IAdvancedHardwareReader"/> on a thread of its own, running while
/// somebody holds it and for a short while after (owner decision D107).
/// </summary>
/// <remarks>
/// <para>
/// <b>The thread exists only while it is needed.</b> The first <see cref="Acquire"/> starts it; it opens the sources,
/// reads a tick every interval and publishes it by swapping one reference; when the last holder has let go for longer
/// than the linger it closes the sources and ends. The linger is there so that a session ending while the page is
/// being opened, or two sessions one after the other, do not close and reopen a sensor chip.
/// </para>
/// <para>
/// <b>Nobody waits on it.</b> <see cref="Latest"/> is a read of that reference. A sensor that hangs holds this thread
/// and nothing else: the holders go on seeing the last snapshot, whose <see cref="AdvancedSnapshot.TakenAt"/> says how
/// old it is.
/// </para>
/// <para>
/// <b>A tick that throws twice ends the run</b> — the reader's own guards make that a defect in the reader rather than
/// a sensor's fault, and a loop that throws every second is worse than no readings.
/// </para>
/// </remarks>
public sealed class AdvancedHardwareHub : IAdvancedHardware, IDisposable
{
    /// <summary>Ticks that may throw before the run ends.</summary>
    public const int MaxTickFaults = 2;

    private readonly Lock _gate = new();
    private readonly Func<IAdvancedHardwareReader> _readerFactory;
    private readonly TimeSpan _interval;
    private readonly TimeSpan _linger;
    private readonly Action<string> _note;
    private readonly ManualResetEventSlim _wake = new(initialState: false);

    private Thread? _thread;
    private IAdvancedHardwareReader? _reader;
    private volatile AdvancedSnapshot? _latest;
    private int _holders;
    private long _idleSince;
    private bool _disposed;

    /// <summary>A hub that reads nothing until it is held.</summary>
    /// <param name="readerFactory">Makes the reader a run uses; called on the hub's thread, once per run.</param>
    /// <param name="interval">Between two ticks.</param>
    /// <param name="linger">How long the run goes on after the last holder let go.</param>
    /// <param name="note">Told, in one line, when a run ends because its ticks threw — the Agent's log.</param>
    public AdvancedHardwareHub(Func<IAdvancedHardwareReader> readerFactory, TimeSpan interval, TimeSpan linger, Action<string>? note = null)
    {
        ArgumentNullException.ThrowIfNull(readerFactory);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(linger, TimeSpan.Zero);
        _readerFactory = readerFactory;
        _interval = interval;
        _linger = linger;
        _note = note ?? (static _ => { });
    }

    public AdvancedSnapshot? Latest => _latest;

    /// <summary>How many hold the reader now.</summary>
    public int Holders
    {
        get
        {
            lock (_gate)
            {
                return _holders;
            }
        }
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

    public IDisposable Acquire()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _holders++;
            if (_thread is null)
            {
                _thread = new Thread(Run) { IsBackground = true, Name = "fl-advanced" };
                _thread.Start();
            }
        }

        return new Hold(this);
    }

    public IReadOnlyList<AdvancedFact> ReadSessionFacts(AdvancedSessionTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        IAdvancedHardwareReader? reader;
        lock (_gate)
        {
            reader = _reader;
        }

        // Outside the lock: the read can take some hundred milliseconds, and a run that ends meanwhile disposes the
        // reader, whose own guard then answers with nothing.
        return reader?.ReadSessionFacts(target) ?? [];
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

    private void Release()
    {
        lock (_gate)
        {
            if (_holders > 0 && --_holders == 0)
            {
                _idleSince = Stopwatch.GetTimestamp();
            }
        }
    }

    private void Run()
    {
        IAdvancedHardwareReader? reader = null;
        try
        {
            reader = _readerFactory();
            reader.Open();
            lock (_gate)
            {
                _reader = reader;
            }

            int faults = 0;
            while (!Finished())
            {
                try
                {
                    _latest = reader.Tick();
                    faults = 0;
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    if (++faults >= MaxTickFaults)
                    {
                        _note($"advanced: the reader's tick threw {MaxTickFaults} times running and the run ends ({e.GetType().Name}: {e.Message})");
                        break;
                    }
                }

                Wait();
            }
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _note($"advanced: the reader did not start ({e.GetType().Name}: {e.Message})");
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_thread, Thread.CurrentThread))
                {
                    _thread = null;
                    _reader = null;
                    _latest = null;
                }
            }

            reader?.Dispose();
        }
    }

    /// <summary>
    /// True when the run is over: the hub is disposed, or nobody has held it for the linger. Decided under the lock,
    /// and the thread's slot is cleared in the same step, so an <see cref="Acquire"/> that arrives a moment later
    /// starts a run of its own rather than joining one that is closing.
    /// </summary>
    private bool Finished()
    {
        lock (_gate)
        {
            if (!_disposed && (_holders > 0 || Stopwatch.GetElapsedTime(_idleSince) < _linger))
            {
                return false;
            }

            if (ReferenceEquals(_thread, Thread.CurrentThread))
            {
                _thread = null;
                _reader = null;
                _latest = null;
            }

            return true;
        }
    }

    private void Wait()
    {
        try
        {
            _wake.Wait(_interval);
        }
        catch (ObjectDisposedException)
        {
            // Dispose gave up waiting for this thread and released the event: the next Finished() ends the run.
        }
    }

    private sealed class Hold(AdvancedHardwareHub hub) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                hub.Release();
            }
        }
    }
}
