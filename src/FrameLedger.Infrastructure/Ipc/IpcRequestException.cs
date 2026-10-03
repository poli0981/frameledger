// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

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
