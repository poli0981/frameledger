// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Diagnostics.CodeAnalysis;
using FrameLedger.Application.Advanced;

namespace FrameLedger.Application.Recording;

/// <summary>
/// What a hooked game published and no telemetry sample carries yet (owner decision D150): at most
/// <see cref="Capacity"/> sets of readings, the older taken first.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why not one.</b> A publication arrives about once a second and a sample exactly once a second, on two clocks,
/// and the recorder looks at both every tenth of a second or so. Until 0.1.0-beta.49 it kept the newest set and
/// overwrote it: when a publication landed just after a sample, a look could fall between the two - that sample went
/// without, and at the next change of step the set that had waited was overwritten by its successor. In the owner's
/// sessions that was one sample in five without a report for minutes on end (HANDOFF, item 46).
/// </para>
/// <para>
/// <b>What two give.</b> A set that waited is carried by the next sample instead of being replaced, so with both sides
/// on a second a session lacks a report at one sample at most - the first time the two fall out of step - and loses
/// none after it. A reading then stands at the sample after the one it would have ridden: a second later, no more.
/// </para>
/// <para>
/// <b>What it does not give.</b> With the sensor interval above a second more sets arrive than samples: a third
/// pushes the oldest out, and not every reading is stored - as before, and as the guide says.
/// </para>
/// </remarks>
public sealed class PendingInGameReadings
{
    /// <summary>The most sets that wait: the one a sample is about to carry, and the one that arrived meanwhile.</summary>
    public const int Capacity = 2;

    private readonly Queue<IReadOnlyList<AdvancedReading>> _waiting = new(Capacity);

    /// <summary>How many sets wait.</summary>
    public int Count => _waiting.Count;

    /// <summary>A publication's readings. A set with nothing in it does not wait: it would only stand in a real one's way.</summary>
    /// <param name="readings">The readings of one publication.</param>
    public void Offer(IReadOnlyList<AdvancedReading> readings)
    {
        ArgumentNullException.ThrowIfNull(readings);
        if (readings.Count == 0)
        {
            return;
        }

        if (_waiting.Count == Capacity)
        {
            _ = _waiting.Dequeue();
        }

        _waiting.Enqueue(readings);
    }

    /// <summary>The oldest set that waits, which then waits no longer; false when none does.</summary>
    /// <param name="readings">The set, when there is one.</param>
    public bool TryTake([NotNullWhen(true)] out IReadOnlyList<AdvancedReading>? readings) => _waiting.TryDequeue(out readings);
}
