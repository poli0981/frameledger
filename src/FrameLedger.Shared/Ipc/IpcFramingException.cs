// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Shared.Ipc;

/// <summary>A frame that cannot be read: over the cap, or a stream that ended inside one.</summary>
public sealed class IpcFramingException : Exception
{
    public IpcFramingException()
    {
    }

    public IpcFramingException(string message)
        : base(message)
    {
    }

    public IpcFramingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
