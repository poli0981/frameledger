// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;

namespace FrameLedger.Domain.Tests.Canary;

/// <summary>C# 15 canary: a union and its exhaustive switch.</summary>
public sealed class UnionCanary
{
    private sealed record Cat(string Name);

    private sealed record Dog(string Name);

#pragma warning disable CA1815 // the canary asks what the tools read, not whether a union should carry its own equality
    private union Pet(Cat, Dog);
#pragma warning restore CA1815

    private static string NameOf(Pet pet) => pet switch
    {
        Cat cat => cat.Name,
        Dog dog => dog.Name,
    };

    [Fact]
    public void ACaseConvertsToTheUnionAndTheSwitchIsExhaustive()
    {
        Pet pet = new Dog("Rex");

        NameOf(pet).Should().Be("Rex");
        NameOf(new Cat("Tom")).Should().Be("Tom");
    }
}
