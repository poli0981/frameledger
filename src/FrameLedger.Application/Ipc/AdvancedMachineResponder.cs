// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Security.Cryptography;
using System.Text;
using FrameLedger.Application.Advanced;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.Application.Ipc;

/// <summary>
/// Answers <c>GetAdvancedMachine</c> (<c>07_IPC</c> §Messages; owner decisions D101 and D107): what the Agent's shared
/// advanced reader last read, for the App's page while it is open.
/// </summary>
/// <remarks>
/// <para>
/// <b>It reads no sensor.</b> The pipe's read loop awaits its handler, so a driver call here would hold up
/// <c>Ping</c> and <c>StopSession</c>. The answer is <see cref="IAdvancedHardware.Latest"/> — a read of one reference —
/// and the only thing awaited is the option, one row of <c>settings</c>, asked for at most once every two seconds.
/// </para>
/// <para>
/// <b>A request is also the page's hold on the reader</b> (D107: with no game running, sensors are read only while the
/// page is open). The first request acquires the reader and each one renews a lease; when no request has come for the
/// lease's length — the page closed, the App gone, the pipe dropped — the hold is let go, and the reader closes its
/// sources some seconds later unless a session holds it too. Nothing has to tell the Agent that the page closed.
/// </para>
/// <para>
/// <b>Off means nothing is read</b> (D101): with <c>telemetry.advanced</c> off the answer is <c>Error AdvancedOff</c>,
/// and a hold a previous request took is let go at once.
/// </para>
/// </remarks>
public sealed class AdvancedMachineResponder : IDisposable
{
    /// <summary>How long one request keeps the reader running. The page asks once a second; five rides out a slow one.</summary>
    public static readonly TimeSpan DefaultLease = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan _optionFor = TimeSpan.FromSeconds(2);

    private readonly IAdvancedHardware _hardware;
    private readonly Func<CancellationToken, ValueTask<bool>> _isOn;
    private readonly TimeProvider _clock;
    private readonly TimeSpan _lease;
    private readonly ITimer _timer;
    private readonly Lock _gate = new();

    private IDisposable? _hold;
    private long _touched;
    private bool _disposed;
    private (bool Value, long At)? _option;
    private (IReadOnlyList<AdvancedFact> Facts, string Json, string Hash)? _facts;

    /// <summary>Over the Agent's one reader, the option as the Agent reads it, and the clock the lease is counted on.</summary>
    public AdvancedMachineResponder(IAdvancedHardware hardware, Func<CancellationToken, ValueTask<bool>> isOn, TimeProvider clock, TimeSpan? lease = null)
    {
        _hardware = hardware ?? throw new ArgumentNullException(nameof(hardware));
        _isOn = isOn ?? throw new ArgumentNullException(nameof(isOn));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _lease = lease ?? DefaultLease;
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_lease, TimeSpan.Zero, nameof(lease));
        _timer = clock.CreateTimer(static state => ((AdvancedMachineResponder)state!).ReleaseIfExpired(), this, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    /// <summary>Whether a request's lease holds the reader now.</summary>
    public bool IsHolding
    {
        get
        {
            lock (_gate)
            {
                return _hold is not null;
            }
        }
    }

    /// <summary>The answer to one <c>GetAdvancedMachine</c>: <c>AdvancedMachineAck</c>, or <c>Error AdvancedOff</c>.</summary>
    public async ValueTask<byte[]> AnswerAsync(IpcEnvelope request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!await IsOnAsync(ct).ConfigureAwait(false))
        {
            Release();
            return IpcCodec.Encode(IpcMessageType.Error, request.Id,
                new ErrorAck(IpcErrorCode.AdvancedOff, "Advanced metrics is off (Settings, telemetry.advanced): nothing is read"));
        }

        Touch();
        return IpcCodec.Encode(IpcMessageType.AdvancedMachineAck, request.Id, AckOf(_hardware.Latest, IpcCodec.Payload<GetAdvancedMachineRequest>(request)?.FactsHash));
    }

