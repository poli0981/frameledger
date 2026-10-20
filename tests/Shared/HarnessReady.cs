// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Diagnostics;

namespace FrameLedger.Testing;

/// <summary>
/// When a hook-harness started for an injection test is really up: the line it prints, flushed, right before its present
/// loop — after its device and swap chain exist, so its module list is whole and it is presenting.
/// </summary>
/// <remarks>
/// The injection tests slept 800 ms after starting the harness and then looked for it or injected into it: a guess at
/// when it presents, made under a full <c>build.ps1 check</c> — every test assembly at once, each starting WARP devices —
/// where a guess can be short. Waiting for the harness's own signal replaces the guess with the event it stood for
/// (beta.27). Reading its standard output asynchronously also keeps that pipe drained for the rest of its life, so a
/// harness that prints more never blocks on a full pipe. The guard's refusals of 2026-10-19 and 20 were not this:
/// <c>ShmDrainIntegrationTests.InjectAsync</c> says what they were.
/// </remarks>
internal static class HarnessReady
{
    /// <summary>What hook-harness prints, then flushes, right before it presents (<c>main.cpp</c>, <c>--hold-presenting</c>).</summary>
    public const string PresentingMarker = "presenting for";

    /// <summary>How long a harness may take to reach its present loop; bounded, so one that never does fails its test.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    /// <summary>Whether <paramref name="harness"/> printed <see cref="PresentingMarker"/> within <see cref="Timeout"/>.</summary>
    /// <param name="harness">A hook-harness started with its standard output redirected, which nobody else reads.</param>
    /// <param name="ct">The test's cancellation.</param>
    public static async Task<bool> WaitUntilPresentingAsync(Process harness, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(harness);
        var presenting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.OutputDataReceived += (_, e) =>
        {
            if (e.Data is { } line && line.Contains(PresentingMarker, StringComparison.Ordinal))
            {
                presenting.TrySetResult();
            }
        };
        harness.BeginOutputReadLine();
        try
        {
            await presenting.Task.WaitAsync(Timeout, ct).ConfigureAwait(false);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }
}
