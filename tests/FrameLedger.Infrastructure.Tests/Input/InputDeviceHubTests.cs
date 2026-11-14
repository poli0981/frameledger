// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Diagnostics;
using FluentAssertions;
using FrameLedger.Application.Capture;
using FrameLedger.Application.Input;
using FrameLedger.Application.Telemetry;
using FrameLedger.Infrastructure.Input;
using FrameLedger.Testing;

namespace FrameLedger.Infrastructure.Tests.Input;

/// <summary>
/// The Agent's one input reader and the sessions' views of it (<c>21_INPUT_DEVICES</c> §The reader): it runs only
/// while a view is open, each view counts the slices in which its own game was in front, a pause is not observed, and
/// a reader that keeps failing ends its run with one line and takes no session with it. The Windows behind it is the
/// tests' own.
/// </summary>
public sealed class InputDeviceHubTests
{
    private static readonly TimeSpan _slice = TimeSpan.FromMilliseconds(5);
    private static readonly TimeSpan _patience = TimeSpan.FromSeconds(10);

    private static GameProcess Pids(params int[] pids) => GameProcess.Unpinned([.. pids.Select(static p => new GameProcessId(p, DateTimeOffset.UnixEpoch))]);

    /// <summary>Reads the view until <paramref name="enough"/> says so, adding up what it returned.</summary>
    private static (int Span, int Observed, int Foreground, int Controller, long Slices) ReadUntil(IInputActivitySource view, Func<(int Span, int Observed, int Foreground, int Controller, long Slices), bool> enough)
    {
        (int Span, int Observed, int Foreground, int Controller, long Slices) sum = default;
        var clock = Stopwatch.StartNew();
        while (!enough(sum) && clock.Elapsed < _patience)
        {
            if (view.TryRead(out InputReading? reading))
            {
                sum = (sum.Span + reading.SpanMs, sum.Observed + reading.ObservedMs, sum.Foreground + reading.ForegroundMs, sum.Controller + reading.ControllerMs, reading.Health.Slices);
            }
            else
            {
                Thread.Sleep(2);
            }
        }

        return sum;
    }

    private static int Count(List<string> notes)
    {
        lock (notes)
        {
            return notes.Count;
        }
    }

    private static void WaitUntil(Func<bool> condition)
    {
        var clock = Stopwatch.StartNew();
        while (!condition() && clock.Elapsed < _patience)
        {
            Thread.Sleep(2);
        }

        condition().Should().BeTrue("it should have happened within the patience of the test");
    }

    [Fact]
    public void WithNoViewTheHubHasNoThreadAndMakesNoBackend()
    {
        int made = 0;
        using var hub = new InputDeviceHub(
            () =>
            {
                made++;
                return new FakeInputBackend();
            },
            interval: _slice);

        hub.IsRunning.Should().BeFalse("with no session nothing is read: no thread, no window, no registration");
        made.Should().Be(0);
    }

    [Fact]
    public void AViewCountsTheSlicesItsOwnGameWasInFrontOfAndAnotherViewItsOwn()
    {
        using var backend = new FakeInputBackend { Foreground = 500 };
        backend.Pads[0] = new XInputReading(1, Buttons: 0x1000, 0, 0, 0, 0, 0, 0);
        backend.Details[0] = new XInputDetails(Wireless: false, BatteryType: 1, BatteryLevel: 0);
        using var hub = new InputDeviceHub(() => backend, interval: _slice);
        using IInputActivitySource mine = hub.Open();
        using IInputActivitySource other = hub.Open();
        mine.Follow(Pids(400, 500));
        other.Follow(Pids(600));

        (int span, int observed, int foreground, int controller, long slices) = ReadUntil(mine, static s => s.Controller > 0 && s.Slices >= 3);
        (int otherSpan, int otherObserved, int otherForeground, int otherController, _) = ReadUntil(other, static s => s.Slices >= 3);

        hub.IsRunning.Should().BeTrue();
        backend.Opened.Should().BeTrue();
        slices.Should().BeGreaterThanOrEqualTo(3);
        foreground.Should().BeGreaterThan(0).And.BeLessThanOrEqualTo(observed);
        controller.Should().BeGreaterThan(0).And.BeLessThanOrEqualTo(foreground);
        observed.Should().BeLessThanOrEqualTo(span);
        otherObserved.Should().BeGreaterThan(0, "the other session is observed too");
        otherSpan.Should().BeGreaterThan(0);
        otherForeground.Should().Be(0, "its game was never in front");
        otherController.Should().Be(0, "a controller in use while another program is in front is not that session's");
    }

    [Fact]
    public void APinnedProcessIsTheSessionsGame()
    {
        using var backend = new FakeInputBackend { Foreground = 77 };
        using var hub = new InputDeviceHub(() => backend, interval: _slice);
        using IInputActivitySource view = hub.Open();
        view.Follow(GameProcess.Pinned(77, NSubstitute.Substitute.For<ITargetLiveness>()));

        ReadUntil(view, static s => s.Foreground > 0).Foreground.Should().BeGreaterThan(0);
    }

