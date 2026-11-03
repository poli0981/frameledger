// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Infrastructure.Import;

/// <summary>
/// Windows' own answers about an app package (beta.33): the family name a package identity has, and whether a package of
/// that family is installed for the current user. The Xbox library asks both of every game folder it finds, so a folder
/// the Xbox app left behind, or one installed for another user, is not offered.
/// </summary>
public interface IInstalledPackages
{
    /// <summary>The package family name Windows computes for <paramref name="name"/> and <paramref name="publisher"/>, or null when it does not.</summary>
    string? FamilyName(string name, string publisher);

    /// <summary>True when a package of the family <paramref name="familyName"/> is installed for the current user.</summary>
    bool IsInstalled(string familyName);
}
