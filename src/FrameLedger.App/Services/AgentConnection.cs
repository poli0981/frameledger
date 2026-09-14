using System.IO;
using FrameLedger.Infrastructure.Ipc;
using FrameLedger.Shared.Ipc;
using Serilog;

namespace FrameLedger.App.Services;

/// <summary>
/// The App's end of the pipe as a long-running state machine (<c>07_IPC</c> §Client behavior): a connect
/// round of <see cref="AgentConnectionOptions.ConnectAttempts"/> × backoff; on failure start the Agent beside
/// this executable and try again; then <c>Hello</c>, <c>GetStatus</c>, keepalive pings, and the event stream
/// until the pipe drops — then the next round. "Treat the pipe as unreliable": everything persisted comes from
/// SQLite, and this class only ever degrades the live status.
/// </summary>
/// <remarks>
/// Runs on the thread pool; <see cref="Changed"/> and <see cref="EventReceived"/> fire there, and a view model
/// marshals to the dispatcher. Nothing here touches WPF.
/// </remarks>
public sealed class AgentConnection : IAgentLink, IAsyncDisposable
{
    private readonly IAgentLauncher _launcher;
    private readonly Func<PipeClient> _pipes;
    private readonly AgentConnectionOptions _options;
    private readonly string _appVersion;
    private readonly Lock _lock = new();
    private readonly SemaphoreSlim _retryNow = new(0);
    private PipeClient? _client;
    private long _rounds;
    private long _launches;
    private volatile bool _holdLaunches;

    public AgentConnection(IAgentLauncher launcher, Func<PipeClient> pipes, AgentConnectionOptions options, string appVersion)
    {
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
        _pipes = pipes ?? throw new ArgumentNullException(nameof(pipes));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _appVersion = appVersion ?? throw new ArgumentNullException(nameof(appVersion));
    }

    public event EventHandler? Changed;

    /// <summary>Every Agent → UI event, as it arrives; the payload is decoded by whoever needs it.</summary>
    public event EventHandler<AgentEventArgs>? EventReceived;

    public AgentConnectionState State { get; private set; } = AgentConnectionState.Connecting;

    public HelloAck? Hello { get; private set; }

    public StatusAck? Status { get; private set; }

    /// <summary>Connect rounds completed (a test's clock).</summary>
    public long Rounds => Interlocked.Read(ref _rounds);

    /// <summary>How many times the Agent was started from beside this executable.</summary>
    public long Launches => Interlocked.Read(ref _launches);

    /// <summary>Skip the wait before the next round (the banner's Retry).</summary>
    public void RetryNow() => _retryNow.Release();

    /// <summary>True while the update's apply has asked that no Agent be started from beside this executable.</summary>
    public bool LaunchesHeld => _holdLaunches;

    /// <inheritdoc />
    public void SetLaunchHold(bool hold) => _holdLaunches = hold;

    /// <summary>Loop until cancelled.</summary>
    public async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await RoundAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            finally
            {
                Interlocked.Increment(ref _rounds);
            }