    [Fact]
    public void WithNoWindowInFrontNoSessionIsInFront()
    {
        using var backend = new FakeInputBackend { Foreground = 0 };
        using var hub = new InputDeviceHub(() => backend, interval: _slice);
        using IInputActivitySource view = hub.Open();
        view.Follow(Pids(0));

        (_, int observed, int foreground, _, _) = ReadUntil(view, static s => s.Slices >= 3);

        observed.Should().BeGreaterThan(0);
        foreground.Should().Be(0, "pid 0 is Windows' word for no window in front, never a game");
    }

    [Fact]
    public void WhileRecordingIsPausedASliceIsNotObserved()
    {
        bool paused = true;
        using var backend = new FakeInputBackend { Foreground = 500 };
        using var hub = new InputDeviceHub(() => backend, () => Volatile.Read(ref paused), _slice);
        using IInputActivitySource view = hub.Open();
        view.Follow(Pids(500));

        (int span, int observed, _, _, _) = ReadUntil(view, static s => s.Slices >= 3);
        observed.Should().Be(0, "a pause is a gap");
        span.Should().BeGreaterThan(0, "the clock does not stop for a pause: a battery level is timed by it");

        Volatile.Write(ref paused, false);
        ReadUntil(view, static s => s.Observed > 0).Observed.Should().BeGreaterThan(0);
    }

    [Fact]
    public void TheRunEndsWithItsLastViewAndANewViewStartsANewOne()
    {
        var made = new List<FakeInputBackend>();
        var notes = new List<string>();
        using var hub = new InputDeviceHub(
            () =>
            {
                var backend = new FakeInputBackend();
                lock (made)
                {
                    made.Add(backend);
                }

                return backend;
            },
            interval: _slice,
            note: line =>
            {
                lock (notes)
                {
                    notes.Add(line);
                }
            });

        IInputActivitySource first = hub.Open();
        ReadUntil(first, static s => s.Slices >= 2);
        first.Dispose();

        // The run's last act is its line in the log: by then the backend is disposed and the thread's slot is free.
        WaitUntil(() => Count(notes) == 1);
        hub.IsRunning.Should().BeFalse();

        lock (made)
        {
            made.Should().ContainSingle().Which.Disposed.Should().BeTrue("the window and the registration go with the last session");
        }

        lock (notes)
        {
            notes.Should().ContainSingle().Which.Should().StartWith("input: run ended - slices ").And.Contain("raw input registered").And.Contain("xinput there").And.Contain("last input read");
        }

        using IInputActivitySource second = hub.Open();
        ReadUntil(second, static s => s.Slices >= 1).Slices.Should().BeGreaterThanOrEqualTo(1);
        lock (made)
        {
            made.Should().HaveCount(2, "a new session opens a new run, with a backend of its own");
        }
    }

    [Fact]
    public void AReaderThatKeepsFailingEndsItsRunWithOneLineAndTheViewSimplyHasNothingMore()
    {
        var notes = new List<string>();
        using var backend = new FakeInputBackend { ThrowOnDrain = true };
        using var hub = new InputDeviceHub(() => backend, interval: _slice, note: line =>
        {
            lock (notes)
            {
                notes.Add(line);
            }
        });
        using IInputActivitySource view = hub.Open();

        WaitUntil(() => Count(notes) == 2);

        hub.IsRunning.Should().BeFalse();
        view.TryRead(out _).Should().BeFalse("no slice was ever made");
        lock (notes)
        {
            notes.Should().HaveCount(2);
            notes[0].Should().StartWith($"input: the reader's slice threw {InputDeviceHub.MaxFaults} times running and the run ends (InvalidOperationException");
            notes[1].Should().StartWith("input: run ended");
        }
    }

    [Fact]
    public void ABackendThatCannotBeMadeEndsTheRunWithOneLine()
    {
        var notes = new List<string>();
        using var hub = new InputDeviceHub(() => throw new InvalidOperationException("no window station"), interval: _slice, note: line =>
        {
            lock (notes)
            {
                notes.Add(line);
            }
        });
        using IInputActivitySource view = hub.Open();

        WaitUntil(() => Count(notes) == 2);

        hub.IsRunning.Should().BeFalse();
        lock (notes)
        {
            notes[0].Should().Be("input: the reader did not start (InvalidOperationException: no window station)");
        }
    }

    [Fact]
    public void ADisposedHubOpensNoViewAndDisposingTwiceIsNothing()
    {
        using var backend = new FakeInputBackend();
        var hub = new InputDeviceHub(() => backend, interval: _slice);
        IInputActivitySource view = hub.Open();
        ReadUntil(view, static s => s.Slices >= 1);

        hub.Dispose();
        hub.Dispose();

        hub.IsRunning.Should().BeFalse();
        Action open = () => hub.Open();
        open.Should().Throw<ObjectDisposedException>();
        view.Dispose();
    }

    [Fact]
    public void ASliceMustBeLongerThanNothing()
    {
        Action zero = () => _ = new InputDeviceHub(static () => new FakeInputBackend(), interval: TimeSpan.Zero);
        Action none = () => _ = new InputDeviceHub(null!);

        zero.Should().Throw<ArgumentOutOfRangeException>();
        none.Should().Throw<ArgumentNullException>();
    }
}
