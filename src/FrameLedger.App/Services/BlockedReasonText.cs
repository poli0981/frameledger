using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace FrameLedger.App.Services;

/// <summary>
/// <c>games.hook_blocked_reason</c> in the user's language (beta.8, 2026-09-25): which anti-cheat, and what of it the
/// guard found — a module in the game, a folder or file shipped with it, the title or its store id on the lists.
/// </summary>
/// <remarks>
/// The Agent stores a block as <c>Reason|Family|Signal</c> since 2026-09-25 (<c>SqliteGameConsentStore.BlockText</c>).
/// Rows written before then say <c>"Reason: Family Signal"</c>, which cannot be split back apart — family names and file
/// names both carry spaces — so those are shown as they were stored, which is what the page always showed.
/// </remarks>
[SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture, which changes at runtime")]
public static class BlockedReasonText
{
    public static string Describe(string stored)
    {
        ArgumentNullException.ThrowIfNull(stored);
        string[] parts = stored.Split('|');
        if (parts.Length != 3)
        {
            return stored;
        }

        string format = parts[0] switch
        {
            "BlockedModule" => Strings.Blocked_Module_Format,
            "AntiCheatDirectory" => Strings.Blocked_Directory_Format,
            "AntiCheatFile" => Strings.Blocked_File_Format,
            "BlockedExecutable" => Strings.Blocked_Executable_Format,
            "BlockedStoreId" => Strings.Blocked_StoreId_Format,
            _ => Strings.Blocked_Other_Format,
        };
        string family = parts[1].Length > 0 ? parts[1] : Strings.Common_NotAvailable;
        string signal = parts[2].Length > 0 ? parts[2] : Strings.Common_NotAvailable;
        return string.Format(CultureInfo.CurrentCulture, format, family, signal);
    }
}