            await WaitForRetryAsync(ct).ConfigureAwait(false);
        }
    }

    public bool IsConnected => State == AgentConnectionState.Connected && _client is not null;

    /// <inheritdoc />
    public Task<IpcEnvelope> RequestAsync<TRequest>(string type, TRequest payload, CancellationToken ct = default)
        where TRequest : class
    {
        PipeClient? client = _client;
        if (client is null || State != AgentConnectionState.Connected)
        {
            throw new InvalidOperationException("the Agent is not connected");
        }

        return client.RequestEnvelopeAsync(type, payload, _options.RequestTimeout, ct);
    }

    /// <summary>Ask the Agent for its status now; null when not connected or when the request failed.</summary>
    public async Task<StatusAck?> RefreshStatusAsync(CancellationToken ct = default)
    {
        PipeClient? client = _client;
        if (client is null || State != AgentConnectionState.Connected)
        {
            return null;
        }

        try
        {
            StatusAck status = await client.GetStatusAsync(_options.RequestTimeout, ct).ConfigureAwait(false);
            Set(AgentConnectionState.Connected, Hello, status);
            return status;
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or IpcRequestException or ObjectDisposedException)
        {
            return null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        PipeClient? client = Interlocked.Exchange(ref _client, null);
        if (client is not null)
        {
            await client.DisposeAsync().ConfigureAwait(false);
        }

        _retryNow.Dispose();
    }

    private async Task RoundAsync(CancellationToken ct)
    {
        Set(AgentConnectionState.Connecting, null, null);
        PipeClient? client = await TryConnectAsync(ct).ConfigureAwait(false);
        if (client is null && _launcher.CanLaunch && !_holdLaunches)
        {
            Set(AgentConnectionState.Starting, null, null);
            if (_launcher.TryStart())
            {
                Interlocked.Increment(ref _launches);
                client = await TryConnectAsync(ct).ConfigureAwait(false);
            }
        }

        if (client is null)
        {
            Set(_launcher.CanLaunch ? AgentConnectionState.Offline : AgentConnectionState.Missing, null, null);
            return;
        }

        _client = client;
        try
        {
            HelloAck hello = await client.HelloAsync(_appVersion, _options.RequestTimeout, ct).ConfigureAwait(false);
            StatusAck status = await client.GetStatusAsync(_options.RequestTimeout, ct).ConfigureAwait(false);
            Set(AgentConnectionState.Connected, hello, status);
            Log.Information("agent: connected (agent {Version}, pid {Pid}, telemetry {Telemetry})", hello.AgentVersion, hello.Pid, hello.TelemetrySource);
            await PumpAsync(client, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or IpcRequestException or ObjectDisposedException or IpcFramingException)
        {
            Log.Warning("agent: connection lost ({Kind}: {Message})", ex.GetType().Name, ex.Message);
        }
        finally
        {
            _client = null;
            await client.DisposeAsync().ConfigureAwait(false);
            Set(AgentConnectionState.Offline, null, null);
        }
    }

    private async Task<PipeClient?> TryConnectAsync(CancellationToken ct)
    {
        for (int attempt = 0; attempt < _options.ConnectAttempts; attempt++)
        {
            PipeClient client = _pipes();
            try
            {
                await client.ConnectAsync(_options.ConnectTimeout, ct).ConfigureAwait(false);
                return client;
            }
            catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException)
            {
                await client.DisposeAsync().ConfigureAwait(false);
            }

            await Task.Delay(_options.ConnectBackoff, ct).ConfigureAwait(false);
        }

        return null;
    }

    /// <summary>Events until the pipe drops, with a keepalive ping whenever the stream has been quiet for the interval.</summary>
    private async Task PumpAsync(PipeClient client, CancellationToken ct)
    {
        while (true)
        {
            ValueTask<IpcEnvelope> read = client.Events.ReadAsync(ct);
            Task<IpcEnvelope> readTask = read.AsTask();
            Task first = await Task.WhenAny(readTask, Task.Delay(_options.Keepalive, ct)).ConfigureAwait(false);
            if (first != readTask)
            {
                await client.PingAsync(_options.RequestTimeout, ct).ConfigureAwait(false);
                continue;
            }

            IpcEnvelope envelope;
            try
            {
                envelope = await readTask.ConfigureAwait(false);
            }
            catch (System.Threading.Channels.ChannelClosedException)
            {
                return;
            }

            EventReceived?.Invoke(this, new AgentEventArgs(envelope));
        }
    }

    private async Task WaitForRetryAsync(CancellationToken ct)
    {
        try
        {
            await _retryNow.WaitAsync(_options.RetryInterval, ct).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void Set(AgentConnectionState state, HelloAck? hello, StatusAck? status)
    {
        lock (_lock)
        {
            State = state;
            Hello = hello;
            Status = status;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
