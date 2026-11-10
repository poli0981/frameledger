// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;

namespace FrameLedger.Domain.Tests.Canary;

/// <summary>C# 15 canary: a closed hierarchy and its exhaustive switch.</summary>
public sealed class ClosedHierarchyCanary
{
    private closed record State;

    private sealed record Idle : State;

    private sealed record Busy(int Percent) : State;

    private static string Describe(State state) => state switch
    {
        Idle => "idle",
        Busy(var percent) => $"{percent}%",
    };

    [Fact]
    public void EveryDescendantIsHandledWithoutADefaultArm()
    {
        Describe(new Idle()).Should().Be("idle");
        Describe(new Busy(40)).Should().Be("40%");
    }
}
