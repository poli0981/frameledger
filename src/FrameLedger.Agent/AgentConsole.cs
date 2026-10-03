// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Agent;

/// <summary>
/// The console verbs' two streams. Operator-facing English, like the capture host's: no <c>.resx</c>
/// exists yet, and these lines are read by the person who typed the verb, never by the App.
/// </summary>
internal static class AgentConsole
{
    public static void Line(string text) => Console.Out.WriteLine(text);

    public static void Problem(string text) => Console.Error.WriteLine(text);
}
