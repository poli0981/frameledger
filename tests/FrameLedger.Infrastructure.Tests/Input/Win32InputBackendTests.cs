// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Diagnostics;
using FluentAssertions;
using FrameLedger.Application.Input;
using FrameLedger.Infrastructure.Input;

namespace FrameLedger.Infrastructure.Tests.Input;

/// <summary>
/// The reader's Windows side against the real Windows of whatever machine runs this - a developer's PC with or without
/// a controller, CI's runner with none. It asserts the CONTRACT, never the hardware: it opens, answers every question
/// without throwing, says honestly what it could register, and leaves nothing behind. <b>What it reads from a real
/// controller is not established by this</b>; that is owner-only item 48.
/// </summary>
[Trait("Category", "Integration")]
public sealed class Win32InputBackendTests
{
    [Fact]
    public void ItOpensAnswersEveryQuestionAndClosesWithOrWithoutAController()
    {
        using var backend = new Win32InputBackend();
        var sink = new Recorder();

        // Refused is a valid answer - a session with no interactive desktop gives none - and is said, not hidden.
        bool registered = backend.Open();
        backend.Drain(sink);
        backend.Drain(sink);

        for (int slot = 0; slot < InputReader.Slots; slot++)
        {
            if (backend.TryReadPad(slot, out XInputReading pad))
            {
                pad.LeftTrigger.Should().BeInRange(0, 255);
                backend.TryReadPadDetails(slot, out XInputDetails details).Should().BeTrue("a pad XInput has a state for has capabilities");
                details.BatteryType.Should().BeOneOf(0, 1, 2, 3, 0xFF);
            }
        }

        if (backend.TryReadLastInputTick(out uint tick))
        {
            // The same clock as Environment.TickCount, and never ahead of it by more than a moment.
            unchecked((int)((uint)Environment.TickCount - tick)).Should().BeGreaterThan(-60_000);
        }

        uint front = backend.ForegroundProcessId();
        if (front != 0)
        {
            Action named = () => Process.GetProcessById((int)front).Dispose();
            named.Should().NotThrow("what is in front is a live process");
        }

        backend.ThreadCpuMs().Should().BeGreaterThanOrEqualTo(0);
        sink.Reports.Should().BeGreaterThanOrEqualTo(0);
        if (!registered)
        {
            sink.Arrived.Should().Be(0, "nothing is listed for a registration Windows refused");
        }
    }

    [Fact]
    public void AReaderOverItTakesSlicesAndSaysWhatAnswered()
    {
        using var backend = new Win32InputBackend();
        var reader = new InputReader(backend);
        reader.Open();

        InputSlice first = reader.Slice(100);
        InputSlice second = reader.Slice(100);

        second.Health.Slices.Should().Be(2);
        second.Health.Faults.Should().Be(0);
        second.Health.ThreadCpuMs.Should().BeGreaterThanOrEqualTo(first.Health.ThreadCpuMs);
        second.Health.XInputAvailable.Should().Be(backend.XInputAvailable);
        // All(), not OnlyContain: with no controller connected the list is empty, and that is a valid answer.
        second.Devices.All(static d => d.Device.Id > 0).Should().BeTrue("every controller it lists has a number");
    }

    [Fact]
    public void TheHubRunsOverItAndEndsCleanly()
    {
        var notes = new List<string>();
        using var hub = new InputDeviceHub(static () => new Win32InputBackend(), interval: TimeSpan.FromMilliseconds(10), note: line =>
        {
            lock (notes)
            {
                notes.Add(line);
            }
        });
        IInputActivitySource view = hub.Open();
        var clock = Stopwatch.StartNew();
        InputReading? reading = null;
        while (reading is null && clock.Elapsed < TimeSpan.FromSeconds(10))
        {
            if (!view.TryRead(out reading))
            {
                Thread.Sleep(5);
            }
        }

        view.Dispose();

        reading.Should().NotBeNull("the reader's thread made a slice");
        reading.SpanMs.Should().BeGreaterThanOrEqualTo(0);
        reading.ForegroundMs.Should().Be(0, "this view followed no process");
        lock (notes)
        {
            notes.Should().NotContain(static n => n.Contains("did not start", StringComparison.Ordinal) || n.Contains("threw", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void ItCanBeOpenedAgainAfterItWasClosedAndClosedTwice()
    {
        var first = new Win32InputBackend();
        bool registered = first.Open();
        first.Dispose();
        first.Dispose();

        using var second = new Win32InputBackend();
        second.Open().Should().Be(registered, "the window's class and the registration were given back");
        Action reopen = () => first.Open();
        reopen.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void BeforeItIsOpenedItHandsOverNothing()
    {
        using var backend = new Win32InputBackend();
        var sink = new Recorder();

        backend.Drain(sink);

        sink.Arrived.Should().Be(0);
        sink.Reports.Should().Be(0);
    }

    private sealed class Recorder : IInputBackendSink
    {
        public int Arrived { get; private set; }

        public int Reports { get; private set; }

        void IInputBackendSink.Arrived(nint device, RawController controller) => Arrived++;

        public void Removed(nint device)
        {
        }

        public void Report(nint device, ReadOnlySpan<byte> report) => Reports++;
    }
}
