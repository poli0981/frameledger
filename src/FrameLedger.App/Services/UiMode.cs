// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Services;

/// <summary>
/// The App over its own data folder, or a VIEWER over a copy of one (beta.15, owner decision D52:
/// <c>FrameLedger.exe --data-dir &lt;folder&gt;</c>, read by <see cref="DataFolderArgument"/>).
/// </summary>
/// <remarks>
/// <para>
/// A viewer opens the ledger in that folder and logs beside it, and it starts and contacts no Agent: the pipe is one per
/// user and the Agent behind it records into the PROFILE's ledger, so a request carrying this ledger's game ids would act
/// on another game — a consent written for the wrong one (rule 1), sessions deleted from the wrong one. Nor does it
/// change anything on this PC: no Run entry, no Vulkan layer, no logon task, no administrator mode, no update. The
/// composition holds that structurally (<see cref="MachineFacingServices"/> registers no pipe client and no launcher in a
/// viewer); the pages disable the controls that would ask, so nothing on screen offers what cannot happen.
/// </para>
/// <para>
/// D6 is unchanged: the Agent's own <c>--data-dir</c> stays under <c>--console</c>.
/// </para>
/// </remarks>
/// <param name="IsViewer">True under <c>--data-dir</c>.</param>
/// <param name="DataDirectory">The folder the ledger, the logs and the crash dumps are in.</param>
public sealed record UiMode(bool IsViewer, string DataDirectory)
{
    /// <summary>The App over the profile's own folder, as every composition without a <c>--data-dir</c> is.</summary>
    public static UiMode Profile { get; } = new(false, UiPaths.ProfileDirectory);

    /// <summary>Whether the controls that ask the Agent, or change this PC, are live: everywhere but a viewer.</summary>
    public bool ActsOnThisPc => !IsViewer;
}
