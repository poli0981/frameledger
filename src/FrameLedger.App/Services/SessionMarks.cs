using System.Globalization;
using FrameLedger.Application.Persistence;

namespace FrameLedger.App.Services;

/// <summary>Marks a session carries for as long as it exists, stated the same way on every surface.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format string is a resource that follows the UI culture, which changes at runtime")]
public static class SessionMarks
{
    /// <summary>
    /// "Guard bypassed: Easy Anti-Cheat" for a session that STARTED under the user's guard bypass (schema 0007, owner
    /// decision 2026-09-21), naming what the guard found; null for every other session.
    /// </summary>
    public static string? GuardBypass(SessionRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (!row.GuardBypassed)
        {
            return null;
        }

        string found = row.GuardBypassFamily is { Length: > 0 } family ? family : Shared.Strings.Safety_Bypass_Session_Unnamed;
        return string.Format(CultureInfo.CurrentCulture, Shared.Strings.Safety_Bypass_Session_Format, found);
    }
}
