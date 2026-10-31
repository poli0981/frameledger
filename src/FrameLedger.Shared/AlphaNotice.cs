// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Shared;

/// <summary>
/// The Alpha notice (owner decision D74, 2026-10-31): <c>legal/ALPHA_NOTICE.md</c>, which an alpha build's Legal Gate shows
/// with a tick box of its own and records in <c>legal_acceptance</c> under <see cref="DocumentKey"/>. Here rather than in the
/// App because the Agent reads the same row: an alpha's <c>--serve</c> does not start until the notice is accepted at
/// <see cref="Version"/>. <c>AlphaNoticeTests</c> holds the constant to the document's own <c>**Version:**</c> line, so the
/// text cannot move without the gate showing it again.
/// </summary>
public static class AlphaNotice
{
    /// <summary>The document's key in <c>legal_acceptance</c>, beside <c>eula</c>, <c>disclaimer</c> and <c>privacy</c>.</summary>
    public const string DocumentKey = "alpha";

    /// <summary>The version this build carries, as the document's <c>**Version:**</c> line states it.</summary>
    public const string Version = "1.0";
}
