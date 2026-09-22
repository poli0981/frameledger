namespace FrameLedger.Application.Recording;

/// <summary>
/// The words <c>sessions.capture_notes</c> is made of (<c>ExitStatusMapper.Describe</c>, <c>SessionRecorder.Skeleton</c>):
/// <c>end=&lt;SessionEndReason&gt;; exit_code=…; tier2: attach=…; guard=&lt;reason&gt;/&lt;family&gt;/&lt;signal&gt;; …</c>.
/// Read back so a Tier-2 row can say WHY it measured nothing (<c>04_CAPTURE</c>: "the reason is the payload of Tier 2"),
/// which the schema keeps in this one text column rather than in fields (2026-09-22).
/// </summary>
public readonly record struct CaptureNotes(string? End, string? GuardReason, string? GuardFamily, string? GuardSignal)
{
    public static CaptureNotes Parse(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return default;
        }

        string? end = null, reason = null, family = null, signal = null;
        foreach (string raw in notes.Split(';'))
        {
            string part = raw.Trim();
            if (part.StartsWith("end=", StringComparison.Ordinal))
            {
                end = part[4..];
            }
            else if (part.StartsWith("guard=", StringComparison.Ordinal))
            {
                string[] words = part[6..].Split('/', 3);
                reason = words.Length > 0 && words[0].Length > 0 ? words[0] : null;
                family = words.Length > 1 && words[1].Length > 0 ? words[1] : null;
                signal = words.Length > 2 && words[2].Length > 0 ? words[2] : null;
            }
        }

        return new CaptureNotes(end, reason, family, signal);
    }
}
