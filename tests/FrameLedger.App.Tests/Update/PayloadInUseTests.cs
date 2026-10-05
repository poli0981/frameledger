// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.IO;
using FluentAssertions;
using FrameLedger.App.Update;

namespace FrameLedger.App.Tests.Update;

/// <summary>
/// beta.14: an update waits while a game still holds a library FrameLedger loads into games. A file another process keeps
/// open stands in for a mapped image here — both refuse the exclusive write the check asks for.
/// </summary>
public sealed class PayloadInUseTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fl-payload-" + Guid.NewGuid().ToString("N"));

    public PayloadInUseTests()
    {
        Directory.CreateDirectory(_dir);
        foreach (string name in PayloadInUse.Files)
        {
            File.WriteAllBytes(Path.Combine(_dir, name), [0x4D, 0x5A]);
        }
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void NothingHoldsThePayloadAfterTheGamesHaveExited() => PayloadInUse.Find(_dir).Should().BeNull();

    [Fact]
    public void AnOverlayAGameStillHoldsIsNamed()
    {
        using var game = new FileStream(Path.Combine(_dir, "FrameLedger.Overlay.dll"), FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);

        PayloadInUse.Find(_dir).Should().Be("FrameLedger.Overlay.dll");
    }

    [Fact]
    public void AFolderWithoutThePayloadHoldsNothing() => PayloadInUse.Find(Path.GetTempPath()).Should().BeNull("a library that is not there is not in use");
}
