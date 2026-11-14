// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Input;

/// <summary>
/// What each source of the input reader did, as counts (<c>21_INPUT_DEVICES</c> §What the reader says about itself).
/// The reader was built without a controller to try it on (owner decision, 2026-11-14), so a session carries the
/// evidence of what answered on the PC it ran on: whether Windows accepted the registration, how many reports came,
/// whether XInput said anything while the Agent was in the background. Counts only - nothing here names a device.
/// </summary>
public sealed record InputHealth
{
    /// <summary>Whether Windows accepted the registration for game controllers' reports.</summary>
    public bool RawInputRegistered { get; init; }

    /// <summary>Reports of game controllers received.</summary>
    public long RawReports { get; init; }

    /// <summary>Reports that could not be read: a layout the parser does not know, or one Windows' own parser refused.</summary>
    public long RawReportsUnread { get; init; }

    /// <summary>Whether XInput's library was there to ask.</summary>
    public bool XInputAvailable { get; init; }

    /// <summary>States XInput returned for a connected pad.</summary>
    public long XInputReads { get; init; }

    /// <summary>
    /// Slices in which a controller Windows presents through XInput sent reports that are a pad in use while XInput's
    /// own state said nothing was. More than a handful means XInput does not answer a program in the background on
    /// this PC, and its pads were then read from their reports alone.
    /// </summary>
    public long XInputSilent { get; init; }

    /// <summary>Whether Windows gave the time of the last keyboard or mouse input.</summary>
    public bool LastInputAvailable { get; init; }

    /// <summary>Slices the reader took.</summary>
    public long Slices { get; init; }

    /// <summary>Processor time the reader's thread used, in milliseconds.</summary>
    public long ThreadCpuMs { get; init; }

    /// <summary>Slices that failed with an error and were skipped.</summary>
    public int Faults { get; init; }

    /// <summary>What happened since <paramref name="start"/>: the counts less the ones it holds; the yes-or-no answers are the later ones.</summary>
    public InputHealth Since(InputHealth start)
    {
        ArgumentNullException.ThrowIfNull(start);
        return this with
        {
            RawReports = RawReports - start.RawReports,
            RawReportsUnread = RawReportsUnread - start.RawReportsUnread,
            XInputReads = XInputReads - start.XInputReads,
            XInputSilent = XInputSilent - start.XInputSilent,
            Slices = Slices - start.Slices,
            ThreadCpuMs = ThreadCpuMs - start.ThreadCpuMs,
            Faults = Faults - start.Faults,
        };
    }

    /// <summary>The sources that answered, as <c>sessions.input_source</c> stores them: <c>rawinput</c>, <c>xinput</c> and <c>lastinput</c>, joined by <c>+</c>; empty when none did.</summary>
    public string Sources()
    {
        var parts = new List<string>(3);
        if (RawInputRegistered)
        {
            parts.Add("rawinput");
        }

        if (XInputAvailable)
        {
            parts.Add("xinput");
        }

        if (LastInputAvailable)
        {
            parts.Add("lastinput");
        }

        return string.Join('+', parts);
    }
}
