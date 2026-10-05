// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Application.Capture;
using FrameLedger.Domain.Sessions;

namespace FrameLedger.Application.Recording;

/// <summary>
/// <c>04_CAPTURE</c> §Crash &amp; exit classification, as one function of what the session saw:
/// how it ended, the process's exit code, and whether the event log names the executable.
/// </summary>
/// <remarks>
/// <para>
/// The crash-witness window is <c>[start, end + 30 s]</c>; the caller asks the
/// <see cref="ICrashEventSource"/> with exactly that and hands the answer in. The precedence is
/// the document's: our own safety stop is <c>unhooked_safety</c> whatever the process did next; the
/// capture side stopping on its own is <c>degraded</c>; an exception's exit code (<see cref="IsExceptionCode"/>), an
/// event, or — since beta.14 — an engine's crash reporter the game started before it left is <c>crashed</c> — since
/// beta.8; any non-zero code was until then, End task's 1 included — and anything else,
/// including a target still running when the session ends, is <c>normal</c> with its code in the notes.
/// </para>
/// <para>
/// <c>interrupted</c> is never produced here: it is the recovery path's, for a session whose Agent
/// died (<c>PartialRecovery</c>).
/// </para>
/// </remarks>
public static class ExitStatusMapper
{
    /// <summary>How long after the session's end an event may still be attributed to it.</summary>
    public static readonly TimeSpan CrashWitnessGrace = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The status from the end, the exit code and the witnesses. <c>crashReporterCounted</c> (beta.14): the game started an
    /// engine's crash reporter and left within a minute (<see cref="CrashReporterRule"/>) — Unreal's crash ends with exit
    /// code 3 and no event, so this is its only witness.
    /// </summary>
    public static ExitStatus Map(SessionEndReason reason, int? exitCode, bool crashEventFound, bool crashReporterCounted = false)
    {
        switch (reason)
        {
            case SessionEndReason.SafetyUnhook:
                return ExitStatus.UnhookedSafety;
            case SessionEndReason.WriterSelfDisabled:
            case SessionEndReason.WriterStoppedBlocklisted:
            case SessionEndReason.SupervisionLost:
            case SessionEndReason.WriterNeverInstalledHooks:
                return ExitStatus.Degraded;
            default:
                // A crash is an exception's exit code or the Application log's witness (beta.8). Any other non-zero code is
                // the program's own exit — End task and taskkill leave 1 — and was a "crash" until 2026-09-25; the code stays
                // in the notes, and the crash policy still counts it inside its window (CrashAutoDisablePolicy).
                return crashEventFound || crashReporterCounted || exitCode is { } code && IsExceptionCode(code) ? ExitStatus.Crashed : ExitStatus.Normal;
        }
    }

    /// <summary>
    /// Whether an exit code is an unhandled exception's rather than a program's own: an NTSTATUS error in
    /// <c>0xC0000000–0xC0FFFFFF</c> (access violation, stack overflow, fail-fast, heap corruption, an in-page I/O error…)
    /// other than <c>0xC000013A</c> (closed from the console), an unhandled C++ (<c>0xE06D7363</c>) or .NET
    /// (<c>0xE0434352</c>) exception, or a breakpoint (<c>0x80000003</c>). <c>-1</c> is a program's own error exit, not one.
    /// </summary>
    public static bool IsExceptionCode(int code)
    {
        uint u = unchecked((uint)code);
        return u switch
        {
            0xC000013A => false,
            >= 0xC0000000 and <= 0xC0FFFFFF => true,
            0xE06D7363 or 0xE0434352 or 0x80000003 => true,
            _ => false,
        };
    }

    /// <summary>
    /// What <c>sessions.capture_notes</c> carries about the end: the fine reason the four statuses
    /// collapse, the exit code when there was one, the witness when there was one — and since beta.14 the crash reporter the
    /// game started, if one was seen: <c>crash_reporter=</c> when it counted, <c>crash_reporter_seen=</c> when it did not.
    /// </summary>
    public static string Describe(SessionEndReason reason, int? exitCode, bool crashEventFound, string? crashReporter = null, bool crashReporterCounted = false)
    {
        string note = "end=" + reason;
        if (exitCode is { } code)
        {
            note += "; exit_code=" + code.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (crashEventFound)
        {
            note += "; crash_event=application_log";
        }

        if (crashReporter is { Length: > 0 })
        {
            note += (crashReporterCounted ? "; crash_reporter=" : "; crash_reporter_seen=") + crashReporter;
        }

        return note;
    }
}
