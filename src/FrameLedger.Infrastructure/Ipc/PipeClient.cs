using System.Collections.Concurrent;
using System.Globalization;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading.Channels;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.Infrastructure.Ipc;

/// <summary>
/// The UI's end of channel C (<c>07_IPC</c> §Client behavior): connect, correlate acks by id, hand events to a
/// reader. <c>PipeOptions.CurrentUserOnly</c> makes the client refuse a server that is not owned by its own user,
/// the mirror of the server's check on it.
/// </summary>
/// <remarks>
/// Backoff and "start the Agent, retry" are the App's (P3 PR-2); this class connects once and reports.
/// </remarks>
public sealed class PipeClient : IAsyncDisposable
{
    private readonly NamedPipeClientStream _pipe;
    private readonly Channel<IpcEnvelope> _events = Channel.CreateUnbounded<IpcEnvelope>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
    private readonly ConcurrentDictionary<string, TaskCompletionSource<IpcEnvelope>> _pending = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private Task? _reader;
    private long _nextId;
    private int _disposed;

    public PipeClient(string pipeName = IpcProtocol.PipeName)
    {
        ArgumentException.ThrowIfNullOrEmpty(pipeName);
        _pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
    }

    /// <summary>Everything the Agent sent without an id, in order. Completes when the pipe closes.</summary>
    public ChannelReader<IpcEnvelope> Events => _events.Reader;

    public bool IsConnected => _pipe.IsConnected;

    /// <summary>Connect once and start reading; the App's backoff and "start the Agent" live above this.</summary>
    /// <exception cref="TimeoutException">No server, or both instances busy, within <paramref name="timeout"/>.</exception>
    public async Task ConnectAsync(TimeSpan timeout, CancellationToken ct = default)
    {
        await _pipe.ConnectAsync((int)timeout.TotalMilliseconds, ct).ConfigureAwait(false);
        _reader = ReadLoopAsync(_lifetime.Token);
    }

    public Task<HelloAck> HelloAsync(string appVersion, TimeSpan timeout, CancellationToken ct = default) =>
        RequestAsync<HelloRequest, HelloAck>(IpcMessageType.Hello, new HelloRequest(appVersion, IpcProtocol.Version), IpcMessageType.HelloAck, timeout, ct);

    public Task<StatusAck> GetStatusAsync(TimeSpan timeout, CancellationToken ct = default) =>
        RequestAsync<GetStatusRequest, StatusAck>(IpcMessageType.GetStatus, new GetStatusRequest(), IpcMessageType.StatusAck, timeout, ct);

    public Task<PongAck> PingAsync(TimeSpan timeout, CancellationToken ct = default) =>
        RequestAsync<PingRequest, PongAck>(IpcMessageType.Ping, new PingRequest(), IpcMessageType.Pong, timeout, ct);

    /// <summary>One request, one ack of <paramref name="ackType"/>.</summary>
    /// <exception cref="IpcRequestException">The Agent answered <c>Error</c>, or with another type.</exception>
    /// <exception cref="TimeoutException">No ack within <paramref name="timeout"/>.</exception>
    public async Task<TAck> RequestAsync<TRequest, TAck>(string type, TRequest payload, string ackType, TimeSpan timeout, CancellationToken ct = default)
        where TRequest : class
        where TAck : class
    {
        string id = Interlocked.Increment(ref _nextId).ToString(CultureInfo.InvariantCulture);
        var tcs = new TaskCompletionSource<IpcEnvelope>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;
        try
        {
            await SendAsync(IpcCodec.Encode(type, id, payload), ct).ConfigureAwait(false);
            IpcEnvelope ack = await tcs.Task.WaitAsync(timeout, ct).ConfigureAwait(false);
            if (string.Equals(ack.Type, IpcMessageType.Error, StringComparison.Ordinal))
            {
                ErrorAck? error = IpcCodec.Payload<ErrorAck>(ack);
                throw new IpcRequestException(error?.Code ?? IpcMessageType.Error, error?.Message ?? "the Agent answered Error with no payload");
            }

            if (!string.Equals(ack.Type, ackType, StringComparison.Ordinal))
            {
                throw new IpcRequestException("UnexpectedAck", $"expected {ackType}, the Agent answered {ack.Type}");
            }

            return IpcCodec.Payload<TAck>(ack) ?? throw new IpcRequestException("EmptyAck", $"{ack.Type} carried no payload");
        }
        finally
        {
            _pending.TryRemove(id, out _);
        }
    }

    /// <summary>Raw send, for a test that wants to put a malformed frame on the wire.</summary>
    public async Task SendAsync(ReadOnlyMemory<byte> body, CancellationToken ct = default)
    {
        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await IpcFraming.WriteAsync(_pipe, body, ct).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        await _lifetime.CancelAsync().ConfigureAwait(false);
        await _pipe.DisposeAsync().ConfigureAwait(false);
        // A local, not the field: the reader was started by this instance and ends with the pipe it reads,
        // so joining it here cannot deadlock; the analyzer cannot see that through a field (VSTHRD003).
        Task? reader = _reader;
        if (reader is not null)
        {
            try
            {
                await reader.ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is OperationCanceledException or IOException or ObjectDisposedException)
            {
            }
        }

        _lifetime.Dispose();
        _writeLock.Dispose();
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        Exception? ended = null;
        try
        {
            while (true)
            {
                byte[]? frame = await IpcFraming.ReadAsync(_pipe, ct).ConfigureAwait(false);
                if (frame is null)
                {
                    break;
                }

                IpcEnvelope envelope;
                try
                {
                    envelope = IpcCodec.Decode(frame);
                }
                catch (JsonException)
                {
                    continue;
                }

                if (envelope.Id is { } id && _pending.TryRemove(id, out TaskCompletionSource<IpcEnvelope>? waiter))
                {
                    waiter.TrySetResult(envelope);
                }
                else
                {
                    _events.Writer.TryWrite(envelope);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is IOException or IpcFramingException or ObjectDisposedException)
        {
            ended = ex;
        }
        finally
        {
            _events.Writer.TryComplete();
            foreach (TaskCompletionSource<IpcEnvelope> waiter in _pending.Values)
            {
                waiter.TrySetException(ended ?? new IOException("the pipe closed before the Agent answered"));
            }
        }
    }
}
