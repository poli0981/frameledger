// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Capture;

/// <summary>
/// Pins a pid for the session — a handle held from before the injection until the session
/// ends, so the identity the consent record was checked against cannot recycle under it
/// (<c>20_OPEN_QUESTIONS</c> §S29(e)).
/// </summary>
public interface ITargetLivenessSource
{
    /// <summary>
    /// Null when the pid cannot be pinned — already gone, protected, or another user's. The
    /// session refuses rather than injecting into an identity it cannot hold.
    /// </summary>
    ITargetLiveness? TryPin(int pid);
}
