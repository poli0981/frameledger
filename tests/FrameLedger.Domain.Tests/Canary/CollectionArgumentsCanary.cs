// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;

namespace FrameLedger.Domain.Tests.Canary;

/// <summary>C# 15 canary: collection expression arguments.</summary>
public sealed class CollectionArgumentsCanary
{
    [Fact]
    public void ASetBuiltWithAComparerAndAListWithACapacity()
    {
        HashSet<string> set = [with(StringComparer.OrdinalIgnoreCase), "a", "A"];
        List<int> list = [with(capacity: 4), 1, 2];

        set.Should().HaveCount(1);
        list.Capacity.Should().Be(4);
    }
}
