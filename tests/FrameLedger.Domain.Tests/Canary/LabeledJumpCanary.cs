// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;

namespace FrameLedger.Domain.Tests.Canary;

/// <summary>C# 15 canary: a labeled continue.</summary>
public sealed class LabeledJumpCanary
{
    [Fact]
    public void TheOuterLoopContinuesFromTheInnerOne()
    {
        int[][] rows = [[1, 2], [3, -1, 9], [5]];
        int clean = 0;

    outer: foreach (int[] row in rows)
        {
            foreach (int cell in row)
            {
                if (cell < 0)
                {
                    continue outer;
                }
            }

            clean++;
        }

        clean.Should().Be(2);
    }
}
