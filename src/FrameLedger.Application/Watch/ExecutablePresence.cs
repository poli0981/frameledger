// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Watch;

/// <summary>
/// Why an entry's executable did not read (beta.19, D55) — the distinctions <c>ExecutableIdentity.Read</c>'s null does not
/// make, and the only ones that may remove an entry: <see cref="FileGone"/> is the one answer that does.
/// </summary>
public enum ExecutablePresence
{
    /// <summary>Nothing could be told (a share, a refused listing, an error) — never a reason to remove anything.</summary>
    Unknown,

    /// <summary>The file is there.</summary>
    Present,

    /// <summary>Its drive is not connected (or not ready): the relocator follows it when it is back on another letter.</summary>
    DriveAbsent,

    /// <summary>
    /// The drive is there, and nothing of the path is but its root: a different disk under the same letter as easily as an
    /// uninstall, so not one.
    /// </summary>
    OnlyTheDriveRemains,

    /// <summary>The file is gone from a folder that is still there, or its folder is gone from one that is: uninstalled.</summary>
    FileGone,
}
