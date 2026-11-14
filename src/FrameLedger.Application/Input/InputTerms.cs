// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Globalization;

namespace FrameLedger.Application.Input;

/// <summary>
/// The terms that describe the input reading, and whether they have been accepted on this PC (owner decision D156,
/// 2026-11-14). The option is on unless switched off, and the Agent is often started at logon - before the App that
/// shows a new Privacy Policy has been opened. So the Agent reads controllers and keyboard-and-mouse activity only
/// when the Privacy Policy the user accepted is the one that first described that reading, or a later one.
/// </summary>
public static class InputTerms
{
    /// <summary>The Privacy Policy's key in <c>legal_acceptance</c> - the App's <c>LegalDocuments.Privacy</c>, which a test holds to this.</summary>
    public const string PrivacyDocument = "privacy";

    /// <summary>The first version of the Privacy Policy that describes the reading.</summary>
    public const string MinimumPrivacyVersion = "2.22";

    /// <summary>
    /// Whether an accepted version is <see cref="MinimumPrivacyVersion"/> or later. A version is two whole numbers with
    /// a dot between them, compared as numbers - 2.9 is before 2.22. No version, and anything that is not one, is not
    /// accepted: a document nobody can be shown to have accepted permits nothing.
    /// </summary>
    public static bool Accepted(string? acceptedVersion) =>
        Parse(acceptedVersion) is { } accepted && Parse(MinimumPrivacyVersion) is { } minimum
        && (accepted.Major > minimum.Major || (accepted.Major == minimum.Major && accepted.Minor >= minimum.Minor));

    private static (int Major, int Minor)? Parse(string? version)
    {
        if (version is null)
        {
            return null;
        }

        string[] parts = version.Trim().Split('.');
        return parts.Length == 2
            && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int major)
            && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int minor)
            ? (major, minor)
            : null;
    }
}
