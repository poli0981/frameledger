// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.App.Services;

namespace FrameLedger.App.Tests;

/// <summary>A failed start says why and where the log is (<c>10_LOGGING</c> §Crash handling, 2026-09-15).</summary>
public sealed class StartupFailureDialogTests
{
    [Fact]
    public void TheTextNamesTheReasonAndTheLogFolder()
    {
        const string logs = @"C:\data\FrameLedger\logs";
        var failure = new InvalidOperationException("ledger schema 9 is newer than this build (4)");

        string body = StartupFailureDialog.Body(failure, logs);

        body.Should().Contain("ledger schema 9 is newer than this build (4)").And.Contain(logs);
        body.Should().NotContain("{0}").And.NotContain("{1}");
    }
}
