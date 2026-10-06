// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Update;

/// <summary>
/// The updater as a viewer sees it (beta.15, D52): an update replaces this PC's installed copy, so a <c>--data-dir</c>
/// window never looks for one, downloads one or applies one — it answers as a copy the installer did not lay down.
/// </summary>
public sealed class ViewerUpdateClient : IUpdateClient
{
    public bool IsInstalled => false;

    public string? CurrentVersion => null;

    public UpdateCandidate? FindPendingRestart() => null;

    public Task<UpdateCandidate?> CheckAsync(bool includePrereleases, CancellationToken ct = default) => Task.FromResult<UpdateCandidate?>(null);

    public Task DownloadAsync(UpdateCandidate candidate, IProgress<int>? progress, CancellationToken ct = default) =>
        throw new InvalidOperationException("a viewer (--data-dir) does not update");

    public void ApplyOnExit(UpdateCandidate candidate) => throw new InvalidOperationException("a viewer (--data-dir) does not update");
}
