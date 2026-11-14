// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Infrastructure.Import;

/// <summary>What <see cref="SteamAppInfo"/> read.</summary>
/// <param name="Support">For each application asked about that has a record: <c>full</c>, <c>partial</c> or <c>none</c>. An id that is not here has no record in the file.</param>
/// <param name="Problem">Why the file could not be read, in a few words; null when it was. With a problem, <paramref name="Support"/> is empty.</param>
public sealed record SteamAppInfoResult(IReadOnlyDictionary<uint, string> Support, string? Problem)
{
    public static SteamAppInfoResult Failed(string problem) => new(new Dictionary<uint, string>(), problem);
}
