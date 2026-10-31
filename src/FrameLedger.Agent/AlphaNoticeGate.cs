// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Application.Persistence;
using FrameLedger.Shared;

namespace FrameLedger.Agent;

/// <summary>
/// The alpha line's condition on <c>--serve</c> (owner decision D74, 2026-10-31): an alpha build's Agent serves only once
/// the Alpha notice is accepted at the version this build carries — the App's Legal Gate writes that row. The logon task or
/// the admin mode's prompt can start the Agent before the App has ever shown the notice; that Agent says why in its log and
/// exits, and the App starts it again after the Accept. The main line serves as it always has.
/// </summary>
internal static class AlphaNoticeGate
{
    /// <summary>True when <paramref name="line"/> may serve: the main line always, the alpha line once the notice is accepted.</summary>
    public static async Task<bool> AllowsServingAsync(BuildLine line, ILegalAcceptanceStore store, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(store);
        if (!line.IsAlpha)
        {
            return true;
        }

        LegalAcceptance? accepted = await store.FindAsync(AlphaNotice.DocumentKey, ct).ConfigureAwait(false);
        return accepted is not null && string.Equals(accepted.Version, AlphaNotice.Version, StringComparison.Ordinal);
    }
}