    /// <summary>
    /// Lets the reader go when the last request is a lease old; otherwise waits for what is left of it. The timer's
    /// callback — public only so that a test with a clock of its own can run it.
    /// </summary>
    public void ReleaseIfExpired()
    {
        IDisposable? hold;
        lock (_gate)
        {
            if (_hold is null || _disposed)
            {
                return;
            }

            TimeSpan left = _lease - _clock.GetElapsedTime(_touched);
            if (left > TimeSpan.Zero)
            {
                _timer.Change(left, Timeout.InfiniteTimeSpan);
                return;
            }

            hold = _hold;
            _hold = null;
        }

        hold.Dispose();
    }

    public void Dispose()
    {
        IDisposable? hold;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            hold = _hold;
            _hold = null;
        }

        _timer.Dispose();
        hold?.Dispose();
    }

    private async ValueTask<bool> IsOnAsync(CancellationToken ct)
    {
        (bool Value, long At)? cached;
        lock (_gate)
        {
            cached = _option;
        }

        if (cached is { } known && _clock.GetElapsedTime(known.At) < _optionFor)
        {
            return known.Value;
        }

        bool value = await _isOn(ct).ConfigureAwait(false);
        lock (_gate)
        {
            _option = (value, _clock.GetTimestamp());
        }

        return value;
    }

    private void Touch()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _touched = _clock.GetTimestamp();
            if (_hold is null)
            {
                // Returns at once (IAdvancedHardware.Acquire): the sources open on the reader's own thread.
                _hold = _hardware.Acquire();
                _timer.Change(_lease, Timeout.InfiniteTimeSpan);
            }
        }
    }

    private void Release()
    {
        IDisposable? hold;
        lock (_gate)
        {
            hold = _hold;
            _hold = null;
        }

        hold?.Dispose();
    }

    private AdvancedMachineAck AckOf(AdvancedSnapshot? snapshot, string? clientHash)
    {
        if (snapshot is null)
        {
            // Held, and the reader has not finished its first tick: the page shows that it is being read.
            return new AdvancedMachineAck(Ready: false, TakenAtUnixMs: 0, FactsHash: null, FactsJson: null, Readings: [], Series: [],
                nameof(AdvancedSourceStatus.Unknown), NvapiCode: null, nameof(AdvancedSourceStatus.Unknown), nameof(AdvancedSourceStatus.Unknown));
        }

        (string json, string hash) = FactsOf(snapshot.MachineFacts);
        var readings = new List<AdvancedReadingValue>(snapshot.Readings.Count);
        foreach (AdvancedReading reading in snapshot.Readings)
        {
            // JSON has no NaN; a reading that is not a number is one the reader did not make.
            if (double.IsFinite(reading.Value))
            {
                readings.Add(new AdvancedReadingValue(reading.Series, reading.Value));
            }
        }

        var series = new List<AdvancedSeriesLabel>(snapshot.Series.Count);
        foreach (AdvancedSeriesInfo info in snapshot.Series)
        {
            series.Add(new AdvancedSeriesLabel(info.Series, info.MetricId, info.Label));
        }

        return new AdvancedMachineAck(
            Ready: true,
            snapshot.TakenAt.ToUnixTimeMilliseconds(),
            hash,
            string.Equals(hash, clientHash, StringComparison.Ordinal) ? null : json,
            readings,
            series,
            snapshot.Sources.Nvapi.ToString(),
            snapshot.Sources.NvapiCode,
            snapshot.Sources.Sensors.ToString(),
            snapshot.Sources.ElevatedSensors.ToString());
    }

    /// <summary>The facts as text and their hash, made once per list the reader publishes — it republishes the same one while nothing changes.</summary>
    private (string Json, string Hash) FactsOf(IReadOnlyList<AdvancedFact> facts)
    {
        lock (_gate)
        {
            if (_facts is { } made && ReferenceEquals(made.Facts, facts))
            {
                return (made.Json, made.Hash);
            }
        }

        string json = AdvancedJson.OfFacts(facts);
        string hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
        lock (_gate)
        {
            _facts = (facts, json, hash);
        }

        return (json, hash);
    }
}
