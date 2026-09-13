using System.Collections.Concurrent;
using System.ComponentModel;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.Json;
using System.Threading.Channels;
using FrameLedger.Application.Ipc;
using FrameLedger.Shared.Ipc;
using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security;
using Windows.Win32.Storage.FileSystem;
using Windows.Win32.System.Pipes;

namespace FrameLedger.Infrastructure.Ipc;

/// <summary>
/// The Agent's end of channel C (<c>07_IPC</c> §C, HANDOFF §P3 decision D11): a message-mode named pipe,
/// <c>PIPE_REJECT_REMOTE_CLIENTS</c>, at most <see cref="PipeServerOptions.MaxClients"/> instances, created
/// through <c>CreateNamedPipe</c> with the SDDL <see cref="PipeAccessControl.Sddl"/> states — so what the
/// kernel enforces is the string this class prints, not a library's default.
/// </summary>
/// <remarks>
/// <para>
/// <b>One listening instance at a time, one slot per client.</b> A slot is taken before an instance is created
/// and released when its client is gone; with every slot taken the accept loop waits rather than creating a
/// third instance, which is how "max 2 clients" is a property of the pipe and not of a counter.
/// </para>
/// <para>
/// <b>Identity on the first frame.</b> A client's token user is read by impersonation, which the kernel refuses
/// before the client has written — so the connect is accepted, the first frame is read, the user is compared to
/// this process's, and a stranger is disconnected without an answer.
/// </para>
/// <para>
/// <b>Publish never waits.</b> Each client owns a bounded outbound queue that drops its oldest frame when full;
/// the session loop hands over a frame and returns (<see cref="IIpcEventPublisher"/>'s contract).
/// </para>
/// </remarks>
public sealed class PipeServer : IIpcEventPublisher, IDisposable
{
    private const uint _bufferBytes = 64 * 1024;

    private readonly PipeServerOptions _options;
    private readonly IIpcRequestHandler _handler;
    private readonly Action<string> _log;
    private readonly Func<NamedPipeServerStream, SecurityIdentifier?> _clientUserOf;
    private readonly SecurityIdentifier _owner;
    private readonly byte[] _descriptor;
    private readonly string _fullName;
    private readonly SemaphoreSlim _slots;
    private readonly ConcurrentDictionary<long, Client> _clients = new();
    private long _nextClientId;
    private long _rejected;
    private long _dropped;

