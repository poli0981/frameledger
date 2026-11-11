// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Diagnostics;
using System.Globalization;
using FrameLedger.Application.Advanced;
using FrameLedger.Infrastructure.Telemetry;

namespace FrameLedger.CaptureHost.Telemetry;

/// <summary>
/// The instrument of <c>20_OPEN_QUESTIONS</c> §M12 and §M13: what does "Advanced metrics" read on THIS PC, and what
/// does reading it cost? It runs the reader the Agent will run — <see cref="AdvancedHardwareReader"/> over the real
/// NVAPI bridge and LibreHardwareMonitor — for a few ticks, and prints everything it got.
/// </summary>
/// <remarks>
/// <para>
/// <b>It opens exactly what the Agent would, on this process's own rights.</b> Unelevated: NVAPI and the graphics
/// card's sensors. As administrator with PawnIO installed: the processor, the memory and the motherboard as well —
/// that run is the first there has ever been (§M13, and §M10 with it), which is why its cost is printed: the time
/// the open took, each tick's, the processor time this process used and its working set (NFR-2).
/// </para>
/// <para>
/// <b>No game, no injection, no consent record</b> — like <c>probe-lhm</c>, it touches this process and nothing else.
/// The storage group is not opened by it or by anything (D103).
/// </para>
/// </remarks>
internal static class AdvancedProbe
{
    public const int DefaultSeconds = 5;

    public static int Run(int seconds)
    {
        bool elevated = LhmEnvironment.IsElevated;
        bool? pawnIo = LhmEnvironment.IsPawnIoInstalled;
        HostConsole.Line($"probe-advanced: {seconds} tick(s) of the reader Advanced metrics uses");
        HostConsole.Line($"  process elevated: {elevated}   PawnIO installed: {pawnIo?.ToString() ?? "unknown"}   sensor groups to open: {LhmAdvancedReader.GroupsFor(elevated, pawnIo)}");

        using var self = Process.GetCurrentProcess();
        TimeSpan cpuBefore = self.TotalProcessorTime;
        using AdvancedHardwareReader reader = AdvancedHardwareReader.OverThisPc(TimeProvider.System, static note => HostConsole.Line("  NOTE " + note));

        long started = Stopwatch.GetTimestamp();
        reader.Open();
        HostConsole.Line(string.Create(CultureInfo.InvariantCulture, $"  open: {Stopwatch.GetElapsedTime(started).TotalMilliseconds:F0} ms"));

        // Elapsed time and processor time are told apart on purpose: a tick spends most of its time waiting for the
        // driver, and NFR-2 is about the processor. The first tick carries the libraries' start-up and is listed, not
        // averaged.
        AdvancedSnapshot? last = null;
        var tickMs = new List<double>(seconds);
        var cpuMs = new List<double>(seconds);
        for (int tick = 1; tick <= seconds; tick++)
        {
            self.Refresh();
            TimeSpan cpuAt = self.TotalProcessorTime;
            long at = Stopwatch.GetTimestamp();
            last = reader.Tick();
            tickMs.Add(Stopwatch.GetElapsedTime(at).TotalMilliseconds);
            self.Refresh();
            cpuMs.Add((self.TotalProcessorTime - cpuAt).TotalMilliseconds);
            HostConsole.Line(string.Create(CultureInfo.InvariantCulture, $"  tick {tick}: {last.Readings.Count} reading(s) in {tickMs[^1]:F1} ms elapsed, {cpuMs[^1]:F1} ms of processor time"));
            if (tick < seconds)
            {
                Thread.Sleep(1000);
            }
        }

        if (last is null)
        {
            return 0;
        }

        Describe(last);
        self.Refresh();
        double[] steady = cpuMs.Count > 1 ? [.. cpuMs.Skip(1)] : [.. cpuMs];
        HostConsole.Line(string.Create(CultureInfo.InvariantCulture,
            $"  cost: a tick takes {tickMs.Min():F1} to {tickMs.Max():F1} ms elapsed (mean {tickMs.Average():F1}); after the first, {steady.Average():F1} ms of processor time each (max {steady.Max():F1}) = {steady.Average() / 10.0:F2}% of one core at one tick a second"));
        HostConsole.Line(string.Create(CultureInfo.InvariantCulture,
            $"  cost: this process used {(self.TotalProcessorTime - cpuBefore).TotalMilliseconds:F0} ms of processor time in all, start-up included; working set {self.WorkingSet64 / (1024.0 * 1024.0):F0} MiB. NFR-2 allows the whole Agent 1% of one core and 150 MiB"));
        CostBySource(self, elevated, pawnIo);
        return 0;
    }

