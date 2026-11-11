// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.Application.Advanced;
using FrameLedger.Infrastructure.Telemetry;

namespace FrameLedger.Infrastructure.Tests.Telemetry;

/// <summary>
/// The hub's holding and letting go (owner decision D107) with a reader that reads nothing: a run starts with the
/// first holder, goes on while anybody holds it, and ends — closing the sources — a while after the last let go.
/// </summary>
/// <remarks>
/// Real threads and short real intervals. Every wait below is for a CONDITION, with a ceiling far above what the
/// condition takes, so a busy machine makes a test slower and never red.
/// </remarks>
public sealed class AdvancedHardwareHubTests
{
    private static readonly TimeSpan _interval = TimeSpan.FromMilliseconds(5);
    private static readonly TimeSpan _linger = TimeSpan.FromMilliseconds(60);
    private static readonly TimeSpan _ceiling = TimeSpan.FromSeconds(10);

    private sealed class CountingReader : IAdvancedHardwareReader
    {
        private int _ticks;
        private int _opens;
        private int _disposes;

        public int Ticks => Volatile.Read(ref _ticks);

        public int Opens => Volatile.Read(ref _opens);

        public int Disposes => Volatile.Read(ref _disposes);

        public bool OpenThrows { get; init; }

        public bool TickThrows { get; set; }

        public AdvancedSessionTarget? Asked { get; private set; }

        public void Open()
        {
            Interlocked.Increment(ref _opens);
            if (OpenThrows)
            {
                throw new InvalidOperationException("no source opened");
            }
        }

        public AdvancedSnapshot Tick()
        {
            int tick = Interlocked.Increment(ref _ticks);
            if (TickThrows)
            {
                throw new InvalidOperationException("the tick failed");
            }

            return new AdvancedSnapshot(DateTimeOffset.UnixEpoch.AddSeconds(tick), [new AdvancedReading("adv.nvapi.pstate", tick)], [], [], AdvancedSources.Unknown);
        }

        public IReadOnlyList<AdvancedFact> ReadSessionFacts(AdvancedSessionTarget target)
        {
            Asked = target;
            return [new AdvancedFact("nvapi.session.vrr_enabled", Instance: null, AdvancedValue.Of(true))];
        }

        public void Dispose() => Interlocked.Increment(ref _disposes);
    }

    private static void Until(Func<bool> condition, string what)
        => SpinWait.SpinUntil(condition, _ceiling).Should().BeTrue(what);

    [Fact]
    public void NothingIsReadUntilSomebodyHoldsIt()
    {
        var readers = new List<CountingReader>();
        using var hub = new AdvancedHardwareHub(() => Add(readers, new CountingReader()), _interval, _linger);

        hub.IsRunning.Should().BeFalse();
        hub.Latest.Should().BeNull();
        hub.Holders.Should().Be(0);
        hub.ReadSessionFacts(new AdvancedSessionTarget(1, ExecutablePath: null, GdiDeviceName: null)).Should().BeEmpty();
        readers.Should().BeEmpty("no source was opened: a PC idling with FrameLedger in the tray reads nothing for this");
    }

    [Fact]
    public void TheFirstHolderStartsARunAndSnapshotsFollow()
    {
        var readers = new List<CountingReader>();
        using var hub = new AdvancedHardwareHub(() => Add(readers, new CountingReader()), _interval, _linger);

        using IDisposable hold = hub.Acquire();

        Until(() => hub.Latest is not null, "the first snapshot arrives");
        AdvancedSnapshot first = hub.Latest!;
        Until(() => hub.Latest is { } later && later.TakenAt > first.TakenAt, "a later tick replaces it");
        hub.IsRunning.Should().BeTrue();
        hub.Holders.Should().Be(1);
        Single(readers).Opens.Should().Be(1);
    }

    [Fact]
    public void TwoHoldersShareOneRunAndItGoesOnWhileEitherHolds()
    {
        var readers = new List<CountingReader>();
        using var hub = new AdvancedHardwareHub(() => Add(readers, new CountingReader()), _interval, _linger);
        IDisposable session = hub.Acquire();
        IDisposable page = hub.Acquire();
        Until(() => hub.Latest is not null, "the run reads");

        session.Dispose();
        session.Dispose();
        hub.Holders.Should().Be(1, "letting go twice lets go once");
        int ticks = Single(readers).Ticks;
        Until(() => Single(readers).Ticks > ticks + 20, "the run goes on, well past the linger, while the page holds it");

        hub.IsRunning.Should().BeTrue();
        Count(readers).Should().Be(1, "one reader for both");
        page.Dispose();
    }

    [Fact]
    public void AWhileAfterTheLastHolderLetsGoTheSourcesAreClosed()
    {
        var readers = new List<CountingReader>();
        using var hub = new AdvancedHardwareHub(() => Add(readers, new CountingReader()), _interval, _linger);
        IDisposable hold = hub.Acquire();
        Until(() => hub.Latest is not null, "the run reads");

        hold.Dispose();

        Until(() => !hub.IsRunning, "the run ends after the linger");
        Until(() => Single(readers).Disposes == 1, "and the reader is closed");
        hub.Latest.Should().BeNull("a snapshot of a reader that is closed is not a reading");
        hub.ReadSessionFacts(new AdvancedSessionTarget(1, ExecutablePath: null, GdiDeviceName: null)).Should().BeEmpty();
    }

