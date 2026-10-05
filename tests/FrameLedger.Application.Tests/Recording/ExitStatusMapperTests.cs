// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.Application.Capture;
using FrameLedger.Application.Recording;
using FrameLedger.Domain.Sessions;

namespace FrameLedger.Application.Tests.Recording;

/// <summary>
/// <c>04_CAPTURE</c> §Crash &amp; exit classification, case by case. Since beta.8 an exit code is a crash only when it is an
/// exception's — End task's 1, a program's -1 and Ctrl+C's 0xC000013A are ordinary ends with their code in the notes.
/// </summary>
public sealed class ExitStatusMapperTests
{
    [Theory]
    [InlineData(SessionEndReason.TargetExited, 0, false, ExitStatus.Normal)]
    [InlineData(SessionEndReason.TargetExited, -1073741819, false, ExitStatus.Crashed)]
    [InlineData(SessionEndReason.TargetExited, 0, true, ExitStatus.Crashed)]
    [InlineData(SessionEndReason.TargetExited, 1, false, ExitStatus.Normal)]
    [InlineData(SessionEndReason.TargetExited, -1, false, ExitStatus.Normal)]
    [InlineData(SessionEndReason.TargetExited, unchecked((int)0xC000013A), false, ExitStatus.Normal)]
    [InlineData(SessionEndReason.TargetExited, unchecked((int)0xC0000409), false, ExitStatus.Crashed)]
    [InlineData(SessionEndReason.TargetExited, unchecked((int)0xE06D7363), false, ExitStatus.Crashed)]
    [InlineData(SessionEndReason.TargetExited, unchecked((int)0xE0434352), false, ExitStatus.Crashed)]
    [InlineData(SessionEndReason.TargetExited, unchecked((int)0x80000003), false, ExitStatus.Crashed)]
    [InlineData(SessionEndReason.TargetExited, 1, true, ExitStatus.Crashed)]
    [InlineData(SessionEndReason.TargetExited, null, true, ExitStatus.Crashed)]
    [InlineData(SessionEndReason.Running, null, false, ExitStatus.Normal)]
    [InlineData(SessionEndReason.SupervisionFaulted, null, false, ExitStatus.Normal)]
    [InlineData(SessionEndReason.SafetyUnhook, 0, false, ExitStatus.UnhookedSafety)]
    [InlineData(SessionEndReason.SafetyUnhook, 1, true, ExitStatus.UnhookedSafety)]
    [InlineData(SessionEndReason.SupervisionLost, null, false, ExitStatus.Degraded)]
    [InlineData(SessionEndReason.WriterSelfDisabled, null, false, ExitStatus.Degraded)]
    [InlineData(SessionEndReason.WriterStoppedBlocklisted, null, false, ExitStatus.Degraded)]
    [InlineData(SessionEndReason.WriterNeverInstalledHooks, null, false, ExitStatus.Degraded)]
    [InlineData(SessionEndReason.RefusedByGuard, null, false, ExitStatus.Normal)]
    [InlineData(SessionEndReason.StoppedByUser, null, false, ExitStatus.Normal)]
    [InlineData(SessionEndReason.KillSwitchEngaged, null, false, ExitStatus.Normal)]
    public void MapsTheReasonTheExitCodeAndTheWitness(SessionEndReason reason, int? exitCode, bool witness, ExitStatus expected)
    {
        ExitStatusMapper.Map(reason, exitCode, witness).Should().Be(expected);
    }

    [Fact]
    public void TheNoteCarriesTheFineReasonTheCodeAndTheWitness()
    {
        ExitStatusMapper.Describe(SessionEndReason.TargetExited, -1073741819, true)
            .Should().Be("end=TargetExited; exit_code=-1073741819; crash_event=application_log");
        ExitStatusMapper.Describe(SessionEndReason.Running, null, false).Should().Be("end=Running");
        ExitStatusMapper.CrashWitnessGrace.Should().Be(TimeSpan.FromSeconds(30));
    }

    /// <summary>beta.14: the engine's crash reporter is the third witness — counted, crashed; only seen, a note.</summary>
    [Fact]
    public void ACountedCrashReporterIsACrashAndOneOnlySeenIsANote()
    {
        ExitStatusMapper.Map(SessionEndReason.TargetExited, 3, crashEventFound: false, crashReporterCounted: true).Should().Be(ExitStatus.Crashed);
        ExitStatusMapper.Map(SessionEndReason.TargetExited, 3, crashEventFound: false).Should().Be(ExitStatus.Normal, "exit code 3 alone is the program's own");
        ExitStatusMapper.Map(SessionEndReason.SafetyUnhook, 3, false, crashReporterCounted: true).Should().Be(ExitStatus.UnhookedSafety, "our own stop still outranks");
        ExitStatusMapper.Describe(SessionEndReason.TargetExited, 3, false, "CrashReportClient.exe", crashReporterCounted: true)
            .Should().Be("end=TargetExited; exit_code=3; crash_reporter=CrashReportClient.exe");
        ExitStatusMapper.Describe(SessionEndReason.TargetExited, 0, false, "CrashReporter.exe")
            .Should().Be("end=TargetExited; exit_code=0; crash_reporter_seen=CrashReporter.exe");
    }
}