    public PipeServer(PipeServerOptions options, IIpcRequestHandler handler, Action<string>? log = null,
        Func<NamedPipeServerStream, SecurityIdentifier?>? clientUserOf = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxClients, 1, nameof(options));
        ArgumentOutOfRangeException.ThrowIfLessThan(options.OutboundQueueCapacity, 1, nameof(options));
        _log = log ?? (static _ => { });
        _clientUserOf = clientUserOf ?? PipeAccessControl.ClientUserOf;
        _owner = PipeAccessControl.CurrentUser();
        SecurityDescriptor = PipeAccessControl.Sddl(_owner);
        _descriptor = PipeAccessControl.DescriptorBytes(SecurityDescriptor);
        _fullName = @"\\.\pipe\" + options.PipeName;
        _slots = new SemaphoreSlim(options.MaxClients, options.MaxClients);
    }

    /// <summary>The SDDL every instance is created with.</summary>
    public string SecurityDescriptor { get; }

    public string PipeName => _options.PipeName;

    public int ConnectedClients => _clients.Count;

    public bool HasClients => !_clients.IsEmpty;

    /// <summary>Connections closed because the client's token user was not this process's.</summary>
    public long RejectedClients => Interlocked.Read(ref _rejected);

    /// <summary>Event frames dropped because a client's outbound queue was full.</summary>
    public long DroppedFrames => Interlocked.Read(ref _dropped);

    /// <summary>Accept until cancelled; every client's tasks end with the token too.</summary>
    public async Task RunAsync(CancellationToken ct)
    {
        while (true)
        {
            await _slots.WaitAsync(ct).ConfigureAwait(false);
            NamedPipeServerStream? stream = null;
            try
            {
                stream = CreateInstance();
                await stream.WaitForConnectionAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                await DisposeQuietlyAsync(stream).ConfigureAwait(false);
                _slots.Release();
                throw;
            }
            catch (Exception ex) when (ex is IOException or Win32Exception)
            {
                _log($"pipe: accept failed: {ex.Message}");
                await DisposeQuietlyAsync(stream).ConfigureAwait(false);
                _slots.Release();
                await Task.Delay(250, ct).ConfigureAwait(false);
                continue;
            }

            long id = Interlocked.Increment(ref _nextClientId);
            var client = new Client(this, id, stream);
            _clients[id] = client;
            client.Start(ct);
        }
    }

    public void Publish<T>(string type, T payload)
        where T : class
    {
        if (_clients.IsEmpty)
        {
            return;
        }

        byte[] frame = IpcCodec.Encode(type, null, payload);
        foreach (Client client in _clients.Values)
        {
            client.Enqueue(frame);
        }
    }

    public void Dispose()
    {
        foreach (Client client in _clients.Values)
        {
            client.Close();
        }

        _slots.Dispose();
    }

    private static async ValueTask DisposeQuietlyAsync(NamedPipeServerStream? stream)
    {
        if (stream is not null)
        {
            await stream.DisposeAsync().ConfigureAwait(false);
        }
    }

    // CA2000 cannot see that NamedPipeServerStream takes ownership of the handle (ownsHandle: true, and the
    // stream's Dispose closes it); the catch below disposes it on the one path the stream never exists.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
        Justification = "the SafePipeHandle is owned by the returned stream, or disposed in the catch")]
    private unsafe NamedPipeServerStream CreateInstance()
    {
        fixed (char* name = _fullName)
        fixed (byte* descriptor = _descriptor)
        {
            var attributes = new SECURITY_ATTRIBUTES
            {
                nLength = (uint)sizeof(SECURITY_ATTRIBUTES),
                lpSecurityDescriptor = descriptor,
                bInheritHandle = false,
            };
            HANDLE handle = PInvoke.CreateNamedPipe(
                new PCWSTR(name),
                FILE_FLAGS_AND_ATTRIBUTES.PIPE_ACCESS_DUPLEX | FILE_FLAGS_AND_ATTRIBUTES.FILE_FLAG_OVERLAPPED,
                NAMED_PIPE_MODE.PIPE_TYPE_MESSAGE | NAMED_PIPE_MODE.PIPE_READMODE_MESSAGE | NAMED_PIPE_MODE.PIPE_WAIT
                    | NAMED_PIPE_MODE.PIPE_REJECT_REMOTE_CLIENTS,
                (uint)_options.MaxClients,
                _bufferBytes,
                _bufferBytes,
                0,
                &attributes);
            if (handle.IsNull || (nint)handle.Value == -1)
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError(), $"CreateNamedPipe({_fullName})");
            }

            var safe = new SafePipeHandle((nint)handle.Value, ownsHandle: true);
            try
            {
                return new NamedPipeServerStream(PipeDirection.InOut, isAsync: true, isConnected: false, safe);
            }
            catch
            {
                safe.Dispose();
                throw;
            }
        }
    }

    /// <summary>False — and counted — when the client is not this process's user. Called after the first frame.</summary>
    private bool AcceptIdentity(long id, NamedPipeServerStream stream)
    {
        SecurityIdentifier? user;
        try
        {
            user = _clientUserOf(stream);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or Win32Exception or UnauthorizedAccessException)
        {
            _log($"pipe: client {id}: identity could not be read ({ex.GetType().Name}); refused");
            user = null;
        }

        if (user is not null && user.Equals(_owner))
        {
            return true;
        }

        Interlocked.Increment(ref _rejected);
        _log($"pipe: client {id}: token user {user?.Value ?? "unknown"} is not {_owner.Value}; refused");
        return false;
    }

    private byte[] Answer(byte[] frame)
    {
        IpcEnvelope request;
        try
        {
            request = IpcCodec.Decode(frame);
        }
        catch (JsonException ex)
        {
            return IpcCodec.Encode(IpcMessageType.Error, null, new ErrorAck(IpcErrorCode.Malformed, ex.Message));
        }

        try
        {
            return _handler.Handle(request);
        }
        catch (JsonException ex)
        {
            return IpcCodec.Encode(IpcMessageType.Error, request.Id, new ErrorAck(IpcErrorCode.Malformed, ex.Message));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log($"pipe: handler faulted on {request.Type}: {ex.GetType().Name}: {ex.Message}");
            return IpcCodec.Encode(IpcMessageType.Error, request.Id, new ErrorAck(IpcErrorCode.HandlerFaulted, ex.GetType().Name));
        }
    }

    private void Remove(long id)
    {
        if (_clients.TryRemove(id, out _))
        {
            _slots.Release();
        }
    }

    /// <summary>One connection: a reader that answers requests, a writer that drains the outbound queue.</summary>
    private sealed class Client : IDisposable
    {
        private readonly PipeServer _owner;
        private readonly long _id;
        private readonly NamedPipeServerStream _stream;
        private readonly Channel<byte[]> _outbound;
        private readonly CancellationTokenSource _lifetime = new();
        private Task _run = Task.CompletedTask;

        public Client(PipeServer owner, long id, NamedPipeServerStream stream)
        {
            _owner = owner;
            _id = id;
            _stream = stream;
            _outbound = Channel.CreateBounded<byte[]>(
                new BoundedChannelOptions(owner._options.OutboundQueueCapacity) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true },
                _ => Interlocked.Increment(ref owner._dropped));
        }

        public void Start(CancellationToken ct) => _run = RunAsync(ct);

        public void Enqueue(byte[] frame) => _outbound.Writer.TryWrite(frame);

        /// <summary>Idempotent and safe after the client's own end: a closed client is simply closed.</summary>
        public void Close()
        {
            try
            {
                _lifetime.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        public void Dispose() => _lifetime.Dispose();

        private async Task RunAsync(CancellationToken ct)
        {
            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetime.Token);
            Task writer = WriteLoopAsync(linked.Token);
            try
            {
                await ReadLoopAsync(linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex) when (ex is IOException or IpcFramingException or ObjectDisposedException)
            {
                _owner._log($"pipe: client {_id}: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                _outbound.Writer.TryComplete();
                await linked.CancelAsync().ConfigureAwait(false);
                try
                {
                    await writer.ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is OperationCanceledException or IOException or ObjectDisposedException)
                {
                }

                await _stream.DisposeAsync().ConfigureAwait(false);
                // Out of the table BEFORE the token source goes, so a concurrent Dispose() of the server never
                // reaches a client whose source is already disposed.
                _owner.Remove(_id);
                Dispose();
            }
        }

        private async Task ReadLoopAsync(CancellationToken ct)
        {
            bool identified = false;
            while (true)
            {
                byte[]? frame = await IpcFraming.ReadAsync(_stream, ct).ConfigureAwait(false);
                if (frame is null)
                {
                    return;
                }

                if (!identified)
                {
                    if (!_owner.AcceptIdentity(_id, _stream))
                    {
                        return;
                    }

                    identified = true;
                }

                Enqueue(_owner.Answer(frame));
            }
        }

        private async Task WriteLoopAsync(CancellationToken ct)
        {
            await foreach (byte[] frame in _outbound.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                await IpcFraming.WriteAsync(_stream, frame, ct).ConfigureAwait(false);
            }
        }
    }
}
