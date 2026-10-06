// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Domain.Metrics;

/// <summary>
/// <c>03_METRICS</c> §Sync (beta.14, D49): what the game asked of each present — waiting for the display (a sync interval of
/// one or more: VSync) or allowing tearing (<c>DXGI_PRESENT_ALLOW_TEARING</c>) — as shares of the presents that carried their
/// arguments (<see cref="MeasuredFields.PresentArgs"/>). Both were in every Direct3D record since P1 and read by nothing.
/// </summary>
/// <remarks>
/// <b>Asked, not obtained.</b> A sync interval is the game's request; the driver can override it (a control-panel VSync
/// setting, a variable-refresh display), and nothing in the process says which won. OpenGL's <c>wglSwapBuffers</c> and
/// Vulkan's <c>vkQueuePresentKHR</c> carry neither argument, so a session on those APIs has no presents to count: null,
/// never a 0 % that nobody measured.
/// </remarks>
public sealed record PresentArgsShares
{
    /// <summary><c>DXGI_PRESENT_ALLOW_TEARING</c>.</summary>
    public const uint AllowTearing = 0x200;

    /// <summary>Presents that carried their arguments.</summary>
    public required int Count { get; init; }

    /// <summary>Of those, the ones with a sync interval of one or more.</summary>
    public required int VsyncCount { get; init; }

    /// <summary>Of those, the ones that allowed tearing.</summary>
    public required int TearingCount { get; init; }

    /// <summary>Percent of the counted presents that waited for the display; null when none was counted.</summary>
    public double? VsyncPct => Count > 0 ? VsyncCount * 100.0 / Count : null;

    /// <summary>Percent of the counted presents that allowed tearing; null when none was counted.</summary>
    public double? TearingAllowedPct => Count > 0 ? TearingCount * 100.0 / Count : null;

    public static PresentArgsShares From(IReadOnlyList<FrameSample> stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        int count = 0;
        int vsync = 0;
        int tearing = 0;
        foreach (FrameSample s in stream)
        {
            if (!s.Claims(MeasuredFields.PresentArgs))
            {
                continue;
            }

            count++;
            if (s.SyncInterval >= 1)
            {
                vsync++;
            }

            if ((s.PresentFlags & AllowTearing) != 0)
            {
                tearing++;
            }
        }

        return new PresentArgsShares { Count = count, VsyncCount = vsync, TearingCount = tearing };
    }
}