    /// <summary>
    /// Which source the processor time goes to: each read on its own, many times back to back, because the clock a
    /// process's processor time is counted with ticks every 15.6 ms and one read is shorter than that.
    /// </summary>
    private static void CostBySource(Process self, bool elevated, bool? pawnIo)
    {
        const int Reads = 40;
        using (NvapiAdvancedReader nvapi = NvapiAdvancedReader.OverTheNativeBridge())
        {
            if (nvapi.Start())
            {
                Measure(self, "NVAPI, one live read", Reads, nvapi, static reader => _ = reader.ReadLive());
            }
        }

        using var sensors = new LhmAdvancedReader(new LhmComputerAdapter(LhmAdvancedReader.GroupsFor(elevated, pawnIo)));
        if (sensors.Open())
        {
            Measure(self, "the sensor library, one refresh and read", Reads, sensors, static reader =>
            {
                reader.Refresh();
                _ = reader.Read();
            });
        }
    }

    private static void Measure<T>(Process self, string what, int reads, T source, Action<T> read)
    {
        read(source);
        self.Refresh();
        TimeSpan cpu = self.TotalProcessorTime;
        long at = Stopwatch.GetTimestamp();
        for (int i = 0; i < reads; i++)
        {
            read(source);
        }

        double elapsed = Stopwatch.GetElapsedTime(at).TotalMilliseconds / reads;
        self.Refresh();
        double processor = (self.TotalProcessorTime - cpu).TotalMilliseconds / reads;
        HostConsole.Line(string.Create(CultureInfo.InvariantCulture, $"  cost by source: {what}: {elapsed:F1} ms elapsed, {processor:F1} ms of processor time (mean of {reads})"));
    }

    private static void Describe(AdvancedSnapshot snapshot)
    {
        AdvancedSources s = snapshot.Sources;
        HostConsole.Line($"  sources: NVAPI {s.Nvapi}{(s.NvapiCode is { } code ? $" ({code})" : string.Empty)}; card sensors {s.Sensors}; processor, memory and board sensors {s.ElevatedSensors}");

        HostConsole.Line($"  facts of the PC: {snapshot.MachineFacts.Count}");
        foreach (AdvancedFact fact in snapshot.MachineFacts)
        {
            HostConsole.Line($"    {fact.Id}{(fact.Instance is null ? string.Empty : "#" + fact.Instance)} = {Describe(fact.Value)}");
        }

        var labels = snapshot.Series.ToDictionary(static i => i.Series, static i => i.Label, StringComparer.Ordinal);
        HostConsole.Line($"  readings of the last tick: {snapshot.Readings.Count}");
        foreach (AdvancedReading reading in snapshot.Readings)
        {
            string label = labels.TryGetValue(reading.Series, out string? text) ? $"   [{text}]" : string.Empty;
            HostConsole.Line(string.Create(CultureInfo.InvariantCulture, $"    {reading.Series} = {reading.Value:0.###}{label}"));
        }
    }

    private static string Describe(AdvancedValue value)
    {
        if (value.Number is { } number)
        {
            return number.ToString("0.####", CultureInfo.InvariantCulture);
        }

        if (value.Text is { } text)
        {
            return "\"" + text + "\"";
        }

        if (value.Tokens is { } tokens)
        {
            return "[" + string.Join(", ", tokens) + "]";
        }

        // A table is counted, not listed: a driver profile is the user's settings, and a console scrolls into a paste.
        return value.Rows is { } rows ? $"{rows.Count} row(s)" : "?";
    }
}
