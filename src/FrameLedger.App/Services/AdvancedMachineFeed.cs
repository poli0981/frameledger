// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Application.Advanced;
using FrameLedger.Infrastructure.Ipc;
using FrameLedger.Shared.Ipc;
using Serilog;

namespace FrameLedger.App.Services;

/// <summary>
/// "This PC, now" for the Advanced page (owner decisions D101 and D107): while somebody watches, asks the Agent
/// <c>GetAdvancedMachine</c> once a second and keeps what it answered — the facts, the last value of every series, a
/// short history of each for the live charts, and which sources answer.
/// </summary>
/// <remarks>
/// <para>
/// <b>Watching is what makes the Agent read.</b> Each request keeps the Agent's shared reader running for a few
/// seconds (<c>Application.Ipc.AdvancedMachineResponder</c>), so nothing is asked — and with no game running no sensor
/// is read — unless a page holds <see cref="Watch"/>. The page lets go when it is unloaded or its window is hidden.
/// </para>
/// <para>
/// <b>Nothing here is stored.</b> The history lives in memory, a few minutes long, and is gone with the App; what a
/// session stores is the Agent's, in the ledger.
/// </para>
/// <para>
/// It fires <see cref="Changed"/> on the thread pool, like the Agent's link; a view model marshals through
/// <c>UiThread</c>.
/// </para>
/// </remarks>
public sealed class AdvancedMachineFeed : IDisposable
{
    /// <summary>How many points of each series are kept for the live charts: five minutes at one a second.</summary>
    public const int HistoryPoints = 300;

    private static readonly TimeSpan _defaultInterval = TimeSpan.FromSeconds(1);

    private readonly IAgentRequests _agent;
    private readonly TimeProvider _clock;
    private readonly TimeSpan _interval;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, AdvancedLivePoint> _values = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Queue<AdvancedLivePoint>> _history = new(StringComparer.Ordinal);

    private IReadOnlyList<AdvancedFact> _facts = [];
    private string? _factsHash;
    private long _lastTick = long.MinValue;
    private IReadOnlyList<AdvancedSeriesInfo> _series = [];
    private AdvancedSources _sources = AdvancedSources.Unknown;
    private int _watchers;
    private CancellationTokenSource? _stop;
    private volatile AdvancedMachineView _current = AdvancedMachineView.Stopped;

