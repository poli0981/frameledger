// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.IO;

namespace FrameLedger.App.Services;

/// <summary>
/// beta.22: a request the capture agent never answered, told apart from one it refused and from one never sent. The
/// connection closed under it (<see cref="IOException"/>, <see cref="AgentConnection.AnsweredAsync"/>) or no answer came in
/// time (<see cref="TimeoutException"/>): whether it was carried out is not known, so a caller says so
/// (<c>Agent_Unanswered</c>) and reads what is stored again rather than assuming either way (<c>07_IPC</c> §Client behavior).
/// An <see cref="InvalidOperationException"/> from the request is the connection gone before it was sent: nothing was asked.
/// </summary>
public static class AgentAnswers
{
    public static bool IsUnanswered(Exception ex) => ex is IOException or TimeoutException;
}
