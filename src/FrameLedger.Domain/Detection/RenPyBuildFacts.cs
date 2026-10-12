// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Domain.Detection;

/// <summary>
/// What a Ren'Py title's own files say about the engine it was built with (beta.20), read from the files on disk.
/// </summary>
/// <remarks>
/// Ren'Py ships its engine as Python beside the game's launcher, and the engine's <c>renpy/vc_version.py</c> names its
/// release: <c>version = '8.5.0.25111603'</c>, with <c>version_name</c>, <c>branch</c>, <c>official</c> and <c>nightly</c>
/// beside it. Measured 2026-10-12 on the owner's one Ren'Py install, Bound Between Desks (Steam): <c>8.5.0.25111603</c>,
/// the same number the game's own <c>log.txt</c> printed at its last start (<c>Ren'Py 8.5.0.25111603</c>). Its launcher
/// executable carries no version resource.
/// </remarks>
public sealed record RenPyBuildFacts
{
    /// <summary>The <c>version</c> string of <c>renpy/vc_version.py</c> (<c>8.5.0.25111603</c>), or null when it names none.</summary>
    public string? VcVersion { get; init; }
}
