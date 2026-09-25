using System.Globalization;

namespace FrameLedger.Application.Recording;

/// <summary>
/// The words <c>sessions.capture_notes</c> is made of (<c>ExitStatusMapper.Describe</c>, <c>SessionRecorder.Skeleton</c>):
/// <c>end=&lt;SessionEndReason&gt;; exit_code=…; crash_event=…; launch_error=…; tier2: attach=…; guard=&lt;reason&gt;|&lt;family&gt;|&lt;signal&gt;; …</c>.
/// Read back so a row can say why it ended and, for Tier 2, why it measured nothing (<c>04_CAPTURE</c>: "the reason is the
/// payload of Tier 2"), which the schema keeps in this one text column rather than in fields (2026-09-22).
/// </summary>
/// <remarks>
/// <para>
/// <b>The guard slot has three positions since beta.8 (2026-09-25):</b> <c>guard=Reason|Family|Signal</c>, every slot
/// present even when empty, and <c>;</c> in a signal written as <c>,</c> so it cannot end the slot. Rows written before
/// kept <c>/</c> between the parts and dropped an empty family — so <c>guard=ProcessUnreadable/Access is denied</c> put the
/// signal where the family goes, and a signal with a <c>;</c> in it was cut there. Those rows are read with the one rule
/// that tells the two apart (<see cref="NamesAFamily"/>): a finding names its anti-cheat family, and the gate's own refusals
/// put a label in that slot (<c>not enabled</c>, <c>no consent</c>, <c>kill switch</c>, <c>previously blocked</c>); for any
/// other reason — the guard's "could not look" answers — the second part was the signal.
/// </para>
/// <para>
/// The signal is the last slot and may itself carry <c>|</c>: a <c>PreviouslyBlocked</c> signal is the row's stored block,
/// <c>Reason|Family|Signal</c> since beta.8, and <c>Split('|', 3)</c> keeps it whole.
/// </para>
/// </remarks>
public readonly record struct CaptureNotes(string? End, string? GuardReason, string? GuardFamily, string? GuardSignal)
{
    /// <summary>The process's exit code, when it had one (<c>exit_code=</c>).</summary>
    public int? ExitCode { get; init; }

    /// <summary>Whether the Application log named the executable around the end (<c>crash_event=</c>).</summary>
    public bool CrashEvent { get; init; }

    /// <summary>The Win32 error a launch failed with (<c>launch_error=</c>, beta.8).</summary>
    public int? LaunchError { get; init; }

    public static CaptureNotes Parse(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return default;
        }

        string? end = null, reason = null, family = null, signal = null;
        int? exitCode = null, launchError = null;
        bool crashEvent = false;
        foreach (string raw in notes.Split(';'))
        {
            string part = raw.Trim();
            if (part.StartsWith("end=", StringComparison.Ordinal))
            {
                end = part[4..];
            }
            else if (part.StartsWith("exit_code=", StringComparison.Ordinal))
            {
                exitCode = Int(part[10..]);
            }
            else if (part.StartsWith("crash_event=", StringComparison.Ordinal))
            {
                crashEvent = true;
            }
            else if (part.StartsWith("launch_error=", StringComparison.Ordinal))
            {
                launchError = Int(part[13..]);
            }
            else if (part.StartsWith("guard=", StringComparison.Ordinal))
            {
                (reason, family, signal) = Guard(part[6..]);
            }
        }

        return new CaptureNotes(end, reason, family, signal) { ExitCode = exitCode, CrashEvent = crashEvent, LaunchError = launchError };
    }

    /// <summary>The guard slot as the recorder writes it (beta.8): three positions, a <c>;</c> never inside one.</summary>
    public static string GuardSlot(string reason, string? family, string? signal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return "guard=" + Clean(reason) + "|" + Clean(family) + "|" + Clean(signal);
    }

    /// <summary>
    /// The reasons whose second legacy part was a family: the findings, which name one, and the gate's own refusals, which
    /// always carried a label there (<c>HookedCaptureGate</c>). Every other reason's second part was its signal.
    /// </summary>
    public static bool NamesAFamily(string? reason) => reason is "BlockedModule" or "BlockedDriver" or "BlockedService"
        or "BlockedExecutable" or "BlockedStoreId" or "AntiCheatDirectory" or "AntiCheatFile" or "SuspiciousUnsigned"
        or "HookNotEnabled" or "ConsentMissing" or "KillSwitchEngaged" or "PreviouslyBlocked";

    private static (string? Reason, string? Family, string? Signal) Guard(string value)
    {
        if (value.Contains('|', StringComparison.Ordinal))
        {
            string[] slots = value.Split('|', 3);
            return (Blank(slots[0]), slots.Length > 1 ? Blank(slots[1]) : null, slots.Length > 2 ? Blank(slots[2]) : null);
        }

        // A row from before beta.8: '/' between the parts and an empty family dropped.
        string[] words = value.Split('/', 3);
        string? reason = Blank(words[0]);
        return words.Length switch
        {
            1 => (reason, null, null),
            2 when NamesAFamily(reason) => (reason, Blank(words[1]), null),
            2 => (reason, null, Blank(words[1])),
            _ => (reason, Blank(words[1]), Blank(words[2])),
        };
    }

    private static string Clean(string? s) => string.IsNullOrEmpty(s) ? string.Empty : s.Replace(';', ',');

    private static string? Blank(string s) => s.Length > 0 ? s : null;

    private static int? Int(string s) => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : null;
}