    /// <summary>Over the App's requests to the Agent — a viewer's link is never connected, and the feed says so.</summary>
    public AdvancedMachineFeed(IAgentRequests agent, TimeProvider clock, TimeSpan? interval = null)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _interval = interval ?? _defaultInterval;
    }

    /// <summary>After every request while watched, and once when the last watcher lets go.</summary>
    public event EventHandler? Changed;

    /// <summary>What was last learnt.</summary>
    public AdvancedMachineView Current => _current;

    /// <summary>
    /// Asks the Agent once a second until the returned handle is disposed. The first watcher starts the asking and the
    /// last one stops it; a handle disposed twice counts once.
    /// </summary>
    public IDisposable Watch()
    {
        lock (_gate)
        {
            if (_watchers++ == 0)
            {
                var stop = new CancellationTokenSource();
                _stop = stop;
                _ = Task.Run(() => RunAsync(stop.Token), CancellationToken.None);
            }
        }

        return new Watcher(this);
    }

    /// <summary>The kept points of one series, oldest first; empty for a series never read.</summary>
    public IReadOnlyList<AdvancedLivePoint> HistoryOf(string series)
    {
        lock (_gate)
        {
            return _history.TryGetValue(series, out Queue<AdvancedLivePoint>? points) ? [.. points] : [];
        }
    }

    /// <summary>One request and what it came to. The loop's body, and what a test calls.</summary>
    public async Task PollOnceAsync(CancellationToken ct = default)
    {
        AdvancedFeedState state;
        AdvancedMachineAck? ack = null;
        try
        {
            string? hash;
            lock (_gate)
            {
                hash = _factsHash;
            }

            IpcEnvelope envelope = await _agent.RequestAsync(IpcMessageType.GetAdvancedMachine, new GetAdvancedMachineRequest(hash), ct).ConfigureAwait(false);
            ack = IpcCodec.Payload<AdvancedMachineAck>(envelope);
            state = ack switch
            {
                null => AdvancedFeedState.NoAgent,
                { Ready: false } => AdvancedFeedState.Waiting,
                _ => AdvancedFeedState.Live,
            };
        }
        catch (IpcRequestException ex) when (string.Equals(ex.Code, IpcErrorCode.AdvancedOff, StringComparison.Ordinal))
        {
            state = AdvancedFeedState.Off;
        }
        catch (IpcRequestException ex) when (string.Equals(ex.Code, IpcErrorCode.UnknownType, StringComparison.Ordinal))
        {
            state = AdvancedFeedState.OlderAgent;
        }
        catch (IpcRequestException ex)
        {
            Log.Warning("advanced: GetAdvancedMachine failed ({Code}: {Message})", ex.Code, ex.Message);
            state = AdvancedFeedState.NoAgent;
        }
        catch (Exception ex) when (ex is InvalidOperationException or TimeoutException or System.IO.IOException)
        {
            // Not connected (a viewer never is), no answer in time, or the pipe dropped: the next second asks again.
            state = AdvancedFeedState.NoAgent;
        }

        if (!ct.IsCancellationRequested)
        {
            // A request still in flight when the last watcher let go answers nobody: "stopped" was already said.
            Publish(state, ack);
        }
    }

    public void Dispose()
    {
        CancellationTokenSource? stop;
        lock (_gate)
        {
            _watchers = 0;
            stop = _stop;
            _stop = null;
        }

        stop?.Cancel();
        stop?.Dispose();
    }

    private async Task RunAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(_interval, _clock);
        try
        {
            do
            {
                await PollOnceAsync(ct).ConfigureAwait(false);
            }
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            // The last watcher let go.
        }
    }

    private void Release()
    {
        CancellationTokenSource? stop;
        lock (_gate)
        {
            if (_watchers == 0 || --_watchers > 0)
            {
                return;
            }

            stop = _stop;
            _stop = null;
        }

        stop?.Cancel();
        stop?.Dispose();
        Publish(AdvancedFeedState.Stopped, ack: null);
    }

    private void Publish(AdvancedFeedState state, AdvancedMachineAck? ack)
    {
        AdvancedMachineView view;
        lock (_gate)
        {
            if (state == AdvancedFeedState.Off)
            {
                // Off means nothing is read: what was shown is not "now" any more, and is not kept as if it were.
                Forget();
            }
            else if (state == AdvancedFeedState.Live && ack is not null)
            {
                Apply(ack);
            }

            view = new AdvancedMachineView(
                state,
                _lastTick == long.MinValue ? null : DateTimeOffset.FromUnixTimeMilliseconds(_lastTick),
                _facts,
                new Dictionary<string, AdvancedLivePoint>(_values, StringComparer.Ordinal),
                _series,
                _sources);
        }

        _current = view;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Forget()
    {
        _values.Clear();
        _history.Clear();
        _facts = [];
        _factsHash = null;
        _lastTick = long.MinValue;
        _series = [];
        _sources = AdvancedSources.Unknown;
    }

    private void Apply(AdvancedMachineAck ack)
    {
        if (ack.FactsJson is not null)
        {
            _facts = AdvancedJson.ParseFacts(ack.FactsJson);
            _factsHash = ack.FactsHash;
        }
        else if (!string.Equals(ack.FactsHash, _factsHash, StringComparison.Ordinal))
        {
            // The Agent holds other facts than the ones kept here and sent none: ask for them outright next time.
            _factsHash = null;
        }

        _sources = new AdvancedSources(StatusOf(ack.Nvapi), ack.NvapiCode, StatusOf(ack.Sensors), StatusOf(ack.ElevatedSensors));
        var series = new List<AdvancedSeriesInfo>(ack.Series.Count);
        foreach (AdvancedSeriesLabel label in ack.Series)
        {
            series.Add(new AdvancedSeriesInfo(label.Series, label.MetricId, label.Label));
        }

        _series = series;
        if (ack.TakenAtUnixMs == _lastTick)
        {
            // The same tick as a second ago - the reader's interval and this one are not in step: no new point.
            return;
        }

        _lastTick = ack.TakenAtUnixMs;
        DateTimeOffset at = DateTimeOffset.FromUnixTimeMilliseconds(ack.TakenAtUnixMs);
        foreach (AdvancedReadingValue reading in ack.Readings)
        {
            var point = new AdvancedLivePoint(at, reading.Value);
            _values[reading.Series] = point;
            if (!_history.TryGetValue(reading.Series, out Queue<AdvancedLivePoint>? points))
            {
                points = new Queue<AdvancedLivePoint>();
                _history[reading.Series] = points;
            }

            points.Enqueue(point);
            while (points.Count > HistoryPoints)
            {
                points.Dequeue();
            }
        }
    }

    /// <summary>A state by its name on the wire; a name this build does not know — a newer Agent's — is unknown, never a guess.</summary>
    private static AdvancedSourceStatus StatusOf(string? name) =>
        Enum.TryParse(name, ignoreCase: false, out AdvancedSourceStatus status) && Enum.IsDefined(status) ? status : AdvancedSourceStatus.Unknown;

    private sealed class Watcher(AdvancedMachineFeed feed) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                feed.Release();
            }
        }
    }
}