    [Fact]
    public void AHolderThatComesBackWithinTheLingerKeepsTheSameRun()
    {
        var readers = new List<CountingReader>();
        using var hub = new AdvancedHardwareHub(() => Add(readers, new CountingReader()), _interval, TimeSpan.FromSeconds(30));
        IDisposable first = hub.Acquire();
        Until(() => hub.Latest is not null, "the run reads");

        first.Dispose();
        using IDisposable second = hub.Acquire();
        int ticks = Single(readers).Ticks;
        Until(() => Single(readers).Ticks > ticks + 5, "the same reader goes on");

        Count(readers).Should().Be(1, "a session ending as the page opens does not close and reopen the sensors");
        Single(readers).Disposes.Should().Be(0);
    }

    [Fact]
    public void AfterARunHasEndedTheNextHolderStartsANewOne()
    {
        var readers = new List<CountingReader>();
        using var hub = new AdvancedHardwareHub(() => Add(readers, new CountingReader()), _interval, _linger);
        hub.Acquire().Dispose();
        Until(() => !hub.IsRunning && Count(readers) == 1 && At(readers, 0).Disposes == 1, "the first run ends and closes");

        using IDisposable hold = hub.Acquire();

        Until(() => Count(readers) == 2 && hub.Latest is not null, "a second reader is made and reads");
        At(readers, 1).Disposes.Should().Be(0);
    }

    [Fact]
    public void ASessionsFactsAreAskedOfTheRunningReader()
    {
        var readers = new List<CountingReader>();
        using var hub = new AdvancedHardwareHub(() => Add(readers, new CountingReader()), _interval, _linger);
        using IDisposable hold = hub.Acquire();
        Until(() => hub.Latest is not null, "the run reads");
        var target = new AdvancedSessionTarget(4242, @"C:\Games\game.exe", @"\\.\DISPLAY1");

        IReadOnlyList<AdvancedFact> facts = hub.ReadSessionFacts(target);

        facts.Should().ContainSingle().Which.Id.Should().Be("nvapi.session.vrr_enabled");
        Single(readers).Asked.Should().BeSameAs(target);
        FluentActions.Invoking(() => hub.ReadSessionFacts(null!)).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ATickThatThrowsTwiceRunningEndsTheRunAndSaysSo()
    {
        var readers = new List<CountingReader>();
        var notes = new List<string>();
        using var hub = new AdvancedHardwareHub(() => Add(readers, new CountingReader { TickThrows = true }), _interval, _linger, n => Add(notes, n));

        using IDisposable hold = hub.Acquire();

        Until(() => !hub.IsRunning, "the run ends although it is still held: a loop that throws every tick is worse than no readings");
        Until(() => Single(readers).Disposes == 1, "and the reader is closed");
        Single(readers).Ticks.Should().Be(AdvancedHardwareHub.MaxTickFaults);
        Single(notes).Should().Contain("the tick failed");
        hub.Latest.Should().BeNull();
    }

    [Fact]
    public void AReaderThatDoesNotOpenEndsTheRunAndSaysSo()
    {
        var readers = new List<CountingReader>();
        var notes = new List<string>();
        using var hub = new AdvancedHardwareHub(() => Add(readers, new CountingReader { OpenThrows = true }), _interval, _linger, n => Add(notes, n));

        using IDisposable hold = hub.Acquire();

        Until(() => !hub.IsRunning && Count(readers) == 1 && Single(readers).Disposes == 1, "the run ends and the reader is closed");
        Single(readers).Ticks.Should().Be(0);
        Single(notes).Should().Contain("did not start").And.Contain("no source opened");
    }

    [Fact]
    public void DisposingTheHubEndsTheRunAtOnceAndNothingCanHoldItAfter()
    {
        var readers = new List<CountingReader>();
        var hub = new AdvancedHardwareHub(() => Add(readers, new CountingReader()), _interval, TimeSpan.FromMinutes(5));
        IDisposable hold = hub.Acquire();
        Until(() => hub.Latest is not null, "the run reads");

        hub.Dispose();
        hub.Dispose();

        Until(() => Single(readers).Disposes == 1, "the reader is closed without waiting for the linger");
        hub.IsRunning.Should().BeFalse();
        hold.Dispose();
        FluentActions.Invoking(hub.Acquire).Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void TheIntervalIsPositiveTheLingerIsNotNegativeAndTheMakerIsRequired()
    {
        FluentActions.Invoking(static () => new AdvancedHardwareHub(null!, _interval, _linger)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(static () => new AdvancedHardwareHub(static () => new CountingReader(), TimeSpan.Zero, _linger)).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(static () => new AdvancedHardwareHub(static () => new CountingReader(), _interval, TimeSpan.FromSeconds(-1))).Should().Throw<ArgumentOutOfRangeException>();
    }

    // The lists are written on the hub's thread and read on the test's.
    private static T Add<T>(List<T> list, T item)
    {
        lock (list)
        {
            list.Add(item);
        }

        return item;
    }

    private static int Count<T>(List<T> list)
    {
        lock (list)
        {
            return list.Count;
        }
    }

    private static T At<T>(List<T> list, int index)
    {
        lock (list)
        {
            return list[index];
        }
    }

    private static T Single<T>(List<T> list)
    {
        lock (list)
        {
            return list.Single();
        }
    }
}
