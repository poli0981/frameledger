namespace FrameLedger.Infrastructure.Ipc;

/// <summary>The Agent answered a request with <c>Error</c>, or with an ack of the wrong type.</summary>
public sealed class IpcRequestException : Exception
{
    public IpcRequestException()
    {
    }

    public IpcRequestException(string message)
        : base(message)
    {
    }

    public IpcRequestException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public IpcRequestException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    /// <summary>The <c>ErrorAck.code</c>, when there was one.</summary>
    public string? Code { get; }
}
