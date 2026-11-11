// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.Infrastructure.Telemetry;
using LibreHardwareMonitor.Hardware;

namespace FrameLedger.Infrastructure.Tests.Telemetry;

/// <summary>
/// The two rules of <see cref="LhmLibrary"/> (2026-11-12): opens and closes never run at once, and a <c>Computer</c>
/// is not really closed while another is in use. The fault they exist for - one instance's close taking the
/// library's per-process state from under another - needs an elevated Agent with PawnIO and was met on the owner's
/// PC; here the rules are held on scripted instances, which is what can be held without a driver.
/// </summary>
public sealed class LhmLibraryTests
{
    private const LhmGroups _session = LhmGroups.Gpu;
    private const LhmGroups _advanced = LhmGroups.Gpu | LhmGroups.Cpu | LhmGroups.Memory | LhmGroups.Motherboard;

    /// <summary>A library whose instances are scripted, each remembered with the groups it was made for.</summary>
    private sealed class Script
    {
        public List<(LhmGroups Groups, FakeLhmComputer Computer)> Made { get; } = [];

        public bool NextOpenThrows { get; set; }

        public bool EveryCloseThrows { get; set; }

        public LhmLibrary Library => field ??= new LhmLibrary(groups =>
        {
            var computer = new FakeLhmComputer { OpenThrows = NextOpenThrows, CloseThrows = EveryCloseThrows };
            NextOpenThrows = false;
            Made.Add((groups, computer));
            return computer;
        });
    }

    [Fact]
    public void OneUserOpensItsComputerAndClosingItClosesIt()
    {
        var script = new Script();
        var only = new LhmComputerAdapter(_session, script.Library);

        only.Hardware.Should().BeEmpty("nothing is open yet");
        only.Open();
        only.Open();

        script.Made.Should().ContainSingle("a second Open while open changes nothing").Which.Computer.Opens.Should().Be(1);
        script.Library.Users.Should().Be(1);

        only.Close();
        only.Close();

        script.Made[0].Computer.Closes.Should().Be(1, "and it lets go once");
        script.Library.Users.Should().Be(0);
        script.Library.Parked.Should().Be(0);
    }

    [Fact]
    public void AComputerIsNotClosedWhileAnotherIsInUse()
    {
        // THE DEFECT. A session ends and closes its source while the Advanced reader - held by the page - reads on.
        // The session's Computer must stay open: closing it lets go of what the reader's processor device reads through.
        var script = new Script();
        var reader = new LhmComputerAdapter(_advanced, script.Library);
        var session = new LhmComputerAdapter(_session, script.Library);
        reader.Open();
        session.Open();

        session.Close();

        script.Made.Should().HaveCount(2);
        script.Made.Should().OnlyContain(static m => m.Computer.Closes == 0, "nothing is closed while the reader is open");
        script.Library.Users.Should().Be(1);
        script.Library.Parked.Should().Be(1);
        session.Hardware.Should().BeEmpty("the session's user has let go, whatever the library keeps");

        // The last user closes the library: its own Computer and the parked one, then there is nothing left.
        reader.Close();

        script.Made.Should().OnlyContain(static m => m.Computer.Closes == 1);
        script.Library.Users.Should().Be(0);
        script.Library.Parked.Should().Be(0);
    }

    [Fact]
    public void TheNextSessionTakesTheParkedComputerOfItsGroupsSoNothingPilesUp()
    {
        // The page stays open all evening and ten sessions come and go: one parked Computer, handed on, not ten.
        var script = new Script();
        var reader = new LhmComputerAdapter(_advanced, script.Library);
        reader.Open();

        for (int n = 0; n < 10; n++)
        {
            var session = new LhmComputerAdapter(_session, script.Library);
            session.Open();
            session.Close();
        }

        script.Made.Should().HaveCount(2, "the reader's, and one for every session after the first found it parked");
        script.Made[1].Computer.Opens.Should().Be(1, "a parked Computer is still open: it is handed over, not opened again");
        script.Library.Parked.Should().Be(1);

        // Another set of groups is another Computer: a parked GPU-only one cannot stand in for the processor's.
        var temperature = new LhmComputerAdapter(LhmGroups.Cpu | LhmGroups.Memory, script.Library);
        temperature.Open();
        script.Made.Should().HaveCount(3);
        script.Made[2].Groups.Should().Be(LhmGroups.Cpu | LhmGroups.Memory);

        temperature.Close();
        reader.Close();
        script.Made.Should().OnlyContain(static m => m.Computer.Closes == 1);
    }

    [Fact]
    public void AnOpenThatThrowsCountsNoUserAndLeavesNothingBehind()
    {
        var script = new Script { NextOpenThrows = true };
        var failing = new LhmComputerAdapter(_advanced, script.Library);

        failing.Invoking(static a => a.Open()).Should().Throw<InvalidOperationException>();

        script.Library.Users.Should().Be(0);
        script.Library.Parked.Should().Be(0);
        failing.Close();
        script.Made[0].Computer.Closes.Should().Be(0, "what never opened is not closed");

        // And the next user is the first: its close is the last close.
        var next = new LhmComputerAdapter(_session, script.Library);
        next.Open();
        next.Close();
        script.Made[1].Computer.Closes.Should().Be(1);
    }

    [Fact]
    public void TheLastCloseClosesEveryComputerEvenWhenOneThrowsAndThenSaysSo()
    {
        var script = new Script { EveryCloseThrows = true };
        var reader = new LhmComputerAdapter(_advanced, script.Library);
        var session = new LhmComputerAdapter(_session, script.Library);
        reader.Open();
        session.Open();
        session.Close();

        reader.Invoking(static a => a.Close()).Should().Throw<InvalidOperationException>("the caller is told, as it was before");

        script.Made.Should().OnlyContain(static m => m.Computer.Closes == 1, "a throw from one does not leave the other open");
        script.Library.Users.Should().Be(0);
        script.Library.Parked.Should().Be(0, "and the library starts afresh for the next user");
    }

    [Fact]
    public async Task OpensAndClosesFromManyThreadsNeverRunAtOnce()
    {
        // Rule 1. Each scripted instance says so if it is entered while another Open or Close is inside.
        int inside = 0;
        int overlaps = 0;
        var library = new LhmLibrary(_ => new Probe(() =>
        {
            if (Interlocked.Increment(ref inside) != 1)
            {
                Interlocked.Increment(ref overlaps);
            }

            Thread.SpinWait(200);
            Interlocked.Decrement(ref inside);
        }));

        Task[] users = [.. Enumerable.Range(0, 8).Select(n => Task.Run(() =>
        {
            LhmGroups groups = (n % 2 == 0) ? _session : _advanced;
            for (int i = 0; i < 200; i++)
            {
                var user = new LhmComputerAdapter(groups, library);
                user.Open();
                user.Close();
            }
        }, TestContext.Current.CancellationToken))];
        await Task.WhenAll(users);

        overlaps.Should().Be(0);
        library.Users.Should().Be(0);
        library.Parked.Should().Be(0);
    }

    /// <summary>An instance that runs <paramref name="entered"/> inside every Open and Close.</summary>
    private sealed class Probe(Action entered) : ILhmComputer
    {
        public IReadOnlyList<IHardware> Hardware => [];

        public void Open() => entered();

        public void Update()
        {
        }

        public void Close() => entered();
    }
}
